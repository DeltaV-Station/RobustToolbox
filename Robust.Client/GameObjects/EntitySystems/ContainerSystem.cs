using Robust.Shared.Collections;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Utility;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using static Robust.Shared.Containers.ContainerManagerComponent;

namespace Robust.Client.GameObjects
{
    public sealed class ContainerSystem : SharedContainerSystem
    {
        [Dependency] private readonly INetManager _netMan = default!;
        [Dependency] private readonly IDynamicTypeFactoryInternal _dynFactory = default!;
        [Dependency] private readonly PointLightSystem _lightSys = default!;

        private readonly HashSet<EntityUid> _updateQueue = new();

        public readonly Dictionary<NetEntity, BaseContainer> ExpectedEntities = new();

        public override void Initialize()
        {
            base.Initialize();

            EntityManager.EntityInitialized += HandleEntityInitialized;
            SubscribeLocalEvent<ContainerManagerComponent, ComponentHandleState>(HandleComponentState);

            UpdatesBefore.Add(typeof(SpriteSystem));
        }

        public override void Shutdown()
        {
            EntityManager.EntityInitialized -= HandleEntityInitialized;
            base.Shutdown();
        }

        protected override void ValidateMissingEntity(EntityUid uid, BaseContainer cont, EntityUid missing)
        {
            var netEntity = GetNetEntity(missing);
            DebugTools.Assert(ExpectedEntities.TryGetValue(netEntity, out var expectedContainer) && expectedContainer == cont && cont.ExpectedEntities.Contains(netEntity));
        }

        private void HandleEntityInitialized(EntityUid uid)
        {
            if (!RemoveExpectedEntity(GetNetEntity(uid), out var container))
                return;

            container.Insert(uid, EntityManager, transform: TransformQuery.GetComponent(uid), meta: MetaQuery.GetComponent(uid));
        }

        private void HandleComponentState(EntityUid uid, ContainerManagerComponent component, ref ComponentHandleState args)
        {
            if (args.Current is not ContainerManagerComponentState cast)
                return;

            var xform = TransformQuery.GetComponent(uid);

            // Delete now-gone containers.
            var toDelete = new ValueList<string>();
            foreach (var (id, container) in component.Containers)
            {
                if (cast.Containers.TryGetValue(id, out var stateContainer)
                    && stateContainer.GetType() == container.GetType())
                {
                    continue;
                }

                foreach (var entity in container.ContainedEntities.ToArray())
                {
                    container.Remove(entity,
                        EntityManager,
                        TransformQuery.GetComponent(entity),
                        MetaQuery.GetComponent(entity),
                        force: true,
                        reparent: false);

                    DebugTools.Assert(!container.Contains(entity));
                }

                container.Shutdown(EntityManager, _netMan);
                toDelete.Add(id);
            }

            foreach (var dead in toDelete)
            {
                component.Containers.Remove(dead);
            }

            // Add new containers and update existing contents.

            foreach (var (id, stateContainer) in cast.Containers)
            {
                DebugTools.AssertNotNull(stateContainer.ExpectedEntities);
                if (!component.Containers.TryGetValue(id, out var container))
                {
                    container = _dynFactory.CreateInstanceUnchecked<BaseContainer>(stateContainer.GetType(), inject: false);
                    container.Init(id, uid, component);
                    component.Containers.Add(id, container);
                }

                DebugTools.Assert(container.ID == id);
                container.ShowContents = stateContainer.ShowContents;
                container.OccludesLight = stateContainer.OccludesLight;

                // Remove gone entities.
                var toRemove = new ValueList<EntityUid>();

                DebugTools.Assert(!container.Contains(EntityUid.Invalid));

                // No need to ensure entities here.
                var entities = GetEntityList(stateContainer.CompStateEntities);

                foreach (var entity in container.ContainedEntities)
                {
                    if (!entities.Remove(entity))
                    {
                        toRemove.Add(entity);
                    }
                }

                foreach (var entity in toRemove)
                {
                    container.Remove(
                        entity,
                        EntityManager,
                        TransformQuery.GetComponent(entity),
                        MetaQuery.GetComponent(entity),
                        force: true,
                        reparent: false);

                    DebugTools.Assert(!container.Contains(entity));
                }

                // Remove entities that were expected, but have been removed from the container.
                var removedExpected = new ValueList<NetEntity>();
                foreach (var netEntity in container.ExpectedEntities)
                {
                    var entity = GetEntity(netEntity);

                    if (!entities.Contains(entity))
                        removedExpected.Add(netEntity);
                }

                foreach (var entityUid in removedExpected)
                {
                    RemoveExpectedEntity(entityUid, out _);
                }

                // Add new entities.
                for (var i = 0; i < stateContainer.ExpectedEntities.Count; i++)
                {
                    var netEnt = stateContainer.ExpectedEntities[i];

                    if (!TryGetEntity(netEnt, out var entity) || !TryComp<MetaDataComponent>(entity, out var meta))
                    {
                        AddExpectedEntity(netEnt, container);
                        continue;
                    }

                    // If an entity is currently in the shadow realm, it means we probably left PVS and are now getting
                    // back into range. We do not want to directly insert this entity, as IF the container and entity
                    // transform states did not get sent simultaneously, the entity's transform will be modified by the
                    // insert operation. This means it will then be reset to the shadow realm, causing it to be ejected
                    // from the container. It would then subsequently be parented to the container without ever being
                    // re-inserted, leading to the client seeing what should be hidden entities attached to
                    // containers/players.
                    if ((meta.Flags & MetaDataFlags.Detached) != 0)
                    {
                        AddExpectedEntity(netEnt, container);
                        continue;
                    }

                    if (container.Contains(entity.Value))
                        continue;

                    RemoveExpectedEntity(netEnt, out _);
                    container.Insert(entity.Value, EntityManager,
                        TransformQuery.GetComponent(entity.Value),
                        xform,
                        MetaQuery.GetComponent(entity.Value),
                        force: true);

                    DebugTools.Assert(container.Contains(entity.Value));
                }
            }
        }

        protected override void OnParentChanged(ref EntParentChangedMessage message)
        {
            base.OnParentChanged(ref message);

            var xform = message.Transform;

            if (xform.MapID != MapId.Nullspace)
                _updateQueue.Add(message.Entity);

            // If an entity warped in from null-space (i.e., re-entered PVS) and got attached to a container, do the same checks as for newly initialized entities.
            if (message.OldParent != null && message.OldParent.Value.IsValid())
                return;

            if (!RemoveExpectedEntity(GetNetEntity(message.Entity), out var container))
                return;

            if (xform.ParentUid != container.Owner)
            {
                // This container is expecting an entity... but it got parented to some other entity???
                // Ah well, the sever should send a new container state that updates expected entities so just ignore it for now.
                return;
            }

            container.Insert(message.Entity, EntityManager);
        }

        public void AddExpectedEntity(NetEntity netEntity, BaseContainer container)
        {
#if DEBUG
            var uid = GetEntity(netEntity);

            if (TryComp<MetaDataComponent>(uid, out var meta))
            {
                DebugTools.Assert((meta.Flags & ( MetaDataFlags.Detached | MetaDataFlags.InContainer) ) == MetaDataFlags.Detached,
                    $"Adding entity {ToPrettyString(uid)} to list of expected entities for container {container.ID} in {ToPrettyString(container.Owner)}, despite it already being in a container.");
            }
#endif

            if (!ExpectedEntities.TryAdd(netEntity, container))
            {
                // It is possible that we were expecting this entity in one container, but it has now moved to another
                // container, and this entity's state is just being applied before the old container is getting updated.
                var oldContainer = ExpectedEntities[netEntity];
                ExpectedEntities[netEntity] = container;
                DebugTools.Assert(oldContainer.ExpectedEntities.Contains(netEntity),
                    $"Entity {netEntity} is expected, but not expected in the given container? Container: {oldContainer.ID} in {ToPrettyString(oldContainer.Owner)}");
                oldContainer.ExpectedEntities.Remove(netEntity);
            }

            DebugTools.Assert(!container.ExpectedEntities.Contains(netEntity),
                $"Contained entity {netEntity} was not yet expected by the system, but was already expected by the container: {container.ID} in {ToPrettyString(container.Owner)}");
            container.ExpectedEntities.Add(netEntity);
        }

        public bool RemoveExpectedEntity(NetEntity netEntity, [NotNullWhen(true)] out BaseContainer? container)
        {
            if (!ExpectedEntities.Remove(netEntity, out container))
                return false;

            DebugTools.Assert(container.ExpectedEntities.Contains(netEntity),
                $"While removing expected contained entity {ToPrettyString(netEntity)}, the entity was missing from the container expected set. Container: {container.ID} in {ToPrettyString(container.Owner)}");
            container.ExpectedEntities.Remove(netEntity);
            return true;
        }

        public override void FrameUpdate(float frameTime)
        {
            base.FrameUpdate(frameTime);
            var pointQuery = EntityManager.GetEntityQuery<PointLightComponent>();
            var spriteQuery = EntityManager.GetEntityQuery<SpriteComponent>();
            var xformQuery = EntityManager.GetEntityQuery<TransformComponent>();

            foreach (var toUpdate in _updateQueue)
            {
                if (Deleted(toUpdate))
                    continue;

                UpdateEntityRecursively(toUpdate, xformQuery, pointQuery, spriteQuery);
            }

            _updateQueue.Clear();
        }

        private void UpdateEntityRecursively(
            EntityUid entity,
            EntityQuery<TransformComponent> xformQuery,
            EntityQuery<PointLightComponent> pointQuery,
            EntityQuery<SpriteComponent> spriteQuery)
        {
            // Recursively go up parents and containers to see whether both sprites and lights need to be occluded
            // Could maybe optimise this more by checking nearest parent that has sprite / light and whether it's container
            // occluded but this probably isn't a big perf issue.
            var xform = xformQuery.GetComponent(entity);
            var parent = xform.ParentUid;
            var child = entity;
            var spriteOccluded = false;
            var lightOccluded = false;

            while (parent.IsValid() && (!spriteOccluded || !lightOccluded))
            {
                var parentXform = xformQuery.GetComponent(parent);
                if (TryComp<ContainerManagerComponent>(parent, out var manager) && manager.TryGetContainer(child, out var container))
                {
                    spriteOccluded = spriteOccluded || !container.ShowContents;
                    lightOccluded = lightOccluded || container.OccludesLight;
                }

                child = parent;
                parent = parentXform.ParentUid;
            }

            // Alright so
            // This is the CBT bit.
            // The issue is we need to go through the children and re-check whether they are or are not contained.
            // if they are contained then the occlusion values may need updating for all those children
            UpdateEntity(entity, xform, xformQuery, pointQuery, spriteQuery, spriteOccluded, lightOccluded);
        }

        private void UpdateEntity(
            EntityUid entity,
            TransformComponent xform,
            EntityQuery<TransformComponent> xformQuery,
            EntityQuery<PointLightComponent> pointQuery,
            EntityQuery<SpriteComponent> spriteQuery,
            bool spriteOccluded,
            bool lightOccluded)
        {
            if (spriteQuery.TryGetComponent(entity, out var sprite))
            {
                sprite.ContainerOccluded = spriteOccluded;
            }

            if (pointQuery.TryGetComponent(entity, out var light))
                _lightSys.SetContainerOccluded(entity, lightOccluded, light);

            var childEnumerator = xform.ChildEnumerator;

            // Try to avoid TryComp if we already know stuff is occluded.
            if ((!spriteOccluded || !lightOccluded) && TryComp<ContainerManagerComponent>(entity, out var manager))
            {
                while (childEnumerator.MoveNext(out var child))
                {
                    // Thank god it's by value and not by ref.
                    var childSpriteOccluded = spriteOccluded;
                    var childLightOccluded = lightOccluded;

                    // We already know either sprite or light is not occluding so need to check container.
                    if (manager.TryGetContainer(child.Value, out var container))
                    {
                        childSpriteOccluded = childSpriteOccluded || !container.ShowContents;
                        childLightOccluded = childLightOccluded || container.OccludesLight;
                    }

                    UpdateEntity(child.Value, xformQuery.GetComponent(child.Value), xformQuery, pointQuery, spriteQuery, childSpriteOccluded, childLightOccluded);
                }
            }
            else
            {
                while (childEnumerator.MoveNext(out var child))
                {
                    UpdateEntity(child.Value, xformQuery.GetComponent(child.Value), xformQuery, pointQuery, spriteQuery, spriteOccluded, lightOccluded);
                }
            }
        }
    }
}
