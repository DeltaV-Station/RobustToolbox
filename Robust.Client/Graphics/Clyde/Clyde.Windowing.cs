﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Shared;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using static Robust.Client.Utility.Win32;
using FrameEventArgs = Robust.Shared.Timing.FrameEventArgs;

namespace Robust.Client.Graphics.Clyde
{
    internal partial class  Clyde
    {
        private readonly List<WindowReg> _windows = new();
        private readonly List<WindowHandle> _windowHandles = new();
        private readonly Dictionary<int, MonitorHandle> _monitorHandles = new();

        private int _primaryMonitorId;
        private WindowReg? _mainWindow;

        private IWindowingImpl? _windowing;
        private Renderer _chosenRenderer;

        private ResourcePath? _windowIconPath;
        private Thread? _windowingThread;
        private bool _vSync;
        private WindowMode _windowMode;
        private WindowReg? _currentHoveredWindow;
        private bool _threadWindowBlit;
        private bool EffectiveThreadWindowBlit => _threadWindowBlit && !_isGLES;

        public event Action<TextEventArgs>? TextEntered;
        public event Action<MouseMoveEventArgs>? MouseMove;
        public event Action<MouseEnterLeaveEventArgs>? MouseEnterLeave;
        public event Action<KeyEventArgs>? KeyUp;
        public event Action<KeyEventArgs>? KeyDown;
        public event Action<MouseWheelEventArgs>? MouseWheel;
        public event Action<WindowRequestClosedEventArgs>? CloseWindow;
        public event Action<WindowDestroyedEventArgs>? DestroyWindow;
        public event Action<WindowContentScaleEventArgs>? OnWindowScaleChanged;
        public event Action<WindowResizedEventArgs>? OnWindowResized;
        public event Action<WindowFocusedEventArgs>? OnWindowFocused;

        // NOTE: in engine we pretend the framebuffer size is the screen size..
        // For practical reasons like UI rendering.
        public IClydeWindow MainWindow => _mainWindow?.Handle ??
                                          throw new InvalidOperationException("Windowing is not initialized");

        public Vector2i ScreenSize => _mainWindow?.FramebufferSize ??
                                      throw new InvalidOperationException("Windowing is not initialized");

        public bool IsFocused => _mainWindow?.IsFocused ??
                                 throw new InvalidOperationException("Windowing is not initialized");

        public IEnumerable<IClydeWindow> AllWindows => _windowHandles;

        public Vector2 DefaultWindowScale => _mainWindow?.WindowScale ??
                                             throw new InvalidOperationException("Windowing is not initialized");

        public ScreenCoordinates MouseScreenPosition
        {
            get
            {
                var window = _currentHoveredWindow;
                if (window == null)
                    return default;

                return new ScreenCoordinates(window.LastMousePos, window.Id);
            }
        }

        public string GetKeyName(Keyboard.Key key)
        {
            DebugTools.AssertNotNull(_windowing);

            return _windowing!.KeyGetName(key);
        }

        public uint? GetX11WindowId()
        {
            return _windowing?.WindowGetX11Id(_mainWindow!) ?? null;
        }

        private bool InitWindowing()
        {
            var iconPath = _cfg.GetCVar(CVars.DisplayWindowIconSet);
            if (!string.IsNullOrWhiteSpace(iconPath))
                _windowIconPath = new ResourcePath(iconPath);

            _windowingThread = Thread.CurrentThread;

            _windowing = new GlfwWindowingImpl(this);

            return _windowing.Init();
        }

        private bool TryInitMainWindow(GLContextSpec? glSpec, [NotNullWhen(false)] out string? error)
        {
            DebugTools.AssertNotNull(_glContext);

            var width = _cfg.GetCVar(CVars.DisplayWidth);
            var height = _cfg.GetCVar(CVars.DisplayHeight);
            var prevWidth = width;
            var prevHeight = height;

            IClydeMonitor? monitor = null;
            var fullscreen = false;

            if (_windowMode == WindowMode.Fullscreen)
            {
                monitor = _monitorHandles[_primaryMonitorId];
                width = monitor.Size.X;
                height = monitor.Size.Y;
                fullscreen = true;
            }

            var parameters = new WindowCreateParameters
            {
                Width = width,
                Height = height,
                Monitor = monitor,
                Fullscreen = fullscreen
            };

            var (reg, err) = SharedWindowCreate(glSpec, parameters, null, isMain: true);

            if (reg == null)
            {
                error = err!;
                return false;
            }

            DebugTools.Assert(reg.Id == WindowId.Main);

            if (fullscreen)
            {
                reg.PrevWindowSize = (prevWidth, prevHeight);
                reg.PrevWindowPos = (50, 50);
            }

            error = null;
            return true;
        }

        private unsafe bool InitMainWindowAndRenderer()
        {
            DebugTools.AssertNotNull(_windowing);
            DebugTools.AssertNotNull(_glContext);

            _chosenRenderer = Renderer.OpenGL;
            _openGLVersion = (RendererOpenGLVersion) _cfg.GetCVar(CVars.DisplayOpenGLVersion);

            RendererOpenGLVersion[] versions;

            if (_glContext!.GlesOnly)
            {
                versions = new[]
                {
                    RendererOpenGLVersion.GLES3,
                    RendererOpenGLVersion.GLES2
                };
            }
            else
            {
                versions = new[]
                {
                    RendererOpenGLVersion.GL33,
                    RendererOpenGLVersion.GL31,
                    RendererOpenGLVersion.GLES3,
                    RendererOpenGLVersion.GLES2
                };
            }

            if (_openGLVersion != RendererOpenGLVersion.Auto)
            {
                if (Array.IndexOf(versions, _openGLVersion) != -1)
                    versions = new[] {_openGLVersion};
                else
                    Logger.ErrorS("clyde.win", $"Requested OpenGL version {_openGLVersion} not supported.");
            }

            var succeeded = false;
            string? lastError = null;
            foreach (var version in versions)
            {
                var glSpec = _glContext!.SpecWithOpenGLVersion(version);

                if (!TryInitMainWindow(glSpec, out lastError))
                {
                    Logger.DebugS("clyde.win", $"OpenGL {version} unsupported: {lastError}");
                    continue;
                }

                // We should have a main window now.
                DebugTools.AssertNotNull(_mainWindow);

                succeeded = true;
                _openGLVersion = version;
                _isGLES = _openGLVersion is RendererOpenGLVersion.GLES2 or RendererOpenGLVersion.GLES3;
                _isCore = _openGLVersion is RendererOpenGLVersion.GL33;
                break;
            }

            if (!succeeded)
            {
                if (OperatingSystem.IsWindows())
                {
                    var msgBoxContent = "Failed to create the game window. " +
                                        "This probably means your GPU is too old to play the game. " +
                                        "Try to update your graphics drivers, " +
                                        "or enable compatibility mode in the launcher if that fails.\n" +
                                        $"The exact error is: {lastError}";

                    MessageBoxW(null,
                        msgBoxContent,
                        "Space Station 14: Failed to create window",
                        MB_OK | MB_ICONERROR);
                }

                Logger.FatalS("clyde.win",
                    "Failed to create main game window! " +
                    "This probably means your GPU is too old to run the game. " +
                    $"That or update your graphics drivers. {lastError}");

                return false;
            }

            InitOpenGL();

            return true;
        }

        private IEnumerable<Image<Rgba32>> LoadWindowIcons()
        {
            if (OperatingSystem.IsMacOS() || _windowIconPath == null)
            {
                // Does nothing on macOS so don't bother.
                yield break;
            }

            foreach (var file in _resourceCache.ContentFindFiles(_windowIconPath))
            {
                if (file.Extension != "png")
                {
                    continue;
                }

                using var stream = _resourceCache.ContentFileRead(file);
                yield return Image.Load<Rgba32>(stream);
            }
        }

        private void ShutdownWindowing()
        {
            _windowing?.Shutdown();
        }

        public void SetWindowTitle(string title)
        {
            DebugTools.AssertNotNull(_windowing);
            DebugTools.AssertNotNull(_mainWindow);

            _windowing!.WindowSetTitle(_mainWindow!, title);
        }

        public void SetWindowMonitor(IClydeMonitor monitor)
        {
            DebugTools.AssertNotNull(_windowing);
            DebugTools.AssertNotNull(_mainWindow);

            _windowing!.WindowSetMonitor(_mainWindow!, monitor);
        }

        public void RequestWindowAttention()
        {
            DebugTools.AssertNotNull(_windowing);
            DebugTools.AssertNotNull(_mainWindow);

            _windowing!.WindowRequestAttention(_mainWindow!);
        }

        public IClydeWindow CreateWindow(WindowCreateParameters parameters)
        {
            DebugTools.AssertNotNull(_windowing);
            DebugTools.AssertNotNull(_glContext);
            DebugTools.AssertNotNull(_mainWindow);

            var glSpec = _glContext!.GetNewWindowSpec();

            _glContext.BeforeSharedWindowCreateUnbind();

            var (reg, error) = SharedWindowCreate(
                glSpec,
                parameters,
                glSpec == null ? null : _mainWindow,
                isMain: false);

            // Rebinding is handed by WindowCreated in the GL context.

            if (error != null)
                throw new Exception(error);

            return reg!.Handle;
        }

        private (WindowReg?, string? error) SharedWindowCreate(
            GLContextSpec? glSpec,
            WindowCreateParameters parameters,
            WindowReg? share,
            bool isMain)
        {
            WindowReg? owner = null;
            if (parameters.Owner != null)
                owner = ((WindowHandle)parameters.Owner).Reg;

            var (reg, error) = _windowing!.WindowCreate(glSpec, parameters, share, owner);

            if (reg != null)
            {
                // Window init succeeded, do setup.
                reg.IsMainWindow = isMain;
                if (isMain)
                    _mainWindow = reg;

                _windows.Add(reg);
                _windowHandles.Add(reg.Handle);

                var rtId = AllocRid();
                _renderTargets.Add(rtId, new LoadedRenderTarget
                {
                    Size = reg.FramebufferSize,
                    IsWindow = true,
                    WindowId = reg.Id
                });

                reg.RenderTarget = new RenderWindow(this, rtId);

                _glContext!.WindowCreated(reg);
            }

            // Pass through result whether successful or not, caller handles it.
            return (reg, error);
        }

        private void DoDestroyWindow(WindowReg reg)
        {
            if (reg.IsMainWindow)
                throw new InvalidOperationException("Cannot destroy main window.");

            if (reg.IsDisposed)
                return;

            reg.IsDisposed = true;

            _glContext!.WindowDestroyed(reg);
            _windowing!.WindowDestroy(reg);

            _windows.Remove(reg);
            _windowHandles.Remove(reg.Handle);

            var destroyed = new WindowDestroyedEventArgs(reg.Handle);
            DestroyWindow?.Invoke(destroyed);
            reg.Closed?.Invoke(destroyed);
        }

        public void ProcessInput(FrameEventArgs frameEventArgs)
        {
            _windowing?.ProcessEvents();
            DispatchEvents();
        }

        private void SwapAllBuffers()
        {
            _glContext?.SwapAllBuffers();
        }

        private void VSyncChanged(bool newValue)
        {
            _vSync = newValue;
            _glContext?.UpdateVSync();
        }

        private void WindowModeChanged(int mode)
        {
            _windowMode = (WindowMode) mode;
            _windowing?.UpdateMainWindowMode();
        }

        Task<string> IClipboardManager.GetText()
        {
            return _windowing?.ClipboardGetText(_mainWindow!) ?? Task.FromResult("");
        }

        void IClipboardManager.SetText(string text)
        {
            _windowing?.ClipboardSetText(_mainWindow!, text);
        }

        public IEnumerable<IClydeMonitor> EnumerateMonitors()
        {
            return _monitorHandles.Values;
        }

        public ICursor GetStandardCursor(StandardCursorShape shape)
        {
            DebugTools.AssertNotNull(_windowing);

            return _windowing!.CursorGetStandard(shape);
        }

        public ICursor CreateCursor(Image<Rgba32> image, Vector2i hotSpot)
        {
            DebugTools.AssertNotNull(_windowing);

            return _windowing!.CursorCreate(image, hotSpot);
        }

        public void SetCursor(ICursor? cursor)
        {
            DebugTools.AssertNotNull(_windowing);

            _windowing!.CursorSet(_mainWindow!, cursor);
        }


        private void SetWindowVisible(WindowReg reg, bool visible)
        {
            DebugTools.AssertNotNull(_windowing);

            _windowing!.WindowSetVisible(reg, visible);
        }

        private abstract class WindowReg
        {
            public bool IsDisposed;

            public WindowId Id;
            public Vector2 WindowScale;
            public Vector2 PixelRatio;
            public Vector2i FramebufferSize;
            public Vector2i WindowSize;
            public Vector2i PrevWindowSize;
            public Vector2i WindowPos;
            public Vector2i PrevWindowPos;
            public Vector2 LastMousePos;
            public bool IsFocused;
            public bool IsMinimized;
            public string Title = "";
            public bool IsVisible;
            public IClydeWindow? Owner;

            public bool DisposeOnClose;

            public bool IsMainWindow;
            public WindowHandle Handle = default!;
            public RenderWindow RenderTarget = default!;
            public Action<WindowRequestClosedEventArgs>? RequestClosed;
            public Action<WindowDestroyedEventArgs>? Closed;
        }

        private sealed class WindowHandle : IClydeWindowInternal
        {
            // So funny story
            // When this class was a record, the C# compiler on .NET 5 stack overflowed
            // while compiling the Closed event.
            // VERY funny.

            private readonly Clyde _clyde;
            public readonly WindowReg Reg;

            public bool IsDisposed => Reg.IsDisposed;
            public WindowId Id => Reg.Id;

            public WindowHandle(Clyde clyde, WindowReg reg)
            {
                _clyde = clyde;
                Reg = reg;
            }

            public void Dispose()
            {
                _clyde.DoDestroyWindow(Reg);
            }

            public Vector2i Size => Reg.FramebufferSize;

            public IRenderTarget RenderTarget => Reg.RenderTarget;

            public string Title
            {
                get => Reg.Title;
                set => _clyde._windowing!.WindowSetTitle(Reg, value);
            }

            public bool IsFocused => Reg.IsFocused;
            public bool IsMinimized => Reg.IsMinimized;

            public bool IsVisible
            {
                get => Reg.IsVisible;
                set => _clyde.SetWindowVisible(Reg, value);
            }

            public Vector2 ContentScale => Reg.WindowScale;

            public bool DisposeOnClose
            {
                get => Reg.DisposeOnClose;
                set => Reg.DisposeOnClose = value;
            }

            public event Action<WindowRequestClosedEventArgs> RequestClosed
            {
                add => Reg.RequestClosed += value;
                remove => Reg.RequestClosed -= value;
            }

            public event Action<WindowDestroyedEventArgs>? Destroyed
            {
                add => Reg.Closed += value;
                remove => Reg.Closed -= value;
            }

            public nint? WindowsHWnd => _clyde._windowing!.WindowGetWin32Window(Reg);
        }

        private sealed class MonitorHandle : IClydeMonitor
        {
            public MonitorHandle(int id, string name, Vector2i size, int refreshRate, VideoMode[] videoModes)
            {
                Id = id;
                Name = name;
                Size = size;
                RefreshRate = refreshRate;
                VideoModes = videoModes;
            }

            public int Id { get; }
            public string Name { get; }
            public Vector2i Size { get; }
            public int RefreshRate { get; }
            public IEnumerable<VideoMode> VideoModes { get; }
        }

        private abstract class MonitorReg
        {
            public MonitorHandle Handle = default!;
        }
    }
}
