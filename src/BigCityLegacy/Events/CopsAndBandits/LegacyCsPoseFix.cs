using System;

// A bounded main-thread schedule, independent of Unity so lifecycle races can be tested.
internal sealed class LegacyCsPoseFix
{
    internal enum Pass { Initial, SpawnReady }
    private const double LifetimeSeconds = 3d;
    private const double RetrySeconds = 0.1d;
    private const int MaxAttempts = 3;
    private bool initial, ready, attempted;
    private int initialFrame, readyFrame, lastAttemptFrame, initialAttempts, readyAttempts;
    private double nextAttemptAt;
    internal double Deadline { get; private set; }
    internal bool Finished { get { return !initial && !ready; } }

    internal LegacyCsPoseFix(int frame, double now, Pass pass)
    {
        Deadline = now + LifetimeSeconds;
        Add(frame, now, pass);
    }

    internal void Add(int frame, double now, Pass pass)
    {
        if (pass == Pass.Initial) { if (!initial) initialFrame = frame; initial = true; }
        else { if (!ready) readyFrame = frame; ready = true; }
        Deadline = Math.Max(Deadline, now + LifetimeSeconds);
    }

    internal bool Expired(double now) { return now >= Deadline; }

    internal bool TryBegin(int frame, double now, out Pass pass)
    {
        pass = Pass.Initial;
        if (Finished || Expired(now) || (attempted && frame == lastAttemptFrame) || now < nextAttemptAt) return false;
        if (initial && unchecked(frame - initialFrame) > 0) { pass = Pass.Initial; initialAttempts++; }
        else if (!initial && ready && unchecked(frame - readyFrame) > 0) { pass = Pass.SpawnReady; readyAttempts++; }
        else return false;
        attempted = true;
        lastAttemptFrame = frame;
        return true;
    }

    internal void Complete(Pass pass, bool success, double now)
    {
        if (pass == Pass.Initial) { if (success || initialAttempts >= MaxAttempts) initial = false; }
        else if (success || readyAttempts >= MaxAttempts) ready = false;
        nextAttemptAt = success ? now : now + RetrySeconds;
    }
}
