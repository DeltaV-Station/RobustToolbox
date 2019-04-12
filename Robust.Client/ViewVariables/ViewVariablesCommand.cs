using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using JetBrains.Annotations;
using Robust.Client.Interfaces.Console;
using Robust.Client.Interfaces.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Reflection;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.ViewVariables;

namespace Robust.Client.ViewVariables
{
    [UsedImplicitly]
    public class ViewVariablesCommand : IConsoleCommand
    {
        public string Command => "vv";
        public string Description => "Opens View Variables.";
        public string Help => "Usage: vv <entity ID|IoC interface name|SIoC interface name>";

        public bool Execute(IDebugConsole console, params string[] args)
        {
            var vvm = IoCManager.Resolve<IViewVariablesManager>();
            // If you don't provide an entity ID, it opens the test class.
            // Spooky huh.
            if (args.Length == 0)
            {
                vvm.OpenVV(new VVTest());
            }
            else
            {
                var valArg = args[0];
                if (valArg.StartsWith("SI"))
                {
                    var selector = new ViewVariablesIoCSelector(valArg.Substring(1));
                    vvm.OpenVV(selector);
                    return false;
                }

                if (valArg.StartsWith("I"))
                {
                    // IoC name.
                    var type = IoCManager.Resolve<IReflectionManager>().LooseGetType(valArg);
                    var obj = IoCManager.ResolveType(type);
                    vvm.OpenVV(obj);
                    return false;
                }

                if (valArg.StartsWith("gui/"))
                {
                    var obj = IoCManager.Resolve<IUserInterfaceManager>().RootControl;
                    vvm.OpenVV(obj.GetChild(valArg.Substring(4)));
                    return false;
                }

                var entityManager = IoCManager.Resolve<IEntityManager>();
                var uid = EntityUid.Parse(args[0]);
                vvm.OpenVV(entityManager.GetEntity(uid));
            }

            return false;
        }

        /// <summary>
        ///     Test class to test local VV easily without connecting to the server.
        /// </summary>
        private class VVTest : IEnumerable<object>
        {
            [ViewVariables(VVAccess.ReadWrite)] private int x = 10;

            [ViewVariables]
            public Dictionary<object, object> Dict => new Dictionary<object, object> {{"a", "b"}, {"c", "d"}};

            [ViewVariables]
            public List<object> List => new List<object> {1, 2, 3, 4, 5, 6, 7, 8, 9, x, 11, 12, 13, 14, 15};

            [ViewVariables] private Vector2 Vector = (50, 50);

            public IEnumerator<object> GetEnumerator()
            {
                return List.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}
