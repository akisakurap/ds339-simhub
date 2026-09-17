# InfoPanel.SimHubBridge

SimHub のテレメトリを DS339 (InfoPanel 経由) に表示するためのブリッジプラグインです。

```
SimHub → [SimHubPropertyServer plugin, TCP:18082] → [このプラグイン] → InfoPanel → DS339
```

## 1. SimHub 側の準備

1. https://github.com/pre-martin/SimHubPropertyServer の Releases から `PropertyServer.dll` をダウンロード
   (緑の "Code" ボタンではなく、右側の "Releases" から取得すること)
2. `PropertyServer.dll` を SimHub のインストールフォルダ直下 (`SimHubWPF.exe` と同じ場所) にコピー
3. SimHub を起動 → 初回起動時にプラグイン実行の確認ダイアログが出るので許可
4. `Settings > Plugins` で "Property Server" が有効になっていることを確認
   (SimHub バージョン 9.6.0 以上が必要)

デフォルトではポート `18082` で待ち受けます。変更した場合は `Plugin.cs` の
`new SimHubPropertyClient("127.0.0.1", 18082, ...)` の第2引数も合わせて変更してください。

## 2. このプラグインのビルド

InfoPanel 本体 (habibrehmansg/infopanel) の `InfoPanel.Plugins` への参照が必要です。
このリポジトリ (`InfoPanel.SimHubBridge`) の**親フォルダ** (例: `D:\PG`) で clone してください。

```bash
git clone https://github.com/habibrehmansg/infopanel.git
```

`InfoPanel.SimHubBridge.csproj` は `..\..\infopanel\InfoPanel.Plugins\InfoPanel.Plugins.csproj`
(= `InfoPanel.SimHubBridge` の親の親フォルダに `infopanel` がある想定) を `ProjectReference`
で参照する設定済みです。clone 先のパスが異なる場合はここを実際のパスに書き換えてください。

```bash
dotnet build -c Release
```

`Plugin.cs` は `InfoPanel.Plugins` (habibrehmansg/infopanel) の実ソース
(`BasePlugin` / `PluginSensor` / `PluginText` / `IPluginContainer`) を確認した上で
実装済みです。`Load(List<IPluginContainer> containers)` のようにシグネチャが
`IPluginContainer` 単体でなく `List<IPluginContainer>` を受け取る点、`Id`/`Name` が
コンストラクタ経由でしか設定できない点などを反映しています。
`SimHubPropertyClient.cs` (データ取得ロジック本体) は InfoPanel 側 API の変更の影響を
受けないので、そのまま使い回せます。

## 3. InfoPanel への導入

1. ビルド後の `InfoPanel.SimHubBridge.dll` (と依存 DLL) を InfoPanel の
   Plugins フォルダにコピー、または InfoPanel の「Import Plugin」機能で読み込む
2. InfoPanel を再起動 (または Runtime Plugin Management 対応バージョンならそのまま反映)
3. デザイン画面で「SimHub Bridge」のセンサー/テキスト項目 (Speed, RPM, Gear, Throttle,
   Brake, Fuel, Lap, Track, Car など) が一覧に出るので、DS339 (960×376) のレイアウトに
   ドラッグ&ドロップで配置する

## 4. 公開しているプロパティ

| InfoPanel 項目 | SimHub プロパティ | 型 |
|---|---|---|
| Speed (km/h) | dcp.gd.SpeedKmh | double |
| RPM | dcp.gd.Rpms | double |
| Max RPM | dcp.gd.MaxRpm | double |
| Gear | dcp.gd.Gear | string |
| Throttle (%) | dcp.gd.Throttle | double (0-1 → ×100) |
| Brake (%) | dcp.gd.Brake | double (0-1 → ×100) |
| Clutch (%) | dcp.gd.Clutch | double (0-1 → ×100) |
| Car | dcp.gd.CarModel | string |
| Track | dcp.gd.TrackName | string |
| Lap | dcp.gd.CurrentLap / TotalLaps | string 合成 |
| Current Lap Time | dcp.gd.CurrentLapTime | string |
| Best Lap Time | dcp.gd.BestLapTime | string |
| Fuel (%) | dcp.gd.FuelPercent | double |
| SimHub Connection | (TCP接続状態) | string |

他のプロパティを追加したい場合は `Plugin.cs` の `SubscribedProperties` 配列と
`Update()` メソッドに項目を追加するだけで拡張できます。利用可能な全プロパティ名は
SimHub Property Server に `telnet localhost 18082` で接続後 `help` コマンドを
送ると一覧取得できます。

## 5. 制限事項 (SimHub Property Server 側の仕様)

- 更新レートは 10Hz 固定 (SimHub Property Server の仕様)。DS339 は 60Hz 対応ですが、
  値の更新自体はこのレートが上限になります
- 配列・複合型のプロパティは取得不可 (シンプルな数値・文字列・真偽値のみ)
