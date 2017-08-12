using SS14.Shared.GameObjects;
using SS14.Shared.Interfaces.GameObjects.Components;

namespace SS14.Client.Interfaces.GameObjects.Components
{
    public interface IClientTransformComponent : ITransformComponent
    {
        TransformComponentState lerpStateFrom { get; }
        TransformComponentState lerpStateTo { get; }
    }
}
