using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Serialization.Manager.Attributes;
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

        [DataField("drawFov")]
        private bool _setDrawFovOnInitialize = true;
        private Vector2 _offset = Vector2.Zero;

        public IEye? Eye => _eye;

        [ViewVariables(VVAccess.ReadWrite)]
        public bool Current
        {
            get => _eyeManager.CurrentEye == _eye;
            set
            {
                if (_eye == null)
                {
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
            get => _eye?.Zoom ?? Vector2.One / 0.5f;
            set
            {
                if (_eye == null)
                {
                    Logger.ErrorS("eye", "Tried to set Zoom for default Eye which doesn't exist!");
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
                UpdateEyePosition();
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
                DrawFov = _setDrawFovOnInitialize,
            };
        }

        public override void HandleComponentState(ComponentState? curState, ComponentState? nextState)
        {
            base.HandleComponentState(curState, nextState);

            if (curState is not EyeComponentState state)
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

        /// <summary>
        /// Updates the Eye of this entity with the transform position. This has to be called every frame to
        /// keep the view following the entity.
        /// </summary>
        public void UpdateEyePosition()
        {
            if (_eye == null) return;
            var mapPos = Owner.Transform.MapPosition;
            _eye.Position = new MapCoordinates(mapPos.Position + _offset, mapPos.MapId);
        }
    }
}
