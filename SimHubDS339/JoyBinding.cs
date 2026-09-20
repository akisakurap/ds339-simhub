using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace SimHubDS339
{
    /// <summary>
    /// コントローラー上の入力の種類 (ボタン / 十字キーの 4 方向)。
    /// </summary>
    internal enum JoyInputKind
    {
        /// <summary>標準ボタン (Button 1〜32)。</summary>
        Button,
        /// <summary>POV (ハットスイッチ/十字キー) 上。</summary>
        PovUp,
        /// <summary>POV (ハットスイッチ/十字キー) 右。</summary>
        PovRight,
        /// <summary>POV (ハットスイッチ/十字キー) 下。</summary>
        PovDown,
        /// <summary>POV (ハットスイッチ/十字キー) 左。</summary>
        PovLeft,
    }

    /// <summary>
    /// コントローラー上の 1 つの入力 (ボタン番号 1..32、または十字キーの方向)。
    /// </summary>
    internal readonly record struct JoyInput(JoyInputKind Kind, int Button)
    {
        /// <summary>winmm が扱えるボタン数の上限。</summary>
        public const int MaxButtons = 32;

        /// <summary>指定されたボタン番号 (1..32) から JoyInput を作成する。</summary>
        public static JoyInput ForButton(int button) => new(JoyInputKind.Button, button);

        /// <summary>指定された POV 方向から JoyInput を作成する。</summary>
        public static JoyInput ForPov(JoyInputKind kind) => new(kind, 0);

        /// <summary>設定ファイル用の短い表記 ("B25"、"PovUp" など)。</summary>
        public string Token => Kind == JoyInputKind.Button ? $"B{Button}" : Kind.ToString();

        /// <summary>画面表示用の名前 ("Button 25"、"POV ↑" など)。</summary>
        public string DisplayName => Kind switch
        {
            JoyInputKind.Button => $"Button {Button}",
            JoyInputKind.PovUp => "POV ↑",
            JoyInputKind.PovRight => "POV →",
            JoyInputKind.PovDown => "POV ↓",
            JoyInputKind.PovLeft => "POV ←",
            _ => Kind.ToString(),
        };

        /// <summary>
        /// "B25" や "PovUp" などの表記から入力を解析する (大文字小文字は区別しない)。
        /// </summary>
        public static bool TryParseToken(string token, out JoyInput input)
        {
            input = default;

            // "B1"〜"B32" (大文字小文字不問) の解析
            if (token.Length >= 2 && (token[0] == 'B' || token[0] == 'b') &&
                int.TryParse(token.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out int button) &&
                button >= 1 && button <= MaxButtons)
            {
                input = ForButton(button);
                return true;
            }

            // POV 方向 (PovUp, PovRight, PovDown, PovLeft) の解析
            foreach (var kind in new[] { JoyInputKind.PovUp, JoyInputKind.PovRight, JoyInputKind.PovDown, JoyInputKind.PovLeft })
            {
                if (token.Equals(kind.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    input = ForPov(kind);
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// 「どのコントローラー (VID/PID) のどの入力か」を表すイミュータブルなクラス。
    /// </summary>
    internal sealed class JoyBinding : IEquatable<JoyBinding>
    {
        /// <summary>ベンダー ID (Vendor ID)。</summary>
        public ushort Vid { get; }

        /// <summary>プロダクト ID (Product ID)。</summary>
        public ushort Pid { get; }

        /// <summary>割り当てられた入力 (ボタンまたは POV 方向)。</summary>
        public JoyInput Input { get; }

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="vid">ベンダー ID</param>
        /// <param name="pid">プロダクト ID</param>
        /// <param name="input">入力内容</param>
        public JoyBinding(ushort vid, ushort pid, JoyInput input)
        {
            Vid = vid;
            Pid = pid;
            Input = input;
        }

        /// <summary>機種の識別キー ("044F:B66F")。</summary>
        public string DeviceKey => $"{Vid:X4}:{Pid:X4}";

        /// <summary>
        /// "044F:B66F:B25" 形式の文字列表現を取得する。
        /// </summary>
        public override string ToString() => $"{DeviceKey}:{Input.Token}";

        /// <summary>
        /// "044F:B66F:B25" や "045E:028E:PovUp" などの文字列から JoyBinding を解析する。
        /// </summary>
        public static bool TryParse(string? text, [NotNullWhen(true)] out JoyBinding? binding)
        {
            binding = null;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var parts = text.Split(':', StringSplitOptions.TrimEntries);
            if (parts.Length != 3) return false;

            if (!ushort.TryParse(parts[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var vid)) return false;
            if (!ushort.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var pid)) return false;
            if (!JoyInput.TryParseToken(parts[2], out var input)) return false;

            binding = new JoyBinding(vid, pid, input);
            return true;
        }

        /// <summary>
        /// 他の JoyBinding インスタンスとの等価性を比較する。
        /// </summary>
        public bool Equals(JoyBinding? other)
        {
            if (other is null) return false;
            return Vid == other.Vid && Pid == other.Pid && Input == other.Input;
        }

        /// <summary>
        /// 指定されたオブジェクトとの等価性を比較する。
        /// </summary>
        public override bool Equals(object? obj) => Equals(obj as JoyBinding);

        /// <summary>
        /// ハッシュコードを取得する。
        /// </summary>
        public override int GetHashCode() => HashCode.Combine(Vid, Pid, Input);
    }
}
