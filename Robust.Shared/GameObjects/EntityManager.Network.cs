using System.Collections.Generic;
using Robust.Shared.Map;

namespace Robust.Shared.GameObjects;

public partial class EntityManager
{
    /// <summary>
    /// Inverse lookup for net entities.
    /// Regular lookup uses MetadataComponent.
    /// </summary>
    protected readonly Dictionary<NetEntity, EntityUid> NetEntityLookup = new();

    public virtual bool IsClientSide(EntityUid uid, MetaDataComponent? metadata = null)
    {
        return false;
    }

    public EntityUid ToEntity(NetEntity nEntity)
    {
        return NetEntityLookup.TryGetValue(nEntity, out var entity) ? entity : EntityUid.Invalid;
    }

    public EntityUid? ToEntity(NetEntity? nEntity)
    {
        if (nEntity == null)
            return null;

        return NetEntityLookup.TryGetValue(nEntity.Value, out var entity) ? entity : null;
    }

    public NetEntity ToNetEntity(EntityUid uid, MetaDataComponent? metadata = null)
    {
        return MetaQuery.Resolve(uid, ref metadata) ? metadata.NetEntity : NetEntity.Invalid;
    }

    public NetEntity? ToNetEntity(EntityUid? uid, MetaDataComponent? metadata = null)
    {
        if (uid == null)
            return null;

        return MetaQuery.Resolve(uid.Value, ref metadata) ? metadata.NetEntity : null;
    }

    #region Helpers

    public HashSet<EntityUid> ToEntitySet(HashSet<NetEntity> netEntities)
    {
        var entities = _poolManager.GetEntitySet();
        entities.EnsureCapacity(netEntities.Count);

        foreach (var netEntity in netEntities)
        {
            entities.Add(ToEntity(netEntity));
        }

        return entities;
    }

    public List<EntityUid> ToEntityList(List<NetEntity> netEntities)
    {
        var entities = _poolManager.GetEntityList();
        entities.EnsureCapacity(netEntities.Count);

        foreach (var netEntity in netEntities)
        {
            entities.Add(ToEntity(netEntity));
        }

        return entities;
    }

    /// <summary>
    /// Returns the <see cref="NetEntity"/> to a HashSet of <see cref="EntityUid"/>
    /// </summary>
    public HashSet<NetEntity> ToNetEntitySet(HashSet<EntityUid> entities)
    {
        var newSet = _poolManager.GetNetEntitySet();
        newSet.EnsureCapacity(entities.Count);

        foreach (var ent in entities)
        {
            MetaQuery.TryGetComponent(ent, out var metadata);
            newSet.Add(ToNetEntity(ent, metadata));
        }

        return newSet;
    }

    public List<NetEntity> ToNetEntityList(List<EntityUid> entities)
    {
        var netEntities = _poolManager.GetNetEntityList();
        netEntities.EnsureCapacity(entities.Count);

        foreach (var netEntity in entities)
        {
            netEntities.Add(ToNetEntity(netEntity));
        }

        return netEntities;
    }

    #endregion

    #region NetCoordinates

    public NetCoordinates ToNetCoordinates(EntityCoordinates coordinates)
    {
        return new NetCoordinates(ToNetEntity(coordinates.EntityId), coordinates.Position);
    }

    public NetCoordinates? ToNetCoordinates(EntityCoordinates? coordinates)
    {
        if (coordinates == null)
            return null;

        return new NetCoordinates(ToNetEntity(coordinates.Value.EntityId), coordinates.Value.Position);
    }

    public EntityCoordinates ToCoordinates(NetCoordinates coordinates)
    {
        return new EntityCoordinates(ToEntity(coordinates.NetEntity), coordinates.Position);
    }

    public EntityCoordinates? ToCoordinates(NetCoordinates? coordinates)
    {
        if (coordinates == null)
            return null;

        return new EntityCoordinates(ToEntity(coordinates.Value.NetEntity), coordinates.Value.Position);
    }

    #endregion
}
