using System.Collections.Generic;
using UnityEngine;

internal sealed class LegacyCombatRace : MonoBehaviour
{
    private readonly HashSet<CarControl> armedCars = new HashSet<CarControl>();
    private Race race;
    private Weapone firing;

    internal static LegacyCombatRace GetActive(InputControl input, out CarControl car)
    {
        if (!TryGetDriver(input, out car) || !IsRacing(input))
            return null;

        var race = Race.cur;
        var combat = race.GetComponent<LegacyCombatRace>();
        return combat && combat.armedCars.Contains(car) ? combat : null;
    }

    internal static bool TryGetDriver(InputControl input, out CarControl car)
    {
        car = input ? input.currentOrParent as CarControl : null;
        return car && car.isAlive && !car.net_control && !NetManager.isServer && !NetManager.isOnlineClient &&
            input.isUser() && car.GetHandleInput() == input;
    }

    private static bool IsRacing(InputControl input)
    {
        var race = Race.cur;
        return race && race.isHaveFireZone() && !NetManager.isServer && !NetManager.isOnlineClient &&
            input && input.ai && input.ai.isAiRacing() && !input.ai.GetAiRacing().is_finish;
    }

    internal static LegacyCombatRace GetOrAdd(Race race)
    {
        var combat = race.GetComponent<LegacyCombatRace>();
        if (!combat)
        {
            combat = race.gameObject.AddComponent<LegacyCombatRace>();
            combat.race = race;
        }
        return combat;
    }

    internal static bool IsRaceWeapon(Weapone weapon)
    {
        return weapon is WeaponeRocket1 || weapon is WeaponeGun1 || weapon is WeaponeMine1;
    }

    internal void Arm(CarControl car)
    {
        foreach (var setup in car.GetComponentsInChildren<WeaponeSetup>())
        {
            if (!setup.SelectPrefab || !IsRaceWeapon(setup.SelectPrefab) || setup.created)
                continue;

            if (setup.SelectPrefab is WeaponeGun1 prefab)
            {
                var projectiles = Nuligine.me.weapones.bullets;
                if (!prefab.Stvol || projectiles.Length == 0 || !projectiles[0])
                    continue;

                var gun = Instantiate(prefab, car.tr_view ? car.tr_view : setup.transform);
                setup.created = gun;
                gun.transform.SetLocalTr(setup.transform);
                gun.Init(setup);
                armedCars.Add(car);
                gun.FillFull();
            }
            else
                setup.Create();
        }

        bool hasWeapons = false;
        foreach (var weapon in car.weapones)
        {
            if (!weapon || !IsRaceWeapon(weapon)) continue;
            hasWeapons = true;
            armedCars.Add(car);
        }
        if (!hasWeapons) return;

        var mine = car.GetWeapone(Weapone.Type.CarMine);
        if (mine)
            mine.canBeSelect = true;

        if (!IsRaceWeapon(car.selectWeapone))
        {
            var first = car.GetWeapone(Weapone.Type.CarRocket);
            if (!first) first = car.GetWeapone(Weapone.Type.CarBullet);
            if (!first) first = mine;
            if (first) car.SetSelectWeapone(first);
        }
        var input = car.GetHandleInput();
        if (input && input.ai.isAiRacing())
            input.ai.GetAiRacing().fire_to.enabled = true;

    }

    internal static bool IsArmed(Weapone weapon)
    {
        var race = Race.cur;
        if (!race || !(weapon.control is CarControl car))
            return false;

        var combat = race.GetComponent<LegacyCombatRace>();
        return combat && combat.armedCars.Contains(car);
    }

    internal void Fire(CarControl car, bool pressed)
    {
        if (!pressed)
        {
            StopFiring();
            return;
        }

        var weapon = car.selectWeapone;
        if (!IsRaceWeapon(weapon) || weapon.isEmpty || !CanFire())
            return;

        if (weapon is WeaponeGun1)
            firing = weapon;
        RefreshAim(car);
        weapon.Fire();
    }

    internal void RefreshAim(CarControl car)
    {
        var weapon = car.selectWeapone;
        if (!(weapon is WeaponeRocket1 || weapon is WeaponeGun1))
            return;

        Control target = null;
        float nearest = 100f * 100f;
        if (!weapon.isEmpty)
        {
            Vector3 origin = car.body_pos + Vector3.up * 1.5f;
            Vector3 forward = Vector3.ProjectOnPlane(car.body_dir, Vector3.up).normalized;
            foreach (var racer in race.racers)
            {
                if (!racer || !(racer.currentOrParent is CarControl opponent) || opponent == car ||
                    !opponent.isAlive || !opponent.GetCanKill() || !racer.ai.isAiRacing() ||
                    racer.ai.GetAiRacing().is_finish)
                    continue;

                Vector3 offset = Vector3.ProjectOnPlane(opponent.body_pos - car.body_pos, Vector3.up);
                float distance = offset.sqrMagnitude;
                if (distance < 4f || distance >= nearest || Vector3.Dot(forward, offset.normalized) < 0.3f)
                    continue;

                Vector3 direction = opponent.body.worldCenterOfMass - origin;
                var hit = default(RaycastHit);
                if (hPhysics.RaycastNoTriggers(new Ray(origin, direction), ref hit, direction.magnitude, car.body) &&
                    hit.rigidbody != opponent.body)
                    continue;

                target = opponent;
                nearest = distance;
            }
        }
        weapon.to_fire = target;
    }

    internal static bool CanFire()
    {
        return !Control.NeedSkipOneFire_Or_Beat_Frame && !WeaponeSelect.isStartSelect &&
            !MenuEsc.me && !FlyCamera.me;
    }

    private void LateUpdate()
    {
        if (!firing)
            return;
        var input = firing.control ? firing.control.GetHandleInput() : null;
        if (!GetActive(input, out var car) || car.selectWeapone != firing ||
            input.buts.isInputBlocked || !input.buts.isPressed(InputBut.But.Car_Fire) || !CanFire())
        {
            StopFiring();
            return;
        }
        firing.Fire();
    }

    private void StopFiring()
    {
        firing = null;
    }

    internal void ClearSelections()
    {
        StopFiring();
        foreach (var car in armedCars)
        {
            if (car && IsRaceWeapon(car.selectWeapone))
                car.SetSelectWeapone((Weapone)null);
        }
        armedCars.Clear();
    }

    internal void ReleaseCar(CarControl car)
    {
        if (!armedCars.Remove(car)) return;
        if (firing && firing.control == car) StopFiring();
        if (IsRaceWeapon(car.selectWeapone))
            car.SetSelectWeapone((Weapone)null);
    }
}
