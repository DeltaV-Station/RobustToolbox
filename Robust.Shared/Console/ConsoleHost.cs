using System;
using System.Collections.Generic;
using Robust.Shared.Enums;
using Robust.Shared.IoC;
using Robust.Shared.IoC.Exceptions;
using Robust.Shared.Log;
using Robust.Shared.Network;
using Robust.Shared.Players;
using Robust.Shared.Reflection;
using Robust.Shared.Timing;
using Robust.Shared.ViewVariables;

namespace Robust.Shared.Console
{
    /// <inheritdoc />
    public abstract class ConsoleHost : IConsoleHost
    {
        protected const string SawmillName = "con";

        [Dependency] protected readonly ILogManager LogManager = default!;
        [Dependency] private readonly IReflectionManager ReflectionManager = default!;
        [Dependency] protected readonly INetManager NetManager = default!;
        [Dependency] private readonly IDynamicTypeFactoryInternal _typeFactory = default!;
        [Dependency] private readonly IGameTiming _timing = default!;

        [ViewVariables]
        protected readonly Dictionary<string, IConsoleCommand> AvailableCommands = new();

        private readonly CommandBuffer _commandBuffer = new CommandBuffer();

        /// <inheritdoc />
        public bool IsServer => NetManager.IsServer;

        /// <inheritdoc />
        public IConsoleShell LocalShell { get; }

        /// <inheritdoc />
        public IReadOnlyDictionary<string, IConsoleCommand> RegisteredCommands => AvailableCommands;

        public abstract event ConAnyCommandCallback? AnyCommandExecuted;

        protected ConsoleHost()
        {
            LocalShell = new ConsoleShell(this, null);
        }

        /// <inheritdoc />
        public event EventHandler? ClearText;

        /// <inheritdoc />
        public void LoadConsoleCommands()
        {
            // search for all client commands in all assemblies, and register them
            foreach (var type in ReflectionManager.GetAllChildren<IConsoleCommand>())
            {
                var instance = (IConsoleCommand) _typeFactory.CreateInstanceUnchecked(type, true);
                if (RegisteredCommands.TryGetValue(instance.Command, out var duplicate))
                {
                    throw new InvalidImplementationException(instance.GetType(), typeof(IConsoleCommand),
                        $"Command name already registered: {instance.Command}, previous: {duplicate.GetType()}");
                }

                AvailableCommands[instance.Command] = instance;
            }
        }

        /// <inheritdoc />
        public void RegisterCommand(string command, string description, string help, ConCommandCallback callback)
        {
            if (AvailableCommands.ContainsKey(command))
                throw new InvalidOperationException($"Command already registered: {command}");

            var newCmd = new RegisteredCommand(command, description, help, callback);
            AvailableCommands.Add(command, newCmd);
        }

        /// <inheritdoc />
        public void UnregisterCommand(string command)
        {
            if (!AvailableCommands.TryGetValue(command, out var cmd))
                throw new KeyNotFoundException($"Command {command} is not registered.");

            if (cmd is not RegisteredCommand)
                throw new InvalidOperationException("You cannot unregister commands that have been registered automatically.");

            AvailableCommands.Remove(command);
        }

        //TODO: Pull up
        public abstract void ExecuteCommand(ICommonSession? session, string command);

        //TODO: server -> client forwarding, making the system asymmetrical
        public abstract void RemoteExecuteCommand(ICommonSession? session, string command);

        //TODO: IConsoleOutput for [e#1225]
        public abstract void WriteLine(ICommonSession? session, string text);
        public abstract void WriteError(ICommonSession? session, string text);

        /// <inheritdoc />
        public void ClearLocalConsole()
        {
            ClearText?.Invoke(this, EventArgs.Empty);
        }

        /// <inheritdoc />
        public IConsoleShell GetSessionShell(ICommonSession session)
        {
            if (!IsServer)
                return LocalShell;

            if (session.Status >= SessionStatus.Disconnected)
                throw new InvalidOperationException("Tried to get the session shell of a disconnected peer.");

            return new ConsoleShell(this, session);
        }

        /// <inheritdoc />
        public void ExecuteCommand(string command)
        {
            ExecuteCommand(null, command);
        }

        /// <inheritdoc />
        public void AppendCommand(string command)
        {
            _commandBuffer.Append(command);
        }

        /// <inheritdoc />
        public void InsertCommand(string command)
        {
            _commandBuffer.Insert(command);
        }

        /// <inheritdoc />
        public void CommandBufferExecute()
        {
            _commandBuffer.Tick(_timing.TickRate);

            while (_commandBuffer.TryGetCommand(out var cmd))
            {
                try
                {
                    ExecuteCommand(cmd);
                }
                catch (Exception e)
                {
                    LocalShell.WriteError(e.Message);
                }
            }
        }

        /// <summary>
        /// A console command that was registered inline through <see cref="IConsoleHost"/>.
        /// </summary>
        [Reflect(false)]
        public sealed class RegisteredCommand : IConsoleCommand
        {
            public ConCommandCallback Callback { get; }

            /// <inheritdoc />
            public string Command { get; }

            /// <inheritdoc />
            public string Description { get; }

            /// <inheritdoc />
            public string Help { get; }

            /// <summary>
            /// Constructs a new instance of <see cref="RegisteredCommand"/>.
            /// </summary>
            /// <param name="command">Name of the command.</param>
            /// <param name="description">Short description of the command.</param>
            /// <param name="help">Extended description for the command.</param>
            /// <param name="callback">Callback function that is ran when the command is executed.</param>
            public RegisteredCommand(string command, string description, string help, ConCommandCallback callback)
            {
                Command = command;
                // Should these two be localized somehow?
                Description = description;
                Help = help;
                Callback = callback;
            }

            /// <inheritdoc />
            public void Execute(IConsoleShell shell, string argStr, string[] args)
            {
                Callback(shell, argStr, args);
            }
        }
    }
}
