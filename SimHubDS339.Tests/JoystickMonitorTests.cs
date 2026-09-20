using Xunit;

namespace SimHubDS339.Tests;

/// <summary>
/// JoystickMonitor の押下検出ロジック (DetectPresses / PovToKind) の単体テストクラス。
/// </summary>
public class JoystickMonitorTests
{
    // 十字キー (POV) が中立状態を表す値 (0xFFFF)
    private const uint Centered = 0xFFFF;

    // 指定したボタン番号 (1..32) に対応するビットフラグを取得するヘルパー
    private static uint Bit(int button) => 1u << (button - 1);

    /// <summary>
    /// ボタン・POVともに状態変化がない場合、押下イベントは発生しないこと。
    /// </summary>
    [Fact]
    public void NoChange_ProducesNoPresses()
    {
        Assert.Empty(JoystickMonitor.DetectPresses(0, 0, Centered, Centered));
    }

    /// <summary>
    /// ボタンがオフからオンに変化したとき (立ち上がり)、押下として検出されること。
    /// </summary>
    [Fact]
    public void ButtonGoingDown_IsPressed()
    {
        var presses = JoystickMonitor.DetectPresses(0, Bit(25), Centered, Centered);

        Assert.Equal(new[] { JoyInput.ForButton(25) }, presses);
    }

    /// <summary>
    /// ボタンが押され続けている場合、再度の押下としては検出されないこと。
    /// </summary>
    [Fact]
    public void HeldButton_IsNotPressedAgain()
    {
        Assert.Empty(JoystickMonitor.DetectPresses(Bit(25), Bit(25), Centered, Centered));
    }

    /// <summary>
    /// ボタンが離されたとき (立ち下がり)、押下としては検出されないこと。
    /// </summary>
    [Fact]
    public void ReleasedButton_IsNotAPress()
    {
        Assert.Empty(JoystickMonitor.DetectPresses(Bit(25), 0, Centered, Centered));
    }

    /// <summary>
    /// 2つのボタンが同時に押された場合、ボタン番号の昇順で報告されること。
    /// </summary>
    [Fact]
    public void TwoButtonsAtOnce_AreReportedInAscendingOrder()
    {
        var presses = JoystickMonitor.DetectPresses(0, Bit(25) | Bit(1), Centered, Centered);

        Assert.Equal(new[] { JoyInput.ForButton(1), JoyInput.ForButton(25) }, presses);
    }

    /// <summary>
    /// 別のボタンが押下継続中に新たなボタンが押された場合、新たなボタンのみ報告されること。
    /// </summary>
    [Fact]
    public void NewButtonWhileAnotherIsHeld_OnlyReportsTheNewOne()
    {
        var presses = JoystickMonitor.DetectPresses(Bit(24), Bit(24) | Bit(25), Centered, Centered);

        Assert.Equal(new[] { JoyInput.ForButton(25) }, presses);
    }

    /// <summary>
    /// POV が中立から各方向へ倒されたとき、対応する方向の押下として検出されること。
    /// </summary>
    [Theory]
    [InlineData(0u, JoyInputKind.PovUp)]
    [InlineData(9000u, JoyInputKind.PovRight)]
    [InlineData(18000u, JoyInputKind.PovDown)]
    [InlineData(27000u, JoyInputKind.PovLeft)]
    internal void PovFromCentered_IsPressed(uint pov, JoyInputKind expected)
    {
        var presses = JoystickMonitor.DetectPresses(0, 0, Centered, pov);

        Assert.Equal(new[] { JoyInput.ForPov(expected) }, presses);
    }

    /// <summary>
    /// POV が倒され続けている場合、または中立へ戻った場合は押下として検出されないこと。
    /// </summary>
    [Fact]
    public void PovHeldOrReleased_IsNotPressed()
    {
        Assert.Empty(JoystickMonitor.DetectPresses(0, 0, 0, 0));
        Assert.Empty(JoystickMonitor.DetectPresses(0, 0, 0, Centered));
    }

    /// <summary>
    /// POV がある方向から別の方向へ切り替わった場合、新しい方向の押下として検出されること。
    /// </summary>
    [Fact]
    public void PovChangingDirection_IsPressedForTheNewDirection()
    {
        var presses = JoystickMonitor.DetectPresses(0, 0, 0, 9000);

        Assert.Equal(new[] { JoyInput.ForPov(JoyInputKind.PovRight) }, presses);
    }

    /// <summary>
    /// POV の斜め入力は無視されること。
    /// </summary>
    [Theory]
    [InlineData(4500u)]
    [InlineData(13500u)]
    public void PovDiagonals_AreIgnored(uint diagonal)
    {
        Assert.Empty(JoystickMonitor.DetectPresses(0, 0, Centered, diagonal));
    }

    /// <summary>
    /// POV の中立値 (0xFFFF, 0xFFFFFFFF) は方向なし (null) と判定されること。
    /// </summary>
    [Theory]
    [InlineData(0xFFFFu)]
    [InlineData(0xFFFFFFFFu)]
    public void PovCentered_HasNoDirection(uint centered)
    {
        Assert.Null(JoystickMonitor.PovToKind(centered));
    }
}
