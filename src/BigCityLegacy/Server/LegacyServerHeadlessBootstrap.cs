using System;
using System.Collections;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LegacyServerHeadlessBootstrap : MonoBehaviour
{
    internal static ManualLogSource Logger;
    public static bool IsHeadlessServer
	{
		get
		{
			if (!NetManagerTools.isCommandLineArgHaveServerStr())
			{
				return false;
			}
			if (Application.isBatchMode)
			{
				return true;
			}
			string[] commandLineArgs = Environment.GetCommandLineArgs();
			for (int i = 0; i < commandLineArgs.Length; i++)
			{
				if (commandLineArgs[i] == "-nographics")
				{
					return true;
				}
			}
			return false;
		}
	}

	public static void Install(GameObject host)
	{
		if (!LegacyServerHeadlessBootstrap.IsHeadlessServer || LegacyServerHeadlessBootstrap.installed || host == null)
		{
			return;
		}
		host.GetOrAddComponent<LegacyServerHeadlessBootstrap>();
		LegacyServerHeadlessBootstrap.installed = true;
	}

	private void OnEnable()
	{
		SceneManager.sceneLoaded += this.OnSceneLoaded;
		base.StartCoroutine(this.StripAllLoadedScenesDeferred());
	}

	private void OnDisable()
	{
		SceneManager.sceneLoaded -= this.OnSceneLoaded;
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		base.StartCoroutine(this.StripSceneDeferred(scene));
	}

	private IEnumerator StripAllLoadedScenesDeferred()
	{
		yield return null;
		for (int i = 0; i < SceneManager.sceneCount; i++)
		{
			Scene sceneAt = SceneManager.GetSceneAt(i);
			if (sceneAt.IsValid() && sceneAt.isLoaded)
			{
				this.StripScene(sceneAt);
			}
		}
		yield return Resources.UnloadUnusedAssets();
		GC.Collect();
		yield break;
	}

	private IEnumerator StripSceneDeferred(Scene scene)
	{
		yield return null;
		this.StripScene(scene);
		yield return Resources.UnloadUnusedAssets();
		yield break;
	}

	private void StripScene(Scene scene)
	{
		GameObject[] rootGameObjects = scene.GetRootGameObjects();
		for (int i = 0; i < rootGameObjects.Length; i++)
		{
			this.StripRecursive(rootGameObjects[i].transform);
		}
	}

	private void StripRecursive(Transform tr)
	{
		Camera component = tr.GetComponent<Camera>();
		if (component != null)
		{
			component.enabled = false;
		}
		AudioListener component2 = tr.GetComponent<AudioListener>();
		if (component2 != null)
		{
			global::UnityEngine.Object.Destroy(component2);
		}
		AudioSource component3 = tr.GetComponent<AudioSource>();
		if (component3 != null)
		{
			global::UnityEngine.Object.Destroy(component3);
		}
		Light component4 = tr.GetComponent<Light>();
		if (component4 != null)
		{
			global::UnityEngine.Object.Destroy(component4);
		}
		ReflectionProbe component5 = tr.GetComponent<ReflectionProbe>();
		if (component5 != null)
		{
			global::UnityEngine.Object.Destroy(component5);
		}
		Canvas component6 = tr.GetComponent<Canvas>();
		if (component6 != null)
		{
			component6.enabled = false;
		}
		Graphic[] components = tr.GetComponents<Graphic>();
		for (int i = 0; i < components.Length; i++)
		{
			components[i].enabled = false;
		}
		ParticleSystem component7 = tr.GetComponent<ParticleSystem>();
		if (component7 != null)
		{
			global::UnityEngine.Object.Destroy(component7);
		}
		TrailRenderer component8 = tr.GetComponent<TrailRenderer>();
		if (component8 != null)
		{
			global::UnityEngine.Object.Destroy(component8);
		}
		LineRenderer component9 = tr.GetComponent<LineRenderer>();
		if (component9 != null)
		{
			global::UnityEngine.Object.Destroy(component9);
		}
		Renderer[] components2 = tr.GetComponents<Renderer>();
		for (int j = 0; j < components2.Length; j++)
		{
			global::UnityEngine.Object.Destroy(components2[j]);
		}
		LODGroup component10 = tr.GetComponent<LODGroup>();
		if (component10 != null)
		{
			global::UnityEngine.Object.Destroy(component10);
		}
		for (int k = 0; k < tr.childCount; k++)
		{
			this.StripRecursive(tr.GetChild(k));
		}
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

    private static bool installed;
}
