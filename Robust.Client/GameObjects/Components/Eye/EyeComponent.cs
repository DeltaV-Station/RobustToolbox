﻿using System;
using Robust.Client.Graphics.ClientEye;
using Robust.Client.Interfaces.Graphics.ClientEye;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Components.Eye;
using Robust.Shared.Interfaces.GameObjects.Components;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;
using Robust.Shared.ViewVariables;

namespace Robust.Client.GameObjects
{
    public class EyeComponent : SharedEyeComponent
    {
        [Dependency] private readonly IEyeManager _eyeManager = default!;

        /// <inheritdoc />
        public override string Name => "Eye";

        [ViewVariables]
        private Eye? _eye = default!;

        // Horrible hack to get around ordering issues.
        private bool _setCurrentOnInitialize;
        private bool _setDrawFovOnInitialize;
        private Vector2 _setZoomOnInitialize = Vector2.One/2f;
        private Vector2 _offset = Vector2.Zero;

        public IEye? Eye => _eye;

        private EyeType _eyeType = EyeType.Delayed;

        [ViewVariables(VVAccess.ReadWrite)]
        public bool Current
        {
            get => _eyeManager.CurrentEye == _eye;
            set
            {
                if (_eye == null)
                {
                    _setCurrentOnInitialize = value;
                    return;
                }

                if (_eyeManager.CurrentEye == _eye == value)
                    return;

                if (value)
                {
                    _eyeManager.CurrentEye = _eye;
                }
                else
                {
                    _eyeManager.ClearCurrentEye();
                }
            }
        }

        public override Vector2 Zoom
        {
            get => _eye?.Zoom ?? _setZoomOnInitialize;
            set
            {
                if (_eye == null)
                {
                    _setZoomOnInitialize = value;
                }
                else
                {
                    _eye.Zoom = value;
                }
            }
        }

        public override Angle Rotation
        {
            get => _eye?.Rotation ?? Angle.Zero;
            set
            {
                if (_eye != null)
                    _eye.Rotation = value;
            }
        }

        public override Vector2 Offset
        {
            get => _offset;
            set
            {
                if(_offset.EqualsApprox(value))
                    return;

                _offset = value;
            }
        }

        public override bool DrawFov
        {
            get => _eye?.DrawFov ?? _setDrawFovOnInitialize;
            set
            {
                if (_eye == null)
                {
                    _setDrawFovOnInitialize = value;
                }
                else
                {
                    _eye.DrawFov = value;
                }
            }
        }

        [ViewVariables]
        public MapCoordinates? Position => _eye?.Position;

        /// <inheritdoc />
        public override void Initialize()
        {
            base.Initialize();

            _eye = new Eye
            {
                Position = Owner.Transform.MapPosition,
                Zoom = _setZoomOnInitialize,
                DrawFov = _setDrawFovOnInitialize
            };

            if ((_eyeManager.CurrentEye == _eye) != _setCurrentOnInitialize)
            {
                if (_setCurrentOnInitialize)
                {
                    _eyeManager.ClearCurrentEye();
                }
                else
                {
                    _eyeManager.CurrentEye = _eye;
                }
            }
        }

        public override void HandleComponentState(ComponentState? curState, ComponentState? nextState)
        {
            base.HandleComponentState(curState, nextState);

            if (!(curState is EyeComponentState state))
            {
                return;
            }

            DrawFov = state.DrawFov;
            Zoom = state.Zoom;
            Offset = state.Offset;
            Rotation = state.Rotation;
        }

        public override void OnRemove()
        {
            base.OnRemove();

            Current = false;
        }

        /// <inheritdoc />
        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);

            serializer.DataFieldCached(ref _setZoomOnInitialize, "zoom", Vector2.One/2f);
            serializer.DataFieldCached(ref _setDrawFovOnInitialize, "drawFov", true);
        }

        /// <summary>
        /// Updates the Eye of this entity with the transform position. This has to be called every frame to
        /// keep the view following the entity.
        /// </summary>
        public void UpdateEyePosition(float frameTime)
        {
            if (_eye == null) return;

            var mapPos = Owner.Transform.MapPosition;

            switch (_eyeType)
            {
                case EyeType.Delayed:
                    _eye.Position = new MapCoordinates(_eye.Position.Position + (mapPos.Position - _eye.Position.Position) * 5f * frameTime, mapPos.MapId);
                    break;
                case EyeType.Fixed:
                    _eye.Position = new MapCoordinates(mapPos.Position + _offset, mapPos.MapId);
                    break;
                default:
                    throw new NotImplementedException($"No EyeType implemented for {_eyeType}");
            }
        }
    }

    public enum EyeType
    {
        /// <summary>
        ///     Follows the tracked-entity exactly
        /// </summary>
        Fixed,

        /// <summary>
        ///     Smooths the camera between its current position and the entity
        /// </summary>
        Delayed,
    }
}
