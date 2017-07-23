﻿using SFML.System;
using SFML.Graphics;
using NUnit.Framework;
using SS14.Shared.IoC;
using SS14.Shared.Prototypes;
using SS14.Shared.GameObjects;
using SS14.Shared.Utility;
using SS14.Shared.Maths;
using System.IO;
using System.Collections.Generic;
using YamlDotNet.RepresentationModel;

namespace SS14.UnitTesting.SS14.Shared.Prototypes
{
    [TestFixture]
    public class PrototypeManager_Test : SS14UnitTest
    {
        private IPrototypeManager manager;
        [OneTimeSetUp]
        public void Setup()
        {
            manager = IoCManager.Resolve<IPrototypeManager>();
            manager.LoadFromStream(new StringReader(DOCUMENT));
            manager.Resync();
        }

        [Test]
        public void TestBasicPrototype()
        {
            var prototype = manager.Index<EntityPrototype>("wrench");
            Assert.That(prototype.Name, Is.EqualTo("Not a wrench. Tricked!"));

            var mapping = prototype.Components["TestBasicPrototypeComponent"];
            Assert.That(mapping.GetNode("foo"), Is.EqualTo(new YamlScalarNode("bar!")));
        }

        [Test, Combinatorial]
        public void TestLightPrototype([Values("wallLight", "wallLightChild")] string id)
        {
            var prototype = manager.Index<EntityPrototype>(id);

            Assert.Multiple(() =>
            {
                Assert.That(prototype.Name, Is.EqualTo("Wall Light"));
                Assert.That(prototype.ID, Is.EqualTo(id));
                Assert.That(prototype.Components, Contains.Key("Transform"));
                Assert.That(prototype.Components, Contains.Key("Velocity"));
                Assert.That(prototype.Components, Contains.Key("Direction"));
                Assert.That(prototype.Components, Contains.Key("Clickable"));
                Assert.That(prototype.Components, Contains.Key("Sprite"));
                Assert.That(prototype.Components, Contains.Key("BasicInteractable"));
                Assert.That(prototype.Components, Contains.Key("BasicMover"));
                Assert.That(prototype.Components, Contains.Key("WallMounted"));
                Assert.That(prototype.Components, Contains.Key("Light"));
            });

            var componentData = prototype.Components["Light"];
            var expected = new YamlMappingNode();
            expected.Children[new YamlScalarNode("startState")] = new YamlScalarNode("Off");

            Assert.That(componentData, Is.EquivalentTo(expected));
        }

        [Test]
        public void TestYamlHelpersPrototype()
        {
            var prototype = manager.Index<EntityPrototype>("yamltester");
            Assert.That(prototype.Components, Contains.Key("TestBasicPrototypeComponent"));

            var componentData = prototype.Components["TestBasicPrototypeComponent"];

            Assert.That(componentData["str"].AsString(), Is.EqualTo("hi!"));
            Assert.That(componentData["int"].AsInt(), Is.EqualTo(10));
            Assert.That(componentData["float"].AsFloat(), Is.EqualTo(10f));
            Assert.That(componentData["float2"].AsFloat(), Is.EqualTo(10.5f));
            Assert.That(componentData["boolt"].AsBool(), Is.EqualTo(true));
            Assert.That(componentData["boolf"].AsBool(), Is.EqualTo(false));
            Assert.That(componentData["vec2"].AsVector2f(), Is.EqualTo(new Vector2f(1.5f, 1.5f)));
            Assert.That(componentData["vec2i"].AsVector2i(), Is.EqualTo(new Vector2i(1, 1)));
            Assert.That(componentData["vec3"].AsVector3f(), Is.EqualTo(new Vector3f(1.5f, 1.5f, 1.5f)));
            Assert.That(componentData["vec4"].AsVector4f(), Is.EqualTo(new Vector4f(1.5f, 1.5f, 1.5f, 1.5f)));
            Assert.That(componentData["color"].AsHexColor(), Is.EqualTo(new Color(0xAA, 0xBB, 0xCC)));
            Assert.That(componentData["enumf"].AsEnum<YamlTestEnum>(), Is.EqualTo(YamlTestEnum.Foo));
            Assert.That(componentData["enumb"].AsEnum<YamlTestEnum>(), Is.EqualTo(YamlTestEnum.Bar));
        }

        [Test]
        public void TestPlacementProperties()
        {
            var prototype = manager.Index<EntityPrototype>("mounttester");

            Assert.That(prototype.MountingPoints, Is.EquivalentTo(new int[] {1, 2, 3}));
            Assert.That(prototype.PlacementMode, Is.EqualTo("AlignWall"));
            Assert.That(prototype.PlacementRange, Is.EqualTo(300));
            Assert.That(prototype.PlacementOffset, Is.EqualTo(new Vector2i(30, 45)));
        }

        private enum YamlTestEnum
        {
            Foo,
            Bar
        }

        const string DOCUMENT = @"
- type: entity
  id: wrench
  name: Not a wrench. Tricked!
  components:
  - type: TestBasicPrototypeComponent
    foo: bar!

- type: entity
  id: wallLight
  name: Wall Light
  components:
  - type: Transform
  - type: Velocity
  - type: Direction
  - type: Clickable
  - type: Sprite
  - type: BasicInteractable
  - type: BasicMover
  - type: WallMounted
  - type: Light
    startState: Off

- type: entity
  id: wallLightChild
  parent: wallLight

- type: entity
  id: yamltester
  components:
  - type: TestBasicPrototypeComponent
    str: hi!
    int: 10
    float: 10
    float2: 10.5
    boolt: true
    boolf: false
    vec2: 1.5, 1.5
    vec2i: 1, 1
    vec3: 1.5, 1.5, 1.5
    vec4: 1.5, 1.5, 1.5, 1.5
    color: '#aabbcc'
    enumf: Foo
    enumb: Bar

- type: entity
  id: mounttester
  placement:
    mode: AlignWall
    range: 300
    offset: 30, 45
    nodes:
    - 1
    - 2
    - 3
";
    }

    public class TestBasicPrototypeComponent : Component
    {
        public override string Name => "TestBasicPrototypeComponent";
    }
}

