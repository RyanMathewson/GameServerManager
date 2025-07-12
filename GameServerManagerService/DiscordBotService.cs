using Discord;
using Discord.WebSocket;

namespace GameServerManagerService;

public class DiscordBotService
{
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
            if (message.Author.IsBot) return;
            var config = GameServerManagerWindowsService.Instance.Config!;
            var bot = GameServerManagerWindowsService.Instance.Bot!;
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
            if (requiresServer && !string.IsNullOrWhiteSpace(serverName) && server == null)
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

    private async Task HandleStatusCommand(SocketMessage message, GameServerManagerConfiguration config)
    {
        // Sample CPU usage for each process over 500ms
        var serverInfos = config.Servers.Select(s =>
        {
            var exeName = Path.GetFileNameWithoutExtension(s.ExecutableName);
            var processes = System.Diagnostics.Process.GetProcessesByName(exeName);
            var isRunning = processes.Length != 0;
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

    private static string FormatBytes(long bytes)
    {
        return Utility.FormatBytes(bytes);
    }

    private static async Task SendErrorAsync(ISocketMessageChannel channel, string userMessage, Exception? ex = null)
    {
        await channel.SendMessageAsync($":x: {userMessage}");
        if (ex != null)
            Logger.Error($"Error: {userMessage}", ex);
        else
            Logger.Log($"Error: {userMessage}");
    }

    private static async Task AppendToStatusMessage(IUserMessage statusMessage, string newLine)
    {
        try
        {
            var currentContent = statusMessage.Content;
            var newContent = currentContent + "\n" + newLine;
            
            // Discord message limit is 2000 characters
            if (newContent.Length > 2000)
            {
                // Keep the first line (command info) and recent updates
                var lines = newContent.Split('\n');
                var firstLine = lines[0];
                var recentLines = lines.TakeLast(Math.Min(lines.Length - 1, 15)).ToArray();
                newContent = firstLine + "\n" + string.Join("\n", recentLines);
                
                // If still too long, truncate further
                if (newContent.Length > 2000)
                {
                    newContent = newContent.Substring(0, 1997) + "...";
                }
            }
            
            await statusMessage.ModifyAsync(m => m.Content = newContent);
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to update status message", ex);
        }
    }

    private static async Task HandleStopCommand(SocketMessage message, GameServerManagerConfiguration config, DiscordBotService bot, GameServerConfig server)
    {
        var statusMessage = await message.Channel.SendMessageAsync($"Processing stop command for '{server.Name}'...");
        await StopServer(server, statusMessage);
    }

    private static async Task HandleStartCommand(SocketMessage message, GameServerManagerConfiguration config, DiscordBotService bot, GameServerConfig server)
    {
        var statusMessage = await message.Channel.SendMessageAsync($"Processing start command for '{server.Name}'...");
        await StartServer(server, statusMessage);
    }

    private static async Task HandleBackupCommand(SocketMessage message, GameServerManagerConfiguration config, DiscordBotService bot, GameServerConfig server)
    {
        var statusMessage = await message.Channel.SendMessageAsync($"Processing backup command for '{server.Name}'...");
        var wasRunning = await StopServer(server, statusMessage, "before backup");
        bool backupSuccess = await BackupServer(server, config.BackupLocation, statusMessage);
        if (wasRunning && backupSuccess)
        {
            await StartServer(server, statusMessage, "after backup");
        }
    }

    private static async Task HandleUpdateCommand(SocketMessage message, GameServerManagerConfiguration config, DiscordBotService bot, GameServerConfig server)
    {
        var statusMessage = await message.Channel.SendMessageAsync($"Processing update command for '{server.Name}'...");
        var wasRunning = await StopServer(server, statusMessage, "before update");
        bool backupSuccess = await BackupServer(server, config.BackupLocation, statusMessage, "before update");
        if (!backupSuccess)
        {
            await AppendToStatusMessage(statusMessage, ":x: Backup failed. Update aborted.");
            return;
        }
        try
        {
            await AppendToStatusMessage(statusMessage, $"Running update command for '{server.Name}'...");
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
                var outputBuffer = new List<string>();
                var lastEdit = DateTime.UtcNow;
                var editInterval = TimeSpan.FromSeconds(2);
                var stdOutTask = Task.Run(async () =>
                {
                    string? line;
                    while ((line = await updateProc.StandardOutput.ReadLineAsync()) != null)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            outputBuffer.Add($"> {line}");
                            if ((DateTime.UtcNow - lastEdit) > editInterval)
                            {
                                // For console output, we still replace to keep it manageable
                                var consoleLines = outputBuffer.TakeLast(10);
                                var currentContent = statusMessage.Content;
                                var baseLines = currentContent.Split('\n').TakeWhile(l => !l.StartsWith(">")).ToArray();
                                await statusMessage.ModifyAsync(m => m.Content = string.Join("\n", baseLines) + "\n" + string.Join("\n", consoleLines));
                                lastEdit = DateTime.UtcNow;
                            }
                        }
                    }
                });
                var stdErrTask = Task.Run(async () =>
                {
                    string? line;
                    while ((line = await updateProc.StandardError.ReadLineAsync()) != null)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            outputBuffer.Add($"> {line}");
                            if ((DateTime.UtcNow - lastEdit) > editInterval)
                            {
                                // For console output, we still replace to keep it manageable
                                var consoleLines = outputBuffer.TakeLast(10);
                                var currentContent = statusMessage.Content;
                                var baseLines = currentContent.Split('\n').TakeWhile(l => !l.StartsWith(">")).ToArray();
                                await statusMessage.ModifyAsync(m => m.Content = string.Join("\n", baseLines) + "\n" + string.Join("\n", consoleLines));
                                lastEdit = DateTime.UtcNow;
                            }
                        }
                    }
                });
                await updateProc.WaitForExitAsync();
                await Task.WhenAll(stdOutTask, stdErrTask);
                // Final update for console output
                var finalConsoleLines = outputBuffer.TakeLast(10);
                var finalCurrentContent = statusMessage.Content;
                var finalBaseLines = finalCurrentContent.Split('\n').TakeWhile(l => !l.StartsWith(">")).ToArray();
                await statusMessage.ModifyAsync(m => m.Content = string.Join("\n", finalBaseLines) + "\n" + string.Join("\n", finalConsoleLines));
                // Append completion message
                await AppendToStatusMessage(statusMessage, $"Update command completed for '{server.Name}'.");
            }
            else
            {
                await AppendToStatusMessage(statusMessage, $":x: Failed to start update process for '{server.Name}'.");
                return;
            }
        }
        catch (Exception ex)
        {
            await AppendToStatusMessage(statusMessage, $":x: Failed to update server '{server.Name}': {ex.Message}");
            Logger.Error($"Failed to update server '{server.Name}'", ex);
            return;
        }
        if (wasRunning)
        {
            await StartServer(server, statusMessage, "after update");
        }
    }

    private static async Task<bool> StopServer(GameServerConfig server, IUserMessage statusMessage, string? context = null)
    {
        if (string.IsNullOrWhiteSpace(server.ExecutableName)) return false;
        var exeNameNoExt = Path.GetFileNameWithoutExtension(server.ExecutableName);
        var processes = System.Diagnostics.Process.GetProcessesByName(exeNameNoExt);
        if (processes.Length == 0) return false; // Not running
        
        var contextText = context != null ? $" {context}" : "";
        await AppendToStatusMessage(statusMessage, $"Stopping '{server.Name}'{contextText}...");
        
        int stopped = 0;
        foreach (var proc in processes)
        {
            try { proc.Kill(); stopped++; }
            catch (Exception ex)
            {
                await AppendToStatusMessage(statusMessage, $":x: Failed to stop process {proc.Id} for '{server.Name}': {ex.Message}");
                Logger.Error($"Failed to stop process {proc.Id} for '{server.Name}'", ex);
                throw; // Interrupt the operation
            }
        }
        GameServerManagerWindowsService.Instance.MarkServerStopped(server.Name);
        await AppendToStatusMessage(statusMessage, $"Stopped {stopped} process(es) for '{server.Name}'{contextText}.");
        return true;
    }

    private static async Task StartServer(GameServerConfig server, IUserMessage statusMessage, string? context = null)
    {
        var contextText = context != null ? $" {context}" : "";
        if (!Utility.StartServerProcess(server, out Exception? error))
        {
            await AppendToStatusMessage(statusMessage, $":x: Failed to start '{server.Name}'{contextText}: {error?.Message}");
            return;
        }
        GameServerManagerWindowsService.Instance.MarkServerStarted(server.Name);
        await AppendToStatusMessage(statusMessage, $"Start command executed for '{server.Name}'{contextText}.");
    }

    private static async Task<bool> BackupServer(GameServerConfig server, string backupLocation, IUserMessage statusMessage, string? context = null)
    {
        try
        {
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var backupFileName = $"{server.Name}_backup_{timestamp}.zip";
            var backupFilePath = Path.Combine(backupLocation, backupFileName);
            var contextText = context != null ? $" ({context})" : "";
            await AppendToStatusMessage(statusMessage, $"Performing backup for '{server.Name}'{contextText}...");
            System.IO.Compression.ZipFile.CreateFromDirectory(server.SaveDirectory, backupFilePath);
            await AppendToStatusMessage(statusMessage, $"Backup of '{server.Name}' completed successfully{contextText}. File: {backupFileName}");
            return true;
        }
        catch (Exception ex)
        {
            await AppendToStatusMessage(statusMessage, $":x: Failed to backup server '{server.Name}': {ex.Message}");
            Logger.Error($"Failed to backup server '{server.Name}'", ex);
            return false;
        }
    }
}
