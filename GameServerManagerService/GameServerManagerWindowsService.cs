using System.ServiceProcess;

namespace GameServerManagerService;

public class GameServerManagerWindowsService : ServiceBase
{
    public static GameServerManagerWindowsService Instance { get; private set; } = null!;
    public GameServerManagerConfiguration? Config { get; private set; }
    public DiscordBotService? Bot { get; private set; }

    public GameServerManagerWindowsService()
    {
        Instance = this;
        ServiceName = "Game Server Manager";
    }

    protected override void OnStart(string[] args)
    {
        try
        {
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
            Config = GameServerManagerConfiguration.Load(configPath);
            if (!Config.Validate(out var errors))
            {
                foreach (var err in errors)
                    Logger.Log($"Config validation error: {err}");
                throw new InvalidOperationException("Configuration validation failed. See log for details.");
            }
            Logger.Log($"Service started. Loaded {Config.Servers.Count} servers.");
            // Auto-restart servers if enabled
            if (Config.AutoRestartServersOnBoot)
            {
                var savePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "last_running_servers.json");
                if (File.Exists(savePath))
                {
                    var names = System.Text.Json.JsonSerializer.Deserialize<List<string>>(File.ReadAllText(savePath)) ?? [];
                    File.Delete(savePath);
                    foreach (var name in names)
                    {
                        var server = Config.Servers.FirstOrDefault(s => s.Name == name);
                        if (server != null)
                        {
                            if (Utility.StartServerProcess(server, out Exception? error))
                            {
                                Logger.Log($"Auto-restarted server: {server.Name}");
                            }
                            else
                            {
                                Logger.Error($"Failed to auto-restart server '{server.Name}': {error?.Message}", error!);
                            }
                        }
                    }
                    
                }
            }
            if (!string.IsNullOrWhiteSpace(Config.DiscordBotToken))
            {
                Bot = new DiscordBotService(Config.DiscordBotToken);
                Bot.Start();
            }
            else
            {
                Logger.Log("Discord bot token not found in config.");
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to load config", ex);
            throw;
        }
    }

    protected override void OnStop()
    {
        // Save running servers if auto-restart is enabled
        if (Config?.AutoRestartServersOnBoot == true && Config.Servers != null)
        {
            var runningServers = Config.Servers
                .Where(s => System.Diagnostics.Process.GetProcessesByName(Path.GetFileNameWithoutExtension(s.ExecutableName)).Length > 0)
                .Select(s => s.Name)
                .ToList();
            var savePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "last_running_servers.json");
            File.WriteAllText(savePath, System.Text.Json.JsonSerializer.Serialize(runningServers));
        }
        Bot?.Stop();
        Logger.Log("Service stopped.");
    }

    public void StartAsConsole()
    {
        try
        {
            OnStart([]);
            Console.WriteLine("Service running as console app. Press Enter to exit...");
            Console.ReadLine();
            OnStop();
        }
        catch (Exception ex)
        {
            Logger.Error("Console mode error", ex);
        }
    }
}