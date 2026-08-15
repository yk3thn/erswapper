namespace ERSwapper;

public enum UpdateOutcome
{
    NotConfigured,
    CheckFailed,
    UpToDate,
    Skipped,
    Declined,
    Restarting,
}

public static class UpdateFlow
{
    public static UpdateOutcome Run(IWin32Window? owner, bool honourSkip, out string? version)
    {
        version = null;

        var checker = new UpdateChecker(UpdateSettings.RepositoryOwner, UpdateSettings.RepositoryName);
        if (!checker.IsConfigured) return UpdateOutcome.NotConfigured;

        AppSettings settings = AppSettings.Load();
        UpdateInfo? info;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            info = checker.CheckAsync(AppVersion.Current, cts.Token).GetAwaiter().GetResult();
        }
        catch
        {
            return UpdateOutcome.CheckFailed;
        }

        if (info is null) return UpdateOutcome.UpToDate;

        version = info.TagName;

        if (honourSkip
            && string.Equals(settings.SkippedUpdateVersion, info.TagName, StringComparison.OrdinalIgnoreCase))
        {
            return UpdateOutcome.Skipped;
        }

        using var prompt = new UpdatePromptForm(info);

        if (owner is null) prompt.ShowDialog();
        else prompt.ShowDialog(owner);

        if (prompt.Choice == UpdateChoice.Skip)
        {
            settings.SkippedUpdateVersion = info.TagName;
            TrySave(settings);

            return UpdateOutcome.Skipped;
        }

        if (prompt.Choice == UpdateChoice.Restarting) return UpdateOutcome.Restarting;

        return UpdateOutcome.Declined;
    }

    public static void ForgetSkippedVersion()
    {
        AppSettings settings = AppSettings.Load();

        if (string.IsNullOrWhiteSpace(settings.SkippedUpdateVersion)) return;

        settings.SkippedUpdateVersion = "";
        TrySave(settings);
    }

    private static void TrySave(AppSettings settings)
    {
        try { settings.Save(); } catch { }
    }
}
