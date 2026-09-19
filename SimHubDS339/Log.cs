using System.Diagnostics;
using System.Text;

namespace SimHubDS339
{
    /// <summary>
    /// コンソール出力をファイル (%LOCALAPPDATA%\SimHubDS339\SimHubDS339.log) にも同時に書き出すログ管理クラス。
    /// </summary>
    internal static class Log
    {
        private static readonly string LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SimHubDS339");

        /// <summary>ログファイルのフルパス</summary>
        public static string LogFilePath { get; } = Path.Combine(LogDirectory, "SimHubDS339.log");

        private static readonly string OldLogFilePath = Path.Combine(LogDirectory, "SimHubDS339.old.log");

        private const long MaxLogSizeBytes = 1024 * 1024; // 1MB

        private static StreamWriter? _fileWriter;

        /// <summary>
        /// ログ機能を初期化し、Console.Out および Console.Error をファイルと元の出力への Tee ライターに切り替える。
        /// トレイモードでのみ呼び出す。
        /// </summary>
        public static void Initialize()
        {
            try
            {
                // 保存先フォルダの作成
                if (!Directory.Exists(LogDirectory))
                {
                    Directory.CreateDirectory(LogDirectory);
                }

                // 起動時に 1MB を超えていたらローテーション (.old.log にリネーム)
                if (File.Exists(LogFilePath))
                {
                    var fileInfo = new FileInfo(LogFilePath);
                    if (fileInfo.Length >= MaxLogSizeBytes)
                    {
                        try
                        {
                            if (File.Exists(OldLogFilePath))
                            {
                                File.Delete(OldLogFilePath);
                            }
                            File.Move(LogFilePath, OldLogFilePath);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Log] ローテーションに失敗しました: {ex.Message}");
                        }
                    }
                }

                // ファイル追記用の StreamWriter を作成 (AutoFlush 有効)
                var fileStream = new FileStream(LogFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                _fileWriter = new StreamWriter(fileStream, Encoding.UTF8) { AutoFlush = true };

                // 元の出力を保持しつつ、ファイルと両方に書き込むスレッドセーフな TeeWriter を設定
                var originalOut = Console.Out;
                var originalError = Console.Error;

                Console.SetOut(TextWriter.Synchronized(new TeeTextWriter(originalOut, _fileWriter)));
                Console.SetError(TextWriter.Synchronized(new TeeTextWriter(originalError, _fileWriter)));

                Console.WriteLine($"=== SimHubDS339 起動ログ: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Log] ログ初期化に失敗しました: {ex.Message}");
            }
        }

        /// <summary>
        /// ログファイルを関連付けられた既定のアプリケーションで開く。
        /// </summary>
        public static void OpenLogFile()
        {
            try
            {
                if (!Directory.Exists(LogDirectory))
                {
                    Directory.CreateDirectory(LogDirectory);
                }
                if (!File.Exists(LogFilePath))
                {
                    File.WriteAllText(LogFilePath, $"=== SimHubDS339 ログ作成: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\r\n", Encoding.UTF8);
                }

                Process.Start(new ProcessStartInfo(LogFilePath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Log] ログファイルを開けませんでした: {ex.Message}");
            }
        }

        /// <summary>
        /// 2 つの TextWriter (元のコンソールとファイル) に出力を分岐するクラス。
        /// </summary>
        private sealed class TeeTextWriter : TextWriter
        {
            private readonly TextWriter _first;
            private readonly TextWriter _second;

            public TeeTextWriter(TextWriter first, TextWriter second)
            {
                _first = first;
                _second = second;
            }

            public override Encoding Encoding => _first.Encoding;

            public override void Write(char value)
            {
                _first.Write(value);
                _second.Write(value);
            }

            public override void Write(string? value)
            {
                _first.Write(value);
                _second.Write(value);
            }

            public override void Write(char[] buffer, int index, int count)
            {
                _first.Write(buffer, index, count);
                _second.Write(buffer, index, count);
            }

            public override void WriteLine()
            {
                _first.WriteLine();
                _second.WriteLine();
            }

            public override void WriteLine(string? value)
            {
                _first.WriteLine(value);
                _second.WriteLine(value);
            }

            public override void Flush()
            {
                _first.Flush();
                _second.Flush();
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    // _first (Console.Out) は破棄せず、ファイル側だけ Flush する
                    _second.Flush();
                }
                base.Dispose(disposing);
            }
        }
    }
}
