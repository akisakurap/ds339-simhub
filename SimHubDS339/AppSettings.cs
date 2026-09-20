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
        // 次のページ切り替え用のコントローラー設定文字列 (例: "044F:B66F:B25")
        private string _nextPageJoy = "";
        // 前のページ切り替え用のコントローラー設定文字列 (例: "044F:B66F:B24")
        private string _prevPageJoy = "";

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

        /// <summary>次のページ切り替えに割り当てたコントローラーのボタン (例: "044F:B66F:B25"、空 = 未割当)</summary>
        [JsonPropertyName("nextPageJoy")]
        public string NextPageJoy
        {
            get => _nextPageJoy;
            set => _nextPageJoy = value?.Trim() ?? "";
        }

        /// <summary>前のページ切り替えに割り当てたコントローラーのボタン (例: "044F:B66F:B24"、空 = 未割当)</summary>
        [JsonPropertyName("prevPageJoy")]
        public string PrevPageJoy
        {
            get => _prevPageJoy;
            set => _prevPageJoy = value?.Trim() ?? "";
        }

        /// <summary>「次のページ」のコントローラー割り当てを取得する (未割当・不正な値は null)。</summary>
        public JoyBinding? GetNextPageJoyBinding() => JoyBinding.TryParse(_nextPageJoy, out var binding) ? binding : null;

        /// <summary>「前のページ」のコントローラー割り当てを取得する (未割当・不正な値は null)。</summary>
        public JoyBinding? GetPrevPageJoyBinding() => JoyBinding.TryParse(_prevPageJoy, out var binding) ? binding : null;

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
        public static AppSettings Load() => Load(SettingsFilePath);

        /// <summary>
        /// 指定したパスの設定ファイルから設定を読み込む (テスト用に保存先を指定できる)。
        /// 旧既定のホットキー (4 キー同時押し) は新しい既定値へ移行する。
        /// </summary>
        internal static AppSettings Load(string path)
        {
            // ファイルが存在しない場合は初期既定値のインスタンスを返す
            if (!File.Exists(path))
            {
                return new AppSettings();
            }

            try
            {
                var json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
                // 旧既定値が設定されている場合は新既定値へ移行
                settings.MigrateLegacyHotkeys();
                return settings;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Settings] 設定ファイルの読み込みに失敗しました（既定値を使用します）: {ex.Message}");
                return new AppSettings();
            }
        }

        /// <summary>
        /// 旧既定値 (Ctrl+Alt+Shift+PageDown / PageUp) と完全一致するホットキーだけを新既定値に置き換える。
        /// 新既定値が反対側のキーと衝突する場合は移行しない。
        /// </summary>
        private void MigrateLegacyHotkeys()
        {
            // 現在の設定値が旧既定値と一致するか判定
            bool nextIsLegacy = string.Equals(_nextPageHotkey, HotkeyBinding.LegacyDefaultNext, StringComparison.OrdinalIgnoreCase);
            bool prevIsLegacy = string.Equals(_prevPageHotkey, HotkeyBinding.LegacyDefaultPrev, StringComparison.OrdinalIgnoreCase);

            // 次ページが旧既定値で、前ページの新既定値と衝突しない場合は新既定値へ更新
            if (nextIsLegacy && !string.Equals(_prevPageHotkey, HotkeyBinding.DefaultNext, StringComparison.OrdinalIgnoreCase))
            {
                _nextPageHotkey = HotkeyBinding.DefaultNext;
            }

            // 前ページが旧既定値で、次ページの新既定値と衝突しない場合は新既定値へ更新
            if (prevIsLegacy && !string.Equals(_nextPageHotkey, HotkeyBinding.DefaultPrev, StringComparison.OrdinalIgnoreCase))
            {
                _prevPageHotkey = HotkeyBinding.DefaultPrev;
            }
        }

        /// <summary>
        /// 現在の設定を %APPDATA%\SimHubDS339\settings.json に JSON 形式で保存する。
        /// </summary>
        public void Save() => Save(SettingsFilePath);

        /// <summary>
        /// 現在の設定を指定したパスに JSON 形式で保存する (テスト用に保存先を指定できる)。
        /// </summary>
        internal void Save(string path)
        {
            try
            {
                // 保存先ディレクトリが存在しない場合は作成する
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // JSON シリアライズして指定パスに書き出す
                var json = JsonSerializer.Serialize(this, JsonOptions);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Settings] 設定ファイルの保存に失敗しました: {ex.Message}");
                throw;
            }
        }
    }
}
