﻿using System;
using System.IO;
using SS14.Shared.Bsdiff;
using SS14.Shared.GameStates;
using SS14.Shared.Interfaces.Serialization;
using SS14.Shared.IoC;
using SS14.Shared.Serialization;

namespace SS14.Shared.GameStates
{
    [Serializable, NetSerializable]
    public class GameStateDelta
    {
        public byte[] deltaBytes;
        public uint FromSequence { get; set; }
        public uint Sequence { get; set; }

        public GameStateDelta(byte[] bytes)
        {
            deltaBytes = bytes;
        }

        public GameStateDelta()
        {
        }

        public long Size
        {
            get { return deltaBytes.Length; }
        }

        public void Create(GameState fromState, GameState toState)
        {
            Sequence = toState.Sequence;
            FromSequence = fromState.Sequence;
            using (var stream = Bsdiff.Bsdiff.GenerateBzip2Diff(fromState.GetSerializedDataBuffer(), toState.GetSerializedDataBuffer()))
            {
                deltaBytes = stream.ToArray();
            }
        }

        public GameState Apply(GameState fromState)
        {
            if (fromState.Sequence != FromSequence)
                throw new Exception("Cannot apply GameStateDelta. Sequence incorrect.");
            byte[] fromBuffer = fromState.GetSerializedDataStream().ToArray();
            using (var newBytes = Bsdiff.Bsdiff.ApplyBzip2Patch(fromBuffer, deltaBytes))
            {
                var serializer = IoCManager.Resolve<ISS14Serializer>();
                return serializer.Deserialize<GameState>(newBytes);
            }
        }
    }
}
