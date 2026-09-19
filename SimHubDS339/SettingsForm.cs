using System.Drawing;
using System.Windows.Forms;

namespace SimHubDS339
{
    /// <summary>
    /// アプリケーション設定（自動起動、FPS、SimHub 接続先）を変更するための設定ウィンドウ。
    /// </summary>
    internal sealed class SettingsForm : Form
    {
        private readonly DashboardService _service;
        private readonly AppSettings _settings;
        private AutoStartMode _initialAutoStartMode;

        // コントロール
        private readonly RadioButton _rbAutoNone;
        private readonly RadioButton _rbAutoUser;
        private readonly RadioButton _rbAutoAdmin;
        private readonly NumericUpDown _numFps;
        private readonly TextBox _txtHost;
        private readonly NumericUpDown _numPort;
        private readonly Button _btnResetSimHub;
        private readonly Label _lblPrivilege;
        private readonly Button _btnOk;
        private readonly Button _btnCancel;

        public SettingsForm(DashboardService service, AppSettings settings)
        {
            _service = service;
            _settings = settings;

            // 基本フォーム設定
            Text = "SimHubDS339 設定";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            // 座標は 96 DPI 基準で書いているので、表示 DPI に合わせて拡大させる
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(500, 480);
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
                Size = new Size(468, 160),
            };

            _rbAutoNone = new RadioButton
            {
                Text = "自動起動しない",
                Location = new Point(16, 24),
                AutoSize = true,
            };
            _rbAutoUser = new RadioButton
            {
                Text = "Windows 起動時に起動する",
                Location = new Point(16, 48),
                AutoSize = true,
            };
            _rbAutoAdmin = new RadioButton
            {
                Text = "Windows 起動時に管理者として起動する (CPU 温度を表示する場合)",
                Location = new Point(16, 72),
                AutoSize = true,
            };

            var lblStartupNote = new Label
            {
                Text = "※ 管理者として起動するにはタスク スケジューラに登録します。変更時に UAC の確認が表示されます。CPU 温度の取得には PawnIO ドライバも必要です。",
                Location = new Point(16, 100),
                Size = new Size(436, 50),
                ForeColor = SystemColors.GrayText,
            };

            grpStartup.Controls.AddRange(new Control[] { _rbAutoNone, _rbAutoUser, _rbAutoAdmin, lblStartupNote });

            // 現在の自動起動モードを取得して反映
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
                Location = new Point(16, 180),
                Size = new Size(468, 65),
            };

            var lblFps = new Label
            {
                Text = "送信レート (FPS):",
                Location = new Point(16, 28),
                AutoSize = true,
            };

            _numFps = new NumericUpDown
            {
                Location = new Point(130, 26),
                Size = new Size(70, 23),
                Minimum = 1,
                Maximum = 20,
                Value = Math.Clamp(_settings.Fps, 1, 20),
            };

            var lblFpsNote = new Label
            {
                Text = "(1 ～ 20 FPS、既定値: 10)",
                Location = new Point(210, 28),
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
            };

            grpDisplay.Controls.AddRange(new Control[] { lblFps, _numFps, lblFpsNote });

            // === 3. SimHub 接続グループ ===
            var grpSimHub = new GroupBox
            {
                Text = "SimHub 接続設定 (Property Server)",
                Location = new Point(16, 253),
                Size = new Size(468, 110),
            };

            var lblHost = new Label
            {
                Text = "ホスト名 / IP:",
                Location = new Point(16, 28),
                AutoSize = true,
            };

            _txtHost = new TextBox
            {
                Location = new Point(130, 25),
                Size = new Size(180, 23),
                Text = _settings.SimHubHost,
            };

            var lblPort = new Label
            {
                Text = "ポート番号:",
                Location = new Point(16, 62),
                AutoSize = true,
            };

            _numPort = new NumericUpDown
            {
                Location = new Point(130, 59),
                Size = new Size(90, 23),
                Minimum = 1,
                Maximum = 65535,
                Value = Math.Clamp(_settings.SimHubPort, 1, 65535),
            };

            _btnResetSimHub = new Button
            {
                Text = "既定値に戻す",
                Location = new Point(340, 24),
                Size = new Size(110, 27),
            };
            _btnResetSimHub.Click += (_, _) =>
            {
                _txtHost.Text = "127.0.0.1";
                _numPort.Value = 18082;
            };

            grpSimHub.Controls.AddRange(new Control[] { lblHost, _txtHost, lblPort, _numPort, _btnResetSimHub });

            // === 4. 下部ステータスとボタン ===
            _lblPrivilege = new Label
            {
                Text = $"現在の権限: {(PcStatsSampler.IsElevated ? "管理者" : "通常")}",
                Location = new Point(16, 380),
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold),
            };

            _btnOk = new Button
            {
                Text = "OK",
                Location = new Point(286, 420),
                Size = new Size(92, 32),
            };
            _btnOk.Click += OnOkClicked;

            _btnCancel = new Button
            {
                Text = "キャンセル",
                Location = new Point(392, 420),
                Size = new Size(92, 32),
            };
            _btnCancel.Click += (_, _) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            AcceptButton = _btnOk;
            CancelButton = _btnCancel;

            Controls.AddRange(new Control[] { grpStartup, grpDisplay, grpSimHub, _lblPrivilege, _btnOk, _btnCancel });
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

            // 選択された自動起動モード
            var selectedMode = _rbAutoAdmin.Checked ? AutoStartMode.Admin
                : _rbAutoUser.Checked ? AutoStartMode.User
                : AutoStartMode.None;

            // 自動起動設定の変更がある場合
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

            try
            {
                _settings.Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"設定ファイルの保存に失敗しました:\n{ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (needRestart)
            {
                _service.Restart(_settings);
            }

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
