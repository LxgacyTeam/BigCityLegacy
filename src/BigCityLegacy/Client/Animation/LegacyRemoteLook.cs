using BepInEx.Configuration;
using UnityEngine;

[DefaultExecutionOrder(30000)]
internal sealed class LegacyRemoteLook : MonoBehaviour
{
    private static ConfigEntry<bool> setting;
    private PlayerControl player;
    private Animator boundAnimator;
    private readonly LegacyLookRig rig = new LegacyLookRig();
    private Quaternion received, smoothed;
    private bool hasReceived, initialized;
    private float weight, bodyWeight;
    private float previousYaw;
    private int poseFrame = -1;

    internal static bool Enabled
    {
        get => setting == null || setting.Value;
        set
        {
            if (setting == null) return;
            setting.Value = value;
            if (!value)
                foreach (var look in Resources.FindObjectsOfTypeAll<LegacyRemoteLook>()) look.ResetPose();
        }
    }

    internal static void Configure(ConfigFile config)
    {
        setting = config.Bind("Client", "RemoteHeadLook", true,
            "Remote characters follow their synchronized camera direction, on foot and in vehicles.");
    }

    internal static void Attach(PlayerControl value)
    {
        if (setting == null || !value || NetManager.isServer || LegacyCommandLine.HasServerArg() ||
            value.isUsedInMenu || value.net_control && value.net_control.hasAuthority) return;
        var input = value.input ? value.input : value.inputOwner;
        if (input && input.type != InputControl.Type.NetUser || !input && !value.net_control) return;
        var look = value.GetComponent<LegacyRemoteLook>();
        if (!look) look = value.gameObject.AddComponent<LegacyRemoteLook>();
        look.player = value;
    }

    internal static void Receive(NetControl network)
    {
        var value = network.control as PlayerControl;
        if (!value || network.hasAuthority) return;
        Attach(value);
        var look = value.GetComponent<LegacyRemoteLook>();
        if (!look || !LegacyLookPose.ValidRotation(network.net_camDir)) return;
        look.received = network.net_camDir;
        look.hasReceived = true;
    }

    internal void ResetPose()
    {
        rig.Clear(); boundAnimator = null;
        initialized = hasReceived = false; weight = bodyWeight = previousYaw = 0; poseFrame = -1;
    }

    private bool IsRemote => setting != null && setting.Value && player && NetManager.isOnlineClient &&
        !NetManager.isServer && !player.isUsedInMenu && !player.isDead &&
        !(player.net_control && player.net_control.hasAuthority) &&
        !(player.input && player.input.type != InputControl.Type.NetUser) &&
        !(player.inputOwner && player.inputOwner.type == InputControl.Type.User);

    private void Update() { rig.Restore(); }
    private void LateUpdate() { TickPose(Time.deltaTime); }
    private void OnEnable() { Camera.onPreCull += BeforeRender; }
    private void OnDisable() { Camera.onPreCull -= BeforeRender; ResetPose(); }

    private void BeforeRender(Camera camera)
    {
        if (!camera || camera.cameraType != CameraType.Game) return;
        if (!IsRemote) { rig.Restore(); return; }
        if (poseFrame != Time.frameCount) TickPose(Time.deltaTime);
        else if (player.animator && player.animator.gameObject.activeInHierarchy) rig.Reapply();
    }

    internal void TickPose(float dt)
    {
        if (!IsRemote) { ResetPose(); return; }
        if (!player.animator || !player.animator.gameObject.activeInHierarchy || player.distToCam > 80) return;
        bool ragdolled = player.ragdoll && player.ragdoll.isRagdolled;
        if (player.ragdoll && player.ragdoll.isUsed() && !ragdolled) { rig.Restore(); return; }
        if (boundAnimator != player.animator || !rig.Ready || player.head && player.head.headTr && player.head.headTr != rig.Head)
        {
            if (!rig.Bind(player)) return;
            boundAnimator = player.animator;
        }
        if (float.IsNaN(dt) || float.IsInfinity(dt) || dt <= 0) { rig.Reapply(); return; }
        dt = Mathf.Min(dt, 0.05f);
        Quaternion target = hasReceived ? received : player.cur_cam_dir;
        if (!LegacyLookPose.ValidRotation(target)) return;
        bool seated = player.parent_control;
        Quaternion reference = ragdolled ? rig.Reference : (player.tr_view ? player.tr_view.rotation : player.transform.rotation);
        bool busy = player.selectWeapone && (player.selectWeapone.firePressed || player.selectWeapone.isReloading());
        Vector2 angles = LegacyLookPose.Target(Quaternion.Inverse(reference) * (target * Vector3.forward), seated, busy, previousYaw);
        if (ragdolled) angles = new Vector2(Mathf.Clamp(angles.x, -55, 55), Mathf.Clamp(angles.y, -20, 20));
        previousYaw = angles.x;
        if (!initialized) { smoothed = reference; initialized = true; }
        smoothed = LegacyLookPose.SmoothWorld(smoothed, reference * Quaternion.Euler(angles.y, angles.x, 0), dt, seated, ragdolled);
        float bodyTarget = busy ? 0 : ragdolled ? 0.2f : seated ? 0.4f : 0.55f;
        bodyWeight = Mathf.MoveTowards(bodyWeight, bodyTarget, dt * 2);
        weight = Mathf.MoveTowards(weight, 1, dt * 4);
        rig.Apply(smoothed, bodyWeight, weight);
        poseFrame = Time.frameCount;
    }
}
