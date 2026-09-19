using System.Diagnostics;
using SimHubDS339;

// SimHub (Property Server プラグイン, TCP:18082) の値を 960x376 ダッシュボードとして描画し、
// DS339 (MS9132) に libusb で直接送る常駐ツール。AIDA64 / JONSBO 公式アプリは不要 (同時使用不可)。
//
// usage:
//   SimHubDS339                 常駐実行 (Ctrl+C で終了)
//   SimHubDS339 --fps 10        送信レート指定 (既定 10, 範囲 1-20)
//   SimHubDS339 --stats 60      統計ログの出力間隔 (秒, 既定 60)
//   SimHubDS339 --demo          SimHub の代わりに擬似テレメトリでレース画面を表示 (動作確認用)
//   SimHubDS339 --preview DIR   デバイスに触れず、サンプル値で描画した PNG を DIR に出力して終了
//   SimHubDS339 --sensors       PC ステータス用に検出したセンサーを一覧表示して終了
//
// ゲーム未起動時 (SimHub 未接続を含む) は LibreHardwareMonitor で読んだ PC ステータスを表示する。

if (args.Length >= 2 && args[0] == "--preview")
{
    Preview.Run(args[1]);
    return;
}

if (args.Contains("--sensors"))
{
    PcStatsSampler.Dump();
    return;
}

int fps = 10;
int statsSec = 60;
for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--fps" && int.TryParse(args[i + 1], out var f)) fps = Math.Clamp(f, 1, 20);
    if (args[i] == "--stats" && int.TryParse(args[i + 1], out var st)) statsSec = Math.Max(1, st);
}
int periodMs = 1000 / fps;
bool demo = args.Contains("--demo");

Console.WriteLine($"SimHubDS339 starting ({fps} fps). Ctrl+C to exit.");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

using var client = new SimHubPropertyClient("127.0.0.1", 18082, Telemetry.SubscribedProperties);
var adapter = new SimHubPropertyClientAdapter(client);
client.Start();

if (!PcStatsSampler.IsElevated)
    Console.WriteLine("[PC] not elevated: CPU temperature unavailable (needs administrator + PawnIO driver)");
using var pcStats = new PcStatsSampler();
pcStats.Start();

using var renderer = new DashboardRenderer();
using var link = new DisplayLink();

var sw = Stopwatch.StartNew();
long nextFrame = 0;
long lastStats = 0;
int sent = 0;

while (!cts.IsCancellationRequested)
{
    long now = sw.ElapsedMilliseconds;
    if (now >= nextFrame)
    {
        var t = demo ? Demo.Create() : Telemetry.From(adapter);
        pcStats.Paused = t.SimHubConnected && t.GameRunning;
        var frame = renderer.Render(t, pcStats.Current);
        if (link.TrySend(frame)) sent++;

        nextFrame += periodMs;
        if (nextFrame < sw.ElapsedMilliseconds) nextFrame = sw.ElapsedMilliseconds + periodMs;
    }

    if (now - lastStats >= statsSec * 1000L)
    {
        Console.WriteLine($"{DateTime.Now:HH:mm:ss} [stats] frames sent in last {statsSec}s: {sent}, device={(link.IsConnected ? "connected" : "disconnected")}, simhub={(client.IsConnected ? "connected" : "disconnected")}, pcstats={(pcStats.Paused ? "paused" : "running")}");
        sent = 0;
        lastStats = now;
    }

    long wait = nextFrame - sw.ElapsedMilliseconds;
    if (wait > 0)
    {
        try
        {
            await Task.Delay((int)wait, cts.Token);
        }
        catch (TaskCanceledException)
        {
            break;
        }
    }
}

Console.WriteLine("Stopping...");
