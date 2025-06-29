using System.ServiceProcess;

namespace GameServerManagerService;


// Define the program entry point
internal static class Program
{
    private static void Main(string[] args)
    {
        if (args.Contains("--console"))
        {
            var service = new GameServerManagerWindowsService();
            service.StartAsConsole();
        }
        else
        {
            ServiceBase.Run(new GameServerManagerWindowsService());
        }
    }
}

