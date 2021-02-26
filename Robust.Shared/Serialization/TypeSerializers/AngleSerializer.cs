using System.Globalization;
using JetBrains.Annotations;
using Robust.Shared.Maths;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Serialization.Manager.Result;
using Robust.Shared.Serialization.Markdown;

namespace Robust.Shared.Serialization.TypeSerializers
{
    [TypeSerializer]
    public class AngleSerializer : ITypeSerializer<Angle, ValueDataNode>
    {
        public DeserializationResult<Angle> Read(ValueDataNode node, ISerializationContext? context = null)
        {
            var nodeContents = node.Value;

            var angle = nodeContents.EndsWith("rad")
                ? new Angle(double.Parse(nodeContents.Substring(0, nodeContents.Length - 3),
                    CultureInfo.InvariantCulture))
                : Angle.FromDegrees(double.Parse(nodeContents, CultureInfo.InvariantCulture));

            return DeserializationResult.Value(angle);
        }

        public DataNode Write(Angle value, bool alwaysWrite = false,
            ISerializationContext? context = null)
        {
            return new ValueDataNode($"{value.Theta.ToString(CultureInfo.InvariantCulture)} rad");
        }

        [MustUseReturnValue]
        public Angle Copy(Angle source, Angle target)
        {
            return new(source);
        }
    }
}
