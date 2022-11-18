using Robust.Shared.Configuration;
using Robust.Shared.IoC;
using Robust.Shared.Timing;
using System.Collections.Generic;
using System;
using Robust.Shared.Network.Messages;
using Robust.Shared.Network;
using Robust.Shared.Utility;

namespace Robust.Client.Configuration;

internal sealed class ClientNetConfigurationManager : NetConfigurationManager, IClientNetConfigurationManager
{
    [Dependency] private readonly IBaseClient _client = default!;

    private bool _receivedInitialNwVars = false;

    public event EventHandler? ReceivedInitialNwVars;

    public void SyncWithServer()
    {
        DebugTools.Assert(NetManager.IsConnected);

        Sawmill.Info("Sending client info...");

        var msg = new MsgConVars();
        msg.Tick = default;
        msg.NetworkedVars = GetReplicatedVars();
        NetManager.ClientSendMessage(msg);
    }

    public void ClearReceivedInitialNwVars()
    {
        _receivedInitialNwVars = false;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        ReceivedInitialNwVars = null;
        _receivedInitialNwVars = false;
    }

    /// <inheritdoc />
    public override void SetCVar(string name, object value)
    {
        CVar flags;
        using (Lock.ReadGuard())
        {
            if (!_configVars.TryGetValue(name, out var cVar) || !cVar.Registered)
                throw new InvalidConfigurationException($"Trying to set unregistered variable '{name}'");

            flags = cVar.Flags;
            if (NetManager.IsConnected && (cVar.Flags & CVar.NOT_CONNECTED) != 0)
            {
                Sawmill.Warning($"'{name}' can only be changed when not connected to a server.");
                return;
            }

            if (((cVar.Flags & CVar.SERVER) != 0) && _client.RunLevel != ClientRunLevel.SinglePlayerGame)
            {
                Sawmill.Warning($"Only the server can change '{name}'.");
                return;
            }
        }

        // Actually set the CVar
        base.SetCVar(name, value);

        if ((flags & CVar.REPLICATED) == 0)
            return;

        var msg = new MsgConVars();
        msg.Tick = Timing.CurTick;
        msg.NetworkedVars = new List<(string name, object value)>
                {
                    (name, value)
                };
        NetManager.ClientSendMessage(msg);
    }

    protected override void HandleNetVarMessage(MsgConVars message)
    {
        if (!_receivedInitialNwVars)
            ApplyClientNetVarChange(message.NetworkedVars, message.Tick);
        else
            base.HandleNetVarMessage(message);
    }

    protected override void ApplyNetVarChange(
        INetChannel msgChannel,
        List<(string name, object value)> networkedVars,
        GameTick tick)
    {
        ApplyClientNetVarChange(networkedVars, tick);
    }

    public void ApplyClientNetVarChange(List<(string name, object value)> networkedVars, GameTick tick)
    {
        // Server sent us a CVar update.
        Sawmill.Debug($"Handling replicated cvars...");

        foreach (var (name, value) in networkedVars)
        {
            // Actually set the CVar
            SetCVarInternal(name, value, tick);

            Sawmill.Debug($"name={name}, val={value}");
        }

        if (!_receivedInitialNwVars)
        {
            _receivedInitialNwVars = true;
            ReceivedInitialNwVars?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc />
    public override T GetClientCVar<T>(INetChannel channel, string name) => GetCVar<T>(name);
}
