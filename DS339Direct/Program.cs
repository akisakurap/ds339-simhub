using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using DS339Direct;

const int Width = 376;
const int Height = 960;

// usage: DS339Direct [frameFile.rgb] [seconds=60] [framePeriodMs=500] [landscape]
//   landscape 指定時は frameFile を見た目どおりの 960x376 として SendLandscapeFrame で送る。
//   frameFile: 376x960 RGB888 raw. 省略時は赤/緑/青の横帯に白帯が流れるアニメーション。
string? framePath = args.Length > 0 && args[0] != "-" ? args[0] : null;
int seconds = args.Length > 1 ? int.Parse(args[1]) : 60;
int framePeriodMs = args.Length > 2 ? int.Parse(args[2]) : 500;
bool landscape = args.Length > 3 && args[3] == "landscape";
const int HeartbeatPeriodMs = 500; // 公式アプリは xdata 0x0032 を 500ms 周期で読み続けている

Console.WriteLine("DS339 direct protocol test - connecting...");

using var dev = new Ms9132Device();
try
{
    dev.Open();
}
catch (Exception ex)
{
    Console.WriteLine($"Open failed: {ex.Message}");
    return;
}

if (dev.GetChipId(out var chipId)) Console.WriteLine($"chip_id = {chipId}");
if (dev.GetPortType(out var portType)) Console.WriteLine($"port_type = {portType}");

byte[] baseFrame;
if (framePath != null)
{
    baseFrame = File.ReadAllBytes(framePath);
    Console.WriteLine($"Using frame file {framePath}");
}
else
{
    baseFrame = new byte[Width * Height * 3];
    for (int y = 0; y < Height; y++)
    {
        int band = y * 3 / Height;
        for (int x = 0; x < Width; x++)
        {
            int idx = (y * Width + x) * 3;
            baseFrame[idx + band] = 255; // band0=R, band1=G, band2=B
        }
    }
}

byte[] MakeFrame(int n)
{
    if (framePath != null) return baseFrame;
    var f = (byte[])baseFrame.Clone();
    int barY = (n * 24) % Height;
    for (int y = barY; y < Math.Min(barY + 40, Height); y++)
        for (int x = 0; x < Width; x++)
        {
            int idx = (y * Width + x) * 3;
            f[idx] = f[idx + 1] = f[idx + 2] = 255;
        }
    return f;
}

Console.WriteLine("Initializing display...");
dev.InitializeDisplayDS339();

var sw = Stopwatch.StartNew();
long nextHeartbeat = 0;
long nextFrame = 300;
int framesSent = 0, framesFailed = 0;
bool shown = false;

while (sw.ElapsedMilliseconds < seconds * 1000L)
{
    long now = sw.ElapsedMilliseconds;

    if (now >= nextHeartbeat)
    {
        if (!dev.XdataReadOnce(0x0032, 3, out _)) Console.WriteLine($"[{now}ms] heartbeat failed");
        nextHeartbeat += HeartbeatPeriodMs;
    }

    if (now >= nextFrame)
    {
        long t0 = sw.ElapsedMilliseconds;
        bool ok = landscape ? dev.SendLandscapeFrame(baseFrame) : dev.SendFrame(MakeFrame(framesSent + framesFailed));
        if (ok) framesSent++; else framesFailed++;
        Console.WriteLine($"[{t0}ms] frame #{framesSent + framesFailed} {(ok ? "ok" : "FAIL")} ({sw.ElapsedMilliseconds - t0}ms)");

        // 公式アプリは 2 フレーム目の送信後に screen_enable(0xF005=0x50) → video_enable
        if (!shown && framesSent >= 2)
        {
            dev.ShowScreen();
            shown = true;
            Console.WriteLine("screen enabled");
        }
        nextFrame += framePeriodMs;
        if (nextFrame < sw.ElapsedMilliseconds) nextFrame = sw.ElapsedMilliseconds;
    }

    long wait = Math.Min(nextHeartbeat, nextFrame) - sw.ElapsedMilliseconds;
    if (wait > 0) Thread.Sleep((int)wait);
}

Console.WriteLine($"Done. sent={framesSent} failed={framesFailed}");
