using System.ServiceProcess;
using System.Text.Json;
using Discord;
using Discord.WebSocket;

namespace GameServerManagerService;

public class SteamManagerService : ServiceBase
{
    public static SteamManagerService Instance { get; private set; } = null!;
    public SteamManagerConfiguration? Config { get; private set; }
    public DiscordBotService? Bot { get; private set; }

    public SteamManagerService()
    {
        Instance = this;
        ServiceName = "Steam Game Server Manager";
    }

    // This method is called when the service is started
    protected override void OnStart(string[] args)
    {
        try
        {
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
            Config = SteamManagerConfiguration.Load(configPath);
            if (!Config.Validate(out var errors))
            {
                foreach (var err in errors)
                    Logger.Log($"Config validation error: {err}");
                throw new InvalidOperationException("Configuration validation failed. See log for details.");
            }
            Logger.Log($"Service started. Loaded {Config.Servers.Count} servers.");
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

    // This method is called when the service is stopped
    protected override void OnStop()
    {
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

// Define the program entry point
internal static class Program
{
    private static void Main(string[] args)
    {
        if (args.Contains("--console"))
        {
            var service = new SteamManagerService();
            service.StartAsConsole();
        }
        else
        {
            ServiceBase.Run(new SteamManagerService());
        }
    }
}

public class GameServerConfig
{
    public string Name { get; set; } = string.Empty;
    public string InstallLocation { get; set; } = string.Empty;
    public string SaveDirectory { get; set; } = string.Empty;
    public string StartCommand { get; set; } = string.Empty;
    public string UpdateCommand { get; set; } = string.Empty;
    public string ExecutableName { get; set; } = string.Empty;
}

public class SteamManagerConfiguration
{
    public List<GameServerConfig> Servers { get; set; } = new();
    public string BackupLocation { get; set; } = string.Empty;
    public string DiscordBotToken { get; set; } = string.Empty;

    public static SteamManagerConfiguration Load(string path)
    {
        Logger.Log($"Attempting to load config from: {Path.GetFullPath(path)}");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Configuration file not found: {path}");
        }
        var json = File.ReadAllText(path);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var config = JsonSerializer.Deserialize<SteamManagerConfiguration>(json, options) ?? new SteamManagerConfiguration();
        Logger.Log($"Config loaded. Servers: {config.Servers.Count}");
        return config;
    }

    public bool Validate(out List<string> errors)
    {
        errors = new List<string>();
        if (Servers == null || Servers.Count == 0)
            errors.Add("No servers are configured.");
        if (string.IsNullOrWhiteSpace(BackupLocation))
            errors.Add("BackupLocation is not set.");
        else if (!System.IO.Directory.Exists(BackupLocation))
            errors.Add($"BackupLocation '{BackupLocation}' does not exist.");
        if (string.IsNullOrWhiteSpace(DiscordBotToken))
            errors.Add("DiscordBotToken is not set.");
        if (Servers != null)
        {
            var nameSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var server in Servers)
            {
                if (string.IsNullOrWhiteSpace(server.Name))
                    errors.Add("A server is missing a Name.");
                else if (!nameSet.Add(server.Name))
                    errors.Add($"Duplicate server name found: '{server.Name}'");
                if (string.IsNullOrWhiteSpace(server.InstallLocation))
                    errors.Add($"Server '{server.Name}' is missing InstallLocation.");
                if (string.IsNullOrWhiteSpace(server.SaveDirectory))
                    errors.Add($"Server '{server.Name}' is missing SaveDirectory.");
                else if (!System.IO.Directory.Exists(server.SaveDirectory))
                    errors.Add($"SaveDirectory '{server.SaveDirectory}' for server '{server.Name}' does not exist.");
                if (string.IsNullOrWhiteSpace(server.ExecutableName))
                    errors.Add($"Server '{server.Name}' is missing ExecutableName.");
                if (string.IsNullOrWhiteSpace(server.StartCommand))
                    errors.Add($"Server '{server.Name}' is missing StartCommand.");
                if (string.IsNullOrWhiteSpace(server.UpdateCommand))
                    errors.Add($"Server '{server.Name}' is missing UpdateCommand.");
            }
        }
        return errors.Count == 0;
    }
}

public static class Logger
{
    private static readonly object _lock = new();
    private static readonly string LogDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logging");
    private static readonly string LogFilePath = Path.Combine(LogDirectory, "SteamManagerService.log");

    public static void Log(string message)
    {
        string formatted = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
        lock (_lock)
        {
            if (!Directory.Exists(LogDirectory))
                Directory.CreateDirectory(LogDirectory);
            System.IO.File.AppendAllText(LogFilePath, formatted + "\n");
        }
        Console.WriteLine(formatted);
    }

    public static void Error(string message, Exception ex)
    {
        Log($"{message}\n{ex}");
    }
}

public class DiscordBotService
{
    // Command name constants
    private const string CommandStatus = "status";
    private const string CommandStop = "stop";
    private const string CommandStart = "start";
    private const string CommandBackup = "backup";
    private const string CommandUpdate = "update";

    private readonly string _token;
    private DiscordSocketClient? _client;
    private CancellationTokenSource? _cts;

    public DiscordBotService(string token)
    {
        _token = token;
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        Task.Run(() => RunAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _client?.LogoutAsync();
        _client?.StopAsync();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var config = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent
        };
        _client = new DiscordSocketClient(config);
        _client.Log += msg => { Logger.Log($"[Discord] {msg}"); return Task.CompletedTask; };
        _client.MessageReceived += HandleMessageAsync;
        try
        {
            await _client.LoginAsync(TokenType.Bot, _token);
            await _client.StartAsync();
            Logger.Log("Discord bot started.");
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(1000, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Discord bot error", ex);
        }
        finally
        {
            await _client.LogoutAsync();
            await _client.StopAsync();
        }
    }

    private async Task HandleMessageAsync(SocketMessage message)
    {
        try
        {
            Logger.Log($"Received message: Author={message.Author.Username}, Content='{message.Content}', Channel={message.Channel.Name}");
            if (message.Author.IsBot) return;
            var config = SteamManagerService.Instance.Config!;
            var bot = SteamManagerService.Instance.Bot!;
            if (!message.Content.StartsWith("!sm", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            var parts = message.Content.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            var command = parts.Length > 1 ? parts[1].ToLowerInvariant() : string.Empty;
            string serverName = parts.Length > 2 ? parts[2] : string.Empty;
            GameServerConfig? server = null;
            if (!string.IsNullOrWhiteSpace(serverName) && config != null)
            {
                server = config.Servers.FirstOrDefault(s => s.Name.Equals(serverName, StringComparison.OrdinalIgnoreCase));
            }

            // For commands that require a server, warn if not found
            bool requiresServer = command is CommandStop or CommandStart or CommandBackup or CommandUpdate;
            if (requiresServer && (!string.IsNullOrWhiteSpace(serverName) && server == null))
            {
                await SendErrorAsync(message.Channel, $"No server found with name '{serverName}'.");
                return;
            }
            if (config == null || bot == null)
            {
                await SendErrorAsync(message.Channel, "Configuration or bot service is not available.");
                return;
            }

            switch (command)
            {
                case CommandStatus:
                    await HandleStatusCommand(message, config);
                    break;
                case CommandStop:
                    await HandleStopCommand(message, config, bot, server!);
                    break;
                case CommandStart:
                    await HandleStartCommand(message, config, bot, server!);
                    break;
                case CommandBackup:
                    await HandleBackupCommand(message, config, bot, server!);
                    break;
                case CommandUpdate:
                    await HandleUpdateCommand(message, config, bot, server!);
                    break;
                default:
                    await message.Channel.SendMessageAsync($"Unknown command: {command}\nAvailable commands: {CommandStatus}, {CommandStop}, {CommandStart}, {CommandBackup}, {CommandUpdate}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Error handling message: '{message.Content}'", ex);
        }
    }

    private async Task HandleStatusCommand(SocketMessage message, SteamManagerConfiguration config)
    {
        // Sample CPU usage for each process over 500ms
        var serverInfos = config.Servers.Select(s =>
        {
            var exeName = System.IO.Path.GetFileNameWithoutExtension(s.ExecutableName);
            var processes = System.Diagnostics.Process.GetProcessesByName(exeName);
            var isRunning = processes.Any();
            long totalRam = 0;
            var cpuTimes = new List<(int pid, TimeSpan startTime)>();
            foreach (var proc in processes)
            {
                try
                {
                    totalRam += proc.WorkingSet64;
                    cpuTimes.Add((proc.Id, proc.TotalProcessorTime));
                }
                catch { }
            }
            return (s.Name, isRunning, totalRam, processes, cpuTimes);
        }).ToList();

        // Wait 500ms to sample CPU usage
        await Task.Delay(500);

        var statusList = serverInfos.Select(info =>
        {
            double cpuPercent = 0;
            foreach (var proc in info.processes)
            {
                try
                {
                    var oldCpu = info.cpuTimes.FirstOrDefault(x => x.pid == proc.Id).startTime;
                    var newCpu = proc.TotalProcessorTime;
                    var cpuDelta = (newCpu - oldCpu).TotalMilliseconds;
                    // Environment.ProcessorCount for total logical CPUs
                    cpuPercent += cpuDelta / 500.0 / Environment.ProcessorCount * 100.0;
                }
                catch { }
            }
            string ramInfo = info.isRunning ? $" | RAM: {FormatBytes(info.totalRam)}" : string.Empty;
            string cpuInfo = info.isRunning ? $" | CPU: {cpuPercent:F1}%" : string.Empty;
            return $"- {info.Name}: {(info.isRunning ? "running" : "stopped")}{ramInfo}{cpuInfo}";
        });
        await message.Channel.SendMessageAsync($"Server status:\n{string.Join("\n", statusList)}");
    }

    private string FormatBytes(long bytes)
    {
        if (bytes > 1024 * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        if (bytes > 1024 * 1024)
            return $"{bytes / (1024.0 * 1024):F2} MB";
        if (bytes > 1024)
            return $"{bytes / 1024.0:F2} KB";
        return $"{bytes} B";
    }

    private async Task SendErrorAsync(ISocketMessageChannel channel, string userMessage, Exception? ex = null)
    {
        await channel.SendMessageAsync($":x: {userMessage}");
        if (ex != null)
            Logger.Error($"Error: {userMessage}", ex);
        else
            Logger.Log($"Error: {userMessage}");
    }

    private async Task HandleStopCommand(SocketMessage message, SteamManagerConfiguration config, DiscordBotService bot, GameServerConfig server)
    {
        await bot.StopServer(server, message.Channel);
    }

    private async Task HandleStartCommand(SocketMessage message, SteamManagerConfiguration config, DiscordBotService bot, GameServerConfig server)
    {
        await bot.StartServer(server, message.Channel);
    }

    private async Task HandleBackupCommand(SocketMessage message, SteamManagerConfiguration config, DiscordBotService bot, GameServerConfig server)
    {
        var wasRunning = await bot.StopServer(server, message.Channel);
        bool backupSuccess = await bot.BackupServer(server, config.BackupLocation, message.Channel);
        if (wasRunning && backupSuccess)
        {
            await bot.StartServer(server, message.Channel);
        }
    }

    private async Task HandleUpdateCommand(SocketMessage message, SteamManagerConfiguration config, DiscordBotService bot, GameServerConfig server)
    {
        var wasRunning = await bot.StopServer(server, message.Channel);
        bool backupSuccess = await bot.BackupServer(server, config.BackupLocation, message.Channel, "before update");
        if (!backupSuccess)
        {
            await SendErrorAsync(message.Channel, $"Backup failed. Update aborted for '{server.Name}'.");
            return;
        }
        try
        {
            await message.Channel.SendMessageAsync($"Running update command for '{server.Name}'...");
            var updateInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/C {server.UpdateCommand}",
                WorkingDirectory = string.IsNullOrWhiteSpace(server.InstallLocation) ? null : server.InstallLocation,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            var updateProc = System.Diagnostics.Process.Start(updateInfo);
            if (updateProc != null)
            {
                // Stream output to Discord in real-time
                var stdOutTask = Task.Run(async () =>
                {
                    string? line;
                    while ((line = await updateProc.StandardOutput.ReadLineAsync()) != null)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                            await message.Channel.SendMessageAsync($"> {line}");
                    }
                });
                var stdErrTask = Task.Run(async () =>
                {
                    string? line;
                    while ((line = await updateProc.StandardError.ReadLineAsync()) != null)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                            await message.Channel.SendMessageAsync($"> {line}");
                    }
                });
                await updateProc.WaitForExitAsync();
                await Task.WhenAll(stdOutTask, stdErrTask);
                await message.Channel.SendMessageAsync($"Update command completed for '{server.Name}'.");
            }
            else
            {
                await SendErrorAsync(message.Channel, $"Failed to start update process for '{server.Name}'.");
            }
        }
        catch (Exception ex)
        {
            await SendErrorAsync(message.Channel, $"Failed to update server '{server.Name}'", ex);
            return;
        }
        if (wasRunning)
        {
            await bot.StartServer(server, message.Channel);
        }
    }

    private async Task<bool> StopServer(GameServerConfig server, ISocketMessageChannel channel)
    {
        if (string.IsNullOrWhiteSpace(server.ExecutableName)) return false;
        var exeNameNoExt = System.IO.Path.GetFileNameWithoutExtension(server.ExecutableName);
        var processes = System.Diagnostics.Process.GetProcessesByName(exeNameNoExt);
        if (processes.Length == 0) return false; // Not running
        await channel.SendMessageAsync($"Stopping '{server.Name}' before backup...");
        int stopped = 0;
        foreach (var proc in processes)
        {
            try { proc.Kill(); stopped++; }
            catch (Exception ex)
            {
                await SendErrorAsync(channel, $"Failed to stop process {proc.Id} for '{server.Name}'", ex);
                throw; // Interrupt the operation
            }
        }
        await channel.SendMessageAsync($"Stopped {stopped} process(es) for '{server.Name}'.");
        return true;
    }

    private async Task StartServer(GameServerConfig server, ISocketMessageChannel channel)
    {
        if (string.IsNullOrWhiteSpace(server.StartCommand))
        {
            await channel.SendMessageAsync($"Start command not configured for server '{server.Name}'.");
            return;
        }
        try
        {
            await channel.SendMessageAsync($"Starting '{server.Name}'");
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/C {server.StartCommand}",
                WorkingDirectory = string.IsNullOrWhiteSpace(server.InstallLocation) ? null : server.InstallLocation,
                CreateNoWindow = true,
                UseShellExecute = false
            };
            System.Diagnostics.Process.Start(startInfo);
            await channel.SendMessageAsync($"Start command executed for '{server.Name}'.");
        }
        catch (Exception ex)
        {
            await SendErrorAsync(channel, $"Failed to start '{server.Name}'", ex);
        }
    }

    private async Task<bool> BackupServer(GameServerConfig server, string backupLocation, ISocketMessageChannel channel, string? context = null)
    {
        try
        {
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var backupFileName = $"{server.Name}_backup_{timestamp}.zip";
            var backupFilePath = Path.Combine(backupLocation, backupFileName);
            await channel.SendMessageAsync($"Performing backup for '{server.Name}'{(context != null ? " (" + context + ")" : "")}...");
            System.IO.Compression.ZipFile.CreateFromDirectory(server.SaveDirectory, backupFilePath);
            await channel.SendMessageAsync($"Backup of '{server.Name}' completed successfully. File: {backupFileName}");
            return true;
        }
        catch (Exception ex)
        {
            await SendErrorAsync(channel, $"Failed to backup server '{server.Name}'", ex);
            return false;
        }
    }
}
