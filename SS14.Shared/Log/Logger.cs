﻿using SS14.Shared.Interfaces.Log;
using SS14.Shared.IoC;

namespace SS14.Shared.Log
{
    /// <summary>
    /// Static logging API front end. This is here as a convenience to prevent the need to resolve the manager manually.
    /// </summary>
    /// <remarks>
    /// This type is simply a proxy to an IoC based <see cref="ILogManager"/>.
    /// As such, no methods or properties will work if IoC has not been initialized yet.
    /// </remarks>
    /// <seealso cref="ILogManager"/>
    /// <seealso cref="LogManager"/>
    public static class Logger
    {
        /// <summary>
        /// The instance we're using. As it's a direct proxy to IoC this will not work if IoC is not functional.
        /// </summary>
        private static ILogManager LogManagerSingleton => IoCManager.Resolve<ILogManager>();

        public static ISawmill GetSawmill(string name)
        {
            return LogManagerSingleton.GetSawmill(name);
        }

        /// <summary>
        /// Log a message, taking in a format string and format list using the regular <see cref="string.Format" /> syntax.
        /// </summary>
        public static void Log(string sawmillname, string message, LogLevel logLevel, params object[] args)
        {
            var sawmill = LogManagerSingleton.GetSawmill(sawmillname);
            sawmill.Log(message, logLevel, args);
        }

        /// <summary>
        /// Log a message as debug, taking in a format string and format list using the regular <see cref="string.Format" /> syntax.
        /// </summary>
        /// <seealso cref="Log" />
        public static void Debug(string sawmill, string message, params object[] args) => Log(sawmill, message, LogLevel.Debug, args);

        /// <summary>
        /// Log a message as info, taking in a format string and format list using the regular <see cref="string.Format" /> syntax.
        /// </summary>
        /// <seealso cref="Log" />
        public static void Info(string sawmill, string message, params object[] args) => Log(sawmill, message, LogLevel.Info, args);

        /// <summary>
        /// Log a message as warning, taking in a format string and format list using the regular <see cref="string.Format" /> syntax.
        /// </summary>
        /// <seealso cref="Log" />
        public static void Warning(string sawmill, string message, params object[] args) => Log(sawmill, message, LogLevel.Warning, args);

        /// <summary>
        /// Log a message as error, taking in a format string and format list using the regular <see cref="string.Format" /> syntax.
        /// </summary>
        /// <seealso cref="Log" />
        public static void Error(string sawmill, string message, params object[] args) => Log(sawmill, message, LogLevel.Error, args);

        /// <summary>
        /// Log a message as fatal, taking in a format string and format list using the regular <see cref="string.Format" /> syntax.
        /// </summary>
        /// <seealso cref="Log" />
        public static void Fatal(string sawmill, string message, params object[] args) => Log(sawmill, message, LogLevel.Fatal, args);
    }
}
