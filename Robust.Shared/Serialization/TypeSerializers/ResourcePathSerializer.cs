using System;
using JetBrains.Annotations;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Serialization.Manager.Result;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Utility;

namespace Robust.Shared.Serialization.TypeSerializers
{
    [TypeSerializer]
    public class ResourcePathSerializer : ITypeSerializer<ResourcePath, ValueDataNode>
    {
        public DeserializationResult Read(ISerializationManager serializationManager, ValueDataNode node,
            bool skipHook,
            ISerializationContext? context = null)
        {
            return new DeserializedValue<ResourcePath>(new ResourcePath(node.Value));
        }

        public ValidationNode Validate(ISerializationManager serializationManager, ValueDataNode node,
            ISerializationContext? context = null)
        {
            var path = node.Value;
            if (path.EndsWith(".rsi"))
            {
                path = $"{path}{ResourcePath.SYSTEM_SEPARATOR}meta.json";
                if (!path.StartsWith(ResourcePath.SYSTEM_SEPARATOR))
                {
                    path = $"{SharedSpriteComponent.TextureRoot}{ResourcePath.SYSTEM_SEPARATOR}{path}";
                }
            }
            try
            {
                return IoCManager.Resolve<IResourceManager>().ContentFileExists(new ResourcePath(path))
                    ? new ValidatedValueNode(node)
                    : new ErrorNode(node, $"File not found. ({path})", true);
            }
            catch (Exception e)
            {
                return new ErrorNode(node, $"Failed parsing filepath. ({path}) ({e.Message})", true);
            }
        }

        public DataNode Write(ISerializationManager serializationManager, ResourcePath value,
            bool alwaysWrite = false,
            ISerializationContext? context = null)
        {
            return new ValueDataNode(value.ToString());
        }

        [MustUseReturnValue]
        public ResourcePath Copy(ISerializationManager serializationManager, ResourcePath source, ResourcePath target,
            bool skipHook,
            ISerializationContext? context = null)
        {
            return new(source.ToString());
        }
    }
}
