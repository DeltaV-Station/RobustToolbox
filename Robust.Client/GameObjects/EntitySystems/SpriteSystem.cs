using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Robust.Client.ComponentTrees;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Robust.Client.GameObjects
{
    /// <summary>
    /// Updates the layer animation for every visible sprite.
    /// </summary>
    [UsedImplicitly]
    public sealed partial class SpriteSystem : EntitySystem
    {
        [Dependency] private readonly IConfigurationManager _cfg = default!;
        [Dependency] private readonly IEyeManager _eyeManager = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly IPrototypeManager _proto = default!;
        [Dependency] private readonly IResourceCache _resourceCache = default!;
        [Dependency] private readonly SpriteTreeSystem _treeSystem = default!;

        private readonly Queue<SpriteComponent> _inertUpdateQueue = new();
        private readonly HashSet<SpriteComponent> _manualUpdate = new();

        private TimeSpan _syncTime;

        public override void Initialize()
        {
            base.Initialize();

            UpdatesAfter.Add(typeof(SpriteTreeSystem));

            _proto.PrototypesReloaded += OnPrototypesReloaded;
            SubscribeLocalEvent<SpriteComponent, SpriteUpdateInertEvent>(QueueUpdateInert);
            SubscribeLocalEvent<SpriteComponent, ComponentInit>(OnInit);

            _cfg.OnValueChanged(CVars.RenderSpriteDirectionBias, OnBiasChanged, true);
        }

        private void OnInit(EntityUid uid, SpriteComponent component, ComponentInit args)
        {
            // I'm not 100% this is needed, but I CBF with this ATM. Somebody kill server sprite component please.
            QueueUpdateInert(uid, component);
        }

        public override void Shutdown()
        {
            base.Shutdown();
            _proto.PrototypesReloaded -= OnPrototypesReloaded;
            _cfg.UnsubValueChanged(CVars.RenderSpriteDirectionBias, OnBiasChanged);
        }

        private void OnBiasChanged(double value)
        {
            SpriteComponent.DirectionBias = value;
        }

        private void QueueUpdateInert(EntityUid uid, SpriteComponent sprite, ref SpriteUpdateInertEvent ev)
            => QueueUpdateInert(uid, sprite);

        public void QueueUpdateInert(EntityUid uid, SpriteComponent sprite)
        {
            if (sprite._inertUpdateQueued)
                return;

            sprite._inertUpdateQueued = true;
            _inertUpdateQueue.Enqueue(sprite);
        }

        /// <inheritdoc />
        public override void FrameUpdate(float frameTime)
        {
            _syncTime += TimeSpan.FromSeconds(frameTime);

            while (_inertUpdateQueue.TryDequeue(out var sprite))
            {
                sprite.DoUpdateIsInert();
            }

            foreach (var sprite in _manualUpdate)
            {
                if (!sprite.Deleted && !sprite.IsInert)
                    sprite.FrameUpdate(frameTime);
            }

            var pvsBounds = _eyeManager.GetWorldViewbounds();

            var currentMap = _eyeManager.CurrentMap;
            if (currentMap == MapId.Nullspace)
            {
                return;
            }

            var syncQuery = GetEntityQuery<SyncSpriteComponent>();
            var spriteState = (frameTime, _manualUpdate, syncQuery, this);

            _treeSystem.QueryAabb( ref spriteState, static (ref (
                    float frameTime,
                    HashSet<SpriteComponent> _manualUpdate,
                    EntityQuery<SyncSpriteComponent> syncQuery,
                    SpriteSystem system) tuple,
                in ComponentTreeEntry<SpriteComponent> value) =>
                {
                    if (value.Component.IsInert)
                        return true;

                    // So the reason this is here is because we only update animations inside of our viewport(s)
                    // The problem with this is if something comes in pvs range but may not necessarily be in our viewport
                    // then the animation doesn't update. This means the synced sprites can desync before we see them.
                    // However, we can't just have all of our animations update in pvs range in case of situations
                    // where pvs is off (and update every single sprite ever),
                    // hence we just query for every sprite and ensure its timing is correct.
                    if (tuple.syncQuery.HasComponent(value.Uid))
                    {
                        tuple.system.SetAutoAnimateSync(value.Component);
                    }

                    if (!tuple._manualUpdate.Contains(value.Component))
                        value.Component.FrameUpdate(tuple.frameTime);

                    return true;
                }, currentMap, pvsBounds, true);

            _manualUpdate.Clear();
        }

        /// <summary>
        ///     Force update of the sprite component next frame
        /// </summary>
        public void ForceUpdate(SpriteComponent sprite)
        {
            _manualUpdate.Add(sprite);
        }
    }

    /// <summary>
    ///     This event gets raised before a sprite gets drawn using it's post-shader.
    /// </summary>
    public sealed class BeforePostShaderRenderEvent : EntityEventArgs
    {
        public readonly SpriteComponent Sprite;
        public readonly IClydeViewport Viewport;

        public BeforePostShaderRenderEvent(SpriteComponent sprite, IClydeViewport viewport)
        {
            Sprite = sprite;
            Viewport = viewport;
        }
    }
}
