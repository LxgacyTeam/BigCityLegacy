using HarmonyLib;
using UnityEngine;

[HarmonyPatch]
internal static class CombatRacePatches
{
    [HarmonyPatch(typeof(Race), "CloneTrassa")]
    [HarmonyPostfix]
    private static void RestoreBulletPickups(Race __instance)
    {
        if (!__instance.isHaveFireZone() || NetManager.isServer)
            return;

        foreach (var bonus in __instance.bonuses)
        {
            if (bonus && bonus.name == "BonusBullet" && bonus.type != Bonus.BonusType.BonusBullet)
                LegacyRaceBulletPickup.Restore(bonus);
        }
    }

    [HarmonyPatch(typeof(Bonus), "OnTriggerEnter")]
    [HarmonyPrefix]
    private static bool CollectGunAmmo(Bonus __instance, Collider col)
    {
        if (__instance.type != Bonus.BonusType.BonusBullet || !Race.cur || !Race.cur.isHaveFireZone())
            return true;

        var input = col ? col.TryGetHandleInput() : null;
        var gun = input && input.currentOrParent ? input.currentOrParent.GetWeapone(Weapone.Type.CarBullet) : null;
        return gun && gun.totalBulletsCount < gun.maxTotalBulletsCount();
    }

    [HarmonyPatch(typeof(RaceFireZone), "OnTriggerEnter")]
    [HarmonyPostfix]
    private static void RestoreCarGun(Collider coll)
    {
        var race = Race.cur;
        if (!race || !race.isHaveFireZone() || NetManager.isServer || !coll)
            return;

        var input = coll.TryGetHandleInput();
        if (!input || !input.ai.isAiRacing() || !race.racers.Contains(input) ||
            !(input.currentOrParent is CarControl car) || car.net_control)
            return;

        LegacyCombatRace.GetOrAdd(race).Arm(car);
    }

    [HarmonyPatch(typeof(Weapone), "maxTotalBulletsCount")]
    [HarmonyPrefix]
    private static bool UseFullMagazine(Weapone __instance, ref int __result)
    {
        if (!(__instance is WeaponeGun1) || !LegacyCombatRace.IsArmed(__instance))
            return true;

        __result = __instance.maxMagazineCapacity();
        return false;
    }

    [HarmonyPatch(typeof(Weapone), "SelectNextBestWeapone")]
    [HarmonyPrefix]
    private static bool SelectNextRaceWeapon(Weapone __instance)
    {
        if (!LegacyCombatRace.IsRaceWeapon(__instance) || !LegacyCombatRace.IsArmed(__instance))
            return true;

        var car = __instance.control;
        var input = car.GetHandleInput();
        if (input && !input.isUser() && car.selectWeapone == __instance)
        {
            foreach (var weapon in car.weapones)
            {
                if (weapon && weapon.canBeSelect && LegacyCombatRace.IsRaceWeapon(weapon) && !weapon.isEmpty)
                {
                    car.SetSelectWeapone(weapon);
                    break;
                }
            }
        }
        return false;
    }

    [HarmonyPatch(typeof(Race), "OnDestroy")]
    [HarmonyPrefix]
    private static void ClearRaceSelections(Race __instance)
    {
        var combat = __instance.GetComponent<LegacyCombatRace>();
        if (combat) combat.ClearSelections();
    }

    [HarmonyPatch(typeof(AI_Race), "RemoveWeapone")]
    [HarmonyPrefix]
    private static void ClearCarSelection(Control wasControl)
    {
        var race = Race.cur;
        if (!race || !(wasControl is CarControl car)) return;

        var combat = race.GetComponent<LegacyCombatRace>();
        if (combat) combat.ReleaseCar(car);
    }

    [HarmonyPatch(typeof(AI_Race_FireTo), "Update")]
    [HarmonyPrefix]
    private static bool UpdateUserAim(AI_Race_FireTo __instance)
    {
        var combat = LegacyCombatRace.GetActive(__instance.input, out var car);
        if (!combat) return true;

        combat.RefreshAim(car);
        return false;
    }

    [HarmonyPatch(typeof(WeaponePointer_UI), "UpPointerVisible")]
    [HarmonyPostfix]
    private static void ShowRaceTarget(WeaponePointer_UI __instance)
    {
        if (!LegacyCombatRace.GetActive(InputControl.firstUser, out var car))
            return;

        var weapon = car.selectWeapone;
        if (!(weapon is WeaponeRocket1 || weapon is WeaponeGun1))
        {
            __instance.weaponePointer.enabled = false;
            __instance.wepoinePointerLive.gameObject.SetActive(false);
            return;
        }

        var image = __instance.weaponePointer;
        var health = __instance.wepoinePointerLive;
        bool visible = weapon.to_fire && !weapon.isEmpty && !MenuEsc.me && !FlyCamera.me &&
            !WeaponeSelect.isStartSelect && Cam.me && CamCurrent.curView == Cam.me.currentForPlayer &&
            DisableGameUI.CountCreated == 0;
        if (visible)
        {
            var position = GameUI.me.WorkldToScreenPixelFromCenter(weapon.to_fire.body_pos + Vector3.up * 1.5f);
            visible = position.z > 0f;
            image.rectTransform.anchoredPosition = new Vector2(position.x, position.y);
            var sprite = weapon.pointer.sprite;
            if (!sprite) sprite = car.GetWeapone(Weapone.Type.CarRocket)?.pointer.sprite;
            if (sprite) image.sprite = sprite;
            health.color = new Color(Mathf.Clamp01(weapon.to_fire.live / 100f), 0f, 0f, 1f);
        }
        image.enabled = visible;
        image.gameObject.SetActive(visible);
        health.gameObject.SetActive(visible);
    }

    [HarmonyPatch(typeof(Control), "CheckWeapone")]
    [HarmonyPrefix]
    private static bool UseSelectedWeapon(Control __instance, InputBut.But but, bool isStart)
    {
        if (but != InputBut.But.Car_Fire && but != InputBut.But.Car_Mine)
            return true;

        var combat = LegacyCombatRace.GetActive(__instance.input, out var car);
        if (!combat || __instance.parent_control != car)
            return true;

        if (isStart && __instance.input.buts.isInputBlocked)
            return false;

        if (but == InputBut.But.Car_Fire)
            combat.Fire(car, isStart);
        else if (isStart && LegacyCombatRace.CanFire())
            car.GetWeapone(Weapone.Type.CarMine)?.Fire();

        return false;
    }

    [HarmonyPatch(typeof(TouchBut), "isNeed")]
    [HarmonyPostfix]
    private static void ShowFireButton(TouchBut __instance, InputControl input, bool isCanControl, ref bool __result)
    {
        if (__instance.but != InputBut.But.Car_Fire || !LegacyCombatRace.GetActive(input, out var car))
            return;

        var weapon = car.selectWeapone;
        __result = isCanControl && LegacyCombatRace.IsRaceWeapon(weapon) && weapon.isUseble();
    }

}
