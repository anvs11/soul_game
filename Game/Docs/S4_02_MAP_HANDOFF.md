# S4-02 — постоянная карта 1-1, передача У 2

18 ноября 2026. База `main ca5c26d`, ветка `feat/stage4-map`. ExecPlan: `Game/Docs/Plans/S4_MAP_RUNTIME_PLAN.md`. Этот шаг создаёт географический прототип, не завершённый боевой срез Stage 4. S4-01/Encounter/мини-боссы ещё не получены. Push/merge не запрошены.

Локальный кодовый commit: `cdb557898588ca067dba3c9ec51d2311dcdf78d7` (22 явных пути, включая все новые .meta). Этот handoff/ExecPlan/ROLE_2_STATUS фиксируются следующим документационным коммитом. Для передачи нужны оба коммита; кодовая база тестов и рендера описана ниже. Перед фиксацией из пяти только что созданных YAML/meta убраны конечные пробелы, без изменения сериализованных значений; исходные пользовательские assets не форматировались.

## Запуск и видимый результат

В Unity 6000.3.28f1 открыть `Assets/_Game/Scenes/Bootstrap.unity`, Play, нажать новую верхнюю кнопку «1-1: постоянная карта · прототип S4-02». Старые Stage 1/Stage 2/StreetRun кнопки сохранены. Герой начинает в безопасном входе; путь идёт вправо → вверх → вправо через combat_a/b/c к elite. Управление прежнее: WASD/стрелки, Space, Q, E, Esc и BattleControls. Камера следует за героем, её видимая область ограничена внешним прямоугольником карты. В elite три оранжевые НЕБОЕВЫЕ метки и надпись NOT READY. В HUD прямо указана незавершённость элиты/дверей/наград/портала. Через Esc → «Выйти в убежище» возвращаемся к результату «1-1: выход домой»; смерть показывает результат поражения.

## География v1

Одна клетка равна одной Unity world unit. Прямоугольники ниже описывают внутренний пол, min включён, max исключён при перечислении клеток. Все координаты мировые; prefab геометрии находится в (0,0), поэтому совпадают с локальными. Стены из 404 единичных BoxCollider2D снаружи пола, включая углы, слой World, не trigger. В пол и в ширину прохода они не выступают. Цветные полосы и поперечные отметки обозначают проходы. Никакой случайной перестановки геометрии нет.

| RoomId | Минимум | Размер | Центр | Точки сейчас (world) |
|---|---|---|---|---|
| entry | (-7,-6) | 14×12 | (0,0) | герой (0,0), врагов нет |
| combat_a | (13,-8) | 20×16 | (23,0) | melee (20,2),(26,2) |
| combat_b | (13,16) | 20×16 | (23,24) | melee (20,26),(26,26) |
| combat_c | (41,16) | 20×16 | (51,24) | melee (48,26),(54,26) |
| elite | (69,12) | 28×24 | (83,24) | заглушки (76,28),(83,28),(90,28) |

Коридоры: entry→combat_a Rect(7,-2,6,4), combat_a→combat_b Rect(21,8,4,8), combat_b→combat_c Rect(33,22,8,4), combat_c→elite Rect(61,22,8,4). Ширина каждого 4. Общая граница камеры Rect(-8,-9,106,46), ortho half-height 7.5. При viewport крупнее границы по оси камера центрируется, при обычных размерах вся видимая область укладывается в эту границу. Это граница всей карты, не отдельной комнаты: снаружи пола виден тёмный фон.

Elite 28×24 и три разнесённые метки оставляют геометрический запас под 3 будущих экземпляра, таран шириной 3 и телепорт. Clearance новых collider/предупреждений/слизней ещё НЕ аттестован. Типизированный экспорт PointId/EnemyTypeId и проверка физикой всего банка принадлежат S4-04B после точных типов S4-01. Здесь Game.Encounters и его копии не создавались.

## API и жизненный цикл

`RunRequest.FixedStreet1_1(int seed = 0)` создаёт профиль со сценой `ScenePaths.Stage4StreetMap`, признак `IsFixedStreet1_1`. `RunCoordinator.StartRun` принимает его тем же путём, создаёт новый RunId; StreetRoute/StreetGeneration/PlayerTransfer для него отсутствуют. Seed хранится, но фиксированные встречи пока от него не зависят. `SceneFlow` по-прежнему находит единственный FlowScreen, один GameBootstrap остаётся между сценами. Приоритет Defeat перед Victory/Abandoned сохранён.

`StreetMapLayout.Rooms` и `Corridors` доступны только для чтения, Room содержит Id/Bounds/SpawnPoints, `WalkableCells()` перечисляет весь пол; `ContainsHero(Room, Bounds)` требует полного пересечения порога collider-ом с запасом .1. Это география, не Encounter-контракт У 1.

`StreetMapScreen.Configure(PlayerCombatFacade, SandboxEnemy[], TargetRegistry, HudPresenter)` связывает действующие компоненты. Шесть исходных melee изначально неактивны; по два активируются после первого полного входа в соответствующую обычную комнату, Initialize получает единственный registry/героя. Повторное посещение не пересоздаёт врагов. До входа враги соседних комнат не видят/не атакуют героя. После посещения без дверей они могут следовать за героем — изоляция запертого боя относится к S4-04A. Отключение/выгрузка не считается убийством, наград и Cleared пока нет. Исходные prefab/боевой контракт v0.1/баланс/ввод не менялись.

`BoundedFollowCamera.Configure(Transform, Rect)` задаёт героя и границу; статический Clamp учитывает ortho-размер и aspect. Положение обновляется в LateUpdate, физика героя остаётся в ActorMotor2D/FixedUpdate. Runtime-поиска всех объектов каждый кадр нет. `Pause/Resume/Leave` используют прежние BattleControls; на фазовом выходе/смерти выключаются команды/враги, очищается пул героя, подписки/HUD снимаются, timeScale восстанавливается.

Финальный контракт будущего среза: после четырёх обязательных Cleared и всех конечных появлений один безопасный портал у elite/героя, E вблизи завершает `RunOutcome.Victory`, Home показывает результат «1-1: зачистка». Награды/ресурсы не переносятся в постоянный профиль. Сейчас победу не вызывает ни посещение elite, ни убийство шести диагностических melee; доступен явный выход/смерть. Не объявлять заглушку принятой стадией 1-1.

## Файлы и assets

Точечные изменения: `Scripts/Core/RunTypes.cs`, `Scripts/Core/RunCoordinator.cs`, `Scripts/UI/HomeScreen.cs`, `ProjectSettings/EditorBuildSettings.asset` (только добавлена новая сцена). Все пути Scripts здесь относительно `Game/Assets/_Game/`. Новые файлы:

- `Scripts/StreetMap/StreetMapLayout.cs` и `.meta`.
- `Scripts/StreetMap/BoundedFollowCamera.cs` и `.meta`.
- `Scripts/StreetMap/StreetMapScreen.cs` и `.meta`, папка StreetMap и `.meta`.
- `Scripts/Editor/Stage4MapAssets.cs` и `.meta` — создание новых assets через меню; повторный вызов отказывает до записи, если scene/prefab/meta уже есть.
- `Prefabs/Rooms/Stage4/FixedStreetGeometry.prefab` и `.meta`, папка Stage4 и `.meta`.
- `Scenes/Stage4StreetMap.unity` и `.meta`.
- `Tests/EditMode/FixedStreetMapTests.cs` и `.meta`.
- `Tests/PlayMode/FixedStreetMapPlayTests.cs` и `.meta`.
- Этот handoff, ExecPlan и дополнение `Coordination/ROLE_2_STATUS.md`.

GUID новой сцены `e94ec9e1d54365b4d8e8bc8ccd524513`, prefab геометрии `14496c612bdd4af439cf3ce0099acdcb`. Исходные сцены/prefabs/Generation/Actors/Combat/Input/Player.inputactions не заменялись. Пользовательские изменения ProjectSettings.asset сохранены побайтно: SHA256 `AEA1C4F2DBC3C4AF95513EAD72353912AB57E0FB9A0E3671506F8259B56B90F5` до/после Unity. Чужие незавершённые документы и scratch не включать в фиксацию.

## Проверка

Assets созданы настоящим Unity editor API; `create.log` — неуспешная первая попытка (новая additive сцена при untitled batch-сцене), `create2.log`/`create2.exit` — успешное создание с кодом 0 после исправления выбора Single в batch. Пользовательские процессы не завершались, lock не удалялся. После аварийного первого batch остался свободный lock: перед повтором проверены отсутствие процесса Unity и эксклюзивное открытие lock без изменения/удаления.

Полный EditMode: `Game/TestResults/s402-20261118-57a9df/edit1/EditMode.xml`, discovered=87, executed=87, passed=87, failed=0, skipped=0, inconclusive=0; exit=0. Семь новых случаев входят в 87, не прибавляются. Проверены связность каждой клетки пола, безопасный entry/размер elite/коридоры, полное диагональное пересечение collider, камера при landscape/portrait/wide, новый профиль/смерть/новый RunId, настоящие сериализованные World-стены и единственный FlowScreen/Camera/EventSystem/InputSystemUIInputModule.

Полный PlayMode: `Game/TestResults/s402-20261118-57a9df/play1/PlayMode.xml`, discovered=97, executed=97, passed=97, failed=0, skipped=0, inconclusive=0; exit=0, duration=177.2011585 s. Три новых случая входят в 97; прежние 94 также прошли, assertions прежних тестов не менялись. Новый проход Bootstrap_WalkEntireCorridorRouteWithPhysicsAndFollowingCamera — Passed, 20.150921 s: восемь контрольных точек через все пять комнат, настоящий Rigidbody2D/ActorMotor2D с тестовым источником команд, без teleport/Transform перемещения; все 6 диагностических врагов активировались только по посещению и были программно убиты; камера следовала, ложная победа отсутствовала. PhysicalWall_PauseLeaveAndNewAttemptKeepFreshHeroAndResult — Passed, 3.039881 s: движение в реальную стену останавливается, пауза замораживает тело, повторный выход возвращает Home один раз, результат подписан 1-1, новый RunId и новый герой. HeroDeath_ReturnsHomeAndRetainsDefeatPriority — Passed, .924346 s: гибель побеждает одновременную заявку Victory.

Ручной бой/читаемость Game View, самостоятельный Player, Android/мультитач/Safe Area на устройстве и performance в S4-02 NOT RUN. Программное движение и тесты не называются ручной приёмкой. Старые доказательства Stage 3 не превращаются в полную приёмку Stage 4. Визуальные PNG сцены дополнительно создаёт редакторский `Stage4MapAssets.CapturePreviews`, без сохранения изменений scene/prefab; это технический осмотр геометрии, не ручной бой.

Редакторский рендер выполнен с графическим устройством, exit=0: `Game/TestResults/s402-preview-20261117-211404-90c08433/map.png`, `entry.png`, `elite.png` (имя папки содержит UTC, локальная дата — 18 ноября). Все три PNG просмотрены: видны связная география, стены/открытые проходы, полосы порогов, безопасный вход и три отдельные заглушки elite с GEOMETRY ONLY. Runtime UI этим статическим рендером не проверялся. Метод рендера добавлен после тестов, не меняет runtime/scene/prefab; его компиляция и исполнение подтверждены `preview/editor.log`/`exit.txt`. `source-hashes.json` в основной папке результатов фиксирует итоговые исходники.

## Следующий отдельный шаг

Ручная проверка географии из Bootstrap по маршруту выше. После её замечаний — отдельный S4-04A с RoomController/DoorController и учётом будущих появлений; затем S4-04B с мини-картой/типизированным экспортом/порталом. Кувырок только после API S4-01/S4_05_MOVEMENT_STATUS_HANDOFF, планировщик и мини-боссы подключаются позднее устойчивыми передачами У 1. Действующие исходные боевые файлы У 1 этим шагом не правились.
