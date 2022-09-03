using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.IoC.Exceptions;
using Robust.Shared.Maths;
using Robust.Shared.Reflection;
using Robust.Shared.ViewVariables;

namespace Robust.Client.ViewVariables
{
    [UsedImplicitly]
    public sealed class ViewVariablesCommand : IConsoleCommand
    {
        public string Command => "vv";
        public string Description => "Opens View Variables.";
        public string Help => "Usage: vv <entity ID|IoC interface name|SIoC interface name>";

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            var vvm = IoCManager.Resolve<IClientViewVariablesManager>();
            // If you don't provide an entity ID, it opens the test class.
            // Spooky huh.
            if (args.Length == 0)
            {
                vvm.OpenVV(new VVTest());
                return;
            }

            if (argStr.StartsWith("/"))
            {
                var selector = new ViewVariablesPathSelector(argStr);
                vvm.OpenVV(selector);
                return;
            }

            if (argStr.StartsWith("SI"))
            {
                if (argStr.StartsWith("SIoC"))
                    argStr = argStr.Substring(4);

                // Server-side IoC selector.
                var selector = new ViewVariablesIoCSelector(argStr.Substring(1));
                vvm.OpenVV(selector);
                return;
            }

            if (argStr.StartsWith("I"))
            {
                if (argStr.StartsWith("IoC"))
                    argStr = argStr.Substring(3);

                // Client-side IoC selector.
                var reflection = IoCManager.Resolve<IReflectionManager>();
                if (!reflection.TryLooseGetType(argStr, out var type))
                {
                    shell.WriteLine("Unable to find that type.");
                    return;
                }

                object obj;
                try
                {
                    obj = IoCManager.ResolveType(type);
                }
                catch (UnregisteredTypeException)
                {
                    shell.WriteLine("Unable to find that type.");
                    return;
                }
                vvm.OpenVV(obj);
                return;
            }

            // Client side entity system.
            if (argStr.StartsWith("CE"))
            {
                argStr = argStr.Substring(2);
                var reflection = IoCManager.Resolve<IReflectionManager>();

                if (!reflection.TryLooseGetType(argStr, out var type))
                {
                    shell.WriteLine("Unable to find that type.");
                    return;
                }

                vvm.OpenVV(IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem(type));
            }

            if (argStr.StartsWith("SE"))
            {
                // Server-side Entity system selector.
                var selector = new ViewVariablesEntitySystemSelector(argStr.Substring(2));
                vvm.OpenVV(selector);
                return;
            }

            if (argStr.StartsWith("guihover"))
            {
                // UI element.
                var obj = IoCManager.Resolve<IUserInterfaceManager>().CurrentlyHovered;
                if (obj == null)
                {
                    shell.WriteLine("Not currently hovering any control.");
                    return;
                }
                vvm.OpenVV(obj);
                return;
            }

            // Entity.
            if (!EntityUid.TryParse(args[0], out var entity))
            {
                shell.WriteLine("Invalid specifier format.");
                return;
            }

            var entityManager = IoCManager.Resolve<IEntityManager>();
            if (!entityManager.EntityExists(entity))
            {
                shell.WriteLine("That entity does not exist locally. Attempting to open remote view...");
                vvm.OpenVV(new ViewVariablesEntitySelector(entity));
                return;
            }

            vvm.OpenVV(entity);
        }

        /// <summary>
        ///     Test class to test local VV easily without connecting to the server.
        /// </summary>
        private sealed class VVTest : IEnumerable<object>
        {
            [ViewVariables(VVAccess.ReadWrite)] private int x = 10;

            [ViewVariables]
            public Dictionary<object, object> Dict => new() {{"a", "b"}, {"c", "d"}};

            [ViewVariables]
            public List<object> List => new() {1, 2, 3, 4, 5, 6, 7, 8, 9, x, 11, 12, 13, 14, 15, this};

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
