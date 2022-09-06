using System;
using System.Collections.Generic;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Utility;

namespace Robust.Shared.Map;

internal partial class MapManager
{
    // TODO: Move IMapManager stuff to the system
    private Dictionary<MapId, B2DynamicTree<MapGridComponent>> _gridTrees = new();

    private Dictionary<MapId, HashSet<MapGridComponent>> _movedGrids = new();

    /// <summary>
    /// Gets the grids that have moved this tick until broadphase has run.
    /// </summary>
    /// <param name="mapId"></param>
    /// <returns></returns>
    public HashSet<MapGridComponent> GetMovedGrids(MapId mapId)
    {
        return _movedGrids[mapId];
    }

    public void ClearMovedGrids(MapId mapId)
    {
        _movedGrids[mapId].Clear();
    }

    private void StartupGridTrees()
    {
        // Needs to be done on mapmanager startup because the eventbus will clear on shutdown
        // (and mapmanager initialize doesn't run upon connecting to a server every time).
        EntityManager.EventBus.SubscribeEvent<GridInitializeEvent>(EventSource.Local, this, OnGridInit);
        EntityManager.EventBus.SubscribeEvent<GridRemovalEvent>(EventSource.Local, this, OnGridRemove);
        EntityManager.EventBus.SubscribeLocalEvent<MapGridComponent, MoveEvent>(OnGridMove);
        EntityManager.EventBus.SubscribeLocalEvent<MapGridComponent, RotateEvent>(OnGridRotate);
        EntityManager.EventBus.SubscribeLocalEvent<MapGridComponent, EntParentChangedMessage>(OnGridParentChange);
    }

    private void ShutdownGridTrees()
    {
        EntityManager.EventBus.UnsubscribeEvent<GridInitializeEvent>(EventSource.Local, this);
        EntityManager.EventBus.UnsubscribeEvent<GridRemovalEvent>(EventSource.Local, this);
        EntityManager.EventBus.UnsubscribeLocalEvent<MapGridComponent, MoveEvent>();
        EntityManager.EventBus.UnsubscribeLocalEvent<MapGridComponent, RotateEvent>();
        EntityManager.EventBus.UnsubscribeLocalEvent<MapGridComponent, EntParentChangedMessage>();

        DebugTools.Assert(_gridTrees.Count == 0);
        DebugTools.Assert(_movedGrids.Count == 0);
    }

    private void OnMapCreatedGridTree(MapEventArgs e)
    {
        if (e.Map == MapId.Nullspace) return;

        _gridTrees.Add(e.Map, new B2DynamicTree<MapGridComponent>());
        _movedGrids.Add(e.Map, new HashSet<MapGridComponent>());
    }

    private void OnMapDestroyedGridTree(MapEventArgs e)
    {
        if (e.Map == MapId.Nullspace) return;

        _gridTrees.Remove(e.Map);
        _movedGrids.Remove(e.Map);
    }

    private Box2 GetWorldAABB(MapGridComponent grid)
    {
        var xform = EntityManager.GetComponent<TransformComponent>(grid.Owner);

        var (worldPos, worldRot) = xform.GetWorldPositionRotation();

        return new Box2Rotated(grid.LocalAABB, worldRot).CalcBoundingBox().Translated(worldPos);
    }

    private void OnGridInit(GridInitializeEvent args)
    {
        var grid = EntityManager.GetComponent<MapGridComponent>(args.EntityUid);
        var xform = EntityManager.GetComponent<TransformComponent>(args.EntityUid);
        var mapId = xform.MapID;

        if (mapId == MapId.Nullspace) return;

        AddGrid(grid, mapId);
    }

    private void AddGrid(MapGridComponent grid, MapId mapId)
    {
        var aabb = GetWorldAABB(grid);
        var proxy = _gridTrees[mapId].CreateProxy(in aabb, grid);

        grid.MapProxy = proxy;

        _movedGrids[mapId].Add(grid);
    }

    private void OnGridRemove(GridRemovalEvent args)
    {
        var grid = EntityManager.GetComponent<MapGridComponent>(args.EntityUid);
        var xform = EntityManager.GetComponent<TransformComponent>(args.EntityUid);

        // Can't check for free proxy because DetachParentToNull gets called first woo!
        if (xform.MapID == MapId.Nullspace) return;

        RemoveGrid(grid, xform.MapID);
    }

    private void RemoveGrid(MapGridComponent grid, MapId mapId)
    {
        _gridTrees[mapId].DestroyProxy(grid.MapProxy);
        _movedGrids[mapId].Remove(grid);
        grid.MapProxy = DynamicTree.Proxy.Free;
    }

    private void OnGridMove(EntityUid uid, MapGridComponent component, ref MoveEvent args)
    {
        var grid = (MapGridComponent) component;

        // Just maploader / test things
        if (grid.MapProxy == DynamicTree.Proxy.Free) return;

        var xform = EntityManager.GetComponent<TransformComponent>(uid);
        var aabb = GetWorldAABB(grid);
        _gridTrees[xform.MapID].MoveProxy(grid.MapProxy, in aabb, Vector2.Zero);
        _movedGrids[EntityManager.GetComponent<TransformComponent>(grid.Owner).MapID].Add(grid);
    }

    private void OnGridRotate(EntityUid uid, MapGridComponent component, ref RotateEvent args)
    {
        var grid = (MapGridComponent) component;

        // Just maploader / test things
        if (grid.MapProxy == DynamicTree.Proxy.Free) return;

        var xform = EntityManager.GetComponent<TransformComponent>(uid);
        var aabb = GetWorldAABB(grid);
        _gridTrees[xform.MapID].MoveProxy(grid.MapProxy, in aabb, Vector2.Zero);
        _movedGrids[EntityManager.GetComponent<TransformComponent>(grid.Owner).MapID].Add(grid);
    }

    private void OnGridParentChange(EntityUid uid, MapGridComponent component, ref EntParentChangedMessage args)
    {
        var aGrid = component;
        var lifestage = EntityManager.GetComponent<MetaDataComponent>(uid).EntityLifeStage;

        // oh boy
        // Want gridinit to handle this hence specialcase those situations.
        if (lifestage < EntityLifeStage.Initialized) return;

        // Make sure we cleanup old map for moved grid stuff.
        var mapId = args.Transform.MapID;

        // y'all need jesus
        if (args.OldMapId == mapId) return;

        if (aGrid.MapProxy != DynamicTree.Proxy.Free && _movedGrids.TryGetValue(args.OldMapId, out var oldMovedGrids))
        {
            oldMovedGrids.Remove(component);
            RemoveGrid(aGrid, args.OldMapId);
        }

        if (_movedGrids.TryGetValue(mapId, out var newMovedGrids))
        {
            newMovedGrids.Add(component);
            AddGrid(aGrid, mapId);
        }
    }

    public void OnGridBoundsChange(EntityUid uid, MapGridComponent grid)
    {
        // Just MapLoader things.
        if (grid.MapProxy == DynamicTree.Proxy.Free) return;

        var xform = EntityManager.GetComponent<TransformComponent>(uid);
        var aabb = GetWorldAABB(grid);
        _gridTrees[xform.MapID].MoveProxy(grid.MapProxy, in aabb, Vector2.Zero);
        _movedGrids[xform.MapID].Add(grid);
    }
}
