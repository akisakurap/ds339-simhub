using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace SimHubDS339
{
    /// <summary>
    /// 接続中のゲームコントローラー 1 台の情報。
    /// </summary>
    internal sealed record JoyDeviceInfo(int Index, ushort Vid, ushort Pid, string Name, int ButtonCount, bool HasPov)
    {
        /// <summary>機種の識別キー ("044F:B66F")。</summary>
        public string DeviceKey => $"{Vid:X4}:{Pid:X4}";
    }

    /// <summary>
    /// winmm (joyGetPosEx) でゲームコントローラーのボタン/十字キーを定期的に読み取り、
    /// 押した瞬間を <see cref="InputPressed"/> で通知するクラス。
    /// UI スレッドのタイマーで動作するため、イベントは UI スレッド上で発生する。
    /// 読み取るだけでゲームの入力は奪わず、ウィンドウのフォーカスにも依存しない。
    /// </summary>
    internal sealed class JoystickMonitor : IDisposable
    {
        // 走査対象とする最大デバイス数 (0〜15)
        private const int MaxDevices = 16;
        // 入力ポーリング間隔 (ミリ秒)
        private const int PollIntervalMs = 20;
        // デバイス再走査間隔 (ミリ秒)
        private const int RescanIntervalMs = 2000;

        // winmm API 戻り値: 成功
        private const int JoyErrNoError = 0;
        // winmm API フラグ: 全軸・ボタン・POV を取得
        private const uint JoyReturnAll = 0xFF;   // X, Y, Z, R, U, V, POV, ボタン
        // winmm ケイパビリティフラグ: POV (ハットスイッチ) を持つ
        private const uint JoyCapsHasPov = 0x10;
        // 十字キーが中立 (または十字キーなし) を表す値
        private const uint NoPov = 0xFFFF;

        // OEM デバイス名のレジストリパス
        private const string OemRegistryPath =
            @"System\CurrentControlSet\Control\MediaProperties\PrivateProperties\Joystick\OEM\";

        // ジョイスティック性能情報構造体 (winmm joyGetDevCaps 用)
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct JOYCAPS
        {
            public ushort wMid;
            public ushort wPid;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szPname;
            public uint wXmin, wXmax, wYmin, wYmax, wZmin, wZmax;
            public uint wNumButtons, wPeriodMin, wPeriodMax;
            public uint wRmin, wRmax, wUmin, wUmax, wVmin, wVmax;
            public uint wCaps, wMaxAxes, wNumAxes, wMaxButtons;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szRegKey;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szOEMVxD;
        }

        // ジョイスティック拡張入力情報構造体 (winmm joyGetPosEx 用)
        [StructLayout(LayoutKind.Sequential)]
        private struct JOYINFOEX
        {
            public uint dwSize, dwFlags;
            public uint dwXpos, dwYpos, dwZpos, dwRpos, dwUpos, dwVpos;
            public uint dwButtons, dwButtonNumber, dwPOV;
            public uint dwReserved1, dwReserved2;
        }

        // ジョイスティック性能取得 Win32 API
        [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
        private static extern int joyGetDevCaps(IntPtr uJoyID, ref JOYCAPS pjc, int cbjc);

        // ジョイスティック入力状態取得 Win32 API
        [DllImport("winmm.dll")]
        private static extern int joyGetPosEx(uint uJoyID, ref JOYINFOEX pji);

        // ポーリング実行用のタイマー (UI スレッド上で動作)
        private readonly System.Windows.Forms.Timer _timer;
        // デバイス番号ごとの前回の状態 (ボタン状態フラグ, POV 値)
        private readonly Dictionary<int, (uint Buttons, uint Pov)> _states = new();
        // 検出された接続中デバイス一覧
        private List<JoyDeviceInfo> _devices = new();
        // 初回走査が完了したかどうかのフラグ
        private bool _scanned;
        // 最後にデバイス再走査を行った時刻 (Environment.TickCount64)
        private long _lastScanTick;

        /// <summary>ボタン/十字キーが押された瞬間のイベント (UI スレッド上で発生)。</summary>
        public event Action<JoyBinding>? InputPressed;

        /// <summary>現在接続中のコントローラー一覧 (定期的に更新される)。</summary>
        public IReadOnlyList<JoyDeviceInfo> Devices => _devices;

        /// <summary>
        /// JoystickMonitor の新しいインスタンスを生成する。
        /// </summary>
        public JoystickMonitor()
        {
            _timer = new System.Windows.Forms.Timer { Interval = PollIntervalMs };
            _timer.Tick += (_, _) => Poll();
        }

        /// <summary>ポーリングを開始する。</summary>
        public void Start() => _timer.Start();

        /// <summary>
        /// 1 回分の読み取りを行う (タイマーから呼ばれる)。
        /// 初回に読み取った状態は基準値として記録するだけで、押下とは扱わない。
        /// </summary>
        internal void Poll()
        {
            long now = Environment.TickCount64;
            if (!_scanned || now - _lastScanTick >= RescanIntervalMs)
            {
                Rescan();
            }

            foreach (var device in _devices)
            {
                var info = NewInfo();
                if (joyGetPosEx((uint)device.Index, ref info) != JoyErrNoError)
                {
                    // 読めなくなったら基準値を捨て、次に読めたときに取り直す
                    _states.Remove(device.Index);
                    continue;
                }

                uint pov = device.HasPov ? info.dwPOV : NoPov;
                if (!_states.TryGetValue(device.Index, out var previous))
                {
                    _states[device.Index] = (info.dwButtons, pov);
                    continue;
                }

                _states[device.Index] = (info.dwButtons, pov);
                foreach (var input in DetectPresses(previous.Buttons, info.dwButtons, previous.Pov, pov))
                {
                    InputPressed?.Invoke(new JoyBinding(device.Vid, device.Pid, input));
                }
            }
        }

        /// <summary>
        /// 接続中のコントローラーを走査し直し、<see cref="Devices"/> を更新する。
        /// </summary>
        internal void Rescan()
        {
            var found = new List<JoyDeviceInfo>();
            for (int i = 0; i < MaxDevices; i++)
            {
                var caps = new JOYCAPS();
                if (joyGetDevCaps((IntPtr)i, ref caps, Marshal.SizeOf<JOYCAPS>()) != JoyErrNoError)
                {
                    continue;
                }

                // 未接続の番号は joyGetPosEx がエラーを返すので、ここで除外する
                var info = NewInfo();
                if (joyGetPosEx((uint)i, ref info) != JoyErrNoError)
                {
                    continue;
                }

                found.Add(new JoyDeviceInfo(
                    i,
                    caps.wMid,
                    caps.wPid,
                    LookupName(caps.wMid, caps.wPid),
                    (int)caps.wNumButtons,
                    (caps.wCaps & JoyCapsHasPov) != 0));
            }

            _devices = found;
            _scanned = true;
            _lastScanTick = Environment.TickCount64;

            // なくなったデバイスの基準値を破棄する
            foreach (int index in _states.Keys.Where(k => !found.Any(d => d.Index == k)).ToList())
            {
                _states.Remove(index);
            }
        }

        /// <summary>
        /// コントローラーの表示名をレジストリ (OEMName) から取得する。取得できなければ "VID_xxxx&amp;PID_xxxx"。
        /// winmm の szPname は全機種で同じ文字列になるため使わない。
        /// </summary>
        internal static string LookupName(ushort vid, ushort pid)
        {
            string key = $"VID_{vid:X4}&PID_{pid:X4}";
            try
            {
                using var reg = Registry.CurrentUser.OpenSubKey(OemRegistryPath + key);
                if (reg?.GetValue("OEMName") is string name && !string.IsNullOrWhiteSpace(name))
                {
                    return name;
                }
            }
            catch
            {
                // レジストリを読めなくても、キー名で表示できればよい
            }
            return key;
        }

        /// <summary>
        /// 前回と今回の状態を比べ、今回新しく押された入力 (立ち上がり) を返す。
        /// ボタンは番号の昇順。十字キーは、上/右/下/左のいずれかに「変わった」ときだけ 1 つ返す。
        /// </summary>
        internal static IReadOnlyList<JoyInput> DetectPresses(uint prevButtons, uint curButtons, uint prevPov, uint curPov)
        {
            var presses = new List<JoyInput>();

            uint pressed = curButtons & ~prevButtons;
            for (int bit = 0; bit < JoyInput.MaxButtons; bit++)
            {
                if ((pressed & (1u << bit)) != 0)
                {
                    presses.Add(JoyInput.ForButton(bit + 1));
                }
            }

            var prevDirection = PovToKind(prevPov);
            var curDirection = PovToKind(curPov);
            if (curDirection.HasValue && curDirection != prevDirection)
            {
                presses.Add(JoyInput.ForPov(curDirection.Value));
            }

            return presses;
        }

        /// <summary>
        /// winmm の POV 値 (1/100 度、中立は 0xFFFF) を 4 方向に変換する。中立と斜めは null。
        /// </summary>
        internal static JoyInputKind? PovToKind(uint pov)
        {
            if ((pov & 0xFFFF) == 0xFFFF)
            {
                return null;
            }

            return pov switch
            {
                0 => JoyInputKind.PovUp,
                9000 => JoyInputKind.PovRight,
                18000 => JoyInputKind.PovDown,
                27000 => JoyInputKind.PovLeft,
                _ => null,
            };
        }

        // ゼロクリア済みの JOYINFOEX 構造体を初期化して返す
        private static JOYINFOEX NewInfo() => new()
        {
            dwSize = (uint)Marshal.SizeOf<JOYINFOEX>(),
            dwFlags = JoyReturnAll,
        };

        /// <summary>
        /// タイマーを停止しリソースを解放する。
        /// </summary>
        public void Dispose()
        {
            _timer.Stop();
            _timer.Dispose();
        }
    }
}
