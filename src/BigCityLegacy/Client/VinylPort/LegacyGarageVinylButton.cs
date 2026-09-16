using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class LegacyGarageVinylButton : MonoBehaviour
{
    private const string ButtonName = "BigCityLegacyVinylPort";

    private static LegacyGarageVinylButton instance;
    private CarGarageUI lastUi;
    private CarGarageUI pendingUi;
    private int pendingFrames;

    public static void Ensure()
    {
        if (instance == null)
        {
            GameObject go = new GameObject("LegacyGarageVinylButton");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<LegacyGarageVinylButton>();
        }
    }

    private void Update()
    {
        CarGarageUI ui = CarGarageUI.me;
        if (!ui)
        {
            lastUi = null;
            pendingUi = null;
            return;
        }
        if (lastUi == ui) return;
        if (pendingUi != ui)
        {
            pendingUi = ui;
            pendingFrames = 2;
            return;
        }
        pendingFrames--;
        if (pendingFrames > 0) return;
        lastUi = ui;
        pendingUi = null;
        try
        {
            Create(ui);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BigCityLegacy] VinylPort garage button: " + ex);
        }
    }

    private static void Create(CarGarageUI ui)
    {
        if (!ui || !ui.root) return;
        Transform main = ui.root.Find("Main");
        if (!main) return;
        if (main.Find(ButtonName)) return;

        Transform fix = main.Find("FixCar");
        Transform wheels = main.Find("Wheels");
        Transform anchor = (fix && fix.gameObject.activeSelf) ? fix : wheels;
        if (!anchor) return;
        Transform template = fix ? fix : wheels;

        GameObject clone = UnityEngine.Object.Instantiate(template.gameObject, main, false);
        clone.name = ButtonName;

        RectTransform templateRect = template as RectTransform;
        RectTransform cloneRect = clone.transform as RectTransform;
        RectTransform anchorRect = anchor as RectTransform;
        if (templateRect && cloneRect)
        {
            cloneRect.anchorMin = templateRect.anchorMin;
            cloneRect.anchorMax = templateRect.anchorMax;
            cloneRect.pivot = templateRect.pivot;
            cloneRect.sizeDelta = templateRect.sizeDelta;
            cloneRect.anchoredPosition = templateRect.anchoredPosition;
        }
        clone.transform.SetSiblingIndex(anchor.GetSiblingIndex() + 1);
        clone.SetActive(true);

        if (!main.GetComponent<LayoutGroup>() && anchorRect && cloneRect)
        {
            float shift = cloneRect.sizeDelta.y + 8f;
            cloneRect.anchoredPosition = new Vector2(anchorRect.anchoredPosition.x, anchorRect.anchoredPosition.y - shift);
            foreach (Transform sib in main)
            {
                if (sib == anchor || sib == clone.transform) continue;
                RectTransform r = sib as RectTransform;
                if (r && r.anchoredPosition.y < anchorRect.anchoredPosition.y - 1f)
                {
                    r.anchoredPosition -= new Vector2(0f, shift);
                }
            }
        }

        foreach (MenuButtonSomeEvents some in clone.GetComponentsInChildren<MenuButtonSomeEvents>(true))
        {
            some.whenBecomeActive_ActiveGO = null;
        }

        bool wired = false;
        MenuButtonEvent ev = clone.GetComponent<MenuButtonEvent>();
        if (ev)
        {
            ev.OnClickEvent = new UnityEvent();
            ev.OnClickEvent.AddListener(delegate { LegacyVinylPort.Instance.Open(); });
            ev.CallBackEvent = null;
            wired = true;
        }

        MenuButtonGroup grp = clone.GetComponent<MenuButtonGroup>();
        if (grp)
        {
            if (grp.content)
            {
                MenuGroup contentGroup = grp.content.GetComponent<MenuGroup>();
                MenuButtonGroup anchorGrp = anchor.GetComponent<MenuButtonGroup>();
                if (contentGroup && anchorGrp && contentGroup.BackButton == grp)
                {
                    contentGroup.BackButton = anchorGrp;
                }
            }
            grp.content = null;
            grp.selectAfterPress = null;
            grp.manualGroup = null;
            grp.SwithEvent = new UnityEvent();
            grp.SwithEvent.AddListener(delegate { LegacyVinylPort.Instance.Open(); });
            wired = true;
        }

        if (!wired)
        {
            Debug.LogWarning("[BigCityLegacy] VinylPort garage button: template has no MenuButtonEvent/MenuButtonGroup");
        }

        Localize label = ev ? ev.text : (grp ? grp.text : null);
        if (label)
        {
            LegacySettingsMenuHelper.SetLocalize(label, "VinylPort");
        }
        else
        {
            foreach (Text t in clone.GetComponentsInChildren<Text>(true))
            {
                t.text = "VinylPort";
            }
        }

        MenuButton menuButton = clone.GetComponent<MenuButton>();
        if (menuButton)
        {
            menuButton.isStaickPos = true;
            menuButton.isSkipSaves = true;
            menuButton.InitParent();
        }
    }
}