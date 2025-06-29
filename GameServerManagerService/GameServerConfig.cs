namespace GameServerManagerService;

public class GameServerConfig
{
    public string Name { get; set; } = string.Empty;
    public string InstallLocation { get; set; } = string.Empty;
    public string SaveDirectory { get; set; } = string.Empty;
    public string StartCommand { get; set; } = string.Empty;
    public string UpdateCommand { get; set; } = string.Empty;
    public string ExecutableName { get; set; } = string.Empty;
}
