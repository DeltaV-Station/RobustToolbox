using System;
using JetBrains.Annotations;
using Robust.Shared.IoC;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Serialization.Manager.Result;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Utility;
using static Robust.Shared.Utility.SpriteSpecifier;

namespace Robust.Shared.Serialization.TypeSerializers
{
    [TypeSerializer]
    public class SpriteSpecifierSerializer :
        ITypeReaderWriter<SpriteSpecifier, ValueDataNode>,
        ITypeReaderWriter<SpriteSpecifier, MappingDataNode>,
        ITypeCopier<Rsi>,
        ITypeCopier<Texture>,
        ITypeCopier<EntityPrototype>
    {
        [Dependency] private readonly ISerializationManager _serializationManager = default!;

        public DeserializationResult<SpriteSpecifier> Read(ValueDataNode node, ISerializationContext? context = null)
        {
            var path = _serializationManager.ReadValueOrThrow<ResourcePath>(node, context);
            var texture = new Texture(path);

            return new DeserializedValue<SpriteSpecifier>(texture);
        }

        public DeserializationResult<SpriteSpecifier> Read(MappingDataNode node, ISerializationContext? context = null)
        {
            if (node.TryGetNode("sprite", out var spriteNode)
                && node.TryGetNode("state", out var rawStateNode)
                && rawStateNode is ValueDataNode stateNode)
            {
                var path = _serializationManager.ReadValueOrThrow<ResourcePath>(spriteNode, context);
                var rsi = new Rsi(path, stateNode.Value);

                return new DeserializedValue<SpriteSpecifier>(rsi);
            }

            throw new InvalidNodeTypeException();
        }

        public DataNode Write(SpriteSpecifier value,
            bool alwaysWrite = false,
            ISerializationContext? context = null)
        {
            switch (value)
            {
                case Texture tex:
                    return _serializationManager.WriteValue(tex.TexturePath, alwaysWrite, context);
                case Rsi rsi:
                    var mapping = new MappingDataNode();
                    mapping.AddNode("sprite", _serializationManager.WriteValue(rsi.RsiPath, alwaysWrite, context));
                    mapping.AddNode("state", new ValueDataNode(rsi.RsiState));
                    return mapping;
            }

            throw new NotImplementedException();
        }

        public Rsi Copy(Rsi source, Rsi target)
        {
            return new(source.RsiPath, source.RsiState);
        }

        public Texture Copy(Texture source, Texture target)
        {
            return new(source.TexturePath);
        }

        [MustUseReturnValue]
        public EntityPrototype Copy(EntityPrototype source, EntityPrototype target)
        {
            return new(source.EntityPrototypeId);
        }
    }
}
