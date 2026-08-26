using Microsoft.CommandPalette.Extensions;
using Shmuelie.WinRTServer;
using System.Threading;

namespace CmdPalDockPlus.Extension;

public static class Program
{
    [MTAThread]
    public static void Main(string[] args)
    {
        if (args.Length == 0 || args[0] != "-RegisterProcessAsComServer")
        {
            return;
        }

        ComServer server = new();
        using ManualResetEvent disposed = new(false);
        CmdPalDockPlusExtension extension = new(disposed);
        server.RegisterClass<CmdPalDockPlusExtension, IExtension>(() => extension);
        server.Start();
        disposed.WaitOne();
        server.Stop();
        server.UnsafeDispose();
    }
}
