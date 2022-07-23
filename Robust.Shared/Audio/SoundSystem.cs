using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Robust.Shared.Audio
{
    /// <summary>
    /// A static proxy class for interfacing with the AudioSystem.
    /// </summary>
    public static class SoundSystem
    {
        /// <summary>
        /// Used in the PAS to designate the physics collision mask of occluders.
        /// </summary>
        public static int OcclusionCollisionMask
        {
            get => GetAudio()?.OcclusionCollisionMask ?? 0;
            set
            {
                var audio = GetAudio();

                if (audio is null)
                    return;
                audio.OcclusionCollisionMask = value;
            }
        }

        private static IAudioSystem? GetAudio()
        {
            // There appears to be no way to get a System by interface.
            var args = new QueryAudioSystem();
            IoCManager.Resolve<IEntityManager>().EventBus.RaiseEvent(EventSource.Local, args);
            return args.Audio;
        }

        /// <summary>
        /// Play an audio file globally, without position.
        /// </summary>
        /// <param name="filename">The resource path to the OGG Vorbis file to play.</param>
        /// <param name="playerFilter">The set of players that will hear the sound.</param>
        /// <param name="audioParams">Audio parameters to apply when playing the sound.</param>
        public static IPlayingAudioStream? Play(string filename, Filter playerFilter, AudioParams? audioParams = null)
        {
            return GetAudio()?.Play(filename, playerFilter, audioParams);
        }

        /// <summary>
        /// Play an audio file following an entity.
        /// </summary>
        /// <param name="filename">The resource path to the OGG Vorbis file to play.</param>
        /// <param name="playerFilter">The set of players that will hear the sound.</param>
        /// <param name="uid">The UID of the entity "emitting" the audio.</param>
        /// <param name="audioParams">Audio parameters to apply when playing the sound.</param>
        public static IPlayingAudioStream? Play(string filename, Filter playerFilter, EntityUid uid,
            AudioParams? audioParams = null)
        {
            return GetAudio()?.Play(filename, playerFilter, uid, audioParams);
        }

        /// <summary>
        /// Play an audio file at a static position.
        /// </summary>
        /// <param name="filename">The resource path to the OGG Vorbis file to play.</param>
        /// <param name="playerFilter">The set of players that will hear the sound.</param>
        /// <param name="coordinates">The coordinates at which to play the audio.</param>
        /// <param name="audioParams">Audio parameters to apply when playing the sound.</param>
        public static IPlayingAudioStream? Play(string filename, Filter playerFilter, EntityCoordinates coordinates,
            AudioParams? audioParams = null)
        {
            return GetAudio()?.Play(filename, playerFilter, coordinates, audioParams);
        }

        internal sealed class QueryAudioSystem : EntityEventArgs
        {
            public IAudioSystem? Audio { get; set; }
        }
    }
}
