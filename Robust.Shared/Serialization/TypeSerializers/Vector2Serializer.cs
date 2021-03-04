using System.Globalization;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Serialization.Manager.Result;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Validation;

namespace Robust.Shared.Serialization.TypeSerializers
{
    [TypeSerializer]
    public class Vector2Serializer : ITypeSerializer<Vector2, ValueDataNode>
    {
        public DeserializationResult Read(ISerializationManager serializationManager, ValueDataNode node,
            IDependencyCollection dependencies,
            bool skipHook,
            ISerializationContext? context = null)
        {
            string raw = node.Value;
            string[] args = raw.Split(',');

            if (args.Length != 2)
            {
                throw new InvalidMappingException($"Could not parse {nameof(Vector2)}: '{raw}'");
            }

            var x = float.Parse(args[0], CultureInfo.InvariantCulture);
            var y = float.Parse(args[1], CultureInfo.InvariantCulture);
            var vector = new Vector2(x, y);

            return new DeserializedValue<Vector2>(vector);
        }

        public ValidationNode Validate(ISerializationManager serializationManager, ValueDataNode node,
            IDependencyCollection dependencies,
            ISerializationContext? context = null)
        {
            string raw = node.Value;
            string[] args = raw.Split(',');

            if (args.Length != 2)
            {
                return new ErrorNode(node, "Invalid amount of arguments for Vector2.");
            }

            return float.TryParse(args[0], NumberStyles.Any, CultureInfo.InvariantCulture, out _) &&
                   float.TryParse(args[1], NumberStyles.Any, CultureInfo.InvariantCulture, out _)
                ? new ValidatedValueNode(node)
                : new ErrorNode(node, "Failed parsing values for Vector2.");
        }

        public DataNode Write(ISerializationManager serializationManager, Vector2 value, bool alwaysWrite = false,
            ISerializationContext? context = null)
        {
            var valueString = $"{value.X.ToString(CultureInfo.InvariantCulture)}," +
                              $"{value.Y.ToString(CultureInfo.InvariantCulture)}";

            return new ValueDataNode(valueString);
        }

        public Vector2 Copy(ISerializationManager serializationManager, Vector2 source, Vector2 target,
            bool skipHook,
            ISerializationContext? context = null)
        {
            return new(source.X, source.Y);
        }
    }
}
