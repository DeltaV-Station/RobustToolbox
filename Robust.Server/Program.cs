﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Robust.Server.Interfaces;
using Robust.Shared.ContentPack;
using Robust.Shared.Interfaces.Log;
using Robust.Shared.Interfaces.Reflection;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared;
using Robust.Shared.Asynchronous;

namespace Robust.Server
{

    internal static class Program
    {

        private static bool _hasStarted;

        internal static void Main(string[] args)
        {
            Start(args);
        }

        internal static void Start(string[] args, bool contentStart = false)
        {
            if (_hasStarted)
            {
                throw new InvalidOperationException("Cannot start twice!");
            }

            _hasStarted = true;

            if (!CommandLineArgs.TryParse(args, out var parsed))
            {
                return;
            }

            ThreadPool.SetMinThreads(Environment.ProcessorCount * 2, Environment.ProcessorCount);

            new TaskFactory(
                    CancellationToken.None,
                    TaskCreationOptions.LongRunning,
                    TaskContinuationOptions.None,
                    RobustTaskScheduler.Instance
                ).StartNew(() => ParsedMain(parsed, contentStart))
                .GetAwaiter().GetResult();
        }

        private static void ParsedMain(CommandLineArgs args, bool contentStart)
        {
            IoCManager.InitThread();
            ServerIoC.RegisterIoC();
            IoCManager.BuildGraph();
            SetupLogging();
            InitReflectionManager();

            var server = IoCManager.Resolve<IBaseServerInternal>();

            // When the game is ran with the startup executable being content,
            // we have to disable the separate load context.
            // Otherwise the content assemblies will be loaded twice which causes *many* fun bugs.
            server.DisableLoadContext = contentStart;
            server.SetCommandLineArgs(args);

            Logger.Info("Server -> Starting");

            if (server.Start())
            {
                Logger.Fatal("Server -> Can not start server");
                //Not like you'd see this, haha. Perhaps later for logging.
                return;
            }

            string strVersion = Assembly.GetExecutingAssembly().GetName().Version!.ToString();
            Logger.Info("Server Version " + strVersion + " -> Ready");

            IoCManager.Resolve<ISignalHandler>().MaybeStart();

            server.MainLoop();

            Logger.Info("Goodbye.");

            // Used to dispose of systems that want to be disposed.
            // Such as the log manager.
            IoCManager.Clear();
        }

        internal static void InitReflectionManager()
        {
            // gets a handle to the shared and the current (server) dll.
            IoCManager.Resolve<IReflectionManager>().LoadAssemblies(new List<Assembly>(2)
            {
                AppDomain.CurrentDomain.GetAssemblyByName("Robust.Shared"),
                Assembly.GetExecutingAssembly()
            });
        }

        internal static void SetupLogging()
        {
            var mgr = IoCManager.Resolve<ILogManager>();
            var handler = new ConsoleLogHandler();
            mgr.RootSawmill.AddHandler(handler);
            mgr.GetSawmill("res.typecheck").Level = LogLevel.Info;
            mgr.GetSawmill("go.sys").Level = LogLevel.Info;
            mgr.GetSawmill("szr").Level = LogLevel.Info;

#if DEBUG_ONLY_FCE_INFO
#if DEBUG_ONLY_FCE_LOG
            var fce = mgr.GetSawmill("fce");
#endif
            AppDomain.CurrentDomain.FirstChanceException += (sender, args) =>
            {
                // TODO: record FCE stats
#if DEBUG_ONLY_FCE_LOG
                fce.Fatal(message);
#endif
            }
#endif

            var uh = mgr.GetSawmill("unhandled");
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var message = ((Exception) args.ExceptionObject).ToString();
                uh.Log(args.IsTerminating ? LogLevel.Fatal : LogLevel.Error, message);
            };

            var uo = mgr.GetSawmill("unobserved");
            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                uo.Error(args.Exception!.ToString());
#if EXCEPTION_TOLERANCE
                args.SetObserved(); // don't crash
#endif
            };
        }

    }

}
