using System;
using System.Collections.Generic;
using BigCityLegacy.Api;
using UnityEngine;

internal static class LegacyPluginControlClient
{
    private static int generation;
    private static bool busy;
    private static readonly HashSet<uint> seen=new HashSet<uint>();
    internal static void NewConnection() { generation++;seen.Clear(); }
    internal static async void Receive(uint id,List<LegacyActivationChange> changes,string server)
    {
        if(busy || seen.Count>=32 || !seen.Add(id))return;
        busy=true;int requestGeneration=generation;
        try { await LegacyPlugins.Apply(changes,true,server,()=>generation==requestGeneration); }
        catch(Exception ex)
        {
            Debug.LogError("[BCL activation] "+ex);
            LegacyAlerts.ShowLocalized("Plugin changes could not be completed. Check plugin settings before reconnecting.\n"+ex.Message,
                "Не удалось завершить изменения плагинов. Перед подключением проверьте их настройки.\n"+ex.Message,title:"Plugin activation",titleRu:"Активация плагинов");
        }
        finally { busy=false; }
    }
}
