using System.Collections.Generic;
using UnityEngine;

internal static class LegacyChatEventQueue
{
    private const float MinIntervalSeconds = 0.25f;
    private static readonly Queue<(NetInputControl netInput, string message)> _queue = new Queue<(NetInputControl, string)>();
    private static float _lastSendTime = -999f;

    internal static void Enqueue(NetInputControl netInput, string message)
    {
        if (netInput == null || !LegacyCommandLine.ChatEventsEnabled)
        {
            return;
        }

        _queue.Enqueue((netInput, message));
    }

    internal static void Tick()
    {
        if (_queue.Count == 0)
        {
            return;
        }

        if (Time.unscaledTime - _lastSendTime < MinIntervalSeconds)
        {
            return;
        }

        var (netInput, message) = _queue.Dequeue();
        _lastSendTime = Time.unscaledTime;
        ChatEventsPatches.SendChatMessage(netInput, message);
    }
}
