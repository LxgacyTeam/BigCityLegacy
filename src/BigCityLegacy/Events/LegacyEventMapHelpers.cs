using System;
using System.Collections.Generic;
using System.Xml;
using UnityEngine;

internal static class LegacyEventMapHelpers
{
    internal static StartPoses.Item[] ToStartItems(LegacyEventsConfig.PosRot[] positions)
    {
        if (positions == null)
        {
            return new StartPoses.Item[0];
        }
        StartPoses.Item[] array = new StartPoses.Item[positions.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            array[i].pos = positions[i].ToVector3();
            array[i].rot = new Vector3(0f, positions[i].yaw, 0f);
        }
        return array;
    }

    internal static string GetSafeName(LegacyEventsConfig.EventDef def, string savePostfix, int index)
    {
        if (def != null && !string.IsNullOrEmpty(def.name))
        {
            return def.name;
        }
        return savePostfix + "_" + index.ToString("00");
    }

    internal static string GetDisplayName(LegacyEventsConfig.EventDef def, string fallback)
    {
        if (def != null && !string.IsNullOrEmpty(def.displayName))
        {
            return def.displayName;
        }
        if (def != null && !string.IsNullOrEmpty(def.name))
        {
            return def.name;
        }
        return fallback;
    }
}
