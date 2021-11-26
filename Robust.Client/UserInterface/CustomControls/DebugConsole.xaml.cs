using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Robust.Client.AutoGenerated;
using Robust.Client.Console;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.ContentPack;
using Robust.Shared.Input;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Robust.Client.UserInterface.CustomControls
{
    public interface IDebugConsoleView
    {
        /// <summary>
        /// Write a line with a specific color to the console window.
        /// </summary>
        void AddLine(string text, Color color);

        void AddLine(string text);

        void AddFormattedLine(FormattedMessage message);

        void Clear();
    }

    // Quick note on how thread safety works in here:
    // Messages from other threads are not actually immediately drawn. They're stored in a queue.
    // Every frame OR the next time a message on the main thread comes in, this queue is drained.
    // This keeps thread safety while still making it so messages are ordered how they come in.
    // And also if Update() stops firing due to an exception loop the console will still work.
    // (At least from the main thread, which is what's throwing the exceptions..)
    [GenerateTypedNameReferences]
    public sealed partial class DebugConsole : Control, IDebugConsoleView
    {
        [Dependency] private readonly IClientConsoleHost _consoleHost = default!;
        [Dependency] private readonly IResourceManager _resourceManager = default!;

        private static readonly ResourcePath HistoryPath = new("/debug_console_history.json");

        private readonly ConcurrentQueue<FormattedMessage> _messageQueue = new();

        private bool commandChanged = true;
        private readonly List<string> searchResults;
        private int searchIndex = 0;

        public DebugConsole()
        {
            RobustXamlLoader.Load(this);

            IoCManager.InjectDependencies(this);

            CommandBar.OnTextChanged += OnCommandChanged;
            CommandBar.OnKeyBindDown += CommandBarOnOnKeyBindDown;
            CommandBar.OnTextEntered += CommandEntered;
            CommandBar.OnHistoryChanged += OnHistoryChanged;

            _loadHistoryFromDisk();

            searchResults = new List<string>();
        }

        protected override void EnteredTree()
        {
            base.EnteredTree();

            _consoleHost.AddString += OnAddString;
            _consoleHost.AddFormatted += OnAddFormatted;
            _consoleHost.ClearText += OnClearText;
        }

        protected override void ExitedTree()
        {
            base.ExitedTree();

            _consoleHost.AddString -= OnAddString;
            _consoleHost.AddFormatted -= OnAddFormatted;
            _consoleHost.ClearText -= OnClearText;
        }

        private void OnClearText(object? _, EventArgs args)
        {
            Clear();
        }

        private void OnAddFormatted(object? _, AddFormattedMessageArgs args)
        {
            AddFormattedLine(args.Message);
        }

        private void OnAddString(object? _, AddStringArgs args)
        {
            AddLine(args.Text, DetermineColor(args.Local, args.Error));
        }

        private Color DetermineColor(bool local, bool error)
        {
            return Color.White;
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);

            _flushQueue();
        }

        private void CommandEntered(LineEdit.LineEditEventArgs args)
        {
            if (!string.IsNullOrWhiteSpace(args.Text))
            {
                _consoleHost.ExecuteCommand(args.Text);
                CommandBar.Clear();
            }

            commandChanged = true;
        }

        private void OnHistoryChanged()
        {
            _flushHistoryToDisk();
        }

        public void AddLine(string text, Color color)
        {
            var formatted = new FormattedMessage.Builder();
            formatted.PushColor(color);
            formatted.AddText(text);
            formatted.Pop();
            AddFormattedLine(formatted.Build());
        }

        public void AddLine(string text)
        {
            AddLine(text, Color.White);
        }

        public void AddFormattedLine(FormattedMessage message)
        {
            _messageQueue.Enqueue(message);
        }

        public void Clear()
        {
            Output.Clear();
        }

        private void _addFormattedLineInternal(FormattedMessage message)
        {
            Output.AddMessage(message);
        }

        private void _flushQueue()
        {
            while (_messageQueue.TryDequeue(out var message))
            {
                _addFormattedLineInternal(message);
            }
        }

        private void CommandBarOnOnKeyBindDown(GUIBoundKeyEventArgs args)
        {
            if (args.Function == EngineKeyFunctions.TextScrollToBottom)
            {
                Output.ScrollToBottom();
                args.Handle();
            }
            else if (args.Function == EngineKeyFunctions.GuiTabNavigateNext)
            {
                NextCommand();
                args.Handle();
            }
            else if (args.Function == EngineKeyFunctions.GuiTabNavigatePrev)
            {
                PrevCommand();
                args.Handle();
            }
        }

        private void SetInput(string cmd)
        {
            CommandBar.Text = cmd;
            CommandBar.CursorPosition = cmd.Length;
        }

        private void FindCommands()
        {
            searchResults.Clear();
            searchIndex = 0;
            commandChanged = false;
            foreach (var cmd in _consoleHost.RegisteredCommands)
            {
                if (cmd.Key.StartsWith(CommandBar.Text))
                {
                    searchResults.Add(cmd.Key);
                }
            }
        }

        private void NextCommand()
        {
            if (!commandChanged)
            {
                if (searchResults.Count == 0)
                    return;

                searchIndex = (searchIndex + 1) % searchResults.Count;
                SetInput(searchResults[searchIndex]);
                return;
            }

            FindCommands();
            if (searchResults.Count == 0)
                return;

            SetInput(searchResults[0]);
        }

        private void PrevCommand()
        {
            if (!commandChanged)
            {
                if (searchResults.Count == 0)
                    return;

                searchIndex = MathHelper.Mod(searchIndex - 1, searchResults.Count);
                SetInput(searchResults[searchIndex]);
                return;
            }

            FindCommands();
            if (searchResults.Count == 0)
                return;

            SetInput(searchResults[^1]);
        }

        private void OnCommandChanged(LineEdit.LineEditEventArgs args)
        {
            commandChanged = true;
        }

        private async void _loadHistoryFromDisk()
        {
            var sawmill = Logger.GetSawmill("dbgconsole");
            var data = await Task.Run(async () =>
            {
                Stream? stream = null;
                for (var i = 0; i < 3; i++)
                {
                    try
                    {
                        stream = _resourceManager.UserData.OpenRead(HistoryPath);
                        break;
                    }
                    catch (FileNotFoundException)
                    {
                        // Nada, nothing to load in that case.
                        return null;
                    }
                    catch (IOException)
                    {
                        // File locked probably??
                        await Task.Delay(10);
                    }
                }

                if (stream == null)
                {
                    sawmill.Warning("Failed to load debug console history!");
                    return null;
                }

                try
                {
                    return await JsonSerializer.DeserializeAsync<string[]>(stream);
                }
                catch (Exception e)
                {
                    sawmill.Warning($"Failed to load debug console history due to exception!\n{e}");
                    return null;
                }
                finally
                {
                    // ReSharper disable once MethodHasAsyncOverload
                    stream.Dispose();
                }
            });

            if (data == null)
                return;

            CommandBar.ClearHistory();
            CommandBar.History.AddRange(data);
            CommandBar.HistoryIndex = CommandBar.History.Count;
        }

        private async void _flushHistoryToDisk()
        {
            CommandBar.HistoryIndex = CommandBar.History.Count;

            var sawmill = Logger.GetSawmill("dbgconsole");
            var newHistory = JsonSerializer.Serialize(CommandBar.History);

            await Task.Run(async () =>
            {
                Stream? stream = null;

                for (var i = 0; i < 3; i++)
                {
                    try
                    {
                        stream = _resourceManager.UserData.Create(HistoryPath);
                        break;
                    }
                    catch (IOException)
                    {
                        // Probably locking.
                        await Task.Delay(10);
                    }
                }

                if (stream == null)
                {
                    sawmill.Warning("Failed to save debug console history!");
                    return;
                }

                // ReSharper disable once UseAwaitUsing
                using var writer = new StreamWriter(stream, EncodingHelpers.UTF8);
                // ReSharper disable once MethodHasAsyncOverload
                writer.Write(newHistory);
            });
        }
    }
}
