# ds339-simhub

JONSBO 製 3.39 インチサブディスプレイ **DS339** に、SimHub のテレメトリ (速度・ギア・RPM・ラップタイムなど) を **USB で直接** 表示するためのツール群です。

DS339 は MacroSilicon MS912C (USB VID `345F` / PID `9132`) を搭載した独自プロトコルのデバイスで、InfoPanel は非対応、AIDA64 は有償です。そこで純正アプリ JONSBO-AIO の USB 通信をキャプチャしてプロトコルを解析し、自前で描画・転送できるようにしました。

> [!WARNING]
> 非公式ツールです。**使用は自己責任** でお願いします。詳しくは [免責事項](#免責事項) を参照してください。

![レース画面](docs/images/race_normal.png)

```
SimHub → [Property Server plugin, TCP:18082] → SimHubDS339 (GDI+ で 960x376 描画) → libusb → DS339
```

## 構成

| ディレクトリ | 内容 | 状態 |
|---|---|---|
| [`SimHubDS339/`](SimHubDS339/) | **本命**。SimHub のデータをダッシュボードとして描画し、DS339 に送り続ける常駐アプリ | 動作確認済み |
| [`DS339Direct/`](DS339Direct/) | DS339 の USB プロトコル実装 (`Ms9132Device.cs`) と、画像を送るテスト用 CLI | 動作確認済み |
| [`docs/`](docs/) | [プロトコル解析の記録](docs/ds339-reverse-engineering.md) | — |

## 必要なもの

- Windows 10 以降、.NET 8 SDK
- [SimHub](https://www.simhubdash.com/) と [SimHub Property Server](https://github.com/pre-martin/SimHubPropertyServer) プラグイン (`PropertyServer.dll`、ポート 18082)
- [Zadig](https://zadig.akeo.ie/) — DS339 のドライバ差し替え用

## セットアップ

1. **SimHub Property Server を導入**
   `PropertyServer.dll` を SimHub のインストールフォルダに置き、SimHub 起動時のダイアログで「Property Server」を有効にします。

2. **DS339 のドライバを WinUSB に差し替え**
   Zadig を起動し、`Options` → `List All Devices` を有効にして `msusb video (Interface 3)` (USB ID `345F 9132 03`) を選び、`WinUSB` に置き換えます。
   ※ インターフェース 0 (HID) は触りません。

3. **JONSBO-AIO / AIDA64 の DS339 出力を止める**
   同じデバイスを取り合うため、同時には使えません。

4. **ビルド**

   ```bash
   cd SimHubDS339
   dotnet build -c Release
   ```

## 使い方

### 起動

次の 2 つを起動します。順番はどちらが先でも構いません。

1. SimHub
2. `SimHubDS339\bin\Release\net8.0-windows\SimHubDS339.exe` (ダブルクリック)

黒いコンソールウィンドウが開けば動作中です。タスクトレイにアイコンは出ません。

- SimHub 未接続・ゲーム未起動の間は **待機画面** (時計・日付・接続状態)、ゲームで走り始めると **レース画面** に自動で切り替わります
- SimHub やゲームが後から起動しても、自動で接続されます
- SimHub だけを起動しても表示されません。`SimHubDS339.exe` も必ず起動してください
- オプション (`--fps`、`--demo`、`--preview` など) は [SimHubDS339/README.md](SimHubDS339/README.md) を参照してください

### 終了

- **推奨**: コンソールウィンドウを選んで `Ctrl+C`。`Stopping...` と表示され、USB を閉じてから終了します
- ウィンドウを × で閉じても終了します (後始末なしの強制終了ですが、次回起動時に初期化し直すので問題ありません)
- 終了後も DS339 には **最後の画面が静止したまま残ります**。消したい場合は DS339 の USB を抜き差ししてください

### 使用上の注意

- JONSBO-AIO と同時には使えません (DS339 を取り合います)
- `SimHubDS339.exe` を 2 つ同時に起動しないでください
- 動作確認済みのゲーム: Le Mans Ultimate (LMU)

## DS339 プロトコルの要点

詳細と解析の経緯は [docs/ds339-reverse-engineering.md](docs/ds339-reverse-engineering.md) にまとめています。

| 項目 | 値 |
|---|---|
| コマンド | HID Feature Report 8 バイト (SET_REPORT / GET_REPORT、インターフェース 0) |
| 画像 | Bulk OUT ep 4 (インターフェース 3)、65536 バイトずつ + ゼロ長パケット |
| フレーム | 376x960、見た目は横置き 960x376 (時計回りに 90° 回転して送る) |
| 画素 | 非圧縮 3 バイト、B,G,R 順。**0xFF は予約値なので 254 に丸める** |
| ヘッダ / トレイラ | `FF x(16) y(16) w(12)h(12)` / `FF C0 00 00 00 00 00 00` |
| 初期化 | 転送モード 3 (MANUAL_BLOCK)、入力 color `0x11`、出力 VIC `0xAB` / color `0x00`、GPIO 初期化 |
| 表示開始 | 2 フレーム送信後に xdata `0xF005 = 0x50` → 映像有効 |
| ハートビート | xdata `0x0032` を 500ms 周期で読み出し |

## 元に戻す

JONSBO-AIO に戻す場合は、Zadig でインターフェース 3 のドライバを `libusb-win32` に戻してください。

## 免責事項

- 本ソフトウェアは、純正アプリの通信を解析して作成した **非公式** のツールです。JONSBO、MacroSilicon、SimHub とは一切関係がなく、各社への問い合わせはご遠慮ください。
- **使用は自己責任** でお願いします。本ソフトウェアの使用、および Zadig によるドライバの差し替えによって生じたいかなる損害 (DS339・PC の故障や不具合、データの消失、製品保証が受けられなくなることなどを含む) についても、作者は一切の責任を負いません。
- 動作は保証しません。動作確認は手元の DS339 1 台 (chip_id `3` = MS912C、port_type `6`) と Le Mans Ultimate のみで、個体差やファームウェアの違い、SimHub・ゲームのアップデートにより動かなくなる可能性があります。
- 本ソフトウェアは GPL-2.0 の定めにより **無保証 (NO WARRANTY)** で提供されます ([LICENSE](LICENSE) の第 11・12 条)。

## ライセンス

このリポジトリは **GNU General Public License v2.0** で公開します ([LICENSE](LICENSE))。

`DS339Direct/Ms9132Device.cs` は MacroSilicon Technology Co., Ltd. の GPL-2.0 ドライバソースを参照・移植しており、これを組み込む `SimHubDS339` も含めて GPL-2.0 としています。

同梱・依存しているサードパーティのソフトウェアとそのライセンスは [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) を参照してください。
