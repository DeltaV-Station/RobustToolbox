using System;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Utility;

namespace Robust.Shared.GameObjects;

public abstract partial class SharedTransformSystem
{
    #region Anchoring

    internal void ReAnchor(TransformComponent xform,
        MapGridComponent oldGrid,
        MapGridComponent newGrid,
        Vector2i tilePos,
        TransformComponent oldGridXform,
        TransformComponent newGridXform,
        EntityQuery<TransformComponent> xformQuery)
    {
        // Bypass some of the expensive stuff in unanchoring / anchoring.
        oldGrid.Grid.RemoveFromSnapGridCell(tilePos, xform.Owner);
        newGrid.Grid.AddToSnapGridCell(tilePos, xform.Owner);
        // TODO: Could do this re-parent way better.
        // Unfortunately we don't want any anchoring events to go out hence... this.
        xform._anchored = false;
        oldGridXform._children.Remove(xform.Owner);
        newGridXform._children.Add(xform.Owner);
        xform._parent = newGrid.Owner;
        xform._anchored = true;

        SetGridId(xform, newGrid.GridIndex, xformQuery);
        var reParent = new EntParentChangedMessage(xform.Owner, oldGrid.Owner, xform.MapID, xform);
        RaiseLocalEvent(xform.Owner, ref reParent);
        // TODO: Ideally shouldn't need to call the moveevent
        var movEevee = new MoveEvent(xform.Owner,
            new EntityCoordinates(oldGrid.Owner, xform._localPosition),
            new EntityCoordinates(newGrid.Owner, xform._localPosition), xform);
        RaiseLocalEvent(xform.Owner, ref movEevee);

        DebugTools.Assert(xformQuery.GetComponent(oldGrid.Owner).MapID == xformQuery.GetComponent(newGrid.Owner).MapID);
        DebugTools.Assert(xform._anchored);

        Dirty(xform);
        var ev = new ReAnchorEvent(xform.Owner, oldGrid.GridIndex, newGrid.GridIndex, tilePos);
        RaiseLocalEvent(xform.Owner, ref ev);
    }


    public bool AnchorEntity(TransformComponent xform, IMapGrid grid, Vector2i tileIndices)
    {
        var result = grid.AddToSnapGridCell(tileIndices, xform.Owner);

        if (result)
        {
            // Mark as static first to avoid the velocity change on parent change.
            if (TryComp<PhysicsComponent>(xform.Owner, out var physicsComponent))
                physicsComponent.BodyType = BodyType.Static;

            // anchor snapping
            // Internally it will do the parent update; doing it separately just triggers a redundant move.
            xform.Coordinates = new EntityCoordinates(grid.GridEntityId, grid.GridTileToLocal(tileIndices).Position);
            xform.SetAnchored(result);
        }

        return result;
    }

    public bool AnchorEntity(TransformComponent xform, IMapGridComponent component)
    {
        return AnchorEntity(xform, component.Grid);
    }

    public bool AnchorEntity(TransformComponent xform, IMapGrid grid)
    {
        var tileIndices = grid.TileIndicesFor(xform.Coordinates);
        return AnchorEntity(xform, grid, tileIndices);
    }

    public bool AnchorEntity(TransformComponent xform)
    {
        if (!_mapManager.TryGetGrid(xform.GridID, out var grid))
        {
            return false;
        }

        var tileIndices = grid.TileIndicesFor(xform.Coordinates);
        return AnchorEntity(xform, grid, tileIndices);
    }

    public void Unanchor(TransformComponent xform)
    {
        //HACK: Client grid pivot causes this.
        //TODO: make grid components the actual grid
        if(xform.GridID == GridId.Invalid)
            return;

        UnanchorEntity(xform, Comp<IMapGridComponent>(_mapManager.GetGridEuid(xform.GridID)));
    }

    public void UnanchorEntity(TransformComponent xform, IMapGridComponent grid)
    {
        var tileIndices = grid.Grid.TileIndicesFor(xform.Coordinates);
        grid.Grid.RemoveFromSnapGridCell(tileIndices, xform.Owner);
        if (TryComp<PhysicsComponent>(xform.Owner, out var physicsComponent))
        {
            physicsComponent.BodyType = BodyType.Dynamic;
        }

        xform.SetAnchored(false);
    }

    #endregion

    #region Contains

    /// <summary>
    ///     Returns whether the given entity is a child of this transform or one of its descendants.
    /// </summary>
    public bool ContainsEntity(TransformComponent xform, EntityUid entity)
    {
        return ContainsEntity(xform, entity, GetEntityQuery<TransformComponent>());
    }

    /// <inheritdoc cref="ContainsEntity(Robust.Shared.GameObjects.TransformComponent,Robust.Shared.GameObjects.EntityUid)"/>
    public bool ContainsEntity(TransformComponent xform, EntityUid entity, EntityQuery<TransformComponent> xformQuery)
    {
        return ContainsEntity(xform, xformQuery.GetComponent(entity), xformQuery);
    }

    /// <inheritdoc cref="ContainsEntity(Robust.Shared.GameObjects.TransformComponent,Robust.Shared.GameObjects.EntityUid)"/>
    public bool ContainsEntity(TransformComponent xform, TransformComponent entityTransform)
    {
        return ContainsEntity(xform, entityTransform, GetEntityQuery<TransformComponent>());
    }

    /// <inheritdoc cref="ContainsEntity(Robust.Shared.GameObjects.TransformComponent,Robust.Shared.GameObjects.EntityUid)"/>
    public bool ContainsEntity(TransformComponent xform, TransformComponent entityTransform, EntityQuery<TransformComponent> xformQuery)
    {
        // Is the entity the scene root
        if (!entityTransform.ParentUid.IsValid())
            return false;

        // Is this the direct parent of the entity
        if (xform.Owner == entityTransform.ParentUid)
            return true;

        // Recursively search up the parents for this object
        var parentXform = xformQuery.GetComponent(entityTransform.ParentUid);
        return ContainsEntity(xform, parentXform, xformQuery);
    }

    #endregion

    #region Component Lifetime

    private void OnCompInit(EntityUid uid, TransformComponent component, ComponentInit args)
    {
        // Children MAY be initialized here before their parents are.
        // We do this whole dance to handle this recursively,
        // setting _mapIdInitialized along the way to avoid going to the IMapComponent every iteration.
        static MapId FindMapIdAndSet(TransformComponent xform, IEntityManager entMan, EntityQuery<TransformComponent> xformQuery)
        {
            if (xform._mapIdInitialized)
                return xform.MapID;

            var value = MapId.Nullspace;

            if (xform.ParentUid.IsValid())
            {
                value = FindMapIdAndSet(xformQuery.GetComponent(xform.ParentUid), entMan, xformQuery);
            }
            else
            {
                // Entity is the Map so set the value to itself.
                if (entMan.TryGetComponent(xform.Owner, out IMapComponent? mapComp))
                {
                    value = mapComp.WorldMap;
                }
                // If that fails then entity was spawned in nullspace.
            }

            xform.MapID = value;
            xform._mapIdInitialized = true;
            return value;
        }

        var xformQuery = GetEntityQuery<TransformComponent>();

        if (!component._mapIdInitialized)
        {
            FindMapIdAndSet(component, EntityManager, xformQuery);
            component._mapIdInitialized = true;
        }

        // Has to be done if _parent is set from ExposeData.
        if (component.ParentUid.IsValid())
        {
            // Note that _children is a SortedSet<EntityUid>,
            // so duplicate additions (which will happen) don't matter.
            xformQuery.GetComponent(component.ParentUid)._children.Add(uid);
        }

        component.GridID = component.GetGridIndex(xformQuery);
        component.RebuildMatrices();
    }

    private void OnCompStartup(EntityUid uid, TransformComponent component, ComponentStartup args)
    {
        // Re-Anchor the entity if needed.
        if (component._anchored && _mapManager.TryFindGridAt(component.MapPosition, out var grid))
        {
            if (!grid.IsAnchored(component.Coordinates, uid))
            {
                AnchorEntity(component, grid);
            }
        }
        else
            component._anchored = false;

        // Keep the cached matrices in sync with the fields.
        Dirty(component);

        var ev = new TransformStartupEvent(component);
        RaiseLocalEvent(uid, ref ev);
    }

    #endregion

    #region GridId

    /// <summary>
    /// Sets the <see cref="GridId"/> for the transformcomponent. Does not Dirty it.
    /// </summary>
    public void SetGridId(TransformComponent xform, GridId gridId)
    {
        SetGridId(xform, gridId, GetEntityQuery<TransformComponent>());
    }

    /// <inheritdoc cref="SetGridId"/> />
    private void SetGridId(TransformComponent xform, GridId gridId, EntityQuery<TransformComponent> xformQuery)
    {
        if (xform.GridID == gridId) return;

        SetGridIdRecursive(xform, gridId, xformQuery);
    }

    private static void SetGridIdRecursive(TransformComponent xform, GridId gridId, EntityQuery<TransformComponent> xformQuery)
    {
        xform._gridId = gridId;
        var childEnumerator = xform.ChildEnumerator;

        while (childEnumerator.MoveNext(out var child))
        {
            SetGridIdRecursive(xformQuery.GetComponent(child.Value), gridId, xformQuery);
        }
    }

    #endregion

    #region Parent

    public TransformComponent? GetParent(EntityUid uid)
    {
        return GetParent(uid, GetEntityQuery<TransformComponent>());
    }

    public TransformComponent? GetParent(EntityUid uid, EntityQuery<TransformComponent> xformQuery)
    {
        return GetParent(xformQuery.GetComponent(uid), xformQuery);
    }

    public TransformComponent? GetParent(TransformComponent xform)
    {
        return GetParent(xform, GetEntityQuery<TransformComponent>());
    }

    public TransformComponent? GetParent(TransformComponent xform, EntityQuery<TransformComponent> xformQuery)
    {
        if (!xform.ParentUid.IsValid()) return null;
        return xformQuery.GetComponent(xform.ParentUid);
    }

    /* TODO: Need to peel out relevant bits of AttachParent e.g. children updates.
    public void SetParent(TransformComponent xform, EntityUid parent, bool move = true)
    {
        if (xform.ParentUid == parent) return;

        if (!parent.IsValid())
        {
            xform.AttachToGridOrMap();
            return;
        }

        if (xform.Anchored)
        {
            xform.Anchored = false;
        }

        if (move)
            xform.AttachParent(parent);
        else
            xform._parent = parent;

        Dirty(xform);
    }
    */

    #endregion

    #region States

    private void ActivateLerp(TransformComponent xform)
    {
        if (xform.ActivelyLerping)
        {
            return;
        }

        xform.ActivelyLerping = true;
        RaiseLocalEvent(xform.Owner, new TransformStartLerpMessage(xform));
    }

    internal void OnGetState(EntityUid uid, TransformComponent component, ref ComponentGetState args)
    {
        args.State = new TransformComponentState(
            component.LocalPosition,
            component.LocalRotation,
            component.ParentUid,
            component.NoLocalRotation,
            component.Anchored);
    }

    internal void OnHandleState(EntityUid uid, TransformComponent component, ref ComponentHandleState args)
    {
        if (args.Current is TransformComponentState newState)
        {
            var newParentId = newState.ParentID;
            var rebuildMatrices = false;

            // Update to new parent.
            // if the new one isn't valid (e.g. PVS) then we'll just return early after yeeting them to nullspace.
            if (component.ParentUid != newParentId)
            {
                if (!newParentId.IsValid())
                {
                    component.DetachParentToNull();
                    return;
                }
                else
                {
                    if (!Exists(newParentId))
                    {
#if !EXCEPTION_TOLERANCE
                        throw new InvalidOperationException($"Unable to find new parent {newParentId}! This probably means the server never sent it.");
#else
                        _logger.Error($"Unable to find new parent {newParentId}! Deleting {ToPrettyString(uid)}");
                        QueueDel(uid);
                        return;
#endif
                    }

                    component.AttachParent(Transform(newParentId));
                }

                rebuildMatrices = true;
            }

            if (component.LocalRotation != newState.Rotation)
            {
                component._localRotation = newState.Rotation;
                rebuildMatrices = true;
            }

            if (!component.LocalPosition.EqualsApprox(newState.LocalPosition))
            {
                var oldPos = component.Coordinates;
                component._localPosition = newState.LocalPosition;

                var ev = new MoveEvent(uid, oldPos, component.Coordinates, component);
                DeferMoveEvent(ref ev);

                rebuildMatrices = true;
            }

            component._prevPosition = newState.LocalPosition;
            component._prevRotation = newState.Rotation;

            // Anchored currently does a TryFindGridAt internally which may fail in particularly... violent situations.
            if (newState.Anchored && !component.Anchored)
            {
                var iGrid = Comp<MapGridComponent>(_mapManager.GetGridEuid(component.GridID));
                AnchorEntity(component, iGrid);
                DebugTools.Assert(component.Anchored);
            }
            else
            {
                component.Anchored = newState.Anchored;
            }

            component._noLocalRotation = newState.NoLocalRotation;

            // This is not possible, because client entities don't exist on the server, so the parent HAS to be a shared entity.
            // If this assert fails, the code above that sets the parent is broken.
            DebugTools.Assert(!component.ParentUid.IsClientSide(), "Transform received a state, but is still parented to a client entity.");

            // Whatever happened on the client, these should still be correct
            DebugTools.Assert(component.ParentUid == newState.ParentID);
            DebugTools.Assert(component.Anchored == newState.Anchored);

            if (rebuildMatrices)
            {
                component.RebuildMatrices();
            }

            Dirty(component);
        }

        if (args.Next is TransformComponentState nextTransform)
        {
            component._nextPosition = nextTransform.LocalPosition;
            component._nextRotation = nextTransform.Rotation;
            component.LerpParent = nextTransform.ParentID;
            ActivateLerp(component);
        }
        else
        {
            // this should cause the lerp to do nothing
            component._nextPosition = null;
            component._nextRotation = null;
            component.LerpParent = EntityUid.Invalid;
        }
    }

    #endregion

    #region World Matrix

    [Pure]
    public Matrix3 GetWorldMatrix(EntityUid uid)
    {
        return Transform(uid).WorldMatrix;
    }

    // Temporary until it's moved here
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Matrix3 GetWorldMatrix(TransformComponent component)
    {
        return component.WorldMatrix;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Matrix3 GetWorldMatrix(EntityUid uid, EntityQuery<TransformComponent> xformQuery)
    {
        return GetWorldMatrix(xformQuery.GetComponent(uid));
    }


    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Matrix3 GetWorldMatrix(TransformComponent component, EntityQuery<TransformComponent> xformQuery)
    {
        return component.WorldMatrix;
    }

    #endregion

    #region World Position

    [Pure]
    public Vector2 GetWorldPosition(EntityUid uid)
    {
        return Transform(uid).WorldPosition;
    }

    // Temporary until it's moved here
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2 GetWorldPosition(TransformComponent component)
    {
        return component.WorldPosition;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2 GetWorldPosition(EntityUid uid, EntityQuery<TransformComponent> xformQuery)
    {
        return GetWorldPosition(xformQuery.GetComponent(uid));
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2 GetWorldPosition(TransformComponent component, EntityQuery<TransformComponent> xformQuery)
    {
        return component.WorldPosition;
    }

    #endregion

    #region World Rotation

    [Pure]
    public Angle GetWorldRotation(EntityUid uid)
    {
        return Transform(uid).WorldRotation;
    }

    // Temporary until it's moved here
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Angle GetWorldRotation(TransformComponent component)
    {
        return component.WorldRotation;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Angle GetWorldRotation(EntityUid uid, EntityQuery<TransformComponent> xformQuery)
    {
        return GetWorldRotation(xformQuery.GetComponent(uid));
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Angle GetWorldRotation(TransformComponent component, EntityQuery<TransformComponent> xformQuery)
    {
        return component.WorldRotation;
    }

    #endregion

    #region Inverse World Matrix

    [Pure]
    public Matrix3 GetInvWorldMatrix(EntityUid uid)
    {
        return Comp<TransformComponent>(uid).InvWorldMatrix;
    }

    // Temporary until it's moved here
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Matrix3 GetInvWorldMatrix(TransformComponent component)
    {
        return component.InvWorldMatrix;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Matrix3 GetInvWorldMatrix(EntityUid uid, EntityQuery<TransformComponent> xformQuery)
    {
        return GetInvWorldMatrix(xformQuery.GetComponent(uid));
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Matrix3 GetInvWorldMatrix(TransformComponent component, EntityQuery<TransformComponent> xformQuery)
    {
        return component.InvWorldMatrix;
    }

    #endregion

    public MapId GetMapId(EntityUid? uid, TransformComponent? xform = null)
    {
        if (uid == null ||
            !uid.Value.IsValid() ||
            !Resolve(uid.Value, ref xform, false)) return MapId.Nullspace;

        return xform.MapID;
    }
}
