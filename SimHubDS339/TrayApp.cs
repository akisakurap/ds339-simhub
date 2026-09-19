using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SimHubDS339
{
    /// <summary>
    /// タスクトレイ常駐アプリケーションのコンテキストクラス。
    /// NotifyIcon、右クリックメニュー、ステータス更新タイマー、設定ウィンドウの制御を行う。
    /// </summary>
    internal sealed class TrayApp : ApplicationContext
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private readonly DashboardService _service;
        private readonly AppSettings _settings;
        private readonly Action? _onReleaseMutex;

        private readonly NotifyIcon _notifyIcon;
        private readonly ContextMenuStrip _contextMenu;
        private readonly ToolStripMenuItem _menuItemDsStatus;
        private readonly ToolStripMenuItem _menuItemShStatus;
        private readonly ToolStripMenuItem _menuItemSettings;
        private readonly ToolStripMenuItem _menuItemRestartAdmin;
        private readonly ToolStripMenuItem _menuItemOpenLog;
        private readonly ToolStripMenuItem _menuItemExit;
        private readonly System.Windows.Forms.Timer _updateTimer;

        private IntPtr _hIcon = IntPtr.Zero;
        private SettingsForm? _settingsForm;
        private bool _isExiting;

        public TrayApp(DashboardService service, AppSettings settings, Action? onReleaseMutex = null)
        {
            _service = service;
            _settings = settings;
            _onReleaseMutex = onReleaseMutex;

            // コンテキストメニューの構築
            _contextMenu = new ContextMenuStrip();

            // 1. 状態表示項目 (無効化表示)
            _menuItemDsStatus = new ToolStripMenuItem("DS339: 確認中...") { Enabled = false };
            _menuItemShStatus = new ToolStripMenuItem("SimHub: 確認中...") { Enabled = false };

            // 2. 設定 (太字・既定項目)
            _menuItemSettings = new ToolStripMenuItem("設定...")
            {
                Font = new Font(_contextMenu.Font, FontStyle.Bold)
            };
            _menuItemSettings.Click += (_, _) => ShowSettings();

            // 3. 管理者として再起動 (非管理者のときのみ表示)
            _menuItemRestartAdmin = new ToolStripMenuItem("管理者として再起動 (CPU 温度を表示)")
            {
                Visible = !PcStatsSampler.IsElevated
            };
            _menuItemRestartAdmin.Click += (_, _) => RestartAsAdmin();

            // 4. ログを開く
            _menuItemOpenLog = new ToolStripMenuItem("ログを開く");
            _menuItemOpenLog.Click += (_, _) => Log.OpenLogFile();

            // 5. 終了
            _menuItemExit = new ToolStripMenuItem("終了");
            _menuItemExit.Click += (_, _) => ExitApplication();

            // メニューに項目を追加
            _contextMenu.Items.AddRange(new ToolStripItem[]
            {
                _menuItemDsStatus,
                _menuItemShStatus,
                new ToolStripSeparator(),
                _menuItemSettings,
                _menuItemRestartAdmin,
                _menuItemOpenLog,
                new ToolStripSeparator(),
                _menuItemExit
            });

            // トレイアイコンの作成と初期化
            var icon = CreateTrayIcon();
            _notifyIcon = new NotifyIcon
            {
                Icon = icon,
                Text = "SimHubDS339",
                ContextMenuStrip = _contextMenu,
                Visible = true
            };

            // ダブルクリックで設定ウィンドウを開く
            _notifyIcon.MouseDoubleClick += (sender, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ShowSettings();
                }
            };

            // 状態更新タイマー (2 秒間隔)
            _updateTimer = new System.Windows.Forms.Timer { Interval = 2000 };
            _updateTimer.Tick += (_, _) => UpdateStatus();
            _updateTimer.Start();

            // 初回状態更新
            UpdateStatus();

            // OS のログオフ・シャットダウン時の終了ハンドラ登録
            SystemEvents.SessionEnding += OnSessionEnding;
            Application.ApplicationExit += OnApplicationExit;
        }

        /// <summary>
        /// コード描画によりタスクトレイアイコン用の Icon を生成する。
        /// </summary>
        private Icon CreateTrayIcon()
        {
            const int size = 32;
            using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.Clear(Color.Transparent);

                // 背景: 角丸四角形 (DashboardRenderer の Accent 色: Color.FromArgb(40, 210, 150))
                var rect = new RectangleF(1, 1, size - 2, size - 2);
                using var brush = new SolidBrush(Color.FromArgb(40, 210, 150));
                using var path = CreateRoundedRectanglePath(rect, 7f);
                g.FillPath(brush, path);

                // 文字: 白抜きの "DS"
                using var textBrush = new SolidBrush(Color.White);
                using var font = new Font("Segoe UI", 13f, FontStyle.Bold, GraphicsUnit.Pixel);
                using var fmt = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString("DS", font, textBrush, rect, fmt);
            }

            _hIcon = bmp.GetHicon();
            return Icon.FromHandle(_hIcon);
        }

        /// <summary>
        /// 角丸四角形の GraphicsPath を作成する。
        /// </summary>
        private static GraphicsPath CreateRoundedRectanglePath(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        /// <summary>
        /// トレイアイコンのツールチップおよびメニュー項目の状態文字列を更新する。
        /// </summary>
        private void UpdateStatus()
        {
            bool dsConnected = _service.DeviceConnected;
            bool shConnected = _service.SimHubConnected;
            bool gameRunning = _service.GameRunning;
            string? gameName = _service.GameName;

            // メニュー項目の更新
            _menuItemDsStatus.Text = $"DS339: {(dsConnected ? "接続中" : "未接続")}";

            string simHubStateText;
            if (!shConnected)
            {
                simHubStateText = "未接続";
            }
            else if (!string.IsNullOrEmpty(gameName))
            {
                simHubStateText = $"接続中 ({gameName})";
            }
            else if (gameRunning)
            {
                simHubStateText = "接続中 (ゲーム実行中)";
            }
            else
            {
                simHubStateText = "接続中 (待機中)";
            }
            _menuItemShStatus.Text = $"SimHub: {simHubStateText}";

            // ツールチップの更新 (最大 63 文字制限を考慮)
            string tip = $"SimHubDS339 - DS339: {(dsConnected ? "接続中" : "未接続")} / SimHub: {(shConnected ? "接続中" : "未接続")}";
            if (tip.Length > 63)
            {
                tip = tip.Substring(0, 63);
            }
            _notifyIcon.Text = tip;
        }

        /// <summary>
        /// 設定ウィンドウを表示する (すでに開いている場合は前面に出す)。
        /// </summary>
        private void ShowSettings()
        {
            if (_settingsForm != null && !_settingsForm.IsDisposed)
            {
                if (_settingsForm.WindowState == FormWindowState.Minimized)
                {
                    _settingsForm.WindowState = FormWindowState.Normal;
                }
                _settingsForm.BringToFront();
                _settingsForm.Activate();
                return;
            }

            _settingsForm = new SettingsForm(_service, _settings);
            _settingsForm.FormClosed += (_, _) => _settingsForm = null;
            _settingsForm.Show();
        }

        /// <summary>
        /// 管理者権限に昇格して自分自身を再起動する。
        /// </summary>
        private void RestartAsAdmin()
        {
            try
            {
                string exePath = Environment.ProcessPath ?? Application.ExecutablePath;
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Verb = "runas",
                    UseShellExecute = true,
                };

                // コマンドライン引数を引き継ぐ
                var args = Environment.GetCommandLineArgs().Skip(1);
                psi.Arguments = string.Join(" ", args.Select(a => $"\"{a}\""));

                var proc = Process.Start(psi);
                if (proc != null)
                {
                    // 昇格プロセスが起動したため、現行プロセスを終了する
                    ExitApplication();
                }
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                // ERROR_CANCELLED: ユーザーが UAC ダイアログをキャンセルした場合は何もしない
            }
            catch (Exception ex)
            {
                MessageBox.Show($"管理者としての再起動に失敗しました:\n{ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// アプリケーションを終了し、常駐サービスやアイコンリソースをクリーンアップする。
        /// </summary>
        private void ExitApplication()
        {
            if (_isExiting) return;
            _isExiting = true;

            SystemEvents.SessionEnding -= OnSessionEnding;
            Application.ApplicationExit -= OnApplicationExit;

            _updateTimer.Stop();
            _updateTimer.Dispose();

            // サービスの停止
            _service.Stop();

            // Mutex の解放
            try
            {
                _onReleaseMutex?.Invoke();
            }
            catch
            {
            }

            // トレイアイコンの破棄
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _contextMenu.Dispose();

            if (_hIcon != IntPtr.Zero)
            {
                DestroyIcon(_hIcon);
                _hIcon = IntPtr.Zero;
            }

            ExitThread();
        }

        private void OnSessionEnding(object? sender, SessionEndingEventArgs e)
        {
            _service.Stop();
        }

        private void OnApplicationExit(object? sender, EventArgs e)
        {
            _service.Stop();
        }
    }
}
