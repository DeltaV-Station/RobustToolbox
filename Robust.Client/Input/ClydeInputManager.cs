using Robust.Client.Graphics;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Robust.Client.Input
{
    internal sealed partial class ClydeInputManager : InputManager
    {
        [Dependency] private IClydeInternal _clyde = default!;

        public override ScreenCoordinates MouseScreenPosition => _clyde.MouseScreenPosition;

        public override string GetKeyName(Keyboard.Key key)
        {
            return _clyde.GetKeyName(key);
        }
    }
}
