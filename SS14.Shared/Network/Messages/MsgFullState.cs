﻿using Lidgren.Network;
using SS14.Shared.GameStates;
using SS14.Shared.Interfaces.Network;
using SS14.Shared.Interfaces.Serialization;
using SS14.Shared.IoC;
using System.IO;
using System.IO.Compression;

namespace SS14.Shared.Network.Messages
{
    public class MsgFullState : NetMessage
    {
        #region REQUIRED
        public static readonly NetMessages ID = NetMessages.FullState;
        public static readonly MsgGroups GROUP = MsgGroups.Core;

        public static readonly string NAME = ID.ToString();
        public MsgFullState(INetChannel channel)
            : base(NAME, GROUP, ID)
        { }
        #endregion

        public GameState State { get; set; }

        public override void ReadFromBuffer(NetIncomingMessage buffer)
        {
            int length = buffer.ReadInt32();
            byte[] stateData = Decompress(buffer.ReadBytes(length));
            using (var stateStream = new MemoryStream(stateData))
            {
                var serializer = IoCManager.Resolve<ISS14Serializer>();
                State = serializer.Deserialize<GameState>(stateStream);
            }
        }

        public override void WriteToBuffer(NetOutgoingMessage buffer)
        {
            byte[] stateData = Compress(State.GetSerializedDataBuffer());
            
            buffer.Write(stateData.Length);
            buffer.Write(stateData);
        }

        #region Compression

        /// <summary>
        /// Compresses a decompressed state data byte array into a compressed one.
        /// </summary>
        /// <param name="stateData">full state data</param>
        /// <returns></returns>
        private static byte[] Compress(byte[] stateData)
        {
            using (var compressedDataStream = new MemoryStream())
            {
                using (var gzip = new GZipStream(compressedDataStream, CompressionMode.Compress, true))
                {
                    gzip.Write(stateData, 0, stateData.Length);
                }
                return compressedDataStream.ToArray();
            }
        }

        /// <summary>
        /// Decompresses a compressed state data byte array into a decompressed one.
        /// </summary>
        /// <param name="compressedStateData">compressed state data</param>
        /// <returns></returns>
        private static byte[] Decompress(byte[] compressedStateData)
        {
            // Create a GZIP stream with decompression mode.
            // ... Then create a buffer and write into while reading from the GZIP stream.
            using (var compressedStream = new MemoryStream(compressedStateData))
            using (var stream = new GZipStream(compressedStream, CompressionMode.Decompress))
            {
                const int size = 2048;
                var buffer = new byte[size];
                using (var memory = new MemoryStream())
                {
                    int count = 0;
                    do
                    {
                        count = stream.Read(buffer, 0, size);
                        if (count > 0)
                        {
                            memory.Write(buffer, 0, count);
                        }
                    } while (count > 0);
                    return memory.ToArray();
                }
            }
        }

        #endregion Compression
    }
}
