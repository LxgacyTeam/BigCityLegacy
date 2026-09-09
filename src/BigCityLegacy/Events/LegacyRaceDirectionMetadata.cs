using System;
using UnityEngine;

public sealed class LegacyRaceDirectionMetadata : MonoBehaviour
{
    public int onlineLapsCount;
    public int onlineMinLapsCount;
    public int onlineMaxLapsCount;

    public static LegacyRaceDirectionMetadata Get(RaceDirectionPoints points)
    {
        if (!points)
        {
            return null;
        }
        LegacyRaceDirectionMetadata data = points.GetComponent<LegacyRaceDirectionMetadata>();
        if (!data)
        {
            data = points.gameObject.AddComponent<LegacyRaceDirectionMetadata>();
        }
        return data;
    }

    public static int GetOnlineLapsCount(RaceDirectionPoints points)
    {
        if (!points)
        {
            return 0;
        }
        if (points.isSprint)
        {
            return 1;
        }
        LegacyRaceDirectionMetadata data = points.GetComponent<LegacyRaceDirectionMetadata>();
        if (!data)
        {
            return 0;
        }
        if (data.onlineLapsCount > 0)
        {
            return Math.Max(1, data.onlineLapsCount);
        }
        if (data.onlineMinLapsCount > 0 || data.onlineMaxLapsCount > 0)
        {
            int min = Math.Max(1, data.onlineMinLapsCount);
            int max = Math.Max(min, data.onlineMaxLapsCount);
            return mRandom.Range(min, max + 1);
        }
        return 0;
    }
}
