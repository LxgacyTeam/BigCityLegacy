using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using BepInEx.Bootstrap;

namespace BigCityLegacy.Api
{
    [AttributeUsage(AttributeTargets.Class,Inherited=false,AllowMultiple=false)]
    public sealed class LegacyPluginActivationAttribute : Attribute
    {
        public bool Supported { get; private set; }
        public bool RequiresRestart { get; set; } = true;
        public LegacyPluginActivationAttribute(bool supported=true) { Supported=supported; }
    }
    public interface ILegacyPluginActivation
    {
        bool IsEnabled { get; }
        void Enable();
        void Disable();
    }
    public sealed class LegacyPluginStatus
    {
        public string Guid { get; internal set; }
        public string Name { get; internal set; }
        public string Version { get; internal set; }
        public bool Installed { get; internal set; }
        public bool SupportsActivation { get; internal set; }
        public bool IsEnabled { get; internal set; }
        public bool RequiresRestart { get; internal set; }
    }
    public static class LegacyPlugins
    {
        internal static ILegacyPluginActivation Resolve(string guid,out LegacyPluginStatus status)
        {
            status=new LegacyPluginStatus { Guid=guid,IsEnabled=true };
            foreach(var info in Chainloader.PluginInfos.Values)
            {
                if(info==null || info.Metadata==null || !string.Equals(info.Metadata.GUID,guid,StringComparison.OrdinalIgnoreCase))continue;
                status.Installed=true;status.Name=info.Metadata.Name;status.Version=info.Metadata.Version.ToString();
                if(info.Instance==null)return null;
                var marker=info.Instance.GetType().GetCustomAttribute<LegacyPluginActivationAttribute>(false);
                var adapter=info.Instance as ILegacyPluginActivation;
                if(marker==null || !marker.Supported || adapter==null)return null;
                status.IsEnabled=adapter.IsEnabled;status.SupportsActivation=true;status.RequiresRestart=marker.RequiresRestart;return adapter;
            }
            return null;
        }
        public static LegacyPluginStatus GetStatus(string pluginGuid)
        {
            if(string.IsNullOrWhiteSpace(pluginGuid))throw new ArgumentException("Plugin GUID is required.");
            LegacyPluginStatus result;Resolve(pluginGuid,out result);return result;
        }
        public static Task<bool> SetEnabledAsync(string pluginGuid,bool enabled,bool confirm=true)
        { return Apply(new List<LegacyActivationChange> { new LegacyActivationChange { Guid=pluginGuid,Enabled=enabled } },confirm,null,null); }
        internal static async Task<bool> Apply(List<LegacyActivationChange> changes,bool confirm,string server,Func<bool> stillValid)
        {
            var statuses=await LegacyClientThread.Invoke(()=>
            {
                var result=new List<LegacyPluginStatus>();
                foreach(var change in changes)
                {
                    LegacyPluginStatus status;
                    if(Resolve(change.Guid,out status)==null || change.Version!=null && status.Version!=change.Version)
                        throw new InvalidOperationException("Plugin activation API/version is unavailable: "+change.Guid);
                    result.Add(status);
                }
                return result;
            });
            bool restart=false;var body=new StringBuilder();var ru=new StringBuilder();
            for(int i=0;i<changes.Count;i++)
            {
                var c=changes[i];var s=statuses[i];restart|=s.RequiresRestart;
                string name=s.Name ?? c.Guid;if(name.Length>64)name=name.Substring(0,64);
                body.Append(c.Enabled?"Enable ":"Disable ").Append(name).Append(" [").Append(c.Guid).Append("]\n");
                ru.Append(c.Enabled?"Включить ":"Выключить ").Append(name).Append(" [").Append(c.Guid).Append("]\n");
            }
            if(confirm)
            {
                string prefix=server==null ? "Apply these plugin changes?\n\n" : "Server "+server+" requires these plugin changes before joining:\n\n";
                string prefixRu=server==null ? "Применить изменения плагинов?\n\n" : "Сервер "+server+" требует изменений плагинов для подключения:\n\n";
                bool yes=await LegacyDialogs.ShowAsync(new LegacyDialogOptions { Title="Plugin activation",TitleRu="Активация плагинов",
                    Message=prefix+body+(restart?"\nThe game will restart. Reconnect after restarting.":"\nReconnect after applying the changes."),
                    MessageRu=prefixRu+ru+(restart?"\nИгра будет перезапущена. После перезапуска подключитесь повторно.":"\nПосле применения подключитесь повторно."),
                    YesBtnText="Apply",YesBtnTextRu="Применить",YesBtnType=LegacyDialogButtonType.Green });
                if(!yes)return false;
            }
            await LegacyClientThread.Invoke(()=>
            {
                if(stillValid!=null && !stillValid())throw new InvalidOperationException("Server request expired after a new connection attempt.");
                // Validate the whole batch again before invoking any plugin method.
                var adapters=new List<ILegacyPluginActivation>();
                foreach(var c in changes)
                {
                    LegacyPluginStatus s;var adapter=Resolve(c.Guid,out s);
                    if(adapter==null || c.Version!=null && s.Version!=c.Version)throw new InvalidOperationException("Plugin changed while confirmation was open: "+c.Guid);
                    adapters.Add(adapter);
                }
                for(int i=0;i<changes.Count;i++)
                {
                    var c=changes[i];var adapter=adapters[i];
                    if(adapter.IsEnabled!=c.Enabled) { if(c.Enabled)adapter.Enable();else adapter.Disable(); }
                    if(!statuses[i].RequiresRestart && adapter.IsEnabled!=c.Enabled)throw new InvalidOperationException("Plugin did not apply its activation state: "+c.Guid);
                }
                return true;
            });
            if(restart)return await LegacyGame.RestartAsync(false);
            return true;
        }
    }
}
