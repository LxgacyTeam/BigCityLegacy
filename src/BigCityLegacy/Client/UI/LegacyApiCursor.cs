using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

internal static class LegacyApiCursor
{
    private static readonly HashSet<int> owners=new HashSet<int>();
    private static readonly FieldInfo middle=typeof(hCursor).GetField("hiddentByMiddleMouse",BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);
    private static bool wasVisible, wasMiddle;
    private static CursorLockMode wasLock;
    internal static bool Visible { get { return owners.Count>0; } }
    internal static void Set(int owner,bool visible)
    {
        bool had=Visible;
        if(visible)
        {
            if(!had) { wasVisible=Cursor.visible;wasLock=Cursor.lockState;wasMiddle=middle!=null && (bool)middle.GetValue(null); }
            owners.Add(owner);
        }
        else owners.Remove(owner);
        if(had==Visible)return;
        if(!Visible && !wasVisible && middle!=null)middle.SetValue(null,wasMiddle);
        if(hVR.me && hVR.me.cursor)hCursor.CheckState();
        else if(Visible) { Cursor.lockState=CursorLockMode.None;Cursor.visible=true; }
        else { Cursor.lockState=wasLock;Cursor.visible=wasVisible; }
    }
    internal static void Tick()
    { if(Visible) { if(hVR.me && hVR.me.cursor && !hVR.me.cursor.gameObject.activeSelf)hCursor.CheckState();Cursor.lockState=CursorLockMode.None;Cursor.visible=true; } }
}
