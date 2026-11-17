using Game.Core;
using System;
using Game.Street;
using UnityEngine.UI;
using UnityEngine;
namespace Game.UI
{
    public sealed class HomeScreen : FlowScreen
    {
        public bool IsShowingResult { get; private set; }
        private bool diagnostic, stage2, fixedStreet;
        private RunCoordinator boundRun;
        public Button FixedStreetButton { get; private set; }
        private RunOutcome? outcome;
        private string streetError = "";
        public InputField SeedInput { get; private set; }
        public string StreetError => streetError;
        protected override void OnBound(RunCoordinator value)
        { boundRun=value; outcome=value.LastOutcome; IsShowingResult=outcome.HasValue; diagnostic=value.CurrentRequest.IsStage2Diagnostic; stage2=value.CurrentRequest.IsStage2Route; fixedStreet=value.CurrentRequest.IsFixedStreet1_1; }
        protected override string Title => IsShowingResult
            ? (fixedStreet ? "1-1: " + OutcomeLabel : diagnostic ? "Stage 2: проверка завершена" : outcome==RunOutcome.Victory ? (stage2 ? "Stage 2: победа" : "Победа") : outcome==RunOutcome.Defeat ? "Поражение" : "Вылазка завершена") : "Убежище";
        protected override string Description => IsShowingResult
            ? (diagnostic ? "Диагностическая арена, без босса и наград. Итог: " + OutcomeLabel : "Попытка завершена. Награды на этом этапе не начисляются.")
            : "Зачистите арену, подойдите к выходу и нажмите E.";
        private string OutcomeLabel => outcome==RunOutcome.Victory ? "зачистка" : outcome==RunOutcome.Defeat ? "гибель героя" : "выход домой";
        protected override string ButtonLabel => IsShowingResult ? "В убежище" : "Начать вылазку";
        protected override bool CanAct(RunCoordinator value) => value.Phase==RunPhase.Home;
        protected override void Act(RunCoordinator value)
        {
            if (IsShowingResult) { IsShowingResult=false; Refresh(); }
            else value.StartRun(RunRequest.Sandbox());
        }
        protected override bool ShowStreet => !IsShowingResult && Application.CanStreamedLevelBeLoaded(ScenePaths.Stage3StreetSandbox);
        protected override void ActStreet(RunCoordinator value)
        {
            streetError = "";
            try
            {
                var data = Resources.Load<StreetCatalogAsset>("StreetCatalog");
                if (data == null) throw new ArgumentException("StreetCatalog resource is missing.");
                var catalog = StreetGenerationAdapter.BuildCatalog(data.Modules);
                if (StreetGenerationAdapter.TryGenerate(SeedInput.text, catalog, out var result, out streetError))
                {
                    Debug.Log("STREET_GENERATED " + result.Diagnostic);
                    value.StartGeneratedStreetRun(result, catalog);
                }
            }
            catch (ArgumentException exception) { streetError = "Ошибка каталога: " + exception.Message; }
            Refresh();
        }
        protected override void OnViewCreated(Transform panel, Font font)
        {
            if (Application.CanStreamedLevelBeLoaded(ScenePaths.Stage4StreetMap)) CreateFixedStreetButton(panel, font);
            if (StreetButton == null) return;
            var rect = new GameObject("StreetSeed", typeof(RectTransform), typeof(Image), typeof(InputField)).GetComponent<RectTransform>();
            rect.SetParent(panel, false);
            rect.anchoredPosition = new Vector2(205, -320);
            rect.sizeDelta = new Vector2(190, 65);
            rect.GetComponent<Image>().color = new Color(.12f, .16f, .18f);
            SeedInput = rect.GetComponent<InputField>();
            SeedInput.targetGraphic = rect.GetComponent<Image>();
            var text = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(rect, false);
            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8, 3); textRect.offsetMax = new Vector2(-8, -3);
            text.font = font; text.fontSize = 19; text.color = Color.white; text.alignment = TextAnchor.MiddleLeft;
            SeedInput.textComponent = text;
            var placeholder = Instantiate(text, rect);
            placeholder.name = "Placeholder"; placeholder.text = "Seed · пусто = новый"; placeholder.fontSize = 15; placeholder.color = Color.gray;
            SeedInput.placeholder = placeholder;
        }
        protected override void OnViewRefreshed()
        {
            if (SeedInput != null) SeedInput.gameObject.SetActive(ShowStreet);
            if (FixedStreetButton != null)
            {
                FixedStreetButton.gameObject.SetActive(!IsShowingResult && boundRun.Phase != RunPhase.Faulted);
                FixedStreetButton.interactable = boundRun.Phase == RunPhase.Home;
            }
        }
        private void CreateFixedStreetButton(Transform panel, Font font)
        {
            var rect = new GameObject("FixedStreet1_1", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
            rect.SetParent(panel, false);
            rect.anchoredPosition = new Vector2(0, 240);
            rect.sizeDelta = new Vector2(600, 55);
            rect.GetComponent<Image>().color = new Color(.16f, .4f, .44f);
            FixedStreetButton = rect.GetComponent<Button>();
            FixedStreetButton.targetGraphic = rect.GetComponent<Image>();
            FixedStreetButton.navigation = new Navigation { mode = Navigation.Mode.None };
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(rect, false);
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.font = font; label.fontSize = 22; label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white; label.raycastTarget = false;
            label.text = "1-1: постоянная карта · прототип S4-02";
            FixedStreetButton.onClick.AddListener(() =>
            {
                if (boundRun != null && boundRun.Phase == RunPhase.Home && !IsShowingResult)
                    boundRun.StartRun(RunRequest.FixedStreet1_1());
            });
        }
        protected override string SecondaryLabel => Application.CanStreamedLevelBeLoaded(ScenePaths.Stage2CombatSandbox)
            && Application.CanStreamedLevelBeLoaded(ScenePaths.Stage2BossSandbox)
            ? "Stage 2: арена и полицейский" : null;
        protected override bool ShowSecondary => !IsShowingResult;
        protected override bool CanSecondaryAct(RunCoordinator value) => value.Phase==RunPhase.Home && !IsShowingResult;
        protected override void ActSecondary(RunCoordinator value) => value.StartRun(RunRequest.Stage2());
        protected override string Message(RunCoordinator value) => value.Phase==RunPhase.Home ? (string.IsNullOrEmpty(streetError) ? "WASD · Space · Q · E" : streetError) : "Переходим на арену…";
    }
}
