using System;
using System.Collections.Generic;
using System.Diagnostics;
using Robust.Server.Console;
using Robust.Server.Player;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Network;
using Robust.Shared.Network.Messages;
using Robust.Shared.Prototypes;

namespace Robust.Server.Prototypes
{
    public sealed partial class ServerPrototypeManager : PrototypeManager
    {
        [Dependency] private IPlayerManager _playerManager = default!;
        [Dependency] private IConGroupController _conGroups = default!;
        [Dependency] private INetManager _netManager = default!;
        [Dependency] private IBaseServerInternal _server = default!;

        public ServerPrototypeManager()
        {
            RegisterIgnore("shader");
            RegisterIgnore("uiTheme");
            RegisterIgnore("font");
        }

        public override void Initialize()
        {
            base.Initialize();

            _netManager.RegisterNetMessage<MsgReloadPrototypes>(HandleReloadPrototypes, NetMessageAccept.Server);
        }

        private void HandleReloadPrototypes(MsgReloadPrototypes msg)
        {
#if TOOLS
            if (!_playerManager.TryGetSessionByChannel(msg.MsgChannel, out var player) ||
                !_conGroups.CanAdminReloadPrototypes(player))
            {
                return;
            }

            var sw = Stopwatch.StartNew();

            ReloadPrototypes(msg.Paths);

            Sawmill.Info($"Reloaded prototypes in {sw.ElapsedMilliseconds} ms");
#endif
        }

        public override void LoadDefaultPrototypes(Dictionary<Type, HashSet<string>>? changed = null)
        {
            LoadDirectory(_server.Options.PrototypeDirectory, changed: changed);
            ResolveResults();
        }
    }
}
