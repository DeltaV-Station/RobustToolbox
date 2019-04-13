using System;
using SS14.Client.Interfaces;
using SS14.Client.Interfaces.Graphics;
using SS14.Shared.Configuration;
using SS14.Shared.Interfaces.Configuration;
using SS14.Shared.IoC;
using SS14.Shared.Maths;

namespace SS14.Client.Graphics
{
    public enum WindowMode
    {
        Windowed = 0,
        Fullscreen = 1,
        // Maybe add borderless? Not sure how good Godot's default fullscreen is with alt tabbing.
    }

    /// <summary>
    ///     Manages the game window, resolutions, fullscreen mode, VSync, etc...
    /// </summary>
    internal abstract class DisplayManager : IDisplayManager, IPostInjectInit
    {
        private const string CVarVSync = "display.vsync";
        private const string CVarWindowMode = "display.windowmode";

        [Dependency] protected readonly IConfigurationManager _configurationManager;
        [Dependency] protected readonly IGameControllerProxyInternal _gameController;

        protected WindowMode WindowMode { get; private set; } = WindowMode.Windowed;
        protected bool VSync { get; private set; } = true;

        public virtual void PostInject()
        {
            _configurationManager.RegisterCVar(CVarVSync, VSync, CVar.ARCHIVE, _vSyncChanged);
            _configurationManager.RegisterCVar(CVarWindowMode, (int) WindowMode, CVar.ARCHIVE, _windowModeChanged);
        }

        public abstract Vector2i ScreenSize { get; }
        public abstract void SetWindowTitle(string title);
        public abstract void Initialize();

        protected virtual void ReloadConfig()
        {
            ReadConfig();
        }

        public abstract event Action<WindowResizedEventArgs> OnWindowResized;

        protected virtual void ReadConfig()
        {
            WindowMode = (WindowMode) _configurationManager.GetCVar<int>(CVarWindowMode);
            VSync = _configurationManager.GetCVar<bool>(CVarVSync);
        }

        private void _vSyncChanged(bool newValue)
        {
            VSync = newValue;
            VSyncChanged();
        }

        protected virtual void VSyncChanged()
        {
        }

        private void _windowModeChanged(int newValue)
        {
            WindowMode = (Graphics.WindowMode)newValue;
            WindowModeChanged();
        }

        protected virtual void WindowModeChanged()
        {
        }
    }
}
