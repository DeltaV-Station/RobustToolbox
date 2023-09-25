using System.Numerics;
using JetBrains.Annotations;
using Robust.Server.GameStates;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Sources;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Players;

namespace Robust.Server.GameObjects;

[UsedImplicitly]
public sealed class AudioSystem : SharedAudioSystem
{
    [Dependency] private readonly PvsOverrideSystem _pvs = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AudioComponent, ComponentStartup>(OnAudioStartup);
    }

    private void OnAudioStartup(EntityUid uid, AudioComponent component, ComponentStartup args)
    {
        component.Source = new DummyAudioSource();
    }

    private void AddAudioFilter(EntityUid uid, Filter filter)
    {
        var nent = GetNetEntity(uid);
        _pvs.AddSessionOverrides(nent, filter);
    }

    private void SetupAudio(AudioComponent component, string fileName, AudioParams? audioParams)
    {
        audioParams ??= AudioParams.Default;
        component.FileName = fileName;
        component.Params = audioParams.Value;
    }

    /// <inheritdoc />
    public override (EntityUid Entity, AudioComponent Component)? PlayGlobal(string filename, Filter playerFilter, bool recordReplay, AudioParams? audioParams = null)
    {
        var entity = Spawn(AudioEntity, MapCoordinates.Nullspace);
        var audio = Comp<AudioComponent>(entity);
        SetupAudio(audio, filename, audioParams);
        AddAudioFilter(entity, playerFilter);

        return (entity, audio);
    }

    public override (EntityUid Entity, AudioComponent Component)? PlayEntity(string filename, Filter playerFilter, EntityUid uid, bool recordReplay, AudioParams? audioParams = null)
    {
        if (!Exists(uid))
            return null;

        var entity = Spawn(AudioEntity, new EntityCoordinates(uid, Vector2.Zero));
        var audio = Comp<AudioComponent>(entity);
        SetupAudio(audio, filename, audioParams);
        AddAudioFilter(entity, playerFilter);

        return (entity, audio);
    }

    /// <inheritdoc />
    public override (EntityUid Entity, AudioComponent Component)? PlayStatic(string filename, Filter playerFilter, EntityCoordinates coordinates, bool recordReplay, AudioParams? audioParams = null)
    {
        if (!coordinates.IsValid(EntityManager))
            return null;

        var entity = Spawn(AudioEntity, coordinates);
        var audio = Comp<AudioComponent>(entity);
        SetupAudio(audio, filename, audioParams);
        AddAudioFilter(entity, playerFilter);

        return (entity, audio);
    }

    /// <inheritdoc />
    public override (EntityUid Entity, AudioComponent Component)? PlayPredicted(SoundSpecifier? sound, EntityUid source, EntityUid? user, AudioParams? audioParams = null)
    {
        if (sound == null)
            return null;

        var filter = Filter.Pvs(source, entityManager: EntityManager, playerManager: PlayerManager, cfgManager: CfgManager).RemoveWhereAttachedEntity(e => e == user);
        return PlayEntity(GetSound(sound), filter, source, true, audioParams ?? sound.Params);
    }

    public override (EntityUid Entity, AudioComponent Component)? PlayPredicted(SoundSpecifier? sound, EntityCoordinates coordinates, EntityUid? user, AudioParams? audioParams = null)
    {
        if (sound == null)
            return null;

        var filter = Filter.Pvs(coordinates, entityMan: EntityManager, playerMan: PlayerManager).RemoveWhereAttachedEntity(e => e == user);
        return PlayStatic(GetSound(sound), filter, coordinates, true, audioParams ?? sound.Params);
    }

    public override (EntityUid Entity, AudioComponent Component)? PlayGlobal(string filename, ICommonSession recipient, AudioParams? audioParams = null)
    {
        return PlayGlobal(filename, Filter.SinglePlayer(recipient), false, audioParams);
    }

    public override (EntityUid Entity, AudioComponent Component)? PlayGlobal(string filename, EntityUid recipient, AudioParams? audioParams = null)
    {
        if (TryComp(recipient, out ActorComponent? actor))
            return PlayGlobal(filename, actor.PlayerSession, audioParams);

        return null;
    }

    public override (EntityUid Entity, AudioComponent Component)? PlayEntity(string filename, ICommonSession recipient, EntityUid uid, AudioParams? audioParams = null)
    {
        return PlayEntity(filename, Filter.SinglePlayer(recipient), uid, false, audioParams);
    }

    public override (EntityUid Entity, AudioComponent Component)? PlayEntity(string filename, EntityUid recipient, EntityUid uid, AudioParams? audioParams = null)
    {
        if (TryComp(recipient, out ActorComponent? actor))
            return PlayEntity(filename, actor.PlayerSession, uid, audioParams);

        return null;
    }

    public override (EntityUid Entity, AudioComponent Component)? PlayStatic(string filename, ICommonSession recipient, EntityCoordinates coordinates, AudioParams? audioParams = null)
    {
        return PlayStatic(filename, Filter.SinglePlayer(recipient), coordinates, false, audioParams);
    }

    public override (EntityUid Entity, AudioComponent Component)? PlayStatic(string filename, EntityUid recipient, EntityCoordinates coordinates, AudioParams? audioParams = null)
    {
        if (TryComp(recipient, out ActorComponent? actor))
            return PlayStatic(filename, actor.PlayerSession, coordinates, audioParams);

        return null;
    }
}
