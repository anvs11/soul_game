using UnityEngine;
using UnityEngine.UI;

namespace Game.StreetMap
{
    public sealed class StreetMiniMap : MonoBehaviour
    {
        private RectTransform safe, map, marker;
        private readonly Image[] rooms = new Image[5];
        private StreetMapScreen source;
        private Rect lastSafe;
        public RectTransform SafeRoot => safe;
        public Vector2 HeroMapPosition => marker.anchoredPosition;
        public void Initialize(StreetMapScreen screen)
        {
            source=screen;
            var canvas=new GameObject("MapCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
            canvas.transform.SetParent(transform,false); canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<Canvas>().sortingOrder=5;
            var scaler=canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1280,720); scaler.matchWidthOrHeight=.5f;
            safe=Rect("SafeArea",canvas.transform); ApplySafe();
            map=Rect("FullStreetMap",safe); map.anchorMin=map.anchorMax=map.pivot=new Vector2(1,1);
            map.anchoredPosition=new Vector2(-20,-90); map.sizeDelta=new Vector2(280,140);
            var background=map.gameObject.AddComponent<Image>(); background.color=new Color(.02f,.04f,.05f,.85f); background.raycastTarget=false;
            foreach(var corridor in StreetMapLayout.Corridors) Paint("Road",corridor.center,corridor.size,new Color(.35f,.4f,.4f));
            for(int i=0;i<5;i++) rooms[i]=Paint(StreetMapLayout.Rooms[i].Id,StreetMapLayout.Rooms[i].Center,StreetMapLayout.Rooms[i].Bounds.size,Color.gray);
            marker=Paint("You",screen.Player.transform.position,new Vector2(2,2),Color.yellow).rectTransform;
            var label=Rect("Legend",map); label.anchorMin=label.anchorMax=new Vector2(.5f,0);label.anchoredPosition=new Vector2(0,-12);label.sizeDelta=new Vector2(280,24);
            var text=label.gameObject.AddComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=14;text.color=Color.white;text.text="Карта · жёлтая точка — вы";text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;
        }
        public static Vector2 Project(Vector2 world) => (world-StreetMapLayout.CameraBounds.center)*2.4f;
        private Image Paint(string name,Vector2 center,Vector2 size,Color color)
        {
            var rect=Rect(name,map);rect.anchoredPosition=Project(center);rect.sizeDelta=size*2.4f;
            var image=rect.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;return image;
        }
        private static RectTransform Rect(string name,Transform parent)
        {var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);return rect;}
        private void LateUpdate()
        {
            if(source==null || marker==null)return;
            if(lastSafe!=Screen.safeArea)ApplySafe();
            marker.anchoredPosition=Project(source.MiniMapPosition);
            for(int i=0;i<rooms.Length;i++) rooms[i].color=source.HasVisited(i)?new Color(.25f,.7f,.6f):new Color(.35f,.4f,.45f);
        }
        private void ApplySafe()
        {
            lastSafe=Screen.safeArea;if(Screen.width<=0 || Screen.height<=0)return;
            safe.anchorMin=new Vector2(lastSafe.xMin/Screen.width,lastSafe.yMin/Screen.height);
            safe.anchorMax=new Vector2(lastSafe.xMax/Screen.width,lastSafe.yMax/Screen.height);safe.offsetMin=safe.offsetMax=Vector2.zero;
        }
    }
}
