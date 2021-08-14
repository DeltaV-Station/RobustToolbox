using System;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;
using Robust.Shared.ViewVariables;

namespace Robust.Shared.GameObjects
{
    [NetworkedComponent()]
    public class SharedEyeComponent : Component
    {
        public override string Name => "Eye";

        [ViewVariables(VVAccess.ReadWrite)]
        public virtual bool DrawFov { get; set; }

        [ViewVariables(VVAccess.ReadWrite)]
        public virtual Vector2 Zoom { get; set; }

        [ViewVariables(VVAccess.ReadWrite)]
        public virtual Vector2 Offset { get; set; }

        [ViewVariables(VVAccess.ReadWrite)]
        public virtual Angle Rotation { get; set; }
        
        /// <summary>
        ///     The visibility mask for this eye.
        ///     The player will be able to get updates for entities whose layers match the mask.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        public virtual uint VisibilityMask { get; set; }
    }

    [NetSerializable, Serializable]
    public class EyeComponentState : ComponentState
    {
        public bool DrawFov { get; }
        public Vector2 Zoom { get; }
        public Vector2 Offset { get; }
        public Angle Rotation { get; }
        public uint VisibilityMask { get; }

        public EyeComponentState(bool drawFov, Vector2 zoom, Vector2 offset, Angle rotation, uint visibilityMask)
        {
            DrawFov = drawFov;
            Zoom = zoom;
            Offset = offset;
            Rotation = rotation;
            VisibilityMask = visibilityMask;
        }
    }
}
