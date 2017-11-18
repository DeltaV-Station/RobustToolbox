﻿using Lidgren.Network;
using OpenTK;
using SS14.Client.GameObjects;
using SS14.Client.Graphics;
using SS14.Client.Graphics.Input;
using SS14.Client.Graphics.Lighting;
using SS14.Client.Graphics.Render;
using SS14.Client.Graphics.Shader;
using SS14.Client.Graphics.Sprites;
using SS14.Client.Graphics.Textures;
using SS14.Client.Graphics.Views;
using SS14.Client.Helpers;
using SS14.Client.Interfaces.GameObjects;
using SS14.Client.Interfaces.GameObjects.Components;
using SS14.Client.Interfaces.Player;
using SS14.Client.Interfaces.Resource;
using SS14.Client.Map;
using SS14.Client.ResourceManagement;
using SS14.Client.UserInterface.Components;
using SS14.Shared;
using SS14.Shared.Configuration;
using SS14.Shared.GameObjects;
using SS14.Shared.Interfaces.Configuration;
using SS14.Shared.Interfaces.GameObjects;
using SS14.Shared.Interfaces.GameObjects.Components;
using SS14.Shared.Interfaces.Map;
using SS14.Shared.Interfaces.Serialization;
using SS14.Shared.Interfaces.Timing;
using SS14.Shared.IoC;
using SS14.Shared.Map;
using SS14.Shared.Maths;
using SS14.Shared.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using FrameEventArgs = SS14.Client.Graphics.FrameEventArgs;
using Vector2 = SS14.Shared.Maths.Vector2;
using Vector2i = SS14.Shared.Maths.Vector2i;
using SS14.Client.UserInterface;
using SS14.Client.UserInterface.Controls;
using SS14.Client.UserInterface.CustomControls;
using SS14.Shared.ContentPack;

namespace SS14.Client.State.States
{
    public class GameScreen : State
    {
        #region Variables
        private bool _recalculateScene = true;
        private bool _redrawOverlay = true;
        private bool _redrawTiles = true;
        private bool _showDebug; // show AABBs & Bounding Circles on Entities.

        private RenderImage _baseTarget;
        private Sprite _baseTargetSprite;

        private IClientEntityManager _entityManager;
        private IComponentManager _componentManager;

        private GaussianBlur _gaussianBlur;

        private List<RenderImage> _cleanupList = new List<RenderImage>();
        private List<Sprite> _cleanupSpriteList = new List<Sprite>();

        private SpriteBatch _floorBatch;
        private SpriteBatch _gasBatch;
        private SpriteBatch _decalBatch;

        #region Mouse/Camera stuff
        public ScreenCoordinates MousePosScreen = new ScreenCoordinates(0, 0, 0);
        public LocalCoordinates MousePosWorld = new LocalCoordinates(0, 0, 0, 0);
        private View view;
        #endregion Mouse/Camera stuff

        #region UI Variables

        private Screen _uiScreen;

        private int _prevScreenWidth = 0;
        private int _prevScreenHeight = 0;

        private MenuWindow _menu;
        private Chatbox _gameChat;
        #endregion UI Variables

        #region Lighting
        private Sprite _lightTargetIntermediateSprite;
        private Sprite _lightTargetSprite;

        private bool bPlayerVision = false;
        /// <summary>
        ///     True if lighting is disabled.
        /// </summary>
        private bool bFullVision = false;
        private bool debugWallOccluders = false;
        private bool debugPlayerShadowMap = false;
        private bool debugHitboxes = false;
        public bool BlendLightMap = true;

        private TechniqueList LightblendTechnique;
        private GLSLShader Lightmap;

        private LightArea lightArea1024;
        private LightArea lightArea128;
        private LightArea lightArea256;
        private LightArea lightArea512;

        private ILight playerVision;
        private ISS14Serializer serializer;

        public RenderImage PlayerOcclusionTarget { get; private set; }
        public RenderImage OccluderDebugTarget { get; private set; }
        public RenderImage LightTarget { get; private set; }
        public RenderImage LightTargetIntermediate { get; private set; }
        public RenderImage ComposedSceneTarget { get; private set; }
        public RenderImage OverlayTarget { get; private set; }
        public RenderImage SceneTarget { get; private set; }
        public RenderImage TilesTarget { get; private set; }
        public RenderImage ScreenShadows { get; private set; }
        public RenderImage ShadowBlendIntermediate { get; private set; }
        public RenderImage ShadowIntermediate { get; private set; }

        private ShadowMapResolver shadowMapResolver;

        #endregion Lighting

        private GameScreenDebug DebugManager;

        #endregion Variables

        public GameScreen(IDictionary<Type, object> managers) : base(managers)
        {
            if (Environment.OSVersion.Platform == PlatformID.MacOSX || Environment.OSVersion.Platform == PlatformID.Unix)
            {
                // Disable lighting on non-Windows versions by default because the rendering is broken.
                bFullVision = true;
            }
        }

        #region IState Members

        public override void Startup()
        {
            var manager = IoCManager.Resolve<IConfigurationManager>();
            manager.RegisterCVar("player.name", "Joe Genero", CVar.ARCHIVE);

            _cleanupList = new List<RenderImage>();
            _cleanupSpriteList = new List<Sprite>();

            UserInterfaceManager.AddComponent(_uiScreen);

            //Init serializer
            serializer = IoCManager.Resolve<ISS14Serializer>();

            _entityManager = IoCManager.Resolve<IClientEntityManager>();
            _componentManager = IoCManager.Resolve<IComponentManager>();
            IoCManager.Resolve<IMapManager>().OnTileChanged += OnTileChanged;
            IoCManager.Resolve<IPlayerManager>().OnPlayerMove += OnPlayerMove;

            NetworkManager.MessageArrived += NetworkManagerMessageArrived;

            NetOutgoingMessage message = NetworkManager.CreateMessage();
            message.Write((byte)NetMessages.RequestMap);
            NetworkManager.ClientSendMessage(message, NetDeliveryMethod.ReliableUnordered);

            // TODO This should go somewhere else, there should be explicit session setup and teardown at some point.
            var message1 = NetworkManager.CreateMessage();
            message1.Write((byte)NetMessages.ClientName);
            message1.Write(ConfigurationManager.GetCVar<string>("player.name"));
            NetworkManager.ClientSendMessage(message1, NetDeliveryMethod.ReliableOrdered);

            // Create new
            _gaussianBlur = new GaussianBlur(ResourceCache);

            InitializeRenderTargets();
            InitializeSpriteBatches();
            InitalizeLighting();

            DebugManager = new GameScreenDebug(this);
            FormResizeUI();
        }

        private void InitializeRenderTargets()
        {
            var width = CluwneLib.Window.Viewport.Size.X;
            var height = CluwneLib.Window.Viewport.Size.Y;
            view = new View(Vector2.Zero, new Vector2(width, height));
            UpdateView(false);
            _baseTarget = new RenderImage("baseTarget", width, height, true);
            _cleanupList.Add(_baseTarget);

            _baseTargetSprite = new Sprite(_baseTarget.Texture);
            _cleanupSpriteList.Add(_baseTargetSprite);

            SceneTarget = new RenderImage("sceneTarget", width, height, true)
            {
                View = view
            };
            _cleanupList.Add(SceneTarget);
            TilesTarget = new RenderImage("tilesTarget", width, height, true)
            {
                View = view
            };
            _cleanupList.Add(TilesTarget);

            OverlayTarget = new RenderImage("OverlayTarget", width, height, true);
            _cleanupList.Add(OverlayTarget);

            OverlayTarget.BlendSettings.ColorSrcFactor = BlendMode.Factor.SrcAlpha;
            OverlayTarget.BlendSettings.ColorDstFactor = BlendMode.Factor.OneMinusSrcAlpha;
            OverlayTarget.BlendSettings.AlphaSrcFactor = BlendMode.Factor.SrcAlpha;
            OverlayTarget.BlendSettings.AlphaDstFactor = BlendMode.Factor.OneMinusSrcAlpha;

            ComposedSceneTarget = new RenderImage("composedSceneTarget", width, height,
                                                 ImageBufferFormats.BufferRGB888A8);
            _cleanupList.Add(ComposedSceneTarget);

            LightTarget = new RenderImage("lightTarget", width, height, ImageBufferFormats.BufferRGB888A8);

            _cleanupList.Add(LightTarget);
            _lightTargetSprite = new Sprite(LightTarget.Texture);

            _cleanupSpriteList.Add(_lightTargetSprite);

            LightTargetIntermediate = new RenderImage("lightTargetIntermediate", width, height,
                                                      ImageBufferFormats.BufferRGB888A8);
            _cleanupList.Add(LightTargetIntermediate);
            _lightTargetIntermediateSprite = new Sprite(LightTargetIntermediate.Texture);
            _cleanupSpriteList.Add(_lightTargetIntermediateSprite);
        }

        private void InitializeSpriteBatches()
        {
            _gasBatch = new SpriteBatch();
            _gasBatch.BlendingSettings.ColorSrcFactor = BlendMode.Factor.SrcAlpha;
            _gasBatch.BlendingSettings.ColorDstFactor = BlendMode.Factor.OneMinusDstAlpha;
            _gasBatch.BlendingSettings.AlphaSrcFactor = BlendMode.Factor.SrcAlpha;
            _gasBatch.BlendingSettings.AlphaDstFactor = BlendMode.Factor.OneMinusSrcAlpha;

            _decalBatch = new SpriteBatch();
            _decalBatch.BlendingSettings.ColorSrcFactor = BlendMode.Factor.SrcAlpha;
            _decalBatch.BlendingSettings.ColorDstFactor = BlendMode.Factor.OneMinusDstAlpha;
            _decalBatch.BlendingSettings.AlphaSrcFactor = BlendMode.Factor.SrcAlpha;
            _decalBatch.BlendingSettings.AlphaDstFactor = BlendMode.Factor.OneMinusSrcAlpha;

            _floorBatch = new SpriteBatch();
        }

        private Vector2i _gameChatSize = new Vector2i(475, 175); // TODO: Move this magic variable

        /// <inheritdoc />
        public override void InitializeGUI()
        {
            _uiScreen = new Screen();
            _uiScreen.DrawBackground = false;
            _uiScreen.DrawBorder = false;

            // Setup the ESC Menu
            _menu = new MenuWindow();
            _menu.Alignment = Align.HCenter | Align.VCenter;
            _menu.Visible = false;
            _uiScreen.AddControl(_menu);

            //Init GUI components
            _gameChat = new Chatbox(_gameChatSize);
            _gameChat.Alignment = Align.Right;
            _gameChat.Size = new Vector2i(475, 175);
            _gameChat.Resize += (sender, args) => { _gameChat.LocalPosition = new Vector2i(-10 + -_gameChat.Size.X, 10); };
            _gameChat.TextSubmitted += ChatTextboxTextSubmitted;
            _uiScreen.AddControl(_gameChat);
        }

        private void InitalizeLighting()
        {
            shadowMapResolver = new ShadowMapResolver(ShadowmapSize.Size1024, ShadowmapSize.Size1024);

            var lightManager = IoCManager.Resolve<ILightManager>();
            var resourceCache = IoCManager.Resolve<IResourceCache>();

            var reductionEffectTechnique = resourceCache.GetTechnique("reductionEffect");
            var resolveShadowsEffectTechnique = resourceCache.GetTechnique("resolveShadowsEffect");
            shadowMapResolver.LoadContent(reductionEffectTechnique, resolveShadowsEffectTechnique);

            lightManager.LightMask = resourceCache.GetSprite("whitemask");
            lightArea128 = new LightArea(ShadowmapSize.Size128, lightManager.LightMask);
            lightArea256 = new LightArea(ShadowmapSize.Size256, lightManager.LightMask);
            lightArea512 = new LightArea(ShadowmapSize.Size512, lightManager.LightMask);
            lightArea1024 = new LightArea(ShadowmapSize.Size1024, lightManager.LightMask);

            var width = CluwneLib.Window.Viewport.Size.X;
            var height = CluwneLib.Window.Viewport.Size.Y;
            ScreenShadows = new RenderImage("screenShadows", width, height, ImageBufferFormats.BufferRGB888A8);

            _cleanupList.Add(ScreenShadows);
            ScreenShadows.UseDepthBuffer = false;
            ShadowIntermediate = new RenderImage("shadowIntermediate", width, height, ImageBufferFormats.BufferRGB888A8);
            _cleanupList.Add(ShadowIntermediate);
            ShadowIntermediate.UseDepthBuffer = false;
            ShadowBlendIntermediate = new RenderImage("shadowBlendIntermediate", width, height, ImageBufferFormats.BufferRGB888A8);
            _cleanupList.Add(ShadowBlendIntermediate);
            ShadowBlendIntermediate.UseDepthBuffer = false;
            PlayerOcclusionTarget = new RenderImage("playerOcclusionTarget", width, height, ImageBufferFormats.BufferRGB888A8);
            _cleanupList.Add(PlayerOcclusionTarget);
            PlayerOcclusionTarget.UseDepthBuffer = false;

            LightblendTechnique = resourceCache.GetTechnique("lightblend");
            Lightmap = resourceCache.GetShader("lightmap");

            playerVision = lightManager.CreateLight();
            playerVision.Color = Color.Blue;
            playerVision.Radius = 1024;
            playerVision.Coordinates = new LocalCoordinates(0, 0, 0, 0);

            OccluderDebugTarget = new RenderImage("debug", width, height);
        }

        /// <inheritdoc />
        public override void FormResize()
        {
            FormResizeUI();
            ResetRendertargets();
            _redrawTiles = true;
            IoCManager.Resolve<ILightManager>().RecalculateLights();
            RecalculateScene();

            base.FormResize();
        }

        private void FormResizeUI()
        {
            _uiScreen.Width = (int)CluwneLib.Window.Viewport.Size.X;
            _uiScreen.Height = (int)CluwneLib.Window.Viewport.Size.Y;

            UserInterfaceManager.ResizeComponents();
        }

        /// <inheritdoc />
        public override void Update(FrameEventArgs e)
        {
            base.Update(e);

            _componentManager.Update(e.Elapsed);
            _entityManager.Update(e.Elapsed);
            PlacementManager.Update(MousePosScreen);
            PlayerManager.Update(e.Elapsed);

            if (PlayerManager.ControlledEntity != null)
            {
                var newpos = PlayerManager.ControlledEntity.GetComponent<ITransformComponent>().WorldPosition;
                if (CluwneLib.Camera.Position != newpos)
                {
                    CluwneLib.Camera.Position = newpos;
                    MousePosWorld = CluwneLib.ScreenToCoordinates(MousePosScreen); // Use WorldCenter to calculate, so we need to update again
                    UpdateView();
                }
            }
        }

        private void UpdateView(bool updateTargets = true)
        {
            view.Center = CluwneLib.Camera.Position * CluwneLib.Camera.PixelsPerMeter;
            if (updateTargets)
            {
                SceneTarget.View = view;
                TilesTarget.View = view;
            }
        }

        /// <inheritdoc />
        public override void Render(FrameEventArgs e)
        {
            CluwneLib.Window.Graphics.Clear(Color.Black);

            CalculateAllLights();

            if (PlayerManager.ControlledEntity == null)
            {
                return;
            }

            // vp is the rectangle in which we can render in world space.
            var vp = CluwneLib.WorldViewport;
            var map = PlayerManager.ControlledEntity.GetComponent<ITransformComponent>().MapID;

            if (!bFullVision)
            {
                ILight[] lights = IoCManager.Resolve<ILightManager>().LightsIntersectingRect(vp);

                // Render the lightmap
                RenderLightsIntoMap(lights);
            }

            CalculateSceneBatches(vp);

            //Draw all rendertargets to the scenetarget
            SceneTarget.BeginDrawing();
            SceneTarget.Clear(Color.Black);

            //PreOcclusion
            RenderTiles();

            SceneTarget.View = view;
            RenderComponents(e.Elapsed, vp, map);

            RenderOverlay();

            SceneTarget.EndDrawing();
            SceneTarget.ResetCurrentRenderTarget();
            //_sceneTarget.Blit(0, 0, CluwneLib.Window.Size.X, CluwneLib.Window.Size.Y);

            //Debug.DebugRendertarget(_sceneTarget);

            if (bFullVision)
                SceneTarget.Blit(0, 0, CluwneLib.Window.Viewport.Size.X, CluwneLib.Window.Viewport.Size.Y);
            else
                LightScene();

            DebugManager.RenderDebug(vp, map);

            //Render the placement manager shit
            PlacementManager.Render();
        }

        private void RenderTiles()
        {
            if (_redrawTiles)
            {
                //Set rendertarget to draw the rest of the scene
                TilesTarget.BeginDrawing();
                TilesTarget.Clear(Color.Black);

                if (_floorBatch.Count > 0)
                {
                    TilesTarget.Draw(_floorBatch);
                }

                TilesTarget.EndDrawing();
                _redrawTiles = false;
            }

            TilesTarget.Blit(0, 0, TilesTarget.Width, TilesTarget.Height, Color.White, BlitterSizeMode.Scale);
        }

        private void RenderOverlay()
        {
            if (_redrawOverlay)
            {
                OverlayTarget.BeginDrawing();
                OverlayTarget.Clear(Color.Transparent);

                // Render decal batch

                if (_decalBatch.Count > 0)
                    OverlayTarget.Draw(_decalBatch);

                if (_gasBatch.Count > 0)
                    OverlayTarget.Draw(_gasBatch);

                _redrawOverlay = false;
                OverlayTarget.EndDrawing();
            }

            OverlayTarget.Blit(0, 0, TilesTarget.Width, TilesTarget.Height, Color.White, BlitterSizeMode.Crop);
        }

        /// <inheritdoc />
        public override void Shutdown()
        {
            UserInterfaceManager.RemoveComponent(_uiScreen);

            IoCManager.Resolve<IPlayerManager>().Detach();

            //TODO: Are these lists actually needed?
            //_cleanupSpriteList.ForEach(s => s.Dispose());
            _cleanupSpriteList.Clear();
            _cleanupList.ForEach(t => { t.Dispose(); });
            _cleanupList.Clear();

            shadowMapResolver.Dispose();
            _entityManager.Shutdown();
            UserInterfaceManager.DisposeAllComponents();
            NetworkManager.MessageArrived -= NetworkManagerMessageArrived;
            _decalBatch.Dispose();
            _floorBatch.Dispose();
            _gasBatch.Dispose();
            GC.Collect();
        }

        #endregion IState Members

        #region Input

        #region Keyboard
        public override void KeyDown(KeyEventArgs e)
        {
            if (UserInterfaceManager.KeyDown(e)) //KeyDown returns true if the click is handled by the ui component.
                return; // UI consumes key

            // pass key to game
            KeyBindingManager.KeyDown(e);

            if (e.Key == Keyboard.Key.F1)
            {
                //TODO FrameStats
                CluwneLib.FrameStatsVisible = !CluwneLib.FrameStatsVisible;
            }
            if (e.Key == Keyboard.Key.F2)
            {
                _showDebug = !_showDebug;
                CluwneLib.Debug.ToggleWallDebug();
                CluwneLib.Debug.ToggleAABBDebug();
                CluwneLib.Debug.ToggleGridDisplayDebug();
            }
            if (e.Key == Keyboard.Key.F3)
            {
                ToggleOccluderDebug();
            }
            if (e.Key == Keyboard.Key.F4)
            {
                debugHitboxes = !debugHitboxes;
            }
            if (e.Key == Keyboard.Key.F5)
            {
                PlayerManager.SendVerb("save", 0);
            }
            if (e.Key == Keyboard.Key.F6)
            {
                bFullVision = !bFullVision;
            }
            if (e.Key == Keyboard.Key.F7)
            {
                bPlayerVision = !bPlayerVision;
            }
            if (e.Key == Keyboard.Key.F8)
            {
                NetOutgoingMessage message = NetworkManager.CreateMessage();
                message.Write((byte)NetMessages.ForceRestart);
                NetworkManager.ClientSendMessage(message, NetDeliveryMethod.ReliableUnordered);
            }
            if (e.Key == Keyboard.Key.Escape)
            {
                _menu.Visible = !_menu.Visible;
            }
            if (e.Key == Keyboard.Key.F9)
            {
                UserInterfaceManager.ToggleMoveMode();
            }
            if (e.Key == Keyboard.Key.F10)
            {
                UserInterfaceManager.DisposeAllComponents<TileSpawnWindow>(); //Remove old ones.
                UserInterfaceManager.AddComponent(new TileSpawnWindow(new Vector2i(350, 410))); //Create a new one.
            }
            if (e.Key == Keyboard.Key.F11)
            {
                UserInterfaceManager.DisposeAllComponents<EntitySpawnWindow>(); //Remove old ones.
                UserInterfaceManager.AddComponent(new EntitySpawnWindow(new Vector2i(350, 410))); //Create a new one.
            }
        }

        public override void KeyUp(KeyEventArgs e)
        {
            KeyBindingManager.KeyUp(e);
        }

        public override void TextEntered(TextEventArgs e)
        {
            UserInterfaceManager.TextEntered(e);
        }
        #endregion Keyboard

        #region Mouse
        public override void MouseUp(MouseButtonEventArgs e)
        {
            UserInterfaceManager.MouseUp(e);
        }

        public override void MouseDown(MouseButtonEventArgs e)
        {
            if (PlayerManager.ControlledEntity == null)
                return;

            if (UserInterfaceManager.MouseDown(e))
                // MouseDown returns true if the click is handled by the ui component.
                return;

            if (PlacementManager.IsActive && !PlacementManager.Eraser)
            {
                switch (e.Button)
                {
                    case Mouse.Button.Left:
                        PlacementManager.HandlePlacement();
                        return;
                    case Mouse.Button.Right:
                        PlacementManager.Clear();
                        return;
                    case Mouse.Button.Middle:
                        PlacementManager.Rotate();
                        return;
                }
            }

            #region Object clicking

            // Find all the entities intersecting our click
            IEnumerable<IEntity> entities =
                _entityManager.GetEntitiesIntersecting(MousePosWorld.Position);

            // Check the entities against whether or not we can click them
            var clickedEntities = new List<ClickData>();
            foreach (IEntity entity in entities)
            {
                if (entity.TryGetComponent<IClientClickableComponent>(out var component)
                 && component.CheckClick(MousePosWorld, out int drawdepthofclicked))
                {
                    clickedEntities.Add(new ClickData(entity, drawdepthofclicked));
                }
            }

            if (!clickedEntities.Any())
            {
                return;
            }

            //Sort them by which we should click
            IEntity entToClick = (from cd in clickedEntities
                                  orderby cd.Drawdepth ascending,
                                      cd.Clicked.GetComponent<ITransformComponent>().LocalPosition
                                      .Y ascending
                                  select cd.Clicked).Last();

            if (PlacementManager.Eraser && PlacementManager.IsActive)
            {
                PlacementManager.HandleDeletion(entToClick);
                return;
            }

            // Check whether click is outside our 1.5 meter range
            float checkDistance = 1.5f;
            if (!PlayerManager.ControlledEntity.GetComponent<ITransformComponent>().LocalPosition.InRange(entToClick.GetComponent<ITransformComponent>().LocalPosition, checkDistance))
                return;

            var clickable = entToClick.GetComponent<IClientClickableComponent>();
            switch (e.Button)
            {
                case Mouse.Button.Left:
                    clickable.DispatchClick(PlayerManager.ControlledEntity, MouseClickType.Left);
                    break;
                case Mouse.Button.Right:
                    clickable.DispatchClick(PlayerManager.ControlledEntity, MouseClickType.Right);
                    break;
                case Mouse.Button.Middle:
                    UserInterfaceManager.DisposeAllComponents<PropEditWindow>();
                    UserInterfaceManager.AddComponent(new PropEditWindow(new Vector2i(400, 400), ResourceCache,
                                                                            entToClick));
                    return;
            }

            #endregion Object clicking
        }

        public override void MouseMove(MouseMoveEventArgs e)
        {
            if (PlayerManager.ControlledEntity != null && PlayerManager.ControlledEntity.TryGetComponent<ITransformComponent>(out var transform))
            {
                MousePosScreen = new ScreenCoordinates(e.NewPosition, transform.MapID);
            }
            else
            {
                MousePosScreen = new ScreenCoordinates(e.NewPosition, Shared.Map.MapManager.NULLSPACE);
            }
            MousePosWorld = CluwneLib.ScreenToCoordinates(MousePosScreen);
            UserInterfaceManager.MouseMove(e);
        }

        public override void MouseMoved(MouseMoveEventArgs e)
        {
            //TODO: Figure out what to do with this
        }

        public override void MousePressed(MouseButtonEventArgs e)
        {
            //TODO: Figure out what to do with this
        }

        public override void MouseWheelMove(MouseWheelScrollEventArgs e)
        {
            UserInterfaceManager.MouseWheelMove(e);
        }

        public override void MouseEntered(EventArgs e)
        {
            UserInterfaceManager.MouseEntered(e);
        }

        public override void MouseLeft(EventArgs e)
        {
            UserInterfaceManager.MouseLeft(e);
        }
        #endregion Mouse

        #region Chat
        private void HandleChatMessage(NetIncomingMessage msg)
        {
            var channel = (ChatChannel)msg.ReadByte();
            string text = msg.ReadString();
            int entityId = msg.ReadInt32();
            string message;
            switch (channel)
            {
                case ChatChannel.Ingame:
                case ChatChannel.Server:
                case ChatChannel.OOC:
                case ChatChannel.Radio:
                    message = "[" + channel + "] " + text;
                    break;
                default:
                    message = text;
                    break;
            }
            _gameChat.AddLine(message, channel);
            if (entityId > 0 && _entityManager.TryGetEntity(entityId, out IEntity a))
            {
                a.SendMessage(this, ComponentMessageType.EntitySaidSomething, channel, text);
            }
        }

        private void ChatTextboxTextSubmitted(Chatbox chatbox, string text)
        {
            SendChatMessage(text);
        }

        private void SendChatMessage(string text)
        {
            NetOutgoingMessage message = NetworkManager.CreateMessage();
            message.Write((byte)NetMessages.ChatMessage);
            message.Write((byte)ChatChannel.Player);
            message.Write(text);
            message.Write(-1);
            NetworkManager.ClientSendMessage(message, NetDeliveryMethod.ReliableUnordered);
        }

        #endregion Chat

        #endregion Input

        #region Event Handlers

        #region Messages

        private void NetworkManagerMessageArrived(object sender, NetMessageArgs args)
        {
            NetIncomingMessage message = args.RawMessage;
            if (message == null)
            {
                return;
            }
            switch (message.MessageType)
            {
                case NetIncomingMessageType.StatusChanged:
                    var statMsg = (NetConnectionStatus)message.ReadByte();
                    if (statMsg == NetConnectionStatus.Disconnected)
                    {
                        string disconnectMessage = message.ReadString();
                        UserInterfaceManager.AddComponent(new DisconnectedScreenBlocker(StateManager,
                                                                                        UserInterfaceManager,
                                                                                        ResourceCache,
                                                                                        disconnectMessage));
                    }
                    break;
                case NetIncomingMessageType.Data:
                    var messageType = (NetMessages)message.ReadByte();
                    switch (messageType)
                    {
                        case NetMessages.PlayerSessionMessage:
                            PlayerManager.HandleNetworkMessage(message);
                            break;
                        case NetMessages.PlacementManagerMessage:
                            PlacementManager.HandleNetMessage(message);
                            break;
                        case NetMessages.ChatMessage:
                            HandleChatMessage(message);
                            break;
                    }
                    break;
            }
        }

        #endregion Messages

        private void OnPlayerMove(object sender, MoveEventArgs args)
        {
            //Recalculate scene batches for drawing.
            RecalculateScene();
        }

        public void OnTileChanged(int gridId, TileRef tileRef, Tile oldTile)
        {
            IoCManager.Resolve<ILightManager>().RecalculateLightsInView(Box2.FromDimensions(tileRef.X, tileRef.Y, 1, 1));
            // Recalculate the scene batches.
            RecalculateScene();
        }

        #endregion Event Handlers

        #region Lighting in order of call

        /**
         *  Calculate lights In player view
         *  Render Lights in view to lightmap  > Screenshadows
         *
         *
         **/

        private void CalculateAllLights()
        {
            if (bFullVision)
            {
                return;
            }
            foreach
            (ILight l in IoCManager.Resolve<ILightManager>().GetLights().Where(l => l.LightArea.Calculated == false))
            {
                CalculateLightArea(l);
            }
        }

        /// <summary>
        /// Renders a set of lights into a single lightmap.
        /// If a light hasn't been prerendered yet, it renders that light.
        /// </summary>
        /// <param name="lights">Array of lights</param>
        private void RenderLightsIntoMap(IEnumerable<ILight> lights)
        {
            //Step 1 - Calculate lights that haven't been calculated yet or need refreshing
            foreach (ILight l in lights.Where(l => l.LightArea.Calculated == false))
            {
                if (l.LightState != LightState.On)
                    continue;
                //Render the light area to its own target.
                CalculateLightArea(l);
            }

            //Step 2 - Set up the render targets for the composite lighting.
            RenderImage source = ScreenShadows;
            #if !MACOS
            source.Clear(Color.Black);
            #else
            // For some insane reason, SFML does not clear the texture on MacOS.
            // unless you call CopyToImage() on it, which we can't due to performance.
            // This works though!
            source.BeginDrawing();
            // N̵̤̜͙͔̲͖̓̓͐ͭ́̎̅ͮ̆͠O̸̢̖ͬͣ̋T̓ͩ̊̏͊͏̩̼͞Ẻ̿̇͆͏̬̗̖̮͉͚͈ͅ ̛̗̱͚̬͈ͤͭͯ̄̄F̬ͮ̈ͯṚ͙̭̘̤̹̰̂͗ͯ͑̀̾̽ͪ̐͘Ȍ͎̣͚̍͆̊M̯͚ͮ̉̀̌ ̵̖̠̬͉̟ͫ̓̉͠ͅP͇͖͖̻̳̪͔̅͗ͧ͒͟͜͞J̝͍̻̜̖͖̝̻̓͂͝Ḇ̣̲̫̗͉̥̯̓ͥ͂̔̃ͅ
            // ̶̯͚̯̱͓̣̻͍̄ͪͬ͆̓͆̈͂̉͠Į͖͕͇̜̟̘͌͊͐ͅ ̡̜̮̟̭͙̋͒͐ͯ̚͝ͅḪ̫̥̗̥̯̱̿̋͐̓̄ͫ̚͢ͅE͎͉͕̙̼̼̙̩ͨ̏͆ͪ̂̿́́R̳͍̰͕̲͐͒̊̿̀͑̅̒͢͡E͉̫̺̮͚͔̻̠̒̂ͫ͂B̶͓̗̝̈̋͊ͯ̄̉ͣ͆Y̎ͫ͑ͧ͏͖̦̝̰̝̙̠̹ ̩̹͇̜̝̈́̇̒ͪ̉̐͋ͮD̵̘͒ͫ̈͂̉́͘Ě̤͑ͭͧ̿͋͝Ċ͎̬̫͔̩̐̄̓̅̂́ͣL̵͖̳̯̈͆͐̓́̎̎͒́A̶̷̹̪̻͉͚̽̅͒ͨ͢R̛͕̠̟͙̼͍̻̪͚͑̊ͤͥ̏E̷̷̢͇̮̋́̈́̌ͨ ̧̩̤̆́̀̈́̕T̯͖̝̪ͨͤͬ̽͟͡H̘̟͊͞I̵͙̞̯̙͓̯̊ͯͥ̅͐͗̂͆̆S̴̃̔͐ͫͥͣ̑͆̔̕͏̞̣̥̩̜̟̪͎ ̡̱͙̜ͮͧͣ̏ͪ͢͝C̢̧͍̫̙̣̯̘͚͒̒ͧ͗O̼̹͚͉͙̦̻̽̑̇͊͌ͩͧ͒͞͝D͛̍̚̚҉̟͚̯̮̺̜E̵̮̠͎̞ͤ̓̒̎ͨ̈́̌͞ ̘͇͍̞̼̮̲̹̌̊̄͞À̛͉̹̘͜S̴͙̯̝̙͎̘͙͎̻̎ͥ̍ͬͤ̅̕ ̡̘̯͊ͫͦ̉Cͯ̆͊ͣ́͏̙̝̬U̻͈͚͓̞̞̮͖ͬ͂̽͂̋̎͆̌R̡̘̗̙͍ͯ̿̃̀S̶̠̤̅̆E̒̈̓͘͏̨̙̫̗̮͎͕͉̪D̈́͏͚͔̦̖͎̖̕
            // ̢͓̌T̼̗̰͒ͪ͢͟͠Õ̢̻̙̼̰̜͇͇̮̲̅̔̄͂͠U̜̝͕͆̃͂̇̚C̨̜̟̗͓̪̹̔̿̄̓̏͜Hͭ͌ͪͩ͏̡͡ͅ ̳͇̏̊̎̋͊ͥ̄͒A̴̷̻͖͈̟͖̦̖ͯ͛ͮͨ̊ͨ̐̔ͅͅT̷̼̠̓ͩ́͂ͥͯ̚͟ͅ ̢̞͔̓ͫ̾̓̕Y̶ͩͦͥ̿̍ͧͩ͏̶͚̜͙̥͕̩͖̲O̷̭̺̫͍̞̭͇̪ͦU̙̲̖̠̭̹͕̥̥͌̎ͯͦ̐R̨̬̠̠̹̺͑̄͌ͬ͋̽ͦ͞ ̸̨̼̟̗̻̮̻̣̩͂̾̐ͬͮͦ͛ͬ͡O̶̡̤̫̲̼͚͎̝͚ͣ͌̇W̛̜͚͔̫̹̱̠͐̀ͦ̉͢͝Ń̯̞̩̰̬̞̓ͤ͐ͣ͟ ̴̀̍͊͐͗̌̅͊ͪ҉̯̭̻̼̰P̣̯͚͕̬͙ͩͦ̆͂͑ͮ̈́Ȩ̱̠͊̂̀R̵̪͍̗̰̟͚͕͙͔ͧ̍ͦ̃̾̾ͬ́͟I̷̢̤̼̿̽̂͊̆̿̓L̥̭̏ͯ̍ͣ͝
            //͎̆̒̿ͤ̀͝͝H̙͇̽ͩ̓̚E̜̘̭̟͓͖̓̔̑̀͞͠ͅL̪̰̺̼̊̐̌P̸̴̴̙̻̻̗̯̤͎͓̿̊͌ͪ ̯̜͊̍̄M̩̻̺̬̗͕̬̈͗́ͯ̚̚͜Ȇ̟̜͙̙ ̅͐͐҉̱̫̼̱h̢̼͎͕̪͉͂̊ͤͣ͛͂̄ͯ͝͠t͎̺̼͙̰͓ͥ̏́ţ͕̼̱̲͈̹̾ͣͯͮ̄̅ͧͦ̚p̧̜̹͚̦ͧ̊̀̽ͫ̓̓ͣ̚͡ş̨̮̣̼̰̞̝̫͋̌ͬ͊͑ͣ:̷͇͚̲̻̩̞ͤ͐͞/̈́͋ͯ͂̀̅ͪ͑͞͏͖̮̯͍̟͚͓͎/̟̩̲͑̚ĩ̶̢̲̬̦͍͈̯͉̓̅͟.̦̭̲̭̂̓̿̈́̄͟ï͋͘҉̘̪̠̣̰m̖͎̮͆̀ͯ̑̃ͅg̢̝͉͔̽̃̀̂u̢̱̞̫̱̹̪̇͟r̸̯̞̹͓̥̮̮̝̹͌̀͌̈́͑.̪̦͕̞̥͕̩̎ͤ̇̉̒̓c̨̩̰̎̂ͬͤ̍̓̓ṍ̵͍͈̣̰m̛̱̥̘͙͈ͫͭ̒ͪͮ/̓͆̽̀͐̿͘҉̘̲͈̬̹̟M̡̺͍̜̺̘̰̼͂̎̃͞͝l̴̫̘̦̺̑ͪ̃͢ͅn̤̱̺̿͌ͨ͡U̧̢̜̞̝̒͒̐̄̊̽ͤͫͅL̡̺͉̠͖͚͉͚ͥͧ͋ͬ̀b̵̶̪̝̟̔ͪ̂̊A̧̧̝̭͖̭͍̬͑̀.̞̬͈́ͫ̍͘ͅp̶͎̠̱̍ͪ̆n̩͕̬̈ͪ̋ͅg̘̗̙̻͎̩̲͙͊ͨͭͣ͌̚̕
            CluwneLib.drawRectangle(0, 0, source.Width, source.Height, Color.Black);
            source.EndDrawing();
            #endif

            RenderImage desto = ShadowIntermediate;
            RenderImage copy = null;

            Lightmap.setAsCurrentShader();

            var lightTextures = new List<Texture>();
            var colors = new List<Vector4>();
            var positions = new List<Vector4>();

            //Step 3 - Blend all the lights!
            foreach (ILight Light in lights)
            {
                //Skip off or broken lights (TODO code broken light states)
                if (Light.LightState != LightState.On)
                    continue;

                // LIGHT BLEND STAGE 1 - SIZING -- copys the light texture to a full screen rendertarget
                var area = (LightArea)Light.LightArea;

                //Set the drawing position.
                Vector2 blitPos = CluwneLib.WorldToScreen(area.LightPosition) - area.LightAreaSize * 0.5f;

                //Set shader parameters
                var LightPositionData = new Vector4(blitPos.X / ScreenShadows.Width,
                                                    blitPos.Y / ScreenShadows.Height,
                                                    (float)ScreenShadows.Width / area.RenderTarget.Width,
                                                    (float)ScreenShadows.Height / area.RenderTarget.Height);
                lightTextures.Add(area.RenderTarget.Texture);
                colors.Add(Light.ColorVec);
                positions.Add(LightPositionData);
            }
            int i = 0;
            int num_lights = 6;
            bool draw = false;
            bool fill = false;
            Texture black = IoCManager.Resolve<IResourceCache>().GetSprite("black5x5").Texture;
            var r_img = new Texture[num_lights];
            var r_col = new Vector4[num_lights];
            var r_pos = new Vector4[num_lights];
            do
            {
                if (fill)
                {
                    for (int j = i; j < num_lights; j++)
                    {
                        r_img[j] = black;
                        r_col[j] = Vector4.Zero;
                        r_pos[j] = new Vector4(0, 0, 1, 1);
                    }
                    i = num_lights;
                    draw = true;
                    fill = false;
                }
                if (draw)
                {
                    desto.BeginDrawing();

                    Lightmap.SetUniformArray("LightPosData", r_pos);
                    Lightmap.SetUniformArray("Colors", r_col);
                    Lightmap.SetUniform("light0", r_img[0]);
                    Lightmap.SetUniform("light1", r_img[1]);
                    Lightmap.SetUniform("light2", r_img[2]);
                    Lightmap.SetUniform("light3", r_img[3]);
                    Lightmap.SetUniform("light4", r_img[4]);
                    Lightmap.SetUniform("light5", r_img[5]);
                    Lightmap.SetUniform("sceneTexture", source);

                    // Blit the shadow image on top of the screen
                    source.Blit(0, 0, source.Width, source.Height, BlitterSizeMode.Crop);

                    desto.EndDrawing();

                    //Swap rendertargets to set up for the next light
                    copy = source;
                    source = desto;
                    desto = copy;
                    i = 0;

                    draw = false;
                    fill = false;
                    r_img = new Texture[num_lights];
                    r_col = new Vector4[num_lights];
                    r_pos = new Vector4[num_lights];
                }
                if (lightTextures.Count > 0)
                {
                    r_img[i] = lightTextures[0];
                    lightTextures.RemoveAt(0);

                    r_col[i] = colors[0];
                    colors.RemoveAt(0);

                    r_pos[i] = positions[0];
                    positions.RemoveAt(0);

                    i++;
                }
                if (i == num_lights)
                    //if I is equal to 6 draw
                    draw = true;
                if (i > 0 && i < num_lights && lightTextures.Count == 0)
                    // If all light textures in lightTextures have been processed, fill = true
                    fill = true;
            } while (lightTextures.Count > 0 || draw || fill);

            Lightmap.ResetCurrentShader();

            if (source != ScreenShadows)
            {
                ScreenShadows.BeginDrawing();
                source.Blit(0, 0, source.Width, source.Height);
                ScreenShadows.EndDrawing();
            }
        }

        private void CalculateSceneBatches(Box2 vision)
        {
            if (!_recalculateScene)
                return;

            // Render the player sightline occluder
            RenderPlayerVisionMap();

            //Blur the player vision map
            BlurPlayerVision();

            _decalBatch.BeginDrawing();
            _floorBatch.BeginDrawing();
            _gasBatch.BeginDrawing();

            DrawTiles(vision);

            _floorBatch.EndDrawing();
            _decalBatch.EndDrawing();
            _gasBatch.EndDrawing();

            _recalculateScene = false;
            _redrawTiles = true;
            _redrawOverlay = true;
        }

        private void RenderPlayerVisionMap()
        {
            if (bFullVision)
            {
                PlayerOcclusionTarget.Clear(new Color(211, 211, 211));
                return;
            }
            if (bPlayerVision)
            {
                // I think this should be transparent? Maybe it should be black for the player occlusion...
                // I don't remember. --volundr
                PlayerOcclusionTarget.Clear(Color.Black);
                var playerposition = PlayerManager.ControlledEntity.GetComponent<ITransformComponent>().LocalPosition;
                playerVision.Coordinates = playerposition;

                LightArea area = GetLightArea(RadiusToShadowMapSize(playerVision.Radius));
                area.LightPosition = playerVision.Coordinates.Position; // Set the light position

                TileRef TileReference = playerposition.Grid.GetTile(playerposition);

                if (TileReference.TileDef.IsOpaque)
                {
                    area.LightPosition = new Vector2(area.LightPosition.X, TileReference.Y + playerposition.Grid.TileSize + 1);
                }

                area.BeginDrawingShadowCasters(); // Start drawing to the light rendertarget
                DrawWallsRelativeToLight(area); // Draw all shadowcasting stuff here in black
                area.EndDrawingShadowCasters(); // End drawing to the light rendertarget

                Vector2 blitPos = CluwneLib.WorldToScreen(area.LightPosition) - area.LightAreaSize * 0.5f;
                var tmpBlitPos = CluwneLib.WorldToScreen(area.LightPosition) -
                                 new Vector2(area.RenderTarget.Width, area.RenderTarget.Height) * 0.5f;

                if (debugWallOccluders)
                {
                    OccluderDebugTarget.BeginDrawing();
                    OccluderDebugTarget.Clear(Color.White);
                    area.RenderTarget.Blit((int)tmpBlitPos.X, (int)tmpBlitPos.Y, area.RenderTarget.Width, area.RenderTarget.Height,
                        Color.White, BlitterSizeMode.Crop);
                    OccluderDebugTarget.EndDrawing();
                }

                shadowMapResolver.ResolveShadows(area, false, IoCManager.Resolve<IResourceCache>().GetSprite("whitemask").Texture); // Calc shadows

                if (debugPlayerShadowMap)
                {
                    OccluderDebugTarget.BeginDrawing();
                    OccluderDebugTarget.Clear(Color.White);
                    area.RenderTarget.Blit((int)tmpBlitPos.X, (int)tmpBlitPos.Y, area.RenderTarget.Width, area.RenderTarget.Height, Color.White, BlitterSizeMode.Crop);
                    OccluderDebugTarget.EndDrawing();
                }

                PlayerOcclusionTarget.BeginDrawing(); // Set to shadow rendertarget

                area.RenderTarget.BlendSettings.ColorSrcFactor = BlendMode.Factor.One;
                area.RenderTarget.BlendSettings.ColorDstFactor = BlendMode.Factor.Zero;

                area.RenderTarget.Blit((int)blitPos.X, (int)blitPos.Y, area.RenderTarget.Width, area.RenderTarget.Height, Color.White, BlitterSizeMode.Crop);

                area.RenderTarget.BlendSettings.ColorDstFactor = BlendMode.Factor.SrcAlpha;
                area.RenderTarget.BlendSettings.ColorDstFactor = BlendMode.Factor.OneMinusSrcAlpha;

                PlayerOcclusionTarget.EndDrawing();
            }
            else
            {
                PlayerOcclusionTarget.Clear(Color.Black);
            }
        }

        // Draws all walls in the area around the light relative to it, and in black (test code, not pretty)
        private void DrawWallsRelativeToLight(ILightArea area)
        {
            Vector2 lightAreaSize = CluwneLib.PixelToTile(area.LightAreaSize) / 2;
            var lightArea = Box2.FromDimensions(area.LightPosition - lightAreaSize, CluwneLib.PixelToTile(area.LightAreaSize));

            var entitymanager = IoCManager.Resolve<IClientEntityManager>();

            foreach (IEntity t in entitymanager.GetEntitiesIntersecting(lightArea))
            {
                if (!t.TryGetComponent<OccluderComponent>(out var occluder))
                {
                    continue;
                }
                var transform = t.GetComponent<ITransformComponent>().WorldPosition;
                Vector2 pos = area.ToRelativePosition(CluwneLib.WorldToScreen(transform));
                MapRenderer.RenderPos(occluder.BoundingBox, pos.X, pos.Y);
            }
        }

        private void BlurPlayerVision()
        {
            _gaussianBlur.SetRadius(11);
            _gaussianBlur.SetAmount(2);
            _gaussianBlur.SetSize(new Vector2(PlayerOcclusionTarget.Width, PlayerOcclusionTarget.Height));
            _gaussianBlur.PerformGaussianBlur(PlayerOcclusionTarget);
        }

        /// <summary>
        /// Copys all tile sprites into batches.
        /// </summary>
        private void DrawTiles(Box2 vision)
        {
            var position = PlayerManager.ControlledEntity.GetComponent<ITransformComponent>().LocalPosition;
            var grids = position.Map.FindGridsIntersecting(vision); //Collect all grids in vision range

            //Draw the default grid as the background which will be drawn over
            var background = position.Map.GetDefaultGrid().GetTilesIntersecting(vision, false);
            MapRenderer.DrawTiles(background, _floorBatch, _gasBatch);

            foreach (var grid in grids)
            {
                //We've already drawn the default grid
                if (grid.Index == SS14.Shared.Map.MapManager.DEFAULTGRID)
                    continue;

                //Collects all tiles from grids in vision, gathering empty tiles only from the default grid
                var gridtiles = grid.GetTilesIntersecting(vision);
                MapRenderer.DrawTiles(gridtiles, _floorBatch, _gasBatch);
            }
        }

        /// <summary>
        /// Render the renderables
        /// </summary>
        /// <param name="frametime">time since the last frame was rendered.</param>
        private void RenderComponents(float frameTime, Box2 viewPort, int argMapLevel)
        {
            IEnumerable<IComponent> components = _componentManager.GetComponents<ISpriteRenderableComponent>()
                                          .Cast<IComponent>()
                                          .Union(_componentManager.GetComponents<ParticleSystemComponent>());

            IEnumerable<IRenderableComponent> floorRenderables = from IRenderableComponent c in components
                                                                 orderby c.Bottom ascending, c.DrawDepth ascending
                                                                 where c.DrawDepth < DrawDepth.MobBase &&
                                                                       c.MapID == argMapLevel
                                                                 select c;

            RenderList(new Vector2(viewPort.Left, viewPort.Top), new Vector2(viewPort.Right, viewPort.Bottom),
                       floorRenderables);

            IEnumerable<IRenderableComponent> largeRenderables = from IRenderableComponent c in components
                                                                 orderby c.Bottom ascending
                                                                 where c.DrawDepth >= DrawDepth.MobBase &&
                                                                       c.DrawDepth < DrawDepth.WallTops &&
                                                                       c.MapID == argMapLevel
                                                                 select c;

            RenderList(new Vector2(viewPort.Left, viewPort.Top), new Vector2(viewPort.Right, viewPort.Bottom),
                       largeRenderables);

            IEnumerable<IRenderableComponent> ceilingRenderables = from IRenderableComponent c in components
                                                                   orderby c.Bottom ascending, c.DrawDepth ascending
                                                                   where c.DrawDepth >= DrawDepth.WallTops &&
                                                                         c.MapID == argMapLevel
                                                                   select c;

            RenderList(new Vector2(viewPort.Left, viewPort.Top), new Vector2(viewPort.Right, viewPort.Bottom),
                       ceilingRenderables);
        }

        private void LightScene()
        {
            //Blur the light/shadow map
            BlurShadowMap();

            //Render the scene and lights together to compose the lit scene

            ComposedSceneTarget.BeginDrawing();
            ComposedSceneTarget.Clear(Color.Black);
            LightblendTechnique["FinalLightBlend"].setAsCurrentShader();
            Sprite outofview = IoCManager.Resolve<IResourceCache>().GetSprite("outofview");
            float texratiox = (float)CluwneLib.Window.Viewport.Width / outofview.Texture.Size.X;
            float texratioy = (float)CluwneLib.Window.Viewport.Height / outofview.Texture.Size.Y;
            var maskProps = new Vector4(texratiox, texratioy, 0, 0);

            LightblendTechnique["FinalLightBlend"].SetUniform("PlayerViewTexture", PlayerOcclusionTarget);
            LightblendTechnique["FinalLightBlend"].SetUniform("OutOfViewTexture", outofview.Texture);
            LightblendTechnique["FinalLightBlend"].SetUniform("MaskProps", maskProps);
            LightblendTechnique["FinalLightBlend"].SetUniform("LightTexture", ScreenShadows);
            LightblendTechnique["FinalLightBlend"].SetUniform("SceneTexture", SceneTarget);
            LightblendTechnique["FinalLightBlend"].SetUniform("AmbientLight", new Vector4(.05f, .05f, 0.05f, 1));

            // Blit the shadow image on top of the screen
            ScreenShadows.Blit(0, 0, ScreenShadows.Width, ScreenShadows.Height, Color.White, BlitterSizeMode.Crop);

            LightblendTechnique["FinalLightBlend"].ResetCurrentShader();
            ComposedSceneTarget.EndDrawing();

            PlayerOcclusionTarget.ResetCurrentRenderTarget(); // set the rendertarget back to screen
            PlayerOcclusionTarget.Blit(0, 0, ScreenShadows.Width, ScreenShadows.Height, Color.White, BlitterSizeMode.Crop); //draw playervision again
            PlayerPostProcess();

            //redraw composed scene
            ComposedSceneTarget.Blit(0, 0, (uint)CluwneLib.Window.Viewport.Size.X, (uint)CluwneLib.Window.Viewport.Size.Y, Color.White, BlitterSizeMode.Crop);
        }

        private void BlurShadowMap()
        {
            _gaussianBlur.SetRadius(11);
            _gaussianBlur.SetAmount(2);
            _gaussianBlur.SetSize(new Vector2(ScreenShadows.Width, ScreenShadows.Height));
            _gaussianBlur.PerformGaussianBlur(ScreenShadows);
        }

        private void PlayerPostProcess()
        {
            PlayerManager.ApplyEffects(ComposedSceneTarget);
        }

        #endregion Lighting in order of call

        #region Helper methods

        private void RenderList(Vector2 topleft, Vector2 bottomright, IEnumerable<IRenderableComponent> renderables)
        {
            foreach (IRenderableComponent component in renderables)
            {
                if (component is SpriteComponent)
                {
                    //Slaved components are drawn by their master
                    var c = component as SpriteComponent;
                    if (c.IsSlaved())
                        continue;
                }
                component.Render(topleft, bottomright);
            }
        }

        private void CalculateLightArea(ILight light)
        {
            ILightArea area = light.LightArea;
            if (area.Calculated)
                return;
            area.LightPosition = light.Coordinates.Position; //mousePosWorld; // Set the light position
            TileRef t = light.Coordinates.Grid.GetTile(light.Coordinates);
            if (t.Tile.IsEmpty)
                return;
            if (t.TileDef.IsOpaque)
            {
                area.LightPosition = new Vector2(area.LightPosition.X,
                                                  t.Y +
                                                  light.Coordinates.Grid.TileSize + 1);
            }
            area.BeginDrawingShadowCasters(); // Start drawing to the light rendertarget
            DrawWallsRelativeToLight(area); // Draw all shadowcasting stuff here in black
            area.EndDrawingShadowCasters(); // End drawing to the light rendertarget
            shadowMapResolver.ResolveShadows((LightArea)area, true); // Calc shadows
            area.Calculated = true;
        }

        private ShadowmapSize RadiusToShadowMapSize(int Radius)
        {
            switch (Radius)
            {
                case 128:
                    return ShadowmapSize.Size128;
                case 256:
                    return ShadowmapSize.Size256;
                case 512:
                    return ShadowmapSize.Size512;
                case 1024:
                    return ShadowmapSize.Size1024;
                default:
                    return ShadowmapSize.Size1024;
            }
        }

        private LightArea GetLightArea(ShadowmapSize size)
        {
            switch (size)
            {
                case ShadowmapSize.Size128:
                    return lightArea128;
                case ShadowmapSize.Size256:
                    return lightArea256;
                case ShadowmapSize.Size512:
                    return lightArea512;
                case ShadowmapSize.Size1024:
                    return lightArea1024;
                default:
                    return lightArea1024;
            }
        }

        private void RecalculateScene()
        {
            _recalculateScene = true;
        }

        private void ResetRendertargets()
        {
            foreach (var rt in _cleanupList)
                rt.Dispose();
            foreach (var sp in _cleanupSpriteList)
                sp.Dispose();

            InitializeRenderTargets();
            InitalizeLighting();
        }

        private void ToggleOccluderDebug()
        {
            debugWallOccluders = !debugWallOccluders;
        }

        #endregion Helper methods

        #region Nested type: ClickData

        private struct ClickData
        {
            public readonly IEntity Clicked;
            public readonly int Drawdepth;

            public ClickData(IEntity clicked, int drawdepth)
            {
                Clicked = clicked;
                Drawdepth = drawdepth;
            }
        }

        #endregion Nested type: ClickData

        class GameScreenDebug
        {
            public readonly GameScreen Parent;
            private RectangleShape DebugDisplayBackground;
            private RectangleShape ColliderDebug;
            private TextSprite PositionDebugText;
            private TextSprite FPSText;

            public GameScreenDebug(GameScreen parent)
            {
                Parent = parent;
                DebugDisplayBackground = new RectangleShape()
                {
                    Position = new Vector2(10, 10),
                    Size = new Vector2(180, 180),
                    FillColor = Color.Blue.WithAlpha(64),
                };

                ColliderDebug = new RectangleShape()
                {
                    OutlineThickness = 1f,
                };

                var font = Parent.ResourceCache.GetResource<FontResource>(@"Fonts/bluehigh.ttf").Font;
                PositionDebugText = new TextSprite("", font, 14)
                {
                    Position = new Vector2(15, 15),
                    FillColor = Color.White,
                    Shadowed = true,
                };

                FPSText = new TextSprite("", font, 14)
                {
                    FillColor = Color.White,
                    Shadowed = true,
                };
            }

            public void RenderDebug(Box2 viewport, int argMap)
            {
                if (CluwneLib.Debug.DebugColliders)
                {
                    Color lastColor = default(Color);
                    foreach (var component in Parent._componentManager.GetComponents<CollidableComponent>())
                    {
                        if (component.MapID != argMap)
                        {
                            continue;
                        }
                        var bounds = component.Owner.GetComponent<BoundingBoxComponent>();
                        if (bounds.WorldAABB.IsEmpty() || !bounds.WorldAABB.Intersects(viewport))
                        {
                            continue;
                        }
                        var box = CluwneLib.WorldToScreen(bounds.WorldAABB);
                        ColliderDebug.Position = new Vector2(box.Left, box.Top);
                        ColliderDebug.Size = new Vector2(box.Width, box.Height);
                        if (lastColor != component.DebugColor)
                        {
                            lastColor = component.DebugColor;
                            ColliderDebug.FillColor = lastColor.WithAlpha(64);
                            ColliderDebug.OutlineColor = lastColor.WithAlpha(128);
                        }
                        ColliderDebug.Draw();
                    }
                }
                if (CluwneLib.Debug.DebugGridDisplay)
                {
                    DebugDisplayBackground.Draw();

                    // Player position debug
                    Vector2 playerWorldOffset = Parent.PlayerManager.ControlledEntity.GetComponent<ITransformComponent>().WorldPosition;
                    Vector2 playerTile = CluwneLib.WorldToTile(playerWorldOffset);
                    Vector2 playerScreen = CluwneLib.WorldToScreen(playerWorldOffset);

                    Vector2i mouseScreenPos = (Vector2i)Parent.MousePosScreen.Position;
                    var mousepos = CluwneLib.ScreenToCoordinates(Parent.MousePosScreen);
                    Vector2 mouseWorldOffset = mousepos.ToWorld().Position;
                    Vector2 mouseTile = CluwneLib.WorldToTile(mouseWorldOffset);

                    PositionDebugText.Text = $@"Positioning Debug:
Character Pos:
    Pixel: {playerWorldOffset.X} / {playerWorldOffset.Y}
    World: {playerTile.X} / {playerTile.Y}
    Screen: {playerScreen.X} / {playerScreen.Y}

Mouse Pos:
    Pixel: {mouseWorldOffset.X} / {mouseWorldOffset.Y}
    World: {mouseTile.X} / {mouseTile.Y}
    Screen: {mouseScreenPos.X} / {mouseScreenPos.Y}
    Grid: {mousepos.GridID}
    Map: {mousepos.MapID}";

                    PositionDebugText.Draw();
                }

                if (CluwneLib.Debug.DebugFPS)
                {
                    var fps = Math.Round(IoCManager.Resolve<IGameTiming>().FramesPerSecondAvg, 2);
                    int startY = 10;
                    if (CluwneLib.Debug.DebugGridDisplay)
                    {
                        startY += (int)DebugDisplayBackground.Size.Y;
                    }

                    FPSText.Text = $"FPS: {fps}";
                    FPSText.Position = new Vector2(10, startY);
                    FPSText.Draw();
                }
            }
        }
    }
}
