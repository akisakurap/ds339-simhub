using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SimHubDS339
{
    /// <summary>
    /// アプリケーションのエントリポイント。
    /// コマンドライン引数の解析、起動モード (プレビュー / センサー一覧 / コンソール / トレイ) の分岐、二重起動防止を制御する。
    /// </summary>
    internal static class Program
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        private const int ATTACH_PARENT_PROCESS = -1;
        private const int STD_OUTPUT_HANDLE = -11;
        private const int STD_ERROR_HANDLE = -12;
        private const string MutexName = @"Local\SimHubDS339";

        /// <summary>
        /// アプリケーションのメイン エントリ ポイント。
        /// </summary>
        [STAThread]
        private static void Main(string[] args)
        {
            // 親プロセスのコンソールが存在すれば接続 (コマンドプロンプトからの実行用、失敗しても無視)
            bool hasConsole = AttachConsole(ATTACH_PARENT_PROCESS);
            if (hasConsole)
            {
                EnsureConsoleOutput();
            }

            // --- コマンドライン専用モードの判定 (二重起動チェック・ファイルログ不要) ---

            // 1. --preview DIR モード
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--preview")
                {
                    Preview.Run(args[i + 1]);
                    return;
                }
            }

            // 2. --sensors モード
            if (args.Contains("--sensors"))
            {
                PcStatsSampler.Dump();
                return;
            }

            // 3. --probe モード
            if (args.Contains("--probe"))
            {
                RunProbe();
                return;
            }

            // --- 引数の解析 ---
            int? cmdFps = null;
            int statsSec = 60;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--fps" && int.TryParse(args[i + 1], out var f))
                {
                    cmdFps = Math.Clamp(f, 1, 20);
                }
                if (args[i] == "--stats" && int.TryParse(args[i + 1], out var st))
                {
                    statsSec = Math.Max(1, st);
                }
            }

            bool demo = args.Contains("--demo");
            bool consoleMode = args.Contains("--console");

            // --- 二重起動防止 (名前付き Mutex) ---
            // 管理者として再起動直後などの競合を考慮し、最大 5 秒待機
            Mutex? mutex = null;
            bool hasHandle = false;
            try
            {
                mutex = new Mutex(false, MutexName);
                try
                {
                    hasHandle = mutex.WaitOne(TimeSpan.FromSeconds(5), false);
                }
                catch (AbandonedMutexException)
                {
                    // 以前のプロセスが異常終了して Mutex を放棄した場合
                    hasHandle = true;
                }

                if (!hasHandle)
                {
                    MessageBox.Show("SimHubDS339 はすでに起動しています。", "SimHubDS339", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Mutex] Mutex 取得中にエラーが発生しました: {ex.Message}");
            }

            try
            {
                // 設定ファイルの読み込み
                var settings = AppSettings.Load();

                // コマンドライン引数で --fps が指定された場合は設定ファイルより優先 (設定ファイル自体は書き換えない)
                if (cmdFps.HasValue)
                {
                    settings.Fps = cmdFps.Value;
                }

                if (consoleMode)
                {
                    // --- コンソールモード (--console) ---
                    // 親コンソールがなければ新しいコンソールを割り当てる
                    if (!hasConsole)
                    {
                        if (AllocConsole())
                        {
                            EnsureConsoleOutput();
                        }
                    }

                    Console.WriteLine($"SimHubDS339 コンソールモード起動 ({settings.Fps} fps). Ctrl+C で終了します。");

                    using var service = new DashboardService(settings, demo, statsSec);
                    using var cts = new CancellationTokenSource();

                    Console.CancelKeyPress += (_, e) =>
                    {
                        e.Cancel = true;
                        cts.Cancel();
                    };

                    service.Start();

                    // Ctrl+C 待機
                    cts.Token.WaitHandle.WaitOne();

                    Console.WriteLine("停止中...");
                    service.Stop();
                }
                else
                {
                    // --- トレイ常駐モード (既定) ---
                    // ファイルログの有効化
                    Log.Initialize();

                    ApplicationConfiguration.Initialize();

                    // Mutex 解放用のアクション
                    Action releaseMutex = () =>
                    {
                        if (hasHandle && mutex != null)
                        {
                            try { mutex.ReleaseMutex(); } catch { }
                            try { mutex.Dispose(); } catch { }
                            mutex = null;
                            hasHandle = false;
                        }
                    };

                    using var service = new DashboardService(settings, demo, statsSec);
                    service.Start();

                    // TrayApp に Mutex 解放コールバックを渡し、終了時に速やかに解放できるようにする
                    Application.Run(new TrayApp(service, settings, releaseMutex));
                }
            }
            finally
            {
                if (hasHandle && mutex != null)
                {
                    try
                    {
                        mutex.ReleaseMutex();
                    }
                    catch
                    {
                    }
                    mutex.Dispose();
                }
            }
        }

        /// <summary>
        /// AttachConsole / AllocConsole 後に Console.Out と Console.Error をコンソールの標準出力ハンドルに向ける。
        /// </summary>
        private static void EnsureConsoleOutput()
        {
            try
            {
                var stdOutHandle = GetStdHandle(STD_OUTPUT_HANDLE);
                if (stdOutHandle != IntPtr.Zero && stdOutHandle != new IntPtr(-1))
                {
                    var safeHandle = new Microsoft.Win32.SafeHandles.SafeFileHandle(stdOutHandle, ownsHandle: false);
                    var fs = new FileStream(safeHandle, FileAccess.Write);
                    var writer = new StreamWriter(fs, Console.OutputEncoding) { AutoFlush = true };
                    Console.SetOut(writer);
                }

                var stdErrHandle = GetStdHandle(STD_ERROR_HANDLE);
                if (stdErrHandle != IntPtr.Zero && stdErrHandle != new IntPtr(-1))
                {
                    var safeHandle = new Microsoft.Win32.SafeHandles.SafeFileHandle(stdErrHandle, ownsHandle: false);
                    var fs = new FileStream(safeHandle, FileAccess.Write);
                    var writer = new StreamWriter(fs, Console.OutputEncoding) { AutoFlush = true };
                    Console.SetError(writer);
                }
            }
            catch
            {
                // リダイレクト失敗時は無視
            }
        }

        /// <summary>
        /// --probe オプション: SimHub Property Server に接続して候補プロパティを購読し、一覧を出力する。
        /// </summary>
        private static void RunProbe()
        {
            var settings = AppSettings.Load();
            Console.WriteLine("SimHub プロパティ プローブを開始します...");
            Console.WriteLine($"接続先: {settings.SimHubHost}:{settings.SimHubPort}");
            Console.WriteLine($"購読プロパティ候補数: {SimHubProperties.AllCandidateProperties.Length}");

            using var client = new SimHubPropertyClient(settings.SimHubHost, settings.SimHubPort, SimHubProperties.AllCandidateProperties);
            client.Start();

            // 接続待機 (最大 2 秒)
            int waitCount = 0;
            while (!client.IsConnected && waitCount < 20)
            {
                Thread.Sleep(100);
                waitCount++;
            }

            if (!client.IsConnected)
            {
                Console.WriteLine($"[エラー] SimHub Property Server ({settings.SimHubHost}:{settings.SimHubPort}) に接続できませんでした。");
                Console.WriteLine("SimHub が起動しているか、Property Server プラグイン (TCP 18082) が有効になっているか確認してください。");
                client.Stop();
                return;
            }

            Console.WriteLine("接続成功。5 秒間プロパティ値を受信待機します...");
            Thread.Sleep(5000);

            Console.WriteLine();
            Console.WriteLine("--------------------------------------------------");
            Console.WriteLine(" SimHub 候補プロパティ一覧 (プローブ結果)");
            Console.WriteLine("--------------------------------------------------");

            int receivedCount = 0;
            foreach (var prop in SimHubProperties.AllCandidateProperties)
            {
                bool hasVal = client.Values.TryGetValue(prop, out var val) && val != null;
                client.Types.TryGetValue(prop, out var type);

                if (hasVal)
                {
                    receivedCount++;
                    Console.WriteLine($"[OK] {prop,-55} [{type ?? "unknown"}] = {val}");
                }
                else
                {
                    Console.WriteLine($"[--] {prop,-55} (no value)");
                }
            }

            Console.WriteLine("--------------------------------------------------");
            Console.WriteLine($"合計: {SimHubProperties.AllCandidateProperties.Length} 件中 {receivedCount} 件を受信しました。");
            client.Stop();
        }
    }
}
