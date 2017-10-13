﻿using System;
using System.Collections.Generic;
using System.Linq;
using OpenTK;
using SFML.Graphics;
using SFML.Window;
using SS14.Client.Graphics;
using SS14.Client.UserInterface.Components;
using SS14.Shared.Maths;

namespace SS14.Client.State.States
{
    /// <summary>
    ///     Options screen that displays all in-game options that can be changed.
    /// </summary>
    // Instantiated dynamically through the StateManager.
    public class OptionsMenu : State
    {
        private readonly Dictionary<string, VideoMode> _videoModeList = new Dictionary<string, VideoMode>();
        private Sprite _background;

        private Box2i _boundingArea;
        private Button _btnApply;
        private Button _btnBack;

        private Checkbox _chkFullScreen;
        private Checkbox _chkVSync;
        private Label _lblFullScreen;

        private Label _lblTitle;
        private Label _lblVSync;

        private Listbox _lstResolution;
        private int _previousScreenHeight;
        private int _previousScreenWidth;
        private Sprite _ticketBg;

        /// <summary>
        ///     Constructs an instance of this object.
        /// </summary>
        /// <param name="managers">A dictionary of common managers from the IOC system, so you don't have to resolve them yourself.</param>
        public OptionsMenu(IDictionary<Type, object> managers)
            : base(managers)
        {
            UpdateBounds();
        }

        private void UpdateBounds()
        {
            //TODO: This needs to go in form resize.
            var top = (int) (CluwneLib.Window.Viewport.Size.Y / 2f) - (int) (_boundingArea.Height / 2f);
            _boundingArea = Box2i.FromDimensions(0, top, 1000, 600);
        }

        private void InitializeGui()
        {
            _background = ResourceCache.GetSprite("mainbg");
            _ticketBg = ResourceCache.GetSprite("ticketoverlay");

            _lblTitle = new Label("Options", "CALIBRI", 48, ResourceCache);
            UserInterfaceManager.AddComponent(_lblTitle);

            _lblFullScreen = new Label("Fullscreen", "CALIBRI", ResourceCache);
            UserInterfaceManager.AddComponent(_lblFullScreen);

            _chkFullScreen = new Checkbox(ResourceCache);
            _chkFullScreen.ValueChanged += _chkFullScreen_ValueChanged;
            _chkFullScreen_ValueChanged(ConfigurationManager.GetCVar<bool>("display.fullscreen"), _chkFullScreen);
            UserInterfaceManager.AddComponent(_chkFullScreen);

            _lblVSync = new Label("Vsync", "CALIBRI", ResourceCache);
            UserInterfaceManager.AddComponent(_lblVSync);

            _chkVSync = new Checkbox(ResourceCache);
            _chkVSync.ValueChanged += _chkVSync_ValueChanged;
            _chkVSync_ValueChanged(ConfigurationManager.GetCVar<bool>("display.vsync"), _chkVSync);
            UserInterfaceManager.AddComponent(_chkVSync);

            _lstResolution = new Listbox(250, 150, ResourceCache);
            _lstResolution.ItemSelected += _lstResolution_ItemSelected;
            PopulateAvailableVideoModes();
            UserInterfaceManager.AddComponent(_lstResolution);

            _btnBack = new Button("Back", ResourceCache);
            _btnBack.Clicked += _btnBack_Clicked;
            UserInterfaceManager.AddComponent(_btnBack);

            _btnApply = new Button("Apply Settings", ResourceCache);
            _btnApply.Clicked += _btnApply_Clicked;
            UserInterfaceManager.AddComponent(_btnApply);

            UpdateGuiPosition();
        }

        private void UpdateGuiPosition()
        {
            //TODO: This needs to go in form resize.
            const int sectionPadding = 50;
            const int optionPadding = 10;
            const int labelPadding = 3;

            _lblTitle.Position = new Vector2i(_boundingArea.Left + 10, _boundingArea.Top + 10);
            _lblTitle.Update(0);

            _lstResolution.Position = new Vector2i(_boundingArea.Left + sectionPadding,
                _lblTitle.Position.Y + _lblTitle.ClientArea.Height + sectionPadding);
            _lstResolution.Update(0);

            _chkFullScreen.Position = new Vector2i(_lstResolution.Position.X,
                _lstResolution.Position.Y + _lstResolution.ClientArea.Height + sectionPadding);
            _chkFullScreen.Update(0);
            _lblFullScreen.Position = new Vector2i(_chkFullScreen.Position.X + _chkFullScreen.ClientArea.Width + labelPadding,
                _chkFullScreen.Position.Y);
            _lblFullScreen.Update(0);

            _chkVSync.Position = new Vector2i(_lblFullScreen.Position.X,
                _lblFullScreen.Position.Y + _lblFullScreen.ClientArea.Height + optionPadding);
            _chkVSync.Update(0);
            _lblVSync.Position = new Vector2i(_chkVSync.Position.X + _chkVSync.ClientArea.Width + labelPadding,
                _chkVSync.Position.Y);
            _lblVSync.Update(0);

            _btnApply.Position = new Vector2i(_boundingArea.Left + _boundingArea.Width - (_btnApply.ClientArea.Width + sectionPadding),
                _boundingArea.Top + _boundingArea.Height - (_btnApply.ClientArea.Height + sectionPadding));
            _btnApply.Update(0);
            _btnBack.Position = new Vector2i(_btnApply.Position.X - (_btnBack.ClientArea.Width + optionPadding), _btnApply.Position.Y);
            _btnBack.Update(0);
        }

        private void PopulateAvailableVideoModes()
        {
            _lstResolution.ClearItems();
            _videoModeList.Clear();

            var modes = from v in VideoMode.FullscreenModes
                where v.Height > 748 && v.Width > 1024 //GOSH I HOPE NO ONES USING 16 BIT COLORS. OR RUNNING AT LESS THAN 59 hz
                orderby v.Height * v.Width
                select v;

            if (!modes.Any())
                throw new InvalidOperationException("No available video modes");

            foreach (var vm in modes)
            {
                if (!_videoModeList.ContainsKey(GetVmString(vm)))
                {
                    _videoModeList.Add(GetVmString(vm), vm);
                    _lstResolution.AddItem(GetVmString(vm));
                }
            }

            if (
                _videoModeList.Any(
                    x =>
                        x.Value.Width == CluwneLib.Window.Viewport.Size.X && x.Value.Height == CluwneLib.Window.Viewport.Size.Y))

            {
                var currentMode =
                    _videoModeList.FirstOrDefault(
                        x =>
                            x.Value.Width == CluwneLib.Window.Viewport.Size.X &&
                            x.Value.Height == CluwneLib.Window.Viewport.Size.Y);

                _lstResolution.SelectItem(currentMode.Key);
            }
            else
            {
                //No match due to different refresh rate in windowed mode. Just pick first resolution based on size only.
                var currentMode =
                    _videoModeList.FirstOrDefault(
                        x =>
                            x.Value.Width == CluwneLib.Window.Viewport.Size.X &&
                            x.Value.Height == CluwneLib.Window.Viewport.Size.Y);
                _lstResolution.SelectItem(currentMode.Key);
            }
        }

        /// <inheritdoc />
        public override void Startup()
        {
            NetworkManager.ClientDisconnect("Client killed old session."); //TODO: Is this really needed here?
            InitializeGui();
        }

        /// <inheritdoc />
        public override void Shutdown()
        {
            UserInterfaceManager.DisposeAllComponents();
        }

        /// <inheritdoc />
        public override void Update(FrameEventArgs e)
        {
            if (CluwneLib.Window.Viewport.Size.X != _previousScreenWidth || CluwneLib.Window.Viewport.Size.Y != _previousScreenHeight)
            {
                _previousScreenHeight = (int) CluwneLib.Window.Viewport.Size.Y;
                _previousScreenWidth = (int) CluwneLib.Window.Viewport.Size.X;
                UpdateBounds();
                UpdateGuiPosition();
            }

            _chkFullScreen.Value = ConfigurationManager.GetCVar<bool>("display.fullscreen");
            UserInterfaceManager.Update(e);
        }

        /// <inheritdoc />
        public override void Render(FrameEventArgs e)
        {
            _background.SetTransformToRect(Box2i.FromDimensions(0, 0, (int) CluwneLib.Window.Viewport.Size.X, (int) CluwneLib.Window.Viewport.Size.Y));
            _background.Draw();

            _ticketBg.SetTransformToRect(_boundingArea);
            _ticketBg.Draw();
            UserInterfaceManager.Render(e);
        }

        /// <inheritdoc />
        public override void FormResize() { }

        /// <inheritdoc />
        public override void KeyDown(KeyEventArgs e)
        {
            UserInterfaceManager.KeyDown(e);
        }

        /// <inheritdoc />
        public override void KeyUp(KeyEventArgs e) { }

        /// <inheritdoc />
        public override void MouseUp(MouseButtonEventArgs e)
        {
            UserInterfaceManager.MouseUp(e);
        }

        /// <inheritdoc />
        public override void MouseDown(MouseButtonEventArgs e)
        {
            UserInterfaceManager.MouseDown(e);
        }

        /// <inheritdoc />
        public override void MouseMoved(MouseMoveEventArgs e) { }

        /// <inheritdoc />
        public override void MousePressed(MouseButtonEventArgs e)
        {
            UserInterfaceManager.MouseDown(e);
        }

        /// <inheritdoc />
        public override void MouseMove(MouseMoveEventArgs e)
        {
            UserInterfaceManager.MouseMove(e);
        }

        /// <inheritdoc />
        public override void MouseWheelMove(MouseWheelEventArgs e)
        {
            UserInterfaceManager.MouseWheelMove(e);
        }

        /// <inheritdoc />
        public override void MouseEntered(EventArgs e)
        {
            UserInterfaceManager.MouseEntered(e);
        }

        /// <inheritdoc />
        public override void MouseLeft(EventArgs e)
        {
            UserInterfaceManager.MouseLeft(e);
        }

        /// <inheritdoc />
        public override void TextEntered(TextEventArgs e)
        {
            UserInterfaceManager.TextEntered(e);
        }

        private void _chkVSync_ValueChanged(bool newValue, Checkbox sender)
        {
            ConfigurationManager.SetCVar("display.vsync", newValue);
        }

        private void _chkFullScreen_ValueChanged(bool newValue, Checkbox sender)
        {
            ConfigurationManager.SetCVar("display.fullscreen", newValue);
        }

        private void ApplyVideoMode()
        {
            CluwneLib.UpdateVideoSettings();
        }

        private void _lstResolution_ItemSelected(Label item, Listbox sender)
        {
            if (_videoModeList.ContainsKey(item.Text.Text))
            {
                var sel = _videoModeList[item.Text.Text];
                ConfigurationManager.SetCVar("display.width", (int) sel.Width);
                ConfigurationManager.SetCVar("display.height", (int) sel.Height);
            }
        }

        private string GetVmString(VideoMode vm)
        {
            return $"{vm.Width} x {vm.Height} @ {vm.BitsPerPixel}hz";
        }

        private void _btnApply_Clicked(Button sender)
        {
            ApplyVideoMode();
        }

        private void _btnBack_Clicked(Button sender)
        {
            StateManager.RequestStateChange<MainScreen>();
        }
    }
}
