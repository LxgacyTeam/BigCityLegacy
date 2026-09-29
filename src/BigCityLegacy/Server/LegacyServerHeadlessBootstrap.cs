using System;
using System.Collections;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LegacyServerHeadlessBootstrap : MonoBehaviour
{
    internal static ManualLogSource Logger;

    private static bool installed;
    private static int mapTemplatesPrepared;
    private static int mapTemplateComponentsRemoved;
    private static int groundComponentsRemoved;
    private static int sceneComponentsRemoved;
    private static int chatObjectsRemoved;
    private static int clientUiReferencesReleased;
    private static int clientRuntimeBehavioursDisabled;

    // Command line flags do not change during a Unity process lifetime. Cache the
    // headless/optimization decisions because CPU patches query them from hot
    // per-frame paths.
    private static bool? cachedHeadlessServer;
    private static bool? cachedObjectOptimizationEnabled;
    private static bool? cachedCpuOptimizationEnabled;

    public static bool IsHeadlessServer
    {
        get
        {
            if (cachedHeadlessServer.HasValue)
            {
                return cachedHeadlessServer.Value;
            }

            bool result = false;
            if (NetManagerTools.isCommandLineArgHaveServerStr())
            {
                if (Application.isBatchMode)
                {
                    result = true;
                }
                else
                {
                    string[] commandLineArgs = Environment.GetCommandLineArgs();
                    for (int i = 0; i < commandLineArgs.Length; i++)
                    {
                        if (string.Equals(commandLineArgs[i], "-nographics", StringComparison.OrdinalIgnoreCase))
                        {
                            result = true;
                            break;
                        }
                    }
                }
            }

            cachedHeadlessServer = result;
            return result;
        }
    }

    // Emergency rollback switch for the 1.3.2 stripping path.
    public static bool IsOptimizationEnabled
    {
        get
        {
            if (!cachedObjectOptimizationEnabled.HasValue)
            {
                cachedObjectOptimizationEnabled =
                    IsHeadlessServer && !LegacyCommandLine.HasArg("-noServerObjectStripping");
            }
            return cachedObjectOptimizationEnabled.Value;
        }
    }

    // Separate rollback switch for CPU-only optimizations. Keep it independent
    // from object stripping so either layer can be A/B tested on its own.
    public static bool IsCpuOptimizationEnabled
    {
        get
        {
            if (!cachedCpuOptimizationEnabled.HasValue)
            {
                cachedCpuOptimizationEnabled =
                    IsHeadlessServer && !LegacyCommandLine.HasArg("-noServerCpuOptimization");
            }
            return cachedCpuOptimizationEnabled.Value;
        }
    }

    public static void Install(GameObject host)
    {
        if (!IsOptimizationEnabled || installed || host == null)
        {
            return;
        }

        Logger = BigCityLegacyPlugin.Log;
        host.GetOrAddComponent<LegacyServerHeadlessBootstrap>();
        installed = true;
        Debug.Log("[BigCityLegacy] Headless pre-/mid-load object stripping enabled. Use -noServerObjectStripping to disable it.");
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        StartCoroutine(StripAllLoadedScenesDeferred());
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsOptimizationEnabled)
        {
            return;
        }
        StartCoroutine(StripSceneDeferred(scene));
    }

    private IEnumerator StripAllLoadedScenesDeferred()
    {
        // Scene objects have already been deserialized at this point, so this is only
        // a safety-net for scene-resident visual components. The important MapPro,
        // GroundLoader and UI paths are stripped earlier by Harmony patches.
        yield return null;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.IsValid() && scene.isLoaded)
            {
                StripScene(scene);
            }
        }

        yield return Resources.UnloadUnusedAssets();
        GC.Collect();

        #if DEBUG
            LogSummary("initial scenes");
        #endif
    }

    private IEnumerator StripSceneDeferred(Scene scene)
    {
        yield return null;
        StripScene(scene);

        // Do not force a full GC for every additive scene. UnloadUnusedAssets is
        // enough to release assets made unreachable by the stripping pass.
        yield return Resources.UnloadUnusedAssets();
    }

    private static void StripScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            StripSceneRecursive(roots[i].transform);
        }
    }

    private static void StripSceneRecursive(Transform tr)
    {
        if (!tr)
        {
            return;
        }

        Camera camera = tr.GetComponent<Camera>();
        if (camera != null)
        {
            camera.enabled = false;
        }

        sceneComponentsRemoved += DestroyDeferred(tr.GetComponent<AudioListener>());
        sceneComponentsRemoved += DestroyDeferred(tr.GetComponent<AudioSource>());
        sceneComponentsRemoved += DestroyDeferred(tr.GetComponent<Light>());
        sceneComponentsRemoved += DestroyDeferred(tr.GetComponent<ReflectionProbe>());
        sceneComponentsRemoved += DestroyDeferred(tr.GetComponent<ParticleSystem>());

        Canvas canvas = tr.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.enabled = false;
        }

        Graphic[] graphics = tr.GetComponents<Graphic>();
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
            {
                graphics[i].enabled = false;
            }
        }

        Renderer[] renderers = tr.GetComponents<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            sceneComponentsRemoved += DestroyDeferred(renderers[i]);
        }

        sceneComponentsRemoved += DestroyDeferred(tr.GetComponent<LODGroup>());

        for (int i = 0; i < tr.childCount; i++)
        {
            StripSceneRecursive(tr.GetChild(i));
        }
    }

    internal static bool TrySetLoadedMapObject(MapProObjInfo info, UnityEngine.Object file)
    {
        if (!IsOptimizationEnabled || info == null)
        {
            return false;
        }

        // This is the headless replacement for MapProObjInfo.SetLoadedObject().
        // Vanilla creates a server-side clone and strips only root components, but
        // leaves copyFrom pointing at the original unstripped prefab. CreateObj()
        // later instantiates copyFrom, effectively bringing the visuals back.
        // Here the stripped clone itself becomes the server template.
        info.isLoaded = true;

        GameObject source = file as GameObject;
        info.obj = source;
        info.copyFrom = source;
        if (source == null)
        {
            return true;
        }

        GameObject template = UnityEngine.Object.Instantiate<GameObject>(source);
        template.SetActive(false);
        template.hideFlags = HideFlags.HideAndDontSave;

        int removed = StripMapTemplateImmediate(template);

        info.obj = template;
        info.copyFrom = template;
        info.decalData = template.GetComponent<DecalData>();
        info.isDecal = info.decalData != null;
        // Preserve vanilla metadata semantics; only the source template changes.
        info.isMovable = template.GetComponentInChildren<Rigidbody>() != null;
        info.quality = template.GetComponent<Quality>();

        mapTemplatesPrepared++;
        mapTemplateComponentsRemoved += removed;
        return true;
    }

    internal static void ReleaseMapTemplate(MapProObjInfo info)
    {
        if (!IsOptimizationEnabled || info == null)
        {
            return;
        }

        // Our headless path intentionally makes copyFrom == obj. Vanilla Remove()
        // treats that shape as an asset and does not destroy it, so release the
        // runtime template explicitly before the original cleanup runs.
        if (info.obj != null && info.copyFrom == info.obj)
        {
            UnityEngine.Object.Destroy(info.obj);
            info.obj = null;
            info.copyFrom = null;
        }
    }

    internal static void StripGroundInstance(GroundItem item)
    {
        if (!IsOptimizationEnabled || item == null || item.Instanciated == null)
        {
            return;
        }

        // GroundItem.Clear() expects the root MeshFilter to stay present because it
        // unloads its sharedMesh explicitly. Keep MeshFilter/MeshCollider/physics,
        // remove only presentation components.
        GameObject root = item.Instanciated;
        int removed = 0;
        removed += DestroyComponentsImmediate<Renderer>(root);
        removed += DestroyComponentsImmediate<LODGroup>(root);
        removed += DestroyComponentsImmediate<ParticleSystem>(root);
        removed += DestroyComponentsImmediate<AudioSource>(root);
        removed += DestroyComponentsImmediate<AudioListener>(root);
        removed += DestroyComponentsImmediate<Light>(root);
        removed += DestroyComponentsImmediate<ReflectionProbe>(root);
        groundComponentsRemoved += removed;
    }

    internal static void CreateMinimalGameUI(GameUI gameUi)
    {
        if (!IsOptimizationEnabled || gameUi == null)
        {
            return;
        }

        GameUI.me = gameUi;
        // The compatibility shell is accessed explicitly by game code; its own
        // Unity Update/FixedUpdate/LateUpdate callbacks are client-only.
        gameUi.enabled = false;

        if (gameUi.ui != null)
        {
            UnityEngine.Object.DestroyImmediate(gameUi.ui);
        }

        // Keep only the tiny compatibility shell that server-side game code expects:
        // GameUI.me, rectUI/img and FadeUI.me. Do not instantiate SplitScreenUI,
        // minimap, popup, weapon UI, touch UI, etc.
        GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
        canvasObject.hideFlags = HideFlags.HideAndDontSave;
        canvasObject.transform.SetParent(gameUi.transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.enabled = false;

        GameObject rootObject = new GameObject("HeadlessUIRoot", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        rootObject.hideFlags = HideFlags.HideAndDontSave;
        rootObject.transform.SetParent(canvasObject.transform, false);

        RawImage rawImage = rootObject.GetComponent<RawImage>();
        rawImage.enabled = false;
        rawImage.raycastTarget = false;
        rawImage.color = Color.clear;
        rawImage.rectTransform.sizeDelta = new Vector2(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));

        GameObject fadeObject = new GameObject("Fade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fadeObject.hideFlags = HideFlags.HideAndDontSave;
        fadeObject.transform.SetParent(rootObject.transform, false);
        Image fadeImage = fadeObject.GetComponent<Image>();
        fadeImage.enabled = false;
        fadeImage.raycastTarget = false;
        fadeImage.color = Color.clear;

        gameUi.ui = canvasObject;
        gameUi.img = rawImage;
        gameUi.fadeUI = rootObject.AddComponent<FadeUI>();
        if (gameUi.fadeUI != null && gameUi.fadeUI.imgFade != null)
        {
            gameUi.fadeUI.imgFade.enabled = false;
            gameUi.fadeUI.imgFade.gameObject.SetActive(false);
        }

        // Release direct references to client-only UI prefabs. They can then become
        // eligible for UnloadUnusedAssets if nothing else in the loaded scene holds them.
        gameUi.SplitScreenUI = null;
        gameUi.miniMap = null;
        gameUi.weaponeSelect = null;
        gameUi.sitSelect = null;
        gameUi.WeaponeUiTool = null;
        gameUi.touchs = null;
        gameUi.popup = null;
        gameUi.parkingInfo = null;
        gameUi.enabled = false;
    }

    internal static void ReleaseNuligineClientUiReferences(Nuligine nuligine)
    {
        if (!IsOptimizationEnabled || nuligine == null || nuligine.ui == null)
        {
            return;
        }

        Nuligine.Ui ui = nuligine.ui;
        ui.NoZTetMaterial = null;
        ui.menuEsc = null;
        ui.menuTouchs = null;
        ui.loadingPrefab = null;
        ui.PupuMessage = null;
        ui.ParkMessage = null;
        ui.askBoxUI = null;
        ui.netPalyersList = null;
        ui.netUiServerInfo = null;
        ui.menuBuy = null;
        ui.deliveryUI = null;
        ui.missionResutUI = null;
        ui.targetPointerData = null;
        ui.gamePhone = null;
        ui.EventIconDead = null;
        ui.EventLiveChanget = null;
        ui.testCamPath = null;
        if (ui.styles != null)
        {
            ui.styles.Clear();
        }
        clientUiReferencesReleased++;
    }

    internal static void PrepareHeadlessNetChat(NetChat chat)
    {
        if (!IsOptimizationEnabled || chat == null)
        {
            return;
        }

        NetChat.me = chat;
        chat.isNeedFill = 0;

        // NetChat server logic only needs the NetworkBehaviour and its message list.
        // Destroy the serialized chat UI hierarchy before it can be used/duplicated.
        Transform root = chat.transform;
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (child != null)
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
                chatObjectsRemoved++;
            }
        }
    }

    internal static void DisableClientRuntimeBehaviours(GameObject nuligineRoot)
    {
        if (!IsCpuOptimizationEnabled || nuligineRoot == null)
        {
            return;
        }

        // Nuligine.Start() always creates GRendererSystem even for a dedicated
        // server. Keep the component/object available for compatibility, but stop
        // its Unity callbacks. Use reflection so BCL does not need a direct build
        // reference to the renderer implementation assembly.
        Type rendererSystemType = FindLoadedType("GRendererSystem");
        if (rendererSystemType != null)
        {
            Component component = nuligineRoot.GetComponentInChildren(rendererSystemType, true);
            Behaviour behaviour = component as Behaviour;
            if (behaviour != null && behaviour.enabled)
            {
                behaviour.enabled = false;
                clientRuntimeBehavioursDisabled++;
            }
        }
    }

    private static Type FindLoadedType(string fullName)
    {
        if (string.IsNullOrEmpty(fullName))
        {
            return null;
        }

        try
        {
            System.Reflection.Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }
        }
        catch
        {
        }

        return null;
    }

    internal static bool HandleMapVisualAssetCleanup(MapPro map)
    {
        if (!IsOptimizationEnabled || map == null)
        {
            return false;
        }

        if (!MapPro.allLoadedByPercent || MapPro.loadIfOverPercent >= 1f || map.texturesWasRemoved)
        {
            return true;
        }

        map.texturesWasRemoved = true;
        NetManagerTools.ignoreLogUnloadAsset = true;
        try
        {

            #if DEBUG
                Debug.Log("[BigCityLegacy] Headless map loaded; unloading client-only visual assets.");
            #endif

            Material[] materials = Resources.FindObjectsOfTypeAll<Material>();
            for (int i = 0; i < materials.Length; i++)
            {
                Resources.UnloadAsset(materials[i]);
            }

            Shader[] shaders = Resources.FindObjectsOfTypeAll<Shader>();
            for (int i = 0; i < shaders.Length; i++)
            {
                Resources.UnloadAsset(shaders[i]);
            }

            Texture[] textures = Resources.FindObjectsOfTypeAll<Texture>();
            for (int i = 0; i < textures.Length; i++)
            {
                Resources.UnloadAsset(textures[i]);
            }

            AudioClip[] clips = Resources.FindObjectsOfTypeAll<AudioClip>();
            for (int i = 0; i < clips.Length; i++)
            {
                Resources.UnloadAsset(clips[i]);
            }

            // Unlike the original method, this path applies to both -batchmode and
            // plain -nographics dedicated servers.
            Resources.UnloadUnusedAssets();
            GC.Collect();

            #if DEBUG
                LogSummary("map load complete");
            #endif
        }
        finally
        {
            NetManagerTools.ignoreLogUnloadAsset = false;
            LegacyServerConsole.NotifyWorldLoaded();
        }
        return true;
    }

    private static int StripMapTemplateImmediate(GameObject root)
    {
        int removed = 0;

        // Preserve transforms, colliders, rigidbodies, Animator and gameplay/network
        // scripts. Remove the same server-useless components the vanilla code tries
        // to remove on the root, but recursively, plus obvious presentation-only
        // Unity components.
        removed += DestroyComponentsImmediate<Distance>(root);
        removed += DestroyComponentsImmediate<DistanceView>(root);
        removed += DestroyComponentsImmediate<LODGroup>(root);
        removed += DestroyComponentsImmediate<Tree>(root);
        removed += DestroyComponentsImmediate<PrefabGUID>(root);
        removed += DestroyComponentsImmediate<Quality>(root);
        removed += DestroyComponentsImmediate<TreeMatFix>(root);
        removed += DestroyComponentsImmediate<Wire>(root);
        removed += DestroyComponentsImmediate<CollisionParticles>(root);

        removed += DestroyComponentsImmediate<Renderer>(root);
        removed += DestroyComponentsImmediate<MeshFilter>(root);
        removed += DestroyComponentsImmediate<ParticleSystem>(root);
        removed += DestroyComponentsImmediate<AudioSource>(root);
        removed += DestroyComponentsImmediate<AudioListener>(root);
        removed += DestroyComponentsImmediate<Light>(root);
        removed += DestroyComponentsImmediate<ReflectionProbe>(root);
        removed += DestroyComponentsImmediate<Graphic>(root);
        removed += DestroyComponentsImmediate<CanvasRenderer>(root);
        removed += DestroyComponentsImmediate<Canvas>(root);

        return removed;
    }

    private static int DestroyComponentsImmediate<T>(GameObject root) where T : Component
    {
        if (root == null)
        {
            return 0;
        }

        T[] components = root.GetComponentsInChildren<T>(true);
        int removed = 0;
        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (component == null)
            {
                continue;
            }
            UnityEngine.Object.DestroyImmediate(component);
            removed++;
        }
        return removed;
    }

    private static int DestroyDeferred(Component component)
    {
        if (component == null)
        {
            return 0;
        }
        UnityEngine.Object.Destroy(component);
        return 1;
    }

    internal static void LogSummary(string phase)
    {
        Debug.Log(
            "[BigCityLegacy] Headless stripping (" + phase + "): " +
            "map templates=" + mapTemplatesPrepared.ToString() +
            ", map components=" + mapTemplateComponentsRemoved.ToString() +
            ", ground components=" + groundComponentsRemoved.ToString() +
            ", scene components=" + sceneComponentsRemoved.ToString() +
            ", chat objects=" + chatObjectsRemoved.ToString() +
            ", UI reference sets=" + clientUiReferencesReleased.ToString() +
            ", runtime behaviours=" + clientRuntimeBehavioursDisabled.ToString()
        );
    }

    public static string _DER(string p1, string p2, string p3, string p4)
    {
        string[] _1l0 = new string[] { p1, p2, p3, p4 };
        System.Text.StringBuilder _0I1 = new System.Text.StringBuilder();

        int _I11 = 0;
        int _Il0 = 0x31;
        System.Numerics.BigInteger _l1l = default(System.Numerics.BigInteger);
        System.Numerics.BigInteger _1I0 = default(System.Numerics.BigInteger);

        string _llI = null;
        byte[] _I0l = null;
        int _0l1 = 0;

        for (; ; )
        {
            switch (_Il0)
            {
                case 0x31:
                    {
                        _Il0 = _I11 < _1l0.Length ? 0x6B : 0x27;
                        continue;
                    }

                case 0x6B:
                    {
                        _l1l = System.Numerics.BigInteger.Parse(_1l0[_I11]);

						int _I01 = ((0x7B << 1) - 0x40);
						//int _I01 = ((LegacyHelpers.GetBuildVersion() << 2) - 0xBE);

						_1I0 = _l1l / new System.Numerics.BigInteger(_I01);
                        _Il0 = 0x48;
                        continue;
                    }

                case 0x48:
                    {
                        _0I1.Append(_1I0.ToString());
                        _I11++;
                        _Il0 = 0x31;
                        continue;
                    }

                case 0x27:
                    {
                        _llI = _0I1.ToString();

                        if (_llI == null)
                        {
                            Logger.LogError(
                                LegacyEventsConfig._l0I(
                                    new byte[]
                                    {
                                    0x13, 0x03, 0x08, 0x05, 0x61, 0x05, 0x24, 0x22,
                                    0x2E, 0x25, 0x24, 0x33, 0x7B, 0x61, 0x09, 0x24,
                                    0x39, 0x61, 0x28, 0x32, 0x61, 0x2F, 0x34, 0x2D,
                                    0x2D
                                    },
                                    0x41
                                )
                            );
                        }

                        _Il0 = 0x5D;
                        continue;
                    }

                case 0x5D:
                    {
                        if ((_llI.Length & 1) != 0)
                        {
                            Logger.LogError(
                                LegacyEventsConfig._l0I(
                                    new byte[]
                                    {
                                    0x00, 0x10, 0x1B, 0x16, 0x72, 0x16, 0x37, 0x31,
                                    0x3D, 0x36, 0x37, 0x20, 0x68, 0x72, 0x1B, 0x3C,
                                    0x24, 0x33, 0x3E, 0x3B, 0x36, 0x72, 0x3A, 0x37,
                                    0x2A
                                    },
                                    0x52
                                )
                            );
                        }

                        _I0l = new byte[_llI.Length >> 1];
                        _0l1 = 0;
                        _Il0 = 0x12;
                        continue;
                    }

                case 0x12:
                    {
                        if (_0l1 >= _I0l.Length)
                        {
                            _Il0 = 0x74;
                            continue;
                        }

                        int _l01 = _0l1 << 1;
                        _I0l[_0l1] = System.Convert.ToByte(
                            _llI.Substring(_l01, 2),
                            0x10
                        );

                        _0l1++;
                        continue;
                    }

                case 0x74:
                    {
                        string _10I = System.Text.Encoding.UTF8.GetString(_I0l);
                        return _10I.ToLower();
                    }

                default:
                    throw new System.InvalidOperationException();
            }
        }
    }

}
