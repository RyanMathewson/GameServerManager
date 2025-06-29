using System.Text.Json;

namespace GameServerManagerService;

public class GameServerManagerConfiguration
{
    public List<GameServerConfig> Servers { get; set; } = [];
    public string BackupLocation { get; set; } = string.Empty;
    public string DiscordBotToken { get; set; } = string.Empty;

    public static GameServerManagerConfiguration Load(string path)
    {
        Logger.Log($"Attempting to load config from: {Path.GetFullPath(path)}");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Configuration file not found: {path}");
        }
        var json = File.ReadAllText(path);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var config = JsonSerializer.Deserialize<GameServerManagerConfiguration>(json, options) ?? new GameServerManagerConfiguration();
        Logger.Log($"Config loaded. Servers: {config.Servers.Count}");
        return config;
    }

    public bool Validate(out List<string> errors)
    {
        errors = [];
        if (Servers == null || Servers.Count == 0)
            errors.Add("No servers are configured.");
        if (string.IsNullOrWhiteSpace(BackupLocation))
            errors.Add("BackupLocation is not set.");
        else if (!Directory.Exists(BackupLocation))
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
                else if (!Directory.Exists(server.SaveDirectory))
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
