using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Robust.Shared.IoC;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using YamlDotNet.RepresentationModel;

namespace Robust.UnitTesting.Shared.Serialization.TypeSerializers.Custom;

[TestFixture]
[TestOf(typeof(TypeDictionarySerializer))]
public sealed class TypeDictionarySerializer : RobustUnitTest
{
    private const string TestYaml = @"
SealedTestTypeA:
  x: 1
  y: 2
SealedTestTypeB:
  x: 3
  z: 4
";
    
    [Test]
    public void SerializationTest()
    {
        var seri = IoCManager.Resolve<ISerializationManager>();
        seri.Initialize();

        var stream = new YamlStream();
        stream.Load(new StringReader(TestYaml));
        var node = (MappingDataNode)stream.Documents[0].RootNode.ToDataNode();

        var validation = seri.ValidateNode<Dictionary<Type, AbstractTestData>, 
            MappingDataNode, 
            TypeDictionarySerializer<AbstractTestData>>
            (node);
        Assert.That(validation.GetErrors().Count(), Is.EqualTo(0));

        var data = seri.Read<Dictionary<Type, AbstractTestData>, 
            MappingDataNode, 
            TypeDictionarySerializer<AbstractTestData>>
            (node, notNullableOverride:true);
        Assert.NotNull(data);
        Assert.That(data.Count, Is.EqualTo(2));
        Assert.That(data.ContainsKey(typeof(SealedTestTypeA)));
        Assert.That(data.ContainsKey(typeof(SealedTestTypeB)));

        var a = data[typeof(SealedTestTypeA)] as SealedTestTypeA;
        var b = data[typeof(SealedTestTypeB)] as SealedTestTypeB;
        
        Assert.NotNull(a);
        Assert.NotNull(b);
        
        Assert.That(a!.X, Is.EqualTo(1));
        Assert.That(a.Y, Is.EqualTo(2));
        Assert.That(b!.X, Is.EqualTo(3));
        Assert.That(b.Z, Is.EqualTo(4));

        var newNode = (MappingDataNode)seri.WriteValue<Dictionary<Type, AbstractTestData>, 
                TypeDictionarySerializer<AbstractTestData>>
            (data, notNullableOverride:true);
        
        Assert.Null(node.Except(newNode));
        validation = seri.ValidateNode<Dictionary<Type, AbstractTestData>, 
            MappingDataNode, 
            TypeDictionarySerializer<AbstractTestData>>
            (newNode);
        Assert.That(validation.GetErrors().Count(), Is.EqualTo(0));
    }
    
    [ImplicitDataDefinitionForInheritors]
    private abstract class AbstractTestData
    {
        [DataField("x")] public int X;
    }

    private sealed class SealedTestTypeA : AbstractTestData
    {
        [DataField("y")] public int Y;
    }

    private sealed class SealedTestTypeB : AbstractTestData
    {
        [DataField("z")] public int Z;
    }
}