using Xunit;

namespace SimHubDS339.Tests;

/// <summary>
/// JoyBinding および JoyInput の単体テストクラス。
/// コントローラー入力の文字列表現、パース処理、等価性比較、表示名生成を検証する。
/// </summary>
public class JoyBindingTests
{
    /// <summary>
    /// ToString メソッドが大文字 16 進数の VID:PID と入力トークン（例: "044F:B66F:B25"）を生成し、
    /// DeviceKey プロパティが正しく返ることを確認する。
    /// </summary>
    [Fact]
    public void ToString_Button_UsesUpperHexVidPidAndButtonToken()
    {
        var binding = new JoyBinding(0x044F, 0xB66F, JoyInput.ForButton(25));

        Assert.Equal("044F:B66F:B25", binding.ToString());
        Assert.Equal("044F:B66F", binding.DeviceKey);
    }

    /// <summary>
    /// ボタンおよび POV の有効な入力文字列が TryParse で解析でき、ToString で同一文字列に戻る（ラウンドトリップ）ことを確認する。
    /// </summary>
    [Theory]
    [InlineData("044F:B66F:B25")]
    [InlineData("044F:B66F:B1")]
    [InlineData("044F:B66F:B32")]
    [InlineData("045E:028E:PovUp")]
    [InlineData("045E:028E:PovRight")]
    [InlineData("045E:028E:PovDown")]
    [InlineData("045E:028E:PovLeft")]
    public void TryParse_RoundTrips(string text)
    {
        Assert.True(JoyBinding.TryParse(text, out var binding));
        Assert.Equal(text, binding.ToString());
    }

    /// <summary>
    /// TryParse が大文字・小文字を区別せずにパースできることを確認する。
    /// </summary>
    [Fact]
    public void TryParse_IsCaseInsensitive()
    {
        Assert.True(JoyBinding.TryParse("044f:b66f:b25", out var binding));
        Assert.Equal(new JoyBinding(0x044F, 0xB66F, JoyInput.ForButton(25)), binding);
    }

    /// <summary>
    /// 不正な文字列（null、空文字、範囲外のボタン番号、不正な形式など）が TryParse で正しく拒否されることを確認する。
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("044F:B66F")]
    [InlineData("044F:B66F:B0")]
    [InlineData("044F:B66F:B33")]
    [InlineData("044F:B66F:Button")]
    [InlineData("044F:B66F:Foo")]
    [InlineData("ZZZZ:B66F:B1")]
    [InlineData("10000:B66F:B1")]
    [InlineData("044F:B66F:B1:extra")]
    public void TryParse_RejectsInvalidText(string? text)
    {
        Assert.False(JoyBinding.TryParse(text, out var binding));
        Assert.Null(binding);
    }

    /// <summary>
    /// JoyBinding の等価性比較が、デバイス（VID/PID）と入力内容（ボタン・POV）の両方を正しく識別することを確認する。
    /// </summary>
    [Fact]
    public void Equals_DistinguishesDeviceAndInput()
    {
        var a = new JoyBinding(0x044F, 0xB66F, JoyInput.ForButton(25));

        Assert.Equal(a, new JoyBinding(0x044F, 0xB66F, JoyInput.ForButton(25)));
        Assert.NotEqual(a, new JoyBinding(0x044F, 0xB66F, JoyInput.ForButton(24)));
        Assert.NotEqual(a, new JoyBinding(0x045E, 0x028E, JoyInput.ForButton(25)));
        Assert.NotEqual(a, new JoyBinding(0x044F, 0xB66F, JoyInput.ForPov(JoyInputKind.PovUp)));
    }

    /// <summary>
    /// JoyInput の DisplayName が人間にとって分かりやすい表記（例: "Button 25", "POV ↑"）になることを確認する。
    /// </summary>
    [Fact]
    public void DisplayName_IsHumanReadable()
    {
        Assert.Equal("Button 25", JoyInput.ForButton(25).DisplayName);
        Assert.Equal("POV ↑", JoyInput.ForPov(JoyInputKind.PovUp).DisplayName);
        Assert.Equal("POV →", JoyInput.ForPov(JoyInputKind.PovRight).DisplayName);
        Assert.Equal("POV ↓", JoyInput.ForPov(JoyInputKind.PovDown).DisplayName);
        Assert.Equal("POV ←", JoyInput.ForPov(JoyInputKind.PovLeft).DisplayName);
    }
}
