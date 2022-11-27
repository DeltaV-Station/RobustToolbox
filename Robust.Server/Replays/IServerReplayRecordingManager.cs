using Robust.Shared.Console;
using Robust.Shared.Replays;
using System;

namespace Robust.Server.Replays;

public interface IServerReplayRecordingManager : IReplayRecordingManager
{
    void ToggleRecording();
    void StartRecording(TimeSpan? duration = null);
    void StopRecording();

    /// <summary>
    ///     Returns information about the currently ongoing replay recording, including the currently elapsed time and the compressed replay size.
    /// </summary>
    (float Minutes, int Ticks, float Size, float UncompressedSize) GetReplayStats();
}
