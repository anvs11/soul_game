using System;
using UnityEngine;

namespace Game.UI
{
    internal sealed class SewerEntryScreen
    {
        private readonly RectTransform panel;
        internal bool IsOpen => panel.gameObject.activeSelf;

        internal SewerEntryScreen(RectTransform root, string firearm, Action<bool> confirm, Action cancel)
        {
            panel = Stage5PreviewView.Panel("Спуск в канализацию", root);
            panel.sizeDelta = new Vector2(490, 355);
            Stage5PreviewView.Label("Условия спуска", panel, Vector2.up * 91, new Vector2(445, 140), 21).text =
                "Канализация · 4-1 → 4-5\nВозьми одно оружие и факел. Броня обнулится, рюкзак останется на крыше.\nНаграда · чертёж «Герметичный механизм»";
            Stage5PreviewView.Button($"Взять {firearm}", panel, Vector2.up * 2, () => confirm(false));
            Stage5PreviewView.Button("Взять нож", panel, Vector2.down * 59, () => confirm(true));
            Stage5PreviewView.Button("Остаться на крыше", panel, Vector2.down * 121, cancel);
            Show(false);
        }

        internal void Show(bool visible) => panel.gameObject.SetActive(visible);
    }
}
