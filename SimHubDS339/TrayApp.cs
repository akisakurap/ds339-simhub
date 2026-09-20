using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SimHubDS339
{
    /// <summary>
    /// タスクトレイ常駐アプリケーションのコンテキストクラス。
    /// NotifyIcon、右クリックメニュー、グローバルホットキー、ステータス更新タイマー、設定ウィンドウの制御を行う。
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
        private readonly ToolStripMenuItem _menuItemNextPage;
        private readonly ToolStripMenuItem _menuItemPrevPage;
        private readonly ToolStripMenuItem _menuItemSettings;
        private readonly ToolStripMenuItem _menuItemRestartAdmin;
        private readonly ToolStripMenuItem _menuItemOpenLog;
        private readonly ToolStripMenuItem _menuItemExit;
        private readonly System.Windows.Forms.Timer _updateTimer;

        private readonly HotkeyManager _hotkeyManager;
        private readonly JoystickMonitor _joystick;
        private JoyBinding? _nextPageJoy;
        private JoyBinding? _prevPageJoy;
        private IntPtr _hIcon = IntPtr.Zero;
        private SettingsForm? _settingsForm;
        private bool _isExiting;

        public TrayApp(DashboardService service, AppSettings settings, Action? onReleaseMutex = null)
        {
            _service = service;
            _settings = settings;
            _onReleaseMutex = onReleaseMutex;

            // グローバルホットキーマネージャーの作成
            _hotkeyManager = new HotkeyManager();
            _hotkeyManager.NextPageTriggered += () => _service.NextPage();
            _hotkeyManager.PrevPageTriggered += () => _service.PreviousPage();

            // コントローラー (ホイール等) のボタン監視
            _joystick = new JoystickMonitor();
            _joystick.InputPressed += OnJoystickInput;
            ReloadJoystickBindings();

            // コンテキストメニューの構築
            _contextMenu = new ContextMenuStrip();

            // 1. 状態表示項目 (無効化表示)
            _menuItemDsStatus = new ToolStripMenuItem("DS339: 確認中...") { Enabled = false };
            _menuItemShStatus = new ToolStripMenuItem("SimHub: 確認中...") { Enabled = false };

            // 2. ページ切り替え項目
            _menuItemNextPage = new ToolStripMenuItem("次のページ");
            _menuItemNextPage.Click += (_, _) => _service.NextPage();

            _menuItemPrevPage = new ToolStripMenuItem("前のページ");
            _menuItemPrevPage.Click += (_, _) => _service.PreviousPage();

            // 3. 設定 (太字・既定項目)
            _menuItemSettings = new ToolStripMenuItem("設定...")
            {
                Font = new Font(_contextMenu.Font, FontStyle.Bold)
            };
            _menuItemSettings.Click += (_, _) => ShowSettings();

            // 4. 管理者として再起動 (非管理者のときのみ表示)
            _menuItemRestartAdmin = new ToolStripMenuItem("管理者として再起動 (CPU 温度を表示)")
            {
                Visible = !PcStatsSampler.IsElevated
            };
            _menuItemRestartAdmin.Click += (_, _) => RestartAsAdmin();

            // 5. ログを開く
            _menuItemOpenLog = new ToolStripMenuItem("ログを開く");
            _menuItemOpenLog.Click += (_, _) => Log.OpenLogFile();

            // 6. 終了
            _menuItemExit = new ToolStripMenuItem("終了");
            _menuItemExit.Click += (_, _) => ExitApplication();

            // メニューに項目を追加
            _contextMenu.Items.AddRange(new ToolStripItem[]
            {
                _menuItemDsStatus,
                _menuItemShStatus,
                new ToolStripSeparator(),
                _menuItemNextPage,
                _menuItemPrevPage,
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

            // ホットキーの登録
            RegisterHotkeysFromSettings();

            // コントローラーの監視開始
            _joystick.Start();

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
        /// 設定からコントローラーのボタン割り当てを読み込む (未割当・不正な値は null)。
        /// </summary>
        private void ReloadJoystickBindings()
        {
            _nextPageJoy = _settings.GetNextPageJoyBinding();
            _prevPageJoy = _settings.GetPrevPageJoyBinding();
        }

        /// <summary>
        /// コントローラーのボタンが押されたとき、割り当て済みならページを切り替える。
        /// </summary>
        private void OnJoystickInput(JoyBinding pressed)
        {
            // 設定画面で「検出」の待機中は、そのボタンでページを切り替えない
            if (_settingsForm is { IsDisposed: false, IsCapturingJoystick: true })
            {
                return;
            }

            if (_nextPageJoy != null && _nextPageJoy.Equals(pressed))
            {
                _service.NextPage();
            }
            else if (_prevPageJoy != null && _prevPageJoy.Equals(pressed))
            {
                _service.PreviousPage();
            }
        }

        /// <summary>
        /// 設定からホットキーを読み込み、グローバルホットキーとして登録する。
        /// </summary>
        private void RegisterHotkeysFromSettings()
        {
            if (!HotkeyBinding.TryParse(_settings.NextPageHotkey, out var next))
            {
                HotkeyBinding.TryParse(HotkeyBinding.DefaultNext, out next);
            }
            if (!HotkeyBinding.TryParse(_settings.PrevPageHotkey, out var prev))
            {
                HotkeyBinding.TryParse(HotkeyBinding.DefaultPrev, out prev);
            }

            UpdateMenuHotkeyTexts(next!, prev!);

            if (!_hotkeyManager.Register(next!, prev!, out var errorMessage))
            {
                if (!string.IsNullOrEmpty(errorMessage))
                {
                    Console.WriteLine($"[Hotkey] {errorMessage}");
                    _notifyIcon.ShowBalloonTip(5000, "ホットキー登録エラー", errorMessage, ToolTipIcon.Warning);
                }
            }
        }

        /// <summary>
        /// メニュー項目のテキストにショートカットキー文字列を反映する。
        /// </summary>
        private void UpdateMenuHotkeyTexts(HotkeyBinding next, HotkeyBinding prev)
        {
            _menuItemNextPage.ShortcutKeyDisplayString = next.ToString();
            _menuItemPrevPage.ShortcutKeyDisplayString = prev.ToString();
        }

        /// <summary>
        /// 設定ウィンドウから呼ばれるホットキー再登録デリゲート。
        /// </summary>
        private string? OnRegisterHotkeys(HotkeyBinding next, HotkeyBinding prev)
        {
            if (!_hotkeyManager.Register(next, prev, out var err))
            {
                return err;
            }
            UpdateMenuHotkeyTexts(next, prev);
            return null;
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

                // 背景: 角丸四角形 (Accent 色)
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

        private void UpdateStatus()
        {
            bool dsConnected = _service.DeviceConnected;
            bool shConnected = _service.SimHubConnected;
            bool gameRunning = _service.GameRunning;
            string? gameName = _service.GameName;

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

            string tip = $"SimHubDS339 - DS339: {(dsConnected ? "接続中" : "未接続")} / SimHub: {(shConnected ? "接続中" : "未接続")}";
            if (tip.Length > 63)
            {
                tip = tip.Substring(0, 63);
            }
            _notifyIcon.Text = tip;
        }

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

            _settingsForm = new SettingsForm(_service, _settings, _joystick, OnRegisterHotkeys, ReloadJoystickBindings);
            _settingsForm.FormClosed += (_, _) => _settingsForm = null;
            _settingsForm.Show();
        }

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

                var args = Environment.GetCommandLineArgs().Skip(1);
                psi.Arguments = string.Join(" ", args.Select(a => $"\"{a}\""));

                var proc = Process.Start(psi);
                if (proc != null)
                {
                    ExitApplication();
                }
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                // ERROR_CANCELLED: ユーザーが UAC をキャンセルした場合は何もしない
            }
            catch (Exception ex)
            {
                MessageBox.Show($"管理者としての再起動に失敗しました:\n{ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExitApplication()
        {
            if (_isExiting) return;
            _isExiting = true;

            SystemEvents.SessionEnding -= OnSessionEnding;
            Application.ApplicationExit -= OnApplicationExit;

            _updateTimer.Stop();
            _updateTimer.Dispose();

            // ホットキーの解放
            _hotkeyManager.Dispose();

            // コントローラー監視の停止
            _joystick.Dispose();

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
