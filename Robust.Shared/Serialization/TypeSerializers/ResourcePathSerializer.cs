using JetBrains.Annotations;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Serialization.Manager.Result;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Utility;

namespace Robust.Shared.Serialization.TypeSerializers
{
    [TypeSerializer]
    public class ResourcePathSerializer : ITypeSerializer<ResourcePath, ValueDataNode>
    {
        public DeserializationResult<ResourcePath> Read(ValueDataNode node, ISerializationContext? context = null)
        {
            return new DeserializedValue<ResourcePath>(new ResourcePath(node.Value));
        }

        public DataNode Write(ResourcePath value,
            bool alwaysWrite = false,
            ISerializationContext? context = null)
        {
            return new ValueDataNode(value.ToString());
        }

        [MustUseReturnValue]
        public ResourcePath Copy(ResourcePath source, ResourcePath target)
        {
            return new(source.ToString());
        }
    }
}
