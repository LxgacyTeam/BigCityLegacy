using UnityEngine;

internal sealed class LegacyOnlineRacePoints : MonoBehaviour
{
    internal NetRace Owner;
    internal bool AllowNitroPurchase = true;

    internal bool CanTake(RaceDirection point)
    {
        var points = GetComponent<RaceDirectionPoints>();
        return Owner && Owner.state == Net_BaseEvent.CurState.Race &&
            nTime.NetworkTimeCached >= Owner.beginRaceTime && !points.isFinish &&
            points.curPoint == point && point.gameObject.activeInHierarchy;
    }

    internal static LegacyOnlineRacePoints Find(RaceDirection point)
    {
        return point ? point.GetComponentInParent<LegacyOnlineRacePoints>() : null;
    }
}
