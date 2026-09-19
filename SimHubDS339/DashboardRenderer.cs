using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Linq;
using System.Runtime.Versioning;

namespace SimHubDS339
{
    /// <summary>
    /// 960x376 (DS339 を横置きで見た向き) のダッシュボードを GDI+ で描画し、RGB888 バイト列に変換する。
    /// レース中は MAIN, TYRES, FUEL, DELTA, SESSION の各ページを描画可能。
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
        private static readonly Color ColdColor = Color.FromArgb(60, 140, 230);

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

        /// <summary>
        /// 後方互換性用の描画メソッド (MAIN ページを描画)。
        /// </summary>
        public byte[] Render(Telemetry t, PcStats? pc = null)
        {
            return Render(t, pc, RacePage.Main, null, null);
        }

        /// <summary>
        /// 指定されたページおよびオーバーレイで画面を描画し、RGB888 バイト列を返す。
        /// </summary>
        /// <param name="t">テレメトリスナップショット</param>
        /// <param name="pc">PC ステータススナップショット</param>
        /// <param name="page">描画対象のレースページ</param>
        /// <param name="overlay">一時表示する切替オーバーレイ</param>
        /// <param name="enabledPages">現在有効なページのリスト (インジケータ用)</param>
        public byte[] Render(Telemetry t, PcStats? pc, RacePage page, PageOverlay? overlay, IReadOnlyList<RacePage>? enabledPages = null)
        {
            _g.Clear(Background);

            if (t.SimHubConnected && t.GameRunning)
            {
                switch (page)
                {
                    case RacePage.Tyres:
                        DrawTyres(t);
                        break;
                    case RacePage.Fuel:
                        DrawFuel(t);
                        break;
                    case RacePage.Delta:
                        DrawDelta(t);
                        break;
                    case RacePage.Session:
                        DrawSession(t);
                        break;
                    case RacePage.Main:
                    default:
                        DrawRace(t);
                        break;
                }

                // 共通ページインジケータ (レース画面時のみ最下部に描画)
                DrawPageIndicator(page, enabledPages);

                // ページ切替オーバーレイ (1.5 秒間表示)
                if (overlay != null && overlay.IsActive)
                {
                    DrawOverlay(overlay);
                }
            }
            else
            {
                DrawPcStatus(t, pc);
            }

            return ToRgb();
        }

        public void SavePng(string path) => _bitmap.Save(path, ImageFormat.Png);

        // ---- 共通パーツ ----

        /// <summary>全追加ページ共通のヘッダー行 (y=8〜44)</summary>
        private void DrawHeader(string title, Telemetry t)
        {
            // 左: ページ名
            DrawText(title, Font(18, FontStyle.Bold), Dim, 18, 12);

            // 中央: P3/16  LAP 5/12
            string pos = t.Position is > 0 ? (t.OpponentsCount is > 0 ? $"P{t.Position}/{t.OpponentsCount}" : $"P{t.Position}") : "-";
            string lap = t.CurrentLap.HasValue ? (t.TotalLaps is > 0 ? $"LAP {t.CurrentLap}/{t.TotalLaps}" : $"LAP {t.CurrentLap}") : "-";
            DrawCentered($"{pos}    {lap}", Font(18, FontStyle.Bold), TextColor, new RectangleF(260, 8, 440, 32));

            // 右: 現在ラップタイム
            DrawRight(FormatLap(t.CurrentLapTime), Font(20, FontStyle.Bold), TextColor, new RectangleF(700, 8, 244, 32));

            // 薄い境界線
            using var pen = new Pen(Color.FromArgb(32, 38, 52), 1);
            _g.DrawLine(pen, 16, 46, Width - 16, 46);
        }

        /// <summary>最下部の小さなページインジケータ (MAIN 画面にも被らない位置)</summary>
        private void DrawPageIndicator(RacePage current, IReadOnlyList<RacePage>? enabled)
        {
            var list = enabled ?? Enum.GetValues(typeof(RacePage)).Cast<RacePage>().ToList();
            if (list.Count <= 1) return;

            const float dotSize = 5f;
            const float spacing = 11f;
            float totalW = list.Count * dotSize + (list.Count - 1) * (spacing - dotSize);
            float startX = (Width - totalW) / 2f;
            float y = 368f;

            for (int i = 0; i < list.Count; i++)
            {
                float x = startX + i * spacing;
                bool isCurrent = list[i] == current;
                var color = isCurrent ? Accent : Color.FromArgb(55, 62, 78);
                using var brush = new SolidBrush(color);
                _g.FillEllipse(brush, x, y, dotSize, dotSize);
            }
        }

        /// <summary>画面上部中央のページ名オーバーレイ (下のヘッダ文字が透けないよう不透明で塗る)</summary>
        private void DrawOverlay(PageOverlay overlay)
        {
            var rect = new RectangleF(330, 2, 300, 50);
            FillRounded(rect, Panel, 10);
            DrawRoundedBorder(rect, Accent, 10, 1.5f);
            DrawCentered(overlay.Title, Font(22, FontStyle.Bold), TextColor, rect);
        }

        // ---- 1. MAIN (既存のレース画面、見た目・内容不変) ----

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

        // ---- 2. TYRES (タイヤとブレーキ) ----

        private void DrawTyres(Telemetry t)
        {
            DrawHeader("TYRES & BRAKES", t);

            // 中央の簡易車両シルエット
            var carRect = new RectangleF(464, 58, 32, 298);
            FillRounded(carRect, Color.FromArgb(18, 22, 30), 8);
            using (var pen = new Pen(Color.FromArgb(40, 48, 64), 1.5f))
            {
                _g.DrawLine(pen, carRect.X + carRect.Width / 2, carRect.Y + 14, carRect.X + carRect.Width / 2, carRect.Bottom - 14);
            }

            // 4輪パネル (FL, FR / RL, RR)
            DrawWheelPanel(new RectangleF(16, 56, 434, 144), "FRONT LEFT", t.FrontLeft, isLeft: true, t.TemperatureUnit, t.PressureUnit, t.FrontCompound);
            DrawWheelPanel(new RectangleF(510, 56, 434, 144), "FRONT RIGHT", t.FrontRight, isLeft: false, t.TemperatureUnit, t.PressureUnit, t.FrontCompound);
            DrawWheelPanel(new RectangleF(16, 210, 434, 144), "REAR LEFT", t.RearLeft, isLeft: true, t.TemperatureUnit, t.PressureUnit, t.RearCompound);
            DrawWheelPanel(new RectangleF(510, 210, 434, 144), "REAR RIGHT", t.RearRight, isLeft: false, t.TemperatureUnit, t.PressureUnit, t.RearCompound);
        }

        private void DrawWheelPanel(RectangleF r, string title, TyreWheelData w, bool isLeft, string tempUnit, string pressUnit, string? compound)
        {
            FillRounded(r, Panel, 10);

            // ホイール名とコンパウンド
            DrawText(title, Font(13, FontStyle.Bold), Dim, r.X + 14, r.Y + 10);
            if (!string.IsNullOrEmpty(compound))
            {
                DrawRight(compound, Font(12, FontStyle.Bold), Accent, new RectangleF(r.X + 120, r.Y + 10, r.Width - 134, 16));
            }

            // 平均温度 (大)
            string avgTempStr = w.Temp.HasValue ? $"{Math.Round(w.Temp.Value):0}°" : "-°";
            Color avgTempColor = GetTyreTempColor(w.Temp, tempUnit);
            DrawText(avgTempStr, Font(42, FontStyle.Bold), avgTempColor, r.X + 12, r.Y + 30);

            // 3 分割温度バー (内・中・外)
            // 左輪: 外側が左 (Outer, Middle, Inner)
            // 右輪: 外側が右 (Inner, Middle, Outer)
            double? t1 = isLeft ? w.TempOuter : w.TempInner;
            double? t2 = w.TempMiddle;
            double? t3 = isLeft ? w.TempInner : w.TempOuter;
            string l1 = isLeft ? "O" : "I";
            string l2 = "M";
            string l3 = isLeft ? "I" : "O";

            float barX = r.X + 136;
            float barY = r.Y + 36;
            DrawTempBar(barX, barY, 34, 28, t1, l1, tempUnit);
            DrawTempBar(barX + 40, barY, 34, 28, t2, l2, tempUnit);
            DrawTempBar(barX + 80, barY, 34, 28, t3, l3, tempUnit);

            // 空気圧
            string pressStr = w.Pressure.HasValue ? $"{w.Pressure.Value:0.0}" : "-";
            DrawText("PRES", Font(11), Dim, r.X + 276, r.Y + 32);
            DrawFit($"{pressStr} {pressUnit}", 20, FontStyle.Bold, TextColor, new RectangleF(r.X + 276, r.Y + 46, r.Width - 286, 30));

            // 下部: 摩耗率 & ブレーキ温度
            float by = r.Y + 98;
            string wearStr = w.Wear.HasValue ? $"{w.Wear.Value:0}%" : "-";
            string lastWearStr = w.LastLapWear.HasValue ? $" (-{w.LastLapWear.Value:0.0}%)" : "";
            DrawText("WEAR", Font(12), Dim, r.X + 14, by);
            DrawText($"{wearStr}{lastWearStr}", Font(18, FontStyle.Bold), TextColor, r.X + 60, by - 2);

            // ブレーキ
            string brkStr = w.BrakeTemp.HasValue ? $"{Math.Round(w.BrakeTemp.Value):0}°" : "-°";
            Color brkColor = GetBrakeTempColor(w.BrakeTemp);
            DrawRight($"BRAKE {brkStr}", Font(16, FontStyle.Bold), brkColor, new RectangleF(r.X + 200, by, r.Width - 214, 26));
        }

        private Color GetTyreTempColor(double? temp, string unit)
        {
            if (temp is not { } t) return Dim;
            double c = unit.Equals("F", StringComparison.OrdinalIgnoreCase) ? (t - 32) * 5.0 / 9.0 : t;
            if (c < 65) return ColdColor;
            if (c < 85) return Accent;
            if (c < 105) return Warn;
            return Danger;
        }

        private Color GetBrakeTempColor(double? temp)
        {
            if (temp is not { } t) return Dim;
            if (t >= 800) return Danger;
            if (t >= 600) return Warn;
            return TextColor;
        }

        private void DrawTempBar(float x, float y, float w, float h, double? temp, string label, string unit)
        {
            var rect = new RectangleF(x, y, w, h);
            Color c = GetTyreTempColor(temp, unit);
            FillRounded(rect, temp.HasValue ? c : Color.FromArgb(40, 46, 60), 4);
            DrawCentered(label, Font(11, FontStyle.Bold), Color.FromArgb(20, 20, 24), rect);

            string valStr = temp.HasValue ? $"{Math.Round(temp.Value):0}" : "-";
            DrawCentered(valStr, Font(11), Dim, new RectangleF(x, y + h + 2, w, 16));
        }

        // ---- 3. FUEL (燃料・スティント) ----

        private void DrawFuel(Telemetry t)
        {
            DrawHeader("FUEL & STINT", t);

            float py = 56, ph = 300;
            var leftRect = new RectangleF(16, py, 296, ph);
            var midRect = new RectangleF(326, py, 308, ph);
            var rightRect = new RectangleF(648, py, 296, ph);

            FillRounded(leftRect, Panel, 10);
            FillRounded(midRect, Panel, 10);
            FillRounded(rightRect, Panel, 10);

            // --- 左: 残燃料 ---
            DrawText("REMAINING FUEL", Font(14, FontStyle.Bold), Dim, leftRect.X + 16, leftRect.Y + 12);
            string fuelStr = t.Fuel.HasValue ? $"{t.Fuel.Value:0.0}" : (t.FuelPercent.HasValue ? $"{t.FuelPercent.Value:0}%" : "-");
            DrawText(fuelStr, Font(54, FontStyle.Bold), (t.FuelPercent is < 10) ? Danger : TextColor, leftRect.X + 14, leftRect.Y + 36);
            DrawText(t.FuelUnit, Font(20, FontStyle.Bold), Dim, leftRect.X + 220, leftRect.Y + 62);

            // 燃料バー
            var fuelBar = new RectangleF(leftRect.X + 16, leftRect.Y + 116, leftRect.Width - 32, 16);
            FillRounded(fuelBar, Color.FromArgb(40, 46, 60), 5);
            double ratio = t.MaxFuel is > 0 && t.Fuel.HasValue ? Math.Clamp(t.Fuel.Value / t.MaxFuel.Value, 0, 1)
                          : (t.FuelPercent.HasValue ? Math.Clamp(t.FuelPercent.Value / 100.0, 0, 1) : 0);
            var fill = new RectangleF(fuelBar.X, fuelBar.Y, (float)(fuelBar.Width * ratio), fuelBar.Height);
            if (fill.Width > 1) FillRounded(fill, ratio < 0.1 ? Danger : Accent, 5);

            // 残り周回
            float ly = leftRect.Y + 154;
            DrawText("EST. LAPS LEFT", Font(13), Dim, leftRect.X + 16, ly);
            string lapsLeftStr = t.FuelRemainingLaps.HasValue ? $"{t.FuelRemainingLaps.Value:0.0}" : (t.RemainingLaps.HasValue ? $"{t.RemainingLaps.Value}" : "-");
            DrawText(lapsLeftStr, Font(34, FontStyle.Bold), TextColor, leftRect.X + 16, ly + 20);

            // 残り時間
            float ty = ly + 74;
            DrawText("EST. TIME LEFT", Font(13), Dim, leftRect.X + 16, ty);
            string timeLeftStr = t.FuelRemainingTime.HasValue ? FormatDuration(t.FuelRemainingTime.Value) : (t.SessionTimeLeft.HasValue ? FormatDuration(t.SessionTimeLeft.Value) : "-");
            DrawText(timeLeftStr, Font(28, FontStyle.Bold), TextColor, leftRect.X + 16, ty + 20);

            // --- 中: 消費 & スティント ---
            DrawText("CONSUMPTION & STINT", Font(14, FontStyle.Bold), Dim, midRect.X + 16, midRect.Y + 12);

            // 1周消費
            DrawText("PER LAP", Font(13), Dim, midRect.X + 16, midRect.Y + 40);
            string perLapStr = t.FuelPerLap.HasValue ? $"{t.FuelPerLap.Value:0.00} {t.FuelUnit}" : "-";
            DrawRight(perLapStr, Font(24, FontStyle.Bold), TextColor, new RectangleF(midRect.X + 90, midRect.Y + 34, midRect.Width - 106, 32));

            // スティント周回 & 時間
            float sy = midRect.Y + 82;
            int stLaps = t.Stint?.Laps ?? 0;
            string stTime = t.Stint != null ? FormatDuration(t.Stint.Duration) : "0:00";
            DrawText("STINT", Font(13), Dim, midRect.X + 16, sy);
            DrawRight($"{stLaps} laps  ({stTime})", Font(20, FontStyle.Bold), TextColor, new RectangleF(midRect.X + 70, sy - 4, midRect.Width - 86, 28));

            // スティント消費
            sy += 38;
            string stConsumed = t.Stint?.FuelConsumed.HasValue == true ? $"{t.Stint.FuelConsumed.Value:0.0} {t.FuelUnit}" : "-";
            DrawText("STINT USED", Font(13), Dim, midRect.X + 16, sy);
            DrawRight(stConsumed, Font(20, FontStyle.Bold), TextColor, new RectangleF(midRect.X + 90, sy - 4, midRect.Width - 106, 28));

            // スティント平均燃費
            sy += 38;
            string stAvg = t.Stint?.FuelPerLap.HasValue == true ? $"{t.Stint.FuelPerLap.Value:0.00} {t.FuelUnit}/lap" : "-";
            DrawText("STINT AVG", Font(13), Dim, midRect.X + 16, sy);
            DrawRight(stAvg, Font(20, FontStyle.Bold), TextColor, new RectangleF(midRect.X + 90, sy - 4, midRect.Width - 106, 28));

            // ENERGY (ハイブリッド / Virtual Energy がある場合のみ)
            if (t.EnergyPercent.HasValue)
            {
                float ey = sy + 40;
                DrawText("ENERGY", Font(13, FontStyle.Bold), Accent, midRect.X + 16, ey);
                DrawRight($"{t.EnergyPercent.Value:0}%", Font(20, FontStyle.Bold), Accent, new RectangleF(midRect.X + 90, ey - 4, midRect.Width - 106, 28));

                var eBar = new RectangleF(midRect.X + 16, ey + 24, midRect.Width - 32, 10);
                FillRounded(eBar, Color.FromArgb(40, 46, 60), 4);
                float eRatio = (float)Math.Clamp(t.EnergyPercent.Value / 100.0, 0, 1);
                var eFill = new RectangleF(eBar.X, eBar.Y, eBar.Width * eRatio, eBar.Height);
                if (eFill.Width > 1) FillRounded(eFill, Accent, 4);
            }

            // --- 右: 完走計算 & ピット ---
            DrawText("FINISH REQUIREMENT", Font(14, FontStyle.Bold), Dim, rightRect.X + 16, rightRect.Y + 12);

            // 完走計算
            double? fuelDiff = CalculateFinishFuel(t);
            float fy = rightRect.Y + 44;
            if (fuelDiff.HasValue)
            {
                if (fuelDiff.Value > 0)
                {
                    DrawFit($"+{fuelDiff.Value:0.0} {t.FuelUnit} NEEDED", 26, FontStyle.Bold, Warn, new RectangleF(rightRect.X + 16, fy, rightRect.Width - 32, 40));
                }
                else
                {
                    DrawFit($"SURPLUS {-fuelDiff.Value:0.0} {t.FuelUnit}", 26, FontStyle.Bold, Accent, new RectangleF(rightRect.X + 16, fy, rightRect.Width - 32, 40));
                }
            }
            else
            {
                DrawText("-", Font(32, FontStyle.Bold), Dim, rightRect.X + 16, fy);
            }

            // 最終ピットストップ
            float py2 = fy + 78;
            DrawText("LAST PIT STOP", Font(13), Dim, rightRect.X + 16, py2);
            string lastPit = t.LastPitStopDuration.HasValue && t.LastPitStopDuration.Value > 0
                ? $"{t.LastPitStopDuration.Value:0.0} s" : "-";
            DrawRight(lastPit, Font(24, FontStyle.Bold), TextColor, new RectangleF(rightRect.X + 90, py2 - 4, rightRect.Width - 106, 32));

            // ピット状態
            py2 += 54;
            DrawText("PIT STATUS", Font(13), Dim, rightRect.X + 16, py2);
            string pitStatus = t.IsInPitLane ? "IN PIT LANE" : (t.IsInPit ? "IN PIT" : "ON TRACK");
            Color pitColor = t.IsInPitLane || t.IsInPit ? Warn : Accent;
            DrawRight(pitStatus, Font(20, FontStyle.Bold), pitColor, new RectangleF(rightRect.X + 90, py2 - 4, rightRect.Width - 106, 28));
        }

        private double? CalculateFinishFuel(Telemetry t)
        {
            if (t.Fuel is not { } currentFuel || t.FuelPerLap is not { } perLap || perLap <= 0)
            {
                return null;
            }

            // 周回制レース
            if (t.TotalLaps is > 0 && t.CompletedLaps.HasValue)
            {
                int remaining = t.TotalLaps.Value - t.CompletedLaps.Value;
                if (remaining <= 0) return 0;
                return (remaining * perLap) - currentFuel;
            }

            // 時間制レース
            if (t.SessionTimeLeft.HasValue && t.SessionTimeLeft.Value > TimeSpan.Zero)
            {
                TimeSpan avgLap = t.LastLapTime ?? t.BestLapTime ?? TimeSpan.Zero;
                if (avgLap > TimeSpan.Zero)
                {
                    double laps = Math.Ceiling(t.SessionTimeLeft.Value.TotalSeconds / avgLap.TotalSeconds);
                    return (laps * perLap) - currentFuel;
                }
            }

            // SimHub の RemainingLaps からのフォールバック
            if (t.RemainingLaps.HasValue && t.RemainingLaps.Value > 0)
            {
                return (t.RemainingLaps.Value * perLap) - currentFuel;
            }

            return null;
        }

        // ---- 4. DELTA (デルタ・ラップ・ギャップ) ----

        private void DrawDelta(Telemetry t)
        {
            DrawHeader("DELTA & LAPS", t);

            float leftW = 580;

            // 1. ライブデルタ上段
            var deltaRect = new RectangleF(16, 56, leftW, 104);
            FillRounded(deltaRect, Panel, 10);

            double delta = t.LiveDeltaSeconds ?? 0;
            string deltaText = t.LiveDeltaSeconds.HasValue
                ? (delta > 0 ? $"+{delta:0.000}" : $"{delta:0.000}")
                : "-.---";
            Color deltaColor = t.LiveDeltaSeconds.HasValue
                ? (delta < 0 ? Accent : Danger)
                : TextColor;

            DrawCentered(deltaText, Font(48, FontStyle.Bold), deltaColor, new RectangleF(deltaRect.X, deltaRect.Y + 8, deltaRect.Width, 54));

            // ±2 秒スケール横バー
            var barBg = new RectangleF(deltaRect.X + 40, deltaRect.Y + 68, deltaRect.Width - 80, 14);
            FillRounded(barBg, Color.FromArgb(40, 46, 60), 5);

            float xMid = barBg.X + barBg.Width / 2f;
            using (var centerPen = new Pen(Dim, 1))
            {
                _g.DrawLine(centerPen, xMid, barBg.Y - 2, xMid, barBg.Bottom + 2);
            }

            if (t.LiveDeltaSeconds.HasValue)
            {
                double clamped = Math.Clamp(delta, -2.0, 2.0);
                float px = (float)(clamped / 2.0) * (barBg.Width / 2f);
                if (px < 0)
                {
                    var fill = new RectangleF(xMid + px, barBg.Y, -px, barBg.Height);
                    if (fill.Width > 1) FillRounded(fill, Accent, 4);
                }
                else if (px > 0)
                {
                    var fill = new RectangleF(xMid, barBg.Y, px, barBg.Height);
                    if (fill.Width > 1) FillRounded(fill, Danger, 4);
                }
            }

            // 2. 中段: ラップタイム行
            var lapRect = new RectangleF(16, 168, leftW, 92);
            FillRounded(lapRect, Panel, 10);
            float colW = lapRect.Width / 4f;

            DrawLapCol(lapRect.X, lapRect.Y, colW, "CURRENT", t.CurrentLapTime, TextColor);
            bool isLastBest = t.LastLapTime.HasValue && t.BestLapTime.HasValue && Math.Abs((t.LastLapTime.Value - t.BestLapTime.Value).TotalMilliseconds) < 1;
            DrawLapCol(lapRect.X + colW, lapRect.Y, colW, "LAST", t.LastLapTime, isLastBest ? Purple : TextColor);
            DrawLapCol(lapRect.X + colW * 2, lapRect.Y, colW, "BEST", t.BestLapTime, Purple);
            DrawLapCol(lapRect.X + colW * 3, lapRect.Y, colW, "ALL TIME", t.AllTimeBest, TextColor);

            // 3. 下段: セクター
            var secRect = new RectangleF(16, 268, leftW, 88);
            FillRounded(secRect, Panel, 10);
            float sColW = secRect.Width / 3f;

            DrawSectorCol(secRect.X, secRect.Y, sColW, 1, t.Sectors.S1, t.Sectors.BestS1, t.Sectors.CurrentSector == 1);
            DrawSectorCol(secRect.X + sColW, secRect.Y, sColW, 2, t.Sectors.S2, t.Sectors.BestS2, t.Sectors.CurrentSector == 2);
            DrawSectorCol(secRect.X + sColW * 2, secRect.Y, sColW, 3, t.Sectors.S3, t.Sectors.BestS3, t.Sectors.CurrentSector == 3);

            // 右エリア: 順位 & ギャップ & ラップ有効性
            var rightRect = new RectangleF(608, 56, 336, 300);
            FillRounded(rightRect, Panel, 10);

            // 順位
            string posStr = t.Position is > 0 ? (t.OpponentsCount is > 0 ? $"P{t.Position} / {t.OpponentsCount}" : $"P{t.Position}") : "-";
            DrawText("POSITION", Font(13, FontStyle.Bold), Dim, rightRect.X + 16, rightRect.Y + 14);
            DrawText(posStr, Font(36, FontStyle.Bold), TextColor, rightRect.X + 14, rightRect.Y + 32);

            // ラップ無効時
            if (!t.IsLapValid)
            {
                var invRect = new RectangleF(rightRect.Right - 126, rightRect.Y + 28, 110, 34);
                FillRounded(invRect, Danger, 6);
                DrawCentered("INVALID", Font(16, FontStyle.Bold), TextColor, invRect);
            }

            // ギャップ
            float gy = rightRect.Y + 104;
            DrawText("DRIVER AHEAD", Font(13, FontStyle.Bold), Dim, rightRect.X + 16, gy);
            if (!string.IsNullOrEmpty(t.Gaps.AheadName))
            {
                DrawRight(t.Gaps.AheadName, Font(14), TextColor, new RectangleF(rightRect.X + 110, gy, rightRect.Width - 124, 20));
            }
            string aheadGapStr = t.Gaps.AheadGapSeconds.HasValue ? $"+{t.Gaps.AheadGapSeconds.Value:0.000} s" : "-";
            DrawText(aheadGapStr, Font(26, FontStyle.Bold), TextColor, rightRect.X + 16, gy + 24);

            gy += 88;
            DrawText("DRIVER BEHIND", Font(13, FontStyle.Bold), Dim, rightRect.X + 16, gy);
            if (!string.IsNullOrEmpty(t.Gaps.BehindName))
            {
                DrawRight(t.Gaps.BehindName, Font(14), TextColor, new RectangleF(rightRect.X + 110, gy, rightRect.Width - 124, 20));
            }
            string behindGapStr = t.Gaps.BehindGapSeconds.HasValue ? $"-{t.Gaps.BehindGapSeconds.Value:0.000} s" : "-";
            DrawText(behindGapStr, Font(26, FontStyle.Bold), TextColor, rightRect.X + 16, gy + 24);
        }

        private void DrawLapCol(float x, float y, float w, string label, TimeSpan? time, Color color)
        {
            DrawCentered(label, Font(12), Dim, new RectangleF(x, y + 14, w, 18));
            DrawCentered(FormatLap(time), Font(19, FontStyle.Bold), color, new RectangleF(x, y + 38, w, 36));
        }

        private void DrawSectorCol(float x, float y, float w, int secNum, TimeSpan? current, TimeSpan? best, bool isCurrent)
        {
            var numRect = new RectangleF(x + 14, y + 12, 34, 22);
            FillRounded(numRect, isCurrent ? Accent : Color.FromArgb(40, 46, 60), 4);
            DrawCentered($"S{secNum}", Font(12, FontStyle.Bold), isCurrent ? Color.FromArgb(14, 18, 24) : Dim, numRect);

            string curStr = current.HasValue ? $"{current.Value.TotalSeconds:0.000}" : "-";
            DrawRight(curStr, Font(22, FontStyle.Bold), TextColor, new RectangleF(x + 50, y + 10, w - 64, 30));

            string bestStr = best.HasValue ? $"BEST {best.Value.TotalSeconds:0.000}" : "-";
            DrawRight(bestStr, Font(13), Purple, new RectangleF(x + 50, y + 46, w - 64, 20));
        }

        // ---- 5. SESSION (セッション・環境・車両設定) ----

        private void DrawSession(Telemetry t)
        {
            DrawHeader("SESSION & ENVIRONMENT", t);

            // 上段: セッション種別 & 残り時間 / コース・車名
            var topRect = new RectangleF(16, 56, 928, 88);
            FillRounded(topRect, Panel, 10);

            string sessionType = !string.IsNullOrEmpty(t.SessionTypeName) ? t.SessionTypeName.ToUpperInvariant() : "SESSION";
            DrawText(sessionType, Font(32, FontStyle.Bold), TextColor, topRect.X + 16, topRect.Y + 12);

            string leftInfo = t.SessionTimeLeft.HasValue && t.SessionTimeLeft.Value > TimeSpan.Zero
                ? FormatDuration(t.SessionTimeLeft.Value)
                : (t.RemainingLaps.HasValue ? $"{t.RemainingLaps.Value} LAPS LEFT" : "");
            if (!string.IsNullOrEmpty(leftInfo))
            {
                DrawRight(leftInfo, Font(28, FontStyle.Bold), Accent, new RectangleF(topRect.X + 500, topRect.Y + 14, topRect.Width - 516, 36));
            }

            string trackAndCar = $"{t.TrackNameWithConfig ?? t.TrackName ?? "-"}  |  {t.CarModel ?? "-"}";
            DrawFit(trackAndCar, 16, FontStyle.Regular, Dim, new RectangleF(topRect.X + 18, topRect.Y + 54, topRect.Width - 36, 24));

            // 中段: 環境 & フラッグ
            var midRect = new RectangleF(16, 152, 928, 76);
            FillRounded(midRect, Panel, 10);

            // 気温 / 路面温度
            string airStr = t.AirTemp.HasValue ? $"{t.AirTemp.Value:0.0}°C" : "-°C";
            string roadStr = t.RoadTemp.HasValue ? $"{t.RoadTemp.Value:0.0}°C" : "-°C";
            DrawText("AIR", Font(12), Dim, midRect.X + 16, midRect.Y + 16);
            DrawText(airStr, Font(22, FontStyle.Bold), TextColor, midRect.X + 16, midRect.Y + 34);

            DrawText("ROAD", Font(12), Dim, midRect.X + 140, midRect.Y + 16);
            DrawText(roadStr, Font(22, FontStyle.Bold), TextColor, midRect.X + 140, midRect.Y + 34);

            // 天候
            string rainStr = t.RainIntensity.HasValue && t.RainIntensity.Value > 0.01 ? $"RAIN {t.RainIntensity.Value * 100:0}%" : "DRY";
            DrawText("WEATHER", Font(12), Dim, midRect.X + 280, midRect.Y + 16);
            DrawText(rainStr, Font(20, FontStyle.Bold), t.RainIntensity is > 0.01 ? ColdColor : TextColor, midRect.X + 280, midRect.Y + 34);

            // フラッグ表示
            var flag = GetActiveFlag(t);
            if (flag.HasValue)
            {
                var flagRect = new RectangleF(midRect.X + 460, midRect.Y + 14, midRect.Width - 476, 48);
                FillRounded(flagRect, flag.Value.Bg, 8);
                DrawCentered(flag.Value.Text, Font(20, FontStyle.Bold), flag.Value.Fg, flagRect);
            }

            // 下段: 車両設定タイル & 状態
            var botRect = new RectangleF(16, 236, 928, 118);
            FillRounded(botRect, Panel, 10);

            // タイル 4つ: TC, ABS, BB, MAP
            float tileW = 100;
            DrawSettingTile(botRect.X + 16, botRect.Y + 14, tileW, 88, "TC", t.TCLevel?.ToString() ?? "-", t.TCActive);
            DrawSettingTile(botRect.X + 126, botRect.Y + 14, tileW, 88, "ABS", t.ABSLevel?.ToString() ?? "-", t.ABSActive);
            DrawSettingTile(botRect.X + 236, botRect.Y + 14, tileW, 88, "BB", t.BrakeBias.HasValue ? $"{t.BrakeBias.Value:0.0}%" : "-", false);
            DrawSettingTile(botRect.X + 346, botRect.Y + 14, tileW, 88, "MAP", t.EngineMap?.ToString() ?? "-", false);

            // ピットリミッター
            var limitRect = new RectangleF(botRect.X + 466, botRect.Y + 14, 130, 88);
            FillRounded(limitRect, t.PitLimiterOn ? Danger : Color.FromArgb(34, 38, 50), 8);
            DrawCentered("PIT LIMITER", Font(14, FontStyle.Bold), t.PitLimiterOn ? TextColor : Dim, limitRect);

            // 水温 & 油温
            float tempX = botRect.X + 620;
            string wTempStr = t.WaterTemp.HasValue ? $"{Math.Round(t.WaterTemp.Value):0}°C" : "-°C";
            string oTempStr = t.OilTemp.HasValue ? $"{Math.Round(t.OilTemp.Value):0}°C" : "-°C";
            DrawText("WATER", Font(12), Dim, tempX, botRect.Y + 20);
            DrawText(wTempStr, Font(20, FontStyle.Bold), TextColor, tempX, botRect.Y + 38);

            DrawText("OIL", Font(12), Dim, tempX + 120, botRect.Y + 20);
            DrawText(oTempStr, Font(20, FontStyle.Bold), TextColor, tempX + 120, botRect.Y + 38);

            // ダメージ
            float damX = tempX + 220;
            string damStr = t.DamagePercent.HasValue ? $"{Math.Round(t.DamagePercent.Value):0}%" : "0%";
            Color damCol = t.DamagePercent is >= 30 ? Danger : (t.DamagePercent is >= 10 ? Warn : TextColor);
            DrawText("DAMAGE", Font(12), Dim, damX, botRect.Y + 20);
            DrawText(damStr, Font(24, FontStyle.Bold), damCol, damX, botRect.Y + 40);
        }

        private (Color Bg, Color Fg, string Text)? GetActiveFlag(Telemetry t)
        {
            if (t.FlagCheckered || t.FlagName?.Contains("Checker", StringComparison.OrdinalIgnoreCase) == true)
                return (Color.White, Color.Black, "CHECKERED FLAG");
            if (t.FlagBlack || t.FlagName?.Contains("Black", StringComparison.OrdinalIgnoreCase) == true)
                return (Color.FromArgb(30, 30, 35), Danger, "BLACK FLAG");
            if (t.FlagYellow || t.FlagName?.Contains("Yellow", StringComparison.OrdinalIgnoreCase) == true)
                return (Warn, Color.FromArgb(20, 20, 24), "YELLOW FLAG");
            if (t.FlagBlue || t.FlagName?.Contains("Blue", StringComparison.OrdinalIgnoreCase) == true)
                return (ColdColor, Color.White, "BLUE FLAG");
            if (t.FlagGreen || t.FlagName?.Contains("Green", StringComparison.OrdinalIgnoreCase) == true)
                return (Accent, Color.FromArgb(20, 20, 24), "GREEN FLAG");
            return null;
        }

        private void DrawSettingTile(float x, float y, float w, float h, string title, string val, bool active)
        {
            var rect = new RectangleF(x, y, w, h);
            FillRounded(rect, active ? Color.FromArgb(60, 45, 20) : Color.FromArgb(34, 38, 50), 8);
            DrawCentered(title, Font(13, FontStyle.Bold), active ? Warn : Dim, new RectangleF(x, y + 12, w, 18));
            DrawCentered(val, Font(22, FontStyle.Bold), active ? Warn : TextColor, new RectangleF(x, y + 38, w, 32));
        }

        private void DrawRoundedBorder(RectangleF r, Color color, float radius, float penWidth)
        {
            radius = Math.Min(radius, Math.Min(r.Width, r.Height) / 2);
            using var path = new GraphicsPath();
            float d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            using var pen = new Pen(color, penWidth);
            _g.DrawPath(pen, path);
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

            string loadText = load.HasValue ? $"{load.Value:0}" : "-";
            var big = Font(72, FontStyle.Bold);
            DrawText(loadText, big, TextColor, r.X + 8, r.Y + 34);
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

            var spark = new RectangleF(r.X + 16, r.Y + 128, r.Width - 32, 62);
            DrawSparkline(spark, history);

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

        private void DrawShiftLights(double ratio, bool shift)
        {
            const int count = 20;
            const float margin = 16, gap = 6, top = 12, h = 30;
            float w = (Width - margin * 2 - gap * (count - 1)) / count;
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

        private static string FormatDuration(TimeSpan? t)
        {
            if (t is not { } v || v <= TimeSpan.Zero) return "-:--";
            return v.TotalHours >= 1 ? v.ToString(@"h\:mm\:ss") : $"{(int)v.TotalMinutes}:{v.Seconds:00}";
        }

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
