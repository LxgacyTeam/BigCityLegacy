using BepInEx.Configuration;
using UnityEngine;

[DefaultExecutionOrder(10000)]
internal sealed class LegacyRemoteLook : MonoBehaviour
{
    private static ConfigEntry<bool> setting;
    private PlayerControl player, interest;
    private readonly LegacyLookBone head = new LegacyLookBone(), torso = new LegacyLookBone();
    private readonly RaycastHit[] sightHits = new RaycastHit[16];
    private Vector2 angles, velocity;
    private float bodyYaw, bodyVelocity, interestRefresh, poseWeight;
    private Animator cachedAnimator;
    private Quaternion fallbackCorrection = Quaternion.identity;
    private LegacyGazeCycle gaze;
    private bool engaged, seatedBefore;

    internal static void Configure(ConfigFile config)
    {
        setting = config.Bind("Client", "RemoteHeadLook", true,
            "Natural local head motion for remote players; relaxed short glances in vehicles, including occasional hidden targets.");
    }
    internal static void Attach(PlayerControl value)
    {
        if (setting == null || !value || NetManager.lastWasServer || LegacyCommandLine.HasServerArg() || !value.input || value.input.type != InputControl.Type.NetUser) return;
        var component = value.GetComponent<LegacyRemoteLook>();
        if (!component) component = value.gameObject.AddComponent<LegacyRemoteLook>();
        component.player = value;
        if (component.gaze == null) component.gaze = new LegacyGazeCycle(value.GetInstanceID());
    }
    internal void ResetPose()
    {
        head.Restore(); torso.Restore(); angles = velocity = Vector2.zero;
        bodyYaw = bodyVelocity = poseWeight = 0; interest = null; interestRefresh = 0;
        if (engaged && gaze != null) gaze.Reset(player && player.parent_control);
        engaged = false;
    }
    private void Update() { head.Restore(); torso.Restore(); }
    private void LateUpdate() { TickPose(Time.deltaTime); }
    internal void TickPose(float dt)
    {
        if (setting == null || !setting.Value || !player || !NetManager.isOnlineClient || NetManager.lastWasServer ||
            !player.input || player.input.type != InputControl.Type.NetUser || (player.net_control && player.net_control.hasAuthority) ||
            player.isDead || (player.ragdoll && player.ragdoll.isUsed()) || player.isUsedInMenu ||
            !player.animator || !player.animator.isActiveAndEnabled || !player.AnimatorActive ||
            player.animator.cullingMode == AnimatorCullingMode.CullCompletely || player.distToCam > 80)
        { ResetPose(); return; }
        if (float.IsNaN(dt) || float.IsInfinity(dt) || dt <= 0) return;
        dt = Mathf.Min(dt,0.1f);
        bool seated = player.parent_control;
        if (player.InCar && player.InCar.carState != PlayerInCar.CarState.None && player.InCar.carState != PlayerInCar.CarState.InCar)
        { ResetPose(); return; }
        Transform root = player.tr_view ? player.tr_view : player.transform;
        Transform nativeHead = player.head ? player.head.headTr : null;
        if (cachedAnimator != player.animator || !head.Bone || (nativeHead && nativeHead != head.Bone))
        {
            ResetPose(); cachedAnimator = player.animator; head.Bone = nativeHead;
            torso.Bone = player.anims && player.anims.Chest ? player.anims.Chest : player.head ? player.head.SpineTr : null;
            if (!head.Bone && cachedAnimator.isHuman && cachedAnimator.avatar && cachedAnimator.avatar.isValid)
            {
                head.Bone = cachedAnimator.GetBoneTransform(HumanBodyBones.Head);
                torso.Bone = cachedAnimator.GetBoneTransform(HumanBodyBones.Chest) ?? cachedAnimator.GetBoneTransform(HumanBodyBones.Spine);
            }
            if (head.Bone) fallbackCorrection = Quaternion.Inverse(head.Bone.rotation) * root.rotation;
        }
        if (!head.Bone) { ResetPose(); return; }
        if (seated != seatedBefore)
        {
            interest = null; interestRefresh = 0; seatedBefore = seated;
            if (seated) bodyYaw = bodyVelocity = 0;
        }
        bool armed = player.selectWeapone is PlayerWeaponeBase;
        bool busy = player.selectWeapone && (player.selectWeapone.firePressed || player.selectWeapone.isReloading());
        Vector3 direction = Quaternion.Inverse(root.rotation) * (player.cur_cam_dir * Vector3.forward);
        Vector2 view = LegacyLookPose.Target(direction,seated,busy);
        bool startGlance = gaze.Advance(dt,seated,busy);
        if (busy || (seated && (!gaze.IsGlancing || startGlance))) { interest = null; interestRefresh = 0; }
        if (!busy && (!seated || gaze.IsGlancing))
        {
            interestRefresh -= dt;
            if (interestRefresh <= 0)
            {
                if (!ValidInterest(interest,seated,true)) interest = FindInterest(root,seated,gaze.AllowHidden);
                interestRefresh = 0.5f;
            }
        }
        Vector2? nearby = null;
        if (ValidInterest(interest,seated,true))
        {
            Vector3 toward = Quaternion.Inverse(root.rotation) * (EyePosition(interest) - head.Bone.position);
            nearby = LegacyLookPose.Target(toward,seated,false,true,angles.x);
        }
        Vector2 desired = gaze.Desired(view,nearby,busy);
        bool turnBody = !seated && !busy && !armed && torso.Bone;
        if (!turnBody) desired.x = Mathf.Clamp(desired.x,seated ? -40 : -78,seated ? 40 : 78);
        angles = LegacyLookPose.Smooth(angles,desired,ref velocity,dt,seated);
        float bodyTarget = turnBody ? Mathf.Clamp(Mathf.Sign(angles.x)*Mathf.Max(0,Mathf.Abs(angles.x)-25)*0.65f,-40,40) : 0;
        bodyYaw = Mathf.SmoothDamp(bodyYaw,bodyTarget,ref bodyVelocity,0.4f,70,dt);
        torso.Apply(root,bodyYaw);
        var aim = new Vector2(bodyYaw+Mathf.Clamp(angles.x-bodyYaw,seated ? -40 : -80,seated ? 40 : 80),angles.y);
        Quaternion correction = player.head && player.head.headTr == head.Bone && LegacyLookPose.ValidRotation(player.head.baseHeadRot)
            ? player.head.baseHeadRot : fallbackCorrection;
        poseWeight = Mathf.MoveTowards(poseWeight,1,dt/(seated ? 0.65f : 0.35f));
        head.Aim(root,aim,correction,poseWeight*(seated ? 0.9f : 0.95f));
        engaged = true;
    }
    internal static Vector3 EyePosition(PlayerControl other)
    {
        if (other.head && other.head.headTr && other.head.headTr.gameObject.activeInHierarchy) return other.head.headTr.position;
        if (other.anims && other.anims.head && other.anims.head.gameObject.activeInHierarchy) return other.anims.head.position;
        Transform model = other.tr_view ? other.tr_view : other.transform;
        return model.position + model.up*1.6f;
    }
    private bool ValidInterest(PlayerControl other,bool seated,bool retaining)
    {
        if (!other || other == player || other.isDead || !other.gameObject.activeInHierarchy || other.isUsedInMenu || !other.input ||
            (other.input.type != InputControl.Type.User && other.input.type != InputControl.Type.NetUser)) return false;
        float distance = (EyePosition(other)-head.Bone.position).sqrMagnitude;
        float radius = (seated ? 22 : 16)+(retaining ? 4 : 0);
        return distance >= 0.25f && distance < radius*radius;
    }
    private PlayerControl FindInterest(Transform root,bool seated,bool allowHidden)
    {
        PlayerControl best = null, hidden = null; float bestScore = float.PositiveInfinity, hiddenScore = float.PositiveInfinity;
        for (int i=0;i<Control.controls.Count;i++)
        {
            var other = Control.controls[i] as PlayerControl;
            if (!ValidInterest(other,seated,false)) continue;
            Vector3 delta = EyePosition(other)-head.Bone.position;
            float facing = Vector3.Dot(root.forward,delta.normalized);
            if (facing < -0.5f) continue;
            bool visible = HasSight(other,delta);
            if (!visible && !allowHidden) continue;
            float score = delta.magnitude*(1+0.7f*(1-facing));
            if (!visible)
            {
                if (score < hiddenScore) { hiddenScore = score; hidden = other; }
                continue;
            }
            if (score >= bestScore) continue;
            bestScore = score; best = other;
        }
        return allowHidden && hidden ? hidden : best;
    }
    private bool HasSight(PlayerControl other,Vector3 delta)
    {
        float distance = delta.magnitude;
        int count = Physics.RaycastNonAlloc(head.Bone.position,delta/distance,sightHits,distance,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
        if (count == sightHits.Length) return false;
        for (int i=0;i<count;i++)
        {
            Transform hit = sightHits[i].transform;
            if (!hit || hit.IsChildOf(player.transform) || hit.IsChildOf(other.transform) ||
                (player.parent_control && hit.IsChildOf(player.parent_control.transform))) continue;
            return false;
        }
        return true;
    }
    private void OnDisable() { ResetPose(); }
}
