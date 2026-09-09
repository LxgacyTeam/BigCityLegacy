using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Runtime helper for adding BigCityLegacy-owned items to the original game settings menu.
///
/// Available MenuGroups:
/// User, Render, Sound, Keyboard, JoyStick, TODO
/// </summary>
public static class LegacySettingsMenuHelper
{
    private static readonly List<LegacySettingsMenuItem> Items = new List<LegacySettingsMenuItem>();
    private static readonly List<LegacySettingsHiddenStockItem> HiddenStockItems = new List<LegacySettingsHiddenStockItem>();
    private static readonly Dictionary<int, HashSet<string>> CreatedByGroup = new Dictionary<int, HashSet<string>>();
    private static readonly HashSet<string> MissingTemplateWarnings = new HashSet<string>();
    private static bool debugLogging;

    public static bool DebugLogging
    {
        get { return debugLogging; }
        set { debugLogging = value; }
    }

    public static void Register(LegacySettingsMenuItem item)
    {
        if (item == null)
        {
            throw new ArgumentNullException("item");
        }
        item.Validate();

        for (int i = Items.Count - 1; i >= 0; i--)
        {
            if (string.Equals(Items[i].Id, item.Id, StringComparison.OrdinalIgnoreCase))
            {
                Items.RemoveAt(i);
            }
        }

        Items.Add(item);
        CreatedByGroup.Clear();
    }

    public static LegacySettingsMenuItem RegisterButton(string id, string group, Vector2 position, string label, Action action)
    {
        LegacySettingsMenuItem item = new LegacySettingsMenuItem
        {
            Id = id,
            Group = group,
            Type = LegacySettingsMenuItemType.Button,
            Label = label,
            UseAnchoredPosition = true,
            AnchoredPosition = position,
            ButtonAction = action
        };
        Register(item);
        return item;
    }

    public static LegacySettingsMenuItem RegisterButton(string id, string group, Vector2 position, LegacyLocalizedText label, Action action)
    {
        LegacySettingsMenuItem item = RegisterButton(id, group, position, label.Get(), action);
        item.LocalizedLabel = label;
        return item;
    }

    public static LegacySettingsMenuItem RegisterToggle(string id, string group, Vector2 position, string label, LegacyBoolSettingBinding binding)
    {
        LegacySettingsMenuItem item = new LegacySettingsMenuItem
        {
            Id = id,
            Group = group,
            Type = LegacySettingsMenuItemType.Toggle,
            Label = label,
            UseAnchoredPosition = true,
            AnchoredPosition = position,
            BoolBinding = binding
        };
        Register(item);
        return item;
    }

    public static LegacySettingsMenuItem RegisterToggle(string id, string group, Vector2 position, LegacyLocalizedText label, LegacyBoolSettingBinding binding)
    {
        LegacySettingsMenuItem item = RegisterToggle(id, group, position, label.Get(), binding);
        item.LocalizedLabel = label;
        return item;
    }

    public static LegacySettingsMenuItem RegisterSlider(string id, string group, Vector2 position, string label, LegacyFloatSettingBinding binding)
    {
        LegacySettingsMenuItem item = new LegacySettingsMenuItem
        {
            Id = id,
            Group = group,
            Type = LegacySettingsMenuItemType.Slider,
            Label = label,
            UseAnchoredPosition = true,
            AnchoredPosition = position,
            FloatBinding = binding
        };
        Register(item);
        return item;
    }

    public static LegacySettingsMenuItem RegisterSlider(string id, string group, Vector2 position, LegacyLocalizedText label, LegacyFloatSettingBinding binding)
    {
        LegacySettingsMenuItem item = RegisterSlider(id, group, position, label.Get(), binding);
        item.LocalizedLabel = label;
        return item;
    }

    public static LegacySettingsMenuItem RegisterTextChoice(string id, string group, Vector2 position, string label, LegacyTextChoiceSettingBinding binding)
    {
        LegacySettingsMenuItem item = new LegacySettingsMenuItem
        {
            Id = id,
            Group = group,
            Type = LegacySettingsMenuItemType.TextChoice,
            Label = label,
            UseAnchoredPosition = true,
            AnchoredPosition = position,
            TextChoiceBinding = binding
        };
        Register(item);
        return item;
    }

    public static LegacySettingsMenuItem RegisterTextChoice(string id, string group, Vector2 position, LegacyLocalizedText label, LegacyTextChoiceSettingBinding binding)
    {
        LegacySettingsMenuItem item = RegisterTextChoice(id, group, position, label.Get(), binding);
        item.LocalizedLabel = label;
        return item;
    }

    public static string CurrentLanguageCode
    {
        get { return LegacyLocalizer.CurrentLanguageCode; }
    }

    public static bool IsRussianLanguage
    {
        get { return LegacyLocalizer.IsRussianLanguage; }
    }


    public static LegacySettingsHiddenStockItem HideStockItem(string objectName)
    {
        return HideStockItem(null, objectName, LegacySettingsGroupMatchMode.NameContains);
    }

    public static LegacySettingsHiddenStockItem HideStockItem(string group, string objectName)
    {
        return HideStockItem(group, objectName, LegacySettingsGroupMatchMode.NameContains);
    }

    public static LegacySettingsHiddenStockItem HideStockItem(string group, string objectName, LegacySettingsGroupMatchMode groupMatchMode)
    {
        LegacySettingsHiddenStockItem item = new LegacySettingsHiddenStockItem
        {
            Group = group,
            GroupMatchMode = groupMatchMode,
            ObjectName = objectName
        };
        RegisterHiddenStockItem(item);
        return item;
    }

    public static void RegisterHiddenStockItem(LegacySettingsHiddenStockItem item)
    {
        if (item == null)
        {
            throw new ArgumentNullException("item");
        }
        item.Validate();

        for (int i = HiddenStockItems.Count - 1; i >= 0; i--)
        {
            if (HiddenStockItems[i].SameTarget(item))
            {
                HiddenStockItems.RemoveAt(i);
            }
        }

        HiddenStockItems.Add(item);
    }

    public static void UnhideStockItem(string group, string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return;
        }

        for (int i = HiddenStockItems.Count - 1; i >= 0; i--)
        {
            LegacySettingsHiddenStockItem item = HiddenStockItems[i];
            if (string.Equals(item.ObjectName, objectName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.Group ?? string.Empty, group ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                HiddenStockItems.RemoveAt(i);
            }
        }
    }

    public static void ClearHiddenStockItems()
    {
        HiddenStockItems.Clear();
    }

    public static void Unregister(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return;
        }

        for (int i = Items.Count - 1; i >= 0; i--)
        {
            if (string.Equals(Items[i].Id, id, StringComparison.OrdinalIgnoreCase))
            {
                Items.RemoveAt(i);
            }
        }
        CreatedByGroup.Clear();
    }

    public static void ClearRegisteredItems()
    {
        Items.Clear();
        CreatedByGroup.Clear();
    }

    internal static void ApplyToGroup(MenuGroup group)
    {
        if (!group)
        {
            return;
        }

        ApplyHiddenStockItems(group);

        if (Items.Count == 0)
        {
            return;
        }

        int groupId = group.GetInstanceID();
        HashSet<string> created;
        if (!CreatedByGroup.TryGetValue(groupId, out created))
        {
            created = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CreatedByGroup[groupId] = created;
        }

        if (debugLogging)
        {
            Debug.Log("[BigCityLegacy] Settings MenuGroup: " + GetTransformPath(group.transform));
        }

        for (int i = 0; i < Items.Count; i++)
        {
            LegacySettingsMenuItem item = Items[i];
            if (!item.Matches(group) || created.Contains(item.Id))
            {
                continue;
            }

            try
            {
                GameObject go = CreateItemGameObject(group, item);
                if (go)
                {
                    created.Add(item.Id);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BigCityLegacy] Failed to create settings item '" + item.Id + "': " + ex);
            }
        }
    }

    private static GameObject CreateItemGameObject(MenuGroup group, LegacySettingsMenuItem item)
    {
        MenuButton template = FindTemplate(group, item.Type, item.TemplateNameContains);
        if (!template)
        {
            string key = group.GetInstanceID() + ":" + item.Type + ":" + item.Id;
            if (MissingTemplateWarnings.Add(key))
            {
                Debug.LogWarning("[BigCityLegacy] Settings item '" + item.Id + "' skipped: no template for " + item.Type + " in group " + GetTransformPath(group.transform));
            }
            return null;
        }

        Transform templateTransform = template.transform;
        Transform parent = templateTransform.parent ? templateTransform.parent : group.transform;
        GameObject clone = (GameObject)UnityEngine.Object.Instantiate(templateTransform.gameObject);
        clone.name = "BigCityLegacySetting_" + item.Id;
        clone.transform.SetParent(parent, false);
        clone.SetActive(true);

        RectTransform cloneRect = clone.GetComponent<RectTransform>();
        RectTransform templateRect = templateTransform.GetComponent<RectTransform>();
        if (cloneRect && templateRect)
        {
            cloneRect.anchorMin = templateRect.anchorMin;
            cloneRect.anchorMax = templateRect.anchorMax;
            cloneRect.pivot = templateRect.pivot;
            cloneRect.sizeDelta = templateRect.sizeDelta;
            cloneRect.anchoredPosition = templateRect.anchoredPosition;
        }

        if (cloneRect && item.UseAnchoredPosition)
        {
            cloneRect.anchoredPosition = item.AnchoredPosition;
        }
        if (cloneRect && item.Size.x > 0f && item.Size.y > 0f)
        {
            cloneRect.sizeDelta = item.Size;
        }
        if (item.SiblingIndex >= 0)
        {
            clone.transform.SetSiblingIndex(Mathf.Clamp(item.SiblingIndex, 0, parent.childCount - 1));
        }

        if (item.IgnoreUnityLayout)
        {
            LayoutElement layoutElement = clone.GetComponent<LayoutElement>();
            if (!layoutElement)
            {
                layoutElement = clone.AddComponent<LayoutElement>();
            }
            layoutElement.ignoreLayout = true;
        }

        MenuButton menuButton = clone.GetComponent<MenuButton>();
        if (menuButton)
        {
            menuButton.isStaickPos = item.IgnoreUnityLayout || item.UseAnchoredPosition;
            menuButton.isSkipSaves = true;
        }

        LegacySettingsMenuRuntimeItem runtime = clone.GetComponent<LegacySettingsMenuRuntimeItem>();
        if (!runtime)
        {
            runtime = clone.AddComponent<LegacySettingsMenuRuntimeItem>();
        }
        runtime.Init(item, group);

        // MenuGroup scans its children only in Awake. Custom clones are created later, so register
        // them explicitly to keep stock hover/selected/focus behaviour.
        if (menuButton)
        {
            menuButton.InitParent();
        }

        item.OnCreated?.Invoke(clone);
        return clone;
    }

    private static MenuButton FindTemplate(MenuGroup group, LegacySettingsMenuItemType type, string templateNameContains)
    {
        MenuButton[] buttons = group.GetComponentsInChildren<MenuButton>(true);

        if (!string.IsNullOrEmpty(templateNameContains))
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                MenuButton button = buttons[i];
                if (IsValidTemplate(button) &&
                    (IndexOf(button.gameObject.name, templateNameContains) >= 0 || IndexOf(button.GetType().Name, templateNameContains) >= 0))
                {
                    return button;
                }
            }
        }

        string typeName;
        switch (type)
        {
            case LegacySettingsMenuItemType.Button:
                typeName = "MenuButtonEvent";
                break;
            case LegacySettingsMenuItemType.Toggle:
                typeName = "MenuSettingBool";
                break;
            case LegacySettingsMenuItemType.Slider:
                typeName = "MenuSettingSlider";
                break;
            case LegacySettingsMenuItemType.TextChoice:
                typeName = "MenuSettingText";
                break;
            default:
                typeName = null;
                break;
        }

        if (string.IsNullOrEmpty(typeName))
        {
            return null;
        }

        for (int i = 0; i < buttons.Length; i++)
        {
            MenuButton button = buttons[i];
            if (IsValidTemplate(button) && string.Equals(button.GetType().Name, typeName, StringComparison.Ordinal))
            {
                return button;
            }
        }
        return null;
    }

    private static bool IsValidTemplate(MenuButton button)
    {
        return button && !button.GetComponent<LegacySettingsMenuRuntimeItem>() &&
               IndexOf(button.gameObject.name, "BigCityLegacySetting_") < 0;
    }


    private static void ApplyHiddenStockItems(MenuGroup group)
    {
        if (!group || HiddenStockItems.Count == 0)
        {
            return;
        }

        for (int i = 0; i < HiddenStockItems.Count; i++)
        {
            LegacySettingsHiddenStockItem item = HiddenStockItems[i];
            if (item.Matches(group))
            {
                HideStockItemInGroup(group, item);
            }
        }
    }

    private static void HideStockItemInGroup(MenuGroup group, LegacySettingsHiddenStockItem item)
    {
        Transform[] transforms = group.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform transform = transforms[i];
            if (!transform || transform == group.transform)
            {
                continue;
            }

            GameObject go = transform.gameObject;
            if (!item.MatchesObject(go.name))
            {
                continue;
            }

            if (go.GetComponent<LegacySettingsMenuRuntimeItem>())
            {
                continue;
            }

            bool wasActiveSelf = go.activeSelf;
            RemoveButtonsFromGroup(group, go);
            if (wasActiveSelf)
            {
                go.SetActive(false);
            }

            if (debugLogging && wasActiveSelf)
            {
                Debug.Log("[BigCityLegacy] Hidden stock settings item: " + GetTransformPath(transform));
            }
        }
    }

    private static void RemoveButtonsFromGroup(MenuGroup group, GameObject root)
    {
        if (!group || !root)
        {
            return;
        }

        MenuButton[] buttons = root.GetComponentsInChildren<MenuButton>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            MenuButton button = buttons[i];
            if (!button)
            {
                continue;
            }

            for (int j = group.buttons.Count - 1; j >= 0; j--)
            {
                if (group.buttons[j] == button)
                {
                    group.buttons.RemoveAt(j);
                }
            }

            MenuFocusManager focus = button.focusManger;
            if (focus && focus.curActive == button)
            {
                focus.curActive = null;
            }
        }
    }

    internal static bool GroupMatches(MenuGroup group, string expectedGroup, LegacySettingsGroupMatchMode matchMode)
    {
        if (!group)
        {
            return false;
        }
        if (string.IsNullOrEmpty(expectedGroup))
        {
            return true;
        }

        string name = group.gameObject.name ?? string.Empty;
        string alias = NormalizeGroupName(name);
        string expected = NormalizeGroupName(expectedGroup);
        string path = GetTransformPath(group.transform);
        string normalizedPath = NormalizeGroupPath(path);

        switch (matchMode)
        {
            case LegacySettingsGroupMatchMode.NameEquals:
                return string.Equals(name, expectedGroup, StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(alias, expected, StringComparison.OrdinalIgnoreCase);
            case LegacySettingsGroupMatchMode.PathContains:
                return IndexOf(path, expectedGroup) >= 0 || IndexOf(normalizedPath, expected) >= 0;
            case LegacySettingsGroupMatchMode.PathEquals:
                return string.Equals(path, expectedGroup, StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(normalizedPath, expected, StringComparison.OrdinalIgnoreCase);
            default:
                return IndexOf(name, expectedGroup) >= 0 ||
                       IndexOf(alias, expected) >= 0 ||
                       IndexOf(path, expectedGroup) >= 0 ||
                       IndexOf(normalizedPath, expected) >= 0;
        }
    }

    internal static string NormalizeGroupName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return string.Empty;
        }
        if (string.Equals(name, "JoyGroup", StringComparison.OrdinalIgnoreCase))
        {
            return "JoyStick";
        }
        return name;
    }

    private static string NormalizeGroupPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }
        return path.Replace("JoyGroup", "JoyStick");
    }

    internal static void SetLocalize(Localize localize, string value)
    {
        LegacyLocalizer.SetLocalize(localize, value);
    }

    internal static void SetText(Text text, string value)
    {
        if (text && value != null)
        {
            text.text = value;
        }
    }

    internal static void SetTexts(GameObject root, string label, string value, string tooltip)
    {
        Text[] texts = root.GetComponentsInChildren<Text>(true);
        if (texts.Length > 0 && label != null)
        {
            texts[0].text = label;
        }
        if (texts.Length > 1 && value != null)
        {
            texts[texts.Length - 1].text = value;
        }
        if (texts.Length > 2 && tooltip != null)
        {
            texts[1].text = tooltip;
        }
    }

    internal static string GetTransformPath(Transform transform)
    {
        if (!transform)
        {
            return string.Empty;
        }

        string path = transform.name;
        Transform current = transform.parent;
        while (current)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }
        return path;
    }

    internal static int IndexOf(string text, string value)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(value))
        {
            return -1;
        }
        return text.IndexOf(value, StringComparison.OrdinalIgnoreCase);
    }
}

public enum LegacySettingsMenuItemType
{
    Button,
    Toggle,
    Slider,
    TextChoice
}

public enum LegacySettingsGroupMatchMode
{
    NameContains,
    NameEquals,
    PathContains,
    PathEquals
}

public sealed class LegacySettingsMenuItem
{
    public string Id;
    public string Group;
    public LegacySettingsGroupMatchMode GroupMatchMode = LegacySettingsGroupMatchMode.NameContains;
    public LegacySettingsMenuItemType Type;
    public string Label;
    public LegacyLocalizedText LocalizedLabel;
    public string Tooltip;
    public string TemplateNameContains;
    public Vector2 AnchoredPosition;
    public bool UseAnchoredPosition;
    public Vector2 Size;
    public int SiblingIndex = -1;
    public bool IgnoreUnityLayout = true;
    public Action ButtonAction;
    public LegacyBoolSettingBinding BoolBinding;
    public LegacyFloatSettingBinding FloatBinding;
    public LegacyTextChoiceSettingBinding TextChoiceBinding;
    public Action<GameObject> OnCreated;

    public void Validate()
    {
        if (string.IsNullOrEmpty(Id))
        {
            throw new InvalidOperationException("Settings menu item requires Id.");
        }
        if (string.IsNullOrEmpty(Group))
        {
            throw new InvalidOperationException("Settings menu item '" + Id + "' requires Group.");
        }
        if (string.IsNullOrEmpty(Label))
        {
            Label = Id;
        }
        if (Type == LegacySettingsMenuItemType.Toggle && BoolBinding == null)
        {
            throw new InvalidOperationException("Toggle settings menu item '" + Id + "' requires BoolBinding.");
        }
        if (Type == LegacySettingsMenuItemType.Slider && FloatBinding == null)
        {
            throw new InvalidOperationException("Slider settings menu item '" + Id + "' requires FloatBinding.");
        }
        if (Type == LegacySettingsMenuItemType.TextChoice && TextChoiceBinding == null)
        {
            throw new InvalidOperationException("TextChoice settings menu item '" + Id + "' requires TextChoiceBinding.");
        }
    }

    internal bool Matches(MenuGroup group)
    {
        return LegacySettingsMenuHelper.GroupMatches(group, Group, GroupMatchMode);
    }

    internal string GetLabel()
    {
        return LocalizedLabel.IsEmpty ? Label : LocalizedLabel.Get();
    }
}

public enum LegacySettingsStockItemNameMatchMode
{
    NameEquals,
    NameContains
}

public sealed class LegacySettingsHiddenStockItem
{
    public string Group;
    public LegacySettingsGroupMatchMode GroupMatchMode = LegacySettingsGroupMatchMode.NameContains;
    public string ObjectName;
    public LegacySettingsStockItemNameMatchMode NameMatchMode = LegacySettingsStockItemNameMatchMode.NameEquals;

    public void Validate()
    {
        if (string.IsNullOrEmpty(ObjectName))
        {
            throw new InvalidOperationException("Hidden stock settings item requires ObjectName.");
        }
    }

    internal bool Matches(MenuGroup group)
    {
        return LegacySettingsMenuHelper.GroupMatches(group, Group, GroupMatchMode);
    }

    internal bool MatchesObject(string objectName)
    {
        if (NameMatchMode == LegacySettingsStockItemNameMatchMode.NameContains)
        {
            return LegacySettingsMenuHelper.IndexOf(objectName, ObjectName) >= 0;
        }
        return string.Equals(objectName, ObjectName, StringComparison.OrdinalIgnoreCase);
    }

    internal bool SameTarget(LegacySettingsHiddenStockItem other)
    {
        return other != null &&
               string.Equals(Group ?? string.Empty, other.Group ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
               GroupMatchMode == other.GroupMatchMode &&
               string.Equals(ObjectName, other.ObjectName, StringComparison.OrdinalIgnoreCase) &&
               NameMatchMode == other.NameMatchMode;
    }
}

public sealed class LegacyBoolSettingBinding
{
    public Func<bool> Get;
    public Action<bool> Set;
    public Action<bool> OnChanged;
    public string OnText;
    public string OffText;
    public LegacyLocalizedText OnLocalizedText = new LegacyLocalizedText("On", "Да");
    public LegacyLocalizedText OffLocalizedText = new LegacyLocalizedText("Off", "Выкл");

    public static LegacyBoolSettingBinding PlayerPrefs(string key, bool defaultValue, Action<bool> onChanged = null)
    {
        return new LegacyBoolSettingBinding
        {
            Get = () => UnityEngine.PlayerPrefs.GetInt(key, defaultValue ? 1 : 0) != 0,
            Set = value =>
            {
                UnityEngine.PlayerPrefs.SetInt(key, value ? 1 : 0);
                UnityEngine.PlayerPrefs.Save();
            },
            OnChanged = onChanged
        };
    }

    public bool Read()
    {
        return Get != null && Get();
    }

    public void Write(bool value)
    {
        Set?.Invoke(value);
        OnChanged?.Invoke(value);
    }

    public string Format(bool value)
    {
        if (value)
        {
            return !string.IsNullOrEmpty(OnText) ? OnText : OnLocalizedText.Get();
        }
        return !string.IsNullOrEmpty(OffText) ? OffText : OffLocalizedText.Get();
    }
}

public sealed class LegacyFloatSettingBinding
{
    public Func<float> Get;
    public Action<float> Set;
    public Action<float> OnChanged;
    public float Min;
    public float Max = 1f;
    public bool WholeNumbers;
    public string Format = "0.##";

    /// <summary>
    /// Internal resolution used by the stock MenuSettingSlider clone. 100 is enough for most
    /// percentage-like settings; integer whole-number ranges can override it.
    /// </summary>
    public int SliderSteps = 100;

    public static LegacyFloatSettingBinding PlayerPrefs(string key, float defaultValue, float min, float max, bool wholeNumbers = false, string format = "0.##", Action<float> onChanged = null)
    {
        return new LegacyFloatSettingBinding
        {
            Min = min,
            Max = max,
            WholeNumbers = wholeNumbers,
            Format = string.IsNullOrEmpty(format) ? "0.##" : format,
            Get = () => UnityEngine.PlayerPrefs.GetFloat(key, defaultValue),
            Set = value =>
            {
                UnityEngine.PlayerPrefs.SetFloat(key, value);
                UnityEngine.PlayerPrefs.Save();
            },
            OnChanged = onChanged
        };
    }

    public float Read()
    {
        float value = Get != null ? Get() : Min;
        return Mathf.Clamp(value, Min, Max);
    }

    public void Write(float value)
    {
        value = Mathf.Clamp(value, Min, Max);
        if (WholeNumbers)
        {
            value = Mathf.Round(value);
        }
        Set?.Invoke(value);
        OnChanged?.Invoke(value);
    }

    public string FormatValue(float value)
    {
        if (WholeNumbers)
        {
            return Mathf.RoundToInt(value).ToString();
        }
        return value.ToString(Format, System.Globalization.CultureInfo.InvariantCulture);
    }
}

public sealed class LegacyTextChoiceSettingBinding
{
    public Func<string> Get;
    public Action<string> Set;
    public Action<string> OnChanged;
    public Func<int> GetIndex;
    public Action<int> SetIndex;
    public Action<int> OnIndexChanged;

    /// <summary>
    /// Stable/canonical values. These are stored by string-based PlayerPrefs bindings.
    /// For localized choices they normally stay English or key-like, while the UI uses LocalizedValues.
    /// </summary>
    public string[] Values;
    public LegacyLocalizedText[] LocalizedValues;

    /// <summary>
    /// Stores the selected value itself in PlayerPrefs as a string.
    /// </summary>
    public static LegacyTextChoiceSettingBinding PlayerPrefs(string key, string defaultValue, string[] values, Action<string> onChanged = null)
    {
        return new LegacyTextChoiceSettingBinding
        {
            Values = NormalizeValues(values),
            Get = () => UnityEngine.PlayerPrefs.GetString(key, defaultValue),
            Set = value =>
            {
                UnityEngine.PlayerPrefs.SetString(key, value ?? string.Empty);
                UnityEngine.PlayerPrefs.Save();
            },
            OnChanged = onChanged
        };
    }

    /// <summary>
    /// Stores the selected canonical value in PlayerPrefs as a string, while drawing localized labels in UI.
    /// </summary>
    public static LegacyTextChoiceSettingBinding PlayerPrefs(string key, string defaultValue, LegacyLocalizedText[] values, Action<string> onChanged = null)
    {
        string[] storageValues = BuildStorageValues(values);
        return new LegacyTextChoiceSettingBinding
        {
            Values = storageValues,
            LocalizedValues = NormalizeLocalizedValues(values),
            Get = () => UnityEngine.PlayerPrefs.GetString(key, defaultValue),
            Set = value =>
            {
                UnityEngine.PlayerPrefs.SetString(key, value ?? string.Empty);
                UnityEngine.PlayerPrefs.Save();
            },
            OnChanged = onChanged
        };
    }

    /// <summary>
    /// Stores the selected value index in PlayerPrefs as an int.
    /// The UI still works with the string value required by MenuSettingText.
    /// </summary>
    public static LegacyTextChoiceSettingBinding PlayerPrefs(string key, int defaultIndex, string[] values, Action<int> onChanged = null)
    {
        return PlayerPrefsIndex(key, defaultIndex, values, onChanged);
    }

    /// <summary>
    /// Stores the selected value index in PlayerPrefs as an int and draws localized labels in UI.
    /// </summary>
    public static LegacyTextChoiceSettingBinding PlayerPrefs(string key, int defaultIndex, LegacyLocalizedText[] values, Action<int> onChanged = null)
    {
        return PlayerPrefsIndex(key, defaultIndex, values, onChanged);
    }

    /// <summary>
    /// Stores the selected value index in PlayerPrefs as an int.
    /// Use this overload when the setting should be saved as 0..Values.Length-1.
    /// </summary>
    public static LegacyTextChoiceSettingBinding PlayerPrefsIndex(string key, int defaultIndex, string[] values, Action<int> onChanged = null)
    {
        string[] normalizedValues = NormalizeValues(values);
        int safeDefaultIndex = ClampIndex(defaultIndex, normalizedValues);

        return new LegacyTextChoiceSettingBinding
        {
            Values = normalizedValues,
            GetIndex = () => UnityEngine.PlayerPrefs.GetInt(key, safeDefaultIndex),
            SetIndex = index =>
            {
                UnityEngine.PlayerPrefs.SetInt(key, ClampIndex(index, normalizedValues));
                UnityEngine.PlayerPrefs.Save();
            },
            OnIndexChanged = onChanged
        };
    }

    /// <summary>
    /// Stores the selected value index in PlayerPrefs as an int and draws localized labels in UI.
    /// </summary>
    public static LegacyTextChoiceSettingBinding PlayerPrefsIndex(string key, int defaultIndex, LegacyLocalizedText[] values, Action<int> onChanged = null)
    {
        LegacyLocalizedText[] localized = NormalizeLocalizedValues(values);
        string[] storageValues = BuildStorageValues(localized);
        int safeDefaultIndex = ClampIndex(defaultIndex, storageValues);

        return new LegacyTextChoiceSettingBinding
        {
            Values = storageValues,
            LocalizedValues = localized,
            GetIndex = () => UnityEngine.PlayerPrefs.GetInt(key, safeDefaultIndex),
            SetIndex = index =>
            {
                UnityEngine.PlayerPrefs.SetInt(key, ClampIndex(index, storageValues));
                UnityEngine.PlayerPrefs.Save();
            },
            OnIndexChanged = onChanged
        };
    }

    public string[] GetDisplayValues()
    {
        if (LocalizedValues != null && LocalizedValues.Length > 0)
        {
            string[] result = new string[LocalizedValues.Length];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = LocalizedValues[i].Get();
            }
            return result;
        }
        return NormalizeValues(Values);
    }

    public string Read()
    {
        string[] displayValues = GetDisplayValues();
        if (displayValues.Length == 0)
        {
            return Get != null ? (Get() ?? string.Empty) : string.Empty;
        }
        return displayValues[ReadIndex()];
    }

    public int ReadIndex()
    {
        string[] displayValues = GetDisplayValues();
        string[] storageValues = NormalizeValues(Values);
        int length = Mathf.Max(displayValues.Length, storageValues.Length);
        if (length == 0)
        {
            return 0;
        }

        if (GetIndex != null)
        {
            return Mathf.Clamp(GetIndex(), 0, length - 1);
        }

        string value = Get != null ? Get() : null;
        int index = IndexOfValue(storageValues, value);
        if (index >= 0)
        {
            return index;
        }
        index = IndexOfValue(displayValues, value);
        return index >= 0 ? index : 0;
    }

    public void Write(string value)
    {
        int index = IndexOfValue(GetDisplayValues(), value);
        if (index < 0)
        {
            index = IndexOfValue(Values, value);
        }

        if (index >= 0)
        {
            WriteIndex(index);
            return;
        }

        Set?.Invoke(value);
        OnChanged?.Invoke(value);
    }

    public void WriteIndex(int index)
    {
        string[] displayValues = GetDisplayValues();
        string[] storageValues = NormalizeValues(Values);
        int length = Mathf.Max(displayValues.Length, storageValues.Length);
        if (length == 0)
        {
            Set?.Invoke(string.Empty);
            OnChanged?.Invoke(string.Empty);
            return;
        }

        index = Mathf.Clamp(index, 0, length - 1);

        if (SetIndex != null)
        {
            SetIndex(index);
            OnIndexChanged?.Invoke(index);
            return;
        }

        string storage = index < storageValues.Length ? storageValues[index] : (index < displayValues.Length ? displayValues[index] : string.Empty);
        Set?.Invoke(storage);
        OnChanged?.Invoke(storage);
    }

    private static string[] NormalizeValues(string[] values)
    {
        if (values == null)
        {
            return new string[0];
        }

        string[] normalized = new string[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            normalized[i] = values[i] ?? string.Empty;
        }
        return normalized;
    }

    private static LegacyLocalizedText[] NormalizeLocalizedValues(LegacyLocalizedText[] values)
    {
        if (values == null)
        {
            return new LegacyLocalizedText[0];
        }

        LegacyLocalizedText[] normalized = new LegacyLocalizedText[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            normalized[i] = values[i];
        }
        return normalized;
    }

    private static string[] BuildStorageValues(LegacyLocalizedText[] values)
    {
        LegacyLocalizedText[] normalized = NormalizeLocalizedValues(values);
        string[] result = new string[normalized.Length];
        for (int i = 0; i < normalized.Length; i++)
        {
            result[i] = !string.IsNullOrEmpty(normalized[i].English) ? normalized[i].English : normalized[i].Russian ?? string.Empty;
        }
        return result;
    }

    private static int ClampIndex(int index, string[] values)
    {
        if (values == null || values.Length == 0)
        {
            return 0;
        }
        return Mathf.Clamp(index, 0, values.Length - 1);
    }

    private static int IndexOfValue(string[] values, string value)
    {
        if (values == null || values.Length == 0)
        {
            return -1;
        }

        for (int i = 0; i < values.Length; i++)
        {
            if (string.Equals(values[i], value, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return -1;
    }
}

internal sealed class LegacySettingsMenuRuntimeItem : MonoBehaviour
{
    private LegacySettingsMenuItem item;
    private MenuSettingBool boolItem;
    private MenuSettingSlider sliderItem;
    private MenuSettingText textItem;
    private MenuButtonEvent buttonItem;
    private RectTransform sliderRootRect;
    private Canvas canvas;

    private bool lastBool;
    private int lastSliderRaw;
    private string lastText;
    private int lastTextIndex;
    private string lastLanguage;
    private bool draggingSlider;
    private bool initialized;

    internal void Init(LegacySettingsMenuItem item, MenuGroup group)
    {
        this.item = item;
        canvas = GetComponentInParent<Canvas>();

        boolItem = GetComponent<MenuSettingBool>();
        sliderItem = GetComponent<MenuSettingSlider>();
        textItem = GetComponent<MenuSettingText>();
        buttonItem = GetComponent<MenuButtonEvent>();

        Configure();
        lastLanguage = LegacySettingsMenuHelper.CurrentLanguageCode;
        initialized = true;
    }

    private void LateUpdate()
    {
        if (!initialized || item == null)
        {
            return;
        }

        RefreshLocalizationIfNeeded();

        switch (item.Type)
        {
            case LegacySettingsMenuItemType.Toggle:
                SyncToggle();
                break;
            case LegacySettingsMenuItemType.Slider:
                UpdateSliderDrag();
                SyncSlider();
                break;
            case LegacySettingsMenuItemType.TextChoice:
                SyncTextChoice();
                break;
        }
    }

    private void Configure()
    {
        if (buttonItem)
        {
            buttonItem.isSkipSaves = true;
        }
        if (boolItem)
        {
            boolItem.isSkipSaves = true;
        }
        if (sliderItem)
        {
            sliderItem.isSkipSaves = true;
        }
        if (textItem)
        {
            textItem.isSkipSaves = true;
        }

        switch (item.Type)
        {
            case LegacySettingsMenuItemType.Button:
                ConfigureButton();
                break;
            case LegacySettingsMenuItemType.Toggle:
                ConfigureToggle();
                break;
            case LegacySettingsMenuItemType.Slider:
                ConfigureSlider();
                break;
            case LegacySettingsMenuItemType.TextChoice:
                ConfigureTextChoice();
                break;
        }
    }

    private void ConfigureButton()
    {
        if (!buttonItem)
        {
            LegacySettingsMenuHelper.SetTexts(gameObject, item.GetLabel(), null, item.Tooltip);
            return;
        }

        LegacySettingsMenuHelper.SetLocalize(buttonItem.text, item.GetLabel());
        buttonItem.OnClickEvent = new UnityEvent();
        buttonItem.OnClickEvent.AddListener(InvokeButtonAction);
        buttonItem.CallBackEvent = null;
    }

    private void ConfigureToggle()
    {
        if (!boolItem)
        {
            return;
        }

        LegacySettingsMenuHelper.SetLocalize(boolItem.text, item.GetLabel());
        bool value = item.BoolBinding.Read();
        boolItem.SetBool(value);
        lastBool = boolItem.GetBool();
        SetToggleValueText(lastBool);
    }

    private void ConfigureSlider()
    {
        if (!sliderItem)
        {
            return;
        }

        LegacySettingsMenuHelper.SetText(sliderItem.text, item.GetLabel());
        sliderItem.min = 0;
        sliderItem.max = GetSliderSteps();
        sliderItem.isPrecents = false;

        Transform sliderRoot = transform.Find("SliderRoot");
        if (sliderRoot)
        {
            sliderRootRect = sliderRoot.GetComponent<RectTransform>();
        }

        lastSliderRaw = ValueToSliderRaw(item.FloatBinding.Read());
        sliderItem.SetValue(lastSliderRaw.ToString());
        SetSliderValueText(item.FloatBinding.Read());
    }

    private void ConfigureTextChoice()
    {
        if (!textItem)
        {
            return;
        }

        LegacySettingsMenuHelper.SetLocalize(textItem.text, item.GetLabel());
        textItem.ChangeEvent = new UnityEvent();
        textItem.ignoreValue = string.Empty;
        RefreshTextChoiceValuesFromBinding();
    }

    private void InvokeButtonAction()
    {
        try
        {
            item.ButtonAction?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BigCityLegacy] Settings button '" + item.Id + "' failed: " + ex.Message);
        }
    }

    private void SyncToggle()
    {
        if (!boolItem || item.BoolBinding == null)
        {
            return;
        }

        bool current = boolItem.GetBool();
        if (current != lastBool)
        {
            lastBool = current;
            item.BoolBinding.Write(current);
            SetToggleValueText(current);
            return;
        }

        bool desired = item.BoolBinding.Read();
        if (desired != current)
        {
            boolItem.SetBool(desired);
            lastBool = boolItem.GetBool();
            SetToggleValueText(lastBool);
        }
    }

    private void UpdateSliderDrag()
    {
        if (!sliderItem || !sliderRootRect)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0) && IsMouseInside(sliderRootRect))
        {
            draggingSlider = true;
            ApplySliderFromMouse();
        }
        else if (draggingSlider && Input.GetMouseButton(0))
        {
            ApplySliderFromMouse();
        }

        if (Input.GetMouseButtonUp(0))
        {
            draggingSlider = false;
        }
    }

    private void SyncSlider()
    {
        if (!sliderItem || item.FloatBinding == null)
        {
            return;
        }

        int currentRaw = ParseInt(sliderItem.GetValue(), lastSliderRaw);
        if (currentRaw != lastSliderRaw)
        {
            lastSliderRaw = Mathf.Clamp(currentRaw, sliderItem.min, sliderItem.max);
            float value = SliderRawToValue(lastSliderRaw);
            item.FloatBinding.Write(value);
            SetSliderValueText(item.FloatBinding.Read());
            return;
        }

        int desiredRaw = ValueToSliderRaw(item.FloatBinding.Read());
        if (desiredRaw != currentRaw)
        {
            SetSliderRaw(desiredRaw, false);
        }
        else
        {
            SetSliderValueText(item.FloatBinding.Read());
        }
    }

    private void ApplySliderFromMouse()
    {
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(sliderRootRect, Input.mousePosition, GetEventCamera(), out local))
        {
            return;
        }

        Rect rect = sliderRootRect.rect;
        float t = rect.width <= 0.0001f ? 0f : Mathf.InverseLerp(rect.xMin, rect.xMax, local.x);
        int raw = Mathf.RoundToInt(Mathf.Lerp(sliderItem.min, sliderItem.max, Mathf.Clamp01(t)));
        SetSliderRaw(raw, true);
    }

    private void SetSliderRaw(int raw, bool writeBinding)
    {
        if (!sliderItem || item.FloatBinding == null)
        {
            return;
        }

        raw = Mathf.Clamp(raw, sliderItem.min, sliderItem.max);
        sliderItem.SetValue(raw.ToString());
        lastSliderRaw = raw;

        float value = SliderRawToValue(raw);
        if (writeBinding)
        {
            item.FloatBinding.Write(value);
            value = item.FloatBinding.Read();
        }
        SetSliderValueText(value);
    }

    private void SyncTextChoice()
    {
        if (!textItem || item.TextChoiceBinding == null || textItem.values == null || textItem.values.Length == 0)
        {
            return;
        }

        int currentIndex = GetTextChoiceCurrentIndex();
        if (currentIndex != lastTextIndex)
        {
            lastTextIndex = currentIndex;
            lastText = textItem.GetValue();
            item.TextChoiceBinding.WriteIndex(currentIndex);
            SetTextChoiceValueText(lastText);
            return;
        }

        int desiredIndex = item.TextChoiceBinding.ReadIndex();
        if (desiredIndex != currentIndex)
        {
            SetTextChoiceIndex(desiredIndex);
        }
    }

    private void RefreshLocalizationIfNeeded()
    {
        string currentLanguage = LegacySettingsMenuHelper.CurrentLanguageCode;
        if (string.Equals(currentLanguage, lastLanguage, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        lastLanguage = currentLanguage;

        if (buttonItem)
        {
            LegacySettingsMenuHelper.SetLocalize(buttonItem.text, item.GetLabel());
        }
        if (boolItem)
        {
            LegacySettingsMenuHelper.SetLocalize(boolItem.text, item.GetLabel());
            SetToggleValueText(lastBool);
        }
        if (sliderItem)
        {
            LegacySettingsMenuHelper.SetText(sliderItem.text, item.GetLabel());
        }
        if (textItem)
        {
            LegacySettingsMenuHelper.SetLocalize(textItem.text, item.GetLabel());
            RefreshTextChoiceValuesFromBinding();
        }
    }

    private void RefreshTextChoiceValuesFromBinding()
    {
        if (!textItem || item.TextChoiceBinding == null)
        {
            return;
        }

        textItem.values = item.TextChoiceBinding.GetDisplayValues();
        if (textItem.values == null || textItem.values.Length == 0)
        {
            lastText = string.Empty;
            lastTextIndex = 0;
            SetTextChoiceValueText(string.Empty);
            return;
        }

        SetTextChoiceIndex(item.TextChoiceBinding.ReadIndex());
    }

    private int GetTextChoiceCurrentIndex()
    {
        if (!textItem || textItem.values == null || textItem.values.Length == 0)
        {
            return 0;
        }

        int index = textItem.GetCurIndex();
        if (index >= 0 && index < textItem.values.Length)
        {
            return index;
        }

        string current = textItem.GetValue();
        for (int i = 0; i < textItem.values.Length; i++)
        {
            if (string.Equals(textItem.values[i], current, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return 0;
    }

    private void SetTextChoiceIndex(int index)
    {
        if (!textItem || textItem.values == null || textItem.values.Length == 0)
        {
            return;
        }

        index = Mathf.Clamp(index, 0, textItem.values.Length - 1);
        string value = textItem.values[index];
        textItem.SetValue(value);
        lastTextIndex = GetTextChoiceCurrentIndex();
        lastText = textItem.GetValue();
        SetTextChoiceValueText(lastText);
    }

    private int GetSliderSteps()
    {
        int steps = item.FloatBinding != null ? item.FloatBinding.SliderSteps : 100;
        if (steps <= 0)
        {
            steps = 100;
        }
        if (item.FloatBinding != null && item.FloatBinding.WholeNumbers)
        {
            float range = item.FloatBinding.Max - item.FloatBinding.Min;
            if (range > 0f && range <= 1000f)
            {
                steps = Mathf.Max(1, Mathf.RoundToInt(range));
            }
        }
        return Mathf.Max(1, steps);
    }

    private int ValueToSliderRaw(float value)
    {
        if (item.FloatBinding == null || Mathf.Abs(item.FloatBinding.Max - item.FloatBinding.Min) < 0.0001f)
        {
            return 0;
        }

        float t = Mathf.InverseLerp(item.FloatBinding.Min, item.FloatBinding.Max, value);
        return Mathf.Clamp(Mathf.RoundToInt(t * GetSliderSteps()), 0, GetSliderSteps());
    }

    private float SliderRawToValue(int raw)
    {
        if (item.FloatBinding == null)
        {
            return raw;
        }

        float t = GetSliderSteps() <= 0 ? 0f : Mathf.Clamp01((float)raw / (float)GetSliderSteps());
        float value = Mathf.Lerp(item.FloatBinding.Min, item.FloatBinding.Max, t);
        if (item.FloatBinding.WholeNumbers)
        {
            value = Mathf.Round(value);
        }
        return Mathf.Clamp(value, item.FloatBinding.Min, item.FloatBinding.Max);
    }

    private void SetToggleValueText(bool value)
    {
        if (boolItem && boolItem.value)
        {
            LegacySettingsMenuHelper.SetLocalize(boolItem.value, item.BoolBinding.Format(value));
        }
    }

    private void SetSliderValueText(float value)
    {
        if (sliderItem && sliderItem.value)
        {
            sliderItem.value.text = item.FloatBinding.FormatValue(value);
        }
    }

    private void SetTextChoiceValueText(string value)
    {
        if (textItem && textItem.value)
        {
            LegacySettingsMenuHelper.SetLocalize(textItem.value, value);
        }
    }

    private bool IsMouseInside(RectTransform target)
    {
        return target && RectTransformUtility.RectangleContainsScreenPoint(target, Input.mousePosition, GetEventCamera());
    }

    private Camera GetEventCamera()
    {
        if (!canvas)
        {
            canvas = GetComponentInParent<Canvas>();
        }
        if (!canvas || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }
        return canvas.worldCamera ? canvas.worldCamera : Camera.main;
    }

    private static int ParseInt(string text, int fallback)
    {
        int value;
        return int.TryParse(text, out value) ? value : fallback;
    }
}
