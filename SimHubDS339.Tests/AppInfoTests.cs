using Xunit;

namespace SimHubDS339.Tests;

/// <summary>
/// アプリケーション情報 (AppInfo) の単体テスト。
/// バージョン文字列のフォーマットおよび既定値からの変更を検証する。
/// </summary>
public class AppInfoTests
{
    /// <summary>
    /// バージョン文字列が "X.Y.Z" 形式（3つの数字がドットで区切られた形式）であることを検証する。
    /// </summary>
    [Fact]
    public void VersionText_IsThreePartNumber()
    {
        Assert.Matches(@"^\d+\.\d+\.\d+$", AppInfo.VersionText);
    }

    /// <summary>
    /// バージョン文字列が既定値 "1.0.0" ではないことを検証する。
    /// (csproj に Version を設定し忘れると既定の 1.0.0 になるため)
    /// </summary>
    [Fact]
    public void VersionText_IsNotTheDefaultVersion()
    {
        // csproj に Version を設定し忘れると、既定の 1.0.0 になる
        Assert.NotEqual("1.0.0", AppInfo.VersionText);
    }
}
