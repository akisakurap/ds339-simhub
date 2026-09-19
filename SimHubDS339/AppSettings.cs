using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SimHubDS339
{
    /// <summary>
    /// アプリケーション設定 (%APPDATA%\SimHubDS339\settings.json) の読み込み・保存を管理するクラス。
    /// </summary>
    internal sealed class AppSettings
    {
        private static readonly string SettingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SimHubDS339");

        private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "settings.json");

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        private int _fps = 10;
        private int _simHubPort = 18082;
        private string _simHubHost = "127.0.0.1";
        private string _nextPageHotkey = HotkeyBinding.DefaultNext;
        private string _prevPageHotkey = HotkeyBinding.DefaultPrev;
        private List<string> _enabledPages = new() { "Main", "Tyres", "Fuel", "Delta", "Session" };
        private string _currentPage = "Main";

        /// <summary>送信レート (FPS, 1..20)</summary>
        [JsonPropertyName("fps")]
        public int Fps
        {
            get => _fps;
            set => _fps = Math.Clamp(value, 1, 20);
        }

        /// <summary>SimHub Property Server のホスト名または IP アドレス</summary>
        [JsonPropertyName("simHubHost")]
        public string SimHubHost
        {
            get => _simHubHost;
            set => _simHubHost = string.IsNullOrWhiteSpace(value) ? "127.0.0.1" : value.Trim();
        }

        /// <summary>SimHub Property Server のポート番号 (1..65535)</summary>
        [JsonPropertyName("simHubPort")]
        public int SimHubPort
        {
            get => _simHubPort;
            set => _simHubPort = Math.Clamp(value, 1, 65535);
        }

        /// <summary>次のページ切り替えホットキー文字列 (例: "Ctrl+Alt+Shift+PageDown")</summary>
        [JsonPropertyName("nextPageHotkey")]
        public string NextPageHotkey
        {
            get => _nextPageHotkey;
            set => _nextPageHotkey = string.IsNullOrWhiteSpace(value) ? HotkeyBinding.DefaultNext : value.Trim();
        }

        /// <summary>前のページ切り替えホットキー文字列 (例: "Ctrl+Alt+Shift+PageUp")</summary>
        [JsonPropertyName("prevPageHotkey")]
        public string PrevPageHotkey
        {
            get => _prevPageHotkey;
            set => _prevPageHotkey = string.IsNullOrWhiteSpace(value) ? HotkeyBinding.DefaultPrev : value.Trim();
        }

        /// <summary>有効なレース画面ページのリスト (Main は常に有効)</summary>
        [JsonPropertyName("enabledPages")]
        public List<string> EnabledPages
        {
            get => _enabledPages;
            set
            {
                var list = value?.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList() ?? new List<string>();
                if (!list.Contains("Main", StringComparer.OrdinalIgnoreCase))
                {
                    list.Insert(0, "Main");
                }
                _enabledPages = list;
            }
        }

        /// <summary>現在選択されているレース画面ページ名</summary>
        [JsonPropertyName("currentPage")]
        public string CurrentPage
        {
            get => _currentPage;
            set => _currentPage = string.IsNullOrWhiteSpace(value) ? "Main" : value.Trim();
        }

        /// <summary>
        /// 有効な RacePage 列挙値のリストを取得する。
        /// </summary>
        public List<RacePage> GetEnabledRacePages()
        {
            var result = new HashSet<RacePage> { RacePage.Main };
            foreach (var name in _enabledPages)
            {
                if (Enum.TryParse<RacePage>(name, true, out var p))
                {
                    result.Add(p);
                }
            }
            return Enum.GetValues(typeof(RacePage))
                .Cast<RacePage>()
                .Where(result.Contains)
                .ToList();
        }

        /// <summary>
        /// 現在保存されている RacePage 列挙値を取得する。
        /// </summary>
        public RacePage GetCurrentRacePage()
        {
            if (Enum.TryParse<RacePage>(_currentPage, true, out var p))
            {
                return p;
            }
            return RacePage.Main;
        }

        /// <summary>
        /// 設定ファイルから設定を読み込む。
        /// ファイルが存在しない場合や破損している場合は既定値のインスタンスを返す。
        /// </summary>
        public static AppSettings Load()
        {
            if (!File.Exists(SettingsFilePath))
            {
                return new AppSettings();
            }

            try
            {
                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                return settings ?? new AppSettings();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Settings] 設定ファイルの読み込みに失敗しました（既定値を使用します）: {ex.Message}");
                return new AppSettings();
            }
        }

        /// <summary>
        /// 現在の設定を %APPDATA%\SimHubDS339\settings.json に JSON 形式で保存する。
        /// </summary>
        public void Save()
        {
            try
            {
                if (!Directory.Exists(SettingsDirectory))
                {
                    Directory.CreateDirectory(SettingsDirectory);
                }

                var json = JsonSerializer.Serialize(this, JsonOptions);
                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Settings] 設定ファイルの保存に失敗しました: {ex.Message}");
                throw;
            }
        }
    }
}
