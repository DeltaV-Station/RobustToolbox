using System.Collections.Generic;
using JetBrains.Annotations;
using Robust.Client.Physics;
using Robust.Shared.GameObjects.Components.Transform;
using Robust.Shared.GameObjects.EntitySystemMessages;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics;

namespace Robust.Client.GameObjects.EntitySystems
{
    /// <summary>
    ///     Keeps track of <see cref="DynamicTree{T}"/>s for various rendering-related components.
    /// </summary>
    [UsedImplicitly]
    public sealed class RenderingTreeSystem : EntitySystem
    {
        // Nullspace is not indexed. Keep that in mind.

        [Dependency] private readonly IMapManagerInternal _mapManager = default!;

        private readonly Dictionary<MapId, MapTrees> _mapTrees = new Dictionary<MapId, MapTrees>();

        private readonly List<SpriteComponent> _spriteQueue = new List<SpriteComponent>();
        private readonly List<ClientOccluderComponent> _occluderQueue = new List<ClientOccluderComponent>();
        private readonly List<PointLightComponent> _lightQueue = new List<PointLightComponent>();

        internal DynamicTree<SpriteComponent> GetSpriteTreeForMap(MapId map)
        {
            return _mapTrees[map].SpriteTree;
        }

        internal DynamicTree<ClientOccluderComponent> GetOccluderTreeForMap(MapId map)
        {
            return _mapTrees[map].OccluderTree;
        }

        internal DynamicTree<PointLightComponent> GetLightTreeForMap(MapId map)
        {
            return _mapTrees[map].LightTree;
        }

        public override void Initialize()
        {
            base.Initialize();

            UpdatesBefore.Add(typeof(SpriteSystem));
            UpdatesAfter.Add(typeof(TransformSystem));
            UpdatesAfter.Add(typeof(PhysicsSystem));

            _mapManager.MapCreated += MapManagerOnMapCreated;
            _mapManager.MapDestroyed += MapManagerOnMapDestroyed;

            SubscribeLocalEvent<EntMapIdChangedMessage>(EntMapIdChanged);
            SubscribeLocalEvent<MoveEvent>(EntMoved);
            SubscribeLocalEvent<EntParentChangedMessage>(EntParentChanged);
            SubscribeLocalEvent<OccluderBoundingBoxChangedMessage>(OccluderBoundingBoxChanged);
            SubscribeLocalEvent<PointLightRadiusChangedMessage>(PointLightRadiusChanged);
            SubscribeLocalEvent<RenderTreeRemoveSpriteMessage>(RemoveSprite);
            SubscribeLocalEvent<RenderTreeRemoveLightMessage>(RemoveLight);
            SubscribeLocalEvent<RenderTreeRemoveOccluderMessage>(RemoveOccluder);
        }

        // For these next 3 methods (the Remove* ones):
        // If the Transform is removed BEFORE the Sprite/Light/Occluder,
        // then the MapIdChanged code will handle and remove it (because MapId gets set to nullspace).
        // Otherwise these will still have their past MapId and that's all we need..
        private void RemoveOccluder(RenderTreeRemoveOccluderMessage ev)
        {
            _mapTrees[ev.Map].OccluderTree.Remove(ev.Occluder);
        }

        private void RemoveLight(RenderTreeRemoveLightMessage ev)
        {
            _mapTrees[ev.Map].LightTree.Remove(ev.Light);
        }

        private void RemoveSprite(RenderTreeRemoveSpriteMessage ev)
        {
            _mapTrees[ev.Map].SpriteTree.Remove(ev.Sprite);
        }

        private void PointLightRadiusChanged(PointLightRadiusChangedMessage ev)
        {
            QueueUpdateLight(ev.PointLightComponent);
        }

        private void OccluderBoundingBoxChanged(OccluderBoundingBoxChangedMessage ev)
        {
            QueueUpdateOccluder(ev.Occluder);
        }

        private void EntParentChanged(EntParentChangedMessage ev)
        {
            UpdateEntity(ev.Entity);
        }

        private void EntMoved(MoveEvent ev)
        {
            UpdateEntity(ev.Sender);
        }

        private void UpdateEntity(IEntity entity)
        {
            if (entity.TryGetComponent(out SpriteComponent spriteComponent))
            {
                if (!spriteComponent.TreeUpdateQueued)
                {
                    spriteComponent.TreeUpdateQueued = true;

                    _spriteQueue.Add(spriteComponent);
                }
            }

            if (entity.TryGetComponent(out ClientOccluderComponent occluder))
            {
                QueueUpdateOccluder(occluder);
            }

            if (entity.TryGetComponent(out PointLightComponent light))
            {
                QueueUpdateLight(light);
            }

            foreach (var child in entity.Transform.Children)
            {
                UpdateEntity(child.Owner);
            }
        }

        private void QueueUpdateLight(PointLightComponent light)
        {
            if (!light.TreeUpdateQueued)
            {
                light.TreeUpdateQueued = true;

                _lightQueue.Add(light);
            }
        }

        private void QueueUpdateOccluder(ClientOccluderComponent occluder)
        {
            if (!occluder.TreeUpdateQueued)
            {
                occluder.TreeUpdateQueued = true;

                _occluderQueue.Add(occluder);
            }
        }

        private void EntMapIdChanged(EntMapIdChangedMessage ev)
        {
            // Nullspace is a valid map ID for stuff to have but we also aren't gonna bother indexing it.
            // So that's why there's a GetValueOrDefault.
            var oldMapTrees = _mapTrees.GetValueOrDefault(ev.OldMapId);
            var newMapTrees = _mapTrees.GetValueOrDefault(ev.Entity.Transform.MapID);

            if (ev.Entity.TryGetComponent(out SpriteComponent sprite))
            {
                oldMapTrees?.SpriteTree.Remove(sprite);

                newMapTrees?.SpriteTree.AddOrUpdate(sprite);
            }

            if (ev.Entity.TryGetComponent(out ClientOccluderComponent occluder))
            {
                oldMapTrees?.OccluderTree.Remove(occluder);

                newMapTrees?.OccluderTree.AddOrUpdate(occluder);
            }

            if (ev.Entity.TryGetComponent(out PointLightComponent light))
            {
                oldMapTrees?.LightTree.Remove(light);

                newMapTrees?.LightTree.AddOrUpdate(light);
            }
        }

        private void MapManagerOnMapDestroyed(object? sender, MapEventArgs e)
        {
            _mapTrees.Remove(e.Map);
        }

        private void MapManagerOnMapCreated(object? sender, MapEventArgs e)
        {
            if (e.Map == MapId.Nullspace)
            {
                return;
            }

            _mapTrees.Add(e.Map, new MapTrees());
        }

        public override void FrameUpdate(float frameTime)
        {
            foreach (var queuedUpdateSprite in _spriteQueue)
            {
                var transform = queuedUpdateSprite.Owner.Transform;
                var map = transform.MapID;
                if (map == MapId.Nullspace)
                {
                    continue;
                }
                var updateMapTree = _mapTrees[map].SpriteTree;

                updateMapTree.AddOrUpdate(queuedUpdateSprite);
                queuedUpdateSprite.TreeUpdateQueued = false;
            }

            foreach (var queuedUpdateLight in _lightQueue)
            {
                var transform = queuedUpdateLight.Owner.Transform;
                var map = transform.MapID;
                if (map == MapId.Nullspace)
                {
                    continue;
                }
                var updateMapTree = _mapTrees[map].LightTree;

                updateMapTree.AddOrUpdate(queuedUpdateLight);
                queuedUpdateLight.TreeUpdateQueued = false;
            }

            foreach (var queuedUpdateOccluder in _occluderQueue)
            {
                var transform = queuedUpdateOccluder.Owner.Transform;
                var map = transform.MapID;
                if (map == MapId.Nullspace)
                {
                    continue;
                }
                var updateMapTree = _mapTrees[map].OccluderTree;

                updateMapTree.AddOrUpdate(queuedUpdateOccluder);
                queuedUpdateOccluder.TreeUpdateQueued = false;
            }

            _spriteQueue.Clear();
            _lightQueue.Clear();
            _occluderQueue.Clear();
        }

        private sealed class MapTrees
        {
            public readonly DynamicTree<SpriteComponent> SpriteTree;
            public readonly DynamicTree<PointLightComponent> LightTree;
            public readonly DynamicTree<ClientOccluderComponent> OccluderTree;

            public MapTrees()
            {
                SpriteTree = new DynamicTree<SpriteComponent>(SpriteAabbFunc);
                LightTree = new DynamicTree<PointLightComponent>(LightAabbFunc);
                OccluderTree = new DynamicTree<ClientOccluderComponent>(OccluderAabbFunc);
            }

            private static Box2 SpriteAabbFunc(in SpriteComponent value)
            {
                var worldPos = value.Owner.Transform.WorldPosition;

                return new Box2(worldPos, worldPos);
            }

            private static Box2 LightAabbFunc(in PointLightComponent value)
            {
                var worldPos = value.Owner.Transform.WorldPosition;

                var boxSize = value.Radius * 2;
                return Box2.CenteredAround(worldPos, (boxSize, boxSize));
            }

            private static Box2 OccluderAabbFunc(in ClientOccluderComponent value)
            {
                var worldPos = value.Owner.Transform.WorldPosition;

                return value.BoundingBox.Translated(worldPos);
            }
        }
    }

    internal struct RenderTreeRemoveSpriteMessage
    {
        public RenderTreeRemoveSpriteMessage(SpriteComponent sprite, MapId map)
        {
            Sprite = sprite;
            Map = map;
        }

        public SpriteComponent Sprite { get; }
        public MapId Map { get; }
    }

    internal struct RenderTreeRemoveLightMessage
    {
        public RenderTreeRemoveLightMessage(PointLightComponent light, MapId map)
        {
            Light = light;
            Map = map;
        }

        public PointLightComponent Light { get; }
        public MapId Map { get; }
    }

    internal struct RenderTreeRemoveOccluderMessage
    {
        public RenderTreeRemoveOccluderMessage(ClientOccluderComponent occluder, MapId map)
        {
            Occluder = occluder;
            Map = map;
        }

        public ClientOccluderComponent Occluder { get; }
        public MapId Map { get; }
    }
}
