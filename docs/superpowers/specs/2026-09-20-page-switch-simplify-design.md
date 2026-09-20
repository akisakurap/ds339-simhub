# ページ切り替え操作の簡略化（ホイールボタン対応）設計

日付: 2026-09-20 / 対象: `SimHubDS339`

## 背景と目的

レース画面のページ切り替えが `Ctrl+Alt+Shift+PageDown` / `Ctrl+Alt+Shift+PageUp` の 4 キー同時押しで、走行中に操作できない。
ホイール（T300RS）のボタンで、ゲーム内 MFD のページ送りと同じボタンを使って切り替えられるようにする。

## 要件（ユーザー確認済み）

1. 既定キーを短縮する（3 キー）。ホイールのボタンでも切り替えられるようにし、どちらでも設定できる。
2. 設定画面で「どのコントローラーの、どのボタンで」次/前ページに切り替えるかを設定できる。
3. ユーザー環境の初期値: T300RS の **Button 25 = 次のページ**、**Button 24 = 前のページ**。

## 検証済みの事実（2026-09-20、実機）

- T300RS は winmm から `VID=044F PID=B66F`、ボタン 25 個、軸 4 本、4 方向 POV ハットとして見える（PnP 名 `Ferrari F1 Wheel Advanced T300 (USB)`）。
- デバイス番号は可変（この環境では index 3、index 0 に別の Xbox 360 系コントローラ `045E:028E`）。番号ではなく VID/PID で識別する。
- `joyGetPosEx` で 60 秒間ポーリングし、3840 回成功・失敗 0 回。Button 25（bit24）と Button 24（bit23）の押下/解放を検出できた。押下時間は約 130〜170ms。
- winmm の `szPname` は全機種で同じ文字列（「Microsoft PC ジョイスティック ドライバー」）になるため、表示名には使えない。

未検証: LMU をフルスクリーンで動かしている最中の読み取り（`joyGetPosEx` はフォーカス非依存の想定）。実装後に実機で確認する。

## 設計

### 1. `JoyBinding`（新規、`JoyBinding.cs`）

「どのコントローラーのどの入力か」を表すイミュータブルな値。

- 構成: VID、PID、入力種別（Button 1〜32 / POV 上・右・下・左）
- 文字列表現（`settings.json` 用）: `044F:B66F:B25`、`044F:B66F:POVUp` など。`TryParse` / `ToString` を持つ。
- 同一 VID/PID のデバイスが複数ある場合は区別しない（最初に見つかった 1 台。YAGNI）。

### 2. `JoystickMonitor`（新規）

- UI スレッドの `System.Windows.Forms.Timer`（20ms）で、接続中のデバイスを `joyGetPosEx` で読む。
- 押下の立ち上がり（前回 off → 今回 on）だけをイベント `InputPressed(JoyBinding)` として通知する。開始時点で押されていたボタンは通知しない（初回読み取りは基準値）。
- POV は 4 方向のみ対象。方向が変化して該当方向になった瞬間を「押下」とする。
- デバイス一覧: 起動時と 2 秒ごとに `joyGetDevCaps` で 0〜15 を走査して更新（ホットプラグ対応）。接続中デバイスだけを毎 tick 読む。
- 公開: `IReadOnlyList<JoyDeviceInfo> Devices`（VID/PID、表示名、ボタン数）、`InputPressed` イベント。
- 表示名: レジストリ `HKCU\System\CurrentControlSet\Control\MediaProperties\PrivateProperties\Joystick\OEM\VID_xxxx&PID_xxxx` の `OEMName`。取得できなければ `VID_xxxx&PID_xxxx`。
- 読み取りエラー（未接続など）は握りつぶして次の走査で再判定する。例外でアプリを落とさない。

### 3. 設定 (`AppSettings`)

- 追加: `nextPageJoy`、`prevPageJoy`（文字列、空 = 未割当）。
- 既定キーの変更: `Ctrl+Alt+PageDown` / `Ctrl+Alt+PageUp`（3 キー）。`Ctrl+Alt+矢印` は旧 Intel ドライバの画面回転と衝突するため避ける。
- 既定値の移行: 既存の `settings.json` には旧既定値 `Ctrl+Alt+Shift+PageDown/PageUp` が明示保存されている。`Load` 時に旧既定値と完全一致する場合だけ新既定値へ置き換える（ユーザーが独自に変えた値は触らない）。
- ユーザー環境の初期値: 実装後、`%APPDATA%\SimHubDS339\settings.json` に `nextPageJoy = 044F:B66F:B25`、`prevPageJoy = 044F:B66F:B24` を書き込む。コード上の既定は未割当のまま（個人設定を全ユーザーの既定にしない）。

### 4. キーボードのホットキー (`HotkeyBinding`)

- 修飾キーなしの `F13`〜`F24` を許可する（他アプリ/ゲームと衝突しにくいキーに限定）。
- `IsValid`、`TryParse`、設定画面のキー入力チェックを合わせて更新する。

### 5. 設定画面 (`SettingsForm`)

「レース画面のページ設定」グループを拡張し、次/前それぞれに以下を並べる。

- キーボード: 既存のホットキー入力欄。
- コントローラー: コンボボックス（接続中デバイスの表示名。保存済みで未接続なら「(未接続) 名前」）。
- ボタン: コンボボックス（`Button 1`〜`Button N`（そのデバイスのボタン数）、`POV ↑ → ↓ ←`）。
- 「検出」ボタン: 押すと待機状態になり、次に押されたボタンのデバイスとボタンをコンボボックスに反映する。もう一度「検出」を押すか 10 秒で中止（Esc は設定画面の「キャンセル」として先に処理されるため使わない）。
- 「クリア」ボタン: 割当を外す。
- 検証: 次と前に同じボタンは不可。キーボードと同様、次/前の重複は OK 時にエラー表示。
- ウィンドウの高さを拡張（600 → 約 700）。ページ設定グループより下のコントロールを下へずらす。

### 6. `TrayApp`

- `JoystickMonitor` を生成し、`InputPressed` を保存済みの次/前ボタンと照合して `_service.NextPage()` / `PreviousPage()` を呼ぶ。
- 設定画面が開いている間の「検出」中は、通常のページ切り替えを発火させない（設定画面側が排他的に受け取る）。
- 終了時に `JoystickMonitor` を破棄する。
- ゲーム側の MFD ページ送りと同じボタンを割り当てた場合、ゲームと DS339 が同時に切り替わる（意図した挙動）。

## エラー処理

- 割当済みデバイスが未接続: 何もしない。設定画面では「(未接続)」表示。
- `settings.json` の `nextPageJoy` 等が不正な文字列: 未割当として扱う。
- 次/前に同じホイールボタンを保存しようとした場合: 保存せずエラー表示。

## テスト・検証

- 純粋なロジック（`HotkeyBinding`、`JoyBinding`、`AppSettings` の移行、押下検出）は `SimHubDS339.Tests`（xunit）で自動テストする。
- 実機（T300RS 接続）で: Button 25 で次ページ、Button 24 で前ページ、設定画面の「検出」、切断・再接続後の復帰を確認する。
- LMU をフルスクリーンで起動した状態でもボタンが効くことを確認する。

## スコープ外

- DirectInput / XInput 対応、33 個以上のボタン、同一機種の複数台の区別。
- ボタン長押し・組み合わせ押し。
