using System;
using Game.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    public abstract class FlowScreen : MonoBehaviour
    {
        private RunCoordinator coordinator;
        private bool subscribed;
        private Text status;
        public Button PrimaryButton { get; private set; }
        protected abstract string Title { get; }
        protected abstract string Description { get; }
        protected abstract string ButtonLabel { get; }

        public void Bind(RunCoordinator value)
        {
            Detach();
            coordinator = value ?? throw new ArgumentNullException(nameof(value));
            if (PrimaryButton == null) CreateView();
            Attach();
            Refresh();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(PrimaryButton.gameObject);
        }

        private void OnEnable()
        {
            Attach();
            Refresh();
        }

        private void OnDisable() => Detach();
        private void OnDestroy()
        {
            Detach();
            coordinator = null;
        }

        private void Attach()
        {
            if (coordinator == null || subscribed || !isActiveAndEnabled) return;
            coordinator.Changed += Refresh;
            subscribed = true;
        }

        private void Detach()
        {
            if (coordinator != null && subscribed) coordinator.Changed -= Refresh;
            subscribed = false;
        }

        private void Refresh()
        {
            if (status == null || coordinator == null) return;
            bool failed = coordinator.Phase == RunPhase.Faulted;
            PrimaryButton.interactable = failed || CanAct(coordinator);
            PrimaryButton.GetComponentInChildren<Text>().text = failed ? "Повторить переход домой" : ButtonLabel;
            status.text = failed
                ? "Не удалось перейти между сценами.\n" + coordinator.Error
                : Message(coordinator);
        }

        private void OnPrimary()
        {
            if (coordinator == null) return;
            if (coordinator.Phase == RunPhase.Faulted) coordinator.RetryHome();
            else if (CanAct(coordinator)) Act(coordinator);
        }

        protected abstract bool CanAct(RunCoordinator value);
        protected abstract void Act(RunCoordinator value);
        protected abstract string Message(RunCoordinator value);

        private void CreateView()
        {
            var canvasObject = new GameObject("ScreenCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;

            var background = Rect("Background", canvasObject.transform);
            background.anchorMin = Vector2.zero;
            background.anchorMax = Vector2.one;
            background.sizeDelta = Vector2.zero;
            background.gameObject.AddComponent<Image>().color = new Color(0.055f, 0.08f, 0.095f, 0.96f);

            var panel = Rect("Content", background);
            panel.sizeDelta = new Vector2(880, 540);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Label("Title", panel, font, Title, 44, new Vector2(0, 160), new Vector2(840, 80));
            Label("Description", panel, font, Description, 25, new Vector2(0, 55), new Vector2(840, 140));
            status = Label("Status", panel, font, "", 20, new Vector2(0, -70), new Vector2(840, 95));

            var buttonRect = Rect("PrimaryButton", panel);
            buttonRect.anchoredPosition = new Vector2(0, -190);
            buttonRect.sizeDelta = new Vector2(540, 70);
            var image = buttonRect.gameObject.AddComponent<Image>();
            image.color = new Color(0.12f, 0.47f, 0.35f);
            PrimaryButton = buttonRect.gameObject.AddComponent<Button>();
            PrimaryButton.targetGraphic = image;
            PrimaryButton.onClick.AddListener(OnPrimary);
            Label("Label", buttonRect, font, ButtonLabel, 25, Vector2.zero, buttonRect.sizeDelta);
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            return rect;
        }

        private static Text Label(string name, Transform parent, Font font, string text,
            int fontSize, Vector2 position, Vector2 size)
        {
            var rect = Rect(name, parent);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var label = rect.gameObject.AddComponent<Text>();
            label.font = font;
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.92f, 0.95f, 0.94f);
            label.raycastTarget = false;
            return label;
        }
    }
}
