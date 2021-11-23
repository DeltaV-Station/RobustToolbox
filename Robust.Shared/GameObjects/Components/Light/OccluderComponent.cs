using System;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;
using Robust.Shared.Players;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;

namespace Robust.Shared.GameObjects
{
    [NetworkedComponent()]
    public class OccluderComponent : Component
    {
        public sealed override string Name => "Occluder";

        [DataField("enabled")]
        private bool _enabled = true;
        [DataField("boundingBox")]
        private Box2 _boundingBox = new(-0.5f, -0.5f, 0.5f, 0.5f);

        internal OccluderTreeComponent? Tree = null;

        [ViewVariables(VVAccess.ReadWrite)]
        public Box2 BoundingBox
        {
            get => _boundingBox;
            set
            {
                _boundingBox = value;
                Dirty();
                Owner.EntityManager.EventBus.RaiseLocalEvent(Owner.Uid, new OccluderUpdateEvent(this));
            }
        }

        [ViewVariables(VVAccess.ReadWrite)]
        public virtual bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled == value)
                    return;

                _enabled = value;
                if (_enabled)
                {
                    Owner.EntityManager.EventBus.RaiseLocalEvent(Owner.Uid, new OccluderAddEvent(this));
                }
                else
                {
                    Owner.EntityManager.EventBus.RaiseLocalEvent(Owner.Uid, new OccluderRemoveEvent(this));
                }

                Dirty();
            }
        }

        public override ComponentState GetComponentState()
        {
            return new OccluderComponentState(Enabled, BoundingBox);
        }

        public override void HandleComponentState(ComponentState? curState, ComponentState? nextState)
        {
            if (curState == null)
            {
                return;
            }

            var cast = (OccluderComponentState) curState;

            Enabled = cast.Enabled;
            BoundingBox = cast.BoundingBox;
        }

        [NetSerializable, Serializable]
        private sealed class OccluderComponentState : ComponentState
        {
            public bool Enabled { get; }
            public Box2 BoundingBox { get; }

            public OccluderComponentState(bool enabled, Box2 boundingBox)
            {
                Enabled = enabled;
                BoundingBox = boundingBox;
            }
        }
    }
}
