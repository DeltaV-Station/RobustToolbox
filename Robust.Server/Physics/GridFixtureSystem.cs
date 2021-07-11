using System.Collections.Generic;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Broadphase;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Dynamics;

namespace Robust.Server.Physics
{
    /// <summary>
    /// Handles generating fixtures for MapGrids.
    /// </summary>
    internal sealed class GridFixtureSystem : EntitySystem
    {
        [Dependency] private readonly IMapManager _mapManager = default!;

        private SharedBroadPhaseSystem _broadphase = default!;

        // Is delaying fixture updates a good idea? IDEK. We definitely can't do them on every tile changed
        // because if someone changes 50 tiles that will kill perf. We could probably just run it every Update
        // (and at specific times due to race condition stuff).
        private float _cooldown;
        private float _accumulator;

        private HashSet<MapChunk> _queuedChunks = new();

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<RegenerateChunkCollisionEvent>(HandleCollisionRegenerate);
            _broadphase = Get<SharedBroadPhaseSystem>();

            var configManager = IoCManager.Resolve<IConfigurationManager>();
            configManager.OnValueChanged(CVars.GridFixtureUpdateRate, value => _cooldown = value, true);
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            _accumulator += frameTime;
            if (_accumulator < _cooldown) return;

            _accumulator -= _cooldown;
            Process();
        }

        public void Process()
        {
            foreach (var chunk in _queuedChunks)
            {
                RegenerateCollision(chunk);
            }

            _queuedChunks.Clear();
        }

        private void HandleCollisionRegenerate(RegenerateChunkCollisionEvent ev)
        {
            if (_cooldown <= 0f)
            {
                RegenerateCollision(ev.Chunk);
                return;
            }

            _queuedChunks.Add(ev.Chunk);
        }

        private void RegenerateCollision(MapChunk chunk)
        {
            // Currently this is gonna be hella simple.
            if (!_mapManager.TryGetGrid(chunk.GridId, out var grid) ||
                !EntityManager.TryGetEntity(grid.GridEntityId, out var gridEnt) ||
                !gridEnt.TryGetComponent(out PhysicsComponent? physicsComponent)) return;

            // TODO: Lots of stuff here etc etc, make changes to mapgridchunk.
            var bounds = chunk.CalcLocalBounds();

            // So something goes on with the chunk's internal bounds caching where if there's no data the bound is 0 or something?
            if (bounds.Bottom == bounds.Top || bounds.Left == bounds.Right) return;

            var origin = chunk.Indices * chunk.ChunkSize;
            bounds = bounds.Translated(origin);

            var oldFixture = chunk.Fixture;

            var newFixture = new Fixture(
                new PolygonShape
                {
                    Vertices = new List<Vector2>
                    {
                        bounds.BottomRight,
                        bounds.TopRight,
                        bounds.TopLeft,
                        bounds.BottomLeft,
                    }
                },
                MapGridHelpers.CollisionGroup,
                MapGridHelpers.CollisionGroup,
                true) {ID = $"grid-{grid.Index}_chunk-{chunk.Indices.X}-{chunk.Indices.Y}"};

            // TODO: Chunk will likely need multiple fixtures but future sloth problem lmao fucking dickhead
            if (oldFixture?.Equals(newFixture) == true) return;

            if (oldFixture != null)
                physicsComponent.RemoveFixture(oldFixture);

            newFixture.Body = physicsComponent;
            physicsComponent.AddFixture(newFixture);
            chunk.Fixture = newFixture;

            EntityManager.EventBus.RaiseLocalEvent(gridEnt.Uid,new GridFixtureChangeEvent {OldFixture = oldFixture, NewFixture = newFixture});
        }
    }

    public sealed class GridFixtureChangeEvent : EntityEventArgs
    {
        public Fixture? OldFixture { get; init; }
        public Fixture? NewFixture { get; init; }
    }
}
