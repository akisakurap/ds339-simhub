using System;

namespace SimHubDS339
{
    /// <summary>
    /// 現在のスティントの計測結果スナップショット。
    /// </summary>
    internal sealed class StintInfo
    {
        /// <summary>スティント中の周回数</summary>
        public int Laps { get; init; }

        /// <summary>スティント経過時間</summary>
        public TimeSpan Duration { get; init; }

        /// <summary>スティント中の燃料消費量 (L)</summary>
        public double? FuelConsumed { get; init; }

        /// <summary>スティント中の 1 周あたり平均消費量 (L/lap)</summary>
        public double? FuelPerLap { get; init; }
    }

    /// <summary>
    /// レース中のピットアウトを契機に今スティントの周回数、経過時間、燃料消費を追跡・計測するクラス。
    /// </summary>
    internal sealed class StintTracker
    {
        private bool _wasInPitLane = true;
        private bool _hasStartedStint = false;
        private DateTime _stintStartTime = DateTime.UtcNow;
        private int _startCompletedLaps = 0;
        private double? _startFuel = null;
        private string? _lastSessionType = null;
        private bool _lastGameRunning = false;

        /// <summary>
        /// 最新のテレメトリデータをもとにスティント状態を更新し、現在のスティント情報を返す。
        /// </summary>
        /// <param name="gameRunning">ゲーム実行中フラグ</param>
        /// <param name="sessionType">セッション種別名</param>
        /// <param name="isSessionRestart">セッションリスタートフラグ</param>
        /// <param name="isInPitLane">現在ピットレーン走行中フラグ</param>
        /// <param name="completedLaps">完了周回数</param>
        /// <param name="fuel">現在残燃料</param>
        /// <returns>計測されたスティント情報</returns>
        public StintInfo Update(
            bool gameRunning,
            string? sessionType,
            bool isSessionRestart,
            bool isInPitLane,
            int? completedLaps,
            double? fuel)
        {
            // 1. リセット条件の判定
            // ゲーム未起動→起動、セッション種別の変化、またはセッションリスタート
            bool needReset = (! _lastGameRunning && gameRunning) ||
                             (_lastSessionType != sessionType) ||
                             isSessionRestart;

            if (needReset)
            {
                Reset(completedLaps ?? 0, fuel, isInPitLane);
            }

            _lastGameRunning = gameRunning;
            _lastSessionType = sessionType;

            // 2. ピットレーン退出検知 (true -> false)
            if (_wasInPitLane && !isInPitLane)
            {
                // 新しいスティント開始
                _hasStartedStint = true;
                _stintStartTime = DateTime.UtcNow;
                _startCompletedLaps = completedLaps ?? 0;
                _startFuel = fuel;
            }
            else if (!_hasStartedStint && !isInPitLane && gameRunning)
            {
                // グリッドスタート等で最初からピットレーン外にいる場合
                _hasStartedStint = true;
                _stintStartTime = DateTime.UtcNow;
                _startCompletedLaps = completedLaps ?? 0;
                _startFuel = fuel;
            }

            _wasInPitLane = isInPitLane;

            // 3. スティント情報の算出
            if (!_hasStartedStint)
            {
                return new StintInfo
                {
                    Laps = 0,
                    Duration = TimeSpan.Zero,
                    FuelConsumed = null,
                    FuelPerLap = null,
                };
            }

            int laps = Math.Max(0, (completedLaps ?? 0) - _startCompletedLaps);
            var duration = DateTime.UtcNow - _stintStartTime;
            if (duration < TimeSpan.Zero) duration = TimeSpan.Zero;

            double? consumed = null;
            if (_startFuel.HasValue && fuel.HasValue)
            {
                double diff = _startFuel.Value - fuel.Value;
                // 給油された場合などは負になる可能性があるので 0 以上にクランプ
                consumed = Math.Max(0, diff);
            }

            double? fuelPerLap = null;
            if (consumed.HasValue && laps > 0)
            {
                fuelPerLap = consumed.Value / laps;
            }

            return new StintInfo
            {
                Laps = laps,
                Duration = duration,
                FuelConsumed = consumed,
                FuelPerLap = fuelPerLap,
            };
        }

        /// <summary>
        /// スティント計測状態を明示的に初期化する。
        /// </summary>
        public void Reset(int completedLaps, double? fuel, bool isInPitLane)
        {
            _wasInPitLane = isInPitLane;
            _hasStartedStint = !isInPitLane;
            _stintStartTime = DateTime.UtcNow;
            _startCompletedLaps = completedLaps;
            _startFuel = fuel;
        }
    }
}
