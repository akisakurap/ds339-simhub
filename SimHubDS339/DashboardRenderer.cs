using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.Versioning;

namespace SimHubDS339
{
    /// <summary>
    /// 960x376 (DS339 を横置きで見た向き) のダッシュボードを GDI+ で描画し、RGB888 バイト列に変換する。
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal sealed class DashboardRenderer : IDisposable
    {
        public const int Width = 960;
        public const int Height = 376;

        private static readonly Color Background = Color.FromArgb(12, 14, 20);
        private static readonly Color Panel = Color.FromArgb(26, 30, 40);
        private static readonly Color Dim = Color.FromArgb(120, 128, 145);
        private static readonly Color TextColor = Color.FromArgb(235, 238, 245);
        private static readonly Color Accent = Color.FromArgb(40, 210, 150);
        private static readonly Color Warn = Color.FromArgb(250, 180, 40);
        private static readonly Color Danger = Color.FromArgb(235, 60, 60);
        private static readonly Color ThrottleColor = Color.FromArgb(60, 200, 90);
        private static readonly Color BrakeColor = Color.FromArgb(230, 60, 60);
        private static readonly Color Purple = Color.FromArgb(180, 110, 250);

        private readonly Bitmap _bitmap = new(Width, Height, PixelFormat.Format24bppRgb);
        private readonly Graphics _g;
        private readonly string _fontFamily;
        private readonly Dictionary<(float, FontStyle), Font> _fonts = new();
        private readonly byte[] _rgb = new byte[Width * Height * 3];

        public DashboardRenderer()
        {
            _g = Graphics.FromImage(_bitmap);
            _g.SmoothingMode = SmoothingMode.AntiAlias;
            _g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            using var installed = new InstalledFontCollection();
            var names = installed.Families.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            _fontFamily = new[] { "Bahnschrift", "Segoe UI" }.FirstOrDefault(names.Contains) ?? FontFamily.GenericSansSerif.Name;
        }

        public byte[] Render(Telemetry t, PcStats? pc = null)
        {
            _g.Clear(Background);

            if (t.SimHubConnected && t.GameRunning)
                DrawRace(t);
            else
                DrawPcStatus(t, pc);

            return ToRgb();
        }

        public void SavePng(string path) => _bitmap.Save(path, ImageFormat.Png);

        // ---- race layout ----

        private void DrawRace(Telemetry t)
        {
            double maxRpm = t.MaxRpm > 0 ? t.MaxRpm : 8000;
            double redline = t.RedlineRpm > 0 ? t.RedlineRpm : maxRpm * 0.92;
            double rpmRatio = Math.Clamp(t.Rpm / maxRpm, 0, 1);
            bool shift = t.Rpm >= redline;

            DrawShiftLights(rpmRatio, shift);

            // gear
            var gearRect = new RectangleF(16, 56, 250, 250);
            FillRounded(gearRect, shift ? Color.FromArgb(90, 20, 20) : Panel);
            DrawCentered(t.Gear, Font(170, FontStyle.Bold), shift ? Danger : TextColor, new RectangleF(gearRect.X, gearRect.Y + 6, gearRect.Width, gearRect.Height - 40));
            DrawCentered("GEAR", Font(18), Dim, new RectangleF(gearRect.X, gearRect.Bottom - 40, gearRect.Width, 30));

            // speed + rpm
            var mid = new RectangleF(282, 56, 380, 250);
            FillRounded(mid, Panel);
            DrawCentered(((int)Math.Round(t.SpeedKmh)).ToString(), Font(120, FontStyle.Bold), TextColor, new RectangleF(mid.X, mid.Y + 14, mid.Width, 140));
            DrawCentered("km/h", Font(20), Dim, new RectangleF(mid.X, mid.Y + 144, mid.Width, 30));

            var barRect = new RectangleF(mid.X + 20, mid.Y + 186, mid.Width - 40, 22);
            FillRounded(barRect, Color.FromArgb(45, 50, 64), 6);
            var fill = new RectangleF(barRect.X, barRect.Y, (float)(barRect.Width * rpmRatio), barRect.Height);
            if (fill.Width > 1) FillRounded(fill, shift ? Danger : rpmRatio > 0.8 ? Warn : Accent, 6);
            DrawCentered($"{(int)Math.Round(t.Rpm)} rpm", Font(18), Dim, new RectangleF(mid.X, mid.Y + 212, mid.Width, 30));

            // lap info
            var right = new RectangleF(678, 56, 266, 250);
            FillRounded(right, Panel);
            float y = right.Y + 14;
            string lap = t.CurrentLap.HasValue ? (t.TotalLaps is > 0 ? $"{t.CurrentLap}/{t.TotalLaps}" : $"{t.CurrentLap}") : "-";
            string pos = t.Position is > 0 ? (t.OpponentsCount is > 0 ? $"P{t.Position}/{t.OpponentsCount}" : $"P{t.Position}") : "-";
            DrawLabelValue("LAP", lap, right.X + 16, y, 110);
            DrawLabelValue("POS", pos, right.X + 136, y, 120);
            y += 70;
            DrawTimeRow("CURRENT", t.CurrentLapTime, TextColor, right.X + 16, y); y += 52;
            DrawTimeRow("LAST", t.LastLapTime, TextColor, right.X + 16, y); y += 52;
            DrawTimeRow("BEST", t.BestLapTime, Purple, right.X + 16, y);

            // pedals + fuel
            float by = 318;
            DrawPedal("THR", t.Throttle, ThrottleColor, new RectangleF(16, by, 330, 42));
            DrawPedal("BRK", t.Brake, BrakeColor, new RectangleF(362, by, 330, 42));
            var fuelRect = new RectangleF(708, by, 236, 42);
            FillRounded(fuelRect, Panel, 8);
            string fuel = t.FuelPercent.HasValue ? $"{t.FuelPercent.Value:0}%" : "-";
            var fuelColor = t.FuelPercent is < 10 ? Danger : TextColor;
            DrawText("FUEL", Font(16), Dim, fuelRect.X + 14, fuelRect.Y + 11);
            DrawRight(fuel, Font(24, FontStyle.Bold), fuelColor, new RectangleF(fuelRect.X, fuelRect.Y + 4, fuelRect.Width - 14, 36));
        }

        private void DrawShiftLights(double ratio, bool shift)
        {
            const int count = 20;
            const float margin = 16, gap = 6, top = 12, h = 30;
            float w = (Width - margin * 2 - gap * (count - 1)) / count;
            // 60% から点灯開始し 100% で全点灯
            double lit = shift ? count : Math.Clamp((ratio - 0.6) / 0.4, 0, 1) * count;
            bool blinkOff = shift && DateTime.Now.Millisecond % 250 < 125;

            for (int i = 0; i < count; i++)
            {
                var rect = new RectangleF(margin + i * (w + gap), top, w, h);
                Color on = i < count * 0.5 ? ThrottleColor : i < count * 0.8 ? Warn : Danger;
                if (shift) on = Danger;
                bool isOn = i < lit && !blinkOff;
                FillRounded(rect, isOn ? on : Color.FromArgb(34, 38, 50), 6);
            }
        }

        private void DrawPedal(string label, double value, Color color, RectangleF rect)
        {
            FillRounded(rect, Panel, 8);
            DrawText(label, Font(16), Dim, rect.X + 12, rect.Y + 11);
            var bar = new RectangleF(rect.X + 66, rect.Y + 12, rect.Width - 80, rect.Height - 24);
            FillRounded(bar, Color.FromArgb(45, 50, 64), 5);
            var fill = new RectangleF(bar.X, bar.Y, (float)(bar.Width * value), bar.Height);
            if (fill.Width > 1) FillRounded(fill, color, 5);
        }

        private void DrawLabelValue(string label, string value, float x, float y, float width)
        {
            DrawText(label, Font(15), Dim, x, y);
            DrawFit(value, 32, FontStyle.Bold, TextColor, new RectangleF(x - 4, y + 18, width, 44));
        }

        private void DrawTimeRow(string label, TimeSpan? time, Color color, float x, float y)
        {
            DrawText(label, Font(15), Dim, x, y + 12);
            DrawRight(FormatLap(time), Font(28, FontStyle.Bold), color, new RectangleF(x, y, 234, 44));
        }

        private static string FormatLap(TimeSpan? t)
        {
            if (t is not { } v || v <= TimeSpan.Zero) return "-:--.---";
            return v.TotalHours >= 1 ? v.ToString(@"h\:mm\:ss") : $"{(int)v.TotalMinutes}:{v.Seconds:00}.{v.Milliseconds:000}";
        }

        // ---- PC status layout (ゲーム未起動時) ----

        private void DrawPcStatus(Telemetry t, PcStats? pc)
        {
            var now = DateTime.Now;
            DrawText(now.ToString("HH:mm"), Font(46, FontStyle.Bold), TextColor, 12, 6);
            DrawText(now.ToString("yyyy/MM/dd (ddd)", System.Globalization.CultureInfo.InvariantCulture), Font(20), Dim, 156, 26);

            string status;
            Color statusColor;
            if (!t.SimHubConnected)
            {
                status = "SimHub: not connected";
                statusColor = Warn;
            }
            else
            {
                status = string.IsNullOrEmpty(t.GameName) ? "SimHub: waiting for game" : $"SimHub: waiting for {t.GameName}";
                statusColor = Accent;
            }
            var pill = new RectangleF(584, 14, 360, 42);
            FillRounded(pill, Panel, 21);
            DrawCentered(status, Font(19), statusColor, pill);

            const float top = 70, h = 238, w = 300, gap = 14;
            float x = 16;

            string? clock = pc?.CpuClockMhz is { } mhz ? $"{mhz / 1000:0.0} GHz" : null;
            DrawLoadPanel(new RectangleF(x, top, w, h), "CPU", null, pc?.CpuLoad, pc?.CpuTemp, clock, pc?.CpuHistory);
            x += w + gap;

            string? vram = pc?.VramUsedMb is { } vu && pc.VramTotalMb is { } vt && vt > 0 ? $"VRAM {vu / 1024:0.0}/{vt / 1024:0} GB" : null;
            DrawLoadPanel(new RectangleF(x, top, w, h), "GPU", pc?.GpuName, pc?.GpuLoad, pc?.GpuTemp, vram, pc?.GpuHistory);
            x += w + gap;

            double? ramLoad = pc?.RamUsedGb is { } ru && pc.RamTotalGb is { } rt && rt > 0 ? ru / rt * 100 : null;
            string? ramText = pc?.RamUsedGb is { } used && pc.RamTotalGb is { } total ? $"{used:0.0}/{total:0} GB" : null;
            DrawLoadPanel(new RectangleF(x, top, w, h), "MEMORY", null, ramLoad, null, ramText, pc?.RamHistory);

            // network
            var net = new RectangleF(16, 320, 600, 42);
            FillRounded(net, Panel, 8);
            DrawText("NET", Font(16), Dim, net.X + 14, net.Y + 11);
            DrawText("DOWN", Font(14), Dim, net.X + 90, net.Y + 13);
            DrawText(FormatRate(pc?.NetDownBps), Font(22, FontStyle.Bold), TextColor, net.X + 138, net.Y + 7);
            DrawText("UP", Font(14), Dim, net.X + 330, net.Y + 13);
            DrawText(FormatRate(pc?.NetUpBps), Font(22, FontStyle.Bold), TextColor, net.X + 360, net.Y + 7);

            if (pc != null && pc.CpuTemp == null)
            {
                var hint = new RectangleF(632, 320, 312, 42);
                string text = PcStatsSampler.IsElevated ? "CPU temp: install PawnIO driver" : "CPU temp: admin + PawnIO required";
                DrawCentered(text, Font(16), Dim, hint);
            }
        }

        private void DrawLoadPanel(RectangleF r, string label, string? subLabel, double? load, double? temp, string? detail, IReadOnlyList<double>? history)
        {
            FillRounded(r, Panel);
            DrawText(label, Font(18), Dim, r.X + 16, r.Y + 12);
            if (subLabel != null)
                DrawRight(subLabel, Font(14), Dim, new RectangleF(r.X + 90, r.Y + 12, r.Width - 106, 22));

            // 使用率 (大) と温度 / 詳細
            string loadText = load.HasValue ? $"{load.Value:0}" : "-";
            var big = Font(72, FontStyle.Bold);
            DrawText(loadText, big, TextColor, r.X + 8, r.Y + 34);
            // GenericTypographic で測ると DrawString の左右余白を含まない実幅になる
            float numW = _g.MeasureString(loadText, big, PointF.Empty, StringFormat.GenericTypographic).Width;
            DrawText("%", Font(26, FontStyle.Bold), Dim, r.X + 22 + numW, r.Y + 76);

            if (temp.HasValue || label != "MEMORY")
            {
                var tempColor = temp is >= 85 ? Danger : temp is >= 70 ? Warn : TextColor;
                DrawRight(temp.HasValue ? $"{temp.Value:0}°C" : "-°C", Font(30, FontStyle.Bold), tempColor, new RectangleF(r.X, r.Y + 44, r.Width - 16, 40));
                DrawRight(detail ?? "-", Font(17), Dim, new RectangleF(r.X, r.Y + 86, r.Width - 16, 26));
            }
            else
            {
                DrawRight(detail ?? "-", Font(24, FontStyle.Bold), TextColor, new RectangleF(r.X, r.Y + 50, r.Width - 16, 40));
            }

            // 直近 60 秒の推移
            var spark = new RectangleF(r.X + 16, r.Y + 128, r.Width - 32, 62);
            DrawSparkline(spark, history);

            // 使用率バー
            var bar = new RectangleF(r.X + 16, r.Bottom - 34, r.Width - 32, 16);
            FillRounded(bar, Color.FromArgb(45, 50, 64), 5);
            double ratio = Math.Clamp((load ?? 0) / 100.0, 0, 1);
            var fill = new RectangleF(bar.X, bar.Y, (float)(bar.Width * ratio), bar.Height);
            if (fill.Width > 1) FillRounded(fill, LoadColor(load ?? 0), 5);
        }

        private void DrawSparkline(RectangleF r, IReadOnlyList<double>? history)
        {
            using (var grid = new Pen(Color.FromArgb(40, 45, 58), 1))
            {
                _g.DrawLine(grid, r.X, r.Y, r.Right, r.Y);
                _g.DrawLine(grid, r.X, r.Y + r.Height / 2, r.Right, r.Y + r.Height / 2);
                _g.DrawLine(grid, r.X, r.Bottom, r.Right, r.Bottom);
            }
            if (history == null || history.Count < 2) return;

            // 右端を最新として、1 サンプル = 幅 / (HistoryLength - 1)
            float step = r.Width / (PcStats.HistoryLength - 1);
            var points = new PointF[history.Count];
            for (int i = 0; i < history.Count; i++)
            {
                float px = r.Right - (history.Count - 1 - i) * step;
                float py = r.Bottom - (float)(Math.Clamp(history[i], 0, 100) / 100.0) * r.Height;
                points[i] = new PointF(px, py);
            }

            using var area = new GraphicsPath();
            area.AddLines(points);
            area.AddLine(points[^1], new PointF(points[^1].X, r.Bottom));
            area.AddLine(new PointF(points[^1].X, r.Bottom), new PointF(points[0].X, r.Bottom));
            area.CloseFigure();
            using (var brush = new SolidBrush(Color.FromArgb(50, Accent)))
                _g.FillPath(brush, area);
            using var pen = new Pen(Accent, 2) { LineJoin = LineJoin.Round };
            _g.DrawLines(pen, points);
        }

        private static Color LoadColor(double load) => load >= 95 ? Danger : load >= 80 ? Warn : Accent;

        private static string FormatRate(double? bps)
        {
            if (bps is not { } v) return "-";
            if (v >= 1024 * 1024) return $"{v / (1024 * 1024):0.0} MB/s";
            if (v >= 1024) return $"{v / 1024:0} KB/s";
            return $"{v:0} B/s";
        }

        // ---- helpers ----

        private Font Font(float size, FontStyle style = FontStyle.Regular)
        {
            if (!_fonts.TryGetValue((size, style), out var f))
            {
                f = new Font(_fontFamily, size, style, GraphicsUnit.Pixel);
                _fonts[(size, style)] = f;
            }
            return f;
        }

        private void FillRounded(RectangleF r, Color color, float radius = 14)
        {
            radius = Math.Min(radius, Math.Min(r.Width, r.Height) / 2);
            using var path = new GraphicsPath();
            float d = radius * 2;
            if (d <= 0)
            {
                path.AddRectangle(r);
            }
            else
            {
                path.AddArc(r.X, r.Y, d, d, 180, 90);
                path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
            }
            using var brush = new SolidBrush(color);
            _g.FillPath(brush, path);
        }

        private void DrawText(string text, Font font, Color color, float x, float y)
        {
            using var brush = new SolidBrush(color);
            _g.DrawString(text, font, brush, x, y);
        }

        private void DrawCentered(string text, Font font, Color color, RectangleF rect)
        {
            using var brush = new SolidBrush(color);
            using var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
            _g.DrawString(text, font, brush, rect, fmt);
        }

        private void DrawRight(string text, Font font, Color color, RectangleF rect)
        {
            using var brush = new SolidBrush(color);
            using var fmt = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
            _g.DrawString(text, font, brush, rect, fmt);
        }

        /// <summary>幅に収まるまでフォントサイズを縮めて左寄せで描く。</summary>
        private void DrawFit(string text, float size, FontStyle style, Color color, RectangleF rect)
        {
            var font = Font(size, style);
            while (size > 12 && _g.MeasureString(text, font).Width > rect.Width)
            {
                size -= 2;
                font = Font(size, style);
            }
            using var brush = new SolidBrush(color);
            using var fmt = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
            _g.DrawString(text, font, brush, rect, fmt);
        }

        private unsafe byte[] ToRgb()
        {
            var data = _bitmap.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            try
            {
                byte* scan0 = (byte*)data.Scan0;
                int stride = data.Stride;
                fixed (byte* dst = _rgb)
                {
                    for (int y = 0; y < Height; y++)
                    {
                        byte* src = scan0 + y * stride;
                        byte* d = dst + y * Width * 3;
                        for (int x = 0; x < Width; x++)
                        {
                            // GDI+ の 24bpp はメモリ上 B,G,R 順
                            d[0] = src[2];
                            d[1] = src[1];
                            d[2] = src[0];
                            src += 3;
                            d += 3;
                        }
                    }
                }
            }
            finally
            {
                _bitmap.UnlockBits(data);
            }
            return _rgb;
        }

        public void Dispose()
        {
            foreach (var f in _fonts.Values) f.Dispose();
            _g.Dispose();
            _bitmap.Dispose();
        }
    }
}
