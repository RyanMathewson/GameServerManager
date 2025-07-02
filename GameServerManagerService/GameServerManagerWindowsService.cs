using System.ServiceProcess;

namespace GameServerManagerService;

public class GameServerManagerWindowsService : ServiceBase
{
    public static GameServerManagerWindowsService Instance { get; private set; } = null!;
    public GameServerManagerConfiguration? Config { get; private set; }
    public DiscordBotService? Bot { get; private set; }

    private readonly HashSet<string> _startedServers = new();

    public GameServerManagerWindowsService()
    {
        Instance = this;
        ServiceName = "Game Server Manager";
    }

    public void MarkServerStarted(string name)
    {
        lock (_startedServers)
        {
            Logger.Log($"Marking server '{name}' as started.");
            _startedServers.Add(name);
            SaveStartedServers();
        }
    }

    public void MarkServerStopped(string name)
    {
        lock (_startedServers)
        {
            Logger.Log($"Marking server '{name}' as stopped.");
            _startedServers.Remove(name);
            SaveStartedServers();
        }
    }

    private void SaveStartedServers()
    {
        var savePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "last_running_servers.json");
        Logger.Log($"Saving started servers '{string.Join(", ", _startedServers)}' to disk at '{savePath}'.");
        File.WriteAllText(savePath, System.Text.Json.JsonSerializer.Serialize(_startedServers.ToList()));
    }

    private void LoadStartedServers()
    {
        var savePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "last_running_servers.json");
        Logger.Log($"Loading started servers from disk at '{savePath}'.");
        if (File.Exists(savePath))
        {
            var names = System.Text.Json.JsonSerializer.Deserialize<List<string>>(File.ReadAllText(savePath)) ?? [];
            _startedServers.Clear();
            foreach (var n in names) _startedServers.Add(n);
        }
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

            if (Config.AutoRestartServersOnBoot)
            {
                LoadStartedServers();
                foreach (var name in _startedServers)
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

            // Scan for currently running servers and mark them as started
            foreach (var server in Config.Servers)
            {
                var exeName = Path.GetFileNameWithoutExtension(server.ExecutableName);
                var processes = System.Diagnostics.Process.GetProcessesByName(exeName);
                if (processes.Length > 0)
                {
                    MarkServerStarted(server.Name);
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
        SaveStartedServers();
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