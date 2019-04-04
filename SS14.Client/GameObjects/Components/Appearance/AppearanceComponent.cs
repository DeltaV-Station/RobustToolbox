﻿using System;
using System.Collections.Generic;
using SS14.Shared.GameObjects;
using SS14.Shared.GameObjects.Components.Appearance;
using SS14.Shared.Interfaces.GameObjects;
using SS14.Shared.Interfaces.Reflection;
using SS14.Shared.IoC;
using SS14.Shared.Serialization;
using SS14.Shared.Utility;
using YamlDotNet.RepresentationModel;

namespace SS14.Client.GameObjects
{
    public sealed class AppearanceComponent : SharedAppearanceComponent
    {
        private Dictionary<object, object> data = new Dictionary<object, object>();
        internal List<AppearanceVisualizer> Visualizers = new List<AppearanceVisualizer>();

        public bool AppearanceDirty { get; internal set; } = false;

        static AppearanceComponent()
        {
            YamlObjectSerializer.RegisterTypeSerializer(typeof(AppearanceVisualizer), new VisualizerTypeSerializer());
        }

        public override void SetData(string key, object value)
        {
            SetData(key, value);
        }

        public override void SetData(Enum key, object value)
        {
            SetData(key, value);
        }

        public override T GetData<T>(string key)
        {
            return (T)data[key];
        }

        public override T GetData<T>(Enum key)
        {
            return (T)data[key];
        }

        internal T GetData<T>(object key)
        {
            return (T)data[key];
        }

        public override bool TryGetData<T>(Enum key, out T data)
        {
            return TryGetData(key, out data);
        }

        public override bool TryGetData<T>(string key, out T data)
        {
            return TryGetData(key, out data);
        }

        internal bool TryGetData<T>(object key, out T data)
        {
            if (this.data.TryGetValue(key, out var dat))
            {
                data = (T)dat;
                return true;
            }

            data = default;
            return false;
        }

        private void SetData(object key, object value)
        {
            data[key] = value;

            AppearanceDirty = true;
        }

        public override void HandleComponentState(ComponentState curState, ComponentState nextState)
        {
            var actualState = (AppearanceComponentState)curState;
            data = actualState.Data;
            AppearanceDirty = true;
        }

        public override void ExposeData(ObjectSerializer serializer)
        {
            serializer.DataField(ref Visualizers, "visuals", new List<AppearanceVisualizer>());
        }

        public override void Initialize()
        {
            base.Initialize();

            foreach (var visual in Visualizers)
            {
                visual.InitializeEntity(Owner);
            }
        }

        class VisualizerTypeSerializer : YamlObjectSerializer.TypeSerializer
        {
            public override object NodeToType(Type type, YamlNode node, YamlObjectSerializer serializer)
            {
                var refl = IoCManager.Resolve<IReflectionManager>();
                var mapping = (YamlMappingNode)node;
                var nodeType = mapping.GetNode("type");
                switch (nodeType.AsString())
                {
                    case SpriteLayerToggle.NAME:
                        var keyString = mapping.GetNode("key").AsString();
                        object key;
                        if (refl.TryParseEnumReference(keyString, out var @enum))
                        {
                            key = @enum;
                        }
                        else
                        {
                            key = keyString;
                        }
                        var layer = mapping.GetNode("layer").AsInt();
                        return new SpriteLayerToggle(key, layer);

                    default:
                        var visType = refl.LooseGetType(nodeType.AsString());
                        if (!typeof(AppearanceVisualizer).IsAssignableFrom(visType))
                        {
                            throw new InvalidOperationException();
                        }
                        var vis = (AppearanceVisualizer)Activator.CreateInstance(visType);
                        vis.LoadData(mapping);
                        return vis;
                }
            }

            public override YamlNode TypeToNode(object obj, YamlObjectSerializer serializer)
            {
                switch (obj)
                {
                    case SpriteLayerToggle spriteLayerToggle:
                        YamlScalarNode key;
                        if (spriteLayerToggle.Key is Enum)
                        {
                            var name = spriteLayerToggle.Key.GetType().FullName;
                            key = new YamlScalarNode($"{name}.{spriteLayerToggle.Key}");
                        }
                        else
                        {
                            key = new YamlScalarNode(spriteLayerToggle.Key.ToString());
                        }
                        return new YamlMappingNode
                        {
                            { new YamlScalarNode("type"), new YamlScalarNode(SpriteLayerToggle.NAME) },
                            { new YamlScalarNode("key"), key },
                            { new YamlScalarNode("layer"), new YamlScalarNode(spriteLayerToggle.SpriteLayer.ToString()) },
                        };
                    default:
                        // TODO: A proper way to do serialization here.
                        // I can't use the ExposeData system here since that's specific to entity serializers.
                        return new YamlMappingNode();
                }
            }
        }

        internal class SpriteLayerToggle : AppearanceVisualizer
        {
            public const string NAME = "sprite_layer_toggle";

            public readonly object Key;
            public readonly int SpriteLayer;

            public SpriteLayerToggle(object key, int spriteLayer)
            {
                Key = key;
                SpriteLayer = spriteLayer;
            }
        }
    }

    /// <summary>
    ///     Handles the visualization of data inside of an appearance component.
    ///     Implementations of this class are NOT bound to a specific entity, they are flyweighted across multiple.
    /// </summary>
    public abstract class AppearanceVisualizer
    {
        /// <summary>
        ///     Load data from the prototype declaring this visualizer, to configure settings and such.
        /// </summary>
        public virtual void LoadData(YamlMappingNode node)
        {
        }

        /// <summary>
        ///     Initializes an entity to be managed by this appearance controller.
        ///     DO NOT assume this is your only entity. Visualizers are shared.
        /// </summary>
        public virtual void InitializeEntity(IEntity entity)
        {
        }

        /// <summary>
        ///     Called whenever appearance data for an entity changes.
        ///     Update its visuals here.
        /// </summary>
        /// <param name="component">The appearance component of the entity that might need updating.</param>
        public virtual void OnChangeData(AppearanceComponent component)
        {
        }
    }

    sealed class AppearanceTestComponent : Component
    {
        public override string Name => "AppearanceTest";

        float time;
        bool state;

        public void OnUpdate(float frameTime)
        {
            time += frameTime;
            if (time > 1)
            {
                time -= 1;
                Owner.GetComponent<AppearanceComponent>().SetData("test", state = !state);
            }
        }
    }
}
