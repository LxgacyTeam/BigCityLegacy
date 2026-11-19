using System.Collections.Generic;
using RootMotion.FinalIK;
using UnityEngine;

internal static class LegacyLookPose
{
    internal static bool ValidRotation(Quaternion value)
    {
        float norm = Quaternion.Dot(value, value);
        return !float.IsNaN(norm) && !float.IsInfinity(norm) && norm > 0.9f && norm < 1.1f;
    }

    internal static Vector2 Target(Vector3 direction, bool seated, bool busy, float previousYaw)
    {
        float horizontal = Mathf.Sqrt(direction.x * direction.x + direction.z * direction.z);
        float yaw = horizontal < 0.001f ? previousYaw : Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        if (Mathf.Abs(yaw) > 165 && Mathf.Abs(previousYaw) > 1) yaw = Mathf.Sign(previousYaw) * Mathf.Abs(yaw);
        float limit = busy ? 70 : seated ? 105 : 120;
        return new Vector2(Mathf.Clamp(yaw, -limit, limit),
            Mathf.Clamp(-Mathf.Atan2(direction.y, horizontal) * Mathf.Rad2Deg, seated ? -35 : -40, seated ? 30 : 35));
    }

    internal static Quaternion SmoothWorld(Quaternion current, Quaternion target, float dt, bool seated, bool ragdolled)
    {
        if (float.IsNaN(dt) || float.IsInfinity(dt) || dt <= 0 || !ValidRotation(target)) return current;
        dt = Mathf.Min(dt, 0.05f);
        float response = ragdolled ? 0.3f : seated ? 0.24f : 0.18f;
        Quaternion filtered = Quaternion.Slerp(current, target, 1 - Mathf.Exp(-dt / response));
        return Quaternion.RotateTowards(current, filtered, (ragdolled ? 70 : seated ? 110 : 160) * dt);
    }
}

internal sealed class LegacyLookRig
{
    private sealed class BonePose
    {
        internal Transform Bone;
        internal Quaternion Before, Written;
        internal bool Applied;
        internal void Restore()
        {
            if (Applied && Bone && Quaternion.Angle(Bone.localRotation, Written) < 0.02f) Bone.localRotation = Before;
            Applied = false;
        }
    }

    private readonly IKSolverLookAt solver = new IKSolverLookAt();
    private BonePose[] bones;
    private Transform root, head, anchor;
    private Quaternion anchorToRoot;
    private Quaternion gaze;
    private float bodyWeight, poseWeight;
    private bool stored;

    internal Transform Head => head;
    internal bool Ready => head && solver.initiated;
    internal Quaternion Reference => anchor ? anchor.rotation * anchorToRoot : root.rotation;

    internal bool Bind(PlayerControl player)
    {
        Clear();
        root = player.tr_view ? player.tr_view : player.transform;
        var animator = player.animator;
        head = player.head ? player.head.headTr : null;
        if (!head && animator.isHuman && animator.avatar && animator.avatar.isValid)
            head = animator.GetBoneTransform(HumanBodyBones.Head);
        if (!head) return false;
        var spine = new List<Transform>();
        Transform nativeSpine = player.head ? player.head.SpineTr : null;
        Transform nativeChest = player.anims ? player.anims.Chest : null;
        for (Transform bone = head.parent; bone && bone != root && bone != player.transform; bone = bone.parent)
        {
            string name = bone.name.ToLowerInvariant();
            if (bone == nativeSpine || bone == nativeChest || name.Contains("spine") || name.Contains("chest")) spine.Add(bone);
        }
        spine.Reverse();
        anchor = spine.Count > 0 ? spine[0].parent : head.parent;
        anchorToRoot = anchor ? Quaternion.Inverse(anchor.rotation) * root.rotation : Quaternion.identity;
        if (!solver.SetChain(spine.ToArray(), head, new Transform[0], root) || !solver.initiated) return false;
        if (player.head && player.head.headTr == head && LegacyLookPose.ValidRotation(player.head.baseHeadRot))
            solver.head.axis = player.head.baseHeadRot * Vector3.forward;
        solver.headWeight = 1;
        solver.eyesWeight = 0;
        solver.clampWeight = 0;
        solver.clampWeightHead = 0;
        solver.clampSmoothing = 2;
        bones = new BonePose[spine.Count + 1];
        for (int i = 0; i < spine.Count; i++) bones[i] = new BonePose { Bone = spine[i] };
        bones[bones.Length - 1] = new BonePose { Bone = head };
        return true;
    }

    internal void Apply(Quaternion direction, float body, float weight)
    {
        if (!Ready) return;
        gaze = direction; bodyWeight = body; poseWeight = weight; stored = true;
        Reapply();
    }

    internal void Restore()
    {
        if (bones == null) return;
        for (int i = bones.Length - 1; i >= 0; i--) bones[i].Restore();
    }

    internal void Reapply()
    {
        if (!stored || !Ready) return;
        Restore();
        foreach (BonePose pose in bones) pose.Before = pose.Bone.localRotation;
        solver.bodyWeight = bodyWeight;
        solver.IKPositionWeight = poseWeight;
        solver.IKPosition = head.position + gaze * Vector3.forward * 10;
        solver.Update();
        foreach (BonePose pose in bones) { pose.Written = pose.Bone.localRotation; pose.Applied = true; }
    }

    internal void Clear()
    {
        Restore(); stored = false; bones = null; head = anchor = root = null;
    }
}
