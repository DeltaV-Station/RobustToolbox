using Robust.Shared.Console;
using Robust.Shared.IoC;

namespace Robust.Client.Console.Commands
{
#if CLIENT_SCRIPTING
    internal sealed class ScriptCommand : LocalizedCommands
    {
        public override string Command => "csi";

        public override void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            new ScriptConsoleClient().OpenCentered();
        }
    }

    internal sealed class WatchCommand : LocalizedCommands
    {
        public override void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            new WatchWindow().OpenCentered();
        }
    }
#endif

    internal sealed class ServerScriptCommand : LocalizedCommands
    {
        public override string Command => "scsi";

        public override void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            var mgr = IoCManager.Resolve<IScriptClient>();
            if (!mgr.CanScript)
            {
                shell.WriteError("You do not have server side scripting permission.");
                return;
            }

            mgr.StartSession();
        }
    }
}
