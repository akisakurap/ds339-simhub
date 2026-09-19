using System.ComponentModel;
using System.Diagnostics;
using System.Security;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;

namespace SimHubDS339
{
    /// <summary>
    /// 自動起動モードの列挙体
    /// </summary>
    internal enum AutoStartMode
    {
        /// <summary>自動起動しない</summary>
        None,
        /// <summary>通常権限で自動起動 (HKCU\Run)</summary>
        User,
        /// <summary>管理者権限で自動起動 (タスク スケジューラ)</summary>
        Admin
    }

    /// <summary>
    /// Windows 起動時の自動起動 (レジストリ Run またはタスク スケジューラ) を管理するクラス。
    /// </summary>
    internal static class AutoStart
    {
        private const string TaskName = "SimHubDS339";
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "SimHubDS339";

        /// <summary>
        /// 現在の実行可能ファイルの絶対パスを取得する。
        /// </summary>
        private static string GetExePath()
        {
            var path = Environment.ProcessPath;
            if (string.IsNullOrEmpty(path))
            {
                using var proc = Process.GetCurrentProcess();
                path = proc.MainModule?.FileName;
            }

            if (string.IsNullOrEmpty(path))
            {
                throw new InvalidOperationException("実行可能ファイルのパスを取得できませんでした。");
            }

            return Path.GetFullPath(path);
        }

        /// <summary>
        /// 現在の自動起動モードを取得する。
        /// タスク スケジューラに登録があれば Admin、HKCU Run にあれば User、どちらもなければ None を返す。
        /// </summary>
        public static AutoStartMode GetMode()
        {
            if (IsTaskScheduled())
            {
                return AutoStartMode.Admin;
            }

            if (IsUserRunRegistered())
            {
                return AutoStartMode.User;
            }

            return AutoStartMode.None;
        }

        /// <summary>
        /// 自動起動モードを設定する。指定したモード以外の登録は解除される。
        /// </summary>
        /// <param name="mode">設定する自動起動モード</param>
        public static void SetMode(AutoStartMode mode)
        {
            switch (mode)
            {
                // UAC でキャンセルされうるタスク スケジューラ操作を先に行い、
                // キャンセル時に既存の登録だけが消えた中途半端な状態にならないようにする
                case AutoStartMode.None:
                    RemoveTaskSchedule();
                    RemoveUserRun();
                    break;

                case AutoStartMode.User:
                    RemoveTaskSchedule();
                    SetUserRun();
                    break;

                case AutoStartMode.Admin:
                    SetTaskSchedule();
                    RemoveUserRun();
                    break;
            }
        }

        /// <summary>
        /// タスク スケジューラに SimHubDS339 が登録されているか確認する。
        /// </summary>
        private static bool IsTaskScheduled()
        {
            try
            {
                var psi = new ProcessStartInfo("schtasks.exe", $"/Query /TN \"{TaskName}\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using var proc = Process.Start(psi);
                if (proc == null) return false;
                proc.WaitForExit();
                return proc.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// HKCU Run に SimHubDS339 が登録されているか確認する。
        /// </summary>
        private static bool IsUserRunRegistered()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
                return key?.GetValue(RunValueName) != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// HKCU Run に実行ファイルのパスを登録する。
        /// </summary>
        private static void SetUserRun()
        {
            string exePath = GetExePath();
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                ?? throw new InvalidOperationException("レジストリ Run キーを開けませんでした。");

            key.SetValue(RunValueName, $"\"{exePath}\"");
        }

        /// <summary>
        /// HKCU Run から登録を削除する。
        /// </summary>
        private static void RemoveUserRun()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
                if (key?.GetValue(RunValueName) != null)
                {
                    key.DeleteValue(RunValueName, throwOnMissingValue: false);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AutoStart] HKCU Run の削除に失敗しました: {ex.Message}");
            }
        }

        /// <summary>
        /// タスク スケジューラに管理者権限での自動起動タスクを登録する。
        /// </summary>
        private static void SetTaskSchedule()
        {
            string exePath = GetExePath();
            string workingDir = Path.GetDirectoryName(exePath) ?? "";
            string userId = WindowsIdentity.GetCurrent().Name;

            // XML 文字列の構築 (UTF-16 で保存する)
            string escapedUserId = SecurityElement.Escape(userId) ?? userId;
            string escapedExePath = SecurityElement.Escape(exePath) ?? exePath;
            string escapedWorkingDir = SecurityElement.Escape(workingDir) ?? workingDir;

            string taskXml = $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Description>SimHubDS339 Auto Start</Description>
  </RegistrationInfo>
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
      <UserId>{escapedUserId}</UserId>
      <Delay>PT10S</Delay>
    </LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id=""Author"">
      <UserId>{escapedUserId}</UserId>
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>false</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <IdleSettings>
      <StopOnIdleEnd>true</StopOnIdleEnd>
      <RestartOnIdle>false</RestartOnIdle>
    </IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <WakeToRun>false</WakeToRun>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>7</Priority>
  </Settings>
  <Actions Context=""Author"">
    <Exec>
      <Command>{escapedExePath}</Command>
      <WorkingDirectory>{escapedWorkingDir}</WorkingDirectory>
    </Exec>
  </Actions>
</Task>";

            string tempXmlPath = Path.Combine(Path.GetTempPath(), $"SimHubDS339_Task_{Guid.NewGuid():N}.xml");
            try
            {
                // schtasks は UTF-16 (Unicode) の XML を要求する
                File.WriteAllText(tempXmlPath, taskXml, Encoding.Unicode);
                RunSchtasksElevated($"/Create /TN \"{TaskName}\" /XML \"{tempXmlPath}\" /F");
            }
            finally
            {
                if (File.Exists(tempXmlPath))
                {
                    try { File.Delete(tempXmlPath); } catch { }
                }
            }
        }

        /// <summary>
        /// タスク スケジューラから SimHubDS339 タスクを削除する。
        /// </summary>
        private static void RemoveTaskSchedule()
        {
            if (!IsTaskScheduled())
            {
                return;
            }

            RunSchtasksElevated($"/Delete /TN \"{TaskName}\" /F");
        }

        /// <summary>
        /// schtasks.exe を管理者権限で実行する (自プロセスが管理者でない場合は UAC 昇格を要求)。
        /// </summary>
        private static void RunSchtasksElevated(string arguments)
        {
            if (PcStatsSampler.IsElevated)
            {
                // 自プロセスがすでに管理者権限の場合
                var psi = new ProcessStartInfo("schtasks.exe", arguments)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using var proc = Process.Start(psi)
                    ?? throw new InvalidOperationException("schtasks.exe の起動に失敗しました。");

                string error = proc.StandardError.ReadToEnd();
                proc.WaitForExit();

                if (proc.ExitCode != 0)
                {
                    throw new InvalidOperationException($"タスク スケジューラの操作に失敗しました (ExitCode: {proc.ExitCode}): {error}");
                }
            }
            else
            {
                // 管理者権限へ昇格して実行 (UAC ダイアログが表示される)
                var psi = new ProcessStartInfo("schtasks.exe", arguments)
                {
                    Verb = "runas",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                };

                try
                {
                    using var proc = Process.Start(psi)
                        ?? throw new InvalidOperationException("schtasks.exe の起動に失敗しました。");

                    proc.WaitForExit();

                    if (proc.ExitCode != 0)
                    {
                        throw new InvalidOperationException($"タスク スケジューラの操作に失敗しました (ExitCode: {proc.ExitCode})");
                    }
                }
                catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
                {
                    // ERROR_CANCELLED: ユーザーが UAC 昇格をキャンセルした場合
                    throw new OperationCanceledException("管理者権限の確認がキャンセルされました。", ex);
                }
            }
        }
    }
}
