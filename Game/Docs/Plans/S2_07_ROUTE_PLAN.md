# S2-07 — соединить две арены одной попытки

Этот ExecPlan поддерживается по `.agent/PLANS.md`. База — `6a73363`, ветка `feat/stage2-arenas`. Дата: 15 ноября 2026.

## Purpose / Big Picture

Из Bootstrap игрок сможет выбрать Stage 2, зачистить смешанную арену с бочкой, пройти выходом к полицейскому и увидеть результат только после босса, смерти или отказа. Stage 1 останется отдельной кнопкой. F6 в отдельном PoliceBossPreview будет снимать треть максимального HP: 600→400→200→0. Это исправляет диагностический шаг150; реальные пороги400/200 уже верны.

## Progress

- [x] (2026-11-15) Прочитаны PLANS, передачи S2-05/06, Core, контроллер, существующие тесты. Unity проекта закрыта.
- [x] (2026-11-15) Исправить F6 и проверить точные пороги.
- [x] (2026-11-15) Добавить отдельный профиль маршрута и промежуточный переход без результата.
- [x] (2026-11-15) Добавить бочку точечно в Stage2CombatSandbox, создать Stage2BossSandbox через Unity.
- [x] (2026-11-15) Выполнить новые интеграционные сценарии и весь прежний набор, сохранить XML/числа.
- [x] (2026-11-15) Обновить передачу и ROLE_2_STATUS, проверить diff и локально зафиксировать явный список файлов.
- [ ] Ручная приёмка нового маршрута пользователем; Android/standalone остаются открытыми.

## Surprises & Discoveries

Пороги уже ровно треть/две трети HP. Ошибка впечатления вызвана диагностическим уроном150. Старый Stage2Diagnostic завершает попытку после одной арены; прежние тесты этого поведения сохраняются через явный диагностический запрос. Новый профиль будет отдельным признаком RunRequest при том же имени первой сцены.

## Decision Log

15 ноября, Codex: сохранить старый диагностический API и добавить RunRequest.Stage2. Это позволяет не менять смысл прежних Foundation/диагностических тестов, а Home переключить на настоящий маршрут.

15 ноября, Codex: передавать между новыми экземплярами героя только HP, броню, магазин, резерв, выносливость и выбор оружия через существующий CombatSnapshot в памяти RunCoordinator. Перезарядка, задержки атак, оглушение и защита от прошлого попадания не переносятся. Это не сохранение на диск и не лечение. Новая попытка получает исходные ресурсы.

15 ноября, Codex: смерть единственного обязательного босса завершает маршрут автоматически. Миньоны принадлежат боссу и очищаются его API, не добавляются к обязательному счётчику. Обычная арена по-прежнему требует три уникальные смерти и Interact в зоне выхода. Запрос перехода откладывается до LateUpdate; Defeat/Abandoned отменяют переход в том же кадре.

## Outcomes & Retrospective

Реализован маршрут с сохранением ресурсов, отдельной боссовой ареной и одним результатом. Полный EditMode40/40 и PlayMode78/78 прошли; первый PlayMode75/78 с тремя ошибками подготовки тестов сохранён. Пять сегментов, генерация, торговля, награды и постоянные сохранения вне задачи. Новая ручная приёмка, самостоятельное ревью У1, Android/Mac и реальные сборки не объявляются пройденными.

## Context and Orientation

Корень — `C:/Users/user/projects/soul_game`; Unity-проект `Game`. `Scripts/Core/RunCoordinator.cs` — единственный владелец попытки, `SceneFlow.cs` загружает одну сцену и связывает единственный FlowScreen. `Scripts/Sandbox/ArenaController.cs` подключает игрока, один TargetRegistry, настоящий HudPresenter и BattleControls. В `Scripts/AI/` бочка ExplosiveBarrel реагирует на реальный DamagePacket и не реализует IEnemyTarget. PoliceBoss реализует IEnemyTarget, Configure уже записан в отдельном prefab, Initialize(TargetRegistry,Transform) связывает цель и реестр. Смерть босса чистит его пули/миньонов; disable и unload не Died. Assets находятся под `Game/Assets/_Game/`; соответствующие передачи — `Game/Docs/S2_05_HANDOFF.md` и `S2_06_HANDOFF.md`.

## Plan of Work

Сначала изменить только HazardBossPreview: именованный диагностический шаг MaxHealth/3, подсказка F6, точный тест600/400/200/0. Затем расширить RunRequest флагом IsStage2Route, ScenePaths именем Stage2BossSandbox, а RunCoordinator свойством ActiveArena и методом CompleteArena(scene,Func<CombatSnapshot>). Этот метод принимает запрос первой арены; ResolvePendingFinish сначала разбирает итог, затем промежуточную загрузку. RunId/LastOutcome сохраняются, статистика игрока ожидает связывания нового героя. Неверная сцена/дубль/устаревший запрос отклоняются. На конечной сцене используется прежний FinishRun.

Добавить узкие методы восстановления состояния в HealthState/DamageReceiver/WeaponRuntimeState и PlayerCombatFacade без изменения Contracts v0.1. ArenaController принимает прежние три врага либо одного PoliceBoss. При связывании босса восстанавливает снимок до разрешения управления. При смене фазы выключает AI и управление, очищает все пулы своей сцены и held-ввод. Пауза использует прежний scaled-time и BattleControls; смерть не включает управление повторно.

Создать `Scripts/Editor/Stage2RouteScenes.cs`: открыть Stage2CombatSandbox, добавить один prefab бочки в свободной точке(-5,-6), сохранить точечно. Новый Stage2BossSandbox создать копией принятой PoliceBossPreview, убрать только диагностический компонент привязки, добавить ArenaController с одним боссом и существующими player/registry/HUD/exit. Никакой отладочной F6 в основном маршруте. Сцену добавить в EditorBuildSettings; BuildStage1* оставить без изменений. Повторный запуск сохраняет существующие ресурсы и не перезаписывает новую сцену.

## Concrete Steps

Все пути относительно корня. Изменения вне writable `.agent` выполняются разрешённым escalated инструментом. Редактор: `C:/Program Files/Unity/Hub/Editor/6000.3.28f1/Editor/Unity.exe`.

Из корня выполнить редактор с `-batchmode -quit -projectPath <root>/Game -executeMethod Game.Editor.Stage2RouteScenes.Apply -logFile <new-results>/create.log`. Затем по одному запуску `-batchmode -projectPath <root>/Game -runTests -testPlatform EditMode` и PlayMode с `-testResults <new-results>/EditMode.xml` либо PlayMode.xml, отдельными logFile. Не использовать -quit для runTests. Каждая попытка в новом каталоге Game/TestResults/s207-<guid>, первичные ошибки сохранять. Читать exitcode, XML test-case и итоговые total/passed/failed/skipped/inconclusive, ошибки логов.

## Validation and Acceptance

Добавить отдельные Core-тесты последовательности, дублирования, приоритета смерти и отказа на промежуточном переходе, провала загрузки. Новые PlayMode-сценарии начинают только из Bootstrap и проходят Home secondary: три врага, бочка вне счётчика, настоящий взрыв/толчок/оглушение и блокирование укрытием, зона выхода, сохранение ресурсов/RunId, отсутствие результата на переходе, один босс/registry/EventSystem, очистка активных пуль обоих владельцев, все три стадии/однократные призывы, пауза и смерть во время подготовки. Проверить десять циклов, разные исходы, одно завершение, новые объекты/ресурсы и отсутствие старых подписок. Прежние Foundation assertions и диагностические арены сохраняются. Ручной маршрут: открыть Bootstrap, Play, Stage 2 → уничтожить3 → зелёный выход E → босс → результат → В убежище. Уход из паузы и повтор восстанавливают Time.timeScale=1.

## Idempotence and Recovery

Перед изменениями сохранить git status и проверять diff. Не менять исходную CombatSandbox и принятые prefab, GUID. Создание нового scene guard-ится File.Exists; повтор не перезаписывает. Не удалять scratch, отчёты, пользовательские изменения, не использовать reset/clean. При ошибке исправить код и запустить новый каталог результатов; не скрывать первичные XML. Только локальный коммит явного списка после diff, push/merge не поручены.

## Artifacts and Notes

Фактические XML, команды, числа и ограничения будут внесены в `Game/Docs/S2_07_HANDOFF.md`. ExecPlan копируется в `Game/Docs/Plans/S2_07_ROUTE_PLAN.md`. Новый prefab не требуется: используются отдельные принятые ExplosiveBarrel и PoliceBoss без замены.

## Interfaces and Dependencies

RunRequest.Stage2(int seed=0), IsStage2Route; ScenePaths.Stage2BossSandbox; RunCoordinator.ActiveArena, PlayerTransfer (CombatSnapshot?), CompleteArena(string arena, Func<CombatSnapshot> readState). Сохраняются Stage2Diagnostic и Sandbox. CompleteArena не вызывает FinishRun(Victory) на первой арене. PlayerCombatFacade.RestoreForEncounter(CombatSnapshot) вызывает узкие валидируемые методы здоровья и оружия. ProjectilePool.ClearActive() возвращает активные пули через существующий Despawn. Ввод только Player.inputactions, UI прежний InputSystemUIInputModule.

Первоначальная редакция: план фиксирует минимальную интеграцию и реальные три стадии вместо устаревшей формулировки «две фазы».


Обновление после проверки, 15 ноября: CompleteArena принимает функцию чтения снимка и вызывает её в LateUpdate до выключения боя. Это учитывает последнее попадание в кадре выхода. Первый PlayMode total78:75 passed/3 failed. В новых тестах отключённый AI не публиковал Died, а установка Rigidbody.position ещё не обновила Transform, используемый взрывом. Подготовка исправлена без ослабления assertions. Полный повтор78/78, старые Foundation неизменны. Точные XML и команды — в передаче S2_07_HANDOFF. Ручная приёмка нового маршрута остаётся открытой.
