# S2-04 — смешанная тройка и исправление прыжка


ExecPlan по .agent/PLANS.md, 15 ноября 2026. Репозиторий soul_game, Unity-проект Game/, база df952a7, ветка feat/stage2-arenas. Пользователь принял стрелка и область прыгуна, затем попросил убрать толчок в полёте и ускорить стрельбу.

## Purpose / Big Picture


Из Bootstrap/Home игрок запускает Stage 2 с ближним, прыгуном и стрелком. Прыгун пересекает героя без контакта и поражает область только после приземления. Стрелок готовит выстрел .35 с и восстанавливается .8 с вместо .5/1.2, сохраняя видимый сигнал. Зачистка трёх врагов открывает выход, повтор создаёт свежие экземпляры.

## Progress


- [x] Прочитаны S2_03_HANDOFF, компоненты и API, пользователь подтвердил закрытый Unity.
- [x] Удалён контактный урон в полёте; неподвижный герой не получает урон/толчок до Land, прыгун оказывается за ним.
- [x] Темп стрелка .35/.8 в default и собственном definition asset, прежние проверки прошли.
- [x] ArenaController поддерживает смешанный MonoBehaviour[] и прежний SandboxEnemy[], единый runtime IEnemyTarget список.
- [x] Смешанная тройка подключена к Stage2CombatSandbox, различается цветом/метками/подготовкой.
- [x] EditMode 32/32 и финальный PlayMode 57/57, failed/skipped/inconclusive=0, exit=0. Десять смешанных циклов, Safe Area, уклонения, Defeat и очистка проверены.
- [x] Передача S2_04_HANDOFF и статус роли 2 подготовлены с фактическими результатами.
- [ ] Пользователь ещё не просмотрел смешанную сцену/новый перелёт и темп; Android/standalone и независимое ревью У1 открыты.

## Surprises & Discoveries


Толчок вызван прежним CircleCast в DashStep, который применял DamagePacket и BeginKnockback до Land. Коллизии игрока/прыгуна уже игнорируются, поэтому нужно убрать именно контактную атаку. ArenaController сериализует SandboxEnemy[], напрямую заменить тип массива без миграции нельзя. Сохраняем прежнее поле и добавляем отдельный массив mixedEnemies, используемый только новой сценой.

## Decision Log


Решение 15 ноября — урон/отбрасывание только в Land, центр по фактическому Rigidbody2D после последнего физического шага. Рывок остаётся конечным и блокируется стенами. Более быстрый стрелок использует .35/.8 как умеренное ускорение по запросу пользователя.

Решение 15 ноября — типизация смешанных ссылок через MonoBehaviour в сериализации и IEnemyTarget в runtime. Initialize вызывается точными методами SandboxEnemy/JumpingEnemy/ShootingEnemy. Контракт v0.1 сохраняет свою границу. Старый Configure для сцен/тестов работает через прежний массив, новый ConfigureMixed принимает три компонента. Отписки проходят через собранный runtime-список.

## Outcomes & Retrospective


В работе. Фактическая ручная приёмка смешанной схватки после реализации открыта. Предыдущий ручной просмотр относился к отдельным preview. Android/standalone Stage 2 остаются отдельными условиями.

Кодовая и автоматическая часть завершены. Счётчик врагов перенесён из OnGUI в Safe Area HUD под полосы ресурсов. Старые Foundation assertions сохранены. Первый новый UI coroutine не имел yield и остановил компиляцию; исправлен, лог сохранён. Финальный набор после добавления проверки уклонения в смешанной сцене — 32 EditMode и 57 PlayMode, все passed. Ручной просмотр и платформенные ограничения перечислены в передаче.

## Context and Orientation


AI/JumpingEnemy.cs содержит DashStep и Land, ShootingEnemyDefinition.cs и одноимённый .asset задают времена стрелка. Sandbox/ArenaController.cs связывает реестр/героя/HUD/выход/паузы. SceneFlow/RunCoordinator уже поддерживают Stage2CombatSandbox. Tests/PlayMode/Stage2ArenaTests.cs проверяет прежнюю диагностическую тройку одинаковых врагов, её типовые ожидания надо адаптировать явно к смешанным компонентам, сохраняя сценарии. Foundation остаётся регрессией Stage 1.

## Plan of Work


Milestone 1 удаляет контактный cast и оставляет флаг одного удара приземления. Тест ждёт прохождения мимо неподвижного героя, проверяет HP и позицию до Land, затем единственный урон после фактического приземления. Старые тесты, проверявшие неуязвимость контакта, переименовать в проверку защиты в момент приземления и установить защиту так, чтобы она покрывала сам удар.

Milestone 2 добавляет mixedEnemies и runtime список в ArenaController. OnBound валидирует три уникальные цели, инициализирует и подписывает их. OnPhaseChanged управляет MonoBehaviour.enabled, не оживляет умерших. Detach снимает подписки со всех типов. Editor/Stage2MixedScene.cs заменяет HiddenZombie на прыгуна, FarZombie на стрелка в собственной Stage2 сцене, сохраняет NearZombie ближним, World-укрытия, HUD и один EventSystem. Старые позиции сохраняют сценарий ближний/скрытый/дальний. Формы различаются масштабом спрайтов, collider и атаки от этого не масштабируются.

Milestone 3 прогоняет полные EditMode/PlayMode в новой Game/TestResults/s204-<id>/. Добавляет смешанные десять циклов с активными пулями, двойным выходом и смертью в том же кадре, UI SafeArea/неперекрытие HUD. Существующие проверки укрытий/границ/обнаружения адаптируются к IEnemyTarget без удаления assertions. Предыдущие тесты прыгуна/стрелка продолжают проверять уклонение и стены.

## Concrete Steps


Из корня soul_game запускать разрешённый Unity.exe C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe скрыто. Создание интеграции -batchmode -quit -projectPath <root>/Game -executeMethod Game.Editor.Stage2MixedScene.Apply -logFile <новая папка>/create.log. Тесты -batchmode -nographics -projectPath <root>/Game -runTests -testPlatform EditMode, затем PlayMode, собственные XML и логи. Новые .meta создаёт Unity. Не удалять lockfile/Library/старые отчёты. Diff проверяется перед локальным коммитом явных путей, push отдельно.

## Validation and Acceptance


Все обнаруженные тесты выполнены, failed/skipped=0. В реальной Stage2 сцене по одному каждому типу, registry.Count=3 и EventSystem один. Три смерти открывают выход только в зоне, смерть+выход дают один Defeat. После десяти повторов старые экземпляры уничтожены, пули отсутствуют в Home, ресурсы свежие. SafeArea якоря соответствуют Screen.safeArea, кнопки не перекрывают полосы HUD в проверенном разрешении. Настоящие касания Android и другие экраны не объявляются проверенными без устройства.

## Idempotence and Recovery


Editor-команда распознаёт уже интегрированную сцену, сохраняет только собственную Stage2 сцену и новые definition asset. Исходные prefab Stage 1 не заменяются. При ошибке сохраняются лог и частичные ресурсы, исправление точечное. При смене состава не создаются новые registry/ввод/координатор.

## Artifacts and Notes


Game/Docs/Plans/S2_04_MIXED_ARENA_PLAN.md и Game/Docs/S2_04_HANDOFF.md, Coordination/ROLE_2_STATUS.md. XML локальны. Независимое ревью У1 после выполнения его части этим же агентом пока не подтверждено, фактическое ревью реализации и замечания записываются в передаче.

Создание сцены — Game/TestResults/s204-3be0d10140bc40c8ba0de1794d164037/create.log, exit=0. EditMode — Game/TestResults/s204-run-a6f469dbf2db4b258a29bb7e50c22697/EditMode.xml (32/32). Финальный PlayMode — Game/TestResults/s204-final-6be026ff7e2c4a61ab536973e1291106/PlayMode.xml (57/57). В каждом discovered=executed=passed, failed/skipped/inconclusive=0. Ранние PlayMode56/56 и неудачная компиляция сохраняются.

## Interfaces and Dependencies


ArenaController.ConfigureMixed(PlayerCombatFacade, MonoBehaviour[], TargetRegistry, HudPresenter, Transform). Combatants предоставляет IReadOnlyList<IEnemyTarget> для проверок. Сохранён Configure с SandboxEnemy[]. Runtime-цель валидируется как Unity MonoBehaviour и IEnemyTarget, Initialize выбирает известный тип. Новых пакетов нет.

## Revision Note


15 ноября — план по прямым корректировкам пользователя и реальной передаче S2-03.

15 ноября, итог — смешанная сцена и оба исправления реализованы, внесены реальные тесты и открытая ручная приёмка. Локальная фиксация по .agent/S204_COMMIT_PATHS.txt без push/merge.
