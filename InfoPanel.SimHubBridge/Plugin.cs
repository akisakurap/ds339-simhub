using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using InfoPanel.Plugins;

namespace InfoPanel.SimHubBridge
{
    /// <summary>
    /// SimHub (経由: SimHubPropertyServer プラグイン, TCP:18082) からテレメトリを取得し、
    /// InfoPanel のセンサー/テキスト項目として公開するブリッジプラグイン。
    ///
    /// 前提:
    ///   1. SimHub 側に https://github.com/pre-martin/SimHubPropertyServer の
    ///      PropertyServer.dll を導入し、Settings > Plugins で有効化しておくこと。
    ///   2. このプラグインの DLL (と依存 DLL) を InfoPanel の Plugins フォルダに配置し、
    ///      InfoPanel の「Import Plugin」から読み込むこと。
    ///
    /// InfoPanel.Plugins (habibrehmansg/infopanel リポジトリ) の実ソースに合わせて
    /// BasePlugin / PluginSensor / PluginText / Load シグネチャを実装している。
    /// </summary>
    public class SimHubBridgePlugin : BasePlugin
    {
        // ---- 表示したい SimHub プロパティをここに列挙 ----
        // プロパティ名は SimHubPropertyServer のドキュメント (dcp.* prefix) に準拠。
        // 必要に応じて increase/decrease してよい。
        private static readonly string[] SubscribedProperties =
        {
            "dcp.gd.SpeedKmh",
            "dcp.gd.Rpms",
            "dcp.gd.MaxRpm",
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
            "dcp.GameRunning",
        };

        private SimHubPropertyClient? _client;

        // ---- DS339 に出す項目。InfoPanel のデザイン画面でドラッグ&ドロップできるようになる ----
        private readonly PluginSensor _speedKmh = new("speed_kmh", "Speed (km/h)", 0, "km/h");
        private readonly PluginSensor _rpms = new("rpms", "RPM", 0, "rpm");
        private readonly PluginSensor _maxRpm = new("max_rpm", "Max RPM", 0, "rpm");
        private readonly PluginText _gear = new("gear", "Gear", "-");
        private readonly PluginSensor _throttle = new("throttle", "Throttle", 0, "%");
        private readonly PluginSensor _brake = new("brake", "Brake", 0, "%");
        private readonly PluginSensor _clutch = new("clutch", "Clutch", 0, "%");
        private readonly PluginText _carModel = new("car_model", "Car", "-");
        private readonly PluginText _trackName = new("track_name", "Track", "-");
        private readonly PluginText _lapInfo = new("lap_info", "Lap", "-");
        private readonly PluginText _currentLapTime = new("current_lap_time", "Current Lap", "-");
        private readonly PluginText _bestLapTime = new("best_lap_time", "Best Lap", "-");
        private readonly PluginSensor _fuelPercent = new("fuel_percent", "Fuel", 0, "%");
        private readonly PluginText _connectionStatus = new("connection_status", "SimHub Connection", "Disconnected");

        public override string? ConfigFilePath => null;
        public override TimeSpan UpdateInterval => TimeSpan.FromMilliseconds(100); // SimHub側は10Hz送出なのでこれで十分

        public SimHubBridgePlugin()
            : base("simhub-bridge", "SimHub Bridge", "SimHub のテレメトリを DS339 に表示するブリッジプラグイン")
        {
        }

        public override void Initialize()
        {
            // SimHubPropertyServer のデフォルトポートは 18082。SimHub 側の設定を変えた場合はここも合わせる
            _client = new SimHubPropertyClient("127.0.0.1", 18082, SubscribedProperties);
            _client.ConnectionStateChanged += connected =>
            {
                _connectionStatus.Value = connected ? "Connected" : "Disconnected";
            };
            _client.Start();
        }

        public override void Load(List<IPluginContainer> containers)
        {
            var container = new PluginContainer("SimHub");
            container.Entries.Add(_speedKmh);
            container.Entries.Add(_rpms);
            container.Entries.Add(_maxRpm);
            container.Entries.Add(_gear);
            container.Entries.Add(_throttle);
            container.Entries.Add(_brake);
            container.Entries.Add(_clutch);
            container.Entries.Add(_carModel);
            container.Entries.Add(_trackName);
            container.Entries.Add(_lapInfo);
            container.Entries.Add(_currentLapTime);
            container.Entries.Add(_bestLapTime);
            container.Entries.Add(_fuelPercent);
            container.Entries.Add(_connectionStatus);
            containers.Add(container);
        }

        public override void Update()
        {
            if (_client == null) return;

            _speedKmh.Value = ReadFloat("dcp.gd.SpeedKmh");
            _rpms.Value = ReadFloat("dcp.gd.Rpms");
            _maxRpm.Value = ReadFloat("dcp.gd.MaxRpm");
            _gear.Value = ReadString("dcp.gd.Gear") ?? "-";
            _throttle.Value = ReadFloat("dcp.gd.Throttle") * 100f;
            _brake.Value = ReadFloat("dcp.gd.Brake") * 100f;
            _clutch.Value = ReadFloat("dcp.gd.Clutch") * 100f;
            _carModel.Value = ReadString("dcp.gd.CarModel") ?? "-";
            _trackName.Value = ReadString("dcp.gd.TrackName") ?? "-";
            _currentLapTime.Value = ReadString("dcp.gd.CurrentLapTime") ?? "-";
            _bestLapTime.Value = ReadString("dcp.gd.BestLapTime") ?? "-";
            _fuelPercent.Value = ReadFloat("dcp.gd.FuelPercent");

            var currentLap = ReadString("dcp.gd.CurrentLap");
            var totalLaps = ReadString("dcp.gd.TotalLaps");
            _lapInfo.Value = (currentLap != null && totalLaps != null)
                ? $"{currentLap} / {totalLaps}"
                : "-";
        }

        public override Task UpdateAsync(CancellationToken cancellationToken)
        {
            Update();
            return Task.CompletedTask;
        }

        public override void Close()
        {
            _client?.Stop();
            _client?.Dispose();
            _client = null;
        }

        private string? ReadString(string propertyName)
        {
            if (_client != null && _client.Values.TryGetValue(propertyName, out var v))
                return v;
            return null;
        }

        private float ReadFloat(string propertyName)
        {
            var s = ReadString(propertyName);
            if (s != null && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                return f;
            return 0f;
        }
    }
}
