using JetBrains.Annotations;
using Robust.Client.Player;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Robust.Client.Console.Commands
{
    [UsedImplicitly]
    internal sealed partial class ClientSpawnCommand : LocalizedCommands
    {
        [Dependency] private IPlayerManager _playerManager = default!;
        [Dependency] private IEntityManager _entityManager = default!;

        public override string Command => "cspawn";

        public override void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            var controlled = _playerManager.LocalPlayer?.ControlledEntity ?? EntityUid.Invalid;
            if (controlled == EntityUid.Invalid)
            {
                shell.WriteLine("You don't have an attached entity.");
                return;
            }

            var entityManager = _entityManager;
            entityManager.SpawnEntity(args[0], entityManager.GetComponent<TransformComponent>(controlled).Coordinates);
        }
    }
}
