using System;
using System.Globalization;
using System.Threading;
using Microsoft.Win32;

namespace SimHubAida64Bridge
{
    /// <summary>
    /// SimHub (経由: SimHubPropertyServer プラグイン, TCP:18082) からテレメトリを取得し、
    /// AIDA64 の External Applications (Registry Import Values) 機構に書き込む常駐ツール。
    ///
    /// AIDA64 は HKCU\Software\FinalWire\AIDA64\ImportValues 配下の
    /// Str1..Str10 (文字列) / DW1..DW10 (整数) を SensorPanel / LCD 項目として
    /// 取り込める (Preferences > Hardware Monitoring > External Applications 参照)。
    /// DS339 への描画自体は AIDA64 側 (公式対応済み) に任せる設計。
    ///
    /// 前提:
    ///   1. SimHub 側に PropertyServer.dll (https://github.com/pre-martin/SimHubPropertyServer)
    ///      を導入し有効化しておくこと。
    ///   2. AIDA64 側で SensorPanel / LCD レイアウトに "External Applications" の
    ///      Str1..Str7, DW1..DW6 を配置しておくこと (README 参照)。
    /// </summary>
    internal static class Program
    {
        private const string RegistryPath = @"Software\FinalWire\AIDA64\ImportValues";

        private static readonly string[] SubscribedProperties =
        {
            "dcp.gd.SpeedKmh",
            "dcp.gd.Rpms",
            "dcp.gd.Gear",
            "dcp.gd.Throttle",
            "dcp.gd.Brake",
            "dcp.gd.Clutch",
            "dcp.gd.CarModel",
            "dcp.gd.TrackName",
            "dcp.gd.CurrentLap",
            "dcp.gd.TotalLaps",
            "dcp.gd.CurrentLapTime",
            "dcp.gd.BestLapTime",
            "dcp.gd.FuelPercent",
        };

        private static async System.Threading.Tasks.Task Main()
        {
            Console.WriteLine("SimHub -> AIDA64 bridge starting. Ctrl+C to exit.");

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            using var client = new SimHubPropertyClient("127.0.0.1", 18082, SubscribedProperties);
            client.ConnectionStateChanged += connected =>
            {
                Console.WriteLine(connected ? "[SimHubBridge] Connected." : "[SimHubBridge] Disconnected.");
                WriteString("Str7", connected ? "Connected" : "Disconnected");
            };
            client.Start();

            try
            {
                while (!cts.IsCancellationRequested)
                {
                    PushValues(client);
                    await System.Threading.Tasks.Task.Delay(100, cts.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Ctrl+C による正常終了
            }

            client.Stop();
            Console.WriteLine("Stopped.");
        }

        private static void PushValues(SimHubPropertyClient client)
        {
            WriteInt("DW1", ReadFloat(client, "dcp.gd.SpeedKmh"));
            WriteInt("DW2", ReadFloat(client, "dcp.gd.Rpms"));
            WriteInt("DW3", ReadFloat(client, "dcp.gd.Throttle") * 100f);
            WriteInt("DW4", ReadFloat(client, "dcp.gd.Brake") * 100f);
            WriteInt("DW5", ReadFloat(client, "dcp.gd.Clutch") * 100f);
            WriteInt("DW6", ReadFloat(client, "dcp.gd.FuelPercent"));

            WriteString("Str1", ReadString(client, "dcp.gd.Gear") ?? "-");
            WriteString("Str2", ReadString(client, "dcp.gd.CarModel") ?? "-");
            WriteString("Str3", ReadString(client, "dcp.gd.TrackName") ?? "-");

            var currentLap = ReadString(client, "dcp.gd.CurrentLap");
            var totalLaps = ReadString(client, "dcp.gd.TotalLaps");
            WriteString("Str4", (currentLap != null && totalLaps != null) ? $"{currentLap} / {totalLaps}" : "-");

            WriteString("Str5", ReadString(client, "dcp.gd.CurrentLapTime") ?? "-");
            WriteString("Str6", ReadString(client, "dcp.gd.BestLapTime") ?? "-");
        }

        private static string? ReadString(SimHubPropertyClient client, string propertyName)
        {
            return client.Values.TryGetValue(propertyName, out var v) ? v : null;
        }

        private static float ReadFloat(SimHubPropertyClient client, string propertyName)
        {
            var s = ReadString(client, propertyName);
            if (s != null && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                return f;
            return 0f;
        }

        private static void WriteInt(string name, float value)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
                key?.SetValue(name, (int)Math.Round(value), RegistryValueKind.DWord);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SimHubBridge][WARN] Failed to write {name}: {ex.Message}");
            }
        }

        private static void WriteString(string name, string value)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
                key?.SetValue(name, value, RegistryValueKind.String);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SimHubBridge][WARN] Failed to write {name}: {ex.Message}");
            }
        }
    }
}
