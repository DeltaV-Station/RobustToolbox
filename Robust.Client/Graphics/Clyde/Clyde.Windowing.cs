﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Threading;
using OpenToolkit;
using OpenToolkit.Graphics.OpenGL4;
using OpenToolkit.GraphicsLibraryFramework;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Client.Utility;
using Robust.Shared;
using Robust.Shared.Localization;
using Robust.Shared.Log;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using static Robust.Client.Utility.LiterallyJustMessageBox;
using ErrorCode = OpenToolkit.GraphicsLibraryFramework.ErrorCode;
using FrameEventArgs = Robust.Shared.Timing.FrameEventArgs;
using GLFW = OpenToolkit.GraphicsLibraryFramework.GLFW;
using GLFWCallbacks = OpenToolkit.GraphicsLibraryFramework.GLFWCallbacks;
using Image = SixLabors.ImageSharp.Image;
using Vector2 = Robust.Shared.Maths.Vector2;
using GlfwImage = OpenToolkit.GraphicsLibraryFramework.Image;
using InputAction = OpenToolkit.GraphicsLibraryFramework.InputAction;
using KeyModifiers = OpenToolkit.GraphicsLibraryFramework.KeyModifiers;
using Keys = OpenToolkit.GraphicsLibraryFramework.Keys;
using Monitor = OpenToolkit.GraphicsLibraryFramework.Monitor;
using MouseButton = OpenToolkit.GraphicsLibraryFramework.MouseButton;
using OpenGlProfile = OpenToolkit.GraphicsLibraryFramework.OpenGlProfile;
using ClientApi = OpenToolkit.GraphicsLibraryFramework.ClientApi;
using ContextApi = OpenToolkit.GraphicsLibraryFramework.ContextApi;
using Window = OpenToolkit.GraphicsLibraryFramework.Window;
using WindowHintBool = OpenToolkit.GraphicsLibraryFramework.WindowHintBool;
using WindowHintInt = OpenToolkit.GraphicsLibraryFramework.WindowHintInt;
using WindowHintOpenGlProfile = OpenToolkit.GraphicsLibraryFramework.WindowHintOpenGlProfile;
using WindowHintClientApi = OpenToolkit.GraphicsLibraryFramework.WindowHintClientApi;
using WindowHintContextApi = OpenToolkit.GraphicsLibraryFramework.WindowHintContextApi;
using WindowHintString = OpenToolkit.GraphicsLibraryFramework.WindowHintString;

namespace Robust.Client.Graphics.Clyde
{
    internal unsafe partial class Clyde
    {
        private const int EventQueueSize = 20;
        private bool _glfwInitialized;

        // Keep delegates around to prevent GC issues.
        private GLFWCallbacks.ErrorCallback _errorCallback = default!;
        private GLFWCallbacks.MonitorCallback _monitorCallback = default!;
        private GLFWCallbacks.CharCallback _charCallback = default!;
        private GLFWCallbacks.CursorPosCallback _cursorPosCallback = default!;
        private GLFWCallbacks.KeyCallback _keyCallback = default!;
        private GLFWCallbacks.MouseButtonCallback _mouseButtonCallback = default!;
        private GLFWCallbacks.ScrollCallback _scrollCallback = default!;
        private GLFWCallbacks.WindowCloseCallback _windowCloseCallback = default!;
        private GLFWCallbacks.WindowSizeCallback _windowSizeCallback = default!;
        private GLFWCallbacks.WindowContentScaleCallback _windowContentScaleCallback = default!;
        private GLFWCallbacks.WindowIconifyCallback _windowIconifyCallback = default!;
        private GLFWCallbacks.WindowFocusCallback _windowFocusCallback = default!;

        private readonly List<WindowReg> _windows = new();
        private readonly List<WindowHandle> _windowHandles = new();

        private Renderer _chosenRenderer;
        private IBindingsContext _graphicsContext = default!;
        private WindowReg? _mainWindow;

        private Thread? _mainThread;

        // Can't use ClydeHandle because it's 64 bit.
        // TODO: this should be MONITOR ID.
        private int _nextWindowId = 1;
        private readonly Dictionary<int, MonitorReg> _monitors = new();

        private readonly RefList<GlfwEvent> _glfwEventQueue = new();

        public event Action<TextEventArgs>? TextEntered;
        public event Action<MouseMoveEventArgs>? MouseMove;
        public event Action<KeyEventArgs>? KeyUp;
        public event Action<KeyEventArgs>? KeyDown;
        public event Action<MouseWheelEventArgs>? MouseWheel;
        public event Action<WindowClosedEventArgs>? CloseWindow;
        public event Action? OnWindowScaleChanged;

        // NOTE: in engine we pretend the framebuffer size is the screen size..
        // For practical reasons like UI rendering.
        public IClydeWindow MainWindow => _mainWindow!.Handle;
        public override Vector2i ScreenSize => _mainWindow!.FramebufferSize;
        public override bool IsFocused => _mainWindow!.IsFocused;
        public IEnumerable<IClydeWindow> AllWindows => _windowHandles;
        public Vector2 DefaultWindowScale => _mainWindow!.WindowScale;
        public Vector2 MouseScreenPosition => _mainWindow!.LastMousePos;

        public string GetKeyName(Keyboard.Key key)
        {
            var name = Keyboard.GetSpecialKeyName(key);
            if (name != null)
            {
                return Loc.GetString(name);
            }

            name = GLFW.GetKeyName(Keyboard.ConvertGlfwKeyReverse(key), 0);
            if (name != null)
            {
                return name.ToUpper();
            }

            return Loc.GetString("<unknown key>");
        }

        public string GetKeyNameScanCode(int scanCode)
        {
            return GLFW.GetKeyName(Keys.Unknown, scanCode);
        }

        public int GetKeyScanCode(Keyboard.Key key)
        {
            return GLFW.GetKeyScancode(Keyboard.ConvertGlfwKeyReverse(key));
        }

        public uint? GetX11WindowId()
        {
            try
            {
                return GLFW.GetX11Window(_mainWindow!.GlfwWindow);
            }
            catch (EntryPointNotFoundException)
            {
                return null;
            }
        }

        private bool InitGlfw()
        {
            StoreCallbacks();

            GLFW.SetErrorCallback(_errorCallback);
            if (!GLFW.Init())
            {
                Logger.FatalS("clyde.win", "Failed to initialize GLFW!");
                return false;
            }

            _glfwInitialized = true;
            var version = GLFW.GetVersionString();
            Logger.DebugS("clyde.win", "GLFW initialized, version: {0}.", version);

            return true;
        }

        private bool InitWindowing()
        {
            _mainThread = Thread.CurrentThread;
            if (!InitGlfw())
            {
                return false;
            }

            InitMonitors();
            InitCursors();

            return InitMainWindow();
        }

        private void InitMonitors()
        {
            var monitors = GLFW.GetMonitorsRaw(out var count);

            for (var i = 0; i < count; i++)
            {
                SetupMonitor(monitors[i]);
            }
        }

        private void SetupMonitor(Monitor* monitor)
        {
            var handle = _nextWindowId++;

            DebugTools.Assert(GLFW.GetMonitorUserPointer(monitor) == null, "GLFW window already has user pointer??");

            var name = GLFW.GetMonitorName(monitor);
            var videoMode = GLFW.GetVideoMode(monitor);
            var impl = new ClydeMonitorImpl(handle, name, (videoMode->Width, videoMode->Height),
                videoMode->RefreshRate);

            GLFW.SetMonitorUserPointer(monitor, (void*) handle);
            _monitors[handle] = new MonitorReg
            {
                Id = handle,
                Impl = impl,
                Monitor = monitor
            };
        }

        private void DestroyMonitor(Monitor* monitor)
        {
            var ptr = GLFW.GetMonitorUserPointer(monitor);

            if (ptr == null)
            {
                var name = GLFW.GetMonitorName(monitor);
                Logger.WarningS("clyde.win", $"Monitor '{name}' had no user pointer set??");
                return;
            }

            _monitors.Remove((int) ptr);
            GLFW.SetMonitorUserPointer(monitor, null);
        }

        private bool InitMainWindow()
        {
            var width = ConfigurationManager.GetCVar(CVars.DisplayWidth);
            var height = ConfigurationManager.GetCVar(CVars.DisplayHeight);

            Monitor* monitor = null;

            if (WindowMode == WindowMode.Fullscreen)
            {
                monitor = GLFW.GetPrimaryMonitor();
                var mode = GLFW.GetVideoMode(monitor);
                width = mode->Width;
                height = mode->Height;

                GLFW.WindowHint(WindowHintInt.RefreshRate, mode->RefreshRate);
            }

#if DEBUG
            GLFW.WindowHint(WindowHintBool.OpenGLDebugContext, true);
#endif
            GLFW.WindowHint(WindowHintString.X11ClassName, "SS14");
            GLFW.WindowHint(WindowHintString.X11InstanceName, "SS14");

            _chosenRenderer = (Renderer) ConfigurationManager.GetCVar(CVars.DisplayRenderer);

            var renderers = (_chosenRenderer == Renderer.Default)
                ? stackalloc Renderer[]
                {
                    Renderer.OpenGL33,
                    Renderer.OpenGL31,
                    Renderer.OpenGLES2
                }
                : stackalloc Renderer[] {_chosenRenderer};

            ErrorCode lastGlfwError = default;
            string? lastGlfwErrorDesc = default;

            Window* window = null;

            foreach (var r in renderers)
            {
                window = CreateGlfwWindowForRenderer(r, width, height, ref monitor, null);

                if (window != null)
                {
                    _chosenRenderer = r;
                    _isGLES = _chosenRenderer == Renderer.OpenGLES2;
                    _isCore = _chosenRenderer == Renderer.OpenGL33;
                    break;
                }

                // Window failed to init due to error.
                // Try not to treat the error code seriously.
                lastGlfwError = GLFW.GetError(out lastGlfwErrorDesc);
                Logger.DebugS("clyde.win", $"{r} unsupported: [{lastGlfwError}] ${lastGlfwErrorDesc}");
            }

            if (window == null)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var errorContent = "Failed to create the game window. " +
                                       "This probably means your GPU is too old to play the game. " +
                                       "Try to update your graphics drivers, " +
                                       "or enable compatibility mode in the launcher if that fails.\n" +
                                       $"The exact error is: {lastGlfwError}\n{lastGlfwErrorDesc}";

                    MessageBoxW(null,
                        errorContent,
                        "Space Station 14: Failed to create window",
                        MB_OK | MB_ICONERROR);
                }

                Logger.FatalS("clyde.win",
                    "Failed to create GLFW window! " +
                    "This probably means your GPU is too old to run the game. " +
                    "That or update your graphics drivers.");
                return false;
            }

            _mainWindow = SetupWindow(window);
            _mainWindow.IsMainWindow = true;

            UpdateMainWindowLoadedRtSize();

            GLFW.MakeContextCurrent(window);
            VSyncChanged();
            InitGLContext();

            // Initializing OTK 3 seems to mess with the current context, so ensure it's still set.
            // This took me fucking *forever* to debug because this manifested differently on nvidia drivers vs intel mesa.
            // So I thought it was a calling convention issue with the calli OpenTK emits.
            // Because, in my tests, I had InitGLContext() AFTER the test with a delegate-based invoke of the proc.
            GLFW.MakeContextCurrent(window);

            InitOpenGL();

            return true;
        }

        private static Window* CreateGlfwWindowForRenderer(
            Renderer r,
            int width, int height,
            ref Monitor* monitor,
            Window* contextShare)
        {
            if (r == Renderer.OpenGL33)
            {
                GLFW.WindowHint(WindowHintInt.ContextVersionMajor, 3);
                GLFW.WindowHint(WindowHintInt.ContextVersionMinor, 3);
                GLFW.WindowHint(WindowHintBool.OpenGLForwardCompat, true);
                GLFW.WindowHint(WindowHintClientApi.ClientApi, ClientApi.OpenGlApi);
                GLFW.WindowHint(WindowHintContextApi.ContextCreationApi, ContextApi.NativeContextApi);
                GLFW.WindowHint(WindowHintOpenGlProfile.OpenGlProfile, OpenGlProfile.Core);
                GLFW.WindowHint(WindowHintBool.SrgbCapable, true);
            }
            else if (r == Renderer.OpenGL31)
            {
                GLFW.WindowHint(WindowHintInt.ContextVersionMajor, 3);
                GLFW.WindowHint(WindowHintInt.ContextVersionMinor, 1);
                GLFW.WindowHint(WindowHintBool.OpenGLForwardCompat, false);
                GLFW.WindowHint(WindowHintClientApi.ClientApi, ClientApi.OpenGlApi);
                GLFW.WindowHint(WindowHintContextApi.ContextCreationApi, ContextApi.NativeContextApi);
                GLFW.WindowHint(WindowHintOpenGlProfile.OpenGlProfile, OpenGlProfile.Any);
                GLFW.WindowHint(WindowHintBool.SrgbCapable, true);
            }
            else if (r == Renderer.OpenGLES2)
            {
                GLFW.WindowHint(WindowHintInt.ContextVersionMajor, 2);
                GLFW.WindowHint(WindowHintInt.ContextVersionMinor, 0);
                GLFW.WindowHint(WindowHintBool.OpenGLForwardCompat, true);
                GLFW.WindowHint(WindowHintClientApi.ClientApi, ClientApi.OpenGlEsApi);
                // GLES2 is initialized through EGL to allow ANGLE usage.
                // (It may be an idea to make this a configuration cvar)
                GLFW.WindowHint(WindowHintContextApi.ContextCreationApi, ContextApi.EglContextApi);
                GLFW.WindowHint(WindowHintOpenGlProfile.OpenGlProfile, OpenGlProfile.Any);
                GLFW.WindowHint(WindowHintBool.SrgbCapable, false);
            }

            return GLFW.CreateWindow(width, height, string.Empty, monitor, contextShare);
        }

        private WindowReg SetupWindow(Window* window)
        {
            var reg = new WindowReg
            {
                GlfwWindow = window
            };
            var handle = new WindowHandle(this, reg);
            reg.Handle = handle;

            LoadWindowIcon(window);

            GLFW.SetCharCallback(window, _charCallback);
            GLFW.SetKeyCallback(window, _keyCallback);
            GLFW.SetWindowCloseCallback(window, _windowCloseCallback);
            GLFW.SetCursorPosCallback(window, _cursorPosCallback);
            GLFW.SetWindowSizeCallback(window, _windowSizeCallback);
            GLFW.SetScrollCallback(window, _scrollCallback);
            GLFW.SetMouseButtonCallback(window, _mouseButtonCallback);
            GLFW.SetWindowContentScaleCallback(window, _windowContentScaleCallback);
            GLFW.SetWindowIconifyCallback(window, _windowIconifyCallback);
            GLFW.SetWindowFocusCallback(window, _windowFocusCallback);

            GLFW.GetFramebufferSize(window, out var fbW, out var fbH);
            reg.FramebufferSize = (fbW, fbH);

            GLFW.GetWindowContentScale(window, out var scaleX, out var scaleY);
            reg.WindowScale = (scaleX, scaleY);

            GLFW.GetWindowSize(window, out var w, out var h);
            reg.PrevWindowSize = reg.WindowSize = (w, h);

            GLFW.GetWindowPos(window, out var x, out var y);
            reg.PrevWindowPos = (x, y);

            reg.PixelRatio = reg.FramebufferSize / reg.WindowSize;

            _windows.Add(reg);
            _windowHandles.Add(handle);

            return reg;
        }

        private WindowHandle CreateWindowImpl()
        {
            DebugTools.AssertNotNull(_mainWindow);

            // GLFW.WindowHint(WindowHintBool.SrgbCapable, false);

            Monitor* monitor = null;
            var window = CreateGlfwWindowForRenderer(_chosenRenderer, 1280, 720, ref monitor, _mainWindow!.GlfwWindow);
            if (window == null)
            {
                var errCode = GLFW.GetError(out var desc);
                throw new GlfwException($"{errCode}: {desc}");
            }

            var reg = SetupWindow(window);
            CreateWindowRenderTexture(reg);

            GLFW.MakeContextCurrent(window);

            // VSync always off for non-primary windows.
            GLFW.SwapInterval(0);

            reg.QuadVao = MakeQuadVao();

            UniformConstantsUBO.Rebind();
            ProjViewUBO.Rebind();

            GLFW.MakeContextCurrent(_mainWindow.GlfwWindow);

            return reg.Handle;
        }

        private void CreateWindowRenderTexture(WindowReg reg)
        {
            reg.RenderTexture = CreateRenderTarget(reg.FramebufferSize, new RenderTargetFormatParameters
            {
                ColorFormat = RenderTargetColorFormat.Rgba8Srgb,
                HasDepthStencil = true
            });
        }

        private void LoadWindowIcon(Window* window)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // Does nothing on macOS so don't bother.
                return;
            }

            var icons = new List<Image<Rgba32>>();
            foreach (var file in _resourceCache.ContentFindFiles("/Textures/Logo/icon"))
            {
                if (file.Extension != "png")
                {
                    continue;
                }

                using (var stream = _resourceCache.ContentFileRead(file))
                {
                    var image = Image.Load<Rgba32>(stream);
                    icons.Add(image);
                }
            }

            SetWindowIcon(window, icons);
        }

        private void SetWindowIcon(Window* window, IEnumerable<Image<Rgba32>> icons)
        {
            // Turn each image into a byte[] so we can actually pin their contents.
            // Wish I knew a clean way to do this without allocations.
            var images = icons
                .Select(i => (MemoryMarshal.Cast<Rgba32, byte>(i.GetPixelSpan()).ToArray(), i.Width, i.Height))
                .ToList();

            // ReSharper disable once SuggestVarOrType_Elsewhere
            Span<GCHandle> handles = stackalloc GCHandle[images.Count];
            Span<GlfwImage> glfwImages = new GlfwImage[images.Count];

            for (var i = 0; i < images.Count; i++)
            {
                var image = images[i];
                handles[i] = GCHandle.Alloc(image.Item1, GCHandleType.Pinned);
                var addrOfPinnedObject = (byte*) handles[i].AddrOfPinnedObject();
                glfwImages[i] = new GlfwImage(image.Width, image.Height, addrOfPinnedObject);
            }

            GLFW.SetWindowIcon(window, glfwImages);

            foreach (var handle in handles)
            {
                handle.Free();
            }
        }

        private class GlfwBindingsContext : IBindingsContext
        {
            public IntPtr GetProcAddress(string procName)
            {
                return GLFW.GetProcAddress(procName);
            }
        }

        private void InitGLContext()
        {
            _graphicsContext = new GlfwBindingsContext();
            GL.LoadBindings(_graphicsContext);

            if (_isGLES)
            {
                // On GLES we use some OES and KHR functions so make sure to initialize them.
                OpenToolkit.Graphics.ES20.GL.LoadBindings(_graphicsContext);
            }
        }

        private void ShutdownWindowing()
        {
            if (_glfwInitialized)
            {
                Logger.DebugS("clyde.win", "Terminating GLFW.");
                GLFW.Terminate();
            }
        }

        private WindowReg FindWindow(Window* window)
        {
            foreach (var windowReg in _windows)
            {
                if (windowReg.GlfwWindow == window)
                {
                    return windowReg;
                }
            }

            throw new KeyNotFoundException();
        }

        private static void OnGlfwError(ErrorCode code, string description)
        {
            Logger.ErrorS("clyde.win.glfw", "GLFW Error: [{0}] {1}", code, description);
        }

        private void OnGlfwMonitor(Monitor* monitor, ConnectedState state)
        {
            ref var ev = ref _glfwEventQueue.AllocAdd();
            ev.Type = GlfwEventType.Monitor;

            ev.Monitor.Monitor = monitor;
            ev.Monitor.State = state;
        }

        private void ProcessGlfwEventMonitor(in GlfwEventMonitor ev)
        {
            if (ev.State == ConnectedState.Connected)
            {
                SetupMonitor(ev.Monitor);
            }
            else
            {
                DestroyMonitor(ev.Monitor);
            }
        }

        private void OnGlfwChar(Window* window, uint codepoint)
        {
            ref var ev = ref _glfwEventQueue.AllocAdd();
            ev.Type = GlfwEventType.Char;

            ev.Char.CodePoint = codepoint;
        }

        private void ProcessGlfwEventChar(in GlfwEventChar ev)
        {
            TextEntered?.Invoke(new TextEventArgs(ev.CodePoint));
        }

        private void OnGlfwCursorPos(Window* window, double x, double y)
        {
            ref var ev = ref _glfwEventQueue.AllocAdd();
            ev.Type = GlfwEventType.CursorPos;

            ev.CursorPos.Window = window;
            ev.CursorPos.XPos = x;
            ev.CursorPos.YPos = y;
        }

        private void ProcessGlfwEventCursorPos(in GlfwEventCursorPos ev)
        {
            var windowReg = FindWindow(ev.Window);
            var newPos = ((float) ev.XPos, (float) ev.YPos) * windowReg.PixelRatio;
            var delta = newPos - windowReg.LastMousePos;
            windowReg.LastMousePos = newPos;

            MouseMove?.Invoke(new MouseMoveEventArgs(delta, newPos));
        }

        private void OnGlfwKey(Window* window, Keys key, int scanCode, InputAction action, KeyModifiers mods)
        {
            ref var ev = ref _glfwEventQueue.AllocAdd();
            ev.Type = GlfwEventType.Key;

            ev.Key.Window = window;
            ev.Key.Key = key;
            ev.Key.ScanCode = scanCode;
            ev.Key.Action = action;
            ev.Key.Mods = mods;
        }

        private void ProcessGlfwEventKey(in GlfwEventKey ev)
        {
            EmitKeyEvent(Keyboard.ConvertGlfwKey(ev.Key), ev.Action, ev.Mods);
        }

        private void OnGlfwMouseButton(Window* window, MouseButton button, InputAction action, KeyModifiers mods)
        {
            ref var ev = ref _glfwEventQueue.AllocAdd();
            ev.Type = GlfwEventType.MouseButton;

            ev.MouseButton.Window = window;
            ev.MouseButton.Button = button;
            ev.MouseButton.Action = action;
            ev.MouseButton.Mods = mods;
        }

        private void ProcessGlfwEventMouseButton(in GlfwEventMouseButton ev)
        {
            EmitKeyEvent(Mouse.MouseButtonToKey(Mouse.ConvertGlfwButton(ev.Button)), ev.Action, ev.Mods);
        }

        private void EmitKeyEvent(Keyboard.Key key, InputAction action, KeyModifiers mods)
        {
            var shift = (mods & KeyModifiers.Shift) != 0;
            var alt = (mods & KeyModifiers.Alt) != 0;
            var control = (mods & KeyModifiers.Control) != 0;
            var system = (mods & KeyModifiers.Super) != 0;

            var ev = new KeyEventArgs(
                key,
                action == InputAction.Repeat,
                alt, control, shift, system);

            switch (action)
            {
                case InputAction.Release:
                    KeyUp?.Invoke(ev);
                    break;
                case InputAction.Press:
                case InputAction.Repeat:
                    KeyDown?.Invoke(ev);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, null);
            }
        }

        private void OnGlfwScroll(Window* window, double offsetX, double offsetY)
        {
            ref var ev = ref _glfwEventQueue.AllocAdd();
            ev.Type = GlfwEventType.Scroll;

            ev.Scroll.Window = window;
            ev.Scroll.XOffset = offsetX;
            ev.Scroll.YOffset = offsetY;
        }

        private void ProcessGlfwEventScroll(in GlfwEventScroll ev)
        {
            var windowReg = FindWindow(ev.Window);
            var eventArgs = new MouseWheelEventArgs(((float) ev.XOffset, (float) ev.YOffset), windowReg.LastMousePos);
            MouseWheel?.Invoke(eventArgs);
        }

        private void OnGlfwWindowClose(Window* window)
        {
            ref var ev = ref _glfwEventQueue.AllocAdd();
            ev.Type = GlfwEventType.WindowClose;

            ev.WindowClose.Window = window;
        }

        private void ProcessGlfwEventWindowClose(in GlfwEventWindowClose ev)
        {
            var windowReg = FindWindow(ev.Window);
            CloseWindow?.Invoke(new WindowClosedEventArgs(windowReg.Handle));
        }

        private void OnGlfwWindowSize(Window* window, int width, int height)
        {
            ref var ev = ref _glfwEventQueue.AllocAdd();
            ev.Type = GlfwEventType.WindowSize;

            ev.WindowSize.Window = window;
            ev.WindowSize.Width = width;
            ev.WindowSize.Height = height;
        }

        private void ProcessGlfwEventWindowSize(in GlfwEventWindowSize ev)
        {
            var window = ev.Window;
            var width = ev.Width;
            var height = ev.Height;

            var windowReg = FindWindow(window);
            var oldSize = windowReg.FramebufferSize;
            GLFW.GetFramebufferSize(window, out var fbW, out var fbH);
            windowReg.FramebufferSize = (fbW, fbH);
            windowReg.WindowSize = (width, height);

            if (windowReg.IsMainWindow)
            {
                UpdateMainWindowLoadedRtSize();
            }

            if (fbW == 0 || fbH == 0 || width == 0 || height == 0)
                return;

            windowReg.PixelRatio = windowReg.FramebufferSize / windowReg.WindowSize;

            if (windowReg.IsMainWindow)
            {
                GL.Viewport(0, 0, fbW, fbH);
                CheckGlError();
            }
            else
            {
                windowReg.RenderTexture!.Dispose();
                CreateWindowRenderTexture(windowReg);
            }

            var eventArgs = new WindowResizedEventArgs(oldSize, windowReg.FramebufferSize, windowReg.Handle);
            OnWindowResized?.Invoke(eventArgs);
        }

        private void OnGlfwWindowContentScale(Window* window, float xScale, float yScale)
        {
            ref var ev = ref _glfwEventQueue.AllocAdd();
            ev.Type = GlfwEventType.WindowContentScale;

            ev.WindowContentScale.Window = window;
            ev.WindowContentScale.XScale = xScale;
            ev.WindowContentScale.YScale = yScale;
        }

        private void ProcessGlfwEventWindowContentScale(in GlfwEventWindowContentScale ev)
        {
            var windowReg = FindWindow(ev.Window);
            windowReg.WindowScale = (ev.XScale, ev.YScale);
            OnWindowScaleChanged?.Invoke();
        }

        private void OnGlfwWindowIconify(Window* window, bool iconified)
        {
            ref var ev = ref _glfwEventQueue.AllocAdd();
            ev.Type = GlfwEventType.WindowIconified;

            ev.WindowIconify.Window = window;
            ev.WindowIconify.Iconified = iconified;
        }

        private void ProcessGlfwEventWindowIconify(in GlfwEventWindowIconify ev)
        {
            var windowReg = FindWindow(ev.Window);
            windowReg.IsMinimized = ev.Iconified;
        }

        private void OnGlfwWindowFocus(Window* window, bool focused)
        {
            ref var ev = ref _glfwEventQueue.AllocAdd();
            ev.Type = GlfwEventType.WindowFocus;

            ev.WindowFocus.Window = window;
            ev.WindowFocus.Focused = focused;
        }

        private void ProcessGlfwEventWindowFocus(in GlfwEventWindowFocus ev)
        {
            var windowReg = FindWindow(ev.Window);
            windowReg.IsFocused = ev.Focused;
            OnWindowFocused?.Invoke(new WindowFocusedEventArgs(ev.Focused, windowReg.Handle));
        }

        private void StoreCallbacks()
        {
            _errorCallback = OnGlfwError;
            _monitorCallback = OnGlfwMonitor;
            _charCallback = OnGlfwChar;
            _cursorPosCallback = OnGlfwCursorPos;
            _keyCallback = OnGlfwKey;
            _mouseButtonCallback = OnGlfwMouseButton;
            _scrollCallback = OnGlfwScroll;
            _windowCloseCallback = OnGlfwWindowClose;
            _windowSizeCallback = OnGlfwWindowSize;
            _windowContentScaleCallback = OnGlfwWindowContentScale;
            _windowIconifyCallback = OnGlfwWindowIconify;
            _windowFocusCallback = OnGlfwWindowFocus;
        }

        public override void SetWindowTitle(string title)
        {
            SetWindowTitle(_mainWindow!, title);
        }

        private void SetWindowTitle(WindowReg reg, string title)
        {
            if (title == null)
            {
                throw new ArgumentNullException(nameof(title));
            }

            GLFW.SetWindowTitle(reg.GlfwWindow, title);
            reg.Title = title;
        }

        public void SetWindowMonitor(IClydeMonitor monitor)
        {
            var monitorImpl = (ClydeMonitorImpl) monitor;
            var reg = _monitors[monitorImpl.Id];

            GLFW.SetWindowMonitor(
                _mainWindow!.GlfwWindow,
                reg.Monitor,
                0, 0,
                monitorImpl.Size.X, monitorImpl.Size.Y,
                monitorImpl.RefreshRate);
        }

        public void RequestWindowAttention()
        {
            GLFW.RequestWindowAttention(_mainWindow!.GlfwWindow);
        }

        public IClydeWindow CreateWindow()
        {
            return CreateWindowImpl();
        }

        public void ProcessInput(FrameEventArgs frameEventArgs)
        {
            // GLFW's callback-based event architecture sucks.
            // And there are ridiculous edge-cases like glfwCreateWindow flushing the event queue (wtf???).
            // So we make our own event buffer and process it manually to work around this madness.
            // This is more similar to how SDL2's event queue works.

            GLFW.PollEvents();

            for (var i = 0; i < _glfwEventQueue.Count; i++)
            {
                ref var ev = ref _glfwEventQueue[i];

                try
                {
                    switch (ev.Type)
                    {
                        case GlfwEventType.MouseButton:
                            ProcessGlfwEventMouseButton(ev.MouseButton);
                            break;
                        case GlfwEventType.CursorPos:
                            ProcessGlfwEventCursorPos(ev.CursorPos);
                            break;
                        case GlfwEventType.Scroll:
                            ProcessGlfwEventScroll(ev.Scroll);
                            break;
                        case GlfwEventType.Key:
                            ProcessGlfwEventKey(ev.Key);
                            break;
                        case GlfwEventType.Char:
                            ProcessGlfwEventChar(ev.Char);
                            break;
                        case GlfwEventType.Monitor:
                            ProcessGlfwEventMonitor(ev.Monitor);
                            break;
                        case GlfwEventType.WindowClose:
                            ProcessGlfwEventWindowClose(ev.WindowClose);
                            break;
                        case GlfwEventType.WindowFocus:
                            ProcessGlfwEventWindowFocus(ev.WindowFocus);
                            break;
                        case GlfwEventType.WindowSize:
                            ProcessGlfwEventWindowSize(ev.WindowSize);
                            break;
                        case GlfwEventType.WindowIconified:
                            ProcessGlfwEventWindowIconify(ev.WindowIconify);
                            break;
                        case GlfwEventType.WindowContentScale:
                            ProcessGlfwEventWindowContentScale(ev.WindowContentScale);
                            break;
                        default:
                            Logger.ErrorS("clyde.win", $"Unknown GLFW event type: {ev.Type}");
                            break;
                    }
                }
                catch (Exception e)
                {
                    Logger.ErrorS(
                        "clyde.win",
                        $"Caught exception in windowing event ({ev.Type}):\n{e}");
                }
            }

            _glfwEventQueue.Clear();
            if (_glfwEventQueue.Capacity > EventQueueSize)
            {
                _glfwEventQueue.TrimCapacity(EventQueueSize);
            }
        }

        // Disabling inlining so that I can easily exclude it from profiles.
        // Doesn't matter anyways, it's a few extra cycles per frame.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void SwapAllBuffers()
        {
            foreach (var window in _windows)
            {
                if (!window.IsMainWindow)
                {
                    GLFW.SwapBuffers(window.GlfwWindow);
                }
            }

            // Do main window last since it has vsync.
            SwapMainBuffers();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void SwapMainBuffers()
        {
            GLFW.SwapBuffers(_mainWindow!.GlfwWindow);
        }

        protected override void VSyncChanged()
        {
            if (!_glfwInitialized)
            {
                return;
            }

            GLFW.SwapInterval(VSync ? 1 : 0);
        }

        protected override void WindowModeChanged()
        {
            if (_mainWindow == null)
            {
                return;
            }

            if (WindowMode == WindowMode.Fullscreen)
            {
                GLFW.GetWindowSize(_mainWindow.GlfwWindow, out var w, out var h);
                _mainWindow.PrevWindowSize = (w, h);

                GLFW.GetWindowPos(_mainWindow.GlfwWindow, out var x, out var y);
                _mainWindow.PrevWindowPos = (x, y);
                var monitor = MonitorForWindow(_mainWindow.GlfwWindow);
                var mode = GLFW.GetVideoMode(monitor);

                GLFW.SetWindowMonitor(
                    _mainWindow.GlfwWindow,
                    monitor,
                    0, 0,
                    mode->Width, mode->Height,
                    mode->RefreshRate);
            }
            else
            {
                GLFW.SetWindowMonitor(
                    _mainWindow.GlfwWindow,
                    null,
                    _mainWindow.PrevWindowPos.X, _mainWindow.PrevWindowPos.Y,
                    _mainWindow.PrevWindowSize.X, _mainWindow.PrevWindowSize.Y, 0);
            }
        }

        // glfwGetWindowMonitor only works for fullscreen windows.
        // Picks the monitor with the top-left corner of the window.
        private Monitor* MonitorForWindow(Window* window)
        {
            GLFW.GetWindowPos(window, out var winPosX, out var winPosY);
            var monitors = GLFW.GetMonitorsRaw(out var count);
            for (var i = 0; i < count; i++)
            {
                var monitor = monitors[i];
                GLFW.GetMonitorPos(monitor, out var monPosX, out var monPosY);
                var videoMode = GLFW.GetVideoMode(monitor);

                var box = Box2i.FromDimensions(monPosX, monPosY, videoMode->Width, videoMode->Height);
                if (box.Contains(winPosX, winPosY))
                    return monitor;
            }

            // Fallback
            return GLFW.GetPrimaryMonitor();
        }

        string IClipboardManager.GetText()
        {
            return GLFW.GetClipboardString(_mainWindow!.GlfwWindow);
        }

        void IClipboardManager.SetText(string text)
        {
            GLFW.SetClipboardString(_mainWindow!.GlfwWindow, text);
        }

        public IEnumerable<IClydeMonitor> EnumerateMonitors()
        {
            return _monitors.Values.Select(c => c.Impl);
        }

        private static void SetWindowVisible(WindowReg reg, bool visible)
        {
            reg.IsVisible = visible;

            if (visible)
            {
                GLFW.ShowWindow(reg.GlfwWindow);
            }
            else
            {
                GLFW.HideWindow(reg.GlfwWindow);
            }
        }

        private sealed class WindowReg
        {
            public Vector2 WindowScale;
            public Vector2 PixelRatio;
            public Vector2i FramebufferSize;
            public Vector2i WindowSize;
            public Vector2i PrevWindowSize;
            public Vector2i PrevWindowPos;
            public Vector2 LastMousePos;
            public bool IsFocused;
            public bool IsMinimized;
            public string Title = "";
            public bool IsVisible;

            public bool IsMainWindow;
            public Window* GlfwWindow;
            public WindowHandle Handle = default!;
            public RenderTexture? RenderTexture;
            public GLHandle QuadVao;
            public Action<WindowClosedEventArgs>? Closed;
        }

        private sealed class WindowHandle : IClydeWindow
        {
            // So funny story
            // When this class was a record, the C# compiler on .NET 5 stack overflowed
            // while compiling the Closed event.
            // VERY funny.

            private readonly Clyde _clyde;
            private readonly WindowReg _reg;

            public bool IsDisposed { get; set; }

            public WindowHandle(Clyde clyde, WindowReg reg)
            {
                _clyde = clyde;
                _reg = reg;
            }

            public void Dispose()
            {
            }

            public Vector2i Size => _reg.FramebufferSize;

            public IRenderTarget RenderTarget
            {
                get
                {
                    if (_reg.IsMainWindow)
                    {
                        return _clyde._mainMainWindowRenderMainTarget;
                    }

                    return _reg.RenderTexture!;
                }
            }

            public string Title
            {
                get => _reg.Title;
                set => _clyde.SetWindowTitle(_reg, value);
            }

            public bool IsFocused => _reg.IsFocused;
            public bool IsMinimized => _reg.IsMinimized;

            public bool IsVisible
            {
                get => _reg.IsVisible;
                set => SetWindowVisible(_reg, value);
            }

            public event Action<WindowClosedEventArgs> Closed
            {
                add => _reg.Closed += value;
                remove => _reg.Closed -= value;
            }
        }

        private enum GlfwEventType
        {
            Invalid = 0,
            MouseButton,
            CursorPos,
            Scroll,
            Key,
            Char,
            Monitor,
            WindowClose,
            WindowFocus,
            WindowSize,
            WindowIconified,
            WindowContentScale,
        }

#pragma warning disable 649
        // ReSharper disable NotAccessedField.Local
        [StructLayout(LayoutKind.Explicit)]
        private struct GlfwEvent
        {
            [FieldOffset(0)] public GlfwEventType Type;

            [FieldOffset(0)] public GlfwEventMouseButton MouseButton;
            [FieldOffset(0)] public GlfwEventCursorPos CursorPos;
            [FieldOffset(0)] public GlfwEventScroll Scroll;
            [FieldOffset(0)] public GlfwEventKey Key;
            [FieldOffset(0)] public GlfwEventChar Char;
            [FieldOffset(0)] public GlfwEventWindowClose WindowClose;
            [FieldOffset(0)] public GlfwEventWindowSize WindowSize;
            [FieldOffset(0)] public GlfwEventWindowContentScale WindowContentScale;
            [FieldOffset(0)] public GlfwEventWindowIconify WindowIconify;
            [FieldOffset(0)] public GlfwEventWindowFocus WindowFocus;
            [FieldOffset(0)] public GlfwEventMonitor Monitor;
        }

        private struct GlfwEventMouseButton
        {
            public GlfwEventType Type;

            public Window* Window;
            public MouseButton Button;
            public InputAction Action;
            public KeyModifiers Mods;
        }

        private struct GlfwEventCursorPos
        {
            public GlfwEventType Type;

            public Window* Window;
            public double XPos;
            public double YPos;
        }

        private struct GlfwEventScroll
        {
            public GlfwEventType Type;

            public Window* Window;
            public double XOffset;
            public double YOffset;
        }

        private struct GlfwEventKey
        {
            public GlfwEventType Type;

            public Window* Window;
            public Keys Key;
            public int ScanCode;
            public InputAction Action;
            public KeyModifiers Mods;
        }

        private struct GlfwEventChar
        {
            public GlfwEventType Type;

            public Window* Window;
            public uint CodePoint;
        }

        private struct GlfwEventWindowClose
        {
            public GlfwEventType Type;

            public Window* Window;
        }

        private struct GlfwEventWindowSize
        {
            public GlfwEventType Type;

            public Window* Window;
            public int Width;
            public int Height;
        }

        private struct GlfwEventWindowContentScale
        {
            public GlfwEventType Type;

            public Window* Window;
            public float XScale;
            public float YScale;
        }

        private struct GlfwEventWindowIconify
        {
            public GlfwEventType Type;

            public Window* Window;
            public bool Iconified;
        }

        private struct GlfwEventWindowFocus
        {
            public GlfwEventType Type;

            public Window* Window;
            public bool Focused;
        }

        private struct GlfwEventMonitor
        {
            public GlfwEventType Type;

            public Monitor* Monitor;
            public ConnectedState State;
        }
        // ReSharper restore NotAccessedField.Local
#pragma warning restore 649

        private sealed class MonitorReg
        {
            public int Id;
            public Monitor* Monitor;
            public ClydeMonitorImpl Impl = default!;
        }

        private sealed class ClydeMonitorImpl : IClydeMonitor
        {
            public ClydeMonitorImpl(int id, string name, Vector2i size, int refreshRate)
            {
                Id = id;
                Name = name;
                Size = size;
                RefreshRate = refreshRate;
            }

            public int Id { get; }
            public string Name { get; }
            public Vector2i Size { get; }
            public int RefreshRate { get; }
        }

        [Serializable]
        public class GlfwException : Exception
        {
            public GlfwException()
            {
            }

            public GlfwException(string message) : base(message)
            {
            }

            public GlfwException(string message, Exception inner) : base(message, inner)
            {
            }

            protected GlfwException(
                SerializationInfo info,
                StreamingContext context) : base(info, context)
            {
            }
        }
    }
}
