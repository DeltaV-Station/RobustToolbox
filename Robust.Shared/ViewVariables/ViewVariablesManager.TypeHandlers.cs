using System.Collections.Generic;
using System.Linq;
using Robust.Shared.GameObjects;

namespace Robust.Shared.ViewVariables;

internal abstract partial class ViewVariablesManager
{
    private void InitializeTypeHandlers()
    {
        RegisterTypeHandler<EntityUid>(HandleEntityPath, ListEntityPaths);
    }

    private ViewVariablesPath? HandleEntityPath(EntityUid uid, string relativePath)
    {
        if (!_entMan.EntityExists(uid)
            || !_compFact.TryGetRegistration(relativePath, out var registration, true)
            || !_entMan.TryGetComponent(uid, registration.Idx, out var component))
            return null;

        return new ViewVariablesInstancePath(component);
    }

    private string[] ListEntityPaths(EntityUid uid)
    {
        return _entMan.GetComponents(uid)
            .Select(component => _compFact.GetComponentName(component.GetType()))
            .ToArray();
    }
}
