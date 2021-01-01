﻿using System;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.ViewVariables;

namespace Robust.Shared.GameObjects.Components.Map
{
    /// <summary>
    ///     Represents a map grid inside the ECS system.
    /// </summary>
    public interface IMapGridComponent : IComponent
    {
        GridId GridIndex { get; }
        IMapGrid Grid { get; }
        void ClearGridId();
    }

    /// <inheritdoc cref="IMapGridComponent"/>
    [AutoDataClass]
    internal class MapGridComponent : Component, IMapGridComponent
    {
        [Dependency] private readonly IMapManager _mapManager = default!;

        [ViewVariables(VVAccess.ReadOnly)]
        [YamlField("index")]
        private GridId _gridIndex = GridId.Invalid;

        /// <inheritdoc />
        public override string Name => "MapGrid";

        /// <inheritdoc />
        public override uint? NetID => NetIDs.MAP_GRID;

        /// <inheritdoc />
        public GridId GridIndex
        {
            get => _gridIndex;
            internal set => _gridIndex = value;
        }

        /// <inheritdoc />
        [ViewVariables]
        public IMapGrid Grid => _mapManager.GetGrid(_gridIndex);

        public void ClearGridId()
        {
            _gridIndex = GridId.Invalid;
        }

        public override void OnRemove()
        {
            if(GridIndex != GridId.Invalid)
            {
                if(_mapManager.GridExists(_gridIndex))
                {
                    Logger.DebugS("map", $"Entity {Owner.Uid} removed grid component, removing bound grid {_gridIndex}");
                    _mapManager.DeleteGrid(_gridIndex);
                }
            }

            base.OnRemove();
        }

        /// <inheritdoc />
        public override ComponentState GetComponentState()
        {
            return new MapGridComponentState(_gridIndex, Grid.HasGravity);
        }

        /// <inheritdoc />
        public override void HandleComponentState(ComponentState? curState, ComponentState? nextState)
        {
            base.HandleComponentState(curState, nextState);

            if (!(curState is MapGridComponentState state))
                return;

            _gridIndex = state.GridIndex;
            Grid.HasGravity = state.HasGravity;
        }
    }

    /// <summary>
    ///     Serialized state of a <see cref="MapGridComponentState"/>.
    /// </summary>
    [Serializable, NetSerializable]
    internal class MapGridComponentState : ComponentState
    {
        /// <summary>
        ///     Index of the grid this component is linked to.
        /// </summary>
        public GridId GridIndex { get; }

        public bool HasGravity { get; }

        /// <summary>
        ///     Constructs a new instance of <see cref="MapGridComponentState"/>.
        /// </summary>
        /// <param name="gridIndex">Index of the grid this component is linked to.</param>
        public MapGridComponentState(GridId gridIndex, bool hasGravity)
            : base(NetIDs.MAP_GRID)
        {
            GridIndex = gridIndex;
            HasGravity = hasGravity;
        }
    }
}
