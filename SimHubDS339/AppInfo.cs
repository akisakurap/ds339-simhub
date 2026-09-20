namespace SimHubDS339
{
    /// <summary>
    /// アプリケーションのバージョン情報。
    /// </summary>
    internal static class AppInfo
    {
        /// <summary>
        /// バージョンを "1.0.2" の形式で取得する。値は csproj の Version から決まる。
        /// </summary>
        public static string VersionText
        {
            get
            {
                // アセンブリ情報からバージョンを取得する
                var version = typeof(AppInfo).Assembly.GetName().Version;
                // バージョンが取得できない場合は "?" を返し、取得できた場合は "Major.Minor.Build" 形式で返す
                return version == null
                    ? "?"
                    : $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";
            }
        }
    }
}
