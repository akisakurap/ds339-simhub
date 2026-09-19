using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

            var sampleTyres = CreateSampleTyresTelemetry();
            var sampleFuel = CreateSampleFuelTelemetry();
            var sampleDelta = CreateSampleDeltaTelemetry();
            var sampleSession = CreateSampleSessionTelemetry();

            var samples = new (string Name, Telemetry T, PcStats? Pc, RacePage Page, PageOverlay? Overlay)[]
            {
                // 既存 6 枚
                ("pc_disconnected", new Telemetry { SimHubConnected = false }, pc, RacePage.Main, null),
                ("pc_waiting", new Telemetry { SimHubConnected = true, GameName = "FH6" }, pc, RacePage.Main, null),
                ("pc_notemp", new Telemetry { SimHubConnected = true }, noTemp, RacePage.Main, null),
                ("pc_nodata", new Telemetry { SimHubConnected = false }, null, RacePage.Main, null),
                ("race_normal", new Telemetry
                {
                    SimHubConnected = true, GameRunning = true, GameName = "FH6",
                    SpeedKmh = 187.4, Rpm = 6120, MaxRpm = 8500, RedlineRpm = 8000, Gear = "4",
                    Throttle = 0.82, Brake = 0.0, CurrentLap = 3, TotalLaps = 10, Position = 2, OpponentsCount = 12,
                    CurrentLapTime = TimeSpan.FromSeconds(51.234), LastLapTime = TimeSpan.FromSeconds(92.871),
                    BestLapTime = TimeSpan.FromSeconds(91.502), FuelPercent = 64,
                }, null, RacePage.Main, null),
                ("race_shift", new Telemetry
                {
                    SimHubConnected = true, GameRunning = true,
                    SpeedKmh = 243, Rpm = 8210, MaxRpm = 8500, RedlineRpm = 8000, Gear = "N",
                    Throttle = 1.0, Brake = 0.35, CurrentLap = 12, Position = 11, OpponentsCount = 24,
                    FuelPercent = 7,
                }, null, RacePage.Main, null),

                // 新規 5 枚
                ("race_tyres", sampleTyres, null, RacePage.Tyres, null),
                ("race_fuel", sampleFuel, null, RacePage.Fuel, null),
                ("race_delta", sampleDelta, null, RacePage.Delta, null),
                ("race_session", sampleSession, null, RacePage.Session, null),
                ("race_tyres_overlay", sampleTyres, null, RacePage.Tyres, new PageOverlay("TYRES", TimeSpan.FromSeconds(10))),
            };

            foreach (var (name, t, stats, page, overlay) in samples)
            {
                renderer.Render(t, stats, page, overlay);
                var path = Path.Combine(outDir, name + ".png");
                renderer.SavePng(path);
                Console.WriteLine(path);
            }
        }

        private static Telemetry CreateSampleTyresTelemetry()
        {
            return new Telemetry
            {
                SimHubConnected = true,
                GameRunning = true,
                GameName = "LMU",
                Position = 3,
                OpponentsCount = 18,
                CurrentLap = 8,
                TotalLaps = 20,
                CurrentLapTime = TimeSpan.FromSeconds(64.128),
                FrontCompound = "MEDIUM",
                RearCompound = "MEDIUM",
                TemperatureUnit = "°C",
                PressureUnit = "kPa",
                FrontLeft = new TyreWheelData
                {
                    Temp = 86.4, TempOuter = 83.2, TempMiddle = 86.5, TempInner = 89.1,
                    Pressure = 182.4, Wear = 89.2, LastLapWear = 1.3, BrakeTemp = 580.0
                },
                FrontRight = new TyreWheelData
                {
                    Temp = 88.7, TempInner = 92.1, TempMiddle = 88.4, TempOuter = 85.3,
                    Pressure = 184.8, Wear = 86.5, LastLapWear = 1.6, BrakeTemp = 640.0
                },
                RearLeft = new TyreWheelData
                {
                    Temp = 84.1, TempOuter = 81.5, TempMiddle = 84.2, TempInner = 86.8,
                    Pressure = 178.6, Wear = 91.0, LastLapWear = 1.1, BrakeTemp = 510.0
                },
                RearRight = new TyreWheelData
                {
                    Temp = 85.3, TempInner = 87.4, TempMiddle = 85.1, TempOuter = 82.9,
                    Pressure = 180.2, Wear = 89.8, LastLapWear = 1.2, BrakeTemp = 530.0
                },
            };
        }

        private static Telemetry CreateSampleFuelTelemetry()
        {
            return new Telemetry
            {
                SimHubConnected = true,
                GameRunning = true,
                GameName = "LMU",
                Position = 3,
                OpponentsCount = 18,
                CurrentLap = 8,
                TotalLaps = 20,
                CurrentLapTime = TimeSpan.FromSeconds(64.128),
                LastLapTime = TimeSpan.FromSeconds(102.450),
                BestLapTime = TimeSpan.FromSeconds(101.890),
                Fuel = 38.4,
                MaxFuel = 70.0,
                FuelUnit = "L",
                FuelPercent = 54.8,
                FuelPerLap = 3.25,
                FuelRemainingLaps = 11.8,
                FuelRemainingTime = TimeSpan.FromMinutes(20.5),
                CompletedLaps = 7,
                LastPitStopDuration = 28.6,
                IsInPitLane = false,
                IsInPit = false,
                EnergyPercent = 82.0,
                Stint = new StintInfo
                {
                    Laps = 7,
                    Duration = TimeSpan.FromMinutes(12.3),
                    FuelConsumed = 22.8,
                    FuelPerLap = 3.26,
                }
            };
        }

        private static Telemetry CreateSampleDeltaTelemetry()
        {
            return new Telemetry
            {
                SimHubConnected = true,
                GameRunning = true,
                GameName = "LMU",
                Position = 3,
                OpponentsCount = 18,
                CurrentLap = 8,
                TotalLaps = 20,
                CurrentLapTime = TimeSpan.FromSeconds(58.320),
                LastLapTime = TimeSpan.FromSeconds(102.450),
                BestLapTime = TimeSpan.FromSeconds(101.890),
                AllTimeBest = TimeSpan.FromSeconds(100.950),
                LiveDeltaSeconds = -0.342,
                IsLapValid = true,
                Sectors = new SectorData
                {
                    CurrentSector = 2,
                    S1 = TimeSpan.FromSeconds(31.420),
                    BestS1 = TimeSpan.FromSeconds(31.110),
                    S2 = TimeSpan.FromSeconds(38.250),
                    BestS2 = TimeSpan.FromSeconds(37.950),
                    S3 = null,
                    BestS3 = TimeSpan.FromSeconds(32.830),
                },
                Gaps = new GapData
                {
                    AheadName = "M. Verstappen",
                    AheadGapSeconds = 1.482,
                    BehindName = "L. Hamilton",
                    BehindGapSeconds = 2.140,
                }
            };
        }

        private static Telemetry CreateSampleSessionTelemetry()
        {
            return new Telemetry
            {
                SimHubConnected = true,
                GameRunning = true,
                GameName = "LMU",
                Position = 3,
                OpponentsCount = 18,
                CurrentLap = 8,
                TotalLaps = 20,
                CurrentLapTime = TimeSpan.FromSeconds(58.320),
                SessionTypeName = "RACE",
                SessionTimeLeft = TimeSpan.FromMinutes(38.5),
                TrackNameWithConfig = "Circuit de Spa-Francorchamps (GP)",
                CarModel = "Ferrari 499P Hypercar",
                AirTemp = 22.4,
                RoadTemp = 34.8,
                RainIntensity = 0.0,
                FlagYellow = true,
                FlagName = "Yellow",
                TCLevel = 4,
                ABSLevel = 3,
                BrakeBias = 54.5,
                EngineMap = 2,
                TCActive = false,
                ABSActive = false,
                PitLimiterOn = false,
                WaterTemp = 88.0,
                OilTemp = 104.0,
                DamagePercent = 4.0,
            };
        }

        private static PcStats SamplePc(double? cpuTemp = 52)
        {
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

            // タイヤ温度の周期変化 (75〜95℃)
            double flTemp = 80.0 + Math.Sin(t * 0.3) * 10.0;
            double frTemp = 82.0 + Math.Cos(t * 0.3) * 11.0;
            double rlTemp = 78.0 + Math.Sin(t * 0.25) * 8.0;
            double rrTemp = 81.0 + Math.Cos(t * 0.25) * 9.0;
            double brkTemp = braking ? 680.0 : Math.Max(350.0, 680.0 - (t % 36.0) * 10.0);

            // デルタの揺れ (±1.2 秒)
            double liveDelta = Math.Sin(t * 0.2) * 1.2;

            // 燃料の消費
            double maxFuel = 70.0;
            double currentFuel = Math.Max(5.0, 65.0 - (t * 0.05));
            double fuelPerLap = 3.2;

            // セッションフラグ (40 秒周期で 5 秒間黄旗)
            bool isYellow = (t % 40.0) >= 30.0 && (t % 40.0) <= 35.0;

            // セクター判定 (92 秒を 3 等分)
            double lapSec = t % lapLength;
            int curSector = lapSec < 30.0 ? 1 : (lapSec < 62.0 ? 2 : 3);

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
                CurrentLap = (int)(t / lapLength) % 15 + 1,
                TotalLaps = 15,
                Position = 3,
                OpponentsCount = 16,
                CurrentLapTime = TimeSpan.FromSeconds(lapSec),
                LastLapTime = t >= lapLength ? TimeSpan.FromSeconds(lapLength + 0.873) : null,
                BestLapTime = t >= lapLength ? TimeSpan.FromSeconds(lapLength - 0.412) : null,
                AllTimeBest = TimeSpan.FromSeconds(lapLength - 1.250),
                FuelPercent = Math.Clamp(currentFuel / maxFuel * 100.0, 0, 100),

                // TYRES
                FrontCompound = "SOFT",
                RearCompound = "SOFT",
                TemperatureUnit = "°C",
                PressureUnit = "kPa",
                FrontLeft = new TyreWheelData
                {
                    Temp = flTemp, TempOuter = flTemp - 3, TempMiddle = flTemp, TempInner = flTemp + 3,
                    Pressure = 182.0 + Math.Sin(t * 0.1) * 2, Wear = Math.Max(50, 95 - t * 0.02),
                    LastLapWear = 1.2, BrakeTemp = brkTemp
                },
                FrontRight = new TyreWheelData
                {
                    Temp = frTemp, TempInner = frTemp + 3, TempMiddle = frTemp, TempOuter = frTemp - 3,
                    Pressure = 183.0 + Math.Cos(t * 0.1) * 2, Wear = Math.Max(50, 93 - t * 0.02),
                    LastLapWear = 1.4, BrakeTemp = brkTemp + 20
                },
                RearLeft = new TyreWheelData
                {
                    Temp = rlTemp, TempOuter = rlTemp - 2, TempMiddle = rlTemp, TempInner = rlTemp + 2,
                    Pressure = 179.0, Wear = Math.Max(50, 96 - t * 0.02),
                    LastLapWear = 1.0, BrakeTemp = brkTemp - 80
                },
                RearRight = new TyreWheelData
                {
                    Temp = rrTemp, TempInner = rrTemp + 2, TempMiddle = rrTemp, TempOuter = rrTemp - 2,
                    Pressure = 180.0, Wear = Math.Max(50, 94 - t * 0.02),
                    LastLapWear = 1.1, BrakeTemp = brkTemp - 70
                },

                // FUEL
                Fuel = currentFuel,
                MaxFuel = maxFuel,
                FuelUnit = "L",
                FuelPerLap = fuelPerLap,
                FuelRemainingLaps = currentFuel / fuelPerLap,
                FuelRemainingTime = TimeSpan.FromSeconds((currentFuel / fuelPerLap) * lapLength),
                RemainingLaps = Math.Max(0, 15 - ((int)(t / lapLength) % 15 + 1)),
                SessionTimeLeft = TimeSpan.FromSeconds(Math.Max(0, 1800 - t)),
                CompletedLaps = (int)(t / lapLength) % 15,
                IsInPitLane = false,
                IsInPit = false,
                LastPitStopDuration = 24.5,
                EnergyPercent = Math.Clamp(85.0 + Math.Sin(t * 0.4) * 12.0, 0, 100),
                Stint = new StintInfo
                {
                    Laps = (int)(t / lapLength) % 15,
                    Duration = TimeSpan.FromSeconds(t),
                    FuelConsumed = 65.0 - currentFuel,
                    FuelPerLap = fuelPerLap,
                },

                // DELTA
                LiveDeltaSeconds = liveDelta,
                IsLapValid = true,
                Sectors = new SectorData
                {
                    CurrentSector = curSector,
                    S1 = lapSec >= 30 ? TimeSpan.FromSeconds(30.120) : null,
                    BestS1 = TimeSpan.FromSeconds(29.850),
                    S2 = lapSec >= 62 ? TimeSpan.FromSeconds(32.450) : null,
                    BestS2 = TimeSpan.FromSeconds(32.100),
                    S3 = null,
                    BestS3 = TimeSpan.FromSeconds(29.638),
                },
                Gaps = new GapData
                {
                    AheadName = "M. Verstappen",
                    AheadGapSeconds = Math.Max(0.5, 1.8 + Math.Sin(t * 0.1) * 0.6),
                    BehindName = "L. Hamilton",
                    BehindGapSeconds = Math.Max(0.5, 2.3 + Math.Cos(t * 0.1) * 0.5),
                },

                // SESSION
                SessionTypeName = "RACE",
                TrackNameWithConfig = "Circuit de Spa-Francorchamps",
                CarModel = "Ferrari 499P Hypercar",
                AirTemp = 21.0,
                RoadTemp = 32.5,
                RainIntensity = 0.0,
                FlagYellow = isYellow,
                FlagName = isYellow ? "Yellow" : null,
                TCLevel = 4,
                ABSLevel = 3,
                BrakeBias = 54.0,
                EngineMap = 2,
                TCActive = braking,
                ABSActive = false,
                PitLimiterOn = false,
                WaterTemp = 87.0 + Math.Sin(t * 0.05) * 3,
                OilTemp = 102.0 + Math.Sin(t * 0.05) * 4,
                DamagePercent = 0,
            };
        }
    }
}
