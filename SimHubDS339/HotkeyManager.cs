using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace SimHubDS339
{
    /// <summary>
    /// ホットキーのキーの組み合わせを表すイミュータブルなクラス。
    /// </summary>
    internal sealed class HotkeyBinding : IEquatable<HotkeyBinding>
    {
        public const string DefaultNext = "Ctrl+Alt+PageDown";
        public const string DefaultPrev = "Ctrl+Alt+PageUp";

        /// <summary>旧既定値 (4 キー同時押し)。設定ファイルの移行判定にだけ使う。</summary>
        public const string LegacyDefaultNext = "Ctrl+Alt+Shift+PageDown";
        public const string LegacyDefaultPrev = "Ctrl+Alt+Shift+PageUp";

        /// <summary>修飾キー (Keys.Control, Keys.Alt, Keys.Shift 等)</summary>
        public Keys Modifiers { get; }

        /// <summary>主キー (例: Keys.PageDown)</summary>
        public Keys Key { get; }

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="modifiers">修飾キー</param>
        /// <param name="key">主キー</param>
        public HotkeyBinding(Keys modifiers, Keys key)
        {
            Modifiers = modifiers & Keys.Modifiers;
            Key = key & Keys.KeyCode;
        }

        /// <summary>
        /// 主キーが指定され、かつ「修飾キーが 1 つ以上ある」または「F13〜F24 の単独キー」であるかどうか。
        /// </summary>
        public bool IsValid
        {
            get
            {
                if (Key == Keys.None) return false;
                bool hasModifier = (Modifiers & (Keys.Control | Keys.Alt | Keys.Shift)) != Keys.None;
                return hasModifier || IsSingleKeyAllowed(Key);
            }
        }

        /// <summary>
        /// 修飾キーなしで登録を許可するキーかどうか (F13〜F24 のみ。他のアプリやゲームと衝突しにくいため)。
        /// </summary>
        public static bool IsSingleKeyAllowed(Keys key) => key >= Keys.F13 && key <= Keys.F24;

        /// <summary>
        /// Win32 RegisterHotKey 用の修飾キーフラグを取得する。
        /// </summary>
        public uint GetWin32Modifiers()
        {
            uint mod = 0x4000; // MOD_NOREPEAT
            if ((Modifiers & Keys.Alt) != Keys.None) mod |= 0x0001; // MOD_ALT
            if ((Modifiers & Keys.Control) != Keys.None) mod |= 0x0002; // MOD_CONTROL
            if ((Modifiers & Keys.Shift) != Keys.None) mod |= 0x0004; // MOD_SHIFT
            return mod;
        }

        /// <summary>
        /// Win32 RegisterHotKey 用の仮想キーコードを取得する。
        /// </summary>
        public uint GetWin32Key() => (uint)Key;

        /// <summary>
        /// "Ctrl+Alt+Shift+PageDown" 形式の文字列表現を取得する。
        /// </summary>
        public override string ToString()
        {
            var sb = new StringBuilder();
            if ((Modifiers & Keys.Control) != Keys.None) sb.Append("Ctrl+");
            if ((Modifiers & Keys.Alt) != Keys.None) sb.Append("Alt+");
            if ((Modifiers & Keys.Shift) != Keys.None) sb.Append("Shift+");
            sb.Append(Key switch
            {
                Keys.Prior => "PageUp",
                Keys.Next => "PageDown",
                _ => Key.ToString()
            });
            return sb.ToString();
        }

        /// <summary>
        /// "Ctrl+Alt+Shift+PageDown" などの文字列から HotkeyBinding を解析する。
        /// </summary>
        public static bool TryParse(string? text, out HotkeyBinding binding)
        {
            binding = new HotkeyBinding(Keys.None, Keys.None);
            if (string.IsNullOrWhiteSpace(text)) return false;

            var parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0) return false;

            Keys mods = Keys.None;
            Keys key = Keys.None;

            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i];
                if (p.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || p.Equals("Control", StringComparison.OrdinalIgnoreCase))
                {
                    mods |= Keys.Control;
                }
                else if (p.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                {
                    mods |= Keys.Alt;
                }
                else if (p.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                {
                    mods |= Keys.Shift;
                }
                else
                {
                    // 主キーの解析
                    if (p.Equals("PageUp", StringComparison.OrdinalIgnoreCase)) key = Keys.PageUp;
                    else if (p.Equals("PageDown", StringComparison.OrdinalIgnoreCase)) key = Keys.PageDown;
                    else if (Enum.TryParse<Keys>(p, true, out var parsedKey)) key = parsedKey;
                    else return false;
                }
            }

            var result = new HotkeyBinding(mods, key);
            if (!result.IsValid) return false;

            binding = result;
            return true;
        }

        public bool Equals(HotkeyBinding? other)
        {
            if (other is null) return false;
            return Modifiers == other.Modifiers && Key == other.Key;
        }

        public override bool Equals(object? obj) => Equals(obj as HotkeyBinding);

        public override int GetHashCode() => HashCode.Combine(Modifiers, Key);
    }

    /// <summary>
    /// Win32 RegisterHotKey を用いたグローバルホットキー管理クラス。
    /// UI スレッド上で動作する非表示の NativeWindow を通じて WM_HOTKEY メッセージを処理する。
    /// </summary>
    internal sealed class HotkeyManager : IDisposable
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int WM_HOTKEY = 0x0312;
        private const int IdNextPage = 1001;
        private const int IdPrevPage = 1002;

        private readonly HotkeyWindow _window;
        private HotkeyBinding? _registeredNext;
        private HotkeyBinding? _registeredPrev;

        /// <summary>「次のページ」ホットキーが押下されたときのイベント</summary>
        public event Action? NextPageTriggered;

        /// <summary>「前のページ」ホットキーが押下されたときのイベント</summary>
        public event Action? PrevPageTriggered;

        /// <summary>
        /// コンストラクタ。非表示ウィンドウを作成する。
        /// </summary>
        public HotkeyManager()
        {
            _window = new HotkeyWindow(OnHotKey);
        }

        /// <summary>
        /// 次/前のホットキーを登録する。
        /// </summary>
        /// <param name="next">次のページ用ホットキー</param>
        /// <param name="prev">前のページ用ホットキー</param>
        /// <param name="errorMessage">登録失敗時のエラーメッセージ (すべて成功時は null)</param>
        /// <returns>すべて成功したか</returns>
        public bool Register(HotkeyBinding next, HotkeyBinding prev, out string? errorMessage)
        {
            UnregisterAll();
            errorMessage = null;

            if (next.Equals(prev))
            {
                errorMessage = "「次のページ」と「前のページ」に同じホットキーは設定できません。";
                return false;
            }

            bool nextSuccess = RegisterHotKey(_window.Handle, IdNextPage, next.GetWin32Modifiers(), next.GetWin32Key());
            int nextErr = nextSuccess ? 0 : Marshal.GetLastWin32Error();

            bool prevSuccess = RegisterHotKey(_window.Handle, IdPrevPage, prev.GetWin32Modifiers(), prev.GetWin32Key());
            int prevErr = prevSuccess ? 0 : Marshal.GetLastWin32Error();

            if (nextSuccess) _registeredNext = next;
            if (prevSuccess) _registeredPrev = prev;

            if (!nextSuccess && !prevSuccess)
            {
                errorMessage = $"ホットキー {next} および {prev} は他のアプリが使用中のため登録できませんでした。";
                return false;
            }
            if (!nextSuccess)
            {
                errorMessage = $"ホットキー {next} は他のアプリが使用中のため登録できませんでした。設定で変更してください。";
                return false;
            }
            if (!prevSuccess)
            {
                errorMessage = $"ホットキー {prev} は他のアプリが使用中のため登録できませんでした。設定で変更してください。";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 登録されているホットキーをすべて解除する。
        /// </summary>
        public void UnregisterAll()
        {
            if (_registeredNext != null)
            {
                UnregisterHotKey(_window.Handle, IdNextPage);
                _registeredNext = null;
            }
            if (_registeredPrev != null)
            {
                UnregisterHotKey(_window.Handle, IdPrevPage);
                _registeredPrev = null;
            }
        }

        private void OnHotKey(int id)
        {
            if (id == IdNextPage)
            {
                NextPageTriggered?.Invoke();
            }
            else if (id == IdPrevPage)
            {
                PrevPageTriggered?.Invoke();
            }
        }

        public void Dispose()
        {
            UnregisterAll();
            _window.DestroyHandle();
        }

        /// <summary>
        /// WM_HOTKEY を受信するための非表示ウィンドウ。
        /// </summary>
        private sealed class HotkeyWindow : NativeWindow
        {
            private readonly Action<int> _onHotKey;

            public HotkeyWindow(Action<int> onHotKey)
            {
                _onHotKey = onHotKey;
                CreateHandle(new CreateParams());
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_HOTKEY)
                {
                    int id = m.WParam.ToInt32();
                    _onHotKey(id);
                }
                base.WndProc(ref m);
            }
        }
    }
}
