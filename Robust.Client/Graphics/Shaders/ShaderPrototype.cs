﻿using Robust.Client.Interfaces.ResourceManagement;
using Robust.Client.ResourceManagement;
using Robust.Client.ResourceManagement.ResourceTypes;
using Robust.Client.Utility;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using System;
using System.Collections.Generic;
using Robust.Client.Interfaces.Graphics;
using YamlDotNet.RepresentationModel;
using BlendModeEnum = Godot.CanvasItemMaterial.BlendModeEnum;
using LightModeEnum = Godot.CanvasItemMaterial.LightModeEnum;

namespace Robust.Client.Graphics.Shaders
{
    [Prototype("shader")]
    public sealed class ShaderPrototype : IPrototype, IIndexedPrototype
    {
        public string ID { get; private set; }

        private ShaderKind Kind;

        // Source shader variables.
        private ShaderSourceResource Source;
        private Dictionary<string, object> ShaderParams;

        // Canvas shader variables.
        private LightModeEnum LightMode;
        private BlendModeEnum BlendMode;

        private Shader _canvasKindInstance;

        /// <summary>
        ///     Creates a new instance of this shader.
        /// </summary>
        public Shader Instance()
        {
            switch (GameController.Mode)
            {
                case GameController.DisplayMode.Headless:
                    return new Shader();
                case GameController.DisplayMode.Godot:
                    return _instanceGodot();
                case GameController.DisplayMode.Clyde:
                    return _instanceOpenGL();
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private Shader _instanceOpenGL()
        {
            switch (Kind)
            {
                case ShaderKind.Source:
                    return new Shader(Source.ClydeHandle);
                case ShaderKind.Canvas:
                    if (_canvasKindInstance != null)
                    {
                        return _canvasKindInstance;
                    }

                    string source;
                    if (LightMode == LightModeEnum.Unshaded)
                    {
                        source = SourceUnshaded;
                    }
                    else
                    {
                        source = SourceShaded;
                    }

                    var parsed = ShaderParser.Parse(source);
                    var clyde = IoCManager.Resolve<IClyde>();
                    var instance = new Shader(clyde.LoadShader(parsed));
                    _canvasKindInstance = instance;
                    return instance;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private Shader _instanceGodot()
        {
            Godot.Material mat;

            switch (Kind)
            {
                case ShaderKind.Source:
                    var shaderMat = new Godot.ShaderMaterial
                    {
                        Shader = Source.GodotShader
                    };
                    mat = shaderMat;
                    if (ShaderParams != null)
                    {
                        foreach (var pair in ShaderParams)
                        {
                            shaderMat.SetShaderParam(pair.Key, pair.Value);
                        }
                    }

                    break;
                case ShaderKind.Canvas:
                    mat = new Godot.CanvasItemMaterial
                    {
                        LightMode = LightMode,
                        BlendMode = BlendMode,
                    };
                    break;
                default:
                    throw new InvalidOperationException();
            }

            return new Shader(mat);
        }

        public void LoadFrom(YamlMappingNode mapping)
        {
            ID = mapping.GetNode("id").ToString();

            var kind = mapping.GetNode("kind").AsString();
            switch (kind)
            {
                case "source":
                    Kind = ShaderKind.Source;
                    ReadSourceKind(mapping);
                    break;

                case "canvas":
                    Kind = ShaderKind.Canvas;
                    ReadCanvasKind(mapping);
                    break;

                default:
                    throw new InvalidOperationException($"Invalid shader kind: '{kind}'");
            }
        }

        private void ReadSourceKind(YamlMappingNode mapping)
        {
            var path = mapping.GetNode("path").AsResourcePath();
            var resc = IoCManager.Resolve<IResourceCache>();
            Source = resc.GetResource<ShaderSourceResource>(path);
            if (mapping.TryGetNode<YamlMappingNode>("params", out var paramMapping))
            {
                ShaderParams = new Dictionary<string, object>();
                foreach (var item in paramMapping)
                {
                    var name = item.Key.AsString();
                    // TODO: This.
                    if (true)
                    //if (!Source.TryGetShaderParamType(name, out var type))
                    {
                        Logger.ErrorS("shader", "Shader param '{0}' does not exist on shader '{1}'", name, path);
                        continue;
                    }

                    //var value = ParseShaderParamFor(item.Value, type);
                    //ShaderParams.Add(name, value);
                }
            }
        }

        private void ReadCanvasKind(YamlMappingNode mapping)
        {
            if (mapping.TryGetNode("light_mode", out var node))
            {
                switch (node.AsString())
                {
                    case "normal":
                        LightMode = LightModeEnum.Normal;
                        break;

                    case "unshaded":
                        LightMode = LightModeEnum.Unshaded;
                        break;

                    case "light_only":
                        LightMode = LightModeEnum.LightOnly;
                        break;

                    default:
                        throw new InvalidOperationException($"Invalid light mode: '{node.AsString()}'");
                }
            }

            if (mapping.TryGetNode("blend_mode", out node))
            {
                switch (node.AsString())
                {
                    case "mix":
                        BlendMode = BlendModeEnum.Mix;
                        break;

                    case "add":
                        BlendMode = BlendModeEnum.Add;
                        break;

                    case "subtract":
                        BlendMode = BlendModeEnum.Sub;
                        break;

                    case "multiply":
                        BlendMode = BlendModeEnum.Mul;
                        break;

                    case "premultiplied_alpha":
                        BlendMode = BlendModeEnum.PremultAlpha;
                        break;

                    default:
                        throw new InvalidOperationException($"Invalid blend mode: '{node.AsString()}'");
                }
            }
        }

        private object ParseShaderParamFor(YamlNode node, ShaderParamType type)
        {
            if (!GameController.OnGodot)
            {
                throw new NotImplementedException();
            }
            switch (type)
            {
                case ShaderParamType.Void:
                    throw new NotSupportedException();
                case ShaderParamType.Bool:
                    return node.AsBool();
                case ShaderParamType.Int:
                case ShaderParamType.UInt:
                    return node.AsInt();
                case ShaderParamType.Float:
                    return node.AsFloat();
                case ShaderParamType.Vec2:
                    return node.AsVector2().Convert();
                case ShaderParamType.Vec3:
                    return node.AsVector3().Convert();
                case ShaderParamType.Vec4:
                    try
                    {
                        return node.AsColor().Convert();
                    }
                    catch
                    {
                        var vec4 = node.AsVector4();
                        return new Godot.Quat(vec4.X, vec4.Y, vec4.Z, vec4.W);
                    }

                case ShaderParamType.Sampler2D:
                    var path = node.AsResourcePath();
                    var resc = IoCManager.Resolve<IResourceCache>();
                    return resc.GetResource<TextureResource>(path).Texture.GodotTexture;

                // If something's not handled here, then that's probably because I was lazy.
                default:
                    throw new NotImplementedException();
            }
        }

        enum ShaderKind
        {
            Source,
            Canvas
        }

        private const string SourceUnshaded = @"
render_mode unshaded;
void fragment() {
    COLOR = texture(TEXTURE, UV);
}
";

        private const string SourceShaded = @"
void fragment() {
    COLOR = texture(TEXTURE, UV);
}
";
    }
}
