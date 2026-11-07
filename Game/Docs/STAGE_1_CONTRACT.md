# S1-01 — контракт подключения v0.1

Общая основа: Game/, Unity 6000.3.28f1. Input System 1.20.1, Unity UI 2.0.0, Test Framework 1.6.0; фактические версии хранятся в Packages/manifest.json и packages-lock.json. Базовая Windows-сборка и новый цикл Foundation проверены локально. XML подтверждают EditMode 9/9, PlayMode 3/3; buildId 20261107-100815-e8c398c4. Передача ожидает запуска получателем на Mac; протокол — S1_01_FLOW_CHECK.md.

## Граница боя и представления

Game/Assets/_Game/Scripts/Contracts/CombatContracts.cs не изменён.
CombatSnapshot — readonly struct с показателями Health/MaxHealth, Armor/MaxArmor, Stamina/MaxStamina, Magazine/MagazineCapacity/ReserveAmmo и IsMelee/IsReloading/IsDead. Пример чтения: CombatSnapshot snapshot = source.ReadSnapshot(); UI использует snapshot.Health и snapshot.MaxHealth, не вызывает расчёт урона.

IDefeatable предоставляет IsDead и event Action Died. IPlayerCombatSource добавляет ReadSnapshot, Changed, InteractRequested, PauseRequested и SetControlsEnabled.
Снимок доступен сразу после создания героя. Died вызывается единожды за жизнь; выгрузка сцены не является смертью. Блокировка боевых действий оставляет доступным PauseRequested; смерть не отменяется вызовом SetControlsEnabled(true).
Здоровье/броня не регенерируют; правила ресурсов реализует участник 1, а не контракт или экран.

## Цикл

GameBootstrap владеет единственным RunCoordinator и SceneFlow; сохраняется между сценами.
UI получает координатор через FlowScreen.Bind при загрузке, без поиска героя каждый кадр.
StartRun принимает RunRequest.Sandbox(seed); доступна только CombatSandbox. Seed хранится, генерации ещё нет.
FinishRun принимает Victory/Defeat/Abandoned; запросы разрешаются в LateUpdate один раз, Defeat имеет приоритет.
Проверочная кнопка EmptyArenaScreen вызывает Abandoned. Это не готовая победа и не награда.
Сбой загрузки отображается через Faulted; экран позволяет повторить переход домой.

## Создание и запуск

После окончания компиляции выбрать Game → Setup → Create foundation scenes.
Команда создаёт только отсутствующие Bootstrap, Home и CombatSandbox через Unity, сохраняет .meta и ставит Bootstrap первой в Build Settings. BaselineSmoke остаётся на диске и выключается в списке сборки. При конфликтующей сцене команда останавливается и сохраняет исходник.
Открыть Bootstrap и нажать Play. Home/CombatSandbox напрямую без Bootstrap не создают сервисы: обычная точка входа только Bootstrap.
В Home нажать «Начать проверочную вылазку», на арене — «Тестовый выход — вернуться домой».
Проверочные экраны не содержат героя, врагов, HUD, сохранений или экономики.

## Передача участнику 1

Передавать единый проект Assets/ вместе с .meta, Packages/, ProjectSettings/ и Docs/, из одной принятой версии. Library/, Temp/, Logs/, UserSettings/, Builds/ и LocalState/ не передаются как исходники.
На Mac использовать ту же Unity 6000.3.28f1 и открыть полученный Game/; второй независимый проект не создавать. До принятия основы согласовать общий коммит либо идентификатор переноса; локальный коммит не является доставкой файлов товарищу.

Участник 1 владеет Scripts/Input, Actors, Combat, AI/SandboxEnemy, Player.inputactions и боевыми prefab. Сначала передаёт S1-02: PlayerCommand, движение, здоровье и PlayerCombatFacade : IPlayerCombatSource.
Участник 2 владеет Core, UI, Sandbox, Editor, Contracts, сценами, .asmdef, Packages и ProjectSettings. После S1-02 он размещает Player.prefab и подключает HUD, не меняя состав prefab.
Контракт v0.1 меняется только согласованно. После S1-01 участники продолжают в отдельных ветках от принятой основы.

## Доказательства и ограничения

Новый код и меню не означают автоматически созданные сцены или пройденные тесты. Действительный статус записан в Coordination/ROLE_2_STATUS.md и S1_01_FLOW_CHECK.md. S1-01 принимается после десяти циклов, успешных тестов, новой сборки и проверки получателем. Android/iOS и реальный бой — последующие результаты.

Статус 2026-11-07: локальная проверка DONE, готово к передаче. Запуск получателем и факт получения ещё не подтверждены. Android/iOS не проверялись.
