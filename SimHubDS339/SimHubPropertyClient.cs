using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimHubDS339
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
    /// </summary>
    public sealed class SimHubPropertyClient : IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private readonly IReadOnlyList<string> _propertiesToSubscribe;

        private TcpClient? _tcpClient;
        private StreamWriter? _writer;
        private CancellationTokenSource? _cts;
        private Task? _readLoopTask;

        public ConcurrentDictionary<string, string?> Values { get; } = new();

        public event Action<bool>? ConnectionStateChanged;

        public bool IsConnected => _tcpClient?.Connected == true;

        public SimHubPropertyClient(string host, int port, IReadOnlyList<string> propertiesToSubscribe)
        {
            _host = host;
            _port = port;
            _propertiesToSubscribe = propertiesToSubscribe;
        }

        public void Start()
        {
            if (_cts != null) return;

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
                    _writer = writer;

                    var greeting = await reader.ReadLineAsync().ConfigureAwait(false);
                    Console.WriteLine($"[SimHubBridge] Connected to SimHub Property Server: {greeting}");

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
                            break;
                        }

                        HandleLine(line);
                    }
                }
                catch (Exception ex) when (!token.IsCancellationRequested)
                {
                    Console.WriteLine($"[SimHubBridge][WARN] SimHub connection lost/failed: {ex.Message}. Retrying in 3s...");
                }
                finally
                {
                    ConnectionStateChanged?.Invoke(false);
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
        /// </summary>
        private void HandleLine(string line)
        {
            if (!line.StartsWith("Property ", StringComparison.Ordinal))
            {
                return;
            }

            var rest = line.Substring("Property ".Length);
            var firstSpace = rest.IndexOf(' ');
            if (firstSpace < 0) return;
            var name = rest.Substring(0, firstSpace);

            var afterName = rest.Substring(firstSpace + 1);
            var secondSpace = afterName.IndexOf(' ');
            if (secondSpace < 0) return;
            var value = afterName.Substring(secondSpace + 1);

            Values[name] = value == "(null)" ? null : value;
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
        }
    }
}
