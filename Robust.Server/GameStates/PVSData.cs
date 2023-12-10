using System.Collections.Generic;
using Robust.Shared.Collections;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Robust.Server.GameStates;

/// <summary>
/// Class for storing session specific PVS data.
/// </summary>
internal sealed class SessionPvsData
{
    /// <summary>
    /// All <see cref="EntityUid"/>s that this session saw during the last <see cref="PvsSystem.DirtyBufferSize"/> ticks.
    /// </summary>
    public readonly OverflowDictionary<GameTick, HashSet<NetEntity>> SentEntities = new(PvsSystem.DirtyBufferSize);

    public readonly Dictionary<NetEntity, EntityData> EntityData = new();

    /// <summary>
    /// <see cref="SentEntities"/> overflow in case a player's last ack is more than
    /// <see cref="PvsSystem.DirtyBufferSize"/> ticks behind the current tick.
    /// </summary>
    public (GameTick Tick, HashSet<NetEntity> SentEnts)? Overflow;

    /// <summary>
    /// If true, the client has explicitly requested a full state. Unlike the first state, we will send them all data,
    /// not just data that cannot be implicitly inferred from entity prototypes.
    /// </summary>
    public bool RequestedFull = false;

    public GameTick LastReceivedAck;

    public readonly ICommonSession Session;

    public SessionPvsData(ICommonSession session)
    {
        Session = session;
    }
}

/// <summary>
/// Struct for storing session-specific information about when an entity was last sent to a player.
/// </summary>
internal struct EntityData
{
    public readonly Entity<MetaDataComponent> Entity;

    /// <summary>
    /// Tick at which this entity was last sent to a player.
    /// </summary>
    public GameTick LastSent;

    /// <summary>
    /// Tick at which an entity last left a player's PVS view.
    /// </summary>
    public GameTick LastLeftView;

    /// <summary>
    /// Stores the last tick at which a given entity was acked by a player. Used to avoid re-sending the whole entity
    /// state when an item re-enters PVS. This is only the same as the player's last acked tick if the entity was
    /// present in that state.
    /// </summary>
    public GameTick EntityLastAcked;

    /// <summary>
    /// Entity visibility state when it was last sent to this player.
    /// </summary>
    public PvsEntityVisibility Visibility;

    public EntityData(Entity<MetaDataComponent> entity)
    {
        Entity = entity;
    }

    public override string ToString()
    {
        var rep = new EntityStringRepresentation(Entity);
        return $"PVS Entity: {rep} - {LastSent}/{LastLeftView}/{EntityLastAcked}";
    }
}
