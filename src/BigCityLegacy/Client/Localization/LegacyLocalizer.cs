using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Localization helper for custom UI and stock game localization patches.
/// </summary>
public static class LegacyLocalizer
{
    private static readonly Dictionary<string, LegacyLocalizedText> Replacements = new Dictionary<string, LegacyLocalizedText>(StringComparer.OrdinalIgnoreCase);


    public static string CurrentLanguageCode
    {
        get { return GetCurrentLanguageCode(); }
    }

    public static bool IsRussianLanguage
    {
        get { return IsRussianLanguageCode(CurrentLanguageCode); }
    }

    public static string Text(string english, string russian)
    {
        return Get(new LegacyLocalizedText(english, russian));
    }

    public static string Get(LegacyLocalizedText text)
    {
        return Get(text, CurrentLanguageCode);
    }

    public static string Get(LegacyLocalizedText text, string languageCode)
    {
        bool russian = IsRussianLanguageCode(languageCode);
        string primary = russian ? text.Russian : text.English;
        string fallback = russian ? text.English : text.Russian;
        if (!string.IsNullOrEmpty(primary))
        {
            return primary;
        }
        return fallback ?? string.Empty;
    }

    public static void RegisterReplacement(string key, string english, string russian)
    {
        RegisterReplacement(key, new LegacyLocalizedText(english, russian));
    }

    public static void RegisterReplacement(string key, string value)
    {
        RegisterReplacement(key, LegacyLocalizedText.Same(value));
    }

    public static void RegisterReplacement(string key, LegacyLocalizedText value)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Localization replacement key is required.", "key");
        }

        Replacements[key] = value;
        ApplyRegisteredReplacements(relocalize: true);
    }

    public static void UnregisterReplacement(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        if (Replacements.Remove(key))
        {
            ApplyRegisteredReplacements(relocalize: true);
        }
    }

    public static void ClearReplacements()
    {
        if (Replacements.Count == 0)
        {
            return;
        }

        Replacements.Clear();
        ApplyRegisteredReplacements(relocalize: true);
    }

    public static void ApplyRegisteredReplacements(bool relocalize = true)
    {
        ApplyToManager(LocalizationManager.me, relocalize);
    }

    internal static void ApplyLocalizationManagerPatch(LocalizationManager manager)
    {
        LegacyDefaultReplacements.Register();
        ApplyToManager(manager, relocalize: true);
    }

    public static void ApplyToManager(LocalizationManager manager, bool relocalize = true)
    {
        if (manager == null || Replacements.Count == 0 || manager.langArr == null)
        {
            return;
        }

        try
        {
            for (int i = 0; i < manager.langArr.Count; i++)
            {
                LocalizationManager.Lang lang = manager.langArr[i];
                if (lang == null)
                {
                    continue;
                }

                foreach (KeyValuePair<string, LegacyLocalizedText> pair in Replacements)
                {
                    string value = Get(pair.Value, lang.lang);
                    ReplaceKeyRecursive(lang.root, pair.Key, value);
                }
            }

            if (relocalize)
            {
                RelocalizeAllLoadedObjects();
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BigCityLegacy] localization replacement failed: " + ex.Message);
        }
    }

    public static void RelocalizeAllLoadedObjects()
    {
        Localize[] array = Resources.FindObjectsOfTypeAll<Localize>();
        for (int i = 0; i < array.Length; i++)
        {
            if (array[i])
            {
                array[i].LocalizeIt();
            }
        }
    }

    public static void SetLocalize(Localize localize, string value)
    {
        if (!localize || value == null)
        {
            return;
        }

        localize.text = value;
        if (localize.ui)
        {
            localize.ui.text = value;
        }
    }

    public static void SetLocalize(Localize localize, LegacyLocalizedText value)
    {
        SetLocalize(localize, Get(value));
    }

    public static void SetText(Text text, LegacyLocalizedText value)
    {
        if (text)
        {
            text.text = Get(value);
        }
    }

    internal static string GetCurrentLanguageCode()
    {
        try
        {
            if (SettingsValues.me != null && SettingsValues.me.user != null && SettingsValues.me.user.language != null)
            {
                string value = SettingsValues.me.user.language.GetValue();
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }
        }
        catch
        {
        }

        try
        {
            if (LocalizationManager.me != null && LocalizationManager.me.curLang != null && !string.IsNullOrEmpty(LocalizationManager.me.curLang.lang))
            {
                return LocalizationManager.me.curLang.lang;
            }
        }
        catch
        {
        }

        return Application.systemLanguage == SystemLanguage.Russian ? "ru" : "en";
    }

    private static bool IsRussianLanguageCode(string languageCode)
    {
        if (string.IsNullOrEmpty(languageCode))
        {
            return Application.systemLanguage == SystemLanguage.Russian;
        }

        string normalized = languageCode.Trim().ToLowerInvariant();
        return normalized == "ru";
    }

    private static void ReplaceKeyRecursive(LocalizationManager.Lang.Group group, string targetKey, string newValue)
    {
        if (group == null)
        {
            return;
        }

        if (group.KeyValue != null && group.KeyValue.ContainsKey(targetKey))
        {
            group.KeyValue[targetKey] = newValue;
        }

        if (group.KeyValArr != null)
        {
            for (int i = 0; i < group.KeyValArr.Length; i++)
            {
                LocalizationManager.Lang.Group.KeyVal pair = group.KeyValArr[i];
                if (pair != null && string.Equals(pair.Key, targetKey, StringComparison.OrdinalIgnoreCase))
                {
                    pair.Value = newValue;
                }
            }
        }

        if (group.childs == null)
        {
            return;
        }

        for (int i = 0; i < group.childs.Length; i++)
        {
            ReplaceKeyRecursive(group.childs[i], targetKey, newValue);
        }
    }
}

public struct LegacyLocalizedText
{
    public string English;
    public string Russian;

    public LegacyLocalizedText(string english, string russian)
    {
        English = english;
        Russian = russian;
    }

    public bool IsEmpty
    {
        get { return string.IsNullOrEmpty(English) && string.IsNullOrEmpty(Russian); }
    }

    public string Get()
    {
        return LegacyLocalizer.Get(this);
    }

    public string Get(string languageCode)
    {
        return LegacyLocalizer.Get(this, languageCode);
    }

    public static LegacyLocalizedText Same(string value)
    {
        return new LegacyLocalizedText(value, value);
    }

    public static implicit operator LegacyLocalizedText(string value)
    {
        return Same(value);
    }

    public override string ToString()
    {
        return Get();
    }
}
