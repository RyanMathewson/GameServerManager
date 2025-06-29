namespace GameServerManagerService;

public static class Logger
{
    private static readonly object _lock = new();
    private static readonly string LogDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logging");
    private static readonly string LogFilePath = Path.Combine(LogDirectory, "GameServerManagerService.log");

    public static void Log(string message)
    {
        string formatted = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
        lock (_lock)
        {
            if (!Directory.Exists(LogDirectory))
            {
                Directory.CreateDirectory(LogDirectory);
            }
            File.AppendAllText(LogFilePath, formatted + "\n");
        }
        Console.WriteLine(formatted);
    }

    public static void Error(string message, Exception ex)
    {
        Log($"{message}\n{ex}");
    }
}
