using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    internal sealed class Stage5PreviewView
    {
        private readonly Text balance, prompt, effect, offer, error;
        private readonly RectTransform dialog;
        private readonly RectTransform supplyPanel;
        private readonly Text supplyTitle, backpackLabel, ammoLabel, supplySecondaryLabel;
        private readonly Button applyMedkit, storeMedkit;
        private readonly Text rewardTitle, rewardDescription;
        private readonly Button retrySave;

        public Stage5PreviewView(RectTransform safe, Action accept, Action decline,
            Action backpack, Action apply, Action store, Action closeSupply, Action retry)
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

            var backpackButton = Button("Рюкзак · B", safe, Vector2.down * 113, backpack);
            var backpackRect = (RectTransform)backpackButton.transform;
            backpackRect.anchorMin = backpackRect.anchorMax = new Vector2(.5f, 1);
            backpackRect.sizeDelta = new Vector2(220, 38);
            backpackLabel = backpackButton.GetComponentInChildren<Text>();
            supplyPanel = Panel("Аптечки", safe);
            supplyTitle = Label("Найденное снабжение", supplyPanel, Vector2.up * 118, new Vector2(420, 40), 24);
            ammoLabel = Label("Патроны в рюкзаке", supplyPanel, Vector2.up * 76, new Vector2(420, 45), 18);
            applyMedkit = Button("Применить · Tab", supplyPanel, Vector2.up * 18, apply);
            storeMedkit = Button("В рюкзак · E", supplyPanel, Vector2.down * 36, store);
            supplySecondaryLabel = storeMedkit.GetComponentInChildren<Text>();
            Button("Закрыть · B", supplyPanel, Vector2.down * 101, closeSupply);
            supplyPanel.gameObject.SetActive(false);

            rewardTitle = Label("Название находки", safe, Vector2.up * 164, new Vector2(370, 36), 23);
            rewardTitle.rectTransform.anchorMin = rewardTitle.rectTransform.anchorMax = new Vector2(.5f, 0);
            var detail = Rect("Карточка находки", safe, Vector2.up * 119, new Vector2(370, 45));
            detail.anchorMin = detail.anchorMax = new Vector2(.5f, 0);
            var backing = detail.gameObject.AddComponent<Image>();
            backing.color = new Color(.075f, .095f, .10f, .94f);
            backing.raycastTarget = false;
            rewardDescription = Label("Описание находки", detail, Vector2.zero, new Vector2(355, 40), 17);
            RenderReward(null, null);
            retrySave = Button("Повторить сохранение", safe, Vector2.zero, retry);
            retrySave.gameObject.SetActive(false);
        }

        public void RenderSaveRetry(string error) => retrySave.gameObject.SetActive(error != null);

        public void RenderReward(string title, string description)
        {
            rewardTitle.gameObject.SetActive(title != null);
            rewardDescription.transform.parent.gameObject.SetActive(title != null);
            rewardTitle.text = title;
            rewardDescription.text = description;
        }

        public void RenderSupplies(bool visible, bool open, bool found, int medkits, bool canHeal,
            string ammo, bool hasCompatibleAmmo)
        {
            backpackLabel.transform.parent.gameObject.SetActive(visible);
            backpackLabel.text = $"Рюкзак · B · {medkits}";
            supplyPanel.gameObject.SetActive(visible && open);
            supplyTitle.text = found ? "Найдена аптечка на 10 HP" : $"В рюкзаке аптечек: {medkits}";
            applyMedkit.interactable = canHeal;
            ammoLabel.gameObject.SetActive(!found && ammo != null);
            ammoLabel.text = ammo;
            storeMedkit.gameObject.SetActive(found || ammo != null);
            storeMedkit.interactable = found || hasCompatibleAmmo;
            supplySecondaryLabel.text = found ? "В рюкзак · E" : "Патроны в резерв · E";
        }

        public void Render(int coins, int pending, int confirmed, string hint, string effects, string question, string notice)
        {
            balance.text = $"Монеты {coins}   ·   Кристаллы {confirmed}" + (pending > 0 ? $"  (+{pending})" : "");
            prompt.text = question == null && !supplyPanel.gameObject.activeSelf
                ? string.IsNullOrEmpty(hint) ? notice : string.IsNullOrEmpty(notice) ? hint : hint + "\n" + notice
                : "";
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
