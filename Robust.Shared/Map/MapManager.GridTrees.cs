using System.Collections.Generic;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Utility;

namespace Robust.Shared.Map;

internal partial class MapManager
{
    // TODO: Move IMapManager stuff to the system
    private Dictionary<MapId, B2DynamicTree<MapGrid>> _gridTrees = new();
    private Dictionary<MapId, HashSet<IMapGrid>> _movedGrids = new();

    // Gets the grids that have moved this tick until broadphase has run.
    public HashSet<IMapGrid> GetMovedGrids(MapId mapId)
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

        _gridTrees.Add(e.Map, new B2DynamicTree<MapGrid>());
        _movedGrids.Add(e.Map, new HashSet<IMapGrid>());
    }

    private void OnMapDestroyedGridTree(MapEventArgs e)
    {
        if (e.Map == MapId.Nullspace) return;

        _gridTrees.Remove(e.Map);
        _movedGrids.Remove(e.Map);
    }

    private Box2 GetWorldAABB(MapGrid grid)
    {
        var xform = EntityManager.GetComponent<TransformComponent>(grid.GridEntityId);

        var (worldPos, worldRot) = xform.GetWorldPositionRotation();

        return new Box2Rotated(grid.LocalBounds, worldRot).CalcBoundingBox().Translated(worldPos);
    }

    private void OnGridInit(GridInitializeEvent args)
    {
        var grid = (MapGrid) GetGrid(args.GridId);
        var xform = EntityManager.GetComponent<TransformComponent>(args.EntityUid);
        var mapId = xform.MapID;

        if (mapId == MapId.Nullspace) return;

        AddGrid(grid, mapId);
    }

    private void AddGrid(MapGrid grid, MapId mapId)
    {
        var aabb = GetWorldAABB(grid);
        var proxy = _gridTrees[mapId].CreateProxy(in aabb, grid);

        grid.MapProxy = proxy;

        _movedGrids[mapId].Add(grid);
    }

    private void OnGridRemove(GridRemovalEvent args)
    {
        var grid = (MapGrid) GetGrid(args.GridId);
        var xform = EntityManager.GetComponent<TransformComponent>(args.EntityUid);

        // Can't check for free proxy because DetachParentToNull gets called first woo!
        if (xform.MapID == MapId.Nullspace) return;

        RemoveGrid(grid, xform.MapID);
    }

    private void RemoveGrid(MapGrid grid, MapId mapId)
    {
        _gridTrees[mapId].DestroyProxy(grid.MapProxy);
        _movedGrids[mapId].Remove(grid);
        grid.MapProxy = DynamicTree.Proxy.Free;
    }

    private void OnGridMove(EntityUid uid, MapGridComponent component, ref MoveEvent args)
    {
        var grid = (MapGrid) component.Grid;

        // Just maploader / test things
        if (grid.MapProxy == DynamicTree.Proxy.Free) return;

        var xform = EntityManager.GetComponent<TransformComponent>(uid);
        var aabb = GetWorldAABB(grid);
        _gridTrees[xform.MapID].MoveProxy(grid.MapProxy, in aabb, Vector2.Zero);
        _movedGrids[grid.ParentMapId].Add(grid);
    }

    private void OnGridRotate(EntityUid uid, MapGridComponent component, ref RotateEvent args)
    {
        var grid = (MapGrid) component.Grid;

        // Just maploader / test things
        if (grid.MapProxy == DynamicTree.Proxy.Free) return;

        var xform = EntityManager.GetComponent<TransformComponent>(uid);
        var aabb = GetWorldAABB(grid);
        _gridTrees[xform.MapID].MoveProxy(grid.MapProxy, in aabb, Vector2.Zero);
        _movedGrids[grid.ParentMapId].Add(grid);
    }

    private void OnGridParentChange(EntityUid uid, MapGridComponent component, ref EntParentChangedMessage args)
    {
        var aGrid = (MapGrid)component.Grid;
        var lifestage = EntityManager.GetComponent<MetaDataComponent>(uid).EntityLifeStage;

        // oh boy
        // Want gridinit / gridremoval to handle this hence specialcase those situations.
        if (lifestage < EntityLifeStage.Initialized) return;

        var oldMapId = args.OldParent == null
            ? MapId.Nullspace
            : EntityManager.GetComponent<TransformComponent>(args.OldParent.Value).MapID;

        // Make sure we cleanup old map for moved grid stuff.
        var mapId = EntityManager.GetComponent<TransformComponent>(uid).MapID;

        // y'all need jesus
        if (oldMapId == mapId) return;

        if (aGrid.MapProxy != DynamicTree.Proxy.Free && _movedGrids.TryGetValue(oldMapId, out var oldMovedGrids))
        {
            oldMovedGrids.Remove(component.Grid);
            RemoveGrid(aGrid, oldMapId);
        }

        if (_movedGrids.TryGetValue(mapId, out var newMovedGrids))
        {
            newMovedGrids.Add(component.Grid);
            AddGrid(aGrid, mapId);
        }
    }

    public void OnGridBoundsChange(EntityUid uid, MapGrid grid)
    {
        // Just MapLoader things.
        if (grid.MapProxy == DynamicTree.Proxy.Free) return;

        var xform = EntityManager.GetComponent<TransformComponent>(uid);
        var aabb = GetWorldAABB(grid);
        _gridTrees[xform.MapID].MoveProxy(grid.MapProxy, in aabb, Vector2.Zero);
        _movedGrids[xform.MapID].Add(grid);
    }
}
