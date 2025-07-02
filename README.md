# GameServerManager

A Windows service and Discord bot for managing Steam-based game servers. This solution provides automated server management, backup, and update features, with Discord integration for remote control and status monitoring.

## Features
- Manage multiple game servers
- Start, stop, backup, and update servers via Discord commands
- Automatically restart previously running servers after reboot/shutdown

## Getting Started

### Prerequisites
- .NET 8.0 SDK or newer
- Windows OS (service mode)
- Discord bot token (see Discord developer portal)

### Setup
1. **Clone the repository:**
   ```sh
   git clone https://github.com/RyanMathewson/GameServerManager.git
   cd GameServerManager
   ```
2. **Configure your servers:**
   - Copy `GameServerManagerService/config.template.json` to `GameServerManagerService/config.json`.
   - Edit `config.json` with your server and Discord bot details.
3. **Build the solution:**
   ```sh
   dotnet build GameServerManagerService/GameServerManagerService.csproj
   ```
4. **Run as a console app (for testing):**
   ```sh
   dotnet run --project GameServerManagerService/GameServerManagerService.csproj -- --console
   ```
5. **Install as a Windows service:**
   - Run `install_service.bat` as administrator.

## Adding and Configuring the Discord Bot

1. **Create a Discord Application and Bot:**
   - Go to the [Discord Developer Portal](https://discord.com/developers/applications).
   - Click "New Application" and give it a name.
   - In the application settings, go to the "Bot" tab and click "Add Bot".
   - Under the bot settings, click "Reset Token" to generate your bot token. **Copy this token** (you will need it for your config).

2. **Invite the Bot to Your Server:**
   - In the Developer Portal, go to the "OAuth2" > "URL Generator".
   - Under "Scopes", select `bot`.
   - Under "Bot Permissions", select the permissions your bot needs (at minimum: `Send Messages`, `Read Messages`, `View Channels`).
   - Copy the generated URL, open it in your browser, and invite the bot to your Discord server.

3. **Configure the Bot Token in `config.json`:**
   - Open your `GameServerManagerService/config.json` file.
   - Set the `DiscordBotToken` field to your bot token:
     ```json
     {
       "DiscordBotToken": "YOUR_BOT_TOKEN_HERE",
       // ... other config fields ...
     }
     ```
   - Save the file. **Never share or commit your bot token!**

4. **Restart the Service or Console App:**
   - After updating your config, restart the service or console app to apply changes.

## Discord Bot Commands
- `!sm status` - Show server status (running, RAM, CPU)
- `!sm start <server>` - Start a server
- `!sm stop <server>` - Stop a server
- `!sm backup <server>` - Backup a server
- `!sm update <server>` - Update a server (with pre-update backup)

## Important Note: Game Save Locations & Service User

Some game servers store saved games and configuration files in user-specific directories (such as `C:\Users\<username>\AppData` or similar). By default, this service runs as the **Local System** account, which means game saves and settings may be stored in a different location than when you start the server manually (e.g., from your own user account).

**Implications:**
- If you start a server manually, it may load/save data from your user profile.
- If the service starts the server, it may load/save data from the Local System profile.
- This can result in different save files, configs, or even missing saves depending on how the server was started.

**Recommendation:**
- For best results, configure the Windows service to run under **your own user account** (via the Windows Services management console). This ensures game servers use the same save/config location as when you start them manually, making the transition from manual management to this service seamless and avoiding issues with missing or duplicate saves.
- Always check your game server documentation for details on where it stores data.

## Configuration File (`config.json`)

The main configuration file for the service and Discord bot is `GameServerManagerService/config.json`. You should create this file by copying `config.template.json` and editing it to match your environment.

**Key fields:**
- `DiscordBotToken`: *(string)* Your Discord bot token. Required for Discord integration.
- `AutoRestartServersOnBoot`: *(bool)* If `true`, the service will attempt to restart any servers that were running before a reboot or shutdown.
- `BackupLocation`: *(string)* Path where server backups will be stored.
- `Servers`: *(array)* List of game server definitions. Each server entry includes:
  - `Name`: *(string)* Friendly name for the server (used in Discord commands).
  - `ExecutableName`: *(string)* Name of the server executable (e.g., `valheim_server.exe`).
  - `StartCommand`: *(string)* Command to start the server.
  - `UpdateCommand`: *(string, optional)* Command to update the server.
  - `InstallLocation`: *(string, optional)* Directory where the server is installed.
  - `SaveDirectory`: *(string, optional)* Directory containing the server's save files (for backup).

**Example:**
```json
{
  "DiscordBotToken": "YOUR_BOT_TOKEN_HERE",
  "AutoRestartServersOnBoot": true,
  "BackupLocation": "C:/GameBackups",
  "Servers": [
    {
      "Name": "Valheim",
      "ExecutableName": "valheim_server.exe",
      "StartCommand": "start_valheim.bat",
      "UpdateCommand": "update_valheim.bat",
      "InstallLocation": "C:/Servers/Valheim",
      "SaveDirectory": "C:/Servers/Valheim/saves"
    }
  ]
}
```

## License
MIT License

---

For issues or contributions, open an issue or pull request on GitHub.
