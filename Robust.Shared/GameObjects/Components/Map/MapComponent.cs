using System;
using Robust.Shared.GameStates;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;

namespace Robust.Shared.GameObjects
{
    /// <summary>
    ///     Represents a world map inside the ECS system.
    /// </summary>
    public interface IMapComponent : IComponent
    {
        bool LightingEnabled { get; set; }
        MapId WorldMap { get; }
        bool MapPaused { get; internal set; }
        bool MapPreInit { get; internal set; }
    }

    /// <inheritdoc cref="IMapComponent"/>
    [ComponentReference(typeof(IMapComponent))]
    [NetworkedComponent]
    public sealed class MapComponent : Component, IMapComponent
    {
        [Dependency] private readonly IEntityManager _entMan = default!;

        [ViewVariables(VVAccess.ReadOnly)]
        [DataField("index")]
        private MapId _mapIndex = MapId.Nullspace;

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField(("lightingEnabled"))]
        public bool LightingEnabled { get; set; } = true;

        /// <inheritdoc />
        public MapId WorldMap
        {
            get => _mapIndex;
            internal set => _mapIndex = value;
        }

        internal bool MapPaused { get; set; } = false;

        /// <inheritdoc />
        bool IMapComponent.MapPaused
        {
            get => this.MapPaused;
            set => this.MapPaused = value;
        }

        internal bool MapPreInit { get; set; } = false;

        /// <inheritdoc />
        bool IMapComponent.MapPreInit
        {
            get => this.MapPreInit;
            set => this.MapPreInit = value;
        }

        /// <inheritdoc />
        public override ComponentState GetComponentState()
        {
            return new MapComponentState(_mapIndex, LightingEnabled);
        }

        /// <inheritdoc />
        public override void HandleComponentState(ComponentState? curState, ComponentState? nextState)
        {
            base.HandleComponentState(curState, nextState);

            if (curState is not MapComponentState state)
                return;

            _mapIndex = state.MapId;
            LightingEnabled = state.LightingEnabled;
            var xformQuery = _entMan.GetEntityQuery<TransformComponent>();

            xformQuery.GetComponent(Owner).ChangeMapId(_mapIndex, xformQuery);
        }
    }

    /// <summary>
    ///     Serialized state of a <see cref="MapGridComponentState"/>.
    /// </summary>
    [Serializable, NetSerializable]
    internal sealed class MapComponentState : ComponentState
    {
        public MapId MapId { get; }
        public bool LightingEnabled { get; }

        public MapComponentState(MapId mapId, bool lightingEnabled)
        {
            MapId = mapId;
            LightingEnabled = lightingEnabled;
        }
    }
}
