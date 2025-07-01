namespace GameServerManagerService;

public static class Utility
{
    public static string FormatBytes(long bytes)
    {
        if (bytes > 1024 * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        if (bytes > 1024 * 1024)
            return $"{bytes / (1024.0 * 1024):F2} MB";
        if (bytes > 1024)
            return $"{bytes / 1024.0:F2} KB";
        return $"{bytes} B";
    }

    public static bool StartServerProcess(GameServerConfig server, out Exception? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(server.StartCommand))
        {
            error = new ArgumentException($"Start command not configured for server '{server.Name}'");
            return false;
        }
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/C {server.StartCommand}",
                WorkingDirectory = string.IsNullOrWhiteSpace(server.InstallLocation) ? null : server.InstallLocation,
                CreateNoWindow = true,
                UseShellExecute = false
            };
            System.Diagnostics.Process.Start(startInfo);
            return true;
        }
        catch (Exception ex)
        {
            error = ex;
            return false;
        }
    }
}
