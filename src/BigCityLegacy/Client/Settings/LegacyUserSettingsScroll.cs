using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

internal sealed class LegacyUserSettingsScroll : MonoBehaviour
{
    private const float ValueRightInset = 36f;
    private MenuGroup group;
    private MenuSroll_Area area;
    private RectTransform viewport;
    private RectTransform content;
    private MenuButton about;
    private readonly List<MenuButton> rows = new List<MenuButton>();
    private Canvas canvas;

    internal static void Ensure(MenuGroup group)
    {
        if (!group || !string.Equals(group.name, "User", StringComparison.OrdinalIgnoreCase)) return;
        var scroll = group.GetComponent<LegacyUserSettingsScroll>();
        if (!scroll) scroll = group.gameObject.AddComponent<LegacyUserSettingsScroll>();
        scroll.group = group;
        if (!scroll.area) scroll.Initialize();
    }

    private void Initialize()
    {
        var parent = group.transform.parent;
        var source = parent ? parent.Find("Render/items") : null;
        var template = source ? source.GetComponent<MenuSroll_Area>() : null;
        if (!template || !template.bar || !template.scroll_root) return;
        var clone = Instantiate(template.gameObject);
        clone.SetActive(false);
        clone.name = "BigCityLegacyUserScroll";
        clone.transform.SetParent(group.transform, false);
        area = clone.GetComponent<MenuSroll_Area>();
        viewport = clone.GetComponent<RectTransform>();
        content = area.scroll_root;
        foreach (var copiedGroup in clone.GetComponentsInChildren<MenuGroup>(true)) DestroyImmediate(copiedGroup);
        for (var i = content.childCount - 1; i >= 0; --i) DestroyImmediate(content.GetChild(i).gameObject);
        viewport.anchorMin = viewport.anchorMax = new Vector2(0f, 1f);
        viewport.pivot = new Vector2(0f, 1f);
        viewport.anchoredPosition = Vector2.zero;
        content.anchorMin = content.anchorMax = new Vector2(0f, 1f);
        content.pivot = new Vector2(0f, 1f);
        content.anchoredPosition = Vector2.zero;
        area.isHorizontal = false;
        area.resizeBar = false;
        area.scrollAlpha_100 = 0f;
        canvas = GetComponentInParent<Canvas>();
        RefreshRows();
        LayoutRows();
        clone.SetActive(true);
    }

    private void RefreshRows()
    {
        var changed = false;
        rows.RemoveAll(row => !row);
        foreach (var button in group.GetComponentsInChildren<MenuButton>(true))
        {
            if (button.GetComponentInParent<MenuGroup>() != group) continue;
            if (!button.gameObject.activeInHierarchy && !rows.Contains(button)) continue;
            if (button.transform.parent && button.transform.parent.GetComponentInParent<MenuButton>()) continue;
            if (button.name == "BigCityLegacySetting_user.about")
            {
                about = button;
                button.isStaickPos = true;
                if (button.transform.parent != group.transform) button.transform.SetParent(group.transform, false);
                continue;
            }
            if (rows.Contains(button)) continue;
            rows.Add(button);
            changed = true;
        }
        if (changed) rows.Sort((a, b) =>
        {
            if (a.name == "LegacyMapExpansionSetting") return b.name == a.name ? 0 : 1;
            if (b.name == "LegacyMapExpansionSetting") return -1;
            return ((RectTransform)b.transform).anchoredPosition.y.CompareTo(((RectTransform)a.transform).anchoredPosition.y);
        });
        var mask = viewport.GetComponent<MenuMaskArea>();
        foreach (var row in rows)
        {
            row.isStaickPos = true;
            if (row.transform.parent != content) row.transform.SetParent(content, false);
            foreach (var press in row.GetComponentsInChildren<MenuPress>(true)) press.maskArea = mask;
        }
    }

    private void LayoutRows()
    {
        var rect = (RectTransform)group.transform;
        var width = Mathf.Max(100f, rect.rect.width);
        var height = Mathf.Max(100f, rect.rect.height);
        var reserved = 0f;
        if (about && about.gameObject.activeSelf)
        {
            var footer = (RectTransform)about.transform;
            reserved = Mathf.Max(footer.rect.height, 40f) + 20f;
            footer.anchorMin = footer.anchorMax = new Vector2(0f, 0f);
            footer.pivot = new Vector2(0f, 0f);
            footer.anchoredPosition = new Vector2(0f, 8f);
            footer.sizeDelta = new Vector2(width, footer.sizeDelta.y);
        }
        viewport.sizeDelta = new Vector2(width, Mathf.Max(80f, height - reserved));
        var bar = (RectTransform)area.bar.transform;
        bar.anchorMin = bar.anchorMax = new Vector2(1f, 1f);
        bar.pivot = new Vector2(1f, 1f);
        bar.anchoredPosition = Vector2.zero;
        bar.sizeDelta = new Vector2(Mathf.Max(12f, bar.sizeDelta.x), viewport.sizeDelta.y);
        var y = 0f;
        foreach (var row in rows)
        {
            var rowRect = (RectTransform)row.transform;
            rowRect.anchorMin = rowRect.anchorMax = new Vector2(0f, 1f);
            rowRect.pivot = new Vector2(0f, 1f);
            rowRect.sizeDelta = new Vector2(width - bar.sizeDelta.x - 8f, rowRect.sizeDelta.y);
            rowRect.anchoredPosition = new Vector2(0f, row.gameObject.activeSelf ? -y : 0f);
            LayoutValue(row, rowRect.rect.width);
            LayoutSlider(row, rowRect.rect.width);
            if (row.gameObject.activeSelf) y += Mathf.Max(36f, rowRect.rect.height) + 5f;
        }
        area.cur_max_size = Mathf.Max(0f, y - 5f);
        area.area_size = viewport.sizeDelta.y;
        content.sizeDelta = new Vector2(width, area.cur_max_size);
        if (y <= area.area_size) area.scrollAlpha_100 = 0f;
    }

    private static void LayoutValue(MenuButton row, float width)
    {
        var choice = row as MenuSettingText;
        var boolean = row as MenuSettingBool;
        var slider = row as MenuSettingSlider;
        var text = choice && choice.value ? choice.value.ui : boolean && boolean.value ? boolean.value.ui : slider ? slider.value : null;
        if (!text) return;
        var slot = Mathf.Min(width * 0.43f, Mathf.Max(68f, text.preferredWidth + 8f));
        var rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-ValueRightInset, 0f);
        rect.sizeDelta = new Vector2(slot, Mathf.Max(30f, rect.sizeDelta.y));
        text.alignment = TextAnchor.MiddleRight;
        if (!choice) return;
        LayoutArrow(choice.R, 6f);
        LayoutArrow(choice.L, ValueRightInset + slot + 6f);
    }

    private static void LayoutArrow(Text text, float inset)
    {
        if (!text) return;
        var rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-inset, 0f);
        rect.sizeDelta = new Vector2(22f, Mathf.Max(30f, rect.sizeDelta.y));
    }

    private static void LayoutSlider(MenuButton row, float width)
    {
        var slider = row as MenuSettingSlider;
        if (!slider || !slider.text || !slider.value) return;
        var track = slider.transform.Find("SliderRoot") as RectTransform;
        if (!track) return;
        var valueWidth = Mathf.Max(68f, slider.value.preferredWidth + 8f);
        var labelWidth = Mathf.Min(slider.text.preferredWidth + 8f, width * 0.52f);
        var label = slider.text.rectTransform;
        label.anchorMin = label.anchorMax = new Vector2(0f, 0.5f);
        label.pivot = new Vector2(0f, 0.5f);
        label.anchoredPosition = new Vector2(8f, 0f);
        label.sizeDelta = new Vector2(labelWidth, ((RectTransform)row.transform).rect.height);
        slider.text.resizeTextForBestFit = true;
        slider.text.resizeTextMinSize = Mathf.Min(18, slider.text.fontSize);
        slider.text.resizeTextMaxSize = slider.text.fontSize;
        var value = slider.value.rectTransform;
        value.anchorMin = value.anchorMax = new Vector2(1f, 0.5f);
        value.pivot = new Vector2(1f, 0.5f);
        value.anchoredPosition = new Vector2(-ValueRightInset, 0f);
        value.sizeDelta = new Vector2(valueWidth, value.sizeDelta.y);
        var left = labelWidth + 20f;
        track.anchorMin = track.anchorMax = new Vector2(0f, 0.5f);
        track.pivot = new Vector2(0f, 0.5f);
        track.anchoredPosition = new Vector2(left, 0f);
        track.sizeDelta = new Vector2(Mathf.Max(30f, width - left - valueWidth - ValueRightInset - 12f), track.sizeDelta.y);
    }

    private void LateUpdate()
    {
        if (!area) return;
        RefreshRows();
        LayoutRows();
        var overflow = area.cur_max_size - area.area_size;
        if (overflow <= 0f) return;
        var camera = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (RectTransformUtility.RectangleContainsScreenPoint(viewport, Input.mousePosition, camera))
            area.scrollAlpha_100 = Mathf.Clamp(area.scrollAlpha_100 - Input.mouseScrollDelta.y * 55f / overflow * 100f, 0f, 100f);
        if (!Cursor.visible || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
        {
            var focus = GetComponentInParent<MenuFocusManager>();
            var active = focus ? focus.curActive : null;
            if (active && rows.Contains(active))
            {
                var row = (RectTransform)active.transform;
                var offset = overflow * Mathf.Clamp01(area.scrollAlpha_100 / 100f);
                var top = -row.anchoredPosition.y;
                if (top < offset) area.scrollAlpha_100 = top / overflow * 100f;
                else if (top + row.rect.height > offset + area.area_size)
                    area.scrollAlpha_100 = (top + row.rect.height - area.area_size) / overflow * 100f;
            }
        }
        area.scrollAlpha_100 = Mathf.Clamp(area.scrollAlpha_100, 0f, 100f);
        content.anchoredPosition = new Vector2(0f, overflow * area.scrollAlpha_100 / 100f);
    }
}
