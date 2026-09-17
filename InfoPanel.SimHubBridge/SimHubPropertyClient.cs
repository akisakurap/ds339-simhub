using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace InfoPanel.SimHubBridge
{
    /// <summary>
    /// SimHub Property Server (https://github.com/pre-martin/SimHubPropertyServer) との
    /// telnet ライクな行指向 TCP プロトコルを喋るクライアント。
    ///
    /// プロトコル仕様 (doc/PropertyServer/PropertyServer.adoc より):
    ///   接続直後にサーバーが "SimHub Property Server" という行を送ってくる。
    ///   クライアントは "subscribe <propertyName>" を送るとその後、値が変化するたびに
    ///     "Property <name> <type> <value>"
    ///   という行が push される。値が無い場合は "(null)"。
    ///   更新レートは 10Hz 固定。
    ///
    /// 例:
    ///   &gt; SimHub Property Server
    ///   &lt; subscribe dcp.gd.SpeedKmh
    ///   &gt; Property dcp.gd.SpeedKmh double (null)
    ///   &gt; Property dcp.gd.SpeedKmh double 123.4
    /// </summary>
    public sealed class SimHubPropertyClient : IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private readonly IReadOnlyList<string> _propertiesToSubscribe;

        private TcpClient? _tcpClient;
        private StreamReader? _reader;
        private StreamWriter? _writer;
        private CancellationTokenSource? _cts;
        private Task? _readLoopTask;

        /// <summary>プロパティ名 -&gt; 最新の文字列値。InfoPanel 側は毎フレームここを読む。</summary>
        public ConcurrentDictionary<string, string?> Values { get; } = new();

        /// <summary>接続状態が変わったときに発火 (InfoPanel の接続状態表示などに利用可能)。</summary>
        public event Action<bool>? ConnectionStateChanged;

        public bool IsConnected => _tcpClient?.Connected == true;

        public SimHubPropertyClient(string host, int port, IReadOnlyList<string> propertiesToSubscribe)
        {
            _host = host;
            _port = port;
            _propertiesToSubscribe = propertiesToSubscribe;
        }

        /// <summary>
        /// バックグラウンドで接続・受信ループを開始する。切断時は自動的に再接続を試みる。
        /// 呼び出し側 (InfoPanel プラグインの Initialize) から一度だけ呼べばよい。
        /// </summary>
        public void Start()
        {
            if (_cts != null) return; // already started

            _cts = new CancellationTokenSource();
            _readLoopTask = Task.Run(() => ConnectAndReadLoopAsync(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
            try
            {
                _writer?.WriteLine("disconnect");
                _writer?.Flush();
            }
            catch
            {
                // 切断間際のエラーは無視してよい
            }

            _tcpClient?.Close();
            _readLoopTask?.Wait(TimeSpan.FromSeconds(2));
        }

        private async Task ConnectAndReadLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var client = new TcpClient();
                    await client.ConnectAsync(_host, _port).ConfigureAwait(false);
                    _tcpClient = client;

                    using var stream = client.GetStream();
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true, NewLine = "\n" };
                    _reader = reader;
                    _writer = writer;

                    // サーバーからの挨拶行 ("SimHub Property Server") を1行読み捨てる
                    var greeting = await reader.ReadLineAsync().ConfigureAwait(false);
                    SimHubBridgeLog.Info($"Connected to SimHub Property Server: {greeting}");

                    ConnectionStateChanged?.Invoke(true);

                    foreach (var prop in _propertiesToSubscribe)
                    {
                        await writer.WriteLineAsync($"subscribe {prop}").ConfigureAwait(false);
                    }

                    while (!token.IsCancellationRequested)
                    {
                        var line = await reader.ReadLineAsync().ConfigureAwait(false);
                        if (line == null)
                        {
                            // サーバー側が切断
                            break;
                        }

                        HandleLine(line);
                    }
                }
                catch (Exception ex) when (!token.IsCancellationRequested)
                {
                    SimHubBridgeLog.Warn($"SimHub connection lost/failed: {ex.Message}. Retrying in 3s...");
                }
                finally
                {
                    ConnectionStateChanged?.Invoke(false);
                    _reader = null;
                    _writer = null;
                    _tcpClient = null;
                }

                if (!token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(3), token).ConfigureAwait(false);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// "Property dcp.gd.SpeedKmh double 123.4" のような行をパースして Values に反映する。
        /// 値部分にスペースが含まれる型 (string) を考慮し、先頭3トークンだけ分割し残りを値とする。
        /// </summary>
        private void HandleLine(string line)
        {
            if (!line.StartsWith("Property ", StringComparison.Ordinal))
            {
                // "Available properties:" などの help 出力やその他の行は無視
                return;
            }

            // "Property <name> <type> <value...>"
            var rest = line.Substring("Property ".Length);
            var firstSpace = rest.IndexOf(' ');
            if (firstSpace < 0) return;
            var name = rest.Substring(0, firstSpace);

            var afterName = rest.Substring(firstSpace + 1);
            var secondSpace = afterName.IndexOf(' ');
            if (secondSpace < 0) return;
            // type は現状使っていないが、将来 InfoPanel 側で型別に PluginSensor/PluginText を
            // 出し分けたくなったらここで拾う
            var value = afterName.Substring(secondSpace + 1);

            Values[name] = value == "(null)" ? null : value;
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
        }
    }

    /// <summary>
    /// InfoPanel 本体のログ機構に差し替えやすいよう薄くラップしただけの内部ロガー。
    /// InfoPanel.Plugins 側に Logger/ILogger が提供されている場合はそちらに差し替えること。
    /// </summary>
    internal static class SimHubBridgeLog
    {
        public static void Info(string message) => System.Diagnostics.Debug.WriteLine($"[SimHubBridge] {message}");
        public static void Warn(string message) => System.Diagnostics.Debug.WriteLine($"[SimHubBridge][WARN] {message}");
    }
}
