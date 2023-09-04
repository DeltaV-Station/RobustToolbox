using System.Text;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.IoC;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Robust.Server.Console.Commands
{
    /*
    // Disabled for now since it doesn't actually work.
    sealed class RestartCommand : LocalizedCommands
    {
        [Dependency] private IBaseServer _server = default!;

        public override string Command => "restart";

        public override void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            _server.Restart();
        }
    }
    */

    [InjectDependencies]
    sealed partial class ShutdownCommand : LocalizedCommands
    {
        [Dependency] private IBaseServer _server = default!;

        public override string Command => "shutdown";

        public override void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            _server.Shutdown(null);
        }
    }

    [InjectDependencies]
    sealed partial class NetworkAuditCommand : LocalizedCommands
    {
        [Dependency] private INetManager _netManager = default!;

        public override string Command => "netaudit";
        public override void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            var callbacks = ((NetManager)_netManager).CallbackAudit;

            var sb = new StringBuilder();

            foreach (var kvCallback in callbacks)
            {
                var msgType = kvCallback.Key;
                var call = kvCallback.Value;

                sb.AppendLine($"Type: {msgType.Name.PadRight(16)} Call:{call.Target}");
            }

            shell.WriteLine(sb.ToString());
        }
    }

    [InjectDependencies]
    sealed partial class ShowTimeCommand : LocalizedCommands
    {
        [Dependency] private IGameTiming _timing = default!;

        public override string Command => "showtime";

        public override void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            shell.WriteLine($"Paused: {_timing.Paused}, CurTick: {_timing.CurTick}, CurTime: {_timing.CurTime}, RealTime: {_timing.RealTime}");
        }
    }
}
