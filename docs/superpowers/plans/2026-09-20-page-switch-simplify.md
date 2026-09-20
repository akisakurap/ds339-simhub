# ページ切り替え簡略化（ホイールボタン対応） Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** レース画面のページ切り替えを、短い既定キーと、コントローラー（T300RS 等）のボタン直接割り当てで操作できるようにする。

**Architecture:** 既存の `RegisterHotKey` ベースの `HotkeyManager` はそのまま残し、winmm (`joyGetPosEx`) を 20ms ごとにポーリングする `JoystickMonitor` を追加する。押下の立ち上がりを `JoyBinding`（VID:PID + ボタン/十字キー）として通知し、`TrayApp` が保存済みの割り当てと照合して `DashboardService.NextPage()` / `PreviousPage()` を呼ぶ。設定画面には、コントローラーとボタンを選ぶ `JoyBindingEditor` を追加する。

**Tech Stack:** C# 12 / .NET 8 (`net8.0-windows`)、WinForms、winmm P/Invoke、xunit（テスト用に新規追加）

**Spec:** `docs/superpowers/specs/2026-09-20-page-switch-simplify-design.md`

## Global Constraints

- `SimHubDS339` 本体に追加する NuGet パッケージはなし。入力検知は `winmm.dll` の P/Invoke のみ（テスト用プロジェクトだけ xunit を使う）。
- 対象は `net8.0-windows`、`Nullable` 有効、`ImplicitUsings` 有効。UI 文字列とコメントは既存に合わせて日本語。
- コントローラーは VID/PID で識別する（デバイス番号は接続順で変わる）。同一 VID/PID が複数ある場合は区別しない。
- ポーリング間隔 20ms、デバイス再走査 2 秒、検出待機タイムアウト 10 秒、走査するデバイス番号は 0〜15、ボタンは 1〜32 まで。
- 十字キー（POV）は上/右/下/左の 4 方向のみ。斜めは無視する。
- 修飾キーなしで許可するキーボードのキーは `F13`〜`F24` のみ。
- 既定キーは `Ctrl+Alt+PageDown`（次）/ `Ctrl+Alt+PageUp`（前）。旧既定 `Ctrl+Alt+Shift+PageDown` / `Ctrl+Alt+Shift+PageUp` と完全一致するときだけ新既定へ移行する。
- コミットメッセージは `<type>: <description>` 形式（feat, fix, refactor, docs, test, chore）。**Co-Authored-By などの帰属行は付けない**（ユーザーのグローバル設定で無効化されている）。
- `bin/`、`obj/`、`publish/` はコミットしない（`.gitignore` 済み）。
- ユーザーの `%APPDATA%\SimHubDS339\settings.json` を書き換えるのは Task 5 の指定手順のみ。書き換える前にトレイアプリを終了してもらう。

## 仕様書からの変更点（Task 1 で仕様書にも反映する）

- 単体テスト用プロジェクト `SimHubDS339.Tests` を新規追加する（仕様書は「手動確認」としていたが、純粋なロジックは自動テストにする）。
- `JoyBinding` は `JoystickMonitor.cs` 内ではなく、別ファイル `JoyBinding.cs` にする。
- 「検出」の中止は Esc ではなく、もう一度「検出」を押すか 10 秒経過。Esc は設定画面の「キャンセル」ボタンとして先に処理されるため。
- `AppSettings` に、テスト用に保存先を指定できる `Load(string path)` / `Save(string path)` を追加する。

## File Structure

- Create `SimHubDS339.Tests/SimHubDS339.Tests.csproj` — xunit テストプロジェクト
- Create `SimHubDS339.Tests/HotkeyBindingTests.cs`
- Create `SimHubDS339.Tests/JoyBindingTests.cs`
- Create `SimHubDS339.Tests/AppSettingsTests.cs`
- Create `SimHubDS339.Tests/JoystickMonitorTests.cs`
- Modify `SimHubDS339/SimHubDS339.csproj` — `InternalsVisibleTo`
- Modify `SimHubDS339/HotkeyManager.cs` — `HotkeyBinding`（既定値、F13〜F24 単独許可）
- Create `SimHubDS339/JoyBinding.cs` — `JoyInputKind`、`JoyInput`、`JoyBinding`
- Modify `SimHubDS339/AppSettings.cs` — 新フィールド、旧既定値の移行、`Load/Save(path)`
- Create `SimHubDS339/JoystickMonitor.cs` — `JoyDeviceInfo`、`JoystickMonitor`
- Create `SimHubDS339/JoyBindingEditor.cs` — 設定画面用のコントローラー/ボタン選択コントロール
- Modify `SimHubDS339/TrayApp.cs` — 監視の生成、照合、破棄
- Modify `SimHubDS339/SettingsForm.cs` — UI 追加、検証、保存
- Modify `README.md`、`SimHubDS339/README.md` — 使い方と更新履歴

---

### Task 1: テストプロジェクトと HotkeyBinding の更新

**Files:**
- Create: `SimHubDS339.Tests/SimHubDS339.Tests.csproj`
- Create: `SimHubDS339.Tests/HotkeyBindingTests.cs`
- Modify: `SimHubDS339/SimHubDS339.csproj`
- Modify: `SimHubDS339/HotkeyManager.cs:13-14`, `HotkeyManager.cs:36-44`, `HotkeyManager.cs:91`
- Modify: `docs/superpowers/specs/2026-09-20-page-switch-simplify-design.md`

**Interfaces:**
- Produces: `HotkeyBinding.DefaultNext == "Ctrl+Alt+PageDown"`、`HotkeyBinding.DefaultPrev == "Ctrl+Alt+PageUp"`、`HotkeyBinding.LegacyDefaultNext == "Ctrl+Alt+Shift+PageDown"`、`HotkeyBinding.LegacyDefaultPrev == "Ctrl+Alt+Shift+PageUp"`、`static bool HotkeyBinding.IsSingleKeyAllowed(Keys key)`（F13〜F24 のとき true）。`HotkeyBinding.TryParse("F13", ...)` が true を返す。

- [ ] **Step 1: テストプロジェクトを作り、本体から internal を見えるようにする**

`SimHubDS339.Tests/SimHubDS339.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\SimHubDS339\SimHubDS339.csproj" />
  </ItemGroup>

</Project>
```

`SimHubDS339/SimHubDS339.csproj` の `</Project>` の直前に追加:

```xml
  <ItemGroup>
    <InternalsVisibleTo Include="SimHubDS339.Tests" />
  </ItemGroup>
```

- [ ] **Step 2: 失敗するテストを書く**

`SimHubDS339.Tests/HotkeyBindingTests.cs`:

```csharp
using System.Windows.Forms;
using Xunit;

namespace SimHubDS339.Tests;

public class HotkeyBindingTests
{
    [Fact]
    public void Defaults_AreThreeKeyCombos()
    {
        Assert.Equal("Ctrl+Alt+PageDown", HotkeyBinding.DefaultNext);
        Assert.Equal("Ctrl+Alt+PageUp", HotkeyBinding.DefaultPrev);
    }

    [Fact]
    public void LegacyDefaults_AreTheOldFourKeyCombos()
    {
        Assert.Equal("Ctrl+Alt+Shift+PageDown", HotkeyBinding.LegacyDefaultNext);
        Assert.Equal("Ctrl+Alt+Shift+PageUp", HotkeyBinding.LegacyDefaultPrev);
    }

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

    [Fact]
    public void TryParse_StillAcceptsModifierCombos()
    {
        Assert.True(HotkeyBinding.TryParse("Ctrl+Alt+PageDown", out var binding));
        Assert.Equal(Keys.Control | Keys.Alt, binding.Modifiers);
        Assert.Equal(Keys.PageDown, binding.Key);
    }

    [Theory]
    [InlineData(Keys.F13, true)]
    [InlineData(Keys.F24, true)]
    [InlineData(Keys.F12, false)]
    [InlineData(Keys.PageDown, false)]
    public void IsSingleKeyAllowed_OnlyF13ToF24(Keys key, bool expected)
    {
        Assert.Equal(expected, HotkeyBinding.IsSingleKeyAllowed(key));
    }

    [Fact]
    public void Win32Modifiers_ForSingleKeyIsOnlyNoRepeat()
    {
        Assert.Equal(0x4000u, new HotkeyBinding(Keys.None, Keys.F13).GetWin32Modifiers());
    }
}
```

- [ ] **Step 3: テストを実行して失敗を確認する**

Run: `dotnet test SimHubDS339.Tests --nologo` （`D:\PG\DS339` で実行。初回は NuGet の復元が走る）
Expected: ビルドエラー `CS0117: 'HotkeyBinding' に 'LegacyDefaultNext' の定義がありません` などで FAIL。

- [ ] **Step 4: 最小実装**

`SimHubDS339/HotkeyManager.cs` の 13〜14 行目を置き換える:

```csharp
        public const string DefaultNext = "Ctrl+Alt+PageDown";
        public const string DefaultPrev = "Ctrl+Alt+PageUp";

        /// <summary>旧既定値 (4 キー同時押し)。設定ファイルの移行判定にだけ使う。</summary>
        public const string LegacyDefaultNext = "Ctrl+Alt+Shift+PageDown";
        public const string LegacyDefaultPrev = "Ctrl+Alt+Shift+PageUp";
```

`IsValid`（元の 33〜45 行目、コメント含む）を次に置き換える:

```csharp
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
```

`TryParse` 内の `if (parts.Length < 2) return false;` を次に置き換える:

```csharp
            if (parts.Length == 0) return false;
```

- [ ] **Step 5: テストを実行して成功を確認する**

Run: `dotnet test SimHubDS339.Tests --nologo`
Expected: PASS（`HotkeyBindingTests` の全ケースが成功）

- [ ] **Step 6: 仕様書を実態に合わせる**

`docs/superpowers/specs/2026-09-20-page-switch-simplify-design.md` を 3 か所編集する。

1. `### 1. \`JoyBinding\`（新規、\`JoystickMonitor.cs\` 内）` → `### 1. \`JoyBinding\`（新規、\`JoyBinding.cs\`）`
2. `- 「検出」ボタン: 押すと待機状態になり、次に押されたボタンのデバイスとボタンをコンボボックスに反映する。Esc または 10 秒で中止。` → `- 「検出」ボタン: 押すと待機状態になり、次に押されたボタンのデバイスとボタンをコンボボックスに反映する。もう一度「検出」を押すか 10 秒で中止（Esc は設定画面の「キャンセル」として先に処理されるため使わない）。`
3. `- 単体テストのプロジェクトはないため、\`JoyBinding.TryParse\` / \`ToString\` と \`AppSettings\` の旧既定値移行は、ビルド後に手動で確認する。` → `- 純粋なロジック（\`HotkeyBinding\`、\`JoyBinding\`、\`AppSettings\` の移行、押下検出）は \`SimHubDS339.Tests\`（xunit）で自動テストする。`

- [ ] **Step 7: コミット**

```bash
git add SimHubDS339.Tests SimHubDS339/SimHubDS339.csproj SimHubDS339/HotkeyManager.cs docs/superpowers/specs/2026-09-20-page-switch-simplify-design.md
git commit -m "feat: shorten default page hotkeys and allow F13-F24 single keys"
```

---

### Task 2: JoyBinding（コントローラー入力の値オブジェクト）

**Files:**
- Create: `SimHubDS339/JoyBinding.cs`
- Create: `SimHubDS339.Tests/JoyBindingTests.cs`

**Interfaces:**
- Produces:
  - `enum JoyInputKind { Button, PovUp, PovRight, PovDown, PovLeft }`
  - `readonly record struct JoyInput(JoyInputKind Kind, int Button)`: `const int MaxButtons = 32`、`static JoyInput ForButton(int)`、`static JoyInput ForPov(JoyInputKind)`、`string Token`、`string DisplayName`、`static bool TryParseToken(string, out JoyInput)`
  - `sealed class JoyBinding : IEquatable<JoyBinding>`: `ushort Vid`、`ushort Pid`、`JoyInput Input`、`string DeviceKey`（`"044F:B66F"`）、`ToString()`（`"044F:B66F:B25"`）、`static bool TryParse(string?, [NotNullWhen(true)] out JoyBinding?)`

- [ ] **Step 1: 失敗するテストを書く**

`SimHubDS339.Tests/JoyBindingTests.cs`:

```csharp
using Xunit;

namespace SimHubDS339.Tests;

public class JoyBindingTests
{
    [Fact]
    public void ToString_Button_UsesUpperHexVidPidAndButtonToken()
    {
        var binding = new JoyBinding(0x044F, 0xB66F, JoyInput.ForButton(25));

        Assert.Equal("044F:B66F:B25", binding.ToString());
        Assert.Equal("044F:B66F", binding.DeviceKey);
    }

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

    [Fact]
    public void TryParse_IsCaseInsensitive()
    {
        Assert.True(JoyBinding.TryParse("044f:b66f:b25", out var binding));
        Assert.Equal(new JoyBinding(0x044F, 0xB66F, JoyInput.ForButton(25)), binding);
    }

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

    [Fact]
    public void Equals_DistinguishesDeviceAndInput()
    {
        var a = new JoyBinding(0x044F, 0xB66F, JoyInput.ForButton(25));

        Assert.Equal(a, new JoyBinding(0x044F, 0xB66F, JoyInput.ForButton(25)));
        Assert.NotEqual(a, new JoyBinding(0x044F, 0xB66F, JoyInput.ForButton(24)));
        Assert.NotEqual(a, new JoyBinding(0x045E, 0x028E, JoyInput.ForButton(25)));
        Assert.NotEqual(a, new JoyBinding(0x044F, 0xB66F, JoyInput.ForPov(JoyInputKind.PovUp)));
    }

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
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `dotnet test SimHubDS339.Tests --nologo`
Expected: ビルドエラー `CS0246: 型または名前空間の名前 'JoyBinding' が見つかりませんでした` で FAIL。

- [ ] **Step 3: 実装**

`SimHubDS339/JoyBinding.cs`:

```csharp
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace SimHubDS339
{
    /// <summary>
    /// コントローラー上の入力の種類 (ボタン / 十字キーの 4 方向)。
    /// </summary>
    internal enum JoyInputKind
    {
        Button,
        PovUp,
        PovRight,
        PovDown,
        PovLeft,
    }

    /// <summary>
    /// コントローラー上の 1 つの入力 (ボタン番号 1..32、または十字キーの方向)。
    /// </summary>
    internal readonly record struct JoyInput(JoyInputKind Kind, int Button)
    {
        /// <summary>winmm が扱えるボタン数の上限。</summary>
        public const int MaxButtons = 32;

        public static JoyInput ForButton(int button) => new(JoyInputKind.Button, button);

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

            if (token.Length >= 2 && (token[0] == 'B' || token[0] == 'b') &&
                int.TryParse(token.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out int button) &&
                button >= 1 && button <= MaxButtons)
            {
                input = ForButton(button);
                return true;
            }

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
        public ushort Vid { get; }
        public ushort Pid { get; }
        public JoyInput Input { get; }

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

        public bool Equals(JoyBinding? other)
        {
            if (other is null) return false;
            return Vid == other.Vid && Pid == other.Pid && Input == other.Input;
        }

        public override bool Equals(object? obj) => Equals(obj as JoyBinding);

        public override int GetHashCode() => HashCode.Combine(Vid, Pid, Input);
    }
}
```

- [ ] **Step 4: テストを実行して成功を確認する**

Run: `dotnet test SimHubDS339.Tests --nologo`
Expected: PASS（`JoyBindingTests` を含む全テストが成功）

- [ ] **Step 5: コミット**

```bash
git add SimHubDS339/JoyBinding.cs SimHubDS339.Tests/JoyBindingTests.cs
git commit -m "feat: add JoyBinding value type for controller button assignments"
```

---

### Task 3: AppSettings（新フィールド、旧既定値の移行）

**Files:**
- Modify: `SimHubDS339/AppSettings.cs`
- Create: `SimHubDS339.Tests/AppSettingsTests.cs`

**Interfaces:**
- Consumes: `HotkeyBinding.DefaultNext/DefaultPrev/LegacyDefaultNext/LegacyDefaultPrev`（Task 1）、`JoyBinding.TryParse`（Task 2）
- Produces: `string AppSettings.NextPageJoy` / `PrevPageJoy`（JSON キー `nextPageJoy` / `prevPageJoy`、既定 `""`）、`JoyBinding? AppSettings.GetNextPageJoyBinding()` / `GetPrevPageJoyBinding()`、`static AppSettings AppSettings.Load(string path)`（internal）、`void AppSettings.Save(string path)`（internal）

- [ ] **Step 1: 失敗するテストを書く**

`SimHubDS339.Tests/AppSettingsTests.cs`:

```csharp
using Xunit;

namespace SimHubDS339.Tests;

public class AppSettingsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "SimHubDS339Tests_" + Guid.NewGuid().ToString("N"));

    public AppSettingsTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private AppSettings LoadFromJson(string json)
    {
        var path = Path.Combine(_dir, "settings.json");
        File.WriteAllText(path, json);
        return AppSettings.Load(path);
    }

    [Fact]
    public void Load_MigratesLegacyDefaultHotkeys()
    {
        var settings = LoadFromJson("""
            { "nextPageHotkey": "Ctrl+Alt+Shift+PageDown", "prevPageHotkey": "Ctrl+Alt+Shift+PageUp" }
            """);

        Assert.Equal(HotkeyBinding.DefaultNext, settings.NextPageHotkey);
        Assert.Equal(HotkeyBinding.DefaultPrev, settings.PrevPageHotkey);
    }

    [Fact]
    public void Load_KeepsCustomHotkeys()
    {
        var settings = LoadFromJson("""
            { "nextPageHotkey": "Ctrl+Shift+F5", "prevPageHotkey": "Ctrl+Shift+F6" }
            """);

        Assert.Equal("Ctrl+Shift+F5", settings.NextPageHotkey);
        Assert.Equal("Ctrl+Shift+F6", settings.PrevPageHotkey);
    }

    [Fact]
    public void Load_MigratesOnlyTheLegacyOne()
    {
        var settings = LoadFromJson("""
            { "nextPageHotkey": "Alt+Q", "prevPageHotkey": "Ctrl+Alt+Shift+PageUp" }
            """);

        Assert.Equal("Alt+Q", settings.NextPageHotkey);
        Assert.Equal(HotkeyBinding.DefaultPrev, settings.PrevPageHotkey);
    }

    [Fact]
    public void Load_DoesNotMigrateWhenNewDefaultWouldCollideWithTheOtherKey()
    {
        var settings = LoadFromJson("""
            { "nextPageHotkey": "Ctrl+Alt+PageUp", "prevPageHotkey": "Ctrl+Alt+Shift+PageUp" }
            """);

        Assert.Equal("Ctrl+Alt+PageUp", settings.NextPageHotkey);
        Assert.Equal("Ctrl+Alt+Shift+PageUp", settings.PrevPageHotkey);
    }

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

    [Fact]
    public void Load_CorruptFile_ReturnsDefaults()
    {
        var settings = LoadFromJson("{ not json");

        Assert.Equal(HotkeyBinding.DefaultNext, settings.NextPageHotkey);
        Assert.Equal("", settings.NextPageJoy);
    }

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
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `dotnet test SimHubDS339.Tests --nologo`
Expected: ビルドエラー `CS1061: 'AppSettings' に 'Load' ... / 'NextPageJoy' の定義がありません` で FAIL。

- [ ] **Step 3: 実装**

`SimHubDS339/AppSettings.cs` を 4 か所変更する。

(a) フィールド（`_currentPage` の宣言の直後）に追加:

```csharp
        private string _nextPageJoy = "";
        private string _prevPageJoy = "";
```

(b) `PrevPageHotkey` プロパティの直後に追加:

```csharp
        /// <summary>次のページ切り替えに割り当てたコントローラーのボタン (例: "044F:B66F:B25"、空 = 未割当)</summary>
        [JsonPropertyName("nextPageJoy")]
        public string NextPageJoy
        {
            get => _nextPageJoy;
            set => _nextPageJoy = value?.Trim() ?? "";
        }

        /// <summary>前のページ切り替えに割り当てたコントローラーのボタン (例: "044F:B66F:B24"、空 = 未割当)</summary>
        [JsonPropertyName("prevPageJoy")]
        public string PrevPageJoy
        {
            get => _prevPageJoy;
            set => _prevPageJoy = value?.Trim() ?? "";
        }

        /// <summary>「次のページ」のコントローラー割り当てを取得する (未割当・不正な値は null)。</summary>
        public JoyBinding? GetNextPageJoyBinding() => JoyBinding.TryParse(_nextPageJoy, out var binding) ? binding : null;

        /// <summary>「前のページ」のコントローラー割り当てを取得する (未割当・不正な値は null)。</summary>
        public JoyBinding? GetPrevPageJoyBinding() => JoyBinding.TryParse(_prevPageJoy, out var binding) ? binding : null;
```

(c) `Load()` メソッド全体（XML コメント含む）を次に置き換える:

```csharp
        /// <summary>
        /// 設定ファイルから設定を読み込む。
        /// ファイルが存在しない場合や破損している場合は既定値のインスタンスを返す。
        /// </summary>
        public static AppSettings Load() => Load(SettingsFilePath);

        /// <summary>
        /// 指定したパスの設定ファイルから設定を読み込む (テスト用に保存先を指定できる)。
        /// 旧既定のホットキー (4 キー同時押し) は新しい既定値へ移行する。
        /// </summary>
        internal static AppSettings Load(string path)
        {
            if (!File.Exists(path))
            {
                return new AppSettings();
            }

            try
            {
                var json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
                settings.MigrateLegacyHotkeys();
                return settings;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Settings] 設定ファイルの読み込みに失敗しました（既定値を使用します）: {ex.Message}");
                return new AppSettings();
            }
        }

        /// <summary>
        /// 旧既定値 (Ctrl+Alt+Shift+PageDown / PageUp) と完全一致するホットキーだけを新既定値に置き換える。
        /// 新既定値が反対側のキーと衝突する場合は移行しない。
        /// </summary>
        private void MigrateLegacyHotkeys()
        {
            bool nextIsLegacy = string.Equals(_nextPageHotkey, HotkeyBinding.LegacyDefaultNext, StringComparison.OrdinalIgnoreCase);
            bool prevIsLegacy = string.Equals(_prevPageHotkey, HotkeyBinding.LegacyDefaultPrev, StringComparison.OrdinalIgnoreCase);

            if (nextIsLegacy && !string.Equals(_prevPageHotkey, HotkeyBinding.DefaultNext, StringComparison.OrdinalIgnoreCase))
            {
                _nextPageHotkey = HotkeyBinding.DefaultNext;
            }

            if (prevIsLegacy && !string.Equals(_nextPageHotkey, HotkeyBinding.DefaultPrev, StringComparison.OrdinalIgnoreCase))
            {
                _prevPageHotkey = HotkeyBinding.DefaultPrev;
            }
        }
```

(d) `Save()` メソッド全体（XML コメント含む）を次に置き換える:

```csharp
        /// <summary>
        /// 現在の設定を %APPDATA%\SimHubDS339\settings.json に JSON 形式で保存する。
        /// </summary>
        public void Save() => Save(SettingsFilePath);

        /// <summary>
        /// 現在の設定を指定したパスに JSON 形式で保存する (テスト用に保存先を指定できる)。
        /// </summary>
        internal void Save(string path)
        {
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(this, JsonOptions);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Settings] 設定ファイルの保存に失敗しました: {ex.Message}");
                throw;
            }
        }
```

`SettingsDirectory` フィールドが未使用になった場合は、`SettingsFilePath` の初期化に使われているので残す（削除しない）。

- [ ] **Step 4: テストを実行して成功を確認する**

Run: `dotnet test SimHubDS339.Tests --nologo`
Expected: PASS（`AppSettingsTests` を含む全テストが成功）

- [ ] **Step 5: コミット**

```bash
git add SimHubDS339/AppSettings.cs SimHubDS339.Tests/AppSettingsTests.cs
git commit -m "feat: add controller button settings and migrate legacy page hotkeys"
```

---

### Task 4: JoystickMonitor（winmm ポーリングと押下検出）

**Files:**
- Create: `SimHubDS339/JoystickMonitor.cs`
- Create: `SimHubDS339.Tests/JoystickMonitorTests.cs`

**Interfaces:**
- Consumes: `JoyInput`、`JoyInputKind`、`JoyBinding`（Task 2）
- Produces:
  - `sealed record JoyDeviceInfo(int Index, ushort Vid, ushort Pid, string Name, int ButtonCount, bool HasPov)`: `string DeviceKey`
  - `sealed class JoystickMonitor : IDisposable`:
    - `event Action<JoyBinding>? InputPressed`
    - `IReadOnlyList<JoyDeviceInfo> Devices`
    - `void Start()`、`void Dispose()`
    - `internal void Poll()`、`internal void Rescan()`
    - `internal static string LookupName(ushort vid, ushort pid)`
    - `internal static IReadOnlyList<JoyInput> DetectPresses(uint prevButtons, uint curButtons, uint prevPov, uint curPov)`
    - `internal static JoyInputKind? PovToKind(uint pov)`

- [ ] **Step 1: 失敗するテストを書く（押下検出は純粋関数）**

`SimHubDS339.Tests/JoystickMonitorTests.cs`:

```csharp
using Xunit;

namespace SimHubDS339.Tests;

public class JoystickMonitorTests
{
    private const uint Centered = 0xFFFF;

    private static uint Bit(int button) => 1u << (button - 1);

    [Fact]
    public void NoChange_ProducesNoPresses()
    {
        Assert.Empty(JoystickMonitor.DetectPresses(0, 0, Centered, Centered));
    }

    [Fact]
    public void ButtonGoingDown_IsPressed()
    {
        var presses = JoystickMonitor.DetectPresses(0, Bit(25), Centered, Centered);

        Assert.Equal(new[] { JoyInput.ForButton(25) }, presses);
    }

    [Fact]
    public void HeldButton_IsNotPressedAgain()
    {
        Assert.Empty(JoystickMonitor.DetectPresses(Bit(25), Bit(25), Centered, Centered));
    }

    [Fact]
    public void ReleasedButton_IsNotAPress()
    {
        Assert.Empty(JoystickMonitor.DetectPresses(Bit(25), 0, Centered, Centered));
    }

    [Fact]
    public void TwoButtonsAtOnce_AreReportedInAscendingOrder()
    {
        var presses = JoystickMonitor.DetectPresses(0, Bit(25) | Bit(1), Centered, Centered);

        Assert.Equal(new[] { JoyInput.ForButton(1), JoyInput.ForButton(25) }, presses);
    }

    [Fact]
    public void NewButtonWhileAnotherIsHeld_OnlyReportsTheNewOne()
    {
        var presses = JoystickMonitor.DetectPresses(Bit(24), Bit(24) | Bit(25), Centered, Centered);

        Assert.Equal(new[] { JoyInput.ForButton(25) }, presses);
    }

    [Theory]
    [InlineData(0u, JoyInputKind.PovUp)]
    [InlineData(9000u, JoyInputKind.PovRight)]
    [InlineData(18000u, JoyInputKind.PovDown)]
    [InlineData(27000u, JoyInputKind.PovLeft)]
    public void PovFromCentered_IsPressed(uint pov, JoyInputKind expected)
    {
        var presses = JoystickMonitor.DetectPresses(0, 0, Centered, pov);

        Assert.Equal(new[] { JoyInput.ForPov(expected) }, presses);
    }

    [Fact]
    public void PovHeldOrReleased_IsNotPressed()
    {
        Assert.Empty(JoystickMonitor.DetectPresses(0, 0, 0, 0));
        Assert.Empty(JoystickMonitor.DetectPresses(0, 0, 0, Centered));
    }

    [Fact]
    public void PovChangingDirection_IsPressedForTheNewDirection()
    {
        var presses = JoystickMonitor.DetectPresses(0, 0, 0, 9000);

        Assert.Equal(new[] { JoyInput.ForPov(JoyInputKind.PovRight) }, presses);
    }

    [Theory]
    [InlineData(4500u)]
    [InlineData(13500u)]
    public void PovDiagonals_AreIgnored(uint diagonal)
    {
        Assert.Empty(JoystickMonitor.DetectPresses(0, 0, Centered, diagonal));
    }

    [Theory]
    [InlineData(0xFFFFu)]
    [InlineData(0xFFFFFFFFu)]
    public void PovCentered_HasNoDirection(uint centered)
    {
        Assert.Null(JoystickMonitor.PovToKind(centered));
    }
}
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `dotnet test SimHubDS339.Tests --nologo`
Expected: ビルドエラー `CS0103/CS0246: 'JoystickMonitor' が見つかりません` で FAIL。

- [ ] **Step 3: 実装**

`SimHubDS339/JoystickMonitor.cs`:

```csharp
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
        private const int MaxDevices = 16;
        private const int PollIntervalMs = 20;
        private const int RescanIntervalMs = 2000;

        private const int JoyErrNoError = 0;
        private const uint JoyReturnAll = 0xFF;   // X, Y, Z, R, U, V, POV, ボタン
        private const uint JoyCapsHasPov = 0x10;
        private const uint NoPov = 0xFFFF;        // 十字キーが中立 (または十字キーなし)

        private const string OemRegistryPath =
            @"System\CurrentControlSet\Control\MediaProperties\PrivateProperties\Joystick\OEM\";

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

        [StructLayout(LayoutKind.Sequential)]
        private struct JOYINFOEX
        {
            public uint dwSize, dwFlags;
            public uint dwXpos, dwYpos, dwZpos, dwRpos, dwUpos, dwVpos;
            public uint dwButtons, dwButtonNumber, dwPOV;
            public uint dwReserved1, dwReserved2;
        }

        [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
        private static extern int joyGetDevCaps(IntPtr uJoyID, ref JOYCAPS pjc, int cbjc);

        [DllImport("winmm.dll")]
        private static extern int joyGetPosEx(uint uJoyID, ref JOYINFOEX pji);

        private readonly System.Windows.Forms.Timer _timer;
        private readonly Dictionary<int, (uint Buttons, uint Pov)> _states = new();
        private List<JoyDeviceInfo> _devices = new();
        private bool _scanned;
        private long _lastScanTick;

        /// <summary>ボタン/十字キーが押された瞬間のイベント (UI スレッド上で発生)。</summary>
        public event Action<JoyBinding>? InputPressed;

        /// <summary>現在接続中のコントローラー一覧 (定期的に更新される)。</summary>
        public IReadOnlyList<JoyDeviceInfo> Devices => _devices;

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

        private static JOYINFOEX NewInfo() => new()
        {
            dwSize = (uint)Marshal.SizeOf<JOYINFOEX>(),
            dwFlags = JoyReturnAll,
        };

        public void Dispose()
        {
            _timer.Stop();
            _timer.Dispose();
        }
    }
}
```

- [ ] **Step 4: テストを実行して成功を確認する**

Run: `dotnet test SimHubDS339.Tests --nologo`
Expected: PASS（`JoystickMonitorTests` を含む全テストが成功）

- [ ] **Step 5: 実機でデバイス列挙を確認する（T300RS 接続状態）**

`Rescan` / `LookupName` は winmm とレジストリに依存するため自動テストしない。テストプロジェクトに一時テストを足さず、次の使い捨てスクリプトで確認する（スクリプトはコミットしない）。

Run（PowerShell、`D:\PG\DS339` で実行）:

```powershell
dotnet build SimHubDS339 -c Debug --nologo -v q
$asm = [Reflection.Assembly]::LoadFrom("$PWD\SimHubDS339\bin\Debug\net8.0-windows\SimHubDS339.dll")
$type = $asm.GetType('SimHubDS339.JoystickMonitor')
$monitor = [Activator]::CreateInstance($type)
$type.GetMethod('Rescan', [Reflection.BindingFlags]'NonPublic,Instance').Invoke($monitor, $null)
$type.GetProperty('Devices').GetValue($monitor) | Format-List
```

Expected: `Index=3`、`Vid=1103 (0x044F)`、`Pid=46703 (0xB66F)`、`Name=Ferrari F1 Wheel Advanced T300`、`ButtonCount=25`、`HasPov=True` の 1 件（Index 0 の Xbox 360 系コントローラも一緒に出る）。T300RS が出ない場合は、電源と USB を確認してもらう。`SimHubDS339` は WinForms の `Timer` を生成するため、スクリプトで `Application` のメッセージループがなくてもコンストラクタは動く。エラーになる場合はそのエラーを報告して止まる（推測で先に進まない）。

- [ ] **Step 6: コミット**

```bash
git add SimHubDS339/JoystickMonitor.cs SimHubDS339.Tests/JoystickMonitorTests.cs
git commit -m "feat: add JoystickMonitor polling winmm for controller button presses"
```

---

### Task 5: TrayApp へ組み込み、T300RS の初期値を設定する

**Files:**
- Modify: `SimHubDS339/TrayApp.cs:40`（フィールド）、`TrayApp.cs:51-54`（コンストラクタ）、`TrayApp.cs:127-128`（ホットキー登録の直後）、`TrayApp.cs:334`（終了処理）、`TrayApp.cs:143` の手前（メソッド追加）
- Modify（ユーザーの設定ファイル）: `%APPDATA%\SimHubDS339\settings.json`

**Interfaces:**
- Consumes: `JoystickMonitor`（Task 4）、`AppSettings.GetNextPageJoyBinding/GetPrevPageJoyBinding`（Task 3）、`JoyBinding.Equals`（Task 2）
- Produces: `TrayApp` が private メソッド `ReloadJoystickBindings()` を持つ（Task 6 で設定画面から呼ぶ）。

- [ ] **Step 1: フィールドを追加する**

`private readonly HotkeyManager _hotkeyManager;` の直後に追加:

```csharp
        private readonly JoystickMonitor _joystick;
        private JoyBinding? _nextPageJoy;
        private JoyBinding? _prevPageJoy;
```

- [ ] **Step 2: コンストラクタで生成する**

`_hotkeyManager.PrevPageTriggered += () => _service.PreviousPage();` の直後に追加:

```csharp

            // コントローラー (ホイール等) のボタン監視
            _joystick = new JoystickMonitor();
            _joystick.InputPressed += OnJoystickInput;
            ReloadJoystickBindings();
```

`RegisterHotkeysFromSettings();`（コンストラクタ内の呼び出し）の直後に追加:

```csharp

            // コントローラーの監視開始
            _joystick.Start();
```

- [ ] **Step 3: メソッドを追加する**

`/// 設定からホットキーを読み込み、グローバルホットキーとして登録する。` の `<summary>` ブロックの直前（`RegisterHotkeysFromSettings` の手前）に追加:

```csharp
        /// <summary>
        /// 設定からコントローラーのボタン割り当てを読み込む (未割当・不正な値は null)。
        /// </summary>
        private void ReloadJoystickBindings()
        {
            _nextPageJoy = _settings.GetNextPageJoyBinding();
            _prevPageJoy = _settings.GetPrevPageJoyBinding();
        }

        /// <summary>
        /// コントローラーのボタンが押されたとき、割り当て済みならページを切り替える。
        /// </summary>
        private void OnJoystickInput(JoyBinding pressed)
        {
            if (_nextPageJoy != null && _nextPageJoy.Equals(pressed))
            {
                _service.NextPage();
            }
            else if (_prevPageJoy != null && _prevPageJoy.Equals(pressed))
            {
                _service.PreviousPage();
            }
        }

```

- [ ] **Step 4: 終了処理で破棄する**

`ExitApplication` 内の `_hotkeyManager.Dispose();` の直後に追加:

```csharp

            // コントローラー監視の停止
            _joystick.Dispose();
```

- [ ] **Step 5: ビルドとテスト**

Run: `dotnet build SimHubDS339 -c Debug --nologo -v q && dotnet test SimHubDS339.Tests --nologo`
Expected: ビルド成功（警告は既存以外に増えていないこと）、全テスト PASS。

- [ ] **Step 6: ユーザーの設定ファイルに T300RS の初期値を書き込む**

先にユーザーに「トレイの SimHubDS339 を右クリック →『終了』で終了してほしい」と依頼し、終了を確認してから実行する（アプリを強制終了しない。起動中だと終了時ではなく OK 押下時にしか保存されないが、念のため避ける）。

Run（PowerShell）:

```powershell
$path = Join-Path $env:APPDATA 'SimHubDS339\settings.json'
Copy-Item $path "$path.bak"
$json = Get-Content $path -Raw -Encoding UTF8 | ConvertFrom-Json
$json | Add-Member -NotePropertyName nextPageJoy -NotePropertyValue '044F:B66F:B25' -Force
$json | Add-Member -NotePropertyName prevPageJoy -NotePropertyValue '044F:B66F:B24' -Force
$json | ConvertTo-Json -Depth 5 | Set-Content $path -Encoding UTF8
Get-Content $path
```

Expected: 既存のキー（`fps`、`simHubHost`、`simHubPort`、`nextPageHotkey`、`prevPageHotkey`、`enabledPages`、`currentPage`）がそのまま残り、`nextPageJoy` が `044F:B66F:B25`、`prevPageJoy` が `044F:B66F:B24` で追加されている。バックアップは `settings.json.bak`。

- [ ] **Step 7: 実機で確認する（T300RS と DS339 を接続、`--demo` で起動）**

Run: `Start-Process "D:\PG\DS339\SimHubDS339\bin\Debug\net8.0-windows\SimHubDS339.exe" -ArgumentList '--demo'`

ユーザーに次を確認してもらう（ゲーム不要、擬似データでレース画面が出る）:
1. Button 25 を押す → DS339 のページが 1 つ進む（MAIN → TYRES → …）。
2. Button 24 を押す → 1 つ戻る。
3. `Ctrl+Alt+PageDown` / `Ctrl+Alt+PageUp` でも切り替わる（旧既定から移行されている）。
4. 他のボタンを押しても何も起きない。

確認後、トレイの「終了」で止めてもらう。動かない場合は、トレイの「ログを開く」でログを確認し、原因を報告して止まる。

- [ ] **Step 8: コミット**

```bash
git add SimHubDS339/TrayApp.cs
git commit -m "feat: switch dashboard pages from controller buttons"
```

（`settings.json` はリポジトリ外なのでコミット対象ではない。）

---

### Task 6: 設定画面（コントローラーとボタンの選択 UI）

**Files:**
- Create: `SimHubDS339/JoyBindingEditor.cs`
- Modify: `SimHubDS339/SettingsForm.cs`（フィールド、コンストラクタ、ページ設定グループのレイアウト、キー入力チェック、`OnOkClicked`）
- Modify: `SimHubDS339/TrayApp.cs`（`ShowSettings`、`OnJoystickInput`）

**Interfaces:**
- Consumes: `JoystickMonitor.Devices/Rescan/InputPressed/LookupName`（Task 4）、`JoyBinding`/`JoyInput`（Task 2）、`AppSettings.NextPageJoy/PrevPageJoy/GetNextPageJoyBinding/GetPrevPageJoyBinding`（Task 3）、`HotkeyBinding.IsSingleKeyAllowed`（Task 1）、`TrayApp.ReloadJoystickBindings`（Task 5）
- Produces:
  - `sealed class JoyBindingEditor : UserControl`: コンストラクタ `JoyBindingEditor(JoystickMonitor monitor)`、`JoyBinding? Value { get; set; }`、`bool IsCapturing { get; }`
  - `SettingsForm` のコンストラクタ `SettingsForm(DashboardService service, AppSettings settings, JoystickMonitor joystick, Func<HotkeyBinding, HotkeyBinding, string?>? onRegisterHotkeys = null, Action? onSettingsApplied = null)`、`internal bool IsCapturingJoystick`

- [ ] **Step 1: JoyBindingEditor を作る**

`SimHubDS339/JoyBindingEditor.cs`:

```csharp
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
```

- [ ] **Step 2: SettingsForm のフィールドとコンストラクタ引数を変更する**

`private readonly Func<HotkeyBinding, HotkeyBinding, string?>? _onRegisterHotkeys;` の直後に追加:

```csharp
        private readonly JoystickMonitor _joystick;
        private readonly Action? _onSettingsApplied;
```

`private readonly Button _btnResetHotkeys;` の直後に追加:

```csharp
        private readonly JoyBindingEditor _joyNext;
        private readonly JoyBindingEditor _joyPrev;
```

コンストラクタのシグネチャと先頭の代入（元の 47〜51 行目）を次に置き換える:

```csharp
        public SettingsForm(
            DashboardService service,
            AppSettings settings,
            JoystickMonitor joystick,
            Func<HotkeyBinding, HotkeyBinding, string?>? onRegisterHotkeys = null,
            Action? onSettingsApplied = null)
        {
            _service = service;
            _settings = settings;
            _joystick = joystick;
            _onRegisterHotkeys = onRegisterHotkeys;
            _onSettingsApplied = onSettingsApplied;
```

`ClientSize = new Size(500, 600);` を `ClientSize = new Size(500, 646);` に変更する。

クラス内（`SetupHotkeyTextBox` の手前など）に追加:

```csharp
        /// <summary>
        /// コントローラーのボタン検出の待機中かどうか (待機中はそのボタンでページを切り替えない)。
        /// </summary>
        internal bool IsCapturingJoystick => _joyNext.IsCapturing || _joyPrev.IsCapturing;
```

- [ ] **Step 3: ページ設定グループのレイアウトを置き換える**

元の `var grpPages = new GroupBox` の `Size = new Size(468, 192),` を `Size = new Size(468, 244),` に変更する。

元の 266〜340 行目（`var lblNextHotkey = new Label` から `grpPages.Controls.AddRange(new Control[] { ... });` の閉じまで）を、次に置き換える:

```csharp
            var lblNextHotkey = new Label
            {
                Text = "次 (キー):",
                Location = new Point(16, 61),
                AutoSize = true,
            };

            _txtNextHotkey = new TextBox
            {
                Location = new Point(100, 58),
                Size = new Size(230, 23),
            };

            var lblNextJoy = new Label
            {
                Text = "次 (ボタン):",
                Location = new Point(16, 91),
                AutoSize = true,
            };

            _joyNext = new JoyBindingEditor(_joystick)
            {
                Location = new Point(100, 86),
            };
            _joyNext.Value = _settings.GetNextPageJoyBinding();

            var lblPrevHotkey = new Label
            {
                Text = "前 (キー):",
                Location = new Point(16, 125),
                AutoSize = true,
            };

            _txtPrevHotkey = new TextBox
            {
                Location = new Point(100, 122),
                Size = new Size(230, 23),
            };

            var lblPrevJoy = new Label
            {
                Text = "前 (ボタン):",
                Location = new Point(16, 155),
                AutoSize = true,
            };

            _joyPrev = new JoyBindingEditor(_joystick)
            {
                Location = new Point(100, 150),
            };
            _joyPrev.Value = _settings.GetPrevPageJoyBinding();

            // ホットキー初期値設定とキャプチャ設定
            if (!HotkeyBinding.TryParse(_settings.NextPageHotkey, out var initialNext))
            {
                HotkeyBinding.TryParse(HotkeyBinding.DefaultNext, out initialNext);
            }
            if (!HotkeyBinding.TryParse(_settings.PrevPageHotkey, out var initialPrev))
            {
                HotkeyBinding.TryParse(HotkeyBinding.DefaultPrev, out initialPrev);
            }

            SetupHotkeyTextBox(_txtNextHotkey, initialNext!);
            SetupHotkeyTextBox(_txtPrevHotkey, initialPrev!);

            _btnResetHotkeys = new Button
            {
                Text = "キーを既定値に戻す",
                Location = new Point(344, 57),
                Size = new Size(110, 25),
            };
            _btnResetHotkeys.Click += (_, _) =>
            {
                if (HotkeyBinding.TryParse(HotkeyBinding.DefaultNext, out var defNext))
                {
                    _txtNextHotkey.Text = defNext.ToString();
                    _txtNextHotkey.Tag = defNext;
                }
                if (HotkeyBinding.TryParse(HotkeyBinding.DefaultPrev, out var defPrev))
                {
                    _txtPrevHotkey.Text = defPrev.ToString();
                    _txtPrevHotkey.Tag = defPrev;
                }
            };

            var lblHotkeyNote = new Label
            {
                Text = "※ キー: 入力欄を選んでキーを押すと入力されます (Ctrl / Alt / Shift のいずれかが必須。F13〜F24 は単独でも可)。\n※ ボタン: 「検出」を押してからコントローラーのボタンを押すと自動入力されます。",
                Location = new Point(16, 184),
                Size = new Size(436, 50),
                ForeColor = SystemColors.GrayText,
            };

            grpPages.Controls.AddRange(new Control[]
            {
                _chkMain, _chkTyres, _chkFuel, _chkDelta, _chkSession,
                lblNextHotkey, _txtNextHotkey,
                lblNextJoy, _joyNext,
                lblPrevHotkey, _txtPrevHotkey,
                lblPrevJoy, _joyPrev,
                _btnResetHotkeys,
                lblHotkeyNote
            });
```

下部のコントロールを下へずらす。`_lblPrivilege` の `Location = new Point(16, 526),` → `Location = new Point(16, 576),`、`_btnOk` の `Location = new Point(286, 554),` → `Location = new Point(286, 602),`、`_btnCancel` の `Location = new Point(392, 554),` → `Location = new Point(392, 602),` に変更する。

- [ ] **Step 4: キー入力チェックを F13〜F24 単独に対応させる**

`SetupHotkeyTextBox` 内の次のブロック（元の 397〜402 行目）:

```csharp
                // 修飾キーがない場合は拒否
                if ((e.Modifiers & (Keys.Control | Keys.Alt | Keys.Shift)) == Keys.None)
                {
                    MessageBox.Show(this, "ホットキーには Ctrl、Alt、Shift のいずれかの修飾キーを含める必要があります (ゲーム操作の競合を防ぐため)。", "ホットキー設定エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
```

を次に置き換える:

```csharp
                // 修飾キーがない場合は、F13〜F24 の単独入力だけ許可する
                bool hasModifier = (e.Modifiers & (Keys.Control | Keys.Alt | Keys.Shift)) != Keys.None;
                if (!hasModifier && !HotkeyBinding.IsSingleKeyAllowed(e.KeyCode))
                {
                    MessageBox.Show(this, "ホットキーには Ctrl、Alt、Shift のいずれかの修飾キーを含める必要があります (F13〜F24 は単独で使えます。ゲーム操作との競合を防ぐため)。", "ホットキー設定エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
```

- [ ] **Step 5: OnOkClicked を更新する**

「次のページ」と「前のページ」の同一チェック（`if (nextBinding.Equals(prevBinding)) { ... return; }`）の直後、`// ホットキー変更時の再登録` の直前に追加:

```csharp

            // コントローラーのボタンの検証
            var nextJoy = _joyNext.Value;
            var prevJoy = _joyPrev.Value;
            if (nextJoy != null && prevJoy != null && nextJoy.Equals(prevJoy))
            {
                MessageBox.Show(this, "「次のページ」と「前のページ」に同じボタンは設定できません。", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
```

`_settings.PrevPageHotkey = prevBinding.ToString();` の直後に追加:

```csharp
            _settings.NextPageJoy = nextJoy?.ToString() ?? "";
            _settings.PrevPageJoy = prevJoy?.ToString() ?? "";
```

`_settings.Save();` の `try { ... }` ブロックの直後（保存失敗時の `return;` の後）、`if (needRestart)` の直前に追加:

```csharp

            _onSettingsApplied?.Invoke();
```

- [ ] **Step 6: TrayApp 側を合わせる**

`ShowSettings` 内の `_settingsForm = new SettingsForm(_service, _settings, OnRegisterHotkeys);` を次に置き換える:

```csharp
            _settingsForm = new SettingsForm(_service, _settings, _joystick, OnRegisterHotkeys, ReloadJoystickBindings);
```

`OnJoystickInput` の先頭（`if (_nextPageJoy != null ...` の前）に追加:

```csharp
            // 設定画面で「検出」の待機中は、そのボタンでページを切り替えない
            if (_settingsForm is { IsDisposed: false, IsCapturingJoystick: true })
            {
                return;
            }

```

- [ ] **Step 7: ビルドとテスト**

Run: `dotnet build SimHubDS339 -c Debug --nologo -v q && dotnet test SimHubDS339.Tests --nologo`
Expected: ビルド成功、全テスト PASS。コンパイルエラーが出たら、エラー行の型名・メソッド名を Interfaces の記載と照合して直す（機能は変えない）。

- [ ] **Step 8: 設定画面を実機で確認する（T300RS 接続、`--demo` で起動）**

Run: `Start-Process "D:\PG\DS339\SimHubDS339\bin\Debug\net8.0-windows\SimHubDS339.exe" -ArgumentList '--demo'`

ユーザーに次を確認してもらう（トレイアイコンをダブルクリックして設定を開く）:
1. 「次 (ボタン)」に「Ferrari F1 Wheel Advanced T300」/「Button 25」、「前 (ボタン)」に同/「Button 24」が表示されている（Task 5 で書いた初期値）。
2. 「次 (ボタン)」の「検出」を押し、T300RS の別のボタンを押す → コントローラーとボタンが自動で入力される。待機中にそのボタンを押してもページは切り替わらない。
3. 「検出」を押して 10 秒放置 → 待機が終わり、「検出」に戻る。もう一度「検出」を押すと途中で中止できる。
4. コンボボックスから手動でコントローラーとボタン（POV 含む）を選べる。
5. 「解除」で未割当に戻る。
6. 次と前に同じボタンを設定して OK → 「同じボタンは設定できません」と出て閉じない。
7. キー入力欄で `F13` を単独で押すと入力できる。`F12` 単独は警告が出る。
8. 検出で元の Button 25 / Button 24 に戻して OK → 保存され、Button 25/24 でページが切り替わる。

確認後、トレイの「終了」で止めてもらう。

- [ ] **Step 9: コミット**

```bash
git add SimHubDS339/JoyBindingEditor.cs SimHubDS339/SettingsForm.cs SimHubDS339/TrayApp.cs
git commit -m "feat: add controller and button pickers to the settings window"
```

---

### Task 7: ドキュメント更新と最終確認

**Files:**
- Modify: `README.md:142-155`（ページの切り替え）、`README.md:185`（設定項目）、`README.md:204-207` 付近（更新履歴）
- Modify: `SimHubDS339/README.md:61`、`SimHubDS339/README.md:98-109`

- [ ] **Step 1: ルート README の「ページの切り替え」を更新する**

`README.md` の 142〜155 行目（`| 操作 | 方法 |` の表から `**ハンドルのボタンで切り替える**: ...` の段落まで）を次に置き換える:

```markdown
| 操作 | 方法 |
|---|---|
| 次のページ | `Ctrl + Alt + PageDown` (既定)、または設定で割り当てたコントローラーのボタン |
| 前のページ | `Ctrl + Alt + PageUp` (既定)、または設定で割り当てたコントローラーのボタン |
| マウスで切り替え | タスクトレイのアイコンを右クリック →「次のページ」/「前のページ」 |

- 既定のホットキーは修飾キー 2 つと PageDown / PageUp の組み合わせです。ゲームの操作と被りにくく、ゲームを操作中でもそのまま効きます
- 切り替えると、画面上部に 1.5 秒間ページ名が表示されます
- 最後に表示していたページは記憶され、次回起動時もそのページから始まります
- ホットキーは設定ウィンドウの「レース画面のページ設定」で変更できます。入力欄を選んで割り当てたいキーを押すと入力されます。修飾キー (Ctrl / Alt / Shift) を 1 つ以上含む組み合わせが必要です (F13〜F24 だけは単独で割り当てられます)
- 使わないページは同じ設定でチェックを外すと、切り替え時に飛ばされます (MAIN は外せません)
- 他のアプリが同じキーを使っていて登録できない場合は、トレイに通知が出ます。設定で別のキーに変更してください

**ハンドルのボタンで切り替える**: 設定ウィンドウの「レース画面のページ設定」で、「次 (ボタン)」「前 (ボタン)」に割り当てるコントローラーとボタンを選びます。「検出」を押してからボタンを押すと、コントローラーとボタンが自動で入力されます。ゲーム内の操作 (例: MFD のページ送り) と同じボタンを割り当てると、ゲームと DS339 が同時に切り替わります。Windows に「ゲーム コントローラー」として認識される機器 (ボタンは 32 個まで、十字キーは上下左右) が対象で、キーボード入力への変換ツールは不要です。キーとボタンは、どちらか一方だけでも両方でも設定できます。
```

- [ ] **Step 2: ルート README の設定項目と更新履歴を更新する**

`README.md:185` の `- レース画面のページ設定 (各ページの有効/無効化、切替ホットキーの変更)` を次に置き換える:

```markdown
- レース画面のページ設定 (各ページの有効/無効化、切替ホットキーとコントローラーのボタンの割り当て)
```

更新履歴の `- **2026-09-20**` 配下の先頭に追加:

```markdown
  - ページ切り替えを簡単にしました。既定のホットキーを `Ctrl+Alt+PageDown` / `PageUp` に短縮し (旧既定の設定は自動で移行)、F13〜F24 の単独キーも設定できます
  - ホイールやボタンボックスのボタンでページを切り替えられるようにしました。設定ウィンドウで、コントローラーとボタンを選ぶか「検出」で自動入力できます
```

- [ ] **Step 3: SimHubDS339/README.md を更新する**

61 行目の `- **レース画面のページ設定**: ...` を次に置き換える:

```markdown
- **レース画面のページ設定**: 各ページの ON/OFF (MAIN は常に有効) と、次/前ページ切り替え用のグローバルホットキー、およびコントローラー (ホイール等) のボタンの割り当て
```

98〜109 行目（`### ページ切り替えとホットキー` の節から `> [!TIP]` のブロックまで）を次に置き換える:

```markdown
### ページ切り替えとホットキー

- **グローバルホットキー**:
  - 次のページ: `Ctrl + Alt + PageDown` (既定)
  - 前のページ: `Ctrl + Alt + PageUp` (既定)
  - 修飾キーなしで割り当てられるのは `F13`〜`F24` のみです。旧既定 (`Ctrl+Alt+Shift+PageDown` / `PageUp`) の設定は、起動時に新しい既定へ自動で移行されます。
- **コントローラーのボタン**: 設定ウィンドウで、次/前ページそれぞれにコントローラーとボタン (または十字キーの上下左右) を割り当てられます。winmm (`joyGetPosEx`) で 20ms ごとに読み取るだけなので、ゲームの入力を奪わず、ゲームのウィンドウがアクティブでも動作します。コントローラーは VID/PID で識別し、ボタンは 32 個までが対象です。
- **タスクトレイメニュー**: トレイアイコンを右クリックして「次のページ」「前のページ」をクリックすることでも切り替えられます。
- ページ切り替え直後 1.5 秒間は、画面中央上部にページ名 (例: `TYRES`) が表示されます。
- 設定ウィンドウの「レース画面のページ設定」から、各ページの ON/OFF 切り替え (不要なページをスキップ) や、ホットキー・ボタンの変更が可能です。

> [!TIP]
> **ハンコンのボタンで切り替える方法**:
> 「次 (ボタン)」「前 (ボタン)」の「検出」を押してから、割り当てたいボタンを押すだけです。ゲーム内の操作 (MFD のページ送りなど) と同じボタンを割り当てると、ゲームと DS339 が同時に切り替わります。SimHub の「Controls and events」や JoyToKey などは不要です。
```

- [ ] **Step 4: 設定ウィンドウのスクリーンショットについて**

`docs/images/settings.png` は古い画面のまま。撮り直しはユーザーに依頼する（撮り直すまでは古いまま残ることを最終報告に書く）。この Task では画像を変更しない。

- [ ] **Step 5: 最終確認（実機）**

`dotnet build SimHubDS339 -c Release --nologo -v q && dotnet test SimHubDS339.Tests --nologo` を実行し、成功を確認する。

ユーザーに次を確認してもらう:
1. Release ビルド（`SimHubDS339\bin\Release\net8.0-windows\SimHubDS339.exe`）を通常起動する（`--demo` なし）。
2. LMU をフルスクリーンで起動し、走行中に Button 25 / Button 24 で DS339 のページが切り替わる。ゲーム内のボタン割り当ては変更しない。
3. ゲーム側の MFD ページ送りと同じボタンにした場合、ゲームと DS339 が同時に切り替わっても問題ないか。
4. T300RS を抜き差ししても、再接続後に Button 25 / 24 が再び効く。

動かない場合は、フォーカスの有無・ゲームの入力排他（一部ゲームはデバイスを排他取得する）を疑い、ログとあわせて原因を報告する。ゲーム中に効かないことが分かった場合は、Step 1〜3 で書いた「ゲームのウィンドウがアクティブでも動作します」の記述を実態に合わせて直してから Step 7 に進む。

- [ ] **Step 6: 後始末**

ユーザーが動作を確認できたら、`%APPDATA%\SimHubDS339\settings.json.bak`（Task 5 のバックアップ）を削除してよいか聞く。削除は確認を取ってから行う。

- [ ] **Step 7: コミット**

```bash
git add README.md SimHubDS339/README.md
git commit -m "docs: describe controller button page switching and new default hotkeys"
```
