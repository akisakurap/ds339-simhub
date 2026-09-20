using System.Drawing;
using System.Windows.Forms;

namespace SimHubDS339
{
    /// <summary>
    /// 「どのコントローラーのどのボタンか」を 1 つ選ぶための設定用コントロール。
    /// コントローラー選択・ボタン選択・「検出」(押したボタンを自動入力)・「解除」を持つ。
    /// </summary>
    internal sealed class JoyBindingEditor : UserControl
    {
        private const int CaptureTimeoutMs = 10000;
        private const string DetectText = "検出";
        private const string WaitingText = "待機…";

        private sealed class DeviceItem
        {
            public ushort Vid { get; init; }
            public ushort Pid { get; init; }
            public string Name { get; init; } = "";
            public int ButtonCount { get; init; }
            public bool HasPov { get; init; }
            public bool Connected { get; init; }

            public string Key => $"{Vid:X4}:{Pid:X4}";

            public override string ToString() => Connected ? Name : $"(未接続) {Name}";
        }

        private sealed class InputItem
        {
            public JoyInput Input { get; init; }

            public override string ToString() => Input.DisplayName;
        }

        private readonly JoystickMonitor _monitor;
        private readonly ComboBox _cmbDevice;
        private readonly ComboBox _cmbInput;
        private readonly Button _btnDetect;
        private readonly Button _btnClear;
        private readonly System.Windows.Forms.Timer _captureTimer;
        private JoyBinding? _value;
        private bool _updating;

        /// <summary>「検出」の待機中かどうか。</summary>
        public bool IsCapturing { get; private set; }

        /// <summary>現在の割り当て (未割当は null)。</summary>
        public JoyBinding? Value
        {
            get => _value;
            set
            {
                _value = value;
                RebuildDevices();
                RebuildInputs();
            }
        }

        public JoyBindingEditor(JoystickMonitor monitor)
        {
            _monitor = monitor;
            Size = new Size(356, 25);

            _cmbDevice = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(0, 1),
                Size = new Size(160, 23),
            };
            _cmbDevice.DropDown += (_, _) =>
            {
                // 開くたびに、その時点で接続中のコントローラーへ更新する
                _monitor.Rescan();
                RebuildDevices();
            };
            _cmbDevice.SelectedIndexChanged += OnDeviceChanged;

            _cmbInput = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(166, 1),
                Size = new Size(84, 23),
            };
            _cmbInput.SelectedIndexChanged += OnInputChanged;

            _btnDetect = new Button
            {
                Text = DetectText,
                Location = new Point(256, 0),
                Size = new Size(48, 25),
            };
            _btnDetect.Click += (_, _) =>
            {
                if (IsCapturing) EndCapture();
                else BeginCapture();
            };

            _btnClear = new Button
            {
                Text = "解除",
                Location = new Point(308, 0),
                Size = new Size(48, 25),
            };
            _btnClear.Click += (_, _) =>
            {
                EndCapture();
                Value = null;
            };

            _captureTimer = new System.Windows.Forms.Timer { Interval = CaptureTimeoutMs };
            _captureTimer.Tick += (_, _) => EndCapture();

            Controls.AddRange(new Control[] { _cmbDevice, _cmbInput, _btnDetect, _btnClear });
        }

        /// <summary>
        /// コントローラー一覧を作り直す。割り当て済みの機種が接続されていなければ「(未接続)」として追加する。
        /// 同じ VID/PID が複数接続されていても 1 件にまとめる。
        /// </summary>
        private void RebuildDevices()
        {
            _updating = true;
            try
            {
                _cmbDevice.Items.Clear();
                var seen = new HashSet<string>();
                DeviceItem? selected = null;

                foreach (var device in _monitor.Devices)
                {
                    if (!seen.Add(device.DeviceKey)) continue;

                    var item = new DeviceItem
                    {
                        Vid = device.Vid,
                        Pid = device.Pid,
                        Name = device.Name,
                        ButtonCount = device.ButtonCount,
                        HasPov = device.HasPov,
                        Connected = true,
                    };
                    _cmbDevice.Items.Add(item);
                    if (_value != null && item.Key == _value.DeviceKey)
                    {
                        selected = item;
                    }
                }

                if (_value != null && selected == null)
                {
                    selected = new DeviceItem
                    {
                        Vid = _value.Vid,
                        Pid = _value.Pid,
                        Name = JoystickMonitor.LookupName(_value.Vid, _value.Pid),
                        Connected = false,
                    };
                    _cmbDevice.Items.Add(selected);
                }

                _cmbDevice.SelectedItem = selected;
            }
            finally
            {
                _updating = false;
            }
        }

        /// <summary>
        /// 選択中のコントローラーに合わせて、ボタン一覧を作り直す。
        /// 接続中ならそのボタン数と十字キーの有無に合わせ、未接続なら 1〜32 と十字キーをすべて出す。
        /// </summary>
        private void RebuildInputs()
        {
            _updating = true;
            try
            {
                _cmbInput.Items.Clear();
                if (_cmbDevice.SelectedItem is not DeviceItem device)
                {
                    return;
                }

                int buttonCount = device.Connected ? device.ButtonCount : JoyInput.MaxButtons;
                bool hasPov = !device.Connected || device.HasPov;
                bool valueBelongsToDevice = _value != null && device.Key == _value.DeviceKey;
                InputItem? selected = null;

                void Add(JoyInput input)
                {
                    var item = new InputItem { Input = input };
                    _cmbInput.Items.Add(item);
                    if (valueBelongsToDevice && _value!.Input == input)
                    {
                        selected = item;
                    }
                }

                for (int i = 1; i <= buttonCount; i++)
                {
                    Add(JoyInput.ForButton(i));
                }

                if (hasPov)
                {
                    Add(JoyInput.ForPov(JoyInputKind.PovUp));
                    Add(JoyInput.ForPov(JoyInputKind.PovRight));
                    Add(JoyInput.ForPov(JoyInputKind.PovDown));
                    Add(JoyInput.ForPov(JoyInputKind.PovLeft));
                }

                // 保存済みの入力が一覧にない (ボタン数が減った等) ときも、表示と保存値を一致させる
                if (valueBelongsToDevice && selected == null)
                {
                    Add(_value!.Input);
                }

                _cmbInput.SelectedItem = selected;
            }
            finally
            {
                _updating = false;
            }
        }

        private void OnDeviceChanged(object? sender, EventArgs e)
        {
            if (_updating || _cmbDevice.SelectedItem is not DeviceItem device)
            {
                return;
            }

            // コントローラーを選んだら、まず Button 1 を選んだ状態にする (表示と保存値を常に一致させる)
            _value = new JoyBinding(device.Vid, device.Pid, JoyInput.ForButton(1));
            RebuildInputs();
        }

        private void OnInputChanged(object? sender, EventArgs e)
        {
            if (_updating ||
                _cmbDevice.SelectedItem is not DeviceItem device ||
                _cmbInput.SelectedItem is not InputItem input)
            {
                return;
            }

            _value = new JoyBinding(device.Vid, device.Pid, input.Input);
        }

        private void BeginCapture()
        {
            IsCapturing = true;
            _btnDetect.Text = WaitingText;
            _monitor.InputPressed += OnCaptured;
            _captureTimer.Start();
        }

        private void EndCapture()
        {
            if (!IsCapturing) return;

            IsCapturing = false;
            _captureTimer.Stop();
            _monitor.InputPressed -= OnCaptured;
            _btnDetect.Text = DetectText;
        }

        private void OnCaptured(JoyBinding pressed)
        {
            EndCapture();

            // 待機中に接続されたコントローラーも一覧に含める
            _monitor.Rescan();
            Value = pressed;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                EndCapture();
                _captureTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
