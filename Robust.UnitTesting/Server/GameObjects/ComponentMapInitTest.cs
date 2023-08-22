using System.Numerics;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Robust.UnitTesting.Server.GameObjects;

[TestFixture]
public sealed class ComponentMapInitTest
{
    /// <summary>
    /// Asserts whether a component added after an entity has fully initialized has MapInit called.
    /// </summary>
    [Test]
    public void ComponentMapInit()
    {
        var simFactory = RobustServerSimulation.NewSimulation();
        simFactory.RegisterComponents(fac =>
        {
            fac.RegisterClass<MapInitTestComponent>();
        }).RegisterEntitySystems(fac =>
        {
            fac.LoadExtraSystemType<MapInitTestSystem>();
        });

        var sim = simFactory.InitializeInstance();
        var entManager = sim.Resolve<IEntityManager>();
        var mapManager = sim.Resolve<IMapManager>();
        var mapId = mapManager.CreateMap();

        var ent = entManager.SpawnEntity(null, new MapCoordinates(Vector2.Zero, mapId));
        Assert.That(entManager.GetComponent<MetaDataComponent>(ent).EntityLifeStage, Is.EqualTo(EntityLifeStage.MapInitialized));

        var comp = entManager.AddComponent<MapInitTestComponent>(ent);

        Assert.That(comp.Count, Is.EqualTo(1));

        mapManager.DeleteMap(mapId);
    }

    private sealed class MapInitTestSystem : EntitySystem
    {
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<MapInitTestComponent, MapInitEvent>(OnMapInitTestMapInit);
        }

        private void OnMapInitTestMapInit(EntityUid uid, MapInitTestComponent component, MapInitEvent args)
        {
            component.Count += 1;
        }
    }

    private sealed class MapInitTestComponent : Component
    {
        public int Count = 0;
    }
}
