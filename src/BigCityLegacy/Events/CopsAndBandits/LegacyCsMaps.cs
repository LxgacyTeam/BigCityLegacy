using System;
using System.Collections.Generic;
using System.Xml;
using UnityEngine;

internal static class LegacyCsMaps
{
    internal static Transform CreateCsMap(LegacyEventsConfig.EventDef def, string savePostfix, int index)
    {
        if (savePostfix == "CS")
        {
            if (def.counter == null || def.counter.Length == 0 || def.terror == null || def.terror.Length == 0)
            {
                Debug.LogWarning("Skip CS event '" + def.name + "': need counter and terror positions");
                return null;
            }
        }
        else if (def.start == null || def.start.Length == 0)
        {
            Debug.LogWarning("Skip CS_Sur event '" + def.name + "': need start positions");
            return null;
        }
        GameObject gameObject = new GameObject(LegacyEventMapHelpers.GetSafeName(def, savePostfix, index));
        gameObject.SetActive(false);
        UI_Event_Item_Info ui_Event_Item_Info = gameObject.AddComponent<UI_Event_Item_Info>();
        ui_Event_Item_Info.txt = LegacyEventMapHelpers.GetDisplayName(def, gameObject.name);
        ui_Event_Item_Info.WinCoins = ((def.reward > 0) ? def.reward : 1000);
        Vector3[] dropZonePositions;
        if (savePostfix == "CS")
        {
            LegacyCsMaps.CreateStartPosesChild(gameObject.transform, "CS_Conter", def.counter);
            LegacyCsMaps.CreateStartPosesChild(gameObject.transform, "CS_Terror", def.terror);
            dropZonePositions = LegacyCsMaps.CombinePositions(def.counter, def.terror);
        }
        else
        {
            gameObject.AddComponent<StartPoses>().items = LegacyEventMapHelpers.ToStartItems(def.start);
            dropZonePositions = LegacyCsMaps.ToVectors(def.start);
        }

        // The drop zone is runtime-only metadata used by the server to clear old
        // weapon/medical pickups when a CS / CS_Sur round begins.
        gameObject.AddComponent<LegacyEventDropZone>().Configure(savePostfix, dropZonePositions, def.dropClearRadius);
        return gameObject.transform;
    }

    internal static void CreateStartPosesChild(Transform parent, string name, LegacyEventsConfig.PosRot[] positions)
    {
        GameObject gameObject = new GameObject(name);
        gameObject.SetActive(false);
        gameObject.transform.SetParent(parent, false);
        gameObject.AddComponent<StartPoses>().items = LegacyEventMapHelpers.ToStartItems(positions);
    }

    internal static Vector3[] ToVectors(LegacyEventsConfig.PosRot[] positions)
    {
        if (positions == null || positions.Length == 0)
        {
            return new Vector3[0];
        }

        Vector3[] result = new Vector3[positions.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            result[i] = positions[i].ToVector3();
        }
        return result;
    }

    internal static Vector3[] CombinePositions(LegacyEventsConfig.PosRot[] first, LegacyEventsConfig.PosRot[] second)
    {
        int firstCount = first == null ? 0 : first.Length;
        int secondCount = second == null ? 0 : second.Length;
        Vector3[] result = new Vector3[firstCount + secondCount];
        int index = 0;

        for (int i = 0; i < firstCount; i++)
        {
            result[index++] = first[i].ToVector3();
        }
        for (int i = 0; i < secondCount; i++)
        {
            result[index++] = second[i].ToVector3();
        }

        return result;
    }
}
