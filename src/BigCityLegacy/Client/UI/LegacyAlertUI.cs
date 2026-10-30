using BigCityLegacy.Api;
using BigCityLegacy.UI;
using UnityEngine;

// Persistent client UI. Welcome is delivered here only after world/respawn/fade readiness.
internal sealed class LegacyAlertUI : MonoBehaviour
{
    private LegacyAlertMessage modal, hint;
    private LegacyUIWindow window;
    private LegacyUIScrollView scroll;
    private LegacyUIHintBox hintBox;
    private float hintUntil;
    private bool modalHidden, hintHidden;
    private GUIStyle textStyle;
    private readonly System.Collections.Generic.Queue<LegacyAlertMessage> dialogs = new System.Collections.Generic.Queue<LegacyAlertMessage>();
    internal static bool ModalVisible { get; private set; }
    private static bool Russian { get { return LegacyLocalizer.IsRussianLanguage; } }
    private void RefreshCursor()
    {
        bool visible = window != null && window.IsRendered;
        if (ModalVisible == visible) return;
        ModalVisible = visible;
        if (hVR.me && hVR.me.cursor) hCursor.CheckState();
    }
    private static bool Accept(LegacyAlertMessage alert)
    {
        return !alert.IsWelcome || LegacyWelcomeClient.IsCurrent(alert) && LegacyWelcomePreferences.ShouldShow(alert.ServerKey, alert.UpdatedAt);
    }
    private void Update()
    {
        if (modal != null && modal.IsWelcome && !LegacyWelcomeClient.IsCurrent(modal)) window.Close();
        if (hint != null && hint.IsWelcome && !LegacyWelcomeClient.IsCurrent(hint)) hintBox.Visible = false;
        if (hint != null && Time.realtimeSinceStartup >= hintUntil) hintBox.Visible = false;
        if (hintBox != null && !hintBox.IsRendered) { hint = null; hintBox = null; }
        if (window != null && !window.IsRendered) modal = null;
        LegacyAlertMessage queued;
        while ((queued = LegacyAlerts.Take()) != null)
        {
            if (!Accept(queued)) continue;
            if (queued.Display == LegacyAlertDisplay.Hint)
            {
                hint = queued; hintHidden = false; hintUntil = Time.realtimeSinceStartup + queued.Duration;
                hintBox = new LegacyUIHintBox(new Rect(), new LegacyUIHintBoxOptions { ScaleMultiplier = 1.5f,
                    InputBlockMode = queued.IsWelcome ? LegacyUIInputBlockMode.Window : LegacyUIInputBlockMode.None });
            }
            else { if (dialogs.Count >= 32) dialogs.Dequeue(); dialogs.Enqueue(queued); }
        }
        RefreshCursor();
        if (modal != null || window != null && window.IsRendered) return;
        while (dialogs.Count > 0)
        {
            var alert = dialogs.Dequeue(); if (!Accept(alert)) continue;
            modal = alert; modalHidden = false;
            if (System.Math.Max(modal.Message.Length, (modal.MessageRu ?? "").Length) > 1000 && modal.Display == LegacyAlertDisplay.Small) modal.Display = LegacyAlertDisplay.Large;
            window = new LegacyUIWindow(alert.Caption(Russian), new Rect(100, 100, 540, 300),
                new LegacyUIWindowOptions { ShowCloseButton = true, InputBlockMode = LegacyUIInputBlockMode.Screen });
            scroll = new LegacyUIScrollView(new Rect()) { ShowHorizontalScrollbar = false, Padding = 8f };
            RefreshCursor(); break;
        }
    }
    private void WelcomeToggle(Rect rect, LegacyAlertMessage message, ref bool value)
    {
        bool next = LegacyUI.Toggle(rect, value, LegacyLocalizer.Text("Don't show again", "Не показывать больше"));
        if (next != value) { value = next; LegacyWelcomePreferences.SetHidden(message.ServerKey, message.UpdatedAt, value); }
    }
    private void DrawHint()
    {
        Vector2 screen = LegacyUI.VirtualScreenSize / hintBox.Options.ScaleMultiplier;
        string body = hint.Text(Russian), title = hint.Caption(Russian);
        float width = Mathf.Min(650f, screen.x - 24f);
        float titleHeight = string.IsNullOrEmpty(title) ? 0f : textStyle.CalcHeight(new GUIContent(title), Mathf.Max(1f,width-24f)) + 8f;
        float height = textStyle.CalcHeight(new GUIContent(body), Mathf.Max(1f,width-24f)) + 24f + titleHeight + (hint.IsWelcome ? 28f : 0f);
        height = Mathf.Min(screen.y*.65f,height);
        hintBox.Rect = new Rect((screen.x-width)*.5f,16f,width,height);
        hintBox.Draw("", area =>
        {
            if (titleHeight > 0f) GUI.Label(new Rect(area.x,area.y,area.width,titleHeight), title, new GUIStyle(textStyle) { fontStyle = FontStyle.Bold });
            GUI.Label(new Rect(area.x,area.y+titleHeight,area.width,Mathf.Max(1f,area.height-titleHeight-(hint.IsWelcome ? 28f : 0f))),body,textStyle);
            if (hint.IsWelcome) WelcomeToggle(new Rect(area.x,area.yMax-24f,area.width,24f),hint,ref hintHidden);
        });
    }
    private void OnGUI()
    {
        if (hint == null && (window == null || !window.IsRendered)) return;
        using (LegacyClientUI.Scope(-28000))
        {
            textStyle = new GUIStyle(LegacyUI.Styles.Label) { wordWrap = true, richText = false, alignment = TextAnchor.UpperLeft };
            if (hint != null) DrawHint();
            if (window == null || !window.IsRendered || modal == null) return;
            string text = modal.Text(Russian), title = modal.Caption(Russian);
            window.Title = string.IsNullOrEmpty(title) ? LegacyLocalizer.Text("Notification","Уведомление") : title;
            if (!window.IsDragging)
            {
                Vector2 screen = LegacyUI.VirtualScreenSize;
                float width = Mathf.Min(modal.Display == LegacyAlertDisplay.Large ? 680f : 540f,screen.x-16f);
                float content = textStyle.CalcHeight(new GUIContent(text),Mathf.Max(1f,width-64f));
                float height = modal.Display == LegacyAlertDisplay.Large ? Mathf.Min(500f,screen.y-16f) : Mathf.Min(content+116f+(modal.IsWelcome?28f:0f),Mathf.Min(420f,screen.y-16f));
                height = Mathf.Max(150f,height);
                if (window.Rect.width != width || window.Rect.height != height) window.Rect = new Rect((screen.x-width)*.5f,(screen.y-height)*.5f,width,height);
            }
            window.Draw(area =>
            {
                float footer = modal.IsWelcome ? 66f : 38f;
                Rect body = new Rect(area.x,area.y,area.width,Mathf.Max(10f,area.height-footer));
                float required = textStyle.CalcHeight(new GUIContent(text),Mathf.Max(1f,body.width-32));
                if (modal.Display == LegacyAlertDisplay.Large || required > body.height-20f)
                {
                    scroll.ViewRect = body; scroll.ContentRect = new Rect(0,0,Mathf.Max(1f,body.width-18f),required+16f);
                    scroll.Draw(content => GUI.Label(new Rect(content.x,content.y,content.width,required),text,textStyle));
                }
                else { LegacyUI.HintBox(body,""); GUI.Label(new Rect(body.x+10,body.y+10,body.width-20,body.height-20),text,textStyle); }
                if (modal.IsWelcome) WelcomeToggle(new Rect(area.x,area.yMax-60f,area.width,24f),modal,ref modalHidden);
                if (LegacyUI.Button(new Rect(area.xMax-110,area.yMax-30,110,28),LegacyLocalizer.Text("Close","Закрыть"))) window.Close();
            });
        }
    }
    private void OnDisable() { ModalVisible = false; if (hVR.me && hVR.me.cursor) hCursor.CheckState(); }
}
