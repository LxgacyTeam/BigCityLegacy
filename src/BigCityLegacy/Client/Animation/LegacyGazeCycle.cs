using UnityEngine;

internal sealed class LegacyGazeCycle
{
    private readonly System.Random random;
    private float age, duration;
    private bool seatedMode, glancing;
    private Vector2 side;
    internal bool IsGlancing { get { return glancing; } }
    internal bool AllowHidden { get; private set; }

    internal LegacyGazeCycle(int seed) { random = new System.Random(seed); Reset(false); }
    internal void Reset(bool seated)
    {
        seatedMode = seated; age = 0; glancing = false; AllowHidden = false;
        duration = Range(seated ? 3 : 5, seated ? 5 : 9);
    }
    internal bool Advance(float dt, bool seated, bool busy)
    {
        if (seated != seatedMode) Reset(seated);
        if (float.IsNaN(dt) || float.IsInfinity(dt) || dt <= 0) return false;
        if (busy) { if (glancing) Reset(seated); age = 0; return false; }
        age += Mathf.Min(dt, 0.1f);
        if (age < duration) return false;
        age = 0; glancing = !glancing;
        if (!glancing)
        {
            duration = Range(seated ? 4 : 5, seated ? 7 : 9); AllowHidden = false;
            return false;
        }
        duration = Range(seated ? 1.3f : 0.8f, seated ? 2f : 1.2f);
        side = new Vector2(Range(seated ? -12 : -18, seated ? 12 : 18), Range(-1.5f, 1.5f));
        AllowHidden = seated && random.NextDouble() < 0.22;
        return true;
    }
    internal Vector2 Desired(Vector2 view, Vector2? nearby, bool busy)
    {
        if (busy) return view;
        float envelope = glancing ? Mathf.SmoothStep(0,1,Mathf.Clamp01(age/0.35f)) *
            Mathf.SmoothStep(0,1,Mathf.Clamp01((duration-age)/0.65f)) : 0;
        if (seatedMode)
        {
            var forward = new Vector2(Mathf.Clamp(view.x*0.12f,-5,5), Mathf.Clamp(view.y*0.1f,-1,1));
            return Vector2.Lerp(forward, nearby ?? side, envelope);
        }
        if (nearby.HasValue) return nearby.Value * (1 - envelope*0.08f);
        return Vector2.Lerp(view*0.8f,side,envelope*0.35f);
    }
    private float Range(float min,float max) { return min+(float)random.NextDouble()*(max-min); }
}
