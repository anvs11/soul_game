using Game.Actors;
using Game.Core;
using Game.Input;
using Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game.StreetMap
{
    internal sealed class CityTutorial : MonoBehaviour
    {
        private static readonly string[] Lessons =
        {
            "Двигайтесь с помощью WASD или левого джойстика.",
            "Удерживайте Space или «Огонь». Пистолет сам наводится на видимую цель.",
            "Q или «Оружие» переключает пистолет и нож. Это поможет, когда кончатся патроны.",
            "Обыщите подсвеченную машину через E или «Взять». Аптечку можно применить или убрать в рюкзак.",
            "Зачистите четыре встречи. Затем подойдите к порталу и нажмите E или «Взять»."
        };

        private StreetMapScreen screen;
        private Stage5Runtime profile;
        private ActorMotor2D motor;
        private GameObject card;
        private Text instruction;
        private Text nextLabel;
        private int lesson;
        private float autoAdvance;

        internal void Initialize(StreetMapScreen map, Stage5Runtime runtime, RectTransform safeArea)
        {
            screen = map;
            profile = runtime;
            motor = map.Player.GetComponent<ActorMotor2D>();
            motor.CommandSampled += OnCommand;

            var panel = new GameObject("CityTutorial", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            panel.SetParent(safeArea, false);
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, 1);
            panel.pivot = new Vector2(.5f, 1);
            panel.anchoredPosition = new Vector2(0, -51);
            panel.sizeDelta = new Vector2(475, 118);
            panel.GetComponent<Image>().color = new Color(.055f, .085f, .09f, .94f);
            panel.GetComponent<Image>().raycastTarget = false;
            card = panel.gameObject;

            instruction = Stage5PreviewView.Label("Подсказка City", panel, Vector2.up * 24, new Vector2(450, 58), 19);
            var skip = Stage5PreviewView.Button("Пропустить", panel, new Vector2(-105, -32), Dismiss);
            ((RectTransform)skip.transform).sizeDelta = new Vector2(195, 39);
            var next = Stage5PreviewView.Button("Дальше", panel, new Vector2(105, -32), Advance);
            ((RectTransform)next.transform).sizeDelta = new Vector2(195, 39);
            nextLabel = next.GetComponentInChildren<Text>();
            Refresh();
        }

        private void OnCommand(PlayerCommand command)
        {
            if (autoAdvance > 0 || !card.activeSelf) return;
            bool acted = lesson switch
            {
                0 => command.Move.sqrMagnitude > .04f,
                1 => command.AttackHeld,
                2 => command.SwitchPressed,
                _ => false
            };
            if (acted) autoAdvance = .8f;
        }

        private void Update()
        {
            bool visible = lesson < Lessons.Length && !screen.IsPaused && !screen.Player.IsDead;
            if (card.activeSelf != visible) card.SetActive(visible);
            if (!visible || autoAdvance <= 0) return;
            autoAdvance -= Time.deltaTime;
            if (autoAdvance <= 0) Advance();
        }

        private void Advance()
        {
            if (lesson >= Lessons.Length) return;
            autoAdvance = 0;
            lesson++;
            if (lesson == Lessons.Length) Dismiss();
            else Refresh();
        }

        private void Refresh()
        {
            instruction.text = Lessons[lesson];
            nextLabel.text = lesson == Lessons.Length - 1 ? "Понятно" : "Дальше";
        }

        private void Dismiss()
        {
            lesson = Lessons.Length;
            profile.MarkTutorialSeen();
            card.SetActive(false);
        }

        private void OnDestroy()
        {
            if (motor != null) motor.CommandSampled -= OnCommand;
            if (card != null) Destroy(card);
        }
    }
}
