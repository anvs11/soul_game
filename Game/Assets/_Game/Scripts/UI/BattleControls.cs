using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;
namespace Game.UI
{
    public sealed class BattleControls : MonoBehaviour
    {
        private RectTransform safe;
        private GameObject gameplay, menu;
        private Rect lastSafe;
        private Action pause, resume, leave;
        public OnScreenStick Stick { get; private set; }
        public OnScreenButton Attack { get; private set; }
        public OnScreenButton Dodge { get; private set; }
        public OnScreenButton PauseControl { get; private set; }
        public Button ResumeButton { get; private set; }
        public Button HomeButton { get; private set; }
        public void Initialize(Action pauseAction, Action resumeAction, Action leaveAction, bool enableDodge = false)
        {
            pause=pauseAction; resume=resumeAction; leave=leaveAction;
            if(safe!=null)return;
            var canvas=new GameObject("BattleControlsCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvas.transform.SetParent(transform,false);
            canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<Canvas>().sortingOrder=20;
            var scaler=canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1280,720); scaler.matchWidthOrHeight=.5f;
            safe=Rect("SafeArea",canvas.transform); ApplySafe();
            gameplay=Rect("Gameplay",safe).gameObject; Stretch((RectTransform)gameplay.transform);
            var baseRect=Box("StickBase",gameplay.transform,new Vector2(0,0),new Vector2(135,135),new Vector2(180,180),new Color(.1f,.25f,.23f,.65f));
            var handle=Box("Stick",baseRect,new Vector2(.5f,.5f),Vector2.zero,new Vector2(100,100),new Color(.3f,.6f,.5f,.8f));
            Stick=handle.gameObject.AddComponent<OnScreenStick>(); Stick.controlPath="<Gamepad>/leftStick"; Stick.movementRange=55;
            Attack=Control("Огонь",new Vector2(1,0),new Vector2(-100,120),new Vector2(120,120),"<Gamepad>/buttonSouth");
            if (enableDodge)
                Dodge = Control("Кувырок", new Vector2(1, 0), new Vector2(-100, 237),
                    new Vector2(120, 80), "<Gamepad>/rightShoulder");
            Control("Оружие",new Vector2(1,0),new Vector2(-245,190),new Vector2(110,80),"<Gamepad>/buttonNorth");
            Control("Взять / E",new Vector2(1,0),new Vector2(-245,80),new Vector2(110,80),"<Gamepad>/buttonWest");
            PauseControl=Control("Пауза",new Vector2(1,1),new Vector2(-65,-45),new Vector2(100,60),"<Gamepad>/start");
            menu=Box("PauseMenu",safe,new Vector2(.5f,.5f),Vector2.zero,new Vector2(550,360),new Color(.04f,.08f,.09f,.98f)).gameObject;
            var title=Rect("Title",menu.transform); title.anchoredPosition=new Vector2(0,110); title.sizeDelta=new Vector2(450,70); TextLabel(title,"Пауза");
            ResumeButton=MenuButton("Продолжить",menu.transform,new Vector2(0,20),()=>resume?.Invoke());
            HomeButton=MenuButton("Выйти в убежище",menu.transform,new Vector2(0,-90),()=>leave?.Invoke());
            menu.SetActive(false);
        }
        public void SetPaused(bool value) { ClearHeld(); if(menu!=null)menu.SetActive(value); if(gameplay!=null)gameplay.SetActive(!value); }
        public void ClearHeld()
        {
            if(gameplay==null || EventSystem.current==null)return;
            var data=new PointerEventData(EventSystem.current);
            foreach(var button in gameplay.GetComponentsInChildren<OnScreenButton>()) button.OnPointerUp(data);
            if(Stick!=null && Stick.isActiveAndEnabled)Stick.OnPointerUp(data);
        }
        private void OnApplicationFocus(bool value) { if(!value)ClearHeld(); }
        private void OnApplicationPause(bool value) { if(value)ClearHeld(); }
        private void OnDisable() => ClearHeld();
        private void Update() { if(safe!=null && lastSafe!=Screen.safeArea)ApplySafe(); }
        private void ApplySafe()
        {
            lastSafe=Screen.safeArea; if(Screen.width<=0 || Screen.height<=0)return;
            safe.anchorMin=new Vector2(lastSafe.xMin/Screen.width,lastSafe.yMin/Screen.height);
            safe.anchorMax=new Vector2(lastSafe.xMax/Screen.width,lastSafe.yMax/Screen.height); safe.offsetMin=safe.offsetMax=Vector2.zero;
        }
        private OnScreenButton Control(string label,Vector2 anchor,Vector2 position,Vector2 size,string path)
        {
            var rect=Box(label,gameplay.transform,anchor,position,size,new Color(.15f,.35f,.3f,.8f)); TextLabel(rect,label);
            var button=rect.gameObject.AddComponent<OnScreenButton>(); button.controlPath=path; return button;
        }
        private static Button MenuButton(string label,Transform parent,Vector2 position,Action action)
        {
            var rect=Box(label,parent,new Vector2(.5f,.5f),position,new Vector2(440,75),new Color(.15f,.4f,.3f)); TextLabel(rect,label);
            var button=rect.gameObject.AddComponent<Button>(); NoNavigation(button); button.onClick.AddListener(()=>action()); return button;
        }
        private static void NoNavigation(Button button) { button.navigation=new Navigation{mode=Navigation.Mode.None}; }
        private static RectTransform Rect(string name,Transform parent)
        {
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f); return rect;
        }
        private static void Stretch(RectTransform rect) { rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero; }
        private static RectTransform Box(string name,Transform parent,Vector2 anchor,Vector2 position,Vector2 size,Color color)
        {
            var rect=Rect(name,parent);rect.anchorMin=rect.anchorMax=anchor;rect.anchoredPosition=position;rect.sizeDelta=size;
            rect.gameObject.AddComponent<Image>().color=color;return rect;
        }
        private static void TextLabel(RectTransform parent,string label)
        {
            var rect=Rect("Label",parent);Stretch(rect);var text=rect.gameObject.AddComponent<Text>();
            text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.text=label;text.fontSize=23;text.alignment=TextAnchor.MiddleCenter;text.color=Color.white;text.raycastTarget=false;
        }
    }
}
