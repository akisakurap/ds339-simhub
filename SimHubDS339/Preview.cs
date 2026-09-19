namespace SimHubDS339
{
    /// <summary>デバイスなしでレイアウト確認用の PNG を出力する。</summary>
    internal static class Preview
    {
        public static void Run(string outDir)
        {
            Directory.CreateDirectory(outDir);
            using var renderer = new DashboardRenderer();

            var pc = SamplePc();
            var noTemp = SamplePc(cpuTemp: null);

            var samples = new (string Name, Telemetry T, PcStats? Pc)[]
            {
                ("pc_disconnected", new Telemetry { SimHubConnected = false }, pc),
                ("pc_waiting", new Telemetry { SimHubConnected = true, GameName = "FH6" }, pc),
                ("pc_notemp", new Telemetry { SimHubConnected = true }, noTemp),
                ("pc_nodata", new Telemetry { SimHubConnected = false }, null),
                ("race_normal", new Telemetry
                {
                    SimHubConnected = true, GameRunning = true, GameName = "FH6",
                    SpeedKmh = 187.4, Rpm = 6120, MaxRpm = 8500, RedlineRpm = 8000, Gear = "4",
                    Throttle = 0.82, Brake = 0.0, CurrentLap = 3, TotalLaps = 10, Position = 2, OpponentsCount = 12,
                    CurrentLapTime = TimeSpan.FromSeconds(51.234), LastLapTime = TimeSpan.FromSeconds(92.871),
                    BestLapTime = TimeSpan.FromSeconds(91.502), FuelPercent = 64,
                }, null),
                ("race_shift", new Telemetry
                {
                    SimHubConnected = true, GameRunning = true,
                    SpeedKmh = 243, Rpm = 8210, MaxRpm = 8500, RedlineRpm = 8000, Gear = "N",
                    Throttle = 1.0, Brake = 0.35, CurrentLap = 12, Position = 11, OpponentsCount = 24,
                    FuelPercent = 7,
                }, null),
            };

            foreach (var (name, t, stats) in samples)
            {
                renderer.Render(t, stats);
                var path = Path.Combine(outDir, name + ".png");
                renderer.SavePng(path);
                Console.WriteLine(path);
            }
        }

        private static PcStats SamplePc(double? cpuTemp = 52)
        {
            // それらしい 60 秒分の推移
            double[] Wave(double baseLoad, double amp, double freq) =>
                Enumerable.Range(0, PcStats.HistoryLength).Select(i => Math.Clamp(baseLoad + amp * Math.Sin(i * freq) + (i % 7 == 0 ? amp : 0), 0, 100)).ToArray();

            return new PcStats
            {
                CpuLoad = 23, CpuTemp = cpuTemp, CpuClockMhz = 5600,
                GpuName = "AMD Radeon RX 9070 XT", GpuLoad = 8, GpuTemp = 41, VramUsedMb = 2150, VramTotalMb = 16304,
                RamUsedGb = 18.4, RamTotalGb = 31.8,
                NetDownBps = 1.2 * 1024 * 1024, NetUpBps = 85 * 1024,
                CpuHistory = Wave(20, 10, 0.4), GpuHistory = Wave(8, 5, 0.25), RamHistory = Wave(58, 0.5, 0.1),
            };
        }
    }

    /// <summary>ゲームなしで実機のレース画面を確認するための擬似テレメトリ。</summary>
    internal static class Demo
    {
        private static readonly DateTime Start = DateTime.Now;

        public static Telemetry Create()
        {
            double t = (DateTime.Now - Start).TotalSeconds;
            // 1 ギア 6 秒で 1→6 速を繰り返す
            double inGear = (t % 6.0) / 6.0;
            int gear = (int)(t / 6.0) % 6 + 1;
            double rpm = 3000 + inGear * 5400;
            bool braking = (t % 36.0) > 33.0;
            double lapLength = 92.0;
            return new Telemetry
            {
                SimHubConnected = true,
                GameRunning = true,
                GameName = "DEMO",
                Gear = gear.ToString(),
                Rpm = rpm,
                MaxRpm = 8500,
                RedlineRpm = 8000,
                SpeedKmh = gear * 38 + inGear * 40,
                Throttle = braking ? 0 : 0.6 + 0.4 * inGear,
                Brake = braking ? 0.9 : 0,
                CurrentLap = (int)(t / lapLength) % 10 + 1,
                TotalLaps = 10,
                Position = 3,
                OpponentsCount = 16,
                CurrentLapTime = TimeSpan.FromSeconds(t % lapLength),
                LastLapTime = t >= lapLength ? TimeSpan.FromSeconds(lapLength + 0.873) : null,
                BestLapTime = t >= lapLength ? TimeSpan.FromSeconds(lapLength - 0.412) : null,
                FuelPercent = Math.Max(0, 100 - t / 6.0),
            };
        }
    }
}
