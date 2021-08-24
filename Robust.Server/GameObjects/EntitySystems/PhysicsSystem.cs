﻿using JetBrains.Annotations;
using Robust.Server.Physics;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Physics;

namespace Robust.Server.GameObjects
{
    [UsedImplicitly]
    public class PhysicsSystem : SharedPhysicsSystem
    {
        [Dependency] private readonly IConfigurationManager _configurationManager = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<GridInitializeEvent>(HandleGridInit);
            LoadMetricCVar();
            _configurationManager.OnValueChanged(CVars.MetricsEnabled, _ => LoadMetricCVar());
        }

        private void LoadMetricCVar()
        {
            MetricsEnabled = _configurationManager.GetCVar(CVars.MetricsEnabled);
        }

        private void HandleGridInit(GridInitializeEvent ev)
        {
            var guid = ev.EntityUid;

            if (!EntityManager.TryGetEntity(guid, out var gridEntity)) return;
            var collideComp = gridEntity.EnsureComponent<PhysicsComponent>();
            collideComp.CanCollide = true;
            collideComp.BodyType = BodyType.Static;
        }

        protected override void HandleMapCreated(object? sender, MapEventArgs eventArgs)
        {
            if (eventArgs.Map == MapId.Nullspace) return;
            MapManager.GetMapEntity(eventArgs.Map).AddComponent<PhysicsMapComponent>();
        }

        /// <inheritdoc />
        public override void Update(float frameTime)
        {
            SimulateWorld(frameTime, false);
        }
    }
}
