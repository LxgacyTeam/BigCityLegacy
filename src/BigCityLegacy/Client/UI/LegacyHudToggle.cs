using UnityEngine;

internal static class LegacyHudToggle
{
    private const string ImgPath = "Data/Canvas/img";
    private static bool _hidden;
    private static float _nextSweep;
    private static GameObject _img;

    internal static bool IsHidden { get { return _hidden; } }

    internal static void Toggle()
    {
        _hidden = !_hidden;
        _nextSweep = 0f;
        Apply();
        Debug.Log("[BigCityLegacy] HUD hidden: " + _hidden);
    }

    internal static void Tick()
    {
        if (!_hidden) return;
        if (Time.unscaledTime < _nextSweep) return;
        _nextSweep = Time.unscaledTime + 0.5f;
        Apply();
    }

    private static void Apply()
    {
        if (!_img) _img = GameObject.Find(ImgPath);
        if (!_img) return;
        if (_img.activeSelf == _hidden) _img.SetActive(!_hidden);
    }
}