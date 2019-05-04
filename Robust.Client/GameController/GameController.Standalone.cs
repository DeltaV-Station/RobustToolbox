﻿using System;
using System.Linq;
using System.Runtime.Remoting.Channels;
using Robust.Client.Interfaces;
using Robust.Shared.Interfaces.Timing;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Timing;

namespace Robust.Client
{
    internal partial class GameController
    {
        private GameLoop _mainLoop;

        [Dependency] private IGameTiming _gameTimingHeadless;

        public static void Main()
        {
#if !X64
            throw new InvalidOperationException("The client cannot start outside x64.");
#endif

            IoCManager.InitThread();

            if (Environment.GetCommandLineArgs().Contains("--headless"))
            {
                Mode = DisplayMode.Headless;
            }
            else
            {
                Mode = DisplayMode.Clyde;
            }

            var gc = new GameController();
            gc.Startup();
            gc.MainLoop();
        }


        private void MainLoop()
        {
            _mainLoop = new GameLoop(_gameTimingHeadless)
            {
                SleepMode = Mode == DisplayMode.Headless ? SleepMode.Delay : SleepMode.None
            };

            _mainLoop.Tick += (sender, args) =>
            {
                if (_mainLoop.Running)
                {
                    Update(args.DeltaSeconds);
                }
            };

            if (Mode == DisplayMode.Clyde)
            {
                _mainLoop.Render += (sender, args) =>
                {
                    if (_mainLoop.Running)
                    {
                        _gameTimingHeadless.CurFrame++;
                        _clyde.Render(new FrameEventArgs(args.DeltaSeconds));
                    }
                };
                _mainLoop.Input += (sender, args) =>
                {
                    if (_mainLoop.Running)
                    {
                        _clyde.ProcessInput(new FrameEventArgs(args.DeltaSeconds));
                    }
                };
            }

            _mainLoop.Update += (sender, args) =>
            {
                if (_mainLoop.Running)
                {
                    _frameProcessMain(args.DeltaSeconds);
                }
            };

            // set GameLoop.Running to false to return from this function.
            _mainLoop.Run();
        }
    }
}

