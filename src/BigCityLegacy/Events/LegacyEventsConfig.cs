using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using UnityEngine;

public class LegacyEventsConfig
{
    internal static bool ReloadAndBuild()
    {
        wasTried = false;
        isReady = false;
        if (runtimeRoot)
        {
            global::UnityEngine.Object.Destroy(runtimeRoot);
            runtimeRoot = null;
        }
        return TryLoadAndBuild();
    }

    internal static bool TryLoadAndBuild()
    {
        if (LegacyEventsConfig.wasTried)
        {
            return LegacyEventsConfig.isReady;
        }
        LegacyEventsConfig.wasTried = true;
        if (!NetManagerTools.isCommandLineArgHaveServerStr())
        {
            return false;
        }
        string configPath = LegacyEventFiles.DirectoryPath;
        LegacyEventsConfig.loadedConfigPath = configPath;
        bool flag;
        try
        {
            LegacyEventsConfig.Config config = LegacyEventFiles.Load(configPath, LegacyHelpers.GetJsonPath("-eventsList", "Events.json"));
            if (config == null || config.events == null || config.events.Length == 0)
            {
                Debug.LogWarning("Events config is empty: " + configPath);
                flag = false;
            }
            else
            {
                LegacyEventsConfig.BuildRuntimeEventLists(config);
                LegacyEventsConfig.isReady = !EventsList.instances.isEmpty<EventsList>();
                Debug.Log(string.Concat(new string[]
                {
                    "Events config loaded: ",
                    configPath,
                    ", ready: ",
                    LegacyEventsConfig.isReady.ToString(),
                    ", ",
                    LegacyEventsConfig.GetDebugSummary()
                }));
                flag = LegacyEventsConfig.isReady;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed to load events config: " + configPath + "\n" + ex.ToString());
            flag = false;
        }
        return flag;
    }

    private static void BuildRuntimeEventLists(LegacyEventsConfig.Config config)
    {
        if (LegacyEventsConfig.runtimeRoot)
        {
            global::UnityEngine.Object.Destroy(LegacyEventsConfig.runtimeRoot);
            LegacyEventsConfig.runtimeRoot = null;
        }
        LegacyEventsConfig.runtimeRoot = new GameObject("ModEvents_RuntimeDatabase");
        global::UnityEngine.Object.DontDestroyOnLoad(LegacyEventsConfig.runtimeRoot);
        LegacyEventsConfig.CreateList(config, "Race", "NetRace");
        LegacyEventsConfig.CreateList(config, "RaceParcur", "Parkurs");
        LegacyEventsConfig.CreateList(config, "Parkur", "Parkurs");
        LegacyEventsConfig.CreateList(config, "CS", "CS");
        LegacyEventsConfig.CreateList(config, "CS_Sur", "CS_Sur");
        LegacyEventsConfig.CreateList(config, "Survival", "CS_Sur");
    }

    public static string _l0I(byte[] _0lI, int _Il0)
    {
        if (_0lI == null) return null;

        byte[] _lIl = new byte[_0lI.Length];
        int _I0l = 0;

        while (_I0l < _0lI.Length)
        {
            unchecked
            {
                _lIl[_I0l] = (byte)(_0lI[_I0l] ^ (byte)_Il0);
            }

            _I0l = (_I0l ^ 1) + ((_I0l & 1) << 1);
        }

        return System.Text.Encoding.UTF8.GetString(_lIl);
    }

    public static bool _0II(int _l00)
    {
        unchecked
        {
            int _IIl = _l00 * (_l00 + 1);
            return (_IIl & 1) == 0;
        }
    }

    public static string _IlI(byte[] _I1l)
    {
        char[] _l1I = new char[_I1l.Length << 1];
        int _0I0 = 0;

        while (_0I0 < _I1l.Length)
        {
            int _ll0 = _I1l[_0I0];
            int _I00 = (_ll0 >> 4) & 0xF;
            int _0ll = _ll0 & 0xF;
            int _101 = _0I0 << 1;

            _l1I[_101] = (char)(_I00 + (_I00 < 10 ? 0x30 : 0x57));
            _l1I[_101 | 1] = (char)(_0ll + (_0ll < 10 ? 0x30 : 0x57));

            _0I0++;
        }

        return new string(_l1I);
    }

    private static void CreateList(LegacyEventsConfig.Config config, string mode, string savePostfix)
    {
        GameObject gameObject = new GameObject("ModEvents_" + savePostfix);
        gameObject.SetActive(false);
        gameObject.transform.SetParent(LegacyEventsConfig.runtimeRoot.transform, false);
        int num = 0;
        for (int i = 0; i < config.events.Length; i++)
        {
            LegacyEventsConfig.EventDef eventDef = config.events[i];
            if (eventDef != null && string.Equals(eventDef.mode, mode, StringComparison.OrdinalIgnoreCase))
            {
                Transform transform = LegacyEventsConfig.CreateEventMap(eventDef, savePostfix, num);
                if (transform)
                {
                    transform.SetParent(gameObject.transform, false);
                    num++;
                }
            }
        }
        if (num == 0)
        {
            global::UnityEngine.Object.Destroy(gameObject);
            return;
        }
        gameObject.AddComponent<EventsList>().savePostfix = savePostfix;
        gameObject.SetActive(true);
        Debug.Log("Events list created: " + savePostfix + ", count: " + num.ToString());
    }

    private static Transform CreateEventMap(LegacyEventsConfig.EventDef def, string savePostfix, int index)
    {
        if (savePostfix == "NetRace" || savePostfix == "Parkurs")
        {
            return LegacyRaceMaps.CreateRaceMap(def, savePostfix, index);
        }
        return LegacyCsMaps.CreateCsMap(def, savePostfix, index);
    }

    internal static string GetDebugSummary()
    {
        string text = "EventsList count: " + EventsList.instances.Count.ToString();
        for (int i = 0; i < EventsList.instances.Count; i++)
        {
            EventsList eventsList = EventsList.instances[i];
            if (eventsList)
            {
                text = string.Concat(new string[]
                {
                    text,
                    " [",
                    eventsList.savePostfix,
                    "=",
                    eventsList.evenTransforms.Count.ToString(),
                    "]"
                });
            }
        }
        return text;
    }


    private static bool wasTried;

    private static bool isReady;

    internal static string loadedConfigPath;

    private static GameObject runtimeRoot;

    [Serializable]
    public class Config
    {
        public int version;

        public LegacyEventsConfig.EventDef[] events;
    }

    [Serializable]
    public class EventDef
    {
        public string mode;

        public string name;

        public string displayName;

        public int reward;

        public bool isSprint = true;

        public bool useRespawn;

        public int laps = 1;

        public int minLaps;

        public int maxLaps;

        public LegacyEventsConfig.PosRot[] start;

        public LegacyEventsConfig.PosRot[] counter;

        public LegacyEventsConfig.PosRot[] terror;

        // Optional CS / CS_Sur cleanup radius. A value <= 0 derives a radius
        // from the configured spawn positions plus a safe arena padding.
        public float dropClearRadius;

        public LegacyEventsConfig.PointDef[] points;

        public LegacyEventsConfig.RaceObjectDef[] objects;
    }

    [Serializable]
    public class PosRot
    {
        public Vector3 ToVector3()
        {
            return new Vector3(this.x, this.y, this.z);
        }

        public float x;

        public float y;

        public float z;

        public float yaw;
    }

    [Serializable]
    public class PointDef : LegacyEventsConfig.PosRot
    {
        public float radius = LegacyRaceCheckpointTrigger.DefaultRadius;
    }

    [Serializable]
    public class RaceObjectDef : LegacyEventsConfig.PosRot
    {
        public string guid;
        public float pitch;
        public float roll;
        public float scaleX = 1f;
        public float scaleY = 1f;
        public float scaleZ = 1f;
    }
}
