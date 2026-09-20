using UnityEngine;
using UnityEngine.Networking;

internal static class LegacyTypingIndicator
{
    private const float HeartbeatSeconds = 2f;

    private static GameObject _host;

    internal static void Ensure()
    {
        if (_host) return;
        _host = new GameObject("LegacyTypingIndicator");
        Object.DontDestroyOnLoad(_host);
        _host.AddComponent<Worker>();
    }

    private sealed class Worker : MonoBehaviour
    {
        private bool _lastFocused;
        private float _nextHeartbeat;

        private void Update()
        {
            bool focused = NetChat.me != null && NetChat.nowTyping;
            if (focused != _lastFocused)
            {
                _lastFocused = focused;
                Send(focused);
                _nextHeartbeat = Time.unscaledTime + HeartbeatSeconds;
            }
            else if (focused && Time.unscaledTime >= _nextHeartbeat)
            {
                _nextHeartbeat = Time.unscaledTime + HeartbeatSeconds;
                Send(true);
            }
        }

        private void OnDestroy()
        {
            Send(false);
        }

        private static void Send(bool typing)
        {
            if (!NetworkClient.active) return;
            InputControl first = InputControl.GetFirstUser();
            if (!first || !first.netInput) return;
            if (!first.netInput.hasAuthority || !first.netInput.netControl) return;
            first.netInput.CallCmd_WatchAds(typing);
        }
    }
}