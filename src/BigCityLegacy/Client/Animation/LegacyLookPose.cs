using UnityEngine;

internal static class LegacyLookPose
{
    internal static Vector2 Target(Vector3 direction, bool seated, bool busy, bool tracking = false, float previousYaw = 0)
    {
        if (!Finite(direction.x) || !Finite(direction.y) || !Finite(direction.z) || direction.sqrMagnitude < 0.0001f) return Vector2.zero;
        float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        float pitch = -Mathf.Atan2(direction.y, Mathf.Sqrt(direction.x * direction.x + direction.z * direction.z)) * Mathf.Rad2Deg;
        if (tracking && Mathf.Abs(yaw) > 150 && Mathf.Abs(previousYaw) > 1) yaw = Mathf.Sign(previousYaw) * Mathf.Abs(yaw);
        float fade = tracking ? 1 : 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(100, 165, Mathf.Abs(yaw)));
        float maxYaw = tracking ? (seated ? 40 : 120) : busy ? 45 : seated ? 18 : 65;
        pitch *= Mathf.Lerp(1, 0.55f, Mathf.Clamp01(Mathf.Abs(yaw) / 120));
        return new Vector2(Mathf.Clamp(yaw, -maxYaw, maxYaw) * fade,
            Mathf.Clamp(pitch, seated ? -7 : -14, seated ? 7 : 14) * fade);
    }

    internal static Vector2 Smooth(Vector2 current, Vector2 target, ref Vector2 velocity, float dt, bool seated)
    {
        if (!Finite(dt) || dt <= 0) return current;
        dt = Mathf.Min(dt, 0.1f);
        float xVelocity = velocity.x, yVelocity = velocity.y;
        float x = Mathf.SmoothDamp(current.x, target.x, ref xVelocity, seated ? 0.42f : 0.22f, seated ? 55 : 135, dt);
        float y = Mathf.SmoothDamp(current.y, target.y, ref yVelocity, seated ? 0.55f : 0.35f, seated ? 20 : 35, dt);
        velocity = new Vector2(xVelocity, yVelocity);
        return new Vector2(x, y);
    }

    internal static bool ValidRotation(Quaternion q)
    {
        float norm = q.x*q.x + q.y*q.y + q.z*q.z + q.w*q.w;
        return Finite(norm) && norm > 0.9f && norm < 1.1f;
    }
    private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
}

internal sealed class LegacyLookBone
{
    internal Transform Bone;
    private Quaternion before, written;
    private bool applied;

    internal void Restore()
    {
        if (applied && Bone)
        {
            Quaternion current = Bone.localRotation;
            float same = Mathf.Abs(current.x-written.x)+Mathf.Abs(current.y-written.y)+Mathf.Abs(current.z-written.z)+Mathf.Abs(current.w-written.w);
            float opposite = Mathf.Abs(current.x+written.x)+Mathf.Abs(current.y+written.y)+Mathf.Abs(current.z+written.z)+Mathf.Abs(current.w+written.w);
            if (Mathf.Min(same, opposite) < 0.0001f) Bone.localRotation = before;
        }
        applied = false;
    }

    internal void Apply(Transform root, float yaw)
    {
        if (!Bone) return;
        Restore(); before = Bone.localRotation;
        Bone.rotation = Quaternion.AngleAxis(yaw, root.up) * Bone.rotation;
        written = Bone.localRotation; applied = true;
    }

    internal void Aim(Transform root, Vector2 angles, Quaternion boneToGaze, float weight)
    {
        if (!Bone) return;
        Restore(); before = Bone.localRotation;
        Quaternion target = root.rotation * Quaternion.Euler(angles.y, angles.x, 0) * Quaternion.Inverse(boneToGaze);
        Bone.rotation = Quaternion.Slerp(Bone.rotation, target, weight);
        written = Bone.localRotation; applied = true;
    }
}
