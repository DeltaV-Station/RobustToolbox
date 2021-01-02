using System.IO;
using System.Linq;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Robust.UnitTesting.Shared.ComponentData
{
    [TestFixture]
    public class ComponentSerializationTest : RobustUnitTest
    {
        private string prototype = @"
- type: entity
  id: TestEntity
  components:
  - type: TestComp
    foo: 1
    baz: Testing

- type: entity
  id: CustomTestEntity
  components:
  - type: CustomTestComp
    abc: foo

- type: entity
  id: CustomInheritTestEntity
  components:
  - type: CustomTestCompInheritor
    abc: foo
";

        [OneTimeSetUp]
        public void Setup()
        {
            IoCManager.Resolve<IComponentFactory>().Register<SerializationTestComponent>();
            IoCManager.Resolve<IComponentFactory>().Register<TestCustomDataClassComponent>();
            IoCManager.Resolve<IComponentFactory>().Register<TestCustomDataClassInheritorComponent>();
            IoCManager.Resolve<IComponentManager>().Initialize();

            IoCManager.Resolve<IComponentDataManager>().RegisterCustomDataClasses();

            IoCManager.Resolve<IPrototypeManager>().LoadFromStream(new StringReader(prototype));
            IoCManager.Resolve<IPrototypeManager>().Resync();

        }

        [Test]
        public void ParsingTest()
        {
            var data = IoCManager.Resolve<IPrototypeManager>().Index<EntityPrototype>("TestEntity");

            Assert.That(data.Components["TestComp"] is SerializationTestComponent_AUTODATA);
            Assert.That(data.Components["TestComp"].GetValue("foo"), Is.EqualTo(1));
            Assert.That(data.Components["TestComp"].GetValue("bar"), Is.Null);
            Assert.That(data.Components["TestComp"].GetValue("baz"), Is.EqualTo("Testing"));
        }

        [Test]
        public void PopulatingTest()
        {
            var entity = IoCManager.Resolve<IEntityManager>().CreateEntityUninitialized("TestEntity");
            var comp = entity.GetComponent<SerializationTestComponent>();
            Assert.That(comp.Foo, Is.EqualTo(1));
            Assert.That(comp.Bar, Is.EqualTo(-1));
            Assert.That(comp.Baz, Is.EqualTo("Testing"));
        }

        [Test]
        public void CustomDataClassTest()
        {
            var entity = IoCManager.Resolve<IEntityManager>().CreateEntityUninitialized("CustomTestEntity");
            var comp = entity.GetComponent<TestCustomDataClassComponent>();
            Assert.That(comp.Abc, Is.EqualTo("foobar"));
        }

        [Test]
        public void CustomDataClassInheritanceTest()
        {
            var entity = IoCManager.Resolve<IEntityManager>().CreateEntityUninitialized("CustomInheritTestEntity");
            var comp = entity.GetComponent<TestCustomDataClassInheritorComponent>();
            Assert.That(comp.Abc, Is.EqualTo("foobar"));
        }

        private class SerializationTestComponent : Component
        {
            public override string Name => "TestComp";

            [YamlField("foo")]
            public int Foo = -1;

            [YamlField("bar")]
            public int Bar = -1;

            [YamlField("baz")]
            public string Baz = "abc";
        }

        [CustomDataClass(typeof(ACustomDataClassWithARandomName))]
        private class TestCustomDataClassComponent : Component
        {
            public override string Name => "CustomTestComp";

            [CustomYamlTarget("abc")]
            public string Abc = "ERROR";
        }

        private class TestCustomDataClassInheritorComponent : TestCustomDataClassComponent
        {
            public override string Name => "CustomTestCompInheritor";
        }
    }

    public class ACustomDataClassWithARandomName : Component_AUTODATA
    {
        public override string[] Tags => new[] {"abc"};

        public string? Abc;

        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);
            serializer.DataField(ref Abc, "abc", null);
            Abc += "bar";
        }

        public override object? GetValue(string tag)
        {
            return tag == "abc" ? Abc : base.GetValue(tag);
        }

        public override void SetValue(string tag, object? value)
        {
            if (tag == "abc")
            {
                Abc = (string?)value;
            }
            base.SetValue(tag, value);
        }
    }
}
