using BigCityLegacy.Api;
using BigCityLegacy.UI;
using UnityEngine;

internal sealed class LegacyDialogUI : MonoBehaviour
{
    private LegacyDialogs.Request request;
    private LegacyUIWindow window;
    private LegacyUIScrollView scroll;
    private bool result;
    private void Update()
    {
        LegacyClientThread.Tick();
        if(request!=null && !window.IsRendered) { var done=request;request=null;done.Completion.TrySetResult(result); }
        if(request==null && (request=LegacyDialogs.Take())!=null)
        {
            result=false;
            window=new LegacyUIWindow("",new Rect(100,100,560,300),new LegacyUIWindowOptions { ShowCloseButton=true,InputBlockMode=LegacyUIInputBlockMode.Window });
            scroll=new LegacyUIScrollView(new Rect()) { ShowHorizontalScrollbar=false,Padding=8f };
        }
        LegacyApiCursor.Set(GetInstanceID(),request!=null && window.IsRendered);
    }
    private void LateUpdate() { LegacyApiCursor.Tick(); }
    private static string Text(string main,string ru) { return LegacyLocalizer.IsRussianLanguage && !string.IsNullOrEmpty(ru) ? ru : main ?? ""; }
    private static bool Button(Rect rect,string text,LegacyDialogButtonType type)
    { return type==LegacyDialogButtonType.Green ? LegacyUI.GreenButton(rect,text) : type==LegacyDialogButtonType.Danger ? LegacyUI.DangerButton(rect,text) : LegacyUI.Button(rect,text); }
    private void OnGUI()
    {
        if(request==null || !window.IsRendered)return;
        var options=request.Options;
        using(LegacyClientUI.Scope(-29000))
        {
            GUI.enabled=window.Visible;
            string message=Text(options.Message,options.MessageRu);
            var style=new GUIStyle(LegacyUI.Styles.Label) { wordWrap=true,richText=false,alignment=TextAnchor.UpperLeft };
            Vector2 screen=LegacyUI.VirtualScreenSize;float width=Mathf.Min(600,screen.x-16);
            float required=style.CalcHeight(new GUIContent(message),Mathf.Max(1,width-64));
            float height=Mathf.Max(150,Mathf.Min(required+125,Mathf.Min(500,screen.y-16)));
            if(window.Rect.width!=width || window.Rect.height!=height)window.Rect=new Rect((screen.x-width)/2,(screen.y-height)/2,width,height);
            window.Title=Text(options.Title,options.TitleRu);
            window.Draw(area=>
            {
                Rect body=new Rect(area.x,area.y,area.width,Mathf.Max(20,area.height-42));
                LegacyUI.HintBox(body,"");
                if(required>body.height-20)
                { scroll.ViewRect=body;scroll.ContentRect=new Rect(0,0,Mathf.Max(1,body.width-18),required+16);scroll.Draw(c=>GUI.Label(new Rect(c.x,c.y,c.width,required),message,style)); }
                else GUI.Label(new Rect(body.x+10,body.y+10,body.width-20,body.height-20),message,style);
                float buttonWidth=Mathf.Max(1,(area.width-10)/2);
                if(Button(new Rect(area.x,area.yMax-30,buttonWidth,28),Text(options.NoBtnText ?? LegacyLocalizer.Text("No","Нет"),options.NoBtnTextRu),options.NoBtnType)) { result=false;window.Close(); }
                if(Button(new Rect(area.x+buttonWidth+10,area.yMax-30,buttonWidth,28),Text(options.YesBtnText ?? LegacyLocalizer.Text("Yes","Да"),options.YesBtnTextRu),options.YesBtnType)) { result=true;window.Close(); }
            });
        }
    }
    private void OnDisable()
    { if(window!=null)window.Close();if(request!=null)request.Completion.TrySetResult(false);request=null;LegacyDialogs.CancelPending();LegacyApiCursor.Set(GetInstanceID(),false); }
}
