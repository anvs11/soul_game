# S5-07. Передача сервиса прогрессии У2

**Ветка У1** — `feat/stage5-progression`. **SHA сервиса** — `054c79d32d25b72158fe644d57208f0d2c6e979d`. Этот коммит включает контракт, добычу, NPC и долговременный профиль. Сцены, HUD и привязку конкретных prefab подключает У2 в S5-06.

| Что подключать | Где находится |
| --- | --- |
| Сессия City, включая запись профиля | [CityProgressionSession](../Assets/_Game/Scripts/Progression/CityProgressionSession.cs), [команды профиля](../Assets/_Game/Scripts/Progression/CityProgressionProfile.cs) |
| Команды и снимки | [IProgressionSession](../Assets/_Game/Scripts/Contracts/IProgressionSession.cs), [ICityProgressionSession и боевой адаптер](../Assets/_Game/Scripts/Contracts/CityProgressionContracts.cs), [снимки](../Assets/_Game/Scripts/Contracts/ProgressionSnapshots.cs) |
| Правила добычи и план NPC | [CityEconomy](../Assets/_Game/Scripts/Progression/CityEconomy.cs), [PlayerCombatFacade](../Assets/_Game/Scripts/Actors/PlayerCombatFacade.cs) |
| Хранилище, схема и резерв | [ISaveRepository](../Assets/_Game/Scripts/Progression/ISaveRepository.cs), [JsonSaveRepository](../Assets/_Game/Scripts/Progression/JsonSaveRepository.cs), [ProfileData](../Assets/_Game/Scripts/Progression/ProfileData.cs) |

## Каталог и цены

На входе сервиса каталог из `ProgressionTransitionPoint(PointId, StageId, RoadId, EntranceId, AllowedNpcs)`. `PointId` уникален на весь City. `AllowedNpcs` допускает `WeaponDealer`, `BottleDealer` или оба флага. Две реальные точки макета 1-1 — `1-1/a-to-b` (переулок Барыги) и `1-1/b-to-c` (дверь Диллера). Координаты входа, интерьера и возврата предоставляет каталог карты У2. Примеры дополнительных точек 1-2…1-5 в контракте предназначены для расчётного каталога. На старте сервис один раз планирует 0–1 Барыгу и 0–2 Диллеров, максимум одного NPC на точку. [Примеры каталога из шести точек](S5_01_PROGRESSION_CONTRACT_HANDOFF.md#точки-перехода)

| Предложение | Цена | Результат |
| --- | --- | --- |
| Барыга, `weapon-upgrade` | 3 монеты | Через 2 игровые секунды выдаёт предмет для конкретного экземпляра оружия. `fire-rate` даёт интервал стрельбы ×0,8, `blade-sharpening` даёт урон ножа ×1,25. |
| Диллер, `dealer-bottle` | 4 монеты | Оставляет пузырёк на столе. После отдельного употребления скорость ×1,2 с вероятностью 70% или ×0,8 с вероятностью 30% до следующей новой зачистки. |
| Home, `unlock-city-pistol` | 20 кристаллов по умолчанию | Открывает и выбирает `pistol-city`. ID предложения, ID альтернативного оружия и цену можно задать через `ProgressionHomeOffer`. Базовый ID оружия — `pistol`. |
| Будущее возрождение | 500 кристаллов | Зарезервированная цена в `ProfileData.FutureRevivalPriceCrystals`. |

Обычные `melee`, `jumper` и `shooter` дают 5–10 кристаллов, мини-боссы 75, `heavy_boss` 270, `small_zombie` 11–15. Спавны с сегментом `/summon/` пропускаются. У стрелка шанс на 8 патронов составляет 50%. У каждого зомби отдельный шанс 30% на 1–3 монеты. Полный расчётный City содержит 5 стадий по 3 комнаты с 6–8 врагами, 9 мини-боссов и 2 тяжёлых босса. [Подробности баланса и боевых эффектов](S5_03_CITY_RUNTIME_HANDOFF.md)

## Порядок инициализации и событий

1. В Bootstrap создайте один `CityProgressionSession(() => Time.timeAsDouble, new JsonSaveRepository(profileDirectory))` на открытый профиль. В разработке передайте абсолютный путь к `Game/LocalState`, в сборке путь предоставит приложение. После прерванной попытки сессия фиксирует `Abandoned` и отдаёт Home последнюю подтверждённую ревизию. Для `RecoveredFromBackup` показывайте предупреждение, при `IncompatibleSchema` предложите отдельный каталог профиля.
2. Сгенерируйте `RunId` в RunCoordinator и передайте тот же ID в `StartRun` сервиса вместе с seed и каталогом точек. В текущем RunCoordinator ID создаётся внутри `StartRun` перед `Load`. Разместите вызов сервиса в адаптере после назначения ID и до загрузки. При повторе используйте исходные RunId и ActionId. При ошибке записи оставьте пользователя в Home. После принятого старта выберите prefab или `WeaponDefinition` по `Run.StartingWeaponId` и привяжите фасад через `BindCombat`. Из `Run.Npcs` используйте точки текущего StageId.
3. Передавайте `EnemyDefeated` в `ConfirmEnemyDeath` до `ClearCheckpoint` той же комнаты. SpawnId и checkpointIndex должны быть стабильны во всей попытке. Создавайте и обновляйте видимую добычу по PickupId из снимка. Подбор патронов может оставить остаток. В HUD отдельно выводите монеты, ожидающие и подтверждённые кристаллы.
4. У Барыги передайте ID текущего оружия из `hero.EquippedWeaponId`. Вызовите `CompleteNpcService` по `ReadyAtGameTime`, даже после выхода из интерьера. Применение модуля и употребление пузырька — отдельные команды по ItemId. При переходе сначала примите `EnterStage`, затем перенесите ID тех же экземпляров оружия и привяжите новый фасад.
5. Вызывайте `FinishRun` с `Defeat`, `Abandoned` или `Victory` до уничтожения боевого фасада. `LastRun` содержит отчёт, `Profile` — подтверждённый баланс. При `RetryableFailure / SaveFailed` сохраняйте параметры и ActionId для повтора. Ошибка зачистки оставляет кристаллы ожидающими и приостанавливает переход. `Replayed` с `Failure = None` означает, что действие уже принято. [Профиль, статусы и восстановление](S5_05_PROFILE_HANDOFF.md)

## Фактические результаты проверки

22 ноября 2026 года SHA сервиса `054c79d32d25b72158fe644d57208f0d2c6e979d` проверен в отдельной копии проекта на Unity **6000.3.28f1 (8f082d2fe476)**, macOS arm64. Оба запуска завершились с кодом 0, без упавших, пропущенных или неопределённых тестов.

| Набор | Тесты | Результат |
| --- | --- | --- |
| EditMode | `ProgressionContractTests` 8/8, `CityProgressionTests` 17/17, `ProgressionSaveTests` 13/13 | **38/38**, `Passed`, 1,64 с. [EditMode.xml](../TestResults/S5-07-054c79d-20261122-020532/EditMode.xml) |
| PlayMode | `CityProgressionCombatTests` 1/1, боевые множители движения, стрельбы и ножа | **1/1**, `Passed`, 0,11 с. [PlayMode.xml](../TestResults/S5-07-054c79d-20261122-020532/PlayMode.xml) |

XML-отчёты доступны в локальном `Game/TestResults/S5-07-054c79d-20261122-020532/`. SHA-256 `EditMode.xml` — `4075f8193f347e710fbf60807a313109378ec96cdca4aed0d6c0198cc7f668fb`. SHA-256 `PlayMode.xml` — `9bba74e7a5f7634e08ea4e6ae540e6807144366b224568d3defddd4299e552ad`. Проведён целевой прогон наборов Stage 5. Полная приёмка и проверка интеграции остаются в S5-07, часть 2, и S5-08.

Дополнительный расчёт по 2048 seed дал 49,91% выпадений патронов, 29,96% выпадений монет, 69,44 монеты и 2002,76 кристалла в среднем за полный City. Полезная ветвь пузырька получилась в 68,61% из 1386 покупок. [Методика расчёта](S5_03_CITY_RUNTIME_HANDOFF.md#стартовый-баланс)

Сервис подключён в `016b7fe2665929327d44e93dd9a27c32a9a2c460`. В актуальной 1-1 используется реальный файловый профиль. Результаты интеграции, сборки и повторных запусков записываются в [S5-08](S5_08_ACCEPTANCE.md).

## Повтор прогона

Используйте Unity 6000.3.28f1. Из корня репозитория запустите оба режима последовательно на закрытом проекте. Задайте полный путь к исполняемому файлу редактора в `unity_editor`. Отдельная копия проекта позволяет продолжать работу в открытом Unity.

```sh
report_dir="$PWD/Game/TestResults/S5-07-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$report_dir/checkout"
git archive 054c79d32d25b72158fe644d57208f0d2c6e979d Game | tar -xf - -C "$report_dir/checkout"
"$unity_editor" -batchmode -nographics -projectPath "$report_dir/checkout/Game" \
  -runTests -testPlatform EditMode \
  -testFilter 'Game\.Tests\.(ProgressionContractTests|CityProgressionTests|ProgressionSaveTests)' \
  -testResults "$report_dir/EditMode.xml" -logFile "$report_dir/EditMode.log"
"$unity_editor" -batchmode -nographics -projectPath "$report_dir/checkout/Game" \
  -runTests -testPlatform PlayMode -testFilter 'Game\.Tests\.CityProgressionCombatTests' \
  -testResults "$report_dir/PlayMode.xml" -logFile "$report_dir/PlayMode.log"
```
