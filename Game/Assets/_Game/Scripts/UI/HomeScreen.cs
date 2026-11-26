using Game.Core;
using System;
using Game.Street;
using Game.StreetMap;
using Game.Encounters;
using Game.Combat;
using Game.Contracts;
using System.Globalization;
using System.Linq;
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
        public Button SeededStreetButton { get; private set; }
        public Button Stage5PreviewButton { get; private set; }
        public Button Stage5CityButton { get; private set; }
        public Button FullCityButton { get; private set; }
        private Button cityProfileButton;
        private Button cityMapButton;
        private RectTransform cityMapPanel;
        private RectTransform cityProfilePanel;
        private Button stage5ProfileButton;
        private RectTransform stage5ProfilePanel;
        public InputField FixedSeedInput { get; private set; }
        private RunOutcome? outcome;
        private string streetError = "";
        private static bool Diagnostics => Application.isEditor ||
            Array.IndexOf(Environment.GetCommandLineArgs(), "--diagnostics") >= 0;
        private bool HasCity => Stage5CityButton != null && boundRun.Stage5 != null;
        public InputField SeedInput { get; private set; }
        public string StreetError => streetError;
        protected override void OnBound(RunCoordinator value)
        { boundRun=value; outcome=value.LastOutcome; IsShowingResult=outcome.HasValue; diagnostic=value.CurrentRequest.IsStage2Diagnostic; stage2=value.CurrentRequest.IsStage2Route; fixedStreet=value.CurrentRequest.IsFixedStreet1_1; }
        protected override string Title => IsShowingResult
            ? (boundRun.CurrentRequest.IsRanchPreview ? "Ранчо · 5-1: " + OutcomeLabel :
                boundRun.CurrentRequest.IsStage5City ? CityResultTitle : fixedStreet ? "1-1: " + OutcomeLabel : diagnostic ? "Stage 2: проверка завершена" : outcome==RunOutcome.Victory ? (stage2 ? "Stage 2: победа" : "Победа") : outcome==RunOutcome.Defeat ? "Поражение" : "Вылазка завершена") : "Убежище";
        private string CityResultTitle => outcome == RunOutcome.Defeat ? "Эвакуация в убежище" :
            outcome == RunOutcome.Victory ? (boundRun.CurrentRequest.VisualPreview != CityVisualPreview.None
                ? "Проверочный маршрут завершён" : boundRun.CurrentRequest.IsFullCity ? "CITY завершён" : "Улица зачищена") : "Возвращение с улицы";
        protected override string Description => IsShowingResult
            && (boundRun.CurrentRequest.IsStage5Preview || boundRun.CurrentRequest.IsStage5City) ? Stage5Result()
            : IsShowingResult ? (diagnostic ? "Диагностическая арена, без босса и наград. Итог: " + OutcomeLabel : "Попытка завершена. Награды на этом этапе не начисляются.")
            : "CITY · Улица, гипермаркет и высотка\nПройдите три района и эвакуируйтесь с крыши.";
        private string OutcomeLabel => outcome==RunOutcome.Victory ? "зачистка" : outcome==RunOutcome.Defeat ? "гибель героя" : "выход домой";
        protected override string ButtonLabel => IsShowingResult ? "В убежище" : HasCity && !Diagnostics ? "Начать CITY" : "Тестовая арена";
        protected override bool CanAct(RunCoordinator value) => value.Phase==RunPhase.Home;
        protected override void Act(RunCoordinator value)
        {
            if (IsShowingResult) { IsShowingResult=false; Refresh(); }
            else if (HasCity && !Diagnostics) StartSeededStreet(false, true, true);
            else value.StartRun(RunRequest.Sandbox());
        }
        protected override bool ShowStreet => Diagnostics && !IsShowingResult && Application.CanStreamedLevelBeLoaded(ScenePaths.Stage3StreetSandbox);
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
            if (IsShowingResult && boundRun.CurrentRequest.IsStage5City && outcome.HasValue)
                gameObject.AddComponent<CityFeedback>().ShowResult(outcome.Value, (RectTransform)panel.parent);
            if (Application.CanStreamedLevelBeLoaded(ScenePaths.Stage4StreetMap))
            {
                SeededStreetButton = CreateMapButton(panel, font, "Играть 1-1", new Vector2(-230, 240), 210, StartSeededStreet);
                FixedSeedInput = CreateSeedField(panel, font, "FixedSeed", new Vector2(0, 228), 205, 48);
                FixedStreetButton = CreateMapButton(panel, font, "Тестовый маршрут 1-1", new Vector2(220, 240), 210,
                    () => boundRun.StartRun(RunRequest.FixedStreet1_1()));
                Stage5PreviewButton = CreateMapButton(panel, font, "Проверка Stage 5", new Vector2(-330, 307), 205,
                    () => StartSeededStreet(true));
                Stage5CityButton = CreateMapButton(panel, font, "Выйти на улицу · 1-1", new Vector2(-110, 300), 205,
                    () => StartSeededStreet(false, true));
                cityProfileButton = CreateMapButton(panel, font, "Профиль City", new Vector2(110, 300), 205,
                    () => ShowCityProfile(panel));
                stage5ProfileButton = CreateMapButton(panel, font, "Профиль Stage 5", new Vector2(330, 307), 205,
                    () => stage5ProfilePanel.gameObject.SetActive(!stage5ProfilePanel.gameObject.activeSelf));
                CreatePreviewProfile(panel);
                cityMapButton = CreateMapButton(panel, font, "Карта города", new Vector2(0, -244), 235,
                    () => ShowCityMap(panel));
                if (Diagnostics) ((RectTransform)cityMapButton.transform).anchoredPosition = new Vector2(440, -20);
                FullCityButton = CreateMapButton(panel, font, "Начать CITY", new Vector2(440, -96), 215,
                    () => StartSeededStreet(false, true, true));
            }
            if (StreetButton != null) SeedInput = CreateSeedField(panel, font, "StreetSeed", new Vector2(205, -320), 190, 65);
        }
        private static InputField CreateSeedField(Transform panel, Font font, string name, Vector2 pos, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField)).GetComponent<RectTransform>();
            rect.SetParent(panel, false);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(width, height);
            rect.GetComponent<Image>().color = new Color(.12f, .16f, .18f);
            var input = rect.GetComponent<InputField>();
            input.targetGraphic = rect.GetComponent<Image>();
            var text = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(rect, false);
            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8, 3); textRect.offsetMax = new Vector2(-8, -3);
            text.font = font; text.fontSize = 19; text.color = Color.white; text.alignment = TextAnchor.MiddleLeft;
            input.textComponent = text;
            var placeholder = Instantiate(text, rect);
            placeholder.name = "Placeholder"; placeholder.text = "Seed · пусто = новый"; placeholder.fontSize = 15; placeholder.color = Color.gray;
            input.placeholder = placeholder;
            return input;
        }
        protected override void OnViewRefreshed()
        {
            if (SeedInput != null) SeedInput.gameObject.SetActive(ShowStreet);
            if (FixedStreetButton != null)
            {
                bool visible = !IsShowingResult && boundRun.Phase != RunPhase.Faulted;
                FixedStreetButton.gameObject.SetActive(visible && Diagnostics);
                SeededStreetButton.gameObject.SetActive(visible && Diagnostics);
                FixedSeedInput.gameObject.SetActive(visible);
                FixedStreetButton.interactable = boundRun.Phase == RunPhase.Home;
                SeededStreetButton.interactable = FixedStreetButton.interactable;
                FixedSeedInput.interactable = FixedStreetButton.interactable;
                Stage5PreviewButton.gameObject.SetActive(visible && Diagnostics);
                stage5ProfileButton.gameObject.SetActive(visible && Diagnostics);
                Stage5CityButton.gameObject.SetActive(visible && Diagnostics && boundRun.Stage5 != null);
                cityProfileButton.gameObject.SetActive(visible && boundRun.Stage5 != null);
                cityMapButton.gameObject.SetActive(visible && boundRun.Stage5 != null);
                FullCityButton.gameObject.SetActive(visible && Diagnostics && boundRun.Stage5 != null);
                Stage5PreviewButton.interactable = FixedStreetButton.interactable &&
                    Stage5PreviewProfile.For(boundRun).Scenario != PreviewProfileScenario.FutureSchema;
                stage5ProfileButton.interactable = FixedStreetButton.interactable;
                Stage5CityButton.interactable = cityProfileButton.interactable = FixedStreetButton.interactable;
                if (!visible || !FixedStreetButton.interactable) cityProfilePanel?.gameObject.SetActive(false);
                if (!visible || !FixedStreetButton.interactable) cityMapPanel?.gameObject.SetActive(false);
                if (!visible || !FixedStreetButton.interactable) stage5ProfilePanel.gameObject.SetActive(false);
            }
        }
        private Button CreateMapButton(Transform panel, Font font, string title, Vector2 pos, float width, Action start)
        {
            var rect = new GameObject("FixedStreet1_1", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
            rect.SetParent(panel, false);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(width, 55);
            rect.GetComponent<Image>().color = new Color(.16f, .4f, .44f);
            var button = rect.GetComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(rect, false);
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.font = font; label.fontSize = 17; label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white; label.raycastTarget = false;
            label.text = title;
            button.onClick.AddListener(() =>
            {
                if (boundRun != null && boundRun.Phase == RunPhase.Home && !IsShowingResult)
                    start();
            });
            return button;
        }
        private void StartSeededStreet() => StartSeededStreet(false);

        private void ShowCityMap(Transform parent)
        {
            cityProfilePanel?.gameObject.SetActive(false);
            if (cityMapPanel != null) { cityMapPanel.gameObject.SetActive(true); return; }
            cityMapPanel = CreateCityDialog("Карта города", parent, 565);
            Stage5PreviewView.Button("Начать маршрут CITY", cityMapPanel, Vector2.up * 230,
                () => StartSeededStreet(false, true, true));
            bool ranch = boundRun.Stage5.Session.Snapshot.Profile.CompletedRegionIds.Contains("city");
            Stage5PreviewView.Label("Районы", cityMapPanel, Vector2.up * 156, new Vector2(430, 70), 18).text =
                "Улица → Гипермаркет → Высотка\n" + (ranch ? "Ранчо открыто · маршрут в разработке" : "Ранчо закрыто · завершите CITY");
            Stage5PreviewView.Label("Проверка районов", cityMapPanel, Vector2.up * 90, new Vector2(430, 28), 19).text =
                "Визуальная проверка";
            AddPreview("Улица · 1-1 → 1-3", 37, CityVisualPreview.Street);
            AddPreview("Гипермаркет · 2-1", -19, CityVisualPreview.Market);
            AddPreview("Офис и лифт · 3-4 → 3-5", -75, CityVisualPreview.Office);
            AddPreview("Крыша и канализация · 3-5 → 4-5", -131, CityVisualPreview.Sewer);
            Stage5PreviewView.Button("Проверка ранчо · 5-1", cityMapPanel, Vector2.down * 187, StartRanchPreview);
            Stage5PreviewView.Button("Закрыть", cityMapPanel, Vector2.down * 245, () => cityMapPanel.gameObject.SetActive(false));

            void AddPreview(string label, float y, CityVisualPreview mode)
            {
                Stage5PreviewView.Button(label, cityMapPanel, Vector2.up * y, () =>
                {
                    cityMapPanel.gameObject.SetActive(false);
                    StartSeededStreet(false, true, true, mode);
                });
            }
        }

        private static RectTransform CreateCityDialog(string title, Transform parent, float height)
        {
            var panel = Stage5PreviewView.Panel(title, parent);
            panel.sizeDelta = new Vector2(475, height);
            panel.GetComponent<Image>().enabled = false;
            var shade = new GameObject("Затемнение меню", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            shade.SetParent(panel, false);
            shade.sizeDelta = new Vector2(6000, 6000);
            shade.GetComponent<Image>().color = new Color(.025f, .04f, .047f, .90f);

            var field = new GameObject("Подложка", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            field.SetParent(panel, false);
            field.anchorMin = Vector2.zero;
            field.anchorMax = Vector2.one;
            field.offsetMin = field.offsetMax = Vector2.zero;
            field.GetComponent<Image>().color = new Color(.075f, .095f, .10f);
            return panel;
        }

        private string Stage5Result()
        {
            if (boundRun.CurrentRequest.IsStage5City)
            {
                var snapshot = boundRun.CityRuntime.Session.Snapshot;
                if (boundRun.IsInSewer)
                {
                    string resultSewer = outcome == RunOutcome.Victory
                        ? boundRun.SewerReward > 0 ? $"+{boundRun.SewerReward} кристаллов скверны" : "Получен чертёж «Герметичный механизм»"
                        : $"Канализация · участок {boundRun.CitySegmentId}";
                    return resultSewer + (boundRun.CurrentRequest.VisualPreview == CityVisualPreview.Sewer
                        ? "\nВизуальная проверка · отдельный профиль" : "\nПобеда CITY сохранена");
                }
                if (boundRun.CurrentRequest.VisualPreview != CityVisualPreview.None)
                    return $"Визуальная проверка · {boundRun.CitySegmentId}\nКристаллы в пробе: {snapshot.LastRun?.ConfirmedCrystalsEarned ?? 0}. Баланс убежища сохранён.";
                string result = $"Подтверждено за попытку: {snapshot.LastRun?.ConfirmedCrystalsEarned ?? 0} кристаллов\n" +
                    $"Монеты при завершении: {snapshot.LastRun?.FinalCoins ?? 0} · баланс в убежище: {snapshot.Profile.ConfirmedCrystals}";
                return boundRun.CurrentRequest.IsFullCity && outcome == RunOutcome.Victory
                    ? result + "\nЭвакуация с крыши. Ранчо открыто."
                    : result;
            }
            var profile = Stage5PreviewProfile.For(boundRun);
            return $"Подтверждено за попытку: {profile.LastCrystals} кристаллов\n" +
                $"Монеты при завершении: {profile.LastCoins} · баланс в убежище: {profile.Crystals} (макет)";
        }

        private void ShowCityProfile(Transform parent)
        {
            cityMapPanel?.gameObject.SetActive(false);
            if (cityProfilePanel != null) { cityProfilePanel.gameObject.SetActive(true); return; }
            cityProfilePanel = CreateCityDialog("Профиль City", parent, 405);
            var balance = Stage5PreviewView.Label("Баланс City", cityProfilePanel, Vector2.up * 120, new Vector2(435, 42), 22);
            var state = Stage5PreviewView.Label("Статус City", cityProfilePanel, Vector2.up * 73, new Vector2(430, 48), 17);
            var notice = Stage5PreviewView.Label("Ответ City", cityProfilePanel, Vector2.up * 25, new Vector2(430, 40), 17);
            Button buy = null;
            Button select = null;
            void RefreshCityProfile()
            {
                var profile = boundRun.Stage5.Session.Snapshot.Profile;
                balance.text = $"Кристаллы скверны: {profile.ConfirmedCrystals}";
                state.text = profile.State switch
                {
                    ProgressionProfileState.RecoveredFromBackup => "Профиль восстановлен из резервной копии",
                    ProgressionProfileState.IncompatibleSchema => "Профиль создан более новой версией игры",
                    ProgressionProfileState.Unavailable => "Хранилище профиля недоступно",
                    _ => profile.SelectedWeaponId == "pistol-city" ? "Выбран городской пистолет" : "Выбран обычный пистолет"
                };
                if (profile.UnlockedIds.Contains(Game.Progression.SewerEntryService.BlueprintId))
                    state.text += "\nЧертёж «Герметичный механизм»";
                var offer = profile.HomeOffers.Count > 0 ? profile.HomeOffers[0] : default;
                buy.interactable = offer.OfferId != null || profile.State == ProgressionProfileState.IncompatibleSchema ||
                    profile.State == ProgressionProfileState.Unavailable;
                buy.GetComponentInChildren<Text>().text = profile.State == ProgressionProfileState.IncompatibleSchema
                    ? "Открыть отдельный профиль" : offer.OfferId != null ? $"Открыть городской пистолет · {offer.PriceCrystals}"
                    : profile.State == ProgressionProfileState.Unavailable ? "Повторить запись" : "Городской пистолет открыт";
                select.gameObject.SetActive(profile.UnlockedIds.Contains("pistol-city"));
                select.GetComponentInChildren<Text>().text = profile.SelectedWeaponId == "pistol-city"
                    ? "Выбрать обычный пистолет" : "Выбрать городской пистолет";
            }
            buy = Stage5PreviewView.Button("Покупка", cityProfilePanel, Vector2.down * 15, () =>
            {
                var profile = boundRun.Stage5.Session.Snapshot.Profile;
                if (profile.State == ProgressionProfileState.IncompatibleSchema)
                {
                    boundRun.Stage5.OpenSeparateProfile();
                    notice.text = "Открыт отдельный профиль";
                }
                else
                {
                    string offer = profile.HomeOffers.Count > 0 ? profile.HomeOffers[0].OfferId : "unlock-city-pistol";
                    var result = boundRun.Stage5.Buy(offer);
                    notice.text = result.Failure == ProgressionFailure.None ?
                        "Городской пистолет открыт и выбран" : Stage5Runtime.Describe(result.Failure);
                }
                RefreshCityProfile();
            });
            select = Stage5PreviewView.Button("Выбрать оружие", cityProfilePanel, Vector2.down * 70, () =>
            {
                var weapon = boundRun.Stage5.Session.Snapshot.Profile.SelectedWeaponId == "pistol-city" ? "pistol" : "pistol-city";
                var result = boundRun.Stage5.SelectStarter(weapon);
                notice.text = result.Failure == ProgressionFailure.None ? "Стартовое оружие сохранено" : Stage5Runtime.Describe(result.Failure);
                RefreshCityProfile();
            });
            Stage5PreviewView.Button("Повторить обучение", cityProfilePanel, Vector2.down * 125, () =>
            {
                boundRun.Stage5.ReplayTutorial();
                notice.text = "Подсказки появятся в следующей вылазке";
            });
            Stage5PreviewView.Button("Закрыть", cityProfilePanel, Vector2.down * 180, () => cityProfilePanel.gameObject.SetActive(false));
            RefreshCityProfile();
        }

        private void CreatePreviewProfile(Transform panel)
        {
            stage5ProfilePanel = Stage5PreviewView.Panel("Профиль Stage 5", panel);
            var balance = Stage5PreviewView.Label("Кристаллы профиля", stage5ProfilePanel, Vector2.up * 120, new Vector2(440, 40), 22);
            var scenario = Stage5PreviewView.Label("Состояние профиля", stage5ProfilePanel, Vector2.up * 72, new Vector2(440, 50), 18);
            var notice = Stage5PreviewView.Label("Результат операции", stage5ProfilePanel, Vector2.up * 27, new Vector2(430, 45), 18);
            var profile = Stage5PreviewProfile.For(boundRun);
            Button purchase = null;
            void RefreshProfile()
            {
                balance.text = profile.Summary;
                scenario.text = profile.ScenarioLabel;
                bool incompatible = profile.Scenario == PreviewProfileScenario.FutureSchema;
                purchase.GetComponentInChildren<Text>().text = profile.Selected && !incompatible ? "Тестовый вариант выбран" : profile.PurchaseLabel;
                purchase.interactable = incompatible || !profile.Selected;
                Stage5PreviewButton.interactable = FixedStreetButton.interactable && !incompatible;
            }
            purchase = Stage5PreviewView.Button(profile.PurchaseLabel, stage5ProfilePanel, Vector2.down * 25, () =>
            {
                if (profile.Scenario == PreviewProfileScenario.FutureSchema)
                {
                    profile = profile.OpenSeparate();
                    notice.text = "Создан отдельный тестовый профиль";
                }
                else notice.text = profile.PurchaseOrSelect();
                RefreshProfile();
            });
            Stage5PreviewView.Button("Сменить сценарий профиля", stage5ProfilePanel, Vector2.down * 80, () =>
            {
                profile.AdvanceScenario();
                notice.text = "";
                RefreshProfile();
            });
            Stage5PreviewView.Button("Закрыть", stage5ProfilePanel, Vector2.down * 135, () => stage5ProfilePanel.gameObject.SetActive(false));
            RefreshProfile();
            stage5ProfilePanel.gameObject.SetActive(false);
        }

        private void StartSeededStreet(bool stage5Preview, bool city = false, bool fullCity = false,
            CityVisualPreview visualPreview = CityVisualPreview.None)
        {
            if (stage5Preview && Stage5PreviewProfile.For(boundRun).Scenario == PreviewProfileScenario.FutureSchema) return;
            if (!TryReadSeed(visualPreview != CityVisualPreview.None, out int seed)) return;
            var prefabs = Resources.Load<FixedEncounterPrefabs>(FixedEncounterPrefabs.ResourceName);
            var combat = Resources.Load<Stage4CombatAssets>(Stage4CombatAssets.ResourceName);
            if (prefabs == null || !prefabs.IsComplete || combat == null || !combat.IsComplete)
            {
                streetError = "Не найдены ресурсы противников 1-1.";
                Refresh();
                return;
            }
            var map = StreetMapCatalog.Create();
            var result = new EncounterPlanner().Generate(new EncounterRequest(seed, 1, 1, map));
            if (result.IsSuccess)
            {
                var validation = EncounterPlanValidator.ValidateStage(map, result.Plan);
                if (!validation.IsSuccess) result = validation;
            }
            if (!result.IsSuccess)
            {
                streetError = "Не удалось создать встречу 1-1: " + result.Diagnostic;
                Refresh();
                return;
            }
            streetError = "";
            Debug.Log("FIXED_STREET " + result.Diagnostic + " map=" + result.Plan.MapVersion + " " +
                string.Join(" | ", System.Linq.Enumerable.Select(result.Plan.Rooms, room => room.RoomId + ":" +
                    string.Join(",", System.Linq.Enumerable.Select(room.Waves, wave => wave.WaveId + "@" +
                        wave.DelaySeconds.ToString(CultureInfo.InvariantCulture) + "=" +
                        string.Join("+", System.Linq.Enumerable.Select(wave.Spawns, spawn => spawn.SpawnId + "/" + spawn.EnemyTypeId + "@" + spawn.PointId)))))));
            try
            {
                boundRun.StartRun(city ? RunRequest.Stage5City(result.Plan, fullCity, visualPreview) :
                    stage5Preview ? RunRequest.Stage5Preview(result.Plan) : RunRequest.FixedStreet1_1(result.Plan));
            }
            catch (InvalidOperationException exception)
            {
                streetError = exception.Message;
                Refresh();
                return;
            }
            if (city && boundRun.Phase == RunPhase.Home)
            {
                FixedSeedInput.text = seed.ToString(CultureInfo.InvariantCulture);
                streetError = boundRun.Error;
                Refresh();
            }
        }

        private void StartRanchPreview()
        {
            if (!TryReadSeed(true, out int seed)) return;
            try
            {
                RanchStage.Load();
                var request = RunRequest.RanchPreview(RanchLayout.Plan(seed));
                cityMapPanel.gameObject.SetActive(false);
                streetError = "";
                boundRun.StartRun(request);
            }
            catch (InvalidOperationException exception)
            {
                streetError = exception.Message;
                Refresh();
            }
        }

        private bool TryReadSeed(bool visualPreview, out int seed)
        {
            if (string.IsNullOrWhiteSpace(FixedSeedInput.text))
            {
                seed = visualPreview ? 0 : BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0);
                return true;
            }
            if (int.TryParse(FixedSeedInput.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out seed)) return true;
            cityMapPanel?.gameObject.SetActive(false);
            streetError = "Введите целое число от −2147483648 до 2147483647 или оставьте seed пустым.";
            Refresh();
            return false;
        }

        protected override string SecondaryLabel => Application.CanStreamedLevelBeLoaded(ScenePaths.Stage2CombatSandbox)
            && Application.CanStreamedLevelBeLoaded(ScenePaths.Stage2BossSandbox)
            ? "Stage 2: арена и полицейский" : null;
        protected override bool ShowSecondary => Diagnostics && !IsShowingResult;
        protected override bool CanSecondaryAct(RunCoordinator value) => value.Phase==RunPhase.Home && !IsShowingResult;
        protected override void ActSecondary(RunCoordinator value) => value.StartRun(RunRequest.Stage2());
        protected override string Message(RunCoordinator value) => value.Phase==RunPhase.Home ? (string.IsNullOrEmpty(streetError) ? "WASD · Space · Q · E" : streetError) : "Переходим на арену…";
    }
}
