# サードパーティ ソフトウェア

このリポジトリが同梱・依存・参照しているサードパーティのソフトウェアと、そのライセンスです。

## 同梱しているもの

### libusb 1.0.30

- ファイル: `DS339Direct/native/libusb-1.0.dll` (公式リリース `libusb-1.0.30.7z` の `VS2022/MS64` ビルド、未改変)
- 配布元: https://github.com/libusb/libusb
- ライセンス: GNU Lesser General Public License v2.1 or later
- ソースコード: https://github.com/libusb/libusb/releases/tag/v1.0.30

## ビルド時に NuGet から取得するもの

| パッケージ | バージョン | ライセンス | 配布元 |
|---|---|---|---|
| LibUsbDotNet | 3.0.224 | LGPL-3.0 | https://github.com/LibUsbDotNet/LibUsbDotNet |
| System.Drawing.Common | 8.0.10 | MIT | https://github.com/dotnet/runtime |
| System.Diagnostics.PerformanceCounter | 8.0.1 | MIT | https://github.com/dotnet/runtime |
| LibreHardwareMonitorLib | 0.9.6 | MPL-2.0 | https://github.com/LibreHardwareMonitor/LibreHardwareMonitor |
| DiskInfoToolkit (LibreHardwareMonitorLib の依存) | 1.1.2 | MPL-2.0 | https://github.com/Blacktempel/DiskInfoToolkit |
| RAMSPDToolkit-NDD (LibreHardwareMonitorLib の依存) | 1.4.2 | MPL-2.0 | https://github.com/Blacktempel/RAMSPDToolkit |
| HidSharp (LibreHardwareMonitorLib の依存) | 2.6.4 | Apache-2.0 | https://software.seekye.com/hidsharp |
| Mono.Posix.NETStandard (LibreHardwareMonitorLib の依存) | 1.0.0 | MIT | https://github.com/mono/mono |
| System.Management / System.IO.Ports / System.Threading.AccessControl (LibreHardwareMonitorLib の依存) | 10.0.x | MIT | https://github.com/dotnet/runtime |

## 参照・移植したソースコード

### MacroSilicon MS91xx Linux ドライバ

- 対象: `DS339Direct/Ms9132Device.cs` (HID コマンド体系、レジスタ定義、初期化手順の移植)
- 原著作者: MacroSilicon Technology Co., Ltd.
- 参照元: https://github.com/bambinounos/ms91xx-linux-drm (MacroSilicon 公式ソース `MS91xx_Linux_Drm_SourceCode_V3.0.3.13` / `FrameBuffer_SourceCode_V3.0.2.11` を含む)
- ライセンス: GNU General Public License v2.0

## 連携・参照するソフトウェア (コードは含まない)

| ソフトウェア | ライセンス | 用途 |
|---|---|---|
| [SimHub Property Server](https://github.com/pre-martin/SimHubPropertyServer) | LGPL-3.0 | TCP プロトコル (ポート 18082) でテレメトリを受信 |
| SimHub | プロプライエタリ | テレメトリの提供元 |
| [PawnIO](https://pawnio.eu/) | 配布元を参照 | LibreHardwareMonitorLib が CPU 温度取得に使うカーネルドライバ。任意でユーザーが別途インストールする |
| JONSBO-AIO | プロプライエタリ | プロトコル解析のため USB 通信を観察したのみ。バイナリ・画像・キャプチャデータは含まない |
