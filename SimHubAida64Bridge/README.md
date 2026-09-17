# SimHubAida64Bridge

SimHub のテレメトリを AIDA64 経由で DS339 (JONSBO 製サブディスプレイ) に表示するための常駐ツールです。

```
SimHub → [SimHubPropertyServer plugin, TCP:18082] → [このツール] → AIDA64 (Registry Import Values) → DS339
```

## 背景 (なぜ InfoPanel ではなく AIDA64 経由なのか)

DS339 は MacroSilicon MS9132 (USB VID `345F` / PID `9132`) チップを搭載しており、
Windows 上では `libusb0` ドライバによる独自プロトコルの USB デバイス (`MSDisplay` クラス)
として認識されます。これは InfoPanel が対応している BeadaPanel (`4E58:1001`) や
Turing/TURZX 系パネルとは別チップのため、**InfoPanel では現状 DS339 に描画できません**。

一方 AIDA64 は DS339 を公式にサポートしています (v8.25.8228 以降)。そこで、SimHub の
値を AIDA64 の「External Applications (Registry Import Values)」機構経由で渡す方式に
しています。DS339 への実際の描画は AIDA64 側に任せます。

## 1. SimHub 側の準備

1. https://github.com/pre-martin/SimHubPropertyServer の Releases から `PropertyServer.dll` を取得
2. SimHub インストールフォルダ (`SimHubWPF.exe` と同じ場所) に配置
3. SimHub を起動し、初回確認ダイアログを許可
4. `Settings > Plugins` で "Property Server" が有効になっていることを確認 (デフォルトポート `18082`)

## 2. AIDA64 側の準備

1. AIDA64 で DS339 を LCD/SensorPanel の出力先として設定する (JONSBO / AIDA64 のガイドに従う)
2. `Preferences > Hardware Monitoring > External Applications` で、このツールが書き込む
   レジストリ値を AIDA64 に認識させる (AIDA64 側は自動検出、明示設定が要る場合は
   マニュアル参照: https://www.aida64.com/user-manual/hardware-monitoring/external-applications)
3. SensorPanel / LCD レイアウト編集画面で、以下の External Application 項目を
   ドラッグ&ドロップで配置する

## 3. このツールのビルドと実行

```bash
dotnet build -c Release
```

```bash
D:\PG\DS339\SimHubAida64Bridge\bin\Release\net8.0-windows\SimHubAida64Bridge.exe
```

起動しっぱなしにしておく常駐ツールです。SimHub → AIDA64 双方が起動している状態で
動かしてください。Windows のスタートアップに登録すれば自動起動できます。

## 4. 書き込んでいるレジストリ値

書き込み先: `HKEY_CURRENT_USER\Software\FinalWire\AIDA64\ImportValues`

| レジストリ値 | 種別 | 内容 | 備考 |
|---|---|---|---|
| DW1 | DWORD | Speed (km/h) | 整数丸め |
| DW2 | DWORD | RPM | 整数丸め |
| DW3 | DWORD | Throttle (%) | 0-100 |
| DW4 | DWORD | Brake (%) | 0-100 |
| DW5 | DWORD | Clutch (%) | 0-100 |
| DW6 | DWORD | Fuel (%) | 0-100 |
| Str1 | 文字列 | Gear | |
| Str2 | 文字列 | Car | |
| Str3 | 文字列 | Track | |
| Str4 | 文字列 | Lap (現在/合計) | |
| Str5 | 文字列 | Current Lap Time | |
| Str6 | 文字列 | Best Lap Time | |
| Str7 | 文字列 | SimHub 接続状態 | Connected / Disconnected |

他のプロパティを追加したい場合は `Program.cs` の `SubscribedProperties` と
`PushValues()` に項目を追加してください (Str8-10, DW7-10 が空いています)。

## 5. 制限事項

- 更新レートは SimHub Property Server の仕様で 10Hz 固定
- AIDA64 側のレジストリ読み取り間隔は AIDA64 の Update Rate 設定に依存する
- 配列・複合型のプロパティは取得不可 (シンプルな数値・文字列・真偽値のみ)
