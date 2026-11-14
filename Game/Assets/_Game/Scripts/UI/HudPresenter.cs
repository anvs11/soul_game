using System;
using Game.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class HudPresenter : MonoBehaviour
    {
        private IPlayerCombatSource source;
        private readonly Image[] fills = new Image[3];
        private readonly Text[] labels = new Text[3];
        private RectTransform safeRoot;
        private Rect lastSafeArea;
        private Text state;
        private string hint;
        public void SetHint(string value) { hint=value; if (IsBound) Render(source.ReadSnapshot()); }
        public bool IsBound => source != null;
        public CombatSnapshot LastSnapshot { get; private set; }

        public void Bind(IPlayerCombatSource value)
        {
            Unbind();
            if (value == null) throw new ArgumentNullException(nameof(value));
            EnsureView();
            source = value;
            source.Changed += Render;
            Render(source.ReadSnapshot());
        }

        public void Unbind()
        {
            if (source != null) source.Changed -= Render;
            source = null;
            if (state != null) state.text = "HUD: герой не подключён";
            for (int i = 0; i < 3; i++)
            {
                if (fills[i] != null) fills[i].rectTransform.anchorMax = new Vector2(0, 1);
                if (labels[i] != null) labels[i].text = "—";
            }
        }

        private void OnDisable() => Unbind();
        private void OnDestroy() => Unbind();
        private void Awake() { EnsureView(); Unbind(); }
        private void Update() { if (safeRoot != null && lastSafeArea != Screen.safeArea) ApplySafeArea(); }

        private void Render(CombatSnapshot snapshot)
        {
            LastSnapshot = snapshot;
            SetBar(0, snapshot.Health, snapshot.MaxHealth, $"Здоровье {snapshot.Health:0}/{snapshot.MaxHealth:0}");
            SetBar(1, snapshot.Armor, snapshot.MaxArmor, $"Броня {snapshot.Armor:0}/{snapshot.MaxArmor:0}");
            SetBar(2, snapshot.IsMelee ? snapshot.Stamina : snapshot.Magazine,
                snapshot.IsMelee ? snapshot.MaxStamina : snapshot.MagazineCapacity,
                snapshot.IsMelee ? $"Выносливость {snapshot.Stamina:0}/{snapshot.MaxStamina:0}"
                    : $"Патроны {snapshot.Magazine} / {snapshot.ReserveAmmo}" + (snapshot.IsReloading ? " · перезарядка" : ""));
            state.text = snapshot.IsDead ? "Герой погиб. Остановите Play для нового теста."
                : hint ?? "Проверка S1-03 · WASD: движение · Space: атака · Q: смена оружия\nВыход, итог и мобильные кнопки подключаются следующим этапом.";
        }

        private void SetBar(int index, float value, float maximum, string text)
        {
            float ratio = maximum > 0 ? Mathf.Clamp01(value / maximum) : 0;
            fills[index].rectTransform.anchorMax = new Vector2(ratio, 1);
            labels[index].text = text;
        }

        private void EnsureView()
        {
            if (safeRoot != null) return;
            var canvasObject = new GameObject("CombatHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            safeRoot = Rect("SafeArea", canvasObject.transform);
            ApplySafeArea();
            Color[] colors = { new Color(.65f, .15f, .15f), new Color(.2f, .4f, .7f), new Color(.2f, .6f, .3f) };
            for (int i = 0; i < 3; i++)
            {
                var bar = Rect("Bar" + i, safeRoot);
                bar.anchorMin = bar.anchorMax = new Vector2(0, 1);
                bar.pivot = new Vector2(0, 1);
                bar.anchoredPosition = new Vector2(20, -20 - i * 38);
                bar.sizeDelta = new Vector2(340, 30);
                var background = bar.gameObject.AddComponent<Image>();
                background.color = new Color(.08f, .08f, .08f, .85f);
                background.raycastTarget = false;
                var fill = Rect("Fill", bar);
                Stretch(fill);
                fills[i] = fill.gameObject.AddComponent<Image>();
                fills[i].color = colors[i];
                fills[i].raycastTarget = false;
                labels[i] = Label("Label", bar);
                Stretch(labels[i].rectTransform);
            }
            state = Label("State", safeRoot);
            state.rectTransform.anchorMin = new Vector2(0, 0);
            state.rectTransform.anchorMax = new Vector2(1, 0);
            state.rectTransform.pivot = new Vector2(.5f, 0);
            state.rectTransform.anchoredPosition = new Vector2(0, 15);
            state.rectTransform.sizeDelta = new Vector2(-30, 65);
        }

        private void ApplySafeArea()
        {
            lastSafeArea = Screen.safeArea;
            if (Screen.width <= 0 || Screen.height <= 0) return;
            safeRoot.anchorMin = new Vector2(lastSafeArea.xMin / Screen.width, lastSafeArea.yMin / Screen.height);
            safeRoot.anchorMax = new Vector2(lastSafeArea.xMax / Screen.width, lastSafeArea.yMax / Screen.height);
            safeRoot.offsetMin = safeRoot.offsetMax = Vector2.zero;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        private static Text Label(string name, Transform parent)
        {
            var text = Rect(name, parent).gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 18; text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            return text;
        }
    }
}
