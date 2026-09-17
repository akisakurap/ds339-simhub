using System.Globalization;

namespace SimHubDS339
{
    /// <summary>描画に必要な SimHub の値のスナップショット。</summary>
    internal sealed class Telemetry
    {
        public static readonly string[] SubscribedProperties =
        {
            "dcp.GameRunning",
            "dcp.GameName",
            "dcp.gd.SpeedKmh",
            "dcp.gd.Rpms",
            "dcp.gd.MaxRpm",
            "dcp.gd.CarSettings_CurrentGearRedLineRPM",
            "dcp.gd.Gear",
            "dcp.gd.Throttle",
            "dcp.gd.Brake",
            "dcp.gd.CarModel",
            "dcp.gd.TrackName",
            "dcp.gd.CurrentLap",
            "dcp.gd.TotalLaps",
            "dcp.gd.Position",
            "dcp.gd.OpponentsCount",
            "dcp.gd.CurrentLapTime",
            "dcp.gd.LastLapTime",
            "dcp.gd.BestLapTime",
            "dcp.gd.FuelPercent",
        };

        public bool SimHubConnected { get; init; }
        public bool GameRunning { get; init; }
        public string? GameName { get; init; }
        public double SpeedKmh { get; init; }
        public double Rpm { get; init; }
        public double MaxRpm { get; init; }
        public double RedlineRpm { get; init; }
        public string Gear { get; init; } = "-";
        /// <summary>0..1</summary>
        public double Throttle { get; init; }
        /// <summary>0..1</summary>
        public double Brake { get; init; }
        public string? CarModel { get; init; }
        public string? TrackName { get; init; }
        public int? CurrentLap { get; init; }
        public int? TotalLaps { get; init; }
        public int? Position { get; init; }
        public int? OpponentsCount { get; init; }
        public TimeSpan? CurrentLapTime { get; init; }
        public TimeSpan? LastLapTime { get; init; }
        public TimeSpan? BestLapTime { get; init; }
        /// <summary>0..100、不明なら null</summary>
        public double? FuelPercent { get; init; }

        public static Telemetry From(SimHubPropertyClientAdapter client)
        {
            return new Telemetry
            {
                SimHubConnected = client.IsConnected,
                GameRunning = client.GetBool("dcp.GameRunning"),
                GameName = client.GetString("dcp.GameName"),
                SpeedKmh = client.GetDouble("dcp.gd.SpeedKmh") ?? 0,
                Rpm = client.GetDouble("dcp.gd.Rpms") ?? 0,
                MaxRpm = client.GetDouble("dcp.gd.MaxRpm") ?? 0,
                RedlineRpm = client.GetDouble("dcp.gd.CarSettings_CurrentGearRedLineRPM") ?? 0,
                Gear = client.GetString("dcp.gd.Gear") ?? "-",
                // SimHub の Throttle/Brake は 0..100
                Throttle = Math.Clamp((client.GetDouble("dcp.gd.Throttle") ?? 0) / 100.0, 0, 1),
                Brake = Math.Clamp((client.GetDouble("dcp.gd.Brake") ?? 0) / 100.0, 0, 1),
                CarModel = client.GetString("dcp.gd.CarModel"),
                TrackName = client.GetString("dcp.gd.TrackName"),
                CurrentLap = client.GetInt("dcp.gd.CurrentLap"),
                TotalLaps = client.GetInt("dcp.gd.TotalLaps"),
                Position = client.GetInt("dcp.gd.Position"),
                OpponentsCount = client.GetInt("dcp.gd.OpponentsCount"),
                CurrentLapTime = client.GetTimeSpan("dcp.gd.CurrentLapTime"),
                LastLapTime = client.GetTimeSpan("dcp.gd.LastLapTime"),
                BestLapTime = client.GetTimeSpan("dcp.gd.BestLapTime"),
                FuelPercent = client.GetDouble("dcp.gd.FuelPercent"),
            };
        }
    }

    /// <summary>SimHubPropertyClient の文字列値を型付きで読むための薄いアダプタ。</summary>
    internal sealed class SimHubPropertyClientAdapter
    {
        private readonly SimHubAida64Bridge.SimHubPropertyClient _client;

        public SimHubPropertyClientAdapter(SimHubAida64Bridge.SimHubPropertyClient client)
        {
            _client = client;
        }

        public bool IsConnected => _client.IsConnected;

        public string? GetString(string name)
        {
            return _client.Values.TryGetValue(name, out var v) && !string.IsNullOrEmpty(v) ? v : null;
        }

        public double? GetDouble(string name)
        {
            var s = GetString(name);
            return s != null && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
        }

        public int? GetInt(string name)
        {
            var d = GetDouble(name);
            return d.HasValue ? (int)Math.Round(d.Value) : null;
        }

        public bool GetBool(string name)
        {
            var s = GetString(name);
            return s != null && (s.Equals("True", StringComparison.OrdinalIgnoreCase) || s == "1");
        }

        public TimeSpan? GetTimeSpan(string name)
        {
            var s = GetString(name);
            return s != null && TimeSpan.TryParse(s, CultureInfo.InvariantCulture, out var t) ? t : null;
        }
    }
}
