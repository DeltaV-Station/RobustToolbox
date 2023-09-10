using Robust.Shared.Collections;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using static Robust.Shared.Containers.ContainerManagerComponent;

namespace Robust.Client.GameObjects
{
    public sealed class ContainerSystem : SharedContainerSystem
    {
        [Dependency] private readonly INetManager _netMan = default!;
        [Dependency] private readonly IRobustSerializer _serializer = default!;
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

            container.Insert(uid);
        }

        private void HandleComponentState(EntityUid uid, ContainerManagerComponent component, ref ComponentHandleState args)
        {
            if (args.Current is not ContainerManagerComponentState cast)
                return;

            var metaQuery = GetEntityQuery<MetaDataComponent>();
            var xformQuery = GetEntityQuery<TransformComponent>();
            var xform = xformQuery.GetComponent(uid);

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
                        xformQuery.GetComponent(entity),
                        metaQuery.GetComponent(entity),
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
                DebugTools.AssertNotNull(stateContainer.ContainedEntities);
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
                var entityUids = GetEntityArray(nents);
                foreach (var entity in container.ContainedEntities)
                {
                    if (!stateContainer.Contains(entity))
                        toRemove.Add(entity);
                }

                foreach (var entity in toRemove)
                {
                    container.Remove(
                        entity,
                        EntityManager,
                        xformQuery.GetComponent(entity),
                        metaQuery.GetComponent(entity),
                        force: true,
                        reparent: false);

                    DebugTools.Assert(!container.Contains(entity));
                }

                // Remove entities that were expected, but have been removed from the container.
                var removedExpected = new ValueList<NetEntity>();
                foreach (var entityUid in container.ExpectedEntities)
                {
                    if (!stateContainer.Contains(entityUid))
                        removedExpected.Add(entityUid);
                }

                foreach (var entityUid in removedExpected)
                {
                    RemoveExpectedEntity(entityUid, out _);
                }

                // Add new entities.
                foreach (var entity in stateContainer.ContainedEntities)
                {
                    var entity = entityUids[i];
                    var netEnt = nents[i];

                    if (!EntityManager.TryGetComponent(entity, out MetaDataComponent? meta))
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

                    if (container.Contains(entity))
                        continue;

                    RemoveExpectedEntity(netEnt, out _);
                    container.Insert(entity, EntityManager,
                        xformQuery.GetComponent(entity),
                        xform,
                        metaQuery.GetComponent(entity),
                        force: true);

                    DebugTools.Assert(container.Contains(entity));
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

        public void AddExpectedEntity(EntityUid uid, BaseContainer container)
        {
#if DEBUG
            var uid = GetEntity(entity);

            if (TryComp<MetaDataComponent>(uid, out var meta))
            {
                DebugTools.Assert((meta.Flags & ( MetaDataFlags.Detached | MetaDataFlags.InContainer) ) == MetaDataFlags.Detached,
                    $"Adding entity {ToPrettyString(uid)} to list of expected entities for container {container.ID} in {ToPrettyString(container.Owner)}, despite it already being in a container.");
            }
#endif

            if (!ExpectedEntities.TryAdd(entity, container))
            {
                // It is possible that we were expecting this entity in one container, but it has now moved to another
                // container, and this entity's state is just being applied before the old container is getting updated.
                var oldContainer = ExpectedEntities[entity];
                ExpectedEntities[entity] = container;
                DebugTools.Assert(oldContainer.ExpectedEntities.Contains(entity),
                    $"Entity {entity} is expected, but not expected in the given container? Container: {oldContainer.ID} in {ToPrettyString(oldContainer.Owner)}");
                oldContainer.ExpectedEntities.Remove(entity);
            }

            DebugTools.Assert(!container.ExpectedEntities.Contains(entity),
                $"Contained entity {entity} was not yet expected by the system, but was already expected by the container: {container.ID} in {ToPrettyString(container.Owner)}");
            container.ExpectedEntities.Add(entity);
        }

        public bool RemoveExpectedEntity(EntityUid uid, [NotNullWhen(true)] out BaseContainer? container)
        {
            if (!ExpectedEntities.Remove(entity, out container))
                return false;

            DebugTools.Assert(container.ExpectedEntities.Contains(entity),
                $"While removing expected contained entity {ToPrettyString(entity)}, the entity was missing from the container expected set. Container: {container.ID} in {ToPrettyString(container.Owner)}");
            container.ExpectedEntities.Remove(entity);
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
