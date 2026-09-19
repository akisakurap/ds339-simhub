using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SimHubDS339
{
    /// <summary>
    /// SimHub Property Server から購読するすべてのプロパティ名定数。
    /// </summary>
    internal static class SimHubProperties
    {
        // === 既存 / MAIN ===
        public const string GameRunning = "dcp.GameRunning";
        public const string GameName = "dcp.GameName";
        public const string SpeedKmh = "dcp.gd.SpeedKmh";
        public const string Rpms = "dcp.gd.Rpms";
        public const string MaxRpm = "dcp.gd.MaxRpm";
        public const string RedlineRpm = "dcp.gd.CarSettings_CurrentGearRedLineRPM";
        public const string Gear = "dcp.gd.Gear";
        public const string Throttle = "dcp.gd.Throttle";
        public const string Brake = "dcp.gd.Brake";
        public const string CarModel = "dcp.gd.CarModel";
        public const string TrackName = "dcp.gd.TrackName";
        public const string CurrentLap = "dcp.gd.CurrentLap";
        public const string TotalLaps = "dcp.gd.TotalLaps";
        public const string Position = "dcp.gd.Position";
        public const string OpponentsCount = "dcp.gd.OpponentsCount";
        public const string CurrentLapTime = "dcp.gd.CurrentLapTime";
        public const string LastLapTime = "dcp.gd.LastLapTime";
        public const string BestLapTime = "dcp.gd.BestLapTime";
        public const string FuelPercent = "dcp.gd.FuelPercent";
        public const string IsSessionRestart = "dcp.gd.IsSessionRestart";

        // === TYRES ===
        public const string TyreTempFL = "dcp.gd.TyreTemperatureFrontLeft";
        public const string TyreTempFR = "dcp.gd.TyreTemperatureFrontRight";
        public const string TyreTempRL = "dcp.gd.TyreTemperatureRearLeft";
        public const string TyreTempRR = "dcp.gd.TyreTemperatureRearRight";

        public const string TyreTempFL_Inner = "dcp.gd.TyreTemperatureFrontLeftInner";
        public const string TyreTempFL_Middle = "dcp.gd.TyreTemperatureFrontLeftMiddle";
        public const string TyreTempFL_Outer = "dcp.gd.TyreTemperatureFrontLeftOuter";
        public const string TyreTempFR_Inner = "dcp.gd.TyreTemperatureFrontRightInner";
        public const string TyreTempFR_Middle = "dcp.gd.TyreTemperatureFrontRightMiddle";
        public const string TyreTempFR_Outer = "dcp.gd.TyreTemperatureFrontRightOuter";
        public const string TyreTempRL_Inner = "dcp.gd.TyreTemperatureRearLeftInner";
        public const string TyreTempRL_Middle = "dcp.gd.TyreTemperatureRearLeftMiddle";
        public const string TyreTempRL_Outer = "dcp.gd.TyreTemperatureRearLeftOuter";
        public const string TyreTempRR_Inner = "dcp.gd.TyreTemperatureRearRightInner";
        public const string TyreTempRR_Middle = "dcp.gd.TyreTemperatureRearRightMiddle";
        public const string TyreTempRR_Outer = "dcp.gd.TyreTemperatureRearRightOuter";

        public const string TyrePressureFL = "dcp.gd.TyrePressureFrontLeft";
        public const string TyrePressureFR = "dcp.gd.TyrePressureFrontRight";
        public const string TyrePressureRL = "dcp.gd.TyrePressureRearLeft";
        public const string TyrePressureRR = "dcp.gd.TyrePressureRearRight";
        public const string TyrePressureUnit = "dcp.gd.TyrePressureUnit";

        public const string TyreWearFL = "dcp.gd.TyreWearFrontLeft";
        public const string TyreWearFR = "dcp.gd.TyreWearFrontRight";
        public const string TyreWearRL = "dcp.gd.TyreWearRearLeft";
        public const string TyreWearRR = "dcp.gd.TyreWearRearRight";
        public const string LastLapTyreWearFL = "dcp.gd.LastLapTyreWearFrontLeft";
        public const string LastLapTyreWearFR = "dcp.gd.LastLapTyreWearFrontRight";
        public const string LastLapTyreWearRL = "dcp.gd.LastLapTyreWearRearLeft";
        public const string LastLapTyreWearRR = "dcp.gd.LastLapTyreWearRearRight";

        public const string BrakeTempFL = "dcp.gd.BrakeTemperatureFrontLeft";
        public const string BrakeTempFR = "dcp.gd.BrakeTemperatureFrontRight";
        public const string BrakeTempRL = "dcp.gd.BrakeTemperatureRearLeft";
        public const string BrakeTempRR = "dcp.gd.BrakeTemperatureRearRight";
        public const string TemperatureUnit = "dcp.gd.TemperatureUnit";
        public const string TyreDirtFL = "dcp.gd.TyreDirtFrontLeft";

        // LMU raw タイヤコンパウンド名
        public const string LmuFrontTireCompound = "DataCorePlugin.GameRawData.CurrentPlayerTelemetry.mFrontTireCompoundName";
        public const string LmuRearTireCompound = "DataCorePlugin.GameRawData.CurrentPlayerTelemetry.mRearTireCompoundName";

        // === FUEL ===
        public const string Fuel = "dcp.gd.Fuel";
        public const string MaxFuel = "dcp.gd.MaxFuel";
        public const string FuelUnit = "dcp.gd.FuelUnit";
        public const string Fuel_LitersPerLap = "DataCorePlugin.Computed.Fuel_LitersPerLap";
        public const string Fuel_RemainingLaps = "DataCorePlugin.Computed.Fuel_RemainingLaps";
        public const string Fuel_RemainingTime = "DataCorePlugin.Computed.Fuel_RemainingTime";
        public const string Fuel_LastLapConsumption = "DataCorePlugin.Computed.Fuel_LastLapConsumption";
        public const string RemainingLaps = "dcp.gd.RemainingLaps";
        public const string SessionTimeLeft = "dcp.gd.SessionTimeLeft";
        public const string CompletedLaps = "dcp.gd.CompletedLaps";
        public const string StintOdo = "dcp.gd.StintOdo";
        public const string IsInPit = "dcp.gd.IsInPit";
        public const string IsInPitLane = "dcp.gd.IsInPitLane";
        public const string LastPitStopDuration = "dcp.gd.LastPitStopDuration";
        public const string IsInPitSince = "dcp.gd.IsInPitSince";

        // LMU raw ハイブリッド / Virtual Energy
        public const string LmuBatteryChargeFraction = "DataCorePlugin.GameRawData.CurrentPlayerTelemetry.mBatteryChargeFraction";
        public const string LmuElectricBoostMotorState = "DataCorePlugin.GameRawData.CurrentPlayerTelemetry.mElectricBoostMotorState";
        public const string LmuVirtualEnergy = "DataCorePlugin.GameRawData.CurrentPlayerTelemetry.mVirtualEnergy";

        // === DELTA ===
        public const string SessionBestLiveDeltaSeconds = "PersistantTrackerPlugin.SessionBestLiveDeltaSeconds";
        public const string SessionBestLiveDeltaProgressSeconds = "PersistantTrackerPlugin.SessionBestLiveDeltaProgressSeconds";
        public const string AllTimeBestLiveDeltaSeconds = "PersistantTrackerPlugin.AllTimeBestLiveDeltaSeconds";
        public const string AllTimeBest = "dcp.gd.AllTimeBest";
        public const string CurrentSectorIndex = "dcp.gd.CurrentSectorIndex";
        public const string LastSectorTime = "dcp.gd.LastSectorTime";

        // SimHub セクター
        public const string Sector1LastLapTime = "DataCorePlugin.GameData.Sector1LastLapTime";
        public const string Sector2LastLapTime = "DataCorePlugin.GameData.Sector2LastLapTime";
        public const string Sector3LastLapTime = "DataCorePlugin.GameData.Sector3LastLapTime";
        public const string Sector1BestLapTime = "DataCorePlugin.GameData.Sector1BestLapTime";
        public const string Sector2BestLapTime = "DataCorePlugin.GameData.Sector2BestLapTime";
        public const string Sector3BestLapTime = "DataCorePlugin.GameData.Sector3BestLapTime";

        // SimHub ギャップ
        public const string DriverAhead_00_Gap = "PersistantTrackerPlugin.DriverAhead_00_Gap";
        public const string DriverAhead_00_Name = "PersistantTrackerPlugin.DriverAhead_00_Name";
        public const string DriverBehind_00_Gap = "PersistantTrackerPlugin.DriverBehind_00_Gap";
        public const string DriverBehind_00_Name = "PersistantTrackerPlugin.DriverBehind_00_Name";

        // LMU raw ギャップ
        public const string LmuTimeBehindNext = "DataCorePlugin.GameRawData.CurrentPlayer.mTimeBehindNext";
        public const string LmuTimeBehindLeader = "DataCorePlugin.GameRawData.CurrentPlayer.mTimeBehindLeader";
        public const string LmuLapsBehindNext = "DataCorePlugin.GameRawData.CurrentPlayer.mLapsBehindNext";
        public const string LmuPlace = "DataCorePlugin.GameRawData.CurrentPlayer.mPlace";

        // LMU raw セクター (累積秒)
        public const string LmuCurSector1 = "DataCorePlugin.GameRawData.CurrentPlayer.mCurSector1";
        public const string LmuCurSector2 = "DataCorePlugin.GameRawData.CurrentPlayer.mCurSector2";
        public const string LmuLastSector1 = "DataCorePlugin.GameRawData.CurrentPlayer.mLastSector1";
        public const string LmuLastSector2 = "DataCorePlugin.GameRawData.CurrentPlayer.mLastSector2";
        public const string LmuBestSector1 = "DataCorePlugin.GameRawData.CurrentPlayer.mBestSector1";
        public const string LmuBestSector2 = "DataCorePlugin.GameRawData.CurrentPlayer.mBestSector2";
        public const string LmuLastLapTime = "DataCorePlugin.GameRawData.CurrentPlayer.mLastLapTime";
        public const string LmuBestLapTime = "DataCorePlugin.GameRawData.CurrentPlayer.mBestLapTime";

        public const string PlayerClassOpponentsCount = "dcp.gd.PlayerClassOpponentsCount";
        public const string IsLapValid = "dcp.gd.IsLapValid";
        public const string LapInvalidated = "dcp.gd.LapInvalidated";

        // === SESSION ===
        public const string SessionTypeName = "dcp.gd.SessionTypeName";
        public const string TrackNameWithConfig = "dcp.gd.TrackNameWithConfig";
        public const string CarClass = "dcp.gd.CarClass";
        public const string AirTemperature = "dcp.gd.AirTemperature";
        public const string RoadTemperature = "dcp.gd.RoadTemperature";
        public const string LmuTrackTemp = "DataCorePlugin.GameRawData.Scoring.mScoringInfo.mTrackTemp";
        public const string LmuRaining = "DataCorePlugin.GameRawData.Scoring.mScoringInfo.mRaining";
        public const string LmuDarkCloud = "DataCorePlugin.GameRawData.Scoring.mScoringInfo.mDarkCloud";
        public const string LmuAmbientTemp = "DataCorePlugin.GameRawData.Scoring.mScoringInfo.mAmbientTemp";
        public const string LmuAvgPathWetness = "DataCorePlugin.GameRawData.Scoring.mScoringInfo.mAvgPathWetness";

        // フラグ
        public const string Flag_Name = "dcp.gd.Flag_Name";
        public const string Flag_Yellow = "dcp.gd.Flag_Yellow";
        public const string Flag_Blue = "dcp.gd.Flag_Blue";
        public const string Flag_Green = "dcp.gd.Flag_Green";
        public const string Flag_Checkered = "dcp.gd.Flag_Checkered";
        public const string Flag_Black = "dcp.gd.Flag_Black";
        public const string Flag_White = "dcp.gd.Flag_White";

        // 車両制御・設定
        public const string TCLevel = "dcp.gd.TCLevel";
        public const string ABSLevel = "dcp.gd.ABSLevel";
        public const string BrakeBias = "dcp.gd.BrakeBias";
        public const string EngineMap = "dcp.gd.EngineMap";
        public const string TCActive = "dcp.gd.TCActive";
        public const string ABSActive = "dcp.gd.ABSActive";
        public const string PitLimiterOn = "dcp.gd.PitLimiterOn";

        // 車両状態
        public const string WaterTemperature = "dcp.gd.WaterTemperature";
        public const string OilTemperature = "dcp.gd.OilTemperature";
        public const string CarDamagesAvg = "dcp.gd.CarDamagesAvg";
        public const string CarDamagesMax = "dcp.gd.CarDamagesMax";

        /// <summary>
        /// 全候補プロパティの一覧 (重複なし)。
        /// </summary>
        public static readonly string[] AllCandidateProperties = new[]
        {
            GameRunning, GameName, SpeedKmh, Rpms, MaxRpm, RedlineRpm, Gear, Throttle, Brake,
            CarModel, TrackName, CurrentLap, TotalLaps, Position, OpponentsCount,
            CurrentLapTime, LastLapTime, BestLapTime, FuelPercent, IsSessionRestart,
            TyreTempFL, TyreTempFR, TyreTempRL, TyreTempRR,
            TyreTempFL_Inner, TyreTempFL_Middle, TyreTempFL_Outer,
            TyreTempFR_Inner, TyreTempFR_Middle, TyreTempFR_Outer,
            TyreTempRL_Inner, TyreTempRL_Middle, TyreTempRL_Outer,
            TyreTempRR_Inner, TyreTempRR_Middle, TyreTempRR_Outer,
            TyrePressureFL, TyrePressureFR, TyrePressureRL, TyrePressureRR, TyrePressureUnit,
            TyreWearFL, TyreWearFR, TyreWearRL, TyreWearRR,
            LastLapTyreWearFL, LastLapTyreWearFR, LastLapTyreWearRL, LastLapTyreWearRR,
            BrakeTempFL, BrakeTempFR, BrakeTempRL, BrakeTempRR,
            TemperatureUnit, TyreDirtFL, LmuFrontTireCompound, LmuRearTireCompound,
            Fuel, MaxFuel, FuelUnit, Fuel_LitersPerLap, Fuel_RemainingLaps, Fuel_RemainingTime, Fuel_LastLapConsumption,
            RemainingLaps, SessionTimeLeft, CompletedLaps, StintOdo, IsInPit, IsInPitLane, LastPitStopDuration, IsInPitSince,
            LmuBatteryChargeFraction, LmuElectricBoostMotorState, LmuVirtualEnergy,
            SessionBestLiveDeltaSeconds, SessionBestLiveDeltaProgressSeconds, AllTimeBestLiveDeltaSeconds, AllTimeBest,
            CurrentSectorIndex, LastSectorTime,
            Sector1LastLapTime, Sector2LastLapTime, Sector3LastLapTime,
            Sector1BestLapTime, Sector2BestLapTime, Sector3BestLapTime,
            DriverAhead_00_Gap, DriverAhead_00_Name, DriverBehind_00_Gap, DriverBehind_00_Name,
            LmuTimeBehindNext, LmuTimeBehindLeader, LmuLapsBehindNext, LmuPlace,
            LmuCurSector1, LmuCurSector2, LmuLastSector1, LmuLastSector2, LmuBestSector1, LmuBestSector2,
            LmuLastLapTime, LmuBestLapTime,
            PlayerClassOpponentsCount, IsLapValid, LapInvalidated,
            SessionTypeName, TrackNameWithConfig, CarClass,
            AirTemperature, RoadTemperature, LmuTrackTemp,
            LmuRaining, LmuDarkCloud, LmuAmbientTemp, LmuAvgPathWetness,
            Flag_Name, Flag_Yellow, Flag_Blue, Flag_Green, Flag_Checkered, Flag_Black, Flag_White,
            TCLevel, ABSLevel, BrakeBias, EngineMap, TCActive, ABSActive, PitLimiterOn,
            WaterTemperature, OilTemperature, CarDamagesAvg, CarDamagesMax
        }.Distinct().ToArray();
    }

    /// <summary>
    /// 単一輪のタイヤおよびブレーキ情報。
    /// </summary>
    internal sealed class TyreWheelData
    {
        /// <summary>タイヤ平均温度</summary>
        public double? Temp { get; init; }
        /// <summary>タイヤ内側温度</summary>
        public double? TempInner { get; init; }
        /// <summary>タイヤ中央温度</summary>
        public double? TempMiddle { get; init; }
        /// <summary>タイヤ外側温度</summary>
        public double? TempOuter { get; init; }
        /// <summary>タイヤ空気圧</summary>
        public double? Pressure { get; init; }
        /// <summary>タイヤ摩耗率 (0..100 %)</summary>
        public double? Wear { get; init; }
        /// <summary>直前ラップでの摩耗量 (%)</summary>
        public double? LastLapWear { get; init; }
        /// <summary>ブレーキ温度</summary>
        public double? BrakeTemp { get; init; }
    }

    /// <summary>
    /// セクタータイム情報。
    /// </summary>
    internal sealed class SectorData
    {
        public TimeSpan? S1 { get; init; }
        public TimeSpan? S2 { get; init; }
        public TimeSpan? S3 { get; init; }
        public TimeSpan? BestS1 { get; init; }
        public TimeSpan? BestS2 { get; init; }
        public TimeSpan? BestS3 { get; init; }
        /// <summary>現在走行中のセクター (1, 2, 3)</summary>
        public int? CurrentSector { get; init; }
    }

    /// <summary>
    /// 前後の車とのギャップ情報。
    /// </summary>
    internal sealed class GapData
    {
        public string? AheadName { get; init; }
        public double? AheadGapSeconds { get; init; }
        public string? BehindName { get; init; }
        public double? BehindGapSeconds { get; init; }
    }

    /// <summary>
    /// 描画に必要な SimHub の値のスナップショット。
    /// </summary>
    internal sealed class Telemetry
    {
        public static readonly string[] SubscribedProperties = SimHubProperties.AllCandidateProperties;

        // === 基本・共通 ===
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
        public TimeSpan? AllTimeBest { get; init; }
        /// <summary>0..100、不明なら null</summary>
        public double? FuelPercent { get; init; }
        public bool IsSessionRestart { get; init; }

        // === TYRES ===
        public TyreWheelData FrontLeft { get; init; } = new();
        public TyreWheelData FrontRight { get; init; } = new();
        public TyreWheelData RearLeft { get; init; } = new();
        public TyreWheelData RearRight { get; init; } = new();
        public string PressureUnit { get; init; } = "kPa";
        public string TemperatureUnit { get; init; } = "°C";
        public string? FrontCompound { get; init; }
        public string? RearCompound { get; init; }

        // === FUEL ===
        public double? Fuel { get; init; }
        public double? MaxFuel { get; init; }
        public string FuelUnit { get; init; } = "L";
        public double? FuelPerLap { get; init; }
        public double? FuelRemainingLaps { get; init; }
        public TimeSpan? FuelRemainingTime { get; init; }
        public double? FuelLastLapConsumption { get; init; }
        public int? RemainingLaps { get; init; }
        public TimeSpan? SessionTimeLeft { get; init; }
        public int? CompletedLaps { get; init; }
        public bool IsInPit { get; init; }
        public bool IsInPitLane { get; init; }
        public double? LastPitStopDuration { get; init; }
        public StintInfo? Stint { get; init; }
        /// <summary>ハイブリッド充填率 (0..100 %) または Virtual Energy (%)</summary>
        public double? EnergyPercent { get; init; }

        // === DELTA ===
        /// <summary>生デルタ (秒、プラスは遅い、マイナスは速い)</summary>
        public double? LiveDeltaSeconds { get; init; }
        public SectorData Sectors { get; init; } = new();
        public GapData Gaps { get; init; } = new();
        public bool IsLapValid { get; init; } = true;

        // === SESSION ===
        public string? SessionTypeName { get; init; }
        public string? TrackNameWithConfig { get; init; }
        public string? CarClass { get; init; }
        public double? AirTemp { get; init; }
        public double? RoadTemp { get; init; }
        public double? RainIntensity { get; init; } // 0..1
        public string? FlagName { get; init; }
        public bool FlagYellow { get; init; }
        public bool FlagBlue { get; init; }
        public bool FlagGreen { get; init; }
        public bool FlagCheckered { get; init; }
        public bool FlagBlack { get; init; }
        public bool FlagWhite { get; init; }
        public int? TCLevel { get; init; }
        public int? ABSLevel { get; init; }
        public double? BrakeBias { get; init; }
        public int? EngineMap { get; init; }
        public bool TCActive { get; init; }
        public bool ABSActive { get; init; }
        public bool PitLimiterOn { get; init; }
        public double? WaterTemp { get; init; }
        public double? OilTemp { get; init; }
        public double? DamagePercent { get; init; }

        /// <summary>
        /// SimHub クライアントアダプタから Telemetry インスタンスを構築する。
        /// </summary>
        public static Telemetry From(SimHubPropertyClientAdapter client, StintTracker? stintTracker = null)
        {
            // 基本
            bool gameRunning = client.GetBool(SimHubProperties.GameRunning);
            string? sessionType = client.GetString(SimHubProperties.SessionTypeName);
            bool isRestart = client.GetBool(SimHubProperties.IsSessionRestart);
            bool inPitLane = client.GetBool(SimHubProperties.IsInPitLane);
            int? completedLaps = client.GetInt(SimHubProperties.CompletedLaps);
            double? fuelVal = client.GetDouble(SimHubProperties.Fuel);

            // スティント更新
            StintInfo? stint = stintTracker?.Update(gameRunning, sessionType, isRestart, inPitLane, completedLaps, fuelVal);

            // 温度単位 & 空気圧単位
            string tempUnit = client.GetString(SimHubProperties.TemperatureUnit) ?? "°C";
            string pressUnit = client.GetString(SimHubProperties.TyrePressureUnit) ?? "kPa";
            string fuelUnit = client.GetString(SimHubProperties.FuelUnit) ?? "L";

            // タイヤ 4 輪データ
            var fl = new TyreWheelData
            {
                Temp = client.GetDouble(SimHubProperties.TyreTempFL),
                TempInner = client.GetDouble(SimHubProperties.TyreTempFL_Inner),
                TempMiddle = client.GetDouble(SimHubProperties.TyreTempFL_Middle),
                TempOuter = client.GetDouble(SimHubProperties.TyreTempFL_Outer),
                Pressure = client.GetDouble(SimHubProperties.TyrePressureFL),
                Wear = client.GetDouble(SimHubProperties.TyreWearFL),
                LastLapWear = client.GetDouble(SimHubProperties.LastLapTyreWearFL),
                BrakeTemp = client.GetDouble(SimHubProperties.BrakeTempFL),
            };
            var fr = new TyreWheelData
            {
                Temp = client.GetDouble(SimHubProperties.TyreTempFR),
                TempInner = client.GetDouble(SimHubProperties.TyreTempFR_Inner),
                TempMiddle = client.GetDouble(SimHubProperties.TyreTempFR_Middle),
                TempOuter = client.GetDouble(SimHubProperties.TyreTempFR_Outer),
                Pressure = client.GetDouble(SimHubProperties.TyrePressureFR),
                Wear = client.GetDouble(SimHubProperties.TyreWearFR),
                LastLapWear = client.GetDouble(SimHubProperties.LastLapTyreWearFR),
                BrakeTemp = client.GetDouble(SimHubProperties.BrakeTempFR),
            };
            var rl = new TyreWheelData
            {
                Temp = client.GetDouble(SimHubProperties.TyreTempRL),
                TempInner = client.GetDouble(SimHubProperties.TyreTempRL_Inner),
                TempMiddle = client.GetDouble(SimHubProperties.TyreTempRL_Middle),
                TempOuter = client.GetDouble(SimHubProperties.TyreTempRL_Outer),
                Pressure = client.GetDouble(SimHubProperties.TyrePressureRL),
                Wear = client.GetDouble(SimHubProperties.TyreWearRL),
                LastLapWear = client.GetDouble(SimHubProperties.LastLapTyreWearRL),
                BrakeTemp = client.GetDouble(SimHubProperties.BrakeTempRL),
            };
            var rr = new TyreWheelData
            {
                Temp = client.GetDouble(SimHubProperties.TyreTempRR),
                TempInner = client.GetDouble(SimHubProperties.TyreTempRR_Inner),
                TempMiddle = client.GetDouble(SimHubProperties.TyreTempRR_Middle),
                TempOuter = client.GetDouble(SimHubProperties.TyreTempRR_Outer),
                Pressure = client.GetDouble(SimHubProperties.TyrePressureRR),
                Wear = client.GetDouble(SimHubProperties.TyreWearRR),
                LastLapWear = client.GetDouble(SimHubProperties.LastLapTyreWearRR),
                BrakeTemp = client.GetDouble(SimHubProperties.BrakeTempRR),
            };

            // セクタータイムの解析とフォールバック
            TimeSpan? s1 = client.GetTimeSpan(SimHubProperties.Sector1LastLapTime);
            TimeSpan? s2 = client.GetTimeSpan(SimHubProperties.Sector2LastLapTime);
            TimeSpan? s3 = client.GetTimeSpan(SimHubProperties.Sector3LastLapTime);

            // LMU raw からのセクターフォールバック (累積秒: S2=cum2-cum1, S3=lap-cum2)
            double? lmuS1 = client.GetDouble(SimHubProperties.LmuLastSector1);
            double? lmuS2 = client.GetDouble(SimHubProperties.LmuLastSector2);
            double? lmuLap = client.GetDouble(SimHubProperties.LmuLastLapTime);

            if (s1 == null && lmuS1.HasValue && lmuS1.Value > 0)
            {
                s1 = TimeSpan.FromSeconds(lmuS1.Value);
            }
            if (s2 == null && lmuS1.HasValue && lmuS2.HasValue && lmuS2.Value > lmuS1.Value)
            {
                s2 = TimeSpan.FromSeconds(lmuS2.Value - lmuS1.Value);
            }
            if (s3 == null && lmuS2.HasValue && lmuLap.HasValue && lmuLap.Value > lmuS2.Value)
            {
                s3 = TimeSpan.FromSeconds(lmuLap.Value - lmuS2.Value);
            }

            var sectors = new SectorData
            {
                S1 = s1,
                S2 = s2,
                S3 = s3,
                BestS1 = client.GetTimeSpan(SimHubProperties.Sector1BestLapTime),
                BestS2 = client.GetTimeSpan(SimHubProperties.Sector2BestLapTime),
                BestS3 = client.GetTimeSpan(SimHubProperties.Sector3BestLapTime),
                CurrentSector = client.GetInt(SimHubProperties.CurrentSectorIndex) is { } idx ? idx + 1 : null,
            };

            // ギャップの解析 (LMU raw → SimHub のフォールバック)
            double? aheadGap = client.GetDouble(SimHubProperties.LmuTimeBehindNext)
                               ?? client.GetDouble(SimHubProperties.DriverAhead_00_Gap);
            string? aheadName = client.GetString(SimHubProperties.DriverAhead_00_Name);
            double? behindGap = client.GetDouble(SimHubProperties.DriverBehind_00_Gap);
            string? behindName = client.GetString(SimHubProperties.DriverBehind_00_Name);

            var gaps = new GapData
            {
                AheadGapSeconds = aheadGap,
                AheadName = aheadName,
                BehindGapSeconds = behindGap,
                BehindName = behindName,
            };

            // デルタ (SessionBestLiveDeltaSeconds → Progress)
            double? liveDelta = client.GetDouble(SimHubProperties.SessionBestLiveDeltaSeconds)
                                ?? client.GetDouble(SimHubProperties.SessionBestLiveDeltaProgressSeconds)
                                ?? client.GetDouble(SimHubProperties.AllTimeBestLiveDeltaSeconds);

            // ハイブリッド / Energy (0..100)
            double? energy = null;
            if (client.GetDouble(SimHubProperties.LmuVirtualEnergy) is { } ve)
            {
                energy = ve <= 1.0 ? ve * 100.0 : ve;
            }
            else if (client.GetDouble(SimHubProperties.LmuBatteryChargeFraction) is { } bf)
            {
                energy = bf <= 1.0 ? bf * 100.0 : bf;
            }

            // 天候・路面温度
            double? roadTemp = client.GetDouble(SimHubProperties.RoadTemperature)
                               ?? client.GetDouble(SimHubProperties.LmuTrackTemp);
            double? airTemp = client.GetDouble(SimHubProperties.AirTemperature)
                              ?? client.GetDouble(SimHubProperties.LmuAmbientTemp);
            double? rain = client.GetDouble(SimHubProperties.LmuRaining)
                           ?? client.GetDouble(SimHubProperties.LmuAvgPathWetness);

            // ダメージ
            double? damage = client.GetDouble(SimHubProperties.CarDamagesAvg)
                             ?? client.GetDouble(SimHubProperties.CarDamagesMax);

            // ラップ有効性
            bool isLapValid = true;
            if (client.GetBool(SimHubProperties.LapInvalidated))
            {
                isLapValid = false;
            }
            else if (client.GetString(SimHubProperties.IsLapValid) != null)
            {
                isLapValid = client.GetBool(SimHubProperties.IsLapValid);
            }

            return new Telemetry
            {
                SimHubConnected = client.IsConnected,
                GameRunning = gameRunning,
                GameName = client.GetString(SimHubProperties.GameName),
                SpeedKmh = client.GetDouble(SimHubProperties.SpeedKmh) ?? 0,
                Rpm = client.GetDouble(SimHubProperties.Rpms) ?? 0,
                MaxRpm = client.GetDouble(SimHubProperties.MaxRpm) ?? 0,
                RedlineRpm = client.GetDouble(SimHubProperties.RedlineRpm) ?? 0,
                Gear = client.GetString(SimHubProperties.Gear) ?? "-",
                Throttle = Math.Clamp((client.GetDouble(SimHubProperties.Throttle) ?? 0) / 100.0, 0, 1),
                Brake = Math.Clamp((client.GetDouble(SimHubProperties.Brake) ?? 0) / 100.0, 0, 1),
                CarModel = client.GetString(SimHubProperties.CarModel),
                TrackName = client.GetString(SimHubProperties.TrackName),
                CurrentLap = client.GetInt(SimHubProperties.CurrentLap),
                TotalLaps = client.GetInt(SimHubProperties.TotalLaps),
                Position = client.GetInt(SimHubProperties.Position) ?? client.GetInt(SimHubProperties.LmuPlace),
                OpponentsCount = client.GetInt(SimHubProperties.OpponentsCount) ?? client.GetInt(SimHubProperties.PlayerClassOpponentsCount),
                CurrentLapTime = client.GetTimeSpan(SimHubProperties.CurrentLapTime),
                LastLapTime = client.GetTimeSpan(SimHubProperties.LastLapTime),
                BestLapTime = client.GetTimeSpan(SimHubProperties.BestLapTime),
                AllTimeBest = client.GetTimeSpan(SimHubProperties.AllTimeBest),
                FuelPercent = client.GetDouble(SimHubProperties.FuelPercent),
                IsSessionRestart = isRestart,

                // TYRES
                FrontLeft = fl,
                FrontRight = fr,
                RearLeft = rl,
                RearRight = rr,
                PressureUnit = pressUnit,
                TemperatureUnit = tempUnit,
                FrontCompound = client.GetString(SimHubProperties.LmuFrontTireCompound),
                RearCompound = client.GetString(SimHubProperties.LmuRearTireCompound),

                // FUEL
                Fuel = fuelVal,
                MaxFuel = client.GetDouble(SimHubProperties.MaxFuel),
                FuelUnit = fuelUnit,
                FuelPerLap = client.GetDouble(SimHubProperties.Fuel_LitersPerLap),
                FuelRemainingLaps = client.GetDouble(SimHubProperties.Fuel_RemainingLaps),
                FuelRemainingTime = client.GetTimeSpan(SimHubProperties.Fuel_RemainingTime),
                FuelLastLapConsumption = client.GetDouble(SimHubProperties.Fuel_LastLapConsumption),
                RemainingLaps = client.GetInt(SimHubProperties.RemainingLaps),
                SessionTimeLeft = client.GetTimeSpan(SimHubProperties.SessionTimeLeft),
                CompletedLaps = completedLaps,
                IsInPit = client.GetBool(SimHubProperties.IsInPit),
                IsInPitLane = inPitLane,
                LastPitStopDuration = client.GetDouble(SimHubProperties.LastPitStopDuration),
                Stint = stint,
                EnergyPercent = energy,

                // DELTA
                LiveDeltaSeconds = liveDelta,
                Sectors = sectors,
                Gaps = gaps,
                IsLapValid = isLapValid,

                // SESSION
                SessionTypeName = sessionType,
                TrackNameWithConfig = client.GetString(SimHubProperties.TrackNameWithConfig) ?? client.GetString(SimHubProperties.TrackName),
                CarClass = client.GetString(SimHubProperties.CarClass),
                AirTemp = airTemp,
                RoadTemp = roadTemp,
                RainIntensity = rain,
                FlagName = client.GetString(SimHubProperties.Flag_Name),
                FlagYellow = client.GetBool(SimHubProperties.Flag_Yellow),
                FlagBlue = client.GetBool(SimHubProperties.Flag_Blue),
                FlagGreen = client.GetBool(SimHubProperties.Flag_Green),
                FlagCheckered = client.GetBool(SimHubProperties.Flag_Checkered),
                FlagBlack = client.GetBool(SimHubProperties.Flag_Black),
                FlagWhite = client.GetBool(SimHubProperties.Flag_White),
                TCLevel = client.GetInt(SimHubProperties.TCLevel),
                ABSLevel = client.GetInt(SimHubProperties.ABSLevel),
                BrakeBias = client.GetDouble(SimHubProperties.BrakeBias),
                EngineMap = client.GetInt(SimHubProperties.EngineMap),
                TCActive = client.GetBool(SimHubProperties.TCActive),
                ABSActive = client.GetBool(SimHubProperties.ABSActive),
                PitLimiterOn = client.GetBool(SimHubProperties.PitLimiterOn),
                WaterTemp = client.GetDouble(SimHubProperties.WaterTemperature),
                OilTemp = client.GetDouble(SimHubProperties.OilTemperature),
                DamagePercent = damage,
            };
        }
    }

    /// <summary>SimHubPropertyClient の文字列値を型付きで読むための薄いアダプタ。</summary>
    internal sealed class SimHubPropertyClientAdapter
    {
        private readonly SimHubPropertyClient _client;

        public SimHubPropertyClientAdapter(SimHubPropertyClient client)
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
            if (s == null) return null;
            if (TimeSpan.TryParse(s, CultureInfo.InvariantCulture, out var t)) return t;
            // 数値（秒数）として渡される場合もあるため double としてフォールバック
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var sec) && sec >= 0)
            {
                return TimeSpan.FromSeconds(sec);
            }
            return null;
        }
    }
}
