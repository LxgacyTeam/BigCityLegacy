using System;
using UnityEngine;
using UnityEngine.Rendering;

internal static class LegacyShadowFix
{
    internal const string PlayerPrefsKey = "BigCityLegacy.ShadowFix.Enabled";

    internal static bool IsEnabled
    {
        get { return PlayerPrefs.GetInt(PlayerPrefsKey, 1) != 0; }
    }

    internal static void EnsureDefault()
    {
        if (!PlayerPrefs.HasKey(PlayerPrefsKey))
        {
            PlayerPrefs.SetInt(PlayerPrefsKey, 1);
            PlayerPrefs.Save();
        }
    }

    internal static void SetEnabled(bool enabled)
    {
        bool wasEnabled = IsEnabled;
        PlayerPrefs.SetInt(PlayerPrefsKey, enabled ? 1 : 0);
        PlayerPrefs.Save();

        if (wasEnabled != enabled)
        {
            ApplyToExistingCharacterRenderers(enabled);
        }
    }

    internal static ShadowCastingMode GetShadowCastingMode()
    {
        return IsEnabled ? ShadowCastingMode.On : ShadowCastingMode.Off;
    }

    private static void ApplyToExistingCharacterRenderers(bool enabled)
    {
        ShadowCastingMode mode = enabled ? ShadowCastingMode.On : ShadowCastingMode.Off;

        try
        {
            Anim2Shader[] animShaders = Resources.FindObjectsOfTypeAll<Anim2Shader>();
            for (int i = 0; i < animShaders.Length; i++)
            {
                Anim2Shader anim = animShaders[i];
                if (!anim || !anim.meshRender)
                {
                    continue;
                }

                anim.meshRender.shadowCastingMode = mode;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BigCityLegacy] Failed to apply ShadowFix to Anim2Shader renderers: " + ex.Message);
        }

        try
        {
            HairRenderer[] hairRenderers = Resources.FindObjectsOfTypeAll<HairRenderer>();
            for (int i = 0; i < hairRenderers.Length; i++)
            {
                HairRenderer hair = hairRenderers[i];
                if (!hair)
                {
                    continue;
                }

                Renderer[] renderers = hair.GetComponentsInChildren<Renderer>(true);
                for (int j = 0; j < renderers.Length; j++)
                {
                    Renderer renderer = renderers[j];
                    if (renderer)
                    {
                        renderer.shadowCastingMode = mode;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BigCityLegacy] Failed to apply ShadowFix to HairRenderer children: " + ex.Message);
        }
    }
}
