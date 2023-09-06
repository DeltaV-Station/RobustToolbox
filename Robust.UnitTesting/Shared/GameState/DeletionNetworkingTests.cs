using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using NUnit.Framework;
using Robust.Server.Player;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Systems;
using cIPlayerManager = Robust.Client.Player.IPlayerManager;
using sIPlayerManager = Robust.Server.Player.IPlayerManager;

// ReSharper disable AccessToStaticMemberViaDerivedType

namespace Robust.UnitTesting.Shared.GameState;

/// <summary>
/// This test checks that when entities get deleted, the client receives the game states and deletes the entities.
/// </summary>
/// <remarks>
/// Should help prevent the issue fixed in PR #4044 from reoccurring.
/// </remarks>
public sealed class DeletionNetworkingTests : RobustIntegrationTest
{
    [Test]
    public async Task DeletionNetworkingTest()
    {
        var server = StartServer();
        var client = StartClient();

        await Task.WhenAll(client.WaitIdleAsync(), server.WaitIdleAsync());

        var mapMan = server.ResolveDependency<IMapManager>();
        var sEntMan = server.ResolveDependency<IEntityManager>();
        var cEntMan = client.ResolveDependency<IEntityManager>();
        var netMan = client.ResolveDependency<IClientNetManager>();
        var confMan = server.ResolveDependency<IConfigurationManager>();
        var cPlayerMan = client.ResolveDependency<cIPlayerManager>();
        var sPlayerMan = server.ResolveDependency<sIPlayerManager>();
        var xformSys = sEntMan.EntitySysManager.GetEntitySystem<SharedTransformSystem>();

        Assert.DoesNotThrow(() => client.SetConnectTarget(server));
        client.Post(() => netMan.ClientConnect(null!, 0, null!));
        server.Post(() => confMan.SetCVar(CVars.NetPVS, true));

        async Task RunTicks()
        {
            for (int i = 0; i < 10; i++)
            {
                await server.WaitRunTicks(1);
                await client.WaitRunTicks(1);
            }
        }
        await RunTicks();

        // Set up map & grids
        EntityUid grid1 = default;
        EntityUid grid2 = default;
        NetEntity grid1Net = default;
        NetEntity grid2Net = default;

        await server.WaitPost(() =>
        {
            var mapId = mapMan.CreateMap();
            mapMan.GetMapEntityId(mapId);
            var gridComp = mapMan.CreateGrid(mapId);
            gridComp.SetTile(Vector2i.Zero, new Tile(1));
            grid1 = gridComp.Owner;
            xformSys.SetLocalPosition(grid1, new Vector2(-2,0));
            grid1Net = sEntMan.GetNetEntity(grid1);

            gridComp = mapMan.CreateGrid(mapId);
            gridComp.SetTile(Vector2i.Zero, new Tile(1));
            grid2 = gridComp.Owner;
            xformSys.SetLocalPosition(grid2, new Vector2(2,0));
            grid2Net = sEntMan.GetNetEntity(grid2);
        });

        // Spawn player entity on grid 1
        EntityUid player = default;
        await server.WaitPost(() =>
        {
            var coords = new EntityCoordinates(grid1, new Vector2(0.5f, 0.5f));
            player = sEntMan.SpawnEntity(null, coords);
            var session = (IPlayerSession) sPlayerMan.Sessions.First();
            session.AttachToEntity(player);
            session.JoinGame();
        });

        await RunTicks();

        // Check player got properly attached
        await client.WaitPost(() =>
        {
            var ent = cEntMan.GetNetEntity(cPlayerMan.LocalPlayer?.ControlledEntity);
            Assert.That(ent, Is.EqualTo(sEntMan.GetNetEntity(player)));
        });

        // Spawn two entities, each with one child.
        EntityUid entA = default;
        EntityUid entB = default;
        EntityUid childA = default;
        EntityUid childB = default;

        NetEntity entANet = default;
        NetEntity entBNet = default;
        NetEntity childANet = default;
        NetEntity childBNet = default!;

        var coords = new EntityCoordinates(grid2, new Vector2(0.5f, 0.5f));
        await server.WaitPost(() =>
        {
            entA = sEntMan.SpawnEntity(null, coords);
            entB = sEntMan.SpawnEntity(null, coords);
            childA = sEntMan.SpawnEntity(null, new EntityCoordinates(entA, default));
            childB = sEntMan.SpawnEntity(null, new EntityCoordinates(entB, default));

            entANet = sEntMan.GetNetEntity(entA);
            entBNet = sEntMan.GetNetEntity(entB);
            childANet = sEntMan.GetNetEntity(childA);
            childBNet = sEntMan.GetNetEntity(childB);
        });

        await RunTicks();

        // Get the client version of the entities.
        entA = cEntMan.GetEntity(entANet);
        entB = cEntMan.GetEntity(entBNet);
        childA = cEntMan.GetEntity(childANet);
        childB = cEntMan.GetEntity(childBNet);

        // Spawn client-side children and one client-side entity
        EntityUid entC = default;
        EntityUid childC = default;
        EntityUid clientChildA = default;
        EntityUid clientChildB = default;

        NetEntity entCNet = NetEntity.Invalid;

        await client.WaitPost(() =>
        {
            entC = cEntMan.SpawnEntity(null, cEntMan.GetCoordinates(sEntMan.GetNetCoordinates(coords)));
            entCNet = cEntMan.GetNetEntity(entC);
            childC = cEntMan.SpawnEntity(null, new EntityCoordinates(entC, default));
            clientChildA = cEntMan.SpawnEntity(null, new EntityCoordinates(entA, default));
            clientChildB = cEntMan.SpawnEntity(null, new EntityCoordinates(entB, default));
        });

        await RunTicks();

        // Verify entities exist and have the correct parents.
        NetEntity Parent(EntityUid uid) => cEntMan.GetNetEntity(cEntMan.GetComponent<TransformComponent>(uid).ParentUid);
        await client.WaitPost(() =>
        {
            // Exist
            Assert.That(cEntMan.EntityExists(entA));
            Assert.That(cEntMan.EntityExists(entB));
            Assert.That(cEntMan.EntityExists(entC));
            Assert.That(cEntMan.EntityExists(childA));
            Assert.That(cEntMan.EntityExists(childB));
            Assert.That(cEntMan.EntityExists(childC));
            Assert.That(cEntMan.EntityExists(clientChildA));
            Assert.That(cEntMan.EntityExists(clientChildB));

            // Client-side where appropriate
            Assert.That(cEntMan.IsClientSide(entC));
            Assert.That(cEntMan.IsClientSide(childC));
            Assert.That(cEntMan.IsClientSide(clientChildA));
            Assert.That(cEntMan.IsClientSide(clientChildB));
            Assert.That(!cEntMan.IsClientSide(entA));
            Assert.That(!cEntMan.IsClientSide(entB));
            Assert.That(!cEntMan.IsClientSide(childA));
            Assert.That(!cEntMan.IsClientSide(childB));

            // Correct parents.

            Assert.That(Parent(entA), Is.EqualTo(grid2Net));
            Assert.That(Parent(entB), Is.EqualTo(grid2Net));
            Assert.That(Parent(entC), Is.EqualTo(grid2Net));
            Assert.That(Parent(childA), Is.EqualTo(entANet));
            Assert.That(Parent(childB), Is.EqualTo(entBNet));
            Assert.That(Parent(childC), Is.EqualTo(entCNet));
            Assert.That(Parent(clientChildA), Is.EqualTo(entANet));
            Assert.That(Parent(clientChildB), Is.EqualTo(entBNet));
        });

        // Delete client-side entity.
        await client.WaitPost(() => cEntMan.DeleteEntity(entC));
        await RunTicks();
        await client.WaitPost(() =>
        {
            Assert.That(!cEntMan.EntityExists(entC));
            Assert.That(!cEntMan.EntityExists(childC));
        });

        // Delete server-side entity.
        await server.WaitPost(() => sEntMan.DeleteEntity(sEntMan.GetEntity(entBNet)));
        await RunTicks();
        await client.WaitPost(() =>
        {
            Assert.That(!cEntMan.EntityExists(entB));
            Assert.That(!cEntMan.EntityExists(childB));

            // Was never explicitly deleted by the client.
            Assert.That(cEntMan.EntityExists(clientChildB));
        });

        // Delete the grid (and thus also entity A and all the children)
        await server.WaitPost(() => sEntMan.DeleteEntity(grid2));
        await RunTicks();
        await client.WaitPost(() =>
        {
            Assert.That(!cEntMan.EntityExists(cEntMan.GetEntity(sEntMan.GetNetEntity(grid2))));
            Assert.That(!cEntMan.EntityExists(entA));
            Assert.That(!cEntMan.EntityExists(childA));

            // Was never explicitly deleted by the client.
            Assert.That(cEntMan.EntityExists(clientChildA));
        });
    }
}

