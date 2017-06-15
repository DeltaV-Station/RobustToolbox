using System;
using System.Collections.Generic;
using System.Reflection;
using SS14.Shared.IoC;
using SS14.Shared.Utility;
using SS14.Shared.ContentLoader;
using SS14.Shared.Log;
using SS14.Client.Interfaces;

namespace SS14.Client
{
    public class Program
    {
        /************************************************************************/
        /* program starts here                                                  */
        /************************************************************************/

        [STAThread]
        private static void Main()
        {
            LoadAssemblies();

            var controller = IoCManager.Resolve<IGameController>();
            controller.Run();

            Logger.Info("Goodbye.");
            IoCManager.Clear();
        }

        private static void LoadAssemblies()
        {
            var assemblies = new List<Assembly>(2)
            {
                AppDomain.CurrentDomain.GetAssemblyByName("SS14.Shared"),
                Assembly.GetExecutingAssembly()
            };
            IoCManager.AddAssemblies(assemblies);

            assemblies.Clear();

            // So we can't actually access this until IoC has loaded the initial assemblies. Yay.
            var loader = IoCManager.Resolve<IContentLoader>();
            assemblies.Clear();

            // TODO this should be done on connect.
            // The issue is that due to our giant trucks of shit code.
            // It'd be extremely hard to integrate correctly.
            try
            {
                var contentAssembly = AssemblyHelpers.RelativeLoadFrom("SS14.Shared.Content.dll");
                loader.LoadAssembly(contentAssembly);
                assemblies.Add(contentAssembly);
            }
            catch (Exception e)
            {
                // LogManager won't work yet.
                System.Console.ForegroundColor = ConsoleColor.Red;
                System.Console.WriteLine("**ERROR: Unable to load the shared content assembly (SS14.Shared.Content.dll): {0}", e);
                System.Console.ResetColor();
            }

            try
            {
                var contentAssembly = AssemblyHelpers.RelativeLoadFrom("SS14.Server.Content.dll");
                loader.LoadAssembly(contentAssembly);
                assemblies.Add(contentAssembly);
            }
            catch (Exception e)
            {
                // LogManager won't work yet.
                System.Console.ForegroundColor = ConsoleColor.Red;
                System.Console.WriteLine("**ERROR: Unable to load the server content assembly (SS14.Server.Content.dll): {0}", e);
                System.Console.ResetColor();
            }

            IoCManager.AddAssemblies(assemblies);
        }
    }
}
