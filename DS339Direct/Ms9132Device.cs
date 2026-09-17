using System;
using System.Linq;
using LibUsbDotNet.LibUsb;
using LibUsbDotNet.Main;

namespace DS339Direct
{
    /// <summary>
    /// JONSBO DS339 (MacroSilicon MS912C, USB VID 0x345F / PID 0x9132) を libusb で直接制御する。
    /// コマンド体系は MacroSilicon 公式 GPL-2.0 ドライバ (ms91xx-linux-drm) から、
    /// DS339 固有の初期化・フレーム形式は JONSBO-AIO の USB キャプチャから移植した。
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
        private const byte OpWriteSfrData = 0xC6;
        private const byte OpVideo = 0xA6;

        // sub-ops under OpVideo
        private const byte SubOpUpdateInInfo = 0x01;
        private const byte SubOpUpdateOutInfo = 0x02;
        private const byte SubOpSetTransMode = 0x03;
        private const byte SubOpTransfer = 0x04;
        private const byte SubOpEnable = 0x05;
        private const byte SubOpPower = 0x07;

        // xdata registers
        private const ushort RegChipId = 0xFF00;
        private const ushort RegChipId9120 = 0xF000;
        private const ushort RegVideoPort = 0x0031;
        private const ushort RegScreen = 0xF005;

        private IUsbDevice? _device;
        private UsbContext? _context;
        private UsbDeviceCollection? _devices;
        private UsbEndpointWriter? _bulkWriter;

        public void Open()
        {
            _context = new UsbContext();
            // Disposing this collection also disposes the devices in it, so keep it until Dispose().
            _devices = _context.List();
            var found = _devices.FirstOrDefault(d => d.VendorId == VendorId && d.ProductId == ProductId);
            if (found == null)
            {
                throw new InvalidOperationException($"DS339 (VID {VendorId:X4} PID {ProductId:X4}) が見つかりません。接続を確認してください。");
            }

            _device = found;
            _device.Open();

            // Interface 0 = HID (feature reports), interface 3 = vendor/WinUSB (bulk ep 4).
            foreach (var iface in _device.Configs[0].Interfaces)
            {
                try
                {
                    _device.ClaimInterface(iface.Number);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Ms9132] claim interface {iface.Number} failed (ignored): {ex.Message}");
                }
            }

            _bulkWriter = _device.OpenEndpointWriter((WriteEndpointID)BulkEndpoint);
        }

        private IUsbDevice Device => _device ?? throw new InvalidOperationException("Open() を先に呼んでください。");

        // ---- HID Feature Report (8 bytes) ----

        private bool HidSet(byte[] report8)
        {
            var setup = new UsbSetupPacket(
                bRequestType: 0x21, // OUT | CLASS | INTERFACE
                bRequest: 0x09,     // SET_REPORT
                wValue: ReportTypeFeature << 8,
                wIndex: 0,
                wlength: 8);
            return Device.ControlTransfer(setup, report8, 0, 8) == 8;
        }

        private bool HidGet(byte[] report8)
        {
            var setup = new UsbSetupPacket(
                bRequestType: 0xA1, // IN | CLASS | INTERFACE
                bRequest: 0x01,     // GET_REPORT
                wValue: ReportTypeFeature << 8,
                wIndex: 0,
                wlength: 8);
            return Device.ControlTransfer(setup, report8, 0, 8) == 8;
        }

        // ---- xdata / sfr ----

        public bool XdataReadOnce(ushort addr, byte count, out byte[] data)
        {
            data = new byte[4];
            var report = new byte[8];
            report[0] = OpReadData;
            report[1] = (byte)(addr >> 8);
            report[2] = (byte)addr;

            if (!HidSet(report) || !HidGet(report)) return false;

            Array.Copy(report, 3, data, 0, Math.Min((int)count, 4));
            return true;
        }

        private bool XdataWriteByte(ushort addr, byte value)
        {
            var report = new byte[8];
            report[0] = OpWriteOneByte;
            report[1] = (byte)(addr >> 8);
            report[2] = (byte)addr;
            report[3] = value;
            return HidSet(report);
        }

        private bool SfrWriteByte(byte addr, byte value)
        {
            var report = new byte[8];
            report[0] = OpWriteSfrData;
            report[1] = addr;
            report[2] = value;
            return HidSet(report);
        }

        /// <summary>chip_id: 0=MS9132, 1=MS9120, 2=MS912A, 3=MS912C (DS339)。</summary>
        public bool GetChipId(out byte chipId)
        {
            chipId = 0;
            if (!XdataReadOnce(RegChipId, 3, out var buf)) return false;
            if (buf[1] == 0x13 && buf[2] == 0x0A)
            {
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

        // ---- video commands ----

        private bool Video(byte subOp, params byte[] args)
        {
            var r = new byte[8];
            r[0] = OpVideo;
            r[1] = subOp;
            Array.Copy(args, 0, r, 2, Math.Min(args.Length, 6));
            return HidSet(r);
        }

        private bool SetVideoInInfo(ushort width, ushort height, byte color) =>
            Video(SubOpUpdateInInfo, (byte)(width >> 8), (byte)width, (byte)(height >> 8), (byte)height, color, 0);

        private bool SetVideoOutInfo(byte vic, byte color, ushort width, ushort height) =>
            Video(SubOpUpdateOutInfo, vic, color, (byte)(width >> 8), (byte)width, (byte)(height >> 8), (byte)height);

        private bool SetTransMode(byte mode) => Video(SubOpSetTransMode, mode);

        private bool SetTransEnable(bool enable) => Video(SubOpTransfer, (byte)(enable ? 1 : 0));

        private bool SetVideoEnable(bool enable) => Video(SubOpEnable, (byte)(enable ? 1 : 0));

        private bool SetPowerEnable(bool enable) => Video(SubOpPower, (byte)(enable ? 1 : 0), 2);

        /// <summary>
        /// JONSBO-AIO のキャプチャどおりの DS339 初期化。Linux 公式ドライバとは
        /// 解像度 (376x960)、転送モード (3=MANUAL_BLOCK)、VIC (0xAB)、色指定、GPIO 初期化が異なる。
        /// </summary>
        public bool InitializeDisplayDS339()
        {
            const ushort Width = 376;
            const ushort Height = 960;
            const byte Vic = 0xAB;
            const byte ColorIn = 0x11;  // input RGB888
            const byte ColorOut = 0x00; // panel output

            SetPowerEnable(true);
            SetVideoEnable(false);
            XdataWriteByte(RegScreen, 0x40);

            SfrWriteByte(0xB0, 0xDF);
            SfrWriteByte(0xA0, 0xE4);
            SfrWriteByte(0xA0, 0xF0);
            XdataWriteByte(0xF016, 0x08);

            SetTransMode(3);
            SetVideoInInfo(Width, Height, ColorIn);
            SetVideoOutInfo(Vic, ColorOut, Width, Height);
            return SetTransEnable(true);
        }

        /// <summary>フレームを 2 回送ったあとに呼ぶと表示が切り替わる (キャプチャでの順序)。</summary>
        public bool ShowScreen()
        {
            XdataReadOnce(RegScreen, 1, out _);
            XdataWriteByte(RegScreen, 0x50);
            return SetVideoEnable(true);
        }

        /// <summary>
        /// RGB888 (R,G,B 行優先) のフレームを Bulk OUT ep 4 へ送る。
        /// 形式: header <c>FF x(16) y(16) w(12)h(12)</c> + 画素 + trailer <c>FF C0 00 00 00 00 00 00</c>、
        /// 65536 バイト単位で送信しゼロ長パケットで終える。
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
            // 0xFF is the header/trailer marker; a 0xFF pixel byte makes the chip stall (~12s/frame)
            // and corrupts the image, so clamp to 0xFE. The panel expects B,G,R order.
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
        /// 見た目どおりの横長 RGB888 画像 (960x376) を送る。パネルは横置きのため、
        /// 時計回りに 90 度回転して 376x960 のチップ上フレームに変換する (実機で確認済み)。
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
                // best-effort close
            }

            _devices?.Dispose();
            _context?.Dispose();
        }
    }
}
