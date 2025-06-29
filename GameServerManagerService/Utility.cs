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
}
