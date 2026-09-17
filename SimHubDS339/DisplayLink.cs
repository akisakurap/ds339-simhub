using System.Diagnostics;
using DS339Direct;

namespace SimHubDS339
{
    /// <summary>
    /// DS339 との接続を保持し、切断や送信失敗時に自動で再接続・再初期化するラッパー。
    /// 公式アプリと同様に xdata 0x0032 を 500ms 周期で読むハートビートも担当する。
    /// </summary>
    internal sealed class DisplayLink : IDisposable
    {
        private const int HeartbeatPeriodMs = 500;
        private const int RetryDelayMs = 3000;

        private Ms9132Device? _device;
        private int _framesSinceInit;
        private bool _screenShown;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private long _nextHeartbeatMs;
        private long _nextRetryMs;

        public bool IsConnected => _device != null;

        /// <summary>
        /// 見た目どおりの 960x376 RGB888 フレームを送る。未接続なら接続を試みる。
        /// </summary>
        public bool TrySend(byte[] landscapeRgb)
        {
            if (!EnsureConnected()) return false;

            try
            {
                Heartbeat();

                if (!_device!.SendLandscapeFrame(landscapeRgb))
                {
                    Log("frame send failed, reconnecting");
                    Disconnect();
                    return false;
                }

                // 公式アプリは 2 フレーム送信後に screen_enable(0xF005=0x50) → video_enable する
                _framesSinceInit++;
                if (!_screenShown && _framesSinceInit >= 2)
                {
                    _device.ShowScreen();
                    _screenShown = true;
                    Log("screen enabled");
                }
                return true;
            }
            catch (Exception ex)
            {
                Log($"USB error: {ex.Message}");
                Disconnect();
                return false;
            }
        }

        /// <summary>フレームを送らない間もハートビートだけは維持したい場合に呼ぶ。</summary>
        public void Heartbeat()
        {
            if (_device == null) return;
            if (_clock.ElapsedMilliseconds < _nextHeartbeatMs) return;

            if (!_device.XdataReadOnce(0x0032, 3, out _))
            {
                throw new IOException("heartbeat (xdata 0x0032 read) failed");
            }
            _nextHeartbeatMs = _clock.ElapsedMilliseconds + HeartbeatPeriodMs;
        }

        private bool EnsureConnected()
        {
            if (_device != null) return true;
            if (_clock.ElapsedMilliseconds < _nextRetryMs) return false;

            try
            {
                var dev = new Ms9132Device();
                try
                {
                    dev.Open();
                    if (!dev.InitializeDisplayDS339())
                    {
                        throw new IOException("display initialization failed");
                    }
                }
                catch
                {
                    dev.Dispose();
                    throw;
                }

                _device = dev;
                _framesSinceInit = 0;
                _screenShown = false;
                _nextHeartbeatMs = 0;
                Log("DS339 connected and initialized");
                return true;
            }
            catch (Exception ex)
            {
                Log($"DS339 open failed: {ex.Message} (retry in {RetryDelayMs / 1000}s)");
                _nextRetryMs = _clock.ElapsedMilliseconds + RetryDelayMs;
                return false;
            }
        }

        private void Disconnect()
        {
            try
            {
                _device?.Dispose();
            }
            catch
            {
                // 切断済みデバイスの破棄エラーは無視
            }
            _device = null;
            _nextRetryMs = _clock.ElapsedMilliseconds + RetryDelayMs;
        }

        private static void Log(string message)
        {
            Console.WriteLine($"{DateTime.Now:HH:mm:ss} [DS339] {message}");
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
