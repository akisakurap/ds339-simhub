using System.Diagnostics;

namespace SimHubDS339
{
    /// <summary>
    /// SimHub からのテレメトリ受信、PC ステータス計測、ダッシュボード描画、DS339 への USB 送信ループを管理する常駐サービス。
    /// </summary>
    internal sealed class DashboardService : IDisposable
    {
        private AppSettings _settings;
        private readonly bool _demo;
        private readonly int _statsSec;
        private CancellationTokenSource? _cts;
        private Task? _loopTask;
        private readonly object _lock = new();

        /// <summary>DS339 ディスプレイデバイスが接続されているか</summary>
        public bool DeviceConnected { get; private set; }

        /// <summary>SimHub Property Server に接続されているか</summary>
        public bool SimHubConnected { get; private set; }

        /// <summary>ゲームが実行中か</summary>
        public bool GameRunning { get; private set; }

        /// <summary>現在実行中のゲーム名 (未接続・待機中は null または空)</summary>
        public string? GameName { get; private set; }

        /// <summary>レース画面のページ切り替え状態管理</summary>
        public PageState PageState { get; }

        /// <summary>次の有効なレース画面ページに切り替える</summary>
        public void NextPage() => PageState.Next();

        /// <summary>前の有効なレース画面ページに切り替える</summary>
        public void PreviousPage() => PageState.Previous();

        /// <summary>
        /// サービスを初期化する。
        /// </summary>
        /// <param name="settings">アプリケーション設定</param>
        /// <param name="demo">デモモード (擬似テレメトリ) かどうか</param>
        /// <param name="statsSec">統計ログ出力の間隔 (秒)</param>
        public DashboardService(AppSettings settings, bool demo = false, int statsSec = 60)
        {
            _settings = settings;
            _demo = demo;
            _statsSec = statsSec;

            PageState = new PageState(_settings.GetCurrentRacePage(), _settings.GetEnabledRacePages());
            PageState.PageChanged += page =>
            {
                _settings.CurrentPage = page.ToString();
                try
                {
                    _settings.Save();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Settings] ページ設定の自動保存に失敗しました: {ex.Message}");
                }
            };
        }

        /// <summary>
        /// バックグラウンドタスクで描画・送信ループを開始する。
        /// </summary>
        public void Start()
        {
            lock (_lock)
            {
                if (_loopTask != null && !_loopTask.IsCompleted)
                {
                    return;
                }

                _cts = new CancellationTokenSource();
                var token = _cts.Token;
                _loopTask = Task.Run(() => RunLoopAsync(token), token);
            }
        }

        /// <summary>
        /// ループの停止を要求し、最大 3 秒間終了を待機する。
        /// </summary>
        public void Stop()
        {
            Task? taskToWait;
            lock (_lock)
            {
                if (_cts == null) return;

                _cts.Cancel();
                taskToWait = _loopTask;
            }

            if (taskToWait != null)
            {
                try
                {
                    // 最大 3 秒待機
                    taskToWait.Wait(TimeSpan.FromSeconds(3));
                }
                catch (AggregateException)
                {
                    // タスクキャンセル例外は無視
                }
            }

            lock (_lock)
            {
                _cts?.Dispose();
                _cts = null;
                _loopTask = null;

                DeviceConnected = false;
                SimHubConnected = false;
                GameRunning = false;
                GameName = null;
            }
        }

        /// <summary>
        /// 新しい設定を適用してサービスを再起動する。
        /// </summary>
        /// <param name="newSettings">新しい設定</param>
        public void Restart(AppSettings newSettings)
        {
            lock (_lock)
            {
                Stop();
                _settings = newSettings;
                PageState.UpdateEnabledPages(_settings.GetEnabledRacePages());
                Start();
            }
        }

        /// <summary>
        /// 描画・USB 送信のメインループ処理。
        /// </summary>
        private async Task RunLoopAsync(CancellationToken ct)
        {
            int fps = _settings.Fps;
            int periodMs = 1000 / fps;

            Console.WriteLine($"SimHubDS339 サービス開始 ({fps} fps, SimHub: {_settings.SimHubHost}:{_settings.SimHubPort})");

            using var client = new SimHubPropertyClient(_settings.SimHubHost, _settings.SimHubPort, Telemetry.SubscribedProperties);
            var adapter = new SimHubPropertyClientAdapter(client);
            client.Start();

            if (!PcStatsSampler.IsElevated)
            {
                Console.WriteLine("[PC] 通常権限で実行中: CPU 温度は取得できません (管理者権限と PawnIO ドライバが必要です)");
            }

            using var pcStats = new PcStatsSampler();
            pcStats.Start();

            var stintTracker = new StintTracker();
            using var renderer = new DashboardRenderer();
            using var link = new DisplayLink();

            var sw = Stopwatch.StartNew();
            long nextFrame = 0;
            long lastStats = 0;
            int sent = 0;

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    long now = sw.ElapsedMilliseconds;
                    if (now >= nextFrame)
                    {
                        var t = _demo ? Demo.Create() : Telemetry.From(adapter, stintTracker);
                        pcStats.Paused = t.SimHubConnected && t.GameRunning;
                        var frame = renderer.Render(t, pcStats.Current, PageState.CurrentPage, PageState.GetActiveOverlay(), PageState.EnabledPages);
                        if (link.TrySend(frame))
                        {
                            sent++;
                        }

                        // 状態プロパティの更新
                        DeviceConnected = link.IsConnected;
                        SimHubConnected = client.IsConnected;
                        GameRunning = t.GameRunning;
                        GameName = t.GameName;

                        nextFrame += periodMs;
                        if (nextFrame < sw.ElapsedMilliseconds)
                        {
                            nextFrame = sw.ElapsedMilliseconds + periodMs;
                        }
                    }

                    if (now - lastStats >= _statsSec * 1000L)
                    {
                        Console.WriteLine($"{DateTime.Now:HH:mm:ss} [stats] frames sent in last {_statsSec}s: {sent}, device={(link.IsConnected ? "connected" : "disconnected")}, simhub={(client.IsConnected ? "connected" : "disconnected")}, pcstats={(pcStats.Paused ? "paused" : "running")}");
                        sent = 0;
                        lastStats = now;
                    }

                    long wait = nextFrame - sw.ElapsedMilliseconds;
                    if (wait > 0)
                    {
                        try
                        {
                            await Task.Delay((int)wait, ct);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                }
            }
            finally
            {
                Console.WriteLine("SimHubDS339 サービスを停止しています...");
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
