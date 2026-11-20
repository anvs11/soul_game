using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    internal sealed class Stage5PreviewView
    {
        private readonly Text balance, prompt, effect, offer, error;
        private readonly RectTransform dialog;

        public Stage5PreviewView(RectTransform safe, Action accept, Action decline)
        {
            balance = Label("Баланс", safe, Vector2.up * -28, new Vector2(390, 47), 17);
            balance.rectTransform.anchorMin = balance.rectTransform.anchorMax = new Vector2(.5f, 1);
            effect = Label("Эффекты", safe, Vector2.up * -68, new Vector2(420, 30), 16);
            effect.rectTransform.anchorMin = effect.rectTransform.anchorMax = new Vector2(.5f, 1);
            prompt = Label("Подсказка NPC", safe, Vector2.up * 72, new Vector2(440, 55), 20);
            prompt.rectTransform.anchorMin = prompt.rectTransform.anchorMax = new Vector2(.5f, 0);

            dialog = Panel("Диалог NPC", safe);
            offer = Label("Предложение", dialog, Vector2.up * 83, new Vector2(420, 84), 24);
            error = Label("Ответ", dialog, Vector2.up * 12, new Vector2(410, 35), 19);
            Button("Согласиться · Tab", dialog, new Vector2(0, -42), accept);
            Button("Отказаться · E", dialog, new Vector2(0, -103), decline);
            dialog.gameObject.SetActive(false);
        }

        public void Render(int coins, int pending, int confirmed, string hint, string effects, string question, string notice)
        {
            balance.text = $"Монеты {coins}   ·   Кристаллы {confirmed}" + (pending > 0 ? $"  (+{pending})" : "");
            prompt.text = question == null ? notice ?? hint : "";
            effect.text = effects;
            dialog.gameObject.SetActive(question != null);
            offer.text = question;
            error.text = notice;
        }

        internal static RectTransform Panel(string name, Transform parent)
        {
            var panel = Rect(name, parent, Vector2.zero, new Vector2(475, 320));
            panel.gameObject.AddComponent<Image>().color = new Color(.075f, .095f, .10f, .99f);
            return panel;
        }

        internal static Text Label(string name, Transform parent, Vector2 position, Vector2 size, int fontSize)
        {
            var text = Rect(name, parent, position, size).gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(.95f, .93f, .88f);
            text.raycastTarget = false;
            return text;
        }

        internal static Button Button(string title, Transform parent, Vector2 position, Action action)
        {
            var node = Rect(title, parent, position, new Vector2(340, 50));
            var image = node.gameObject.AddComponent<Image>();
            image.color = new Color(.24f, .34f, .30f);
            var button = node.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => action());
            Label("Текст", node, Vector2.zero, node.sizeDelta, 19).text = title;
            return button;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var node = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            node.SetParent(parent, false);
            node.anchoredPosition = position;
            node.sizeDelta = size;
            return node;
        }
    }
}
