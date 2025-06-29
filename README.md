# GameServerManager

A Windows service and Discord bot for managing Steam-based game servers. This solution provides automated server management, backup, and update features, with Discord integration for remote control and status monitoring.

## Features
- Manage multiple Steam game servers
- Start, stop, backup, and update servers via Discord commands
- Windows service mode and console mode
- Configuration validation and logging

## Project Structure
- `GameServerManagerService/` - Main service and bot implementation
  - `Program.cs` - Service, Discord bot, and configuration logic
  - `config.template.json` - Example configuration file (copy and edit as `config.json`)
  - `install_service.bat` - Script to install the service
- `.vscode/` - VS Code settings and tasks
- `.gitignore` - Excludes sensitive and build files from source control

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

## Security
- **Never commit your `config.json`!** It is excluded by `.gitignore`.
- Store your Discord bot token and sensitive paths only in `config.json`.

## License
MIT License

---

For issues or contributions, open an issue or pull request on GitHub.
