using System;
using System.Linq;
using LibUsbDotNet.LibUsb;
using LibUsbDotNet.Main;

namespace DS339Direct
{
    /// <summary>
    /// MacroSilicon MS9132 (DS339 内蔵チップ, USB VID 0x345F / PID 0x9132) を
    /// libusb 経由で直接叩くクライアント。
    ///
    /// プロトコルは MacroSilicon 公式 GPL ソース (ms91xx-linux-drm リポジトリの
    /// usb_hal/usb_device.c, usb_hal_interface.c, usb_hal_thread.c) を読んで移植した。
    /// HID Feature Report (8 バイト固定) をコントロール転送で送受信し、
    /// 実フレームデータは Bulk OUT (endpoint 4) で送る。
    /// </summary>
    public sealed class Ms9132Device : IDisposable
    {
        public const int VendorId = 0x345F;
        public const int ProductId = 0x9132;

        private const byte ReportTypeFeature = 0x03;
        private const int BulkEndpoint = 4;

        // HID op codes (usb_device_hid.h)
        private const byte OpReadData = 0xB5;
        private const byte OpWriteOneByte = 0xB6;
        private const byte OpReadSfrData = 0xC5;
        private const byte OpWriteSfrData = 0xC6;
        private const byte OpReadFlashEightBytes = 0xF5;
        private const byte OpVideo = 0xA6;

        // sub-ops under OpVideo
        private const byte SubOpTriggerFrame = 0x00;
        private const byte SubOpUpdateInInfo = 0x01;
        private const byte SubOpUpdateOutInfo = 0x02;
        private const byte SubOpSetTransMode = 0x03;
        private const byte SubOpTransfer = 0x04;
        private const byte SubOpEnable = 0x05;
        private const byte SubOpPower = 0x07;

        // xdata registers
        private const ushort RegChipId = 0xFF00;
        private const ushort RegChipId9120 = 0xF000;
        private const ushort RegSdramType = 0x0030;
        private const ushort RegVideoPort = 0x0031;
        private const ushort RegFrameSwitch = 0xD003;
        private const ushort RegHdmiTxMute = 0xFB07;

        private const int TransModeFrame = 0;
        public const byte ColorFormatRgb565 = 0;
        public const byte ColorFormatRgb888 = 1;
        public const byte ColorFormatYuv422 = 2;

        private IUsbDevice? _device;
        private UsbContext? _context;
        private UsbEndpointWriter? _bulkWriter;
        private byte _frameIndex;
        private UsbDeviceCollection? _devices;

        public void Open()
        {
            _context = new UsbContext();
            // このコレクションを using で即 Dispose すると中の IUsbDevice も破棄されてしまうため、
            // Dispose() までフィールドとして保持し続ける。
            _devices = _context.List();
            var found = _devices.FirstOrDefault(d => d.VendorId == VendorId && d.ProductId == ProductId);
            if (found == null)
            {
                throw new InvalidOperationException($"DS339 (VID {VendorId:X4} PID {ProductId:X4}) が見つかりません。接続を確認してください。");
            }

            _device = found;
            _device.Open();

            // インターフェース 0 = HID (Windows 側は HidUsb にバインドされておりここでは Claim できない)
            // インターフェース 3 = Vendor Specific / libusb0 (Bulk OUT ep 0x04 でフレームデータ送信)。
            // HID Feature Report のコントロール転送はインターフェース占有と無関係にデバイスハンドルから送れる。
            foreach (var iface in _device.Configs[0].Interfaces)
            {
                try
                {
                    _device.ClaimInterface(iface.Number);
                    Console.WriteLine($"[Ms9132] claimed interface {iface.Number}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Ms9132] claim interface {iface.Number} failed (ignored): {ex.Message}");
                }
            }

            _bulkWriter = _device.OpenEndpointWriter((WriteEndpointID)BulkEndpoint);

            // 現在の frame_index をチップから読んでおく (画面が既に点灯中の場合の継続性のため)
            if (XdataReadOnce(RegFrameSwitch, 1, out var buf) && buf[0] != 0)
            {
                _frameIndex = 1;
            }
            else
            {
                _frameIndex = 0;
            }
        }

        private IUsbDevice Device => _device ?? throw new InvalidOperationException("Open() を先に呼んでください。");

        public void DumpInterfaces()
        {
            var dev = Device;
            foreach (var config in dev.Configs)
            {
                Console.WriteLine($"Config: {config}");
                foreach (var iface in config.Interfaces)
                {
                    Console.WriteLine($"  Interface: {iface}");
                    foreach (var ep in iface.Endpoints)
                    {
                        Console.WriteLine($"    Endpoint: {ep}");
                    }
                }
            }
        }

        // ---- 低レベル HID Feature Report 送受信 ----

        private bool HidSet(byte[] report8)
        {
            if (report8.Length != 8) throw new ArgumentException("HID report must be 8 bytes");
            var setup = new UsbSetupPacket(
                bRequestType: 0x21, // OUT | CLASS | INTERFACE
                bRequest: 0x09,     // SET_REPORT
                wValue: (ReportTypeFeature << 8) | 0,
                wIndex: 0,
                wlength: 8);
            var sent = Device.ControlTransfer(setup, report8, 0, 8);
            return sent == 8;
        }

        private bool HidGet(byte[] report8)
        {
            if (report8.Length != 8) throw new ArgumentException("HID report must be 8 bytes");
            var setup = new UsbSetupPacket(
                bRequestType: 0xA1, // IN | CLASS | INTERFACE
                bRequest: 0x01,     // GET_REPORT
                wValue: (ReportTypeFeature << 8) | 0,
                wIndex: 0,
                wlength: 8);
            var got = Device.ControlTransfer(setup, report8, 0, 8);
            return got == 8;
        }

        // ---- xdata / sfr アクセス ----

        public bool XdataReadOnce(ushort addr, byte count, out byte[] data)
        {
            data = new byte[4];
            var report = new byte[8];
            report[0] = OpReadData;
            report[1] = (byte)((addr >> 8) & 0xFF);
            report[2] = (byte)(addr & 0xFF);

            if (!HidSet(report)) return false;
            if (!HidGet(report)) return false;

            Array.Copy(report, 3, data, 0, Math.Min((int)count, 4));
            return true;
        }

        public bool XdataWriteByte(ushort addr, byte value)
        {
            var report = new byte[8];
            report[0] = OpWriteOneByte;
            report[1] = (byte)((addr >> 8) & 0xFF);
            report[2] = (byte)(addr & 0xFF);
            report[3] = value;
            return HidSet(report);
        }

        public bool SfrReadByte(byte addr, out byte value)
        {
            var report = new byte[8];
            report[0] = OpReadSfrData;
            report[1] = addr;
            var ok = HidSet(report) && HidGet(report);
            value = ok ? report[2] : (byte)0;
            return ok;
        }

        public bool SfrWriteByte(byte addr, byte value)
        {
            var report = new byte[8];
            report[0] = OpWriteSfrData;
            report[1] = addr;
            report[2] = value;
            return HidSet(report);
        }

        /// <summary>
        /// ms9132_read_flash の移植。8 バイト単位で SPI flash を読む
        /// (GET_REPORT 応答の 8 バイトがそのままデータになる)。
        /// </summary>
        public bool ReadFlash(uint addr, byte[] outBuf, int len)
        {
            int pos = 0;
            while (pos < len)
            {
                var r = new byte[8];
                uint a = addr + (uint)pos;
                r[0] = OpReadFlashEightBytes;
                r[1] = (byte)((a >> 16) & 0xFF);
                r[2] = (byte)((a >> 8) & 0xFF);
                r[3] = (byte)(a & 0xFF);

                if (!HidSet(r)) return false;
                if (!HidGet(r)) return false;

                int copyLen = Math.Min(8, len - pos);
                Array.Copy(r, 0, outBuf, pos, copyLen);
                pos += 8;
            }
            return true;
        }

        /// <summary>
        /// usb_hal_read_custom_timing の移植。フラッシュに書き込まれたカスタム
        /// タイミング情報 (960x376 のような非標準解像度に対応する VIC 等) を読み出す。
        /// 見つからなければ null。
        /// </summary>
        public (byte vic, ushort width, ushort height, byte rate)? ReadCustomTiming(byte chipId)
        {
            const byte ChipId9132 = 0;
            uint baseAddr = (chipId == ChipId9132) ? 0xFC50u : 0x1C00u;

            var head = new byte[8];
            if (!ReadFlash(baseAddr, head, 7)) return null;

            var magic = System.Text.Encoding.ASCII.GetBytes("modify");
            for (int i = 0; i < 6; i++)
            {
                if (head[i] != magic[i]) return null;
            }

            int count = head[6] switch { 0x31 => 1, 0x32 => 2, _ => 0 };
            if (count == 0) return null;

            // misctiming_t: vic(1) polarity(1) htotal(2) vtotal(2) hactive(2) vactive(2) pixclk(2) vfreq(2) hoffset(2) voffset(2) hsyncwidth(2) vsyncwidth(2) = 20 bytes
            var timing = new byte[20];
            if (!ReadFlash(baseAddr + 0x10, timing, timing.Length)) return null;

            byte vic = timing[0];
            ushort hactive = (ushort)(timing[6] | (timing[7] << 8));
            ushort vactive = (ushort)(timing[8] | (timing[9] << 8));
            ushort vfreq = (ushort)(timing[12] | (timing[13] << 8)); // 0.01Hz 単位
            byte rate = (byte)((vfreq + 50) / 100);

            Console.WriteLine($"[Ms9132] custom timing found: vic={vic} {hactive}x{vactive}@{rate}Hz (count={count})");
            return (vic, hactive, vactive, rate);
        }

        public bool GetChipId(out byte chipId)
        {
            chipId = 0;
            if (!XdataReadOnce(RegChipId, 3, out var buf)) return false;
            if (buf[1] == 0x13 && buf[2] == 0x0A)
            {
                chipId = 0; // CHIP_ID_9132
                return true;
            }

            if (!XdataReadOnce(RegChipId9120, 3, out buf)) return false;
            if (buf[1] == 0x16 && buf[2] == 0x0A)
            {
                chipId = buf[0] switch { 0xB7 => (byte)3, 0xA7 => (byte)2, _ => (byte)1 };
                return true;
            }

            return false;
        }

        public bool GetPortType(out byte portType)
        {
            portType = 0;
            if (!XdataReadOnce(RegVideoPort, 1, out var buf)) return false;
            portType = buf[0];
            return true;
        }

        public bool GetSdramType(out byte sdramType)
        {
            sdramType = 0;
            if (!XdataReadOnce(RegSdramType, 1, out var buf)) return false;
            sdramType = buf[0];
            return true;
        }

        // ---- ビデオコマンド ----

        public bool SetVideoInInfo(ushort width, ushort height, byte color, byte byteSel)
        {
            var r = new byte[8];
            r[0] = OpVideo;
            r[1] = SubOpUpdateInInfo;
            r[2] = (byte)((width >> 8) & 0xFF);
            r[3] = (byte)(width & 0xFF);
            r[4] = (byte)((height >> 8) & 0xFF);
            r[5] = (byte)(height & 0xFF);
            r[6] = color;
            r[7] = byteSel;
            return HidSet(r);
        }

        public bool SetVideoOutInfo(byte vic, byte color, ushort width, ushort height)
        {
            var r = new byte[8];
            r[0] = OpVideo;
            r[1] = SubOpUpdateOutInfo;
            r[2] = vic;
            r[3] = color;
            r[4] = (byte)((width >> 8) & 0xFF);
            r[5] = (byte)(width & 0xFF);
            r[6] = (byte)((height >> 8) & 0xFF);
            r[7] = (byte)(height & 0xFF);
            return HidSet(r);
        }

        public bool TriggerFrame(byte index, byte delay)
        {
            var r = new byte[8];
            r[0] = OpVideo;
            r[1] = SubOpTriggerFrame;
            r[2] = index;
            r[3] = delay;
            return HidSet(r);
        }

        public bool SetTransMode(byte mode)
        {
            var r = new byte[8];
            r[0] = OpVideo;
            r[1] = SubOpSetTransMode;
            r[2] = mode;
            return HidSet(r);
        }

        public bool SetTransEnable(bool enable)
        {
            var r = new byte[8];
            r[0] = OpVideo;
            r[1] = SubOpTransfer;
            r[2] = (byte)(enable ? 1 : 0);
            return HidSet(r);
        }

        public bool SetVideoEnable(bool enable)
        {
            var r = new byte[8];
            r[0] = OpVideo;
            r[1] = SubOpEnable;
            r[2] = (byte)(enable ? 1 : 0);
            return HidSet(r);
        }

        public bool SetPowerEnable(bool enable)
        {
            var r = new byte[8];
            r[0] = OpVideo;
            r[1] = SubOpPower;
            r[2] = (byte)(enable ? 1 : 0);
            r[3] = 2;
            return HidSet(r);
        }

        /// <summary>
        /// ms9132_set_screen_enable の移植。usb_device.c のオリジナルは chip_id で
        /// レジスタアドレス/マスクを完全に分岐している (CHIP_ID_9132 とそれ以外で別テーブル)。
        /// </summary>
        public bool SetScreenEnable(bool enable, byte chipId, byte portType)
        {
            const byte ChipId9132 = 0;
            const byte VideoPortHdmi = 5;
            const byte VideoPortVga = 2;
            const byte VideoPortYpbpr = 3;
            const byte VideoPortDigital = 6;

            ushort addr;
            byte mask;
            bool isClear = false;

            if (chipId == ChipId9132)
            {
                if (portType == VideoPortHdmi)
                {
                    addr = RegHdmiTxMute;
                    mask = 1 << 1;
                    isClear = true;
                }
                else
                {
                    addr = 0xF037;
                    mask = 0x1;
                }
            }
            else
            {
                switch (portType)
                {
                    case VideoPortHdmi:
                        addr = 0xF507;
                        mask = 0x2;
                        isClear = true;
                        break;
                    case VideoPortVga:
                        addr = 0xF004;
                        mask = 0x80;
                        break;
                    case VideoPortYpbpr:
                        addr = 0xF030;
                        mask = 0x1;
                        break;
                    case VideoPortDigital:
                        addr = 0xF005;
                        mask = 0x10;
                        break;
                    default:
                        addr = 0xF004;
                        mask = 0x2;
                        break;
                }
            }

            if (!XdataReadOnce(addr, 1, out var buf)) return false;
            byte data = buf[0];

            bool setBit = enable ^ isClear;
            data = setBit ? (byte)(data | mask) : (byte)(data & ~mask);

            return XdataWriteByte(addr, data);
        }

        /// <summary>
        /// DS339 (chip_id=CHIP_ID_912C, port_type=VIDEO_PORT_DIGITAL) 実機を、
        /// 実際に動作している JONSBO-AIO の USB 通信を Wireshark/USBPcap でキャプチャして
        /// 解析した結果そのまま移植した初期化シーケンス。Linux 公式ドライバ (HDMI/VGA
        /// キャプチャ用途) の一般化されたロジックとは異なる、この実機固有の手順:
        ///   - 解像度は 376(w) x 960(h) (縦長。960x376 ではない)
        ///   - trans_mode = MANUAL_BLOCK (3)。FRAME (0) ではない
        ///   - color_in = 0x11 (vpack_out=RGB888, vpack_in=RGB888)
        ///   - video_out の vic = 0xAB (171、標準 VESA/CEA コードにはない値)
        ///   - video_out の color = 0x00 (RGB565 出力レジスタだが、Bulk 転送データ自体は
        ///     RGB888 のまま。チップ内部でパネル出力用に変換していると見られる)
        ///   - GPIO 初期化 (SFR 0xB0/0xA0 書き込み、xdata 0xF016 書き込み) が必須
        ///   - screen_enable は read-modify-write ではなく直接値を書き込む
        ///     (0xF005 に初期化中は 0x40、最後に本番の 0x50)
        ///   - trigger_frame は一切使われない
        /// </summary>
        public bool InitializeDisplayDS339()
        {
            const ushort Width = 376;
            const ushort Height = 960;
            const byte Vic = 0xAB;
            const byte ColorIn = 0x11;
            const byte ColorOut = 0x00;

            SetPowerEnable(true);
            SetVideoEnable(false);

            XdataWriteByte(0xF005, 0x40);

            SfrWriteByte(0xB0, 0xDF);
            SfrWriteByte(0xA0, 0xE4);
            SfrWriteByte(0xA0, 0xF0);
            XdataWriteByte(0xF016, 0x08);

            SetTransMode(3); // MS9132_TRANS_MODE_MANUAL_BLOCK

            SetVideoInInfo(Width, Height, ColorIn, 0);
            SetVideoOutInfo(Vic, ColorOut, Width, Height);

            SetTransEnable(true);

            return true;
        }

        /// <summary>
        /// キャプチャ解析の結果、画面へ実際に反映させるスイッチ。
        /// フレームを 2 回 Bulk 転送し終えたあとにこれを呼ぶと画面が切り替わる。
        /// 実機キャプチャでの順序は screen_enable(0xF005=0x50) → video_enable(true)。
        /// </summary>
        public bool ShowScreen()
        {
            XdataReadOnce(0xF005, 1, out _);
            XdataWriteByte(0xF005, 0x50);
            return SetVideoEnable(true);
        }

        /// <summary>
        /// RGB888 (バイト順は R,G,B, 行優先) のフレームを Bulk OUT (endpoint 4) へ送る。
        /// 実機キャプチャ解析により、画素データの前後に 8 バイトずつのヘッダ/トレイラが必要と判明:
        ///   header : 0xFF, x(16bit BE), y(16bit BE), width(12bit) | height(12bit) (計 8 バイト)
        ///            例 376x960 全面 = FF 00 00 00 00 17 83 C0
        ///   trailer: FF C0 00 00 00 00 00 00 (end of buffer)
        /// 公式アプリは 65536 バイト単位に分割して送り、最後にゼロ長パケットを送っている。
        /// </summary>
        public bool SendFrame(byte[] rgbData, ushort x = 0, ushort y = 0, ushort width = 376, ushort height = 960)
        {
            if (_bulkWriter == null) throw new InvalidOperationException("Open() を先に呼んでください。");
            if (rgbData.Length != width * height * 3) throw new ArgumentException("rgbData length mismatch");

            var buf = new byte[8 + rgbData.Length + 8];
            buf[0] = 0xFF;
            buf[1] = (byte)(x >> 8);
            buf[2] = (byte)x;
            buf[3] = (byte)(y >> 8);
            buf[4] = (byte)y;
            buf[5] = (byte)(width >> 4);
            buf[6] = (byte)(((width & 0x0F) << 4) | ((height >> 8) & 0x0F));
            buf[7] = (byte)height;
            // 0xFF はブロックヘッダ/トレイラのマーカーとして予約されている。画素データに 0xFF が
            // 含まれるとチップ側パーサが誤解釈し、受信が極端に遅くなる (1 フレーム 12 秒) うえに表示も乱れる。
            // 公式アプリのキャプチャでも画素データ中の 0xFF は皆無で、255 は 254 に丸められていた。
            // また実機で色テストした結果、パネル上のバイト順は B,G,R だったため入力 RGB を並べ替える。
            for (int i = 0; i < rgbData.Length; i += 3)
            {
                byte r = rgbData[i], g = rgbData[i + 1], b = rgbData[i + 2];
                buf[8 + i] = b == 0xFF ? (byte)0xFE : b;
                buf[8 + i + 1] = g == 0xFF ? (byte)0xFE : g;
                buf[8 + i + 2] = r == 0xFF ? (byte)0xFE : r;
            }
            buf[buf.Length - 8] = 0xFF;
            buf[buf.Length - 7] = 0xC0;

            const int ChunkSize = 65536;
            for (int offset = 0; offset < buf.Length; offset += ChunkSize)
            {
                int len = Math.Min(ChunkSize, buf.Length - offset);
                var err = _bulkWriter.Write(buf, offset, len, 5000, out var written);
                if (err != LibUsbDotNet.Error.Success || written != len)
                {
                    Console.WriteLine($"[Ms9132] bulk write failed at {offset}: {err}, written={written}/{len}");
                    return false;
                }
            }

            _bulkWriter.Write(Array.Empty<byte>(), 1000, out _);
            return true;
        }

        /// <summary>
        /// DS339 は横置き (見た目 960x376) で実装されており、チップ上の 376x960 フレームの
        /// 行 0 が画面左端、列 0 が画面下端に対応する (実機で確認済み)。
        /// 見た目どおりの横長 RGB888 画像 (960x376, 行優先) を時計回りに 90 度回転して送る。
        /// </summary>
        public bool SendLandscapeFrame(byte[] rgb960x376)
        {
            const int LW = 960, LH = 376;
            if (rgb960x376.Length != LW * LH * 3) throw new ArgumentException("rgb960x376 length mismatch");

            var rotated = new byte[LH * LW * 3];
            for (int r = 0; r < LW; r++)
            {
                for (int c = 0; c < LH; c++)
                {
                    int src = ((LH - 1 - c) * LW + r) * 3;
                    int dst = (r * LH + c) * 3;
                    rotated[dst] = rgb960x376[src];
                    rotated[dst + 1] = rgb960x376[src + 1];
                    rotated[dst + 2] = rgb960x376[src + 2];
                }
            }
            return SendFrame(rotated);
        }

        public void Dispose()
        {
            try
            {
                _device?.Close();
            }
            catch
            {
                // ignore
            }

            _devices?.Dispose();
            _context?.Dispose();
        }
    }
}
