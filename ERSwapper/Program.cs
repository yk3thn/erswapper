namespace ERSwapper;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        AppPaths.ClearTemp();

        Application.ThreadException += (_, e) => ShowFatal(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => ShowFatal(e.ExceptionObject as Exception);

        try
        {
            UpdateInstaller.Cleanup();

            AppSettings.Load().ApplyConfigFolder();

            if (CheckForUpdate()) return;

            StartupResult? startup;

            using (var loading = new LoadingForm())
            {
                if (loading.ShowDialog() != DialogResult.OK || loading.Result is null) return;
                startup = loading.Result;
            }

            Application.Run(new MainForm(startup));
        }
        catch (Exception ex)
        {
            ShowFatal(ex);
        }
    }

    private static bool CheckForUpdate()
    {
        if (!UpdateSettings.CheckOnStartup) return false;

        return UpdateFlow.Run(owner: null, honourSkip: true, out _) == UpdateOutcome.Restarting;
    }

    private static string CrashLogPath => Path.Combine(AppPaths.UserDataDirectory, "crash.log");

    private static void ShowFatal(Exception? ex)
    {
        string detail = ex?.ToString() ?? "Unknown error.";

        try
        {
            File.AppendAllText(CrashLogPath,
                $"---- {DateTime.Now:u} ----{Environment.NewLine}{detail}{Environment.NewLine}{Environment.NewLine}");
        }
        catch { }

        MessageBox.Show(
            "Something went wrong:\r\n\r\n" + (ex?.Message ?? "Unknown error.") +
            "\r\n\r\nDetails were written to:\r\n" + CrashLogPath,
            "ER Swapper",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}
