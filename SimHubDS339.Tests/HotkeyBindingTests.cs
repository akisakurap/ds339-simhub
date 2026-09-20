using System.Windows.Forms;
using Xunit;

namespace SimHubDS339.Tests;

/// <summary>
/// HotkeyBinding の単体テストクラス。
/// 既定のホットキー設定や F13〜F24 の単独キー許可、Win32 修飾キー変換などの動作を検証する。
/// </summary>
public class HotkeyBindingTests
{
    /// <summary>
    /// 新しい既定値（3 キー同時押し）が正しく定義されていることを確認する。
    /// </summary>
    [Fact]
    public void Defaults_AreThreeKeyCombos()
    {
        Assert.Equal("Ctrl+Alt+PageDown", HotkeyBinding.DefaultNext);
        Assert.Equal("Ctrl+Alt+PageUp", HotkeyBinding.DefaultPrev);
    }

    /// <summary>
    /// 設定移行判定用の旧既定値（4 キー同時押し）が正しく保持されていることを確認する。
    /// </summary>
    [Fact]
    public void LegacyDefaults_AreTheOldFourKeyCombos()
    {
        Assert.Equal("Ctrl+Alt+Shift+PageDown", HotkeyBinding.LegacyDefaultNext);
        Assert.Equal("Ctrl+Alt+Shift+PageUp", HotkeyBinding.LegacyDefaultPrev);
    }

    /// <summary>
    /// F13〜F24 キーは修飾キーの有無に関わらず受け付けられることを確認する。
    /// </summary>
    [Theory]
    [InlineData("F13")]
    [InlineData("F24")]
    [InlineData("Ctrl+F13")]
    public void TryParse_AcceptsF13ToF24_WithOrWithoutModifier(string text)
    {
        Assert.True(HotkeyBinding.TryParse(text, out var binding));
        Assert.True(binding.IsValid);
        Assert.Equal(text, binding.ToString());
    }

    /// <summary>
    /// F13〜F24 以外の単独キーや修飾キーのみの指定は拒否されることを確認する。
    /// </summary>
    [Theory]
    [InlineData("F12")]
    [InlineData("A")]
    [InlineData("PageDown")]
    [InlineData("Ctrl")]
    [InlineData("")]
    public void TryParse_RejectsSingleKeysOutsideF13ToF24AndModifierOnly(string text)
    {
        Assert.False(HotkeyBinding.TryParse(text, out _));
    }

    /// <summary>
    /// 通常の修飾キー付きホットキー文字列が正しくパースできることを確認する。
    /// </summary>
    [Fact]
    public void TryParse_StillAcceptsModifierCombos()
    {
        Assert.True(HotkeyBinding.TryParse("Ctrl+Alt+PageDown", out var binding));
        Assert.Equal(Keys.Control | Keys.Alt, binding.Modifiers);
        Assert.Equal(Keys.PageDown, binding.Key);
    }

    /// <summary>
    /// IsSingleKeyAllowed が F13〜F24 のみに true を返すことを確認する。
    /// </summary>
    [Theory]
    [InlineData(Keys.F13, true)]
    [InlineData(Keys.F24, true)]
    [InlineData(Keys.F12, false)]
    [InlineData(Keys.PageDown, false)]
    public void IsSingleKeyAllowed_OnlyF13ToF24(Keys key, bool expected)
    {
        Assert.Equal(expected, HotkeyBinding.IsSingleKeyAllowed(key));
    }

    /// <summary>
    /// 単独キー登録時の Win32 修飾キーフラグが MOD_NOREPEAT (0x4000) のみになることを確認する。
    /// </summary>
    [Fact]
    public void Win32Modifiers_ForSingleKeyIsOnlyNoRepeat()
    {
        Assert.Equal(0x4000u, new HotkeyBinding(Keys.None, Keys.F13).GetWin32Modifiers());
    }
}
