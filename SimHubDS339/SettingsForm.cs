using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SimHubDS339
{
    /// <summary>
    /// アプリケーション設定（自動起動、FPS、SimHub 接続先、ページ設定、ホットキー）を変更するための設定ウィンドウ。
    /// </summary>
    internal sealed class SettingsForm : Form
    {
        private readonly DashboardService _service;
        private readonly AppSettings _settings;
        private readonly Func<HotkeyBinding, HotkeyBinding, string?>? _onRegisterHotkeys;
        private readonly JoystickMonitor _joystick;
        private readonly Action? _onSettingsApplied;
        private AutoStartMode _initialAutoStartMode;

        // コントロール - 起動
        private readonly RadioButton _rbAutoNone;
        private readonly RadioButton _rbAutoUser;
        private readonly RadioButton _rbAutoAdmin;

        // コントロール - 表示
        private readonly NumericUpDown _numFps;

        // コントロール - SimHub
        private readonly TextBox _txtHost;
        private readonly NumericUpDown _numPort;
        private readonly Button _btnResetSimHub;

        // コントロール - レース画面ページ
        private readonly CheckBox _chkMain;
        private readonly CheckBox _chkTyres;
        private readonly CheckBox _chkFuel;
        private readonly CheckBox _chkDelta;
        private readonly CheckBox _chkSession;
        private readonly TextBox _txtNextHotkey;
        private readonly TextBox _txtPrevHotkey;
        private readonly Button _btnResetHotkeys;
        private readonly JoyBindingEditor _joyNext;
        private readonly JoyBindingEditor _joyPrev;

        // コントロール - 下部
        private readonly Label _lblPrivilege;
        private readonly Button _btnOk;
        private readonly Button _btnCancel;

        public SettingsForm(
            DashboardService service,
            AppSettings settings,
            JoystickMonitor joystick,
            Func<HotkeyBinding, HotkeyBinding, string?>? onRegisterHotkeys = null,
            Action? onSettingsApplied = null)
        {
            _service = service;
            _settings = settings;
            _joystick = joystick;
            _onRegisterHotkeys = onRegisterHotkeys;
            _onSettingsApplied = onSettingsApplied;

            // 基本フォーム設定
            Text = "SimHubDS339 設定";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            // 座標は 96 DPI 基準で書いているので、表示 DPI に合わせて拡大させる
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(500, 646);
            ShowInTaskbar = true;

            try
            {
                Font = new Font("Yu Gothic UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            }
            catch
            {
                // フォントが存在しない場合は既定フォントを維持
            }

            // === 1. 起動グループ ===
            var grpStartup = new GroupBox
            {
                Text = "起動設定",
                Location = new Point(16, 12),
                Size = new Size(468, 140),
            };

            _rbAutoNone = new RadioButton
            {
                Text = "自動起動しない",
                Location = new Point(16, 22),
                AutoSize = true,
            };
            _rbAutoUser = new RadioButton
            {
                Text = "Windows 起動時に起動する",
                Location = new Point(16, 44),
                AutoSize = true,
            };
            _rbAutoAdmin = new RadioButton
            {
                Text = "Windows 起動時に管理者として起動する (CPU 温度を表示する場合)",
                Location = new Point(16, 66),
                AutoSize = true,
            };

            var lblStartupNote = new Label
            {
                Text = "※ 管理者として起動するにはタスク スケジューラに登録します。変更時に UAC の確認が表示されます。CPU 温度の取得には PawnIO ドライバも必要です。",
                Location = new Point(16, 90),
                Size = new Size(436, 42),
                ForeColor = SystemColors.GrayText,
            };

            grpStartup.Controls.AddRange(new Control[] { _rbAutoNone, _rbAutoUser, _rbAutoAdmin, lblStartupNote });

            try
            {
                _initialAutoStartMode = AutoStart.GetMode();
            }
            catch
            {
                _initialAutoStartMode = AutoStartMode.None;
            }

            switch (_initialAutoStartMode)
            {
                case AutoStartMode.Admin:
                    _rbAutoAdmin.Checked = true;
                    break;
                case AutoStartMode.User:
                    _rbAutoUser.Checked = true;
                    break;
                default:
                    _rbAutoNone.Checked = true;
                    break;
            }

            // === 2. 表示グループ ===
            var grpDisplay = new GroupBox
            {
                Text = "表示設定",
                Location = new Point(16, 158),
                Size = new Size(468, 58),
            };

            var lblFps = new Label
            {
                Text = "送信レート (FPS):",
                Location = new Point(16, 24),
                AutoSize = true,
            };

            _numFps = new NumericUpDown
            {
                Location = new Point(130, 22),
                Size = new Size(70, 23),
                Minimum = 1,
                Maximum = 20,
                Value = Math.Clamp(_settings.Fps, 1, 20),
            };

            var lblFpsNote = new Label
            {
                Text = "(1 ～ 20 FPS、既定値: 10)",
                Location = new Point(210, 24),
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
            };

            grpDisplay.Controls.AddRange(new Control[] { lblFps, _numFps, lblFpsNote });

            // === 3. SimHub 接続グループ ===
            var grpSimHub = new GroupBox
            {
                Text = "SimHub 接続設定 (Property Server)",
                Location = new Point(16, 222),
                Size = new Size(468, 96),
            };

            var lblHost = new Label
            {
                Text = "ホスト名 / IP:",
                Location = new Point(16, 24),
                AutoSize = true,
            };

            _txtHost = new TextBox
            {
                Location = new Point(130, 21),
                Size = new Size(180, 23),
                Text = _settings.SimHubHost,
            };

            var lblPort = new Label
            {
                Text = "ポート番号:",
                Location = new Point(16, 56),
                AutoSize = true,
            };

            _numPort = new NumericUpDown
            {
                Location = new Point(130, 53),
                Size = new Size(90, 23),
                Minimum = 1,
                Maximum = 65535,
                Value = Math.Clamp(_settings.SimHubPort, 1, 65535),
            };

            _btnResetSimHub = new Button
            {
                Text = "既定値に戻す",
                Location = new Point(340, 20),
                Size = new Size(110, 26),
            };
            _btnResetSimHub.Click += (_, _) =>
            {
                _txtHost.Text = "127.0.0.1";
                _numPort.Value = 18082;
            };

            grpSimHub.Controls.AddRange(new Control[] { lblHost, _txtHost, lblPort, _numPort, _btnResetSimHub });

            // === 4. レース画面のページ設定グループ ===
            var grpPages = new GroupBox
            {
                Text = "レース画面のページ設定",
                Location = new Point(16, 324),
                Size = new Size(468, 244),
            };

            var enabledPages = _settings.GetEnabledRacePages();

            _chkMain = new CheckBox
            {
                Text = "MAIN",
                Location = new Point(16, 24),
                AutoSize = true,
                Checked = true,
                Enabled = false, // MAIN は常に有効
            };
            _chkTyres = new CheckBox
            {
                Text = "TYRES",
                Location = new Point(96, 24),
                AutoSize = true,
                Checked = enabledPages.Contains(RacePage.Tyres),
            };
            _chkFuel = new CheckBox
            {
                Text = "FUEL",
                Location = new Point(184, 24),
                AutoSize = true,
                Checked = enabledPages.Contains(RacePage.Fuel),
            };
            _chkDelta = new CheckBox
            {
                Text = "DELTA",
                Location = new Point(266, 24),
                AutoSize = true,
                Checked = enabledPages.Contains(RacePage.Delta),
            };
            _chkSession = new CheckBox
            {
                Text = "SESSION",
                Location = new Point(356, 24),
                AutoSize = true,
                Checked = enabledPages.Contains(RacePage.Session),
            };

            var lblNextHotkey = new Label
            {
                Text = "次 (キー):",
                Location = new Point(16, 61),
                AutoSize = true,
            };

            _txtNextHotkey = new TextBox
            {
                Location = new Point(100, 58),
                Size = new Size(230, 23),
            };

            var lblNextJoy = new Label
            {
                Text = "次 (ボタン):",
                Location = new Point(16, 91),
                AutoSize = true,
            };

            _joyNext = new JoyBindingEditor(_joystick)
            {
                Location = new Point(100, 86),
            };
            _joyNext.Value = _settings.GetNextPageJoyBinding();

            var lblPrevHotkey = new Label
            {
                Text = "前 (キー):",
                Location = new Point(16, 125),
                AutoSize = true,
            };

            _txtPrevHotkey = new TextBox
            {
                Location = new Point(100, 122),
                Size = new Size(230, 23),
            };

            var lblPrevJoy = new Label
            {
                Text = "前 (ボタン):",
                Location = new Point(16, 155),
                AutoSize = true,
            };

            _joyPrev = new JoyBindingEditor(_joystick)
            {
                Location = new Point(100, 150),
            };
            _joyPrev.Value = _settings.GetPrevPageJoyBinding();

            // ホットキー初期値設定とキャプチャ設定
            if (!HotkeyBinding.TryParse(_settings.NextPageHotkey, out var initialNext))
            {
                HotkeyBinding.TryParse(HotkeyBinding.DefaultNext, out initialNext);
            }
            if (!HotkeyBinding.TryParse(_settings.PrevPageHotkey, out var initialPrev))
            {
                HotkeyBinding.TryParse(HotkeyBinding.DefaultPrev, out initialPrev);
            }

            SetupHotkeyTextBox(_txtNextHotkey, initialNext!);
            SetupHotkeyTextBox(_txtPrevHotkey, initialPrev!);

            _btnResetHotkeys = new Button
            {
                Text = "キーを既定値に戻す",
                Location = new Point(344, 57),
                Size = new Size(110, 25),
            };
            _btnResetHotkeys.Click += (_, _) =>
            {
                if (HotkeyBinding.TryParse(HotkeyBinding.DefaultNext, out var defNext))
                {
                    _txtNextHotkey.Text = defNext.ToString();
                    _txtNextHotkey.Tag = defNext;
                }
                if (HotkeyBinding.TryParse(HotkeyBinding.DefaultPrev, out var defPrev))
                {
                    _txtPrevHotkey.Text = defPrev.ToString();
                    _txtPrevHotkey.Tag = defPrev;
                }
            };

            var lblHotkeyNote = new Label
            {
                Text = "※ キー: 入力欄を選んでキーを押すと入力されます (Ctrl / Alt / Shift のいずれかが必須。F13〜F24 は単独でも可)。\n※ ボタン: 「検出」を押してからコントローラーのボタンを押すと自動入力されます。",
                Location = new Point(16, 184),
                Size = new Size(436, 50),
                ForeColor = SystemColors.GrayText,
            };

            grpPages.Controls.AddRange(new Control[]
            {
                _chkMain, _chkTyres, _chkFuel, _chkDelta, _chkSession,
                lblNextHotkey, _txtNextHotkey,
                lblNextJoy, _joyNext,
                lblPrevHotkey, _txtPrevHotkey,
                lblPrevJoy, _joyPrev,
                _btnResetHotkeys,
                lblHotkeyNote
            });

            // === 5. 下部ステータスとボタン ===
            _lblPrivilege = new Label
            {
                Text = $"現在の権限: {(PcStatsSampler.IsElevated ? "管理者" : "通常")}",
                Location = new Point(16, 576),
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold),
            };

            _btnOk = new Button
            {
                Text = "OK",
                Location = new Point(286, 602),
                Size = new Size(92, 32),
            };
            _btnOk.Click += OnOkClicked;

            _btnCancel = new Button
            {
                Text = "キャンセル",
                Location = new Point(392, 602),
                Size = new Size(92, 32),
            };
            _btnCancel.Click += (_, _) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            AcceptButton = _btnOk;
            CancelButton = _btnCancel;

            Controls.AddRange(new Control[] { grpStartup, grpDisplay, grpSimHub, grpPages, _lblPrivilege, _btnOk, _btnCancel });
        }

        /// <summary>
        /// コントローラーのボタン検出の待機中かどうか (待機中はそのボタンでページを切り替えない)。
        /// </summary>
        internal bool IsCapturingJoystick => _joyNext.IsCapturing || _joyPrev.IsCapturing;

        /// <summary>
        /// ホットキー入力テキストボックスのキーボードイベントをフックして組み合わせをキャプチャする。
        /// </summary>
        private void SetupHotkeyTextBox(TextBox txt, HotkeyBinding initial)
        {
            txt.ReadOnly = true;
            txt.BackColor = SystemColors.Window;
            txt.Text = initial.ToString();
            txt.Tag = initial;

            txt.KeyDown += (s, e) =>
            {
                e.SuppressKeyPress = true;

                // 単独の修飾キー押下 (ControlKey, ShiftKey, Menu など) は入力完了としない
                if (e.KeyCode is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin)
                {
                    return;
                }

                // 修飾キーがない場合は、F13〜F24 の単独入力だけ許可する
                bool hasModifier = (e.Modifiers & (Keys.Control | Keys.Alt | Keys.Shift)) != Keys.None;
                if (!hasModifier && !HotkeyBinding.IsSingleKeyAllowed(e.KeyCode))
                {
                    MessageBox.Show(this, "ホットキーには Ctrl、Alt、Shift のいずれかの修飾キーを含める必要があります (F13〜F24 は単独で使えます。ゲーム操作との競合を防ぐため)。", "ホットキー設定エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var binding = new HotkeyBinding(e.Modifiers, e.KeyCode);
                if (!binding.IsValid)
                {
                    MessageBox.Show(this, "無効なキーの組み合わせです。", "ホットキー設定エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                txt.Text = binding.ToString();
                txt.Tag = binding;
            };
        }

        /// <summary>
        /// OK ボタンがクリックされたときの保存処理。
        /// </summary>
        private void OnOkClicked(object? sender, EventArgs e)
        {
            string host = _txtHost.Text.Trim();
            if (string.IsNullOrEmpty(host))
            {
                MessageBox.Show(this, "SimHub のホスト名を入力してください。", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtHost.Focus();
                return;
            }

            // ホットキーの検証
            var nextBinding = _txtNextHotkey.Tag as HotkeyBinding;
            var prevBinding = _txtPrevHotkey.Tag as HotkeyBinding;

            if (nextBinding == null || !nextBinding.IsValid)
            {
                MessageBox.Show(this, "「次のページ」のホットキーが無効です。", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtNextHotkey.Focus();
                return;
            }
            if (prevBinding == null || !prevBinding.IsValid)
            {
                MessageBox.Show(this, "「前のページ」のホットキーが無効です。", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtPrevHotkey.Focus();
                return;
            }
            if (nextBinding.Equals(prevBinding))
            {
                MessageBox.Show(this, "「次のページ」と「前のページ」に同じホットキーは設定できません。", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // コントローラーのボタンの検証
            var nextJoy = _joyNext.Value;
            var prevJoy = _joyPrev.Value;
            if (nextJoy != null && prevJoy != null && nextJoy.Equals(prevJoy))
            {
                MessageBox.Show(this, "「次のページ」と「前のページ」に同じボタンは設定できません。", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // ホットキー変更時の再登録
            bool hotkeyChanged = (_settings.NextPageHotkey != nextBinding.ToString()) ||
                                 (_settings.PrevPageHotkey != prevBinding.ToString());

            if (hotkeyChanged && _onRegisterHotkeys != null)
            {
                string? err = _onRegisterHotkeys(nextBinding, prevBinding);
                if (err != null)
                {
                    MessageBox.Show(this, err, "ホットキー登録エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    // ウィンドウは閉じない
                    return;
                }
            }

            // 自動起動設定の変更がある場合
            var selectedMode = _rbAutoAdmin.Checked ? AutoStartMode.Admin
                : _rbAutoUser.Checked ? AutoStartMode.User
                : AutoStartMode.None;

            if (selectedMode != _initialAutoStartMode)
            {
                try
                {
                    AutoStart.SetMode(selectedMode);
                    _initialAutoStartMode = selectedMode;
                }
                catch (OperationCanceledException)
                {
                    MessageBox.Show(this, "管理者権限の確認がキャンセルされたため、設定変更を中断しました。", "キャンセル", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"自動起動設定の変更に失敗しました:\n{ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            int newFps = (int)_numFps.Value;
            int newPort = (int)_numPort.Value;

            bool needRestart = (_settings.Fps != newFps) ||
                               (_settings.SimHubHost != host) ||
                               (_settings.SimHubPort != newPort);

            _settings.Fps = newFps;
            _settings.SimHubHost = host;
            _settings.SimHubPort = newPort;
            _settings.NextPageHotkey = nextBinding.ToString();
            _settings.PrevPageHotkey = prevBinding.ToString();
            _settings.NextPageJoy = nextJoy?.ToString() ?? "";
            _settings.PrevPageJoy = prevJoy?.ToString() ?? "";

            // 有効ページの更新
            var enabled = new List<RacePage> { RacePage.Main };
            if (_chkTyres.Checked) enabled.Add(RacePage.Tyres);
            if (_chkFuel.Checked) enabled.Add(RacePage.Fuel);
            if (_chkDelta.Checked) enabled.Add(RacePage.Delta);
            if (_chkSession.Checked) enabled.Add(RacePage.Session);

            _service.PageState.UpdateEnabledPages(enabled);
            _settings.EnabledPages = enabled.Select(p => p.ToString()).ToList();
            _settings.CurrentPage = _service.PageState.CurrentPage.ToString();

            try
            {
                _settings.Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"設定ファイルの保存に失敗しました:\n{ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _onSettingsApplied?.Invoke();

            if (needRestart)
            {
                _service.Restart(_settings);
            }

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
