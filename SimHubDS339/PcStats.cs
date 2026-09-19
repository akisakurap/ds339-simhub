using System.Diagnostics;
using System.Security.Principal;
using LibreHardwareMonitor.Hardware;
using Microsoft.Win32;

namespace SimHubDS339
{
    /// <summary>PC ステータス画面に描く値のスナップショット。取れなかった値は null。</summary>
    internal sealed class PcStats
    {
        public const int HistoryLength = 60;

        /// <summary>0..100</summary>
        public double? CpuLoad { get; init; }
        public double? CpuTemp { get; init; }
        public double? CpuClockMhz { get; init; }
        public string? GpuName { get; init; }
        /// <summary>0..100</summary>
        public double? GpuLoad { get; init; }
        public double? GpuTemp { get; init; }
        public double? VramUsedMb { get; init; }
        public double? VramTotalMb { get; init; }
        public double? RamUsedGb { get; init; }
        public double? RamTotalGb { get; init; }
        /// <summary>bytes/sec (全アダプタ合計)</summary>
        public double? NetDownBps { get; init; }
        public double? NetUpBps { get; init; }
        /// <summary>古い順。最大 <see cref="HistoryLength"/> 件。</summary>
        public IReadOnlyList<double> CpuHistory { get; init; } = Array.Empty<double>();
        public IReadOnlyList<double> GpuHistory { get; init; } = Array.Empty<double>();
        public IReadOnlyList<double> RamHistory { get; init; } = Array.Empty<double>();
    }

    /// <summary>
    /// LibreHardwareMonitor で 1 秒ごとにセンサーを読み、<see cref="Current"/> を差し替える。
    /// Update は数十 ms かかることがあるため、描画ループとは別スレッドで回す。
    /// </summary>
    internal sealed class PcStatsSampler : IDisposable
    {
        private readonly Computer _computer = new()
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsNetworkEnabled = true,
        };
        private readonly CancellationTokenSource _cts = new();
        private readonly Queue<double> _cpuHistory = new();
        private readonly Queue<double> _gpuHistory = new();
        private readonly Queue<double> _ramHistory = new();
        private PerformanceCounter? _cpuPerf;
        private double? _cpuBaseMhz;
        private Thread? _thread;
        private volatile PcStats? _current;
        private volatile bool _paused;
        private bool _warned;

        public PcStats? Current => _current;

        /// <summary>true の間はセンサーを読まない (レース中の負荷を増やさないため)。</summary>
        public bool Paused
        {
            get => _paused;
            set => _paused = value;
        }

        public static bool IsElevated
        {
            get
            {
                using var identity = WindowsIdentity.GetCurrent();
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        public void Start()
        {
            if (_thread != null) return;
            _thread = new Thread(Loop) { IsBackground = true, Name = "PcStatsSampler" };
            _thread.Start();
        }

        private void Loop()
        {
            try
            {
                _computer.Open();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PC][WARN] hardware monitor init failed: {ex.Message}");
                return;
            }
            InitClockFallback();

            while (!_cts.IsCancellationRequested)
            {
                if (!_paused)
                {
                    try
                    {
                        _current = Sample();
                    }
                    catch (Exception ex) when (!_warned)
                    {
                        _warned = true;
                        Console.WriteLine($"[PC][WARN] sensor read failed: {ex.Message}");
                    }
                    catch
                    {
                        // 2 回目以降は黙って次の周期で再試行する
                    }
                }

                if (_cts.Token.WaitHandle.WaitOne(1000)) break;
            }
        }

        private PcStats Sample()
        {
            IHardware? cpu = null, memory = null;
            var gpus = new List<IHardware>();
            var nics = new List<IHardware>();
            foreach (var hw in _computer.Hardware)
            {
                hw.Update();
                foreach (var sub in hw.SubHardware) sub.Update();

                switch (hw.HardwareType)
                {
                    case HardwareType.Cpu: cpu ??= hw; break;
                    // "Virtual Memory" (コミット量) ではなく物理メモリ ("Total Memory") を使う
                    case HardwareType.Memory when !hw.Name.Contains("Virtual", StringComparison.OrdinalIgnoreCase): memory ??= hw; break;
                    case HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel: gpus.Add(hw); break;
                    case HardwareType.Network: nics.Add(hw); break;
                }
            }

            // iGPU より dGPU を優先する
            var gpu = gpus.OrderBy(g => g.HardwareType == HardwareType.GpuIntel ? 1 : 0).FirstOrDefault();

            double? cpuLoad = Find(cpu, SensorType.Load, "CPU Total");
            double? gpuLoad = Find(gpu, SensorType.Load, "GPU Core") ?? Find(gpu, SensorType.Load, "D3D 3D");
            Push(_cpuHistory, cpuLoad);
            Push(_gpuHistory, gpuLoad);

            double? ramUsed = Find(memory, SensorType.Data, "Memory Used");
            double? ramAvail = Find(memory, SensorType.Data, "Memory Available");
            Push(_ramHistory, Find(memory, SensorType.Load, "Memory"));

            return new PcStats
            {
                CpuLoad = cpuLoad,
                CpuTemp = Find(cpu, SensorType.Temperature, "CPU Package") ?? Max(cpu, SensorType.Temperature, n => n.StartsWith("Core", StringComparison.Ordinal)),
                CpuClockMhz = Max(cpu, SensorType.Clock, n => n.Contains("Core", StringComparison.Ordinal) && !n.Contains("Bus", StringComparison.Ordinal)) ?? FallbackClockMhz(),
                GpuName = gpu?.Name,
                GpuLoad = gpuLoad,
                GpuTemp = Find(gpu, SensorType.Temperature, "GPU Core"),
                VramUsedMb = Find(gpu, SensorType.SmallData, "GPU Memory Used"),
                VramTotalMb = Find(gpu, SensorType.SmallData, "GPU Memory Total"),
                RamUsedGb = ramUsed,
                RamTotalGb = ramUsed.HasValue && ramAvail.HasValue ? ramUsed + ramAvail : null,
                NetDownBps = Sum(nics, SensorType.Throughput, "Download Speed"),
                NetUpBps = Sum(nics, SensorType.Throughput, "Upload Speed"),
                CpuHistory = _cpuHistory.ToArray(),
                GpuHistory = _gpuHistory.ToArray(),
                RamHistory = _ramHistory.ToArray(),
            };
        }

        /// <summary>
        /// PawnIO なしでは LHM からクロックが取れないため、タスク マネージャーの「速度」と同じく
        /// 「定格クロック × % Processor Performance」で実効クロックを推定する (管理者権限もドライバも不要)。
        /// </summary>
        private void InitClockFallback()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                if (key?.GetValue("~MHz") is int mhz && mhz > 0) _cpuBaseMhz = mhz;
                _cpuPerf = new PerformanceCounter("Processor Information", "% Processor Performance", "_Total", readOnly: true);
                _cpuPerf.NextValue(); // 初回は常に 0 を返すので読み捨てる
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PC][WARN] CPU clock counter unavailable: {ex.Message}");
                _cpuPerf?.Dispose();
                _cpuPerf = null;
            }
        }

        private double? FallbackClockMhz()
        {
            if (_cpuPerf == null || _cpuBaseMhz is not { } baseMhz) return null;
            float perf = _cpuPerf.NextValue();
            return perf > 0 ? baseMhz * perf / 100.0 : null;
        }

        private static void Push(Queue<double> history, double? value)
        {
            history.Enqueue(value ?? 0);
            while (history.Count > PcStats.HistoryLength) history.Dequeue();
        }

        private static double? Valid(float? v) => v is { } f && !float.IsNaN(f) ? f : null;

        private static double? Find(IHardware? hw, SensorType type, string name)
        {
            if (hw == null) return null;
            foreach (var s in hw.Sensors)
                if (s.SensorType == type && s.Name == name && Valid(s.Value) is { } v) return v;
            return null;
        }

        private static double? Max(IHardware? hw, SensorType type, Func<string, bool> match)
        {
            if (hw == null) return null;
            double? max = null;
            foreach (var s in hw.Sensors)
                if (s.SensorType == type && match(s.Name) && Valid(s.Value) is { } v && (max == null || v > max)) max = v;
            return max;
        }

        private static double? Sum(IEnumerable<IHardware> hws, SensorType type, string name)
        {
            double? sum = null;
            foreach (var hw in hws)
                if (Find(hw, type, name) is { } v) sum = (sum ?? 0) + v;
            return sum;
        }

        /// <summary>--sensors 用: 検出した全センサーを一覧表示する (センサー名の確認用)。</summary>
        public static void Dump()
        {
            var computer = new Computer { IsCpuEnabled = true, IsGpuEnabled = true, IsMemoryEnabled = true, IsNetworkEnabled = true };
            computer.Open();
            try
            {
                // 使用率・スループットは 2 回目の Update から意味のある値になる
                foreach (var hw in computer.Hardware) hw.Update();
                Thread.Sleep(1000);
                foreach (var hw in computer.Hardware)
                {
                    hw.Update();
                    Console.WriteLine($"[{hw.HardwareType}] {hw.Name}");
                    foreach (var s in hw.Sensors)
                        Console.WriteLine($"  {s.SensorType,-12} {s.Name,-32} {s.Value}");
                }
            }
            finally
            {
                computer.Close();
            }

            // 画面に出る値 (クロックのフォールバック等を含む最終結果)
            using var sampler = new PcStatsSampler();
            sampler.Start();
            // Open に数秒かかる。使用率・クロックは 2 回目のサンプルから有効になる
            for (int i = 0; i < 100 && sampler.Current == null; i++) Thread.Sleep(100);
            Thread.Sleep(1200);
            var c = sampler.Current;
            Console.WriteLine();
            Console.WriteLine(c == null
                ? "[Display] (no data)"
                : $"[Display] CPU {c.CpuLoad:0}% {c.CpuTemp:0}C {c.CpuClockMhz:0}MHz / GPU {c.GpuName} {c.GpuLoad:0}% {c.GpuTemp:0}C VRAM {c.VramUsedMb:0}/{c.VramTotalMb:0}MB / RAM {c.RamUsedGb:0.0}/{c.RamTotalGb:0.0}GB / NET down {c.NetDownBps:0} up {c.NetUpBps:0} B/s");
        }

        public void Dispose()
        {
            _cts.Cancel();
            _thread?.Join(TimeSpan.FromSeconds(3));
            try
            {
                _computer.Close();
            }
            catch
            {
                // 終了間際のエラーは無視してよい
            }
            _cpuPerf?.Dispose();
            _cts.Dispose();
        }
    }
}
