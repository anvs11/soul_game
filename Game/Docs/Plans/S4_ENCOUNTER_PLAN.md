# S4-01 — определить Encounter-контракт для постоянной карты


Этот ExecPlan ведётся по `.agent/PLANS.md`. Progress, Surprises & Discoveries, Decision Log и Outcomes & Retrospective обновляются по фактам.

## Purpose / Big Picture


У 2 сможет создавать комнаты/двери на неизменяемом конечном EncounterPlan, пока У 1 разрабатывает выбор столкновений по seed. Encounter означает весь бой одной комнаты, включая отложенные волны и обязательные призывы. Каркас должен компилироваться, давать пример пяти комнат и понятный отказ невалидным данным, не создавать GameObject и не завершать попытку.

## Progress


- [x] (2026-11-18) Сверены main ca5c26d, рабочая карта 4e42f8a и четыре исходных dirty файла; сохранены настройки пользователя.
- [x] (2026-11-18) Создана feat/stage4-encounters от 4e42f8a, чтобы не убирать уже проверенную карту.
- [x] (2026-11-18) S4_01_CONTRACT записан до кода: данные, ресурсные/боевые границы, единицы, волны и конечные призывы.
- [x] (2026-11-18) Созданы immutable данные, синтетический пример пяти комнат, структурный валидатор и тесты отказов/копирования.
- [x] (2026-11-18) Unity EditMode EncounterContractTests: 12 discovered/executed/passed, failed/skipped/inconclusive=0, exit=0. XML Game/TestResults/s401-20261118-a832/edit1/EditMode.xml.
- [x] (2026-11-18) ROLE_1_STATUS и ограничения платформ обновлены; подготовлена локальная передача собственного коммита У 2.

## Surprises & Discoveries


S4-02 уже реализован в cdb5578 и описан в 4e42f8a. Пользователь в одном поручении передал этому исполнителю S4-01 У 1 и затем S4-04A У 2. Поэтому контрактная ветка включает готовую карту в основании; отдельного сетевого merge/публикации не требуется. Входящий контракт до этой работы отсутствует.

## Decision Log


Решение 2026-11-18: контракт — новый Game.Encounters, отдельные версии algorithm/content/map, без изменения Generation/RunGenerator и v0.1 CombatSnapshot. Причина: фиксированная география имеет иной смысл, чем Stage 3 ModuleId.

Решение 2026-11-18: локальные координаты точки отсчитываются от центра комнаты; клетка = 1 world unit. DelaySeconds каждой волны отсчитывается после завершения предыдущей волны, scaled time. Первая волна — после безопасного входа. Так временное alive=0 при задержке явно не означает Cleared.

Решение 2026-11-18: призывы заранее резервируют конечные обязательства SpawnId; они входят в pending до материализации. Невыполненные слоты отменяются только явным сигналом смерти владельца/окончания способности с известным OwnerSpawnId. disable/unload не убийство и не успех. Предлагаемый технический максимум для гранатомётчика — 4 слота на экземпляр до отдельного боевого плана; число не объявляется пользовательским балансом.

## Outcomes & Retrospective


S4-01 завершён в объёме каркаса: чистые неизменяемые данные/пример/структурный validator, понятные отказы и дополнительные combat-интерфейсы. Реальный Unity прогон 12/12, exit0. S4-01 не содержит алгоритма выбора, мини-боссов, эффектов ресурсов или кувырка. Они остаются S4-03/05, интерфейсы не являются их готовой реализацией. Следующая работа этого поручения — S4-04A на этих точных типах. Windows Editor не подтверждает ручной Player/Android/Mac.

## Context and Orientation


Корень soul_game, Unity Game/. Код в Game/Assets/_Game/Scripts/, тесты Game/Assets/_Game/Tests/. Scripts/Contracts/CombatContracts.cs — существующая граница IPlayerCombatSource/CombatSnapshot, Actors/PlayerCombatFacade.cs передаёт события, Combat/DamagePacket.cs — единственный урон. StreetMapLayout У 2 описывает постоянные room bounds. Новые Encounter-данные чистые C# без UnityEngine.Random/GameObject. Синтетический пример не зависит от StreetMap, его можно передать отдельным коммитом.

## Plan of Work


В Scripts/Encounters/EncounterContracts.cs определить копирующие входные коллекции Point/Room/Budget/Map/Request/Spawn/Wave/RoomPlan/Plan/Result и IEncounterPlanner. Конструкторы проверяют структурную корректность, валидатор проверяет известные Id/пределы/версии/размещение. В EncounterExample.cs создать пять известных комнат и фиксированный пример обычных/elite-планов, отдельно от будущего случайного алгоритма. В Scripts/Contracts/Stage4CombatContracts.cs добавить только ресурсный и командный интерфейсы. Существующие combat/input не менять. Документ S4_01_CONTRACT содержит точные сигнатуры, таблицу состояний и владельцев будущих правок. EditMode тесты проверяют защиту от внешних мутаций, неизменность примера между вызовами, явные ошибки, конечные волны/резервы.

## Concrete Steps


Из корня проекта, PowerShell без профиля: проверить Get-Process -Name Unity и Game/Temp/UnityLockfile. Пользовательский процесс не завершать, lock не удалять; свободный оставшийся lock можно проверить эксклюзивным File.Open/Dispose. Разрешён ранее указанный Unity 6000.3.28f1. Запустить через Start-Process -WindowStyle Hidden -PassThru, WaitForExit и записать ExitCode.

    Unity.exe -batchmode -nographics -projectPath <root>/Game -runTests -testPlatform EditMode -testFilter Game.Tests.EncounterContractTests -testResults <root>/Game/TestResults/<new-id>/EditMode.xml -logFile <root>/Game/TestResults/<new-id>/editor.log

Это имя Unity.exe означает C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe, а все результаты находятся внутри проекта. Новая папка на прогон. Test Runner сам выходит, -quit для тестов не добавлять. Считать XML test-case, ноль не успех. После diff/check локальный commit явных файлов; затем git switch feat/stage4-map и git merge --ff-only feat/stage4-encounters — только локальная передача зависимости в рамках текущего поручения.

## Validation and Acceptance


Пример содержит entry/combat_a/combat_b/combat_c/elite. У 2 может прочитать RoomPlan и выполнить конечные появления, используя SpawnId/PointId/EnemyTypeId, не ожидая генератора. Неправильные Id/NaN/дубли/невалидная версия дают понятное сообщение до боя. Одинаковые входные данные не зависят от порядка mutable списков после конструктора. В S4-01 seed хранится, но не имитирует случайный выбор. Unity должна скомпилировать все исходники и реально исполнить новые тесты. Полные регрессии с картой выполняются в следующем S4-04A этого же поручения.

## Idempotence and Recovery


Только новые файлы; не заменять исходные assets, не reset/clean/pull. При совпадении пути сначала осмотреть существующий файл. Ошибка плана не создаёт объекты и не расходует ресурсы. Результат отказа содержит код/сообщение, Plan=null и UsedFallback=false. Будущий fallback обязан пройти те же проверки, а не скрывать отказ.

## Artifacts and Notes


Game/Docs/S4_01_CONTRACT.md — передача сигнатур/владения; Coordination/ROLE_1_STATUS.md — статус и реальные XML. В отчёте отдельны Windows Editor, ручной Player, Android и Mac. Builds/TestResults/Library не добавляются в Git. Исходные dirty документы и ProjectSettings.asset не входят в фиксацию.

## Interfaces and Dependencies


Публичные конструкторы и семантика полностью записываются в S4_01_CONTRACT. Коллекции — ReadOnlyCollection с defensive copy; типы sealed, свойства get-only, порядок копий сохраняется, поиск Id ordinal. Валидация полных случайных бюджетов, график пар на 1000 seed и физический экспорт реальной карты остаются S4-03/04B. IRunResourceSink и IDodgeCommandSink — добавление к v0.1; существующие события/расчёт урона остаются единственными.

Revision 2026-11-18: начальный план S4-01 перед реализацией и последующим S4-04A.

Revision 2026-11-18: каркас/контракт завершены, фактический focused EditMode 12/12, передача У 2 идёт локальным коммитом. Полный алгоритм S4-03 не заявляется.
