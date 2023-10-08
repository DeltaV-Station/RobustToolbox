using System;
using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Players;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.ResourceManagement.ResourceTypes;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Robust.Shared.Audio.Systems;

/// <summary>
/// Handles audio for robust toolbox inside of the sim.
/// </summary>
/// <remarks>
/// Interacts with AudioManager internally.
/// </remarks>
public abstract partial class SharedAudioSystem : EntitySystem
{
    [Dependency] protected readonly IConfigurationManager CfgManager = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] private   readonly INetManager _netManager = default!;
    [Dependency] protected readonly IPrototypeManager ProtoMan = default!;
    [Dependency] private   readonly IResourceCache _resource = default!;
    [Dependency] protected readonly IRobustRandom RandMan = default!;
    [Dependency] protected readonly ISharedPlayerManager PlayerManager = default!;

    /// <summary>
    /// Default max range at which the sound can be heard.
    /// </summary>
    public const float DefaultSoundRange = 20;

    /// <summary>
    /// Used in the PAS to designate the physics collision mask of occluders.
    /// </summary>
    public int OcclusionCollisionMask { get; set; }

    public float ZOffset;

    public override void Initialize()
    {
        base.Initialize();
        InitializeEffect();
        ZOffset = CfgManager.GetCVar(CVars.AudioZOffset);
        CfgManager.OnValueChanged(CVars.AudioZOffset, SetZOffset);
        SubscribeLocalEvent<Components.AudioComponent, ComponentGetStateAttemptEvent>(OnAudioGetStateAttempt);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CfgManager.UnsubValueChanged(CVars.AudioZOffset, SetZOffset);
    }

    protected virtual void SetZOffset(float value)
    {
        var query = AllEntityQuery<Components.AudioComponent>();
        var oldZOffset = ZOffset;
        ZOffset = value;

        while (query.MoveNext(out var uid, out var audio))
        {
            // Pythagoras back to normal then adjust.
            var maxDistance = MathF.Pow(audio.Params.MaxDistance, 2) - oldZOffset;
            var refDistance = MathF.Pow(audio.Params.ReferenceDistance, 2) - oldZOffset;

            audio.Params.MaxDistance = maxDistance;
            audio.Params.ReferenceDistance = refDistance;
            audio.Params = GetAdjustedParams(audio.Params);
            Dirty(uid, audio);
        }
    }

    private void OnAudioGetStateAttempt(EntityUid uid, Components.AudioComponent component, ref ComponentGetStateAttemptEvent args)
    {
        if (component.ExcludedEntity != null && args.Player?.AttachedEntity == component.ExcludedEntity)
            args.Cancelled = true;
    }

    /// <summary>
    /// Considers Z-offset for audio and gets the adjusted distance.
    /// </summary>
    /// <remarks>
    /// Really it's just doing pythagoras for you.
    /// </remarks>
    public float GetAudioDistance(float length)
    {
        return MathF.Sqrt(MathF.Pow(length, 2) + MathF.Pow(ZOffset, 2));
    }

    /// <summary>
    /// Resolves the filepath to a sound file.
    /// </summary>
    public string GetSound(SoundSpecifier specifier)
    {
        switch (specifier)
        {
            case SoundPathSpecifier path:
                return path.Path == default ? string.Empty : path.Path.ToString();

            case SoundCollectionSpecifier collection:
            {
                if (collection.Collection == null)
                    return string.Empty;

                var soundCollection = ProtoMan.Index<SoundCollectionPrototype>(collection.Collection);
                return RandMan.Pick(soundCollection.PickFiles).ToString();
            }
        }

        return string.Empty;
    }

    #region AudioParams

    protected Components.AudioComponent SetupAudio(EntityUid uid, string fileName, AudioParams? audioParams)
    {
        DebugTools.Assert(!string.IsNullOrEmpty(fileName));
        audioParams ??= AudioParams.Default;
        var comp = AddComp<Components.AudioComponent>(uid);
        comp.FileName = fileName;
        comp.Params = GetAdjustedParams(audioParams.Value);

        if (!audioParams.Value.Loop)
        {
            var length = GetAudioLength(fileName);

            var despawn = AddComp<TimedDespawnComponent>(uid);
            // Don't want to clip audio too short due to imprecision.
            despawn.Lifetime = (float) length.TotalSeconds + 0.01f;
        }

        return comp;
    }

    /// <summary>
    /// Accounts for ZOffset on audio distance.
    /// </summary>
    private AudioParams GetAdjustedParams(AudioParams audioParams)
    {
        var maxDistance = GetAudioDistance(audioParams.MaxDistance);
        var refDistance = GetAudioDistance(audioParams.ReferenceDistance);

        return audioParams
            .WithMaxDistance(maxDistance)
            .WithReferenceDistance(refDistance);
    }

    /// <summary>
    /// Sets the audio params volume for an entity.
    /// </summary>
    public void SetVolume(EntityUid? entity, float value, Components.AudioComponent? component = null)
    {
        if (entity == null || !Resolve(entity.Value, ref component))
            return;

        if (component.Params.Volume.Equals(value))
            return;

        component.Params.Volume = value;
        Dirty(entity.Value, component);
    }

    #endregion

    /// <summary>
    /// Gets the timespan of the specified audio.
    /// </summary>
    public TimeSpan GetAudioLength(string filename)
    {
        var resource = _resource.GetResource<AudioResource>(filename);
        return resource.AudioStream.Length;
    }

    /// <summary>
    /// Stops the specified audio entity from playing.
    /// </summary>
    /// <remarks>
    /// Returns null so you can inline the call.
    /// </remarks>
    public EntityUid? Stop(EntityUid? uid, Components.AudioComponent? component = null)
    {
        // One morbillion warnings for logging missing.
        if (uid == null || !Resolve(uid.Value, ref component, false))
            return null;

        if (!Timing.IsFirstTimePredicted || (_netManager.IsClient && !IsClientSide(uid.Value)))
            return null;

        QueueDel(uid);
        return null;
    }

    /// <summary>
    /// Play an audio file globally, without position.
    /// </summary>
    /// <param name="filename">The resource path to the OGG Vorbis file to play.</param>
    /// <param name="playerFilter">The set of players that will hear the sound.</param>
    [return: NotNullIfNotNull("filename")]
    public abstract (EntityUid Entity, Components.AudioComponent Component)? PlayGlobal(string filename, Filter playerFilter, bool recordReplay, AudioParams? audioParams = null);

    /// <summary>
    /// Play an audio file globally, without position.
    /// </summary>
    /// <param name="sound">The sound specifier that points the audio file(s) that should be played.</param>
    /// <param name="playerFilter">The set of players that will hear the sound.</param>
    [return: NotNullIfNotNull("sound")]
    public (EntityUid Entity, Components.AudioComponent Component)? PlayGlobal(SoundSpecifier? sound, Filter playerFilter, bool recordReplay, AudioParams? audioParams = null)
    {
        return sound == null ? null : PlayGlobal(GetSound(sound), playerFilter, recordReplay, sound.Params);
    }

    /// <summary>
    /// Play an audio file globally, without position.
    /// </summary>
    /// <param name="filename">The resource path to the OGG Vorbis file to play.</param>
    /// <param name="recipient">The player that will hear the sound.</param>
    [return: NotNullIfNotNull("filename")]
    public abstract (EntityUid Entity, Components.AudioComponent Component)? PlayGlobal(string filename, ICommonSession recipient, AudioParams? audioParams = null);

    /// <summary>
    /// Play an audio file globally, without position.
    /// </summary>
    /// <param name="sound">The sound specifier that points the audio file(s) that should be played.</param>
    /// <param name="recipient">The player that will hear the sound.</param>
    [return: NotNullIfNotNull("sound")]
    public (EntityUid Entity, Components.AudioComponent Component)? PlayGlobal(SoundSpecifier? sound, ICommonSession recipient)
    {
        return sound == null ? null : PlayGlobal(GetSound(sound), recipient, sound.Params);
    }

    /// <summary>
    /// Play an audio file globally, without position.
    /// </summary>
    /// <param name="filename">The resource path to the OGG Vorbis file to play.</param>
    /// <param name="recipient">The player that will hear the sound.</param>
    [return: NotNullIfNotNull("filename")]
    public abstract (EntityUid Entity, Components.AudioComponent Component)? PlayGlobal(string filename, EntityUid recipient, AudioParams? audioParams = null);

    /// <summary>
    /// Play an audio file globally, without position.
    /// </summary>
    /// <param name="sound">The sound specifier that points the audio file(s) that should be played.</param>
    /// <param name="recipient">The player that will hear the sound.</param>
    [return: NotNullIfNotNull("sound")]
    public (EntityUid Entity, Components.AudioComponent Component)? PlayGlobal(SoundSpecifier? sound, EntityUid recipient, AudioParams? audioParams = null)
    {
        return sound == null ? null : PlayGlobal(GetSound(sound), recipient, sound.Params);
    }

    /// <summary>
    /// Play an audio file following an entity.
    /// </summary>
    /// <param name="filename">The resource path to the OGG Vorbis file to play.</param>
    /// <param name="playerFilter">The set of players that will hear the sound.</param>
    /// <param name="uid">The UID of the entity "emitting" the audio.</param>
    [return: NotNullIfNotNull("filename")]
    public abstract (EntityUid Entity, Components.AudioComponent Component)? PlayEntity(string filename, Filter playerFilter, EntityUid uid, bool recordReplay, AudioParams? audioParams = null);

    /// <summary>
    /// Play an audio file following an entity.
    /// </summary>
    /// <param name="filename">The resource path to the OGG Vorbis file to play.</param>
    /// <param name="recipient">The player that will hear the sound.</param>
    /// <param name="uid">The UID of the entity "emitting" the audio.</param>
    [return: NotNullIfNotNull("filename")]
    public abstract (EntityUid Entity, Components.AudioComponent Component)? PlayEntity(string filename, ICommonSession recipient, EntityUid uid, AudioParams? audioParams = null);

    /// <summary>
    /// Play an audio file following an entity.
    /// </summary>
    /// <param name="filename">The resource path to the OGG Vorbis file to play.</param>
    /// <param name="recipient">The player that will hear the sound.</param>
    /// <param name="uid">The UID of the entity "emitting" the audio.</param>
    [return: NotNullIfNotNull("filename")]
    public abstract (EntityUid Entity, Components.AudioComponent Component)? PlayEntity(string filename, EntityUid recipient, EntityUid uid, AudioParams? audioParams = null);

    /// <summary>
    /// Play an audio file following an entity.
    /// </summary>
    /// <param name="sound">The sound specifier that points the audio file(s) that should be played.</param>
    /// <param name="playerFilter">The set of players that will hear the sound.</param>
    /// <param name="uid">The UID of the entity "emitting" the audio.</param>
    [return: NotNullIfNotNull("sound")]
    public (EntityUid Entity, Components.AudioComponent Component)? PlayEntity(SoundSpecifier? sound, Filter playerFilter, EntityUid uid, bool recordReplay, AudioParams? audioParams = null)
    {
        return sound == null ? null : PlayEntity(GetSound(sound), playerFilter, uid, recordReplay, audioParams ?? sound.Params);
    }

    /// <summary>
    /// Play an audio file following an entity.
    /// </summary>
    /// <param name="sound">The sound specifier that points the audio file(s) that should be played.</param>
    /// <param name="recipient">The player that will hear the sound.</param>
    /// <param name="uid">The UID of the entity "emitting" the audio.</param>
    [return: NotNullIfNotNull("sound")]
    public (EntityUid Entity, Components.AudioComponent Component)? PlayEntity(SoundSpecifier? sound, ICommonSession recipient, EntityUid uid, AudioParams? audioParams = null)
    {
        return sound == null ? null : PlayEntity(GetSound(sound), recipient, uid, audioParams ?? sound.Params);
    }

    /// <summary>
    /// Play an audio file following an entity.
    /// </summary>
    /// <param name="sound">The sound specifier that points the audio file(s) that should be played.</param>
    /// <param name="recipient">The player that will hear the sound.</param>
    /// <param name="uid">The UID of the entity "emitting" the audio.</param>
    [return: NotNullIfNotNull("sound")]
    public (EntityUid Entity, Components.AudioComponent Component)? PlayEntity(SoundSpecifier? sound, EntityUid recipient, EntityUid uid, AudioParams? audioParams = null)
    {
        return sound == null ? null : PlayEntity(GetSound(sound), recipient, uid, audioParams ?? sound.Params);
    }

    /// <summary>
    /// Play an audio file following an entity for every entity in PVS range.
    /// </summary>
    /// <param name="sound">The sound specifier that points the audio file(s) that should be played.</param>
    /// <param name="uid">The UID of the entity "emitting" the audio.</param>
    [return: NotNullIfNotNull("sound")]
    public (EntityUid Entity, Components.AudioComponent Component)? PlayPvs(SoundSpecifier? sound, EntityUid uid, AudioParams? audioParams = null)
    {
        return sound == null ? null : PlayPvs(GetSound(sound), uid, audioParams ?? sound.Params);
    }

    /// <summary>
    /// Play an audio file at the specified EntityCoordinates for every entity in PVS range.
    /// </summary>
    /// <param name="sound">The sound specifier that points the audio file(s) that should be played.</param>
    /// <param name="coordinates">The EntityCoordinates to attach the audio source to.</param>
    [return: NotNullIfNotNull("sound")]
    public (EntityUid Entity, Components.AudioComponent Component)? PlayPvs(SoundSpecifier? sound, EntityCoordinates coordinates, AudioParams? audioParams = null)
    {
        return sound == null ? null : PlayPvs(GetSound(sound), coordinates, audioParams ?? sound.Params);
    }

    /// <summary>
    /// Play an audio file at the specified EntityCoordinates for every entity in PVS range.
    /// </summary>
    /// <param name="sound">The sound specifier that points the audio file(s) that should be played.</param>
    /// <param name="coordinates">The EntityCoordinates to attach the audio source to.</param>
    [return: NotNullIfNotNull("filename")]
    public (EntityUid Entity, Components.AudioComponent Component)? PlayPvs(string filename, EntityCoordinates coordinates, AudioParams? audioParams = null)
    {
        return PlayStatic(filename, Filter.Pvs(coordinates, entityMan: EntityManager, playerMan: PlayerManager), coordinates, true, audioParams);
    }

    /// <summary>
    /// Play an audio file following an entity for every entity in PVS range.
    /// </summary>
    /// <param name="filename">The resource path to the OGG Vorbis file to play.</param>
    /// <param name="uid">The UID of the entity "emitting" the audio.</param>
    [return: NotNullIfNotNull("filename")]
    public (EntityUid Entity, Components.AudioComponent Component)? PlayPvs(string filename, EntityUid uid, AudioParams? audioParams = null)
    {
        return PlayEntity(filename, Filter.Pvs(uid, entityManager: EntityManager, playerManager:PlayerManager, cfgManager:CfgManager), uid, true, audioParams);
    }

    /// <summary>
    /// Plays a predicted sound following an entity. The server will send the sound to every player in PVS range,
    /// unless that player is attached to the "user" entity that initiated the sound. The client-side system plays
    /// this sound as normal
    /// </summary>
    /// <param name="sound">The sound specifier that points the audio file(s) that should be played.</param>
    /// <param name="source">The UID of the entity "emitting" the audio.</param>
    /// <param name="user">The UID of the user that initiated this sound. This is usually some player's controlled entity.</param>
    [return: NotNullIfNotNull("sound")]
    public abstract (EntityUid Entity, Components.AudioComponent Component)? PlayPredicted(SoundSpecifier? sound, EntityUid source, EntityUid? user, AudioParams? audioParams = null);

    /// <summary>
    /// Plays a predicted sound following an EntityCoordinates. The server will send the sound to every player in PVS range,
    /// unless that player is attached to the "user" entity that initiated the sound. The client-side system plays
    /// this sound as normal
    /// </summary>
    /// <param name="sound">The sound specifier that points the audio file(s) that should be played.</param>
    /// <param name="coordinates">The entitycoordinates "emitting" the audio</param>
    /// <param name="user">The UID of the user that initiated this sound. This is usually some player's controlled entity.</param>
    [return: NotNullIfNotNull("sound")]
    public abstract (EntityUid Entity, Components.AudioComponent Component)? PlayPredicted(SoundSpecifier? sound, EntityCoordinates coordinates, EntityUid? user, AudioParams? audioParams = null);

    /// <summary>
    /// Play an audio file at a static position.
    /// </summary>
    /// <param name="filename">The resource path to the OGG Vorbis file to play.</param>
    /// <param name="playerFilter">The set of players that will hear the sound.</param>
    /// <param name="coordinates">The coordinates at which to play the audio.</param>
    [return: NotNullIfNotNull("filename")]
    public abstract (EntityUid Entity, Components.AudioComponent Component)? PlayStatic(string filename, Filter playerFilter, EntityCoordinates coordinates, bool recordReplay, AudioParams? audioParams = null);

    /// <summary>
    /// Play an audio file at a static position.
    /// </summary>
    /// <param name="filename">The resource path to the OGG Vorbis file to play.</param>
    /// <param name="recipient">The player that will hear the sound.</param>
    /// <param name="coordinates">The coordinates at which to play the audio.</param>
    [return: NotNullIfNotNull("filename")]
    public abstract (EntityUid Entity, Components.AudioComponent Component)? PlayStatic(string filename, ICommonSession recipient, EntityCoordinates coordinates, AudioParams? audioParams = null);

    /// <summary>
    /// Play an audio file at a static position.
    /// </summary>
    /// <param name="filename">The resource path to the OGG Vorbis file to play.</param>
    /// <param name="recipient">The player that will hear the sound.</param>
    /// <param name="coordinates">The coordinates at which to play the audio.</param>
    [return: NotNullIfNotNull("filename")]
    public abstract (EntityUid Entity, Components.AudioComponent Component)? PlayStatic(string filename, EntityUid recipient, EntityCoordinates coordinates, AudioParams? audioParams = null);

    /// <summary>
    /// Play an audio file at a static position.
    /// </summary>
    /// <param name="sound">The sound specifier that points the audio file(s) that should be played.</param>
    /// <param name="playerFilter">The set of players that will hear the sound.</param>
    /// <param name="coordinates">The coordinates at which to play the audio.</param>
    [return: NotNullIfNotNull("sound")]
    public (EntityUid Entity, Components.AudioComponent Component)? PlayStatic(SoundSpecifier? sound, Filter playerFilter, EntityCoordinates coordinates, bool recordReplay, AudioParams? audioParams = null)
    {
        return sound == null ? null : PlayStatic(GetSound(sound), playerFilter, coordinates, recordReplay);
    }

    /// <summary>
    /// Play an audio file at a static position.
    /// </summary>
    /// <param name="sound">The sound specifier that points the audio file(s) that should be played.</param>
    /// <param name="recipient">The player that will hear the sound.</param>
    /// <param name="coordinates">The coordinates at which to play the audio.</param>
    [return: NotNullIfNotNull("sound")]
    public (EntityUid Entity, Components.AudioComponent Component)? PlayStatic(SoundSpecifier? sound, ICommonSession recipient, EntityCoordinates coordinates, AudioParams? audioParams = null)
    {
        return sound == null ? null : PlayStatic(GetSound(sound), recipient, coordinates, audioParams ?? sound.Params);
    }

    /// <summary>
    /// Play an audio file at a static position.
    /// </summary>
    /// <param name="sound">The sound specifier that points the audio file(s) that should be played.</param>
    /// <param name="recipient">The player that will hear the sound.</param>
    /// <param name="coordinates">The coordinates at which to play the audio.</param>
    [return: NotNullIfNotNull("sound")]
    public (EntityUid Entity, Components.AudioComponent Component)? PlayStatic(SoundSpecifier? sound, EntityUid recipient, EntityCoordinates coordinates, AudioParams? audioParams = null)
    {
        return sound == null ? null : PlayStatic(GetSound(sound), recipient, coordinates, audioParams ?? sound.Params);
    }
}