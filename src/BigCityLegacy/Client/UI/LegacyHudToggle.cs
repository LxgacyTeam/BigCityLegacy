using System.Collections.Generic;
using UnityEngine;

internal static class LegacyHudToggle
{
    private const string ImgPath = "Data/Canvas/img";
    private static bool _hidden;
    private static float _nextSweep;
    private static GameObject _img;

    private static readonly List<GameObject> _previouslyActive = new List<GameObject>();

    internal static bool IsHidden { get { return _hidden; } }

    internal static void Toggle()
    {
        _hidden = !_hidden;
        _nextSweep = 0f;

        if (_hidden) Hide(true);
        else Restore();

        Debug.Log("[BigCityLegacy] HUD hidden: " + _hidden);
    }

    internal static void Tick()
    {
        if (!_hidden) return;
        if (Time.unscaledTime < _nextSweep) return;
        _nextSweep = Time.unscaledTime + 0.5f;
        Hide(false);
    }

    private static bool FindImg()
    {
        if (!_img) _img = GameObject.Find(ImgPath);
        return _img;
    }

    private static void Hide(bool rememberState)
    {
        if (!FindImg()) return;

        if (rememberState) _previouslyActive.Clear();

        for (int i = 0; i < _img.transform.childCount; i++)
        {
            GameObject child = _img.transform.GetChild(i).gameObject;

            if (rememberState && child.activeSelf)
                _previouslyActive.Add(child);

            if (child.activeSelf)
                child.SetActive(false);
        }
    }

    private static void Restore()
    {
        if (!FindImg()) return;

        for (int i = 0; i < _previouslyActive.Count; i++)
        {
            GameObject child = _previouslyActive[i];

            if (child)
                child.SetActive(true);
        }

        _previouslyActive.Clear();
    }
}