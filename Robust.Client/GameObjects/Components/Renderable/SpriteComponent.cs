﻿using Robust.Client.Graphics;
using Robust.Client.Graphics.ClientEye;
using Robust.Client.Graphics.Drawing;
using Robust.Client.Graphics.Shaders;
using Robust.Client.Interfaces.GameObjects;
using Robust.Client.Interfaces.GameObjects.Components;
using Robust.Client.Interfaces.ResourceManagement;
using Robust.Client.ResourceManagement;
using Robust.Client.Utility;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Components.Renderable;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Robust.Shared.Animations;
using Robust.Shared.GameObjects.Components.Transform;
using Robust.Shared.Interfaces.Reflection;
using Robust.Shared.ViewVariables;

namespace Robust.Client.GameObjects
{
    public sealed class SpriteComponent : SharedSpriteComponent, ISpriteComponent, IClickTargetComponent,
        IComponentDebug
    {
        private bool _visible = true;

        [ViewVariables(VVAccess.ReadWrite)]
        public bool Visible
        {
            get => _visible;
            set => _visible = value;
        }

        private DrawDepth drawDepth = DrawDepth.Objects;

        /// <summary>
        ///     Z-index for drawing.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        public DrawDepth DrawDepth
        {
            get => drawDepth;
            set => drawDepth = value;
        }

        private Vector2 scale = Vector2.One;

        /// <summary>
        ///     A scale applied to all layers.
        /// </summary>
        [Animatable]
        [ViewVariables(VVAccess.ReadWrite)]
        public Vector2 Scale
        {
            get => scale;
            set => scale = value;
        }

        private Angle rotation;

        [Animatable]
        [ViewVariables(VVAccess.ReadWrite)]
        public Angle Rotation
        {
            get => rotation;
            set => rotation = value;
        }

        private Vector2 offset = Vector2.Zero;

        /// <summary>
        ///     Offset applied to all layers.
        /// </summary>
        [Animatable]
        [ViewVariables(VVAccess.ReadWrite)]
        public Vector2 Offset
        {
            get => offset;
            set => offset = value;
        }

        private Color color = Color.White;

        [Animatable]
        [ViewVariables]
        public Color Color
        {
            get => color;
            set => color = value;
        }

        /// <summary>
        ///     Controls whether we use RSI directions to rotate, or just get angular rotation applied.
        ///     If true, all rotation to this sprite component is negated (that is rotation from say the owner being rotated).
        ///     Rotation transformations on individual layers still apply.
        ///     If false, all layers get locked to south and rotation is a transformation.
        /// </summary>
        [ViewVariables]
        public bool Directional
        {
            get => _directional;
            set => _directional = value;
        }

        private bool _directional = true;

        private RSI _baseRsi;

        [ViewVariables]
        public RSI BaseRSI
        {
            get => _baseRsi;
            set
            {
                _baseRsi = value;
                if (Layers == null)
                {
                    return;
                }

                for (var i = 0; i < Layers.Count; i++)
                {
                    var layer = Layers[i];
                    if (!layer.State.IsValid || layer.RSI != null)
                    {
                        continue;
                    }

                    if (value.TryGetState(layer.State, out var state))
                    {
                        (layer.Texture, layer.AnimationTimeLeft) = state.GetFrame(CorrectLayerDir(ref layer, state), 0);
                    }
                    else
                    {
                        Logger.ErrorS(LogCategory,
                            "Layer '{0}'no longer has state '{1}' due to base RSI change. Trace:\n{2}",
                            i, layer.State, Environment.StackTrace);
                        layer.Texture = null;
                    }
                }
            }
        }

        public ShaderInstance PostShader { get; set; }

        [ViewVariables] private Dictionary<object, int> LayerMap = new Dictionary<object, int>();
        [ViewVariables] private bool _layerMapShared;
        [ViewVariables] private List<Layer> Layers;

#pragma warning disable 649
        [Dependency] private readonly IResourceCache resourceCache;
        [Dependency] private readonly IPrototypeManager prototypes;
        [Dependency] private readonly IReflectionManager reflectionManager;
#pragma warning restore 649

        [ViewVariables(VVAccess.ReadWrite)] RSI.State.Direction LastDir;
        [ViewVariables(VVAccess.ReadWrite)] private bool _recalcDirections = false;

        public uint _renderOrder;
        [ViewVariables(VVAccess.ReadWrite)]
        public uint RenderOrder { get => _renderOrder; set => _renderOrder = value; }

        private static ShaderInstance _defaultShader;

        [ViewVariables]
        private ShaderInstance DefaultShader => _defaultShader ??
                                               (_defaultShader = prototypes
                                                   .Index<ShaderPrototype>("shaded")
                                                   .Instance());

        public const string LogCategory = "go.comp.sprite";
        const string LayerSerializationCache = "spritelayer";
        const string LayerMapSerializationCache = "spritelayermap";

        /// <inheritdoc />
        public void LayerMapSet(object key, int layer)
        {
            if (layer < 0 || layer >= Layers.Count)
            {
                throw new ArgumentOutOfRangeException();
            }

            _layerMapEnsurePrivate();
            LayerMap.Add(key, layer);
        }

        /// <inheritdoc />
        public void LayerMapRemove(object key)
        {
            _layerMapEnsurePrivate();
            LayerMap.Remove(key);
        }

        /// <inheritdoc />
        public int LayerMapGet(object key)
        {
            return LayerMap[key];
        }

        /// <inheritdoc />
        public bool LayerMapTryGet(object key, out int layer)
        {
            return LayerMap.TryGetValue(key, out layer);
        }

        private void _layerMapEnsurePrivate()
        {
            if (!_layerMapShared)
            {
                return;
            }

            LayerMap = LayerMap.ShallowClone();
            _layerMapShared = false;
        }

        public void LayerMapReserveBlank(object key)
        {
            if (LayerMapTryGet(key, out var _))
            {
                return;
            }

            LayerMapSet(key, AddBlankLayer());
        }

        public int AddBlankLayer(int? newIndex = null)
        {
            var layer = new Layer {Visible = false};
            return AddLayer(layer, newIndex);
        }

        public int AddLayer(string texturePath, int? newIndex = null)
        {
            return AddLayer(new ResourcePath(texturePath), newIndex);
        }

        public int AddLayer(ResourcePath texturePath, int? newIndex = null)
        {
            if (!resourceCache.TryGetResource<TextureResource>(TextureRoot / texturePath, out var texture))
            {
                if (texturePath.Extension == "rsi")
                {
                    Logger.ErrorS(LogCategory, "Expected texture but got rsi '{0}', did you mean 'sprite:' instead of 'texture:'?", texturePath);
                }
                Logger.ErrorS(LogCategory, "Unable to load texture '{0}'. Trace:\n{1}", texturePath,
                    Environment.StackTrace);
            }

            return AddLayer(texture, newIndex);
        }

        public int AddLayer(Texture texture, int? newIndex = null)
        {
            var layer = new Layer {Texture = texture};
            return AddLayer(layer, newIndex);
        }

        public int AddLayer(RSI.StateId stateId, int? newIndex = null)
        {
            var layer = new Layer {State = stateId};
            if (BaseRSI.TryGetState(stateId, out var state))
            {
                (layer.Texture, layer.AnimationTimeLeft) = state.GetFrame(CorrectLayerDir(ref layer, state), 0);
            }
            else
            {
                Logger.ErrorS(LogCategory, "State does not exist in RSI: '{0}'. Trace:\n{1}", stateId,
                    Environment.StackTrace);
            }

            return AddLayer(layer, newIndex);
        }

        public int AddLayerState(string stateId, int? newIndex = null)
        {
            return AddLayer(new RSI.StateId(stateId), newIndex);
        }

        public int AddLayer(RSI.StateId stateId, string rsiPath, int? newIndex = null)
        {
            return AddLayer(stateId, new ResourcePath(rsiPath), newIndex);
        }

        public int AddLayerState(string stateId, string rsiPath, int? newIndex = null)
        {
            return AddLayer(new RSI.StateId(stateId), rsiPath, newIndex);
        }

        public int AddLayer(RSI.StateId stateId, ResourcePath rsiPath, int? newIndex = null)
        {
            if (!resourceCache.TryGetResource<RSIResource>(TextureRoot / rsiPath, out var res))
            {
                Logger.ErrorS(LogCategory, "Unable to load RSI '{0}'. Trace:\n{1}", rsiPath, Environment.StackTrace);
            }

            return AddLayer(stateId, res?.RSI);
        }

        public int AddLayerState(string stateId, ResourcePath rsiPath, int? newIndex = null)
        {
            return AddLayer(new RSI.StateId(stateId), rsiPath, newIndex);
        }

        public int AddLayer(RSI.StateId stateId, RSI rsi, int? newIndex = null)
        {
            var layer = new Layer {State = stateId, RSI = rsi};
            if (rsi.TryGetState(stateId, out var state))
            {
                (layer.Texture, layer.AnimationTimeLeft) = state.GetFrame(CorrectLayerDir(ref layer, state), 0);
            }
            else
            {
                Logger.ErrorS(LogCategory, "State does not exist in RSI: '{0}'. Trace:\n{1}", stateId,
                    Environment.StackTrace);
            }

            return AddLayer(layer, newIndex);
        }

        public int AddLayerState(string stateId, RSI rsi, int? newIndex = null)
        {
            return AddLayer(new RSI.StateId(stateId), rsi, newIndex);
        }

        public int AddLayer(SpriteSpecifier specifier, int? newIndex = null)
        {
            switch (specifier)
            {
                case SpriteSpecifier.Texture tex:
                    return AddLayer(tex.TexturePath, newIndex);

                case SpriteSpecifier.Rsi rsi:
                    return AddLayerState(rsi.RsiState, rsi.RsiPath, newIndex);

                default:
                    throw new NotImplementedException();
            }
        }

        private int AddLayer(Layer layer, int? newIndex)
        {
            if (newIndex.HasValue)
            {
                Layers.Insert(newIndex.Value, layer);
                foreach (var kv in LayerMap)
                {
                    if (kv.Value >= newIndex.Value)
                    {
                        LayerMap[kv.Key] = kv.Value + 1;
                    }
                }

                return newIndex.Value;
            }
            else
            {
                Layers.Add(layer);
                return Layers.Count - 1;
            }
        }

        public void RemoveLayer(int layer)
        {
            if (Layers.Count <= layer)
            {
                Logger.ErrorS(LogCategory, "Layer with index '{0}' does not exist, cannot remove! Trace:\n{1}", layer,
                    Environment.StackTrace);
                return;
            }

            Layers.RemoveAt(layer);
            foreach (var kv in LayerMap)
            {
                if (kv.Value == layer)
                {
                    LayerMap.Remove(kv.Key);
                }

                else if (kv.Value > layer)
                {
                    LayerMap[kv.Key] = kv.Value - 1;
                }
            }
        }

        public void RemoveLayer(object layerKey)
        {
            if (!LayerMapTryGet(layerKey, out var layer))
            {
                Logger.ErrorS(LogCategory, "Layer with key '{0}' does not exist, cannot remove! Trace:\n{1}", layerKey,
                    Environment.StackTrace);
                return;
            }

            RemoveLayer(layer);
        }

        public void LayerSetShader(int layer, ShaderInstance shader)
        {
            if (Layers.Count <= layer)
            {
                Logger.ErrorS(LogCategory, "Layer with index '{0}' does not exist, cannot set shader! Trace:\n{1}",
                    layer, Environment.StackTrace);
                return;
            }

            var theLayer = Layers[layer];
            theLayer.Shader = shader;
        }

        public void LayerSetShader(object layerKey, ShaderInstance shader)
        {
            if (!LayerMapTryGet(layerKey, out var layer))
            {
                Logger.ErrorS(LogCategory, "Layer with key '{0}' does not exist, cannot set shader! Trace:\n{1}",
                    layerKey, Environment.StackTrace);
                return;
            }

            LayerSetShader(layer, shader);
        }

        public void LayerSetShader(int layer, string shaderName)
        {
            if (!prototypes.TryIndex<ShaderPrototype>(shaderName, out var prototype))
            {
                Logger.ErrorS(LogCategory, "Shader prototype '{0}' does not exist. Trace:\n{1}", shaderName,
                    Environment.StackTrace);
            }

            // This will set the shader to null if it does not exist.
            LayerSetShader(layer, prototype?.Instance());
        }

        public void LayerSetShader(object layerKey, string shaderName)
        {
            if (!LayerMapTryGet(layerKey, out var layer))
            {
                Logger.ErrorS(LogCategory, "Layer with key '{0}' does not exist, cannot set shader! Trace:\n{1}",
                    layerKey, Environment.StackTrace);
                return;
            }

            LayerSetShader(layer, shaderName);
        }

        public void LayerSetSprite(int layer, SpriteSpecifier specifier)
        {
            if (Layers.Count <= layer)
            {
                Logger.ErrorS(LogCategory, "Layer with index '{0}' does not exist, cannot set sprite! Trace:\n{1}",
                    layer, Environment.StackTrace);
                return;
            }

            switch (specifier)
            {
                case SpriteSpecifier.Texture tex:
                    LayerSetTexture(layer, tex.TexturePath);
                    break;
                case SpriteSpecifier.Rsi rsi:
                    LayerSetState(layer, rsi.RsiState, rsi.RsiPath);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        public void LayerSetSprite(object layerKey, SpriteSpecifier specifier)
        {
            if (!LayerMapTryGet(layerKey, out var layer))
            {
                Logger.ErrorS(LogCategory, "Layer with key '{0}' does not exist, cannot set sprite! Trace:\n{1}",
                    layerKey, Environment.StackTrace);
                return;
            }

            LayerSetSprite(layer, specifier);
        }

        public void LayerSetTexture(int layer, Texture texture)
        {
            if (Layers.Count <= layer)
            {
                Logger.ErrorS(LogCategory, "Layer with index '{0}' does not exist, cannot set texture! Trace:\n{1}",
                    layer, Environment.StackTrace);
                return;
            }

            var theLayer = Layers[layer];
            theLayer.State = null;
            theLayer.Texture = texture;
        }

        public void LayerSetTexture(object layerKey, Texture texture)
        {
            if (!LayerMapTryGet(layerKey, out var layer))
            {
                Logger.ErrorS(LogCategory, "Layer with key '{0}' does not exist, cannot set texture! Trace:\n{1}",
                    layerKey, Environment.StackTrace);
                return;
            }

            LayerSetTexture(layer, texture);
        }

        public void LayerSetTexture(int layer, string texturePath)
        {
            LayerSetTexture(layer, new ResourcePath(texturePath));
        }

        public void LayerSetTexture(object layerKey, string texturePath)
        {
            LayerSetTexture(layerKey, new ResourcePath(texturePath));
        }

        public void LayerSetTexture(int layer, ResourcePath texturePath)
        {
            if (!resourceCache.TryGetResource<TextureResource>(TextureRoot / texturePath, out var texture))
            {
                if (texturePath.Extension == "rsi")
                {
                    Logger.ErrorS(LogCategory, "Expected texture but got rsi '{0}', did you mean 'sprite:' instead of 'texture:'?", texturePath);
                }
                Logger.ErrorS(LogCategory, "Unable to load texture '{0}'. Trace:\n{1}", texturePath,
                    Environment.StackTrace);
            }

            LayerSetTexture(layer, texture);
        }

        public void LayerSetTexture(object layerKey, ResourcePath texturePath)
        {
            if (!LayerMapTryGet(layerKey, out var layer))
            {
                Logger.ErrorS(LogCategory, "Layer with key '{0}' does not exist, cannot set texture! Trace:\n{1}",
                    layerKey, Environment.StackTrace);
                return;
            }

            LayerSetTexture(layer, texturePath);
        }

        public void LayerSetState(int layer, RSI.StateId stateId)
        {
            if (Layers.Count <= layer)
            {
                Logger.ErrorS(LogCategory, "Layer with index '{0}' does not exist, cannot set state! Trace:\n{1}",
                    layer, Environment.StackTrace);
                return;
            }

            var theLayer = Layers[layer];
            if (theLayer.State == stateId)
            {
                return;
            }

            theLayer.State = stateId;
            var rsi = theLayer.RSI ?? BaseRSI;
            if (rsi == null)
            {
                Logger.ErrorS(LogCategory, "No RSI to pull new state from! Trace:\n{0}", Environment.StackTrace);
                theLayer.Texture = null;
            }
            else
            {
                if (rsi.TryGetState(stateId, out var state))
                {
                    theLayer.AnimationFrame = 0;
                    theLayer.AnimationTime = 0;
                    (theLayer.Texture, theLayer.AnimationTimeLeft) =
                        state.GetFrame(CorrectLayerDir(ref theLayer, state), 0);
                }
                else
                {
                    Logger.ErrorS(LogCategory, "State '{0}' does not exist in RSI. Trace:\n{1}", stateId,
                        Environment.StackTrace);
                    theLayer.Texture = null;
                }
            }
        }

        public void LayerSetState(object layerKey, RSI.StateId stateId)
        {
            if (!LayerMapTryGet(layerKey, out var layer))
            {
                Logger.ErrorS(LogCategory, "Layer with key '{0}' does not exist, cannot set state! Trace:\n{1}",
                    layerKey, Environment.StackTrace);
                return;
            }

            LayerSetState(layer, stateId);
        }

        public void LayerSetState(int layer, RSI.StateId stateId, RSI rsi)
        {
            if (Layers.Count <= layer)
            {
                Logger.ErrorS(LogCategory, "Layer with index '{0}' does not exist, cannot set state! Trace:\n{1}",
                    layer, Environment.StackTrace);
                return;
            }

            var theLayer = Layers[layer];
            theLayer.State = stateId;
            theLayer.RSI = rsi;
            var actualRsi = theLayer.RSI ?? BaseRSI;
            if (actualRsi == null)
            {
                Logger.ErrorS(LogCategory, "No RSI to pull new state from! Trace:\n{0}", Environment.StackTrace);
                theLayer.Texture = null;
            }
            else
            {
                if (actualRsi.TryGetState(stateId, out var state))
                {
                    theLayer.AnimationFrame = 0;
                    theLayer.AnimationTime = 0;
                    (theLayer.Texture, theLayer.AnimationTimeLeft) =
                        state.GetFrame(CorrectLayerDir(ref theLayer, state), 0);
                }
                else
                {
                    Logger.ErrorS(LogCategory, "State '{0}' does not exist in RSI. Trace:\n{1}", stateId,
                        Environment.StackTrace);
                    theLayer.Texture = null;
                }
            }
        }

        public void LayerSetState(object layerKey, RSI.StateId stateId, RSI rsi)
        {
            if (!LayerMapTryGet(layerKey, out var layer))
            {
                Logger.ErrorS(LogCategory, "Layer with key '{0}' does not exist, cannot set state! Trace:\n{1}",
                    layerKey, Environment.StackTrace);
                return;
            }

            LayerSetState(layer, stateId, rsi);
        }

        public void LayerSetState(int layer, RSI.StateId stateId, string rsiPath)
        {
            LayerSetState(layer, stateId, new ResourcePath(rsiPath));
        }

        public void LayerSetState(object layerKey, RSI.StateId stateId, string rsiPath)
        {
            LayerSetState(layerKey, stateId, new ResourcePath(rsiPath));
        }

        public void LayerSetState(int layer, RSI.StateId stateId, ResourcePath rsiPath)
        {
            if (!resourceCache.TryGetResource<RSIResource>(TextureRoot / rsiPath, out var res))
            {
                Logger.ErrorS(LogCategory, "Unable to load RSI '{0}'. Trace:\n{1}", rsiPath, Environment.StackTrace);
            }

            LayerSetState(layer, stateId, res?.RSI);
        }

        public void LayerSetState(object layerKey, RSI.StateId stateId, ResourcePath rsiPath)
        {
            if (!LayerMapTryGet(layerKey, out var layer))
            {
                Logger.ErrorS(LogCategory, "Layer with key '{0}' does not exist, cannot set state! Trace:\n{1}",
                    layerKey, Environment.StackTrace);
                return;
            }

            LayerSetState(layer, stateId, rsiPath);
        }

        public void LayerSetRSI(int layer, RSI rsi)
        {
            if (Layers.Count <= layer)
            {
                Logger.ErrorS(LogCategory, "Layer with index '{0}' does not exist, cannot set RSI! Trace:\n{1}", layer,
                    Environment.StackTrace);
                return;
            }

            var theLayer = Layers[layer];
            theLayer.RSI = rsi;
            if (!theLayer.State.IsValid)
            {
                return;
            }

            // Gotta do this because somebody might use null as argument (totally valid).
            var actualRsi = theLayer.RSI ?? BaseRSI;
            if (actualRsi == null)
            {
                Logger.ErrorS(LogCategory, "No RSI to pull new state from! Trace:\n{0}", Environment.StackTrace);
                theLayer.Texture = null;
            }
            else
            {
                if (rsi.TryGetState(theLayer.State, out var state))
                {
                    (theLayer.Texture, theLayer.AnimationTimeLeft) =
                        state.GetFrame(CorrectLayerDir(ref theLayer, state), 0);
                }
                else
                {
                    Logger.ErrorS(LogCategory, "State '{0}' does not exist in set RSI. Trace:\n{1}", theLayer.State,
                        Environment.StackTrace);
                    theLayer.Texture = null;
                }
            }
        }

        public void LayerSetRSI(object layerKey, RSI rsi)
        {
            if (!LayerMapTryGet(layerKey, out var layer))
            {
                Logger.ErrorS(LogCategory, "Layer with key '{0}' does not exist, cannot set RSI! Trace:\n{1}", layerKey,
                    Environment.StackTrace);
                return;
            }

            LayerSetRSI(layer, rsi);
        }

        public void LayerSetRSI(int layer, string rsiPath)
        {
            LayerSetRSI(layer, new ResourcePath(rsiPath));
        }

        public void LayerSetRSI(object layerKey, string rsiPath)
        {
            LayerSetRSI(layerKey, new ResourcePath(rsiPath));
        }

        public void LayerSetRSI(int layer, ResourcePath rsiPath)
        {
            if (!resourceCache.TryGetResource<RSIResource>(TextureRoot / rsiPath, out var res))
            {
                Logger.ErrorS(LogCategory, "Unable to load RSI '{0}'. Trace:\n{1}", rsiPath, Environment.StackTrace);
            }

            LayerSetRSI(layer, res?.RSI);
        }

        public void LayerSetRSI(object layerKey, ResourcePath rsiPath)
        {
            if (!LayerMapTryGet(layerKey, out var layer))
            {
                Logger.ErrorS(LogCategory, "Layer with key '{0}' does not exist, cannot set RSI! Trace:\n{1}", layerKey,
                    Environment.StackTrace);
                return;
            }

            LayerSetRSI(layer, rsiPath);
        }

        public void LayerSetScale(int layer, Vector2 scale)
        {
            if (Layers.Count <= layer)
            {
                Logger.ErrorS(LogCategory, "Layer with index '{0}' does not exist, cannot set scale! Trace:\n{1}",
                    layer, Environment.StackTrace);
                return;
            }

            var theLayer = Layers[layer];
            theLayer.Scale = scale;
        }

        public void LayerSetScale(object layerKey, Vector2 scale)
        {
            if (!LayerMapTryGet(layerKey, out var layer))
            {
                Logger.ErrorS(LogCategory, "Layer with key '{0}' does not exist, cannot set scale! Trace:\n{1}",
                    layerKey, Environment.StackTrace);
                return;
            }

            LayerSetScale(layer, scale);
        }


        public void LayerSetRotation(int layer, Angle rotation)
        {
            if (Layers.Count <= layer)
            {
                Logger.ErrorS(LogCategory, "Layer with index '{0}' does not exist, cannot set rotation! Trace:\n{1}",
                    layer, Environment.StackTrace);
                return;
            }

            var theLayer = Layers[layer];
            theLayer.Rotation = rotation;
        }

        public void LayerSetRotation(object layerKey, Angle rotation)
        {
            if (!LayerMapTryGet(layerKey, out var layer))
            {
                Logger.ErrorS(LogCategory, "Layer with key '{0}' does not exist, cannot set rotation! Trace:\n{1}",
                    layerKey, Environment.StackTrace);
                return;
            }

            LayerSetRotation(layer, rotation);
        }

        public void LayerSetVisible(int layer, bool visible)
        {
            if (Layers.Count <= layer)
            {
                Logger.ErrorS(LogCategory, "Layer with index '{0}' does not exist, cannot set visibility! Trace:\n{1}",
                    layer, Environment.StackTrace);
                return;
            }

            var theLayer = Layers[layer];
            theLayer.Visible = visible;
        }

        public void LayerSetVisible(object layerKey, bool visible)
        {
            if (!LayerMapTryGet(layerKey, out var layer))
            {
                Logger.ErrorS(LogCategory, "Layer with key '{0}' does not exist, cannot set visibility! Trace:\n{1}",
                    layerKey, Environment.StackTrace);
                return;
            }

            LayerSetVisible(layer, visible);
        }

        public void LayerSetColor(int layer, Color color)
        {
            if (Layers.Count <= layer)
            {
                Logger.ErrorS(LogCategory, "Layer with index '{0}' does not exist, cannot set color! Trace:\n{1}",
                    layer, Environment.StackTrace);
                return;
            }

            var theLayer = Layers[layer];
            theLayer.Color = color;
        }

        public void LayerSetColor(object layerKey, Color color)
        {
            if (!LayerMapTryGet(layerKey, out var layer))
            {
                Logger.ErrorS(LogCategory, "Layer with key '{0}' does not exist, cannot set color! Trace:\n{1}",
                    layerKey, Environment.StackTrace);
                return;
            }

            LayerSetColor(layer, color);
        }

        public void LayerSetDirOffset(int layer, DirectionOffset offset)
        {
            if (Layers.Count <= layer)
            {
                Logger.ErrorS(LogCategory, "Layer with index '{0}' does not exist, cannot set dir offset! Trace:\n{1}",
                    layer, Environment.StackTrace);
                return;
            }

            var theLayer = Layers[layer];
            theLayer.DirOffset = offset;
            _recalcDirections = true;
        }

        public void LayerSetDirOffset(object layerKey, DirectionOffset offset)
        {
            if (!LayerMapTryGet(layerKey, out var layer))
            {
                Logger.ErrorS(LogCategory, "Layer with key '{0}' does not exist, cannot set dir offset! Trace:\n{1}",
                    layerKey, Environment.StackTrace);
                return;
            }

            LayerSetDirOffset(layer, offset);
        }

        public void LayerSetAnimationTime(int layer, float animationTime)
        {
            if (Layers.Count <= layer)
            {
                Logger.ErrorS(LogCategory, "Layer with index '{0}' does not exist, cannot set animation time! Trace:\n{1}",
                    layer, Environment.StackTrace);
                return;
            }

            var theLayer = Layers[layer];
            if (!theLayer.State.IsValid)
            {
                return;
            }

            // TODO: This could throw exceptions which shouldn't happen I think.
            var state = (theLayer.RSI ?? BaseRSI)[theLayer.State];
            var correctDir = CorrectLayerDir(ref theLayer, state);
            if (animationTime > theLayer.AnimationTime)
            {
                // Handle advancing differently from going backwards.
                theLayer.AnimationTimeLeft -= (animationTime - theLayer.AnimationTime);
            }
            else
            {
                // Going backwards we re-calculate from zero.
                // Definitely possible to optimize this for going backwards but I'm too lazy to figure that out.
                theLayer.AnimationTimeLeft = -animationTime + state.GetFrame(correctDir, 0).delay;
                theLayer.AnimationFrame = 0;
            }

            theLayer.AnimationTime = animationTime;
            // After setting timing data correctly, run advance to get to the correct frame.
            _advanceFrameAnimation(ref theLayer, state, correctDir);
            // And set to said frame.
            theLayer.Texture = state.GetFrame(correctDir, theLayer.AnimationFrame).icon;
        }

        public void LayerSetAnimationTime(object layerKey, float animationTime)
        {
            if (!LayerMapTryGet(layerKey, out var layer))
            {
                Logger.ErrorS(LogCategory, "Layer with key '{0}' does not exist, cannot set animation time! Trace:\n{1}",
                    layerKey, Environment.StackTrace);
                return;
            }

            LayerSetAnimationTime(layer, animationTime);
        }

        public void LayerSetAutoAnimated(int layer, bool autoAnimated)
        {
            if (Layers.Count <= layer)
            {
                Logger.ErrorS(LogCategory, "Layer with index '{0}' does not exist, cannot set auto animated! Trace:\n{1}",
                    layer, Environment.StackTrace);
                return;
            }

            var theLayer = Layers[layer];
            theLayer.AutoAnimated = autoAnimated;
        }

        public void LayerSetAutoAnimated(object layerKey, bool autoAnimated)
        {
            if (!LayerMapTryGet(layerKey, out var layer))
            {
                Logger.ErrorS(LogCategory, "Layer with key '{0}' does not exist, cannot set auto animated! Trace:\n{1}",
                    layerKey, Environment.StackTrace);
                return;
            }

            LayerSetAutoAnimated(layer, autoAnimated);
        }

        /// <inheritdoc />
        public RSI.StateId LayerGetState(int layer)
        {
            if (Layers.Count <= layer)
            {
                Logger.ErrorS(LogCategory, "Layer with index '{0}' does not exist, cannot get state! Trace:\n{1}",
                    layer, Environment.StackTrace);
                return null;
            }

            var thelayer = Layers[layer];
            return thelayer.State;
        }

        public RSI LayerGetActualRSI(int layer)
        {
            if (Layers.Count <= layer)
            {
                throw new ArgumentOutOfRangeException(nameof(layer), $"Layer '{layer}' does not exist.");
            }

            var theLayer = Layers[layer];
            return BaseRSI ?? theLayer.RSI;
        }

        public RSI LayerGetActualRSI(object layerKey)
        {
            if (!LayerMapTryGet(layerKey, out var layer))
            {
                throw new KeyNotFoundException($"Layer '{layerKey}' does not exist.");
            }

            return LayerGetActualRSI(layer);
        }

        internal void OpenGLRender(DrawingHandleWorld drawingHandle, bool useWorldTransform=true)
        {
            Matrix3 transform;
            if (useWorldTransform)
            {
                var angle = Rotation;
                if (Directional)
                {
                    angle -= Owner.Transform.WorldRotation;
                }
                else
                {
                    angle -= new Angle(MathHelper.PiOver2);
                }

                var mOffset = Matrix3.CreateTranslation(Offset);
                var mRotation = Matrix3.CreateRotation(angle);
                Matrix3.Multiply(ref mRotation, ref mOffset, out transform);

                var worldTransform = ((TransformComponent)Owner.Transform).GetWorldMatrixRounded(1f / EyeManager.PIXELSPERMETER);
                //var worldTransform = Owner.Transform.WorldMatrix;
                transform.Multiply(ref worldTransform);
            }
            else
            {
                transform = Matrix3.Identity;
            }
            drawingHandle.SetTransform(transform);
            foreach (var layer in Layers)
            {
                if (!layer.Visible)
                {
                    continue;
                }

                // TODO: Implement layer-specific rotation and scale.
                var texture = layer.Texture ?? resourceCache.GetFallback<TextureResource>();
                if (layer.Shader != null)
                {
                    drawingHandle.UseShader(layer.Shader);
                }
                drawingHandle.DrawTexture(texture, -(Vector2)texture.Size/(2f*EyeManager.PIXELSPERMETER),
                    color * layer.Color);
                if (layer.Shader != null)
                {
                    drawingHandle.UseShader(null);
                }
            }
        }

        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);

            serializer.DataFieldCached(ref scale, "scale", Vector2.One);
            serializer.DataFieldCached(ref rotation, "rotation", Angle.Zero);
            serializer.DataFieldCached(ref offset, "offset", Vector2.Zero);
            serializer.DataFieldCached(ref drawDepth, "drawdepth", DrawDepth.Objects);
            serializer.DataFieldCached(ref color, "color", Color.White);
            serializer.DataFieldCached(ref _directional, "directional", true);
            serializer.DataFieldCached(ref _visible, "visible", true);

            // TODO: Writing?
            if (!serializer.Reading)
            {
                return;
            }

            {
                var rsi = serializer.ReadDataField<string>("sprite", null);
                if (!string.IsNullOrWhiteSpace(rsi))
                {
                    var rsiPath = TextureRoot / rsi;
                    try
                    {
                        BaseRSI = resourceCache.GetResource<RSIResource>(rsiPath).RSI;
                    }
                    catch (Exception e)
                    {
                        Logger.ErrorS(LogCategory, "Unable to load RSI '{0}'. Trace:\n{1}", rsiPath, e);
                    }
                }
            }

            if (serializer.TryGetCacheData<List<Layer>>(LayerSerializationCache, out var layers))
            {
                LayerMap = serializer.GetCacheData<Dictionary<object, int>>(LayerMapSerializationCache);
                _layerMapShared = true;
                Layers = new List<Layer>(layers.Count);
                foreach (var clone in layers)
                {
                    Layers.Add(new Layer(clone));
                }
                // Do this because the directions in the cache may not be correct for us.
                _recalcDirections = true;
                return;
            }

            layers = new List<Layer>();

            var layerMap = new Dictionary<object, int>();

            var layerData =
                serializer.ReadDataField("layers", new List<PrototypeLayerData>());

            {
                var baseState = serializer.ReadDataField<string>("state", null);
                var texturePath = serializer.ReadDataField<string>("texture", null);

                if (baseState != null || texturePath != null)
                {
                    layerData.Insert(0, new PrototypeLayerData
                    {
                        TexturePath = string.IsNullOrWhiteSpace(texturePath) ? null : texturePath,
                        State = string.IsNullOrWhiteSpace(baseState) ? null : baseState,
                        Color = Color.White,
                        Scale = Vector2.One,
                        Visible = true,
                    });
                }
            }

            foreach (var layerDatum in layerData)
            {
                var anyTextureAttempted = false;
                var layer = new Layer();
                if (!string.IsNullOrWhiteSpace(layerDatum.RsiPath))
                {
                    var path = TextureRoot / layerDatum.RsiPath;
                    try
                    {
                        layer.RSI = resourceCache.GetResource<RSIResource>(path).RSI;
                    }
                    catch
                    {
                        Logger.ErrorS(LogCategory, "Unable to load layer RSI '{0}'.", path);
                    }
                }

                if (!string.IsNullOrWhiteSpace(layerDatum.State))
                {
                    anyTextureAttempted = true;
                    var theRsi = layer.RSI ?? BaseRSI;
                    if (theRsi == null)
                    {
                        Logger.ErrorS(LogCategory,
                            "Layer has no RSI to load states from."
                            + "cannot use 'state' property. Prototype: '{0}'", Owner.Prototype.ID);
                    }
                    else
                    {
                        var stateid = new RSI.StateId(layerDatum.State, RSI.Selectors.None);
                        layer.State = stateid;
                        if (theRsi.TryGetState(stateid, out var state))
                        {
                            // Always use south because this layer will be cached in the serializer.
                            (layer.Texture, layer.AnimationTimeLeft) = state.GetFrame(RSI.State.Direction.South, 0);
                        }
                        else
                        {
                            Logger.ErrorS(LogCategory,
                                "State not found in layer RSI: '{0}'.",
                                stateid);
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(layerDatum.TexturePath))
                {
                    anyTextureAttempted = true;
                    if (layer.State.IsValid)
                    {
                        Logger.ErrorS(LogCategory,
                            "Cannot specify 'texture' on a layer if it has an RSI state specified."
                        );
                    }
                    else
                    {
                        layer.Texture =
                            resourceCache.GetResource<TextureResource>(TextureRoot / layerDatum.TexturePath);
                    }
                }

                if (!string.IsNullOrWhiteSpace(layerDatum.Shader))
                {
                    if (prototypes.TryIndex<ShaderPrototype>(layerDatum.Shader, out var prototype))
                    {
                        layer.Shader = prototype.Instance();
                    }
                    else
                    {
                        Logger.ErrorS(LogCategory,
                            "Shader prototype '{0}' does not exist. Prototype: '{1}'",
                            layerDatum.Shader, Owner.Prototype.ID);
                    }
                }

                layer.Color = layerDatum.Color;
                layer.Rotation = layerDatum.Rotation;
                // If neither state: nor texture: were provided we assume that they want a blank invisible layer.
                layer.Visible = anyTextureAttempted && layerDatum.Visible;
                layer.Scale = layerDatum.Scale;

                layers.Add(layer);

                if (layerDatum.MapKeys != null)
                {
                    var index = layers.Count - 1;
                    foreach (var keyString in layerDatum.MapKeys)
                    {
                        object key;
                        if (reflectionManager.TryParseEnumReference(keyString, out var @enum))
                        {
                            key = @enum;
                        }
                        else
                        {
                            key = keyString;
                        }

                        if (layerMap.ContainsKey(key))
                        {
                            Logger.ErrorS(LogCategory, "Duplicate layer map key definition: {0}", key);
                            continue;
                        }

                        layerMap.Add(key, index);
                    }
                }
            }

            Layers = layers;
            LayerMap = layerMap;
            _layerMapShared = true;
            serializer.SetCacheData(LayerSerializationCache, Layers.ShallowClone());
            serializer.SetCacheData(LayerMapSerializationCache, layerMap);
            // Do this because the directions in the cache may not be correct.
            _recalcDirections = true;
        }

        public void FrameUpdate(float delta)
        {
            // TODO: This entire method is a hotspot of redundant code.
            // This is definitely gonna deserve some optimizations later down the line.

            var dirWeAreFacing = GetDir(RSI.State.DirectionType.Dir8);
            var diagonalDirChanged = false;
            var cardinalDirChanged = false;

            if (LastDir != dirWeAreFacing || _recalcDirections)
            {
                diagonalDirChanged = true;
                cardinalDirChanged = LastDir.RoundToCardinal() != dirWeAreFacing.RoundToCardinal() || _recalcDirections;
                LastDir = dirWeAreFacing;
                _recalcDirections = false;
            }

            for (var i = 0; i < Layers.Count; i++)
            {
                var layer = Layers[i];
                // Since State is a struct, we can't null-check it directly.
                if (!layer.State.IsValid || !layer.Visible)
                {
                    continue;
                }

                var rsi = layer.RSI ?? BaseRSI;
                if (rsi == null)
                {
                    continue;
                }
                var state = rsi[layer.State];
                RSI.State.Direction layerSpecificDir;
                if (state.Directions == RSI.State.DirectionType.Dir1)
                {
                    layerSpecificDir = RSI.State.Direction.South;
                }
                else
                {
                    layerSpecificDir = OffsetRsiDir(GetDir(state.Directions), layer.DirOffset);
                }

                // Is this layer's direction changed?
                // This depends on the direction type of the layer.
                var dirChanged = state.Directions == RSI.State.DirectionType.Dir8
                    ? diagonalDirChanged
                    : cardinalDirChanged;

                layer.AnimationTime += delta;
                if (!dirChanged)
                {
                    var delayCount = state.DelayCount(layerSpecificDir);
                    if (delayCount < 2 || !layer.AutoAnimated)
                    {
                        // Don't bother animating this.
                        // There's no animation frames!
                        continue;
                    }

                    layer.AnimationTimeLeft -= delta;
                }
                else
                {
                    // Mess with animation data so _advanceFrameAnimation fixes the position to where it should be.
                    // So cross-direction animations are synced so long as they have the same total length.
                    layer.AnimationFrame = 0;
                    layer.AnimationTimeLeft = -layer.AnimationTime;
                }

                _advanceFrameAnimation(ref layer, state, layerSpecificDir);
                layer.Texture = state.GetFrame(layerSpecificDir, layer.AnimationFrame).icon;
            }
        }

        private static void _advanceFrameAnimation(ref Layer layer, RSI.State state, RSI.State.Direction layerSpecificDir)
        {
            var delayCount = state.DelayCount(layerSpecificDir);
            while (layer.AnimationTimeLeft < 0)
            {
                if (++layer.AnimationFrame >= delayCount)
                {
                    layer.AnimationFrame = 0;
                    layer.AnimationTime = -layer.AnimationTimeLeft;
                }

                layer.AnimationTimeLeft += state.GetFrame(layerSpecificDir, layer.AnimationFrame).delay;
            }
        }

        public override void HandleComponentState(ComponentState curState, ComponentState nextState)
        {
            if (curState == null)
                return;

            var thestate = (SpriteComponentState) curState;

            Visible = thestate.Visible;
            DrawDepth = thestate.DrawDepth;
            Scale = thestate.Scale;
            Rotation = thestate.Rotation;
            Offset = thestate.Offset;
            Color = thestate.Color;
            Directional = thestate.Directional;
            RenderOrder = thestate.RenderOrder;

            if (thestate.BaseRsiPath != null && BaseRSI != null)
            {
                if (resourceCache.TryGetResource<RSIResource>(TextureRoot / thestate.BaseRsiPath, out var res))
                {
                    if (BaseRSI != res.RSI)
                    {
                        BaseRSI = res.RSI;
                    }
                }
                else
                {
                    Logger.ErrorS(LogCategory, "Hey server, RSI '{0}' doesn't exist.", thestate.BaseRsiPath);
                }
            }

            // Maybe optimize this to NOT full clear.
            Layers.Clear();
            for (var i = 0; i < thestate.Layers.Count; i++)
            {
                var netlayer = thestate.Layers[i];
                var layer = new Layer
                {
                    // These are easy so do them here.
                    Scale = netlayer.Scale,
                    Rotation = netlayer.Rotation,
                    Visible = netlayer.Visible,
                    Color = netlayer.Color
                };
                Layers.Add(layer);

                // Using the public API to handle errors.
                // Probably slow as crap.
                // Who am I kidding, DEFINITELY.
                if (netlayer.Shader != null)
                {
                    LayerSetShader(i, netlayer.Shader);
                }

                if (netlayer.RsiPath != null)
                {
                    LayerSetRSI(i, netlayer.RsiPath);
                }

                if (netlayer.TexturePath != null)
                {
                    LayerSetTexture(i, netlayer.TexturePath);
                }
                else if (netlayer.State != null)
                {
                    LayerSetState(i, netlayer.State);
                }
            }
        }

        private RSI.State.Direction GetDir(RSI.State.DirectionType type)
        {
            if (!Directional)
            {
                return RSI.State.Direction.South;
            }

            var angle = new Angle(Owner.Transform.WorldRotation);
            return angle.GetDir().Convert(type);
        }

        private static RSI.State.Direction OffsetRsiDir(RSI.State.Direction dir, DirectionOffset offset)
        {
            // There is probably a better way to do this.
            // Eh.
            switch (offset)
            {
                case DirectionOffset.None:
                    return dir;
                case DirectionOffset.Clockwise:
                    switch (dir)
                    {
                        case RSI.State.Direction.North:
                            return RSI.State.Direction.East;
                        case RSI.State.Direction.East:
                            return RSI.State.Direction.South;
                        case RSI.State.Direction.South:
                            return RSI.State.Direction.West;
                        case RSI.State.Direction.West:
                            return RSI.State.Direction.North;
                        default:
                            throw new NotImplementedException();
                    }
                case DirectionOffset.CounterClockwise:
                    switch (dir)
                    {
                        case RSI.State.Direction.North:
                            return RSI.State.Direction.West;
                        case RSI.State.Direction.East:
                            return RSI.State.Direction.North;
                        case RSI.State.Direction.South:
                            return RSI.State.Direction.East;
                        case RSI.State.Direction.West:
                            return RSI.State.Direction.South;
                        default:
                            throw new NotImplementedException();
                    }
                case DirectionOffset.Flip:
                    switch (dir)
                    {
                        case RSI.State.Direction.North:
                            return RSI.State.Direction.South;
                        case RSI.State.Direction.East:
                            return RSI.State.Direction.West;
                        case RSI.State.Direction.South:
                            return RSI.State.Direction.North;
                        case RSI.State.Direction.West:
                            return RSI.State.Direction.East;
                        default:
                            throw new NotImplementedException();
                    }
                default:
                    throw new NotImplementedException();
            }
        }

        public string GetDebugString()
        {
            var builder = new StringBuilder();
            builder.AppendFormat(
                "vis/depth/scl/rot/ofs/col/diral/dir: {0}/{1}/{2}/{3}/{4}/{5}/{6}/{7}\n",
                Visible, DrawDepth, Scale, Rotation, Offset,
                Color, Directional, GetDir(RSI.State.DirectionType.Dir8)
            );

            foreach (var layer in Layers)
            {
                builder.AppendFormat(
                    "shad/tex/rsi/state/ant/anf/scl/rot/vis/col/dofs: {0}/{1}/{2}/{3}/{4}/{5}/{6}/{7}/{8}/{9}/{10}\n",
                    // These are references and don't include useful data for knowing where they came from, sadly.
                    // "is one set" is better than nothing at least.
                    layer.Shader != null, layer.Texture != null, layer.RSI != null,
                    layer.State,
                    layer.AnimationTimeLeft, layer.AnimationFrame, layer.Scale, layer.Rotation, layer.Visible,
                    layer.Color, layer.DirOffset
                );
            }

            return builder.ToString();
        }

        RSI.State.Direction CorrectLayerDir(ref Layer layer, RSI.State state)
        {
            if (state.Directions == RSI.State.DirectionType.Dir1)
            {
                return RSI.State.Direction.South;
            }

            return OffsetRsiDir(GetDir(state.Directions), layer.DirOffset);
        }

        /// <summary>
        ///     Enum to "offset" a cardinal direction.
        /// </summary>
        public enum DirectionOffset
        {
            /// <summary>
            ///     No offset.
            /// </summary>
            None = 0,

            /// <summary>
            ///     Rotate direction clockwise. (North -> East, etc...)
            /// </summary>
            Clockwise = 1,

            /// <summary>
            ///     Rotate direction counter-clockwise. (North -> West, etc...)
            /// </summary>
            CounterClockwise = 2,

            /// <summary>
            ///     Rotate direction 180 degrees, so flip. (North -> South, etc...)
            /// </summary>
            Flip = 3,
        }

        private class Layer
        {
            public ShaderInstance Shader;
            public Texture Texture;

            public RSI RSI;
            public RSI.StateId State;
            public float AnimationTimeLeft;
            public float AnimationTime;
            public int AnimationFrame;
            public Vector2 Scale = Vector2.One;
            public Angle Rotation;
            public bool Visible = true;
            public Color Color = Color.White;
            public DirectionOffset DirOffset;
            public bool AutoAnimated = true;

            public Layer()
            {
                Visible = true;
                Scale = Vector2.One;
                Color = Color.White;
                AutoAnimated = true;
            }

            public Layer(Layer toClone)
            {
                Shader = toClone.Shader;
                Texture = toClone.Texture;
                RSI = toClone.RSI;
                State = toClone.State;
                AnimationTimeLeft = toClone.AnimationTimeLeft;
                AnimationTime = toClone.AnimationTime;
                AnimationFrame = toClone.AnimationFrame;
                Scale = toClone.Scale;
                Rotation = toClone.Rotation;
                Visible = toClone.Visible;
                Color = toClone.Color;
                DirOffset = toClone.DirOffset;
                AutoAnimated = toClone.AutoAnimated;
            }
        }

        void IAnimationProperties.SetAnimatableProperty(string name, object value)
        {
            if (!name.StartsWith("layer/"))
            {
                AnimationHelper.SetAnimatableProperty(this, name, value);
                return;
            }

            var delimiter = name.IndexOf("/", 6, StringComparison.Ordinal);
            var indexString = name.Substring(6, delimiter - 6);
            var index = int.Parse(indexString, CultureInfo.InvariantCulture);
            var layerProp = name.Substring(delimiter+1);

            switch (layerProp)
            {
                case "texture":
                    LayerSetTexture(index, (string) value);
                    return;
                case "state":
                    LayerSetState(index, (string) value);
                    return;
                case "color":
                    LayerSetColor(index, (Color) value);
                    return;
                default:
                    throw new ArgumentException($"Unknown layer property '{layerProp}'");
            }
        }
    }
}
