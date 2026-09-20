using Xunit;

namespace SimHubDS339.Tests;

/// <summary>
/// AppSettings クラスの読み込み、保存、移行ロジックを検証する単体テスト。
/// 実際の %APPDATA% には触れず、一時ディレクトリを使用してテストを実施する。
/// </summary>
public class AppSettingsTests : IDisposable
{
    // テスト用の一時ディレクトリパス
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "SimHubDS339Tests_" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// テスト初期化: 一時ディレクトリを作成する。
    /// </summary>
    public AppSettingsTests()
    {
        Directory.CreateDirectory(_dir);
    }

    /// <summary>
    /// テストクリーンアップ: 一時ディレクトリを削除する。
    /// </summary>
    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    /// <summary>
    /// 一時ディレクトリに JSON を書き出し、指定パスから AppSettings を読み込むヘルパー。
    /// </summary>
    private AppSettings LoadFromJson(string json)
    {
        var path = Path.Combine(_dir, "settings.json");
        File.WriteAllText(path, json);
        return AppSettings.Load(path);
    }

    /// <summary>
    /// 旧既定ホットキー (Ctrl+Alt+Shift+PageDown / PageUp) が新既定値へ移行されることを検証する。
    /// </summary>
    [Fact]
    public void Load_MigratesLegacyDefaultHotkeys()
    {
        var settings = LoadFromJson("""
            { "nextPageHotkey": "Ctrl+Alt+Shift+PageDown", "prevPageHotkey": "Ctrl+Alt+Shift+PageUp" }
            """);

        Assert.Equal(HotkeyBinding.DefaultNext, settings.NextPageHotkey);
        Assert.Equal(HotkeyBinding.DefaultPrev, settings.PrevPageHotkey);
    }

    /// <summary>
    /// ユーザーが独自にカスタマイズしたホットキーは移行されずに保持されることを検証する。
    /// </summary>
    [Fact]
    public void Load_KeepsCustomHotkeys()
    {
        var settings = LoadFromJson("""
            { "nextPageHotkey": "Ctrl+Shift+F5", "prevPageHotkey": "Ctrl+Shift+F6" }
            """);

        Assert.Equal("Ctrl+Shift+F5", settings.NextPageHotkey);
        Assert.Equal("Ctrl+Shift+F6", settings.PrevPageHotkey);
    }

    /// <summary>
    /// 片方だけが旧既定値である場合、その旧既定値の側だけが移行されることを検証する。
    /// </summary>
    [Fact]
    public void Load_MigratesOnlyTheLegacyOne()
    {
        var settings = LoadFromJson("""
            { "nextPageHotkey": "Alt+Q", "prevPageHotkey": "Ctrl+Alt+Shift+PageUp" }
            """);

        Assert.Equal("Alt+Q", settings.NextPageHotkey);
        Assert.Equal(HotkeyBinding.DefaultPrev, settings.PrevPageHotkey);
    }

    /// <summary>
    /// 新既定値に移行すると反対側のキーと衝突してしまう場合、移行を行わないことを検証する。
    /// </summary>
    [Fact]
    public void Load_DoesNotMigrateWhenNewDefaultWouldCollideWithTheOtherKey()
    {
        var settings = LoadFromJson("""
            { "nextPageHotkey": "Ctrl+Alt+PageUp", "prevPageHotkey": "Ctrl+Alt+Shift+PageUp" }
            """);

        Assert.Equal("Ctrl+Alt+PageUp", settings.NextPageHotkey);
        Assert.Equal("Ctrl+Alt+Shift+PageUp", settings.PrevPageHotkey);
    }

    /// <summary>
    /// 設定ファイルが存在しない場合、各項目の既定値が返されることを検証する。
    /// </summary>
    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var settings = AppSettings.Load(Path.Combine(_dir, "does-not-exist.json"));

        Assert.Equal(HotkeyBinding.DefaultNext, settings.NextPageHotkey);
        Assert.Equal(HotkeyBinding.DefaultPrev, settings.PrevPageHotkey);
        Assert.Equal("", settings.NextPageJoy);
        Assert.Equal("", settings.PrevPageJoy);
        Assert.Null(settings.GetNextPageJoyBinding());
        Assert.Null(settings.GetPrevPageJoyBinding());
    }

    /// <summary>
    /// 設定ファイルが破損している場合、既定値のインスタンスが返されることを検証する。
    /// </summary>
    [Fact]
    public void Load_CorruptFile_ReturnsDefaults()
    {
        var settings = LoadFromJson("{ not json");

        Assert.Equal(HotkeyBinding.DefaultNext, settings.NextPageHotkey);
        Assert.Equal("", settings.NextPageJoy);
    }

    /// <summary>
    /// コントローラー設定フィールドが Save と Load を通して正しく保存・復元されることを検証する。
    /// </summary>
    [Fact]
    public void JoyFields_RoundTripThroughSaveAndLoad()
    {
        var path = Path.Combine(_dir, "roundtrip.json");
        var settings = new AppSettings
        {
            NextPageJoy = "044F:B66F:B25",
            PrevPageJoy = "044F:B66F:B24",
        };

        settings.Save(path);
        var loaded = AppSettings.Load(path);

        Assert.Contains("\"nextPageJoy\"", File.ReadAllText(path));
        Assert.Equal("044F:B66F:B25", loaded.NextPageJoy);
        Assert.Equal("044F:B66F:B24", loaded.PrevPageJoy);
        Assert.Equal(new JoyBinding(0x044F, 0xB66F, JoyInput.ForButton(25)), loaded.GetNextPageJoyBinding());
        Assert.Equal(new JoyBinding(0x044F, 0xB66F, JoyInput.ForButton(24)), loaded.GetPrevPageJoyBinding());
    }

    /// <summary>
    /// コントローラー設定の文字列が不正な場合、未割当 (null) として扱われることを検証する。
    /// </summary>
    [Fact]
    public void InvalidJoyText_IsTreatedAsUnassigned()
    {
        var settings = LoadFromJson("""
            { "nextPageJoy": "garbage", "prevPageJoy": "044F:B66F:B99" }
            """);

        Assert.Null(settings.GetNextPageJoyBinding());
        Assert.Null(settings.GetPrevPageJoyBinding());
    }
}
