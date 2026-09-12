using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[HarmonyPatch]
internal static class SocialLinkRewardPatches
{
    private const string DiamondIconSpriteName = "Icon_Almaz";

    // Exact target from the instantiated menu. Keep this intentionally narrow:
    // Data/Canvas/img/MenuEsc(Clone)/Root/Shop/Groups/Free_menu/item_3/scale (1)/amount/icon
    private const string TargetIconScenePath = "Data/Canvas/img/MenuEsc(Clone)/Root/Shop/Groups/Free_menu/item_3/scale (1)/amount/icon";
    private const string TargetIconLocalPath = "scale (1)/amount/icon";

    private enum RewardCurrency
    {
        None,
        Coins,
        Diamonds
    }

    private struct SocialReward
    {
        public readonly RewardCurrency Currency;
        public readonly int Amount;

        private SocialReward(RewardCurrency currency, int amount)
        {
            Currency = currency;
            Amount = amount;
        }

        public static SocialReward Coins(int amount)
        {
            return new SocialReward(RewardCurrency.Coins, amount);
        }

        public static SocialReward Diamonds(int amount)
        {
            return new SocialReward(RewardCurrency.Diamonds, amount);
        }

        public static SocialReward Disabled()
        {
            return new SocialReward(RewardCurrency.None, 0);
        }
    }

    /*
     * Usage:
     * SocialReward.Coins(25000)
     * SocialReward.Diamonds(50)
     * SocialReward.Disabled()
     */
    private static readonly SocialReward FacebookReward = SocialReward.Coins(250000);
    private static readonly SocialReward InstagramReward = SocialReward.Coins(750000);
    private static readonly SocialReward VkReward = SocialReward.Diamonds(20000);

    private static readonly SocialReward MailReward = SocialReward.Disabled(); // not used in game

    private static readonly FieldInfo CurAmountField =
        AccessTools.Field(typeof(InApp_But), "curAmount");

    private static readonly Dictionary<int, Sprite> OriginalIconSprites = new Dictionary<int, Sprite>();
    private static Sprite DiamondIconSprite;
    private static bool warnedMissingTargetIcon;
    private static bool warnedMissingDiamondSprite;

    [HarmonyPatch(typeof(InApp_But), "Awake")]
    [HarmonyPostfix]
    private static void InAppBut_Awake_Postfix(InApp_But __instance)
    {
        RefreshSocialButton(__instance);
    }

    [HarmonyPatch(typeof(InApp_But), "Update")]
    [HarmonyPostfix]
    private static void InAppBut_Update_Postfix(InApp_But __instance)
    {
        RefreshSocialButton(__instance);
    }

    [HarmonyPatch(typeof(InApp_But), nameof(InApp_But.SubScrive))]
    [HarmonyPrefix]
    private static bool InAppBut_SubScrive_Prefix(InApp_But __instance, string url)
    {
        SocialReward reward;
        if (!TryGetReward(__instance.type, out reward))
        {
            return true;
        }

        RefreshSocialButton(__instance);

        GiveReward(__instance.type, reward);

        if (!string.IsNullOrEmpty(url))
        {
            Application.OpenURL(url);
        }

        return false;
    }

    private static void RefreshSocialButton(InApp_But button)
    {
        if (button == null)
        {
            return;
        }

        SocialReward reward;
        if (!TryGetReward(button.type, out reward))
        {
            return;
        }

        string prefKey = button.type.ToString();

        if (PlayerPrefs.HasKey(prefKey))
        {
            PlayerPrefs.DeleteKey(prefKey);
            PlayerPrefs.Save();
        }

        if (CurAmountField != null)
        {
            CurAmountField.SetValue(button, reward.Amount);
        }

        if (button.amount != null)
        {
            button.amount.gameObject.SetActive(true);
            button.amount.text = reward.Amount.ToString().SeparateByStep(3U, " ", true);
        }

        RefreshRewardIcon(button, reward);
    }

    private static void RefreshRewardIcon(InApp_But button, SocialReward reward)
    {
        // The game does not choose the icon dynamically. The Image has a fixed Sprite in Canvas,
        // so replacing Material/Shader is not enough: we must replace Image.sprite / overrideSprite.
        if (reward.Currency != RewardCurrency.Diamonds)
        {
            return;
        }

        Image icon = FindTargetAmountIcon(button);
        if (!icon)
        {
            if (!warnedMissingTargetIcon)
            {
                warnedMissingTargetIcon = true;
                Debug.LogWarning("[BigCityLegacy] Social reward diamond icon target was not found: " + TargetIconScenePath);
            }
            return;
        }

        Sprite sprite = FindSprite(DiamondIconSpriteName);
        if (!sprite)
        {
            if (!warnedMissingDiamondSprite)
            {
                warnedMissingDiamondSprite = true;
                Debug.LogWarning("[BigCityLegacy] Social reward diamond sprite not found: " + DiamondIconSpriteName);
            }
            return;
        }

        int id = icon.GetInstanceID();
        if (!OriginalIconSprites.ContainsKey(id))
        {
            OriginalIconSprites[id] = icon.overrideSprite ? icon.overrideSprite : icon.sprite;
        }

        if (icon.sprite == sprite && icon.overrideSprite == sprite)
        {
            return;
        }

        icon.sprite = sprite;
        icon.overrideSprite = sprite;
        icon.SetVerticesDirty();
        icon.SetMaterialDirty();
    }

    private static Image FindTargetAmountIcon(InApp_But button)
    {
        if (button)
        {
            Transform local = button.transform.Find(TargetIconLocalPath);
            if (local)
            {
                Image image = local.GetComponent<Image>();
                if (image)
                {
                    return image;
                }
            }

            if (button.amount)
            {
                Transform direct = button.amount.transform.Find("icon");
                if (direct)
                {
                    Image image = direct.GetComponent<Image>();
                    if (image)
                    {
                        return image;
                    }
                }
            }
        }

        Image[] images = Resources.FindObjectsOfTypeAll<Image>();
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (!image || !IsRuntimeSceneObject(image.gameObject))
            {
                continue;
            }

            string path = GetTransformPath(image.transform);
            if (string.Equals(path, TargetIconScenePath, StringComparison.Ordinal))
            {
                return image;
            }
        }

        return null;
    }

    private static Sprite FindSprite(string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName))
        {
            return null;
        }

        if (DiamondIconSprite && string.Equals(DiamondIconSprite.name, spriteName, StringComparison.Ordinal))
        {
            return DiamondIconSprite;
        }

        Sprite[] sprites = Resources.FindObjectsOfTypeAll<Sprite>();
        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite sprite = sprites[i];
            if (sprite && string.Equals(sprite.name, spriteName, StringComparison.Ordinal))
            {
                DiamondIconSprite = sprite;
                return sprite;
            }
        }

        Image[] images = Resources.FindObjectsOfTypeAll<Image>();
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (!image)
            {
                continue;
            }

            Sprite overrideSprite = image.overrideSprite;
            if (overrideSprite && string.Equals(overrideSprite.name, spriteName, StringComparison.Ordinal))
            {
                DiamondIconSprite = overrideSprite;
                return overrideSprite;
            }

            Sprite sprite = image.sprite;
            if (sprite && string.Equals(sprite.name, spriteName, StringComparison.Ordinal))
            {
                DiamondIconSprite = sprite;
                return sprite;
            }
        }

        return null;
    }

    private static bool IsRuntimeSceneObject(GameObject go)
    {
        if (!go)
        {
            return false;
        }

        Scene scene = go.scene;
        return scene.IsValid() && scene.isLoaded;
    }

    private static string GetTransformPath(Transform transform)
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

    private static bool TryGetReward(InApp_But.Types type, out SocialReward reward)
    {
        switch (type)
        {
            case InApp_But.Types.Subs_Fb:
                reward = FacebookReward;
                return reward.Currency != RewardCurrency.None && reward.Amount > 0;

            case InApp_But.Types.Subs_Vk:
                reward = VkReward;
                return reward.Currency != RewardCurrency.None && reward.Amount > 0;

            case InApp_But.Types.Subs_Instagram:
                reward = InstagramReward;
                return reward.Currency != RewardCurrency.None && reward.Amount > 0;

            case InApp_But.Types.Subs_Mail:
                reward = MailReward;
                return reward.Currency != RewardCurrency.None && reward.Amount > 0;

            default:
                reward = SocialReward.Disabled();
                return false;
        }
    }

    private static void GiveReward(InApp_But.Types type, SocialReward reward)
    {
        string reason = "SocialLink_" + type.ToString();

        switch (reward.Currency)
        {
            case RewardCurrency.Coins:
                Money.Add(reward.Amount, reason, true);
                break;

            case RewardCurrency.Diamonds:
                Diamonds.Add(reward.Amount, reason);
                break;
        }
    }
}
