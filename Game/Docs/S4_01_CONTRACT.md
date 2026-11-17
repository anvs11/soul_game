# S4-01 — контракт Encounter v1 и общая боевая граница

18 ноября 2026. У 1/S4-01 временно выполняется исполнителем У 2 по прямому поручению пользователя. Основание main ca5c26d + проверенная карта 4e42f8a; ветка feat/stage4-encounters от 4e42f8a. Этот документ фиксируется до кода. Он добавляет интерфейсы к v0.1, не заменяет CombatSnapshot/IPlayerCombatSource/DamagePacket/TargetRegistry и прежний Generation.RunGenerator.

## Неизменяемые данные Game.Encounters

Все IEnumerable копируются в ReadOnlyCollection; классы sealed, свойства get-only. Пустые/пробельные Id, NaN/Infinity, отрицательные задержки/радиусы и дубли ключей отвергаются ArgumentException с названием поля. Это ошибка authoring, не игровая победа. Координаты float, время scaled seconds. Сигнатуры:

    SpawnPoint(string pointId, float x, float y, float clearanceRadius,
        IEnumerable<string> allowedEnemyTypeIds, bool isHeroEntry = false)
    RoomBudget(int minEnemies, int maxEnemies, int maxCost, int maxWaves, int maxSummons = 0)
    EncounterRoom(string roomId, float centerX, float centerY, float width, float height,
        RoomBudget budget, IEnumerable<SpawnPoint> points, bool isElite = false)
    EncounterMap(string mapId, int mapVersion, IEnumerable<EncounterRoom> rooms)
    EncounterRequest(int seed, int generatorVersion, int contentVersion, EncounterMap map)
    SpawnPlan(string spawnId, string enemyTypeId, string pointId, int reservedSummons = 0)
    WavePlan(string waveId, float delaySeconds, IEnumerable<SpawnPlan> spawns)
    RoomPlan(string roomId, IEnumerable<WavePlan> waves)
    EncounterPlan(int seed, int generatorVersion, int contentVersion,
        string mapId, int mapVersion, IEnumerable<RoomPlan> rooms)
    EncounterResult.Success(EncounterPlan plan, bool usedFallback, string diagnostic)
    EncounterResult.Failure(string diagnostic)
    IEncounterPlanner.Generate(EncounterRequest request) -> EncounterResult
    EncounterPlanValidator.Validate(EncounterMap map, EncounterPlan plan) -> EncounterResult

Map.Rooms, Room.Points/Budget, Plan.Rooms, RoomPlan.Waves, Wave.Spawns, SpawnPoint.AllowedEnemyTypeIds доступны только для чтения. У точки PointId уникален внутри комнаты, RoomId/WaveId/SpawnId ordinal case-sensitive; WaveId уникален в комнате, SpawnId глобально уникален в попытке. EnemyTypeIds: melee,jumper,shooter,elite_slime_grenadier,elite_shield_shotgun,elite_teleport_nurse. Класс EnemyTypes хранит известные Id и технические радиусы/стоимость примера: ordinary .5/1, elite 1/3. Они являются исходными ограничениями данных, новые реальные hitbox/баланс должны быть проверены экспортёром до S4-06.

Одна клетка = 1 Unity world unit. Room.Center — мировая позиция центра; SpawnPoint.X/Y локальны относительно центра. WorldSpawn = center + local. Width/Height — внутренний пол; стены снаружи. ClearanceRadius — свободный круг от World-геометрии, отдельно от расстояния между выбранными точками и героя. HeroEntry точки не разрешают врагов. У 2 до старта экспортирует проверенные collider-ом точки для каждого разрешённого вида. Синтетические данные S4-01 не объявляются физической аттестацией новых мини-боссов.

Min/MaxEnemies учитывает основные SpawnPlan, MaxCost — сумму EnemyTypes.Cost, MaxWaves — конечное число волн, MaxSummons — сумму зарезервированных дочерних слотов. Лимит конструкции — 256 основных врагов/комнату, 16 волн, 64 слота призыва, задержка максимум 60 s на волну; это технические конечные границы прототипа, не баланс. entry имеет бюджет 0/0/0/0 и пустые волны; три combat — ordinary; elite отмечена IsElite и в настоящем будущем плане должна иметь 3 основных/2 elite-вида (2+1).

## Время, SpawnId и Cleared

Волны идут в записанном порядке. DelaySeconds первой отсчитывается после безопасного полного входа героя и закрытия дверей. DelaySeconds следующей начинается, когда погибли все обязательные текущей волны и завершены/явно отменены её конечные призывы. Счётчик времени уменьшается только на scaled deltaTime; пауза его замораживает. Нет скрытого таймера от загрузки сцены. Исполнитель хранит оставшиеся запланированные появления и живые уникальные SpawnId. Cleared публикуется один раз только когда оба равны нулю и все волны окончены. Дубли Died/callback не меняют счёт повторно. OnDisable/Destroy/выгрузка не подтверждают убийство.

Каждый grenadier имеет конечные reservedSummons до старта, предложение для будущего боевого прототипа — максимум 4 на экземпляр. Слоты получают Id ownerSpawnId + "/summon/" + index (0-based), индекс не переиспользуется. Пока слот не материализован, он pending. У 1 вызывает TryMaterializeSummon(ownerSpawnId,index,enemy), а при смерти владельца/завершении способности до использования слотов — CancelUnmaterializedSummons(ownerSpawnId); при смерти владельца живые дети остаются обязательными. Оба вызова проверяют OwnerSpawnId и конечный индекс; отмена чужих слотов/повторная материализация запрещены. Число слизней/их атаки определяются S4-05, контракт не создаёт самих слизней. Если пул/точка недоступны, слот остаётся pending либо встреча получает явный Faulted, никогда не Cleared из-за отказа.

Остановка комнаты при смерти героя/выгрузке/disable отменяет таймеры и подписки, переводит в Cancelled, не даёт Cleared/награду. До входа в соседнюю комнату её актёры неактивны и не зарегистрированы. Дверь закрывается только когда весь герой прошёл створ и створ физически свободен; иначе вход откладывается. Уже начатый бой удерживает героя/врагов до Cleared. RoomController хранит runId/roomId ключ завершения для однократного внешнего обработчика наград; фактическая выдача — позже S4-06.

## Результат генерации и отказ

EncounterResult имеет IsSuccess/Plan/UsedFallback/Diagnostic. Failure всегда Plan=null/UsedFallback=false, Diagnostic непустой с префиксом кода (например UNKNOWN_POINT: combat_b/missing). Success содержит только валидный план. Generate — интерфейс будущего S4-03, алгоритм/1000 seed/fallback в S4-01 НЕ реализованы. До старта validate проверяет версии/Id/бюджеты/точки/радиусы и непересечение кругов в пределах волны. Неподходящие данные не расходуют патроны, не создают GameObject, не запускают попытку. Будущий fallback для той же MapId/MapVersion проходит тот же validator. Физика/достижимость входов — ответственность Unity-экспортёра У 2, который не должен выдавать непроверенный каталог.

## Ресурсный API — интерфейс сейчас, эффект в S4-05B

Новый Game.Contracts.IRunResourceSink: int TryAddReserve(int amount), float TryHeal(float amount), float TryRepair(float amount). Возвращается фактически принятое количество. Неположительный запрос даёт 0, NaN/Infinity дают отказ без изменения. Реализация на PlayerCombatFacade проверяет живого героя/активную попытку/паузу, ограничивает HP/броню максимумами, reserve безопасным определённым контентом пределом, не загружает магазин и не прерывает reload. Нулевой полезный эффект оставляет pickup. За полезную транзакцию ровно одно существующее Changed; новых Changed/Died/расчётов урона не добавлять. Dedup PickupId делает pickup, не этот количественный sink. Профиль/диск/валюта не участвуют. Точный cap reserve и таблица наград записываются в S4-05B до реализации; интерфейс не утверждает неоговорённый баланс.

## Состояния и команда кувырка

Новый Game.Contracts.ActorActionPermissions содержит CanMove/CanAttack/CanDodge/CanSwitchWeapon. IActorActionGate.Permissions — агрегат действующих независимых источников, не новый ввод. Новый IDodgeCommandSink.TryRequestDodge(UnityEngine.Vector2 direction) -> bool: одна edge-команда, zero означает последнее ненулевое направление движения, затем последнее направление прицеливания, затем down. false не списывает ресурс/не сбрасывает cooldown. Это объявление границы, реализации пока нет. Через действующий PlayerCommand У 1 позднее добавляет get-only DodgePressed и последний необязательный аргумент dodgePressed=false к существующему конструктору. LocalInputSource У 2 передаст WasPressedThisFrame; удержание не является повторными запросами. Player.inputactions пока не меняется.

| Состояние | Движение | Атака | Кувырок | Смена оружия | Пауза/домой |
|---|---|---|---|---|---|
| Обычное | да | да | если готов | да | да |
| Яд | да | да | если готов | да | да |
| Замедление | со множителем | да | если готов | да | да |
| Root лозы 3 s | нет | да | нет | да | да |
| Bind розовым попаданием 3 s | нет | нет | нет | да | да |
| Stun, включая туман 2 s | нет | нет | нет | да | да |
| Knockback | только толчок до стены | да | нет | да | да |
| Смерть/переход/пауза | нет | нет | нет | нет | системный путь доступен |

Эффект имеет SourceId/EffectId, собственное время/политику снятия. Разные источники не стирают друг друга. Root/bind/stun запрещают кувырок; cooldown/стоимость не расходуются при отказе. Туман: stun ровно 2 s, только яд/slow до смерти её источника, затем снять именно его эффекты. Повторный tick тумана не продлевает stun бессрочно. Старый ActorMotor2D.Stun сейчас разрешает атаку; изменение произойдёт в S4-05A0 с явным обновлением тестов/причиной, не скрыто в S4-01. Числа длины/скорости/cooldown/стоимости/неуязвимости кувырка остаются предложениями будущего боевого ExecPlan, здесь не назначаются. Движение/толчок только Rigidbody2D, не Transform.

## Явное владение файлами

У 1: новые Scripts/Encounters/*.cs; будущие Scripts/Actors/ActorStatusController.cs, PlayerDodgeController.cs; точечные Actors/ActorMotor2D.cs, Actors/PlayerCombatFacade.cs, Actors/HealthState.cs, Actors/DamageReceiver.cs; Combat/WeaponController.cs, Combat/WeaponRuntimeState.cs; Input/PlayerCommand.cs (только command contract). У 1 владеет новыми AI/Stage4 и Combat/Stage4Resources, prefab новых противников/подборов и их диагностическими сценами. Общий новый Contracts/Stage4CombatContracts.cs сейчас содержит только интерфейсы, дальнейшие изменения сначала отражаются здесь. До ранней передачи движения эти общие файлы не правит У 2.

У 2: Input/LocalInputSource.cs, Input/Player.inputactions, UI/BattleControls.cs и UI/HudPresenter.cs (адаптер/кнопки/отображение), Scripts/StreetMap/*.cs, Core-профиль/маршрутизация, новые map assets и интеграционные тесты. После S4_05_MOVEMENT_STATUS_HANDOFF У 2 подключает готовый DodgePressed/API, не копирует механику. CombatContracts.cs/v0.1, исходные enemy prefab и Generation не заменяются ни одной стороной в этом шаге.

## Текущий объём и доказательства

Синтетический пример создаёт все 5 комнат и обычные фиксированные планы, включая отложенную волну, а также данные elite 3/2. Наличие elite-Id в данных не означает наличие исполняемого prefab. Конструкторы/validator/пример и интерфейсы компилируются и проверяются Unity EditMode; реальные XML/числа и hash добавляются после прогона. Windows Editor не заменяет standalone/Android/Mac, ручной бой/баланс/нагрузка остаются открыты. Следующий исполняемый шаг по текущему поручению — S4-04A с ordinary-встречами и явной заглушкой elite.

Фактический focused Unity6000.3.28f1 EditMode: Game/TestResults/s401-20261118-a832/edit1/EditMode.xml, discovered=12/executed=12/passed=12, failed/skipped/inconclusive=0, exit=0. Проверены defensive copy, неизменяемые коллекции, пример 5 комнат/elite3/2, конечные summon Id, NaN/Infinity/длинные задержки/дубли/точки/версии/неизвестные типы/пересечения и понятные Failure. Новые .meta импортированы Unity. Исходные Actors/Combat/Input/Generation и CombatContracts.cs не менялись; добавлен только Contracts/Stage4CombatContracts.cs. Hash передачи — коммит, впервые добавляющий этот файл (git log -1 --format=%H -- Game/Docs/S4_01_CONTRACT.md), его точное значение фиксируется в последующей S4-04A передаче после локального интегрирования. Кодовый коммит содержит новый Scripts/Encounters/ с .meta, Stage4CombatContracts.cs/.meta, EncounterContractTests.cs/.meta, этот документ/ExecPlan и ROLE_1_STATUS.
