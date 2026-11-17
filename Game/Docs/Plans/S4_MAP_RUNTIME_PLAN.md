# S4-02 / S4-04A — постоянная карта, комнаты и двери


Этот ExecPlan ведётся по `.agent/PLANS.md`. Разделы Progress, Surprises & Discoveries, Decision Log и Outcomes & Retrospective обновляются по фактическому результату.

## Purpose / Big Picture


Игрок выбирает в существующем Home FixedStreet1_1 и проходит пять постоянных комнат по коридорам с физическими стенами и следящей камерой. S4-02 создал географию; текущий S4-04A добавляет запирание проходов трёх обычных комнат до завершения их конечных боёв. В combat_b после первого врага 2 scaled seconds ожидания второй волны: пустая комната ещё не открывает двери. Повторный вход не возрождает врагов и не повторяет Cleared. Пауза/выход домой и старые кнопки Stage1/2/3 сохраняются. Элитная площадка явно незавершена. Победа 1-1 не выдаётся за посещение заглушки. Полный будущий финал: четыре Cleared, один безопасный портал и результат 1-1/Home; это остаётся S4-04B/06.

## Progress


- [x] (2026-11-18) Сверены main ca5c26d, локальные diff и правила; создана feat/stage4-map от ca5c26d без изменения пользовательских файлов.
- [x] (2026-11-18) Определены размеры карты, фиксированные встречи и границы S4-02 до реализации.
- [x] (2026-11-18) Добавлены профиль, Home-кнопка, данные географии и следящая камера.
- [x] (2026-11-18) Новый geometry prefab и Stage4StreetMap созданы средствами Unity, create2.exit=0; исходные assets сохранены, ProjectSettings.asset SHA256 совпадает.
- [x] (2026-11-18) Полный EditMode: 87 discovered/executed/passed, failed/skipped/inconclusive=0, exit=0; связность, границы, профиль, сериализованные стены и контекст сцены проверены.
- [x] (2026-11-18) Полный PlayMode: 97 discovered/executed/passed, failed/skipped/inconclusive=0, exit=0. Новые 3 проверки физического маршрута/камеры/стены/паузы/Home/смерти прошли вместе с прежними 94.
- [x] (2026-11-18) PNG map/entry/elite получены реальным Unity render, preview exit=0; осмотрены связность/пороги/стены и маркировка заглушки. Runtime UI/ручной бой этим не заявляются.
- [x] (2026-11-18) S4_02_MAP_HANDOFF и ROLE_2_STATUS записаны с фактическими XML/числами/API/координатами/ограничениями.
- [x] (2026-11-18) 22 собственных исходных пути локально зафиксированы после staged diff/check: cdb557898588ca067dba3c9ec51d2311dcdf78d7. Пользовательские dirty файлы и scratch исключены, push/merge не выполнялись. Эта документация оформляется отдельной фиксацией.
- [x] (2026-11-18) По новому поручению получен S4-01 5a78ec697045d4c132bd9e251dd5a24ee2c6dd89 локальным ff из feat/stage4-encounters; его focused EditMode 12/12.
- [x] (2026-11-18) S4-04A: реализованы RoomEncounterState/RoomController/DoorController на точных типах S4-01, scene/prefab не менялись.
- [x] (2026-11-18) S4-04A: проверены две волны, pause/cancel/death/unload/dedup/summons, полный диагональный вход и knockback в дверь.
- [x] (2026-11-18) S4-04A: полный EditMode 103/103 и PlayMode 103/103, failed/skipped/inconclusive0, exit0. XML Game/TestResults/s404a-20261118-f18c/edit1/EditMode.xml и play1/PlayMode.xml.
- [x] (2026-11-18) S4-04A handoff и статус оформлены с точным S4-01 hash/API/ограничениями; локальная фиксация только собственных явных путей после diff.

## Surprises & Discoveries


В рабочем дереве до начала были изменения .agent/CONCEPT.md, Game/Docs/S3_08_ACCEPTANCE.md, Game/Docs/STREET_MAP_AND_BOSS_STRATEGY.md и Game/ProjectSettings/ProjectSettings.asset, а также старые scratch/untracked. Они не входят в фиксацию S4-02. ProjectSettings.asset содержит пользовательский preloadedAssets, его сохраняем. S4_01_CONTRACT ещё не получен; Game.Encounters не создаём. UnityLockfile обнаружен, пользователь сообщил, что закрыл проект; перед каждым batch нужна проверка блокировки.

Первая попытка создания обнаружила особенность Unity: batch начинает с untitled scene, и NewScene(Additive) отклоняется даже при отсутствии сохранённых изменений. create.log содержит InvalidOperationException; scene/prefab ещё не были записаны. Builder использует Single только в batch, Additive при ручном запуске. create2.log подтверждает S4_02_MAP_CREATED и exit=0. Первый процесс оставил свободный UnityLockfile после ошибки; при отсутствии процесса проверили эксклюзивное открытие файла, не удаляя и не меняя его.

## Decision Log


Решение (2026-11-18, У 2): отдельная RunRequest.FixedStreet1_1(seed) с ArenaId Stage4StreetMap и флагом IsFixedStreet1_1. RunCoordinator принимает профиль через тот же SceneFlow и сохраняет приоритет Defeat. Никакого StreetRoute для этого профиля. Причина — постоянная карта не является случайным выбором пяти модулей Stage 3.

Решение (2026-11-18, У 2): единица карты равна одной Unity world unit. Комнаты — entry Rect(-7,-6,14,12), combat_a Rect(13,-8,20,16), combat_b Rect(13,16,20,16), combat_c Rect(41,16,20,16), elite Rect(69,12,28,24). Коридоры Rect(7,-2,6,4), Rect(21,8,4,8), Rect(33,22,8,4), Rect(61,22,8,4). Координаты задают внутреннюю площадь пола, стены толщиной 1 снаружи. Минимальная ширина прохода 4, elite 28x24 оставляет место будущему тарану шириной 3, телепорту и конечным призывам. Это геометрический запас, ещё не аттестованный для новых collider У 1.

Решение (2026-11-18, У 2): фиксированные встречи — по два существующих melee в combat_a/b/c в локальных точках (-3,2) и (3,2). В S4-02 вход лишь активирует эту пару, без дверей, Cleared, волн, наград, мини-карты и портала. Безопасный entry не содержит врагов; elite содержит три цветные НЕБОЕВЫЕ метки в локальных (-7,4),(0,4),(7,4). Базовые HP/урон/8+16/нож/скорость не меняются. Причина — закончить один запрошенный шаг, не присваивать S4-04/05.

Решение (2026-11-18, У 2): отсутствие процесса Unity и свободное эксклюзивное открытие оставленного lock разрешают повторный batch; просто существующий файл после завершившегося собственного batch не равен работающему редактору. Пользовательский процесс не завершаем, lock не удаляем. Это позволяет исправить собственный неуспешный запуск, сохраняя ограничение пользователя.

Решение (2026-11-18, У 2, S4-04A): сохранить созданные scene/prefab без замены. StreetMapScreen при Bind создаёт новые RoomController и 6 физических дверей на границах трёх обычных комнат. Двери combat_a: (13,0) размер(.5,4), (23,8) размер(4,.5); combat_b: (23,16),(33,24); combat_c: (41,24),(61,24), соответствующие вертикальные/горизонтальные створы. Перед закрытием весь collider героя внутри room и каждый створ свободен с запасом .05. Триггерная область — математический room Bounds, проверяемый в FixedUpdate; это исключает раннее запирание при диагональном пересечении и повторные trigger callbacks от нескольких collider.

Решение (2026-11-18, У 2, S4-04A): фиксированные 6 существующих melee и баланс не меняются. combat_a/c получают одну волну по два; combat_b получает 1+1, задержка второй 2 scaled seconds после смерти первого. Пример Encounter У 1 содержит elite3/2 только как данные; runtime elite остаётся явной заглушкой и не получает Cleared/награду. Никаких порталов/мини-карты/подборов в S4-04A. До входа актёры соседей неактивны. Успех приходит только из реального Died/IsDead; disable/destroy сохраняет обязательство, cancel прекращает бой без Cleared.

Решение (2026-11-18, У 2, S4-04A): чистый RoomEncounterState учитывает primary SpawnId, reserved summon slots и live Id; RoomController — Unity-исполнитель с подписками, безопасным spawn retry и закрытием дверей. Cleared публикуется один раз с RunId/RoomId. Фактическую награду пока не выдаём, событие является единственным будущим входом её обработчика. Материализованный ребёнок остаётся обязательным после смерти владельца; только pending слоты его призыва явно отменяются. Таймеры обновляются FixedUpdate со scaled deltaTime, на pause/death/disable/unload не продолжаются.

## Outcomes & Retrospective


S4-02 реализован и проверен прежними 87/87 EditMode и 97/97 PlayMode. Поверх него S4-04A добавил конечный реестр обязательств/физические двери/волны через точный S4-01 5a78ec6: текущие полные EditMode 103/103, PlayMode 103/103, failed/skipped/inconclusive0, оба exit0, PlayMode duration191.670567 s. В полном числе 12 контрактных случаев и 4 новых pure-state/6 новых физических случаев, прежние assertions не менялись. Через настоящую физику пройден весь маршрут, отдельно проверены двери/отбрасывание/волны/остановка. Scene/prefab не заменялись; пользовательский ProjectSettings.asset SHA сохранён. Ручной бой/Game View, отдельный Player/Android/Mac/performance для новой версии NOT RUN. Контракт/данные не подменяют будущие генератор/мини-боссов/подборы/кувырок; мини-карта и итоговый портал остаются следующими задачами. Весь Stage4 не объявляется принятым.

## Context and Orientation


Unity-проект находится в Game/. Assets/_Game/Scripts/Core/RunTypes.cs хранит имена сцен и RunRequest, RunCoordinator.cs — один жизненный цикл попытки, SceneFlow.cs загружает сцену и привязывает единственный FlowScreen. HomeScreen.cs/FlowScreen.cs создают существующие кнопки во время работы. Player.prefab содержит действующий LocalInputSource, ActorMotor2D, DamageReceiver, WeaponController и собственный ProjectilePool. SandboxEnemy.prefab — уже принятый ближний враг. Новый StreetMapScreen наследует FlowScreen и использует тот же PlayerCombatFacade, TargetRegistry, HudPresenter и BattleControls. Новая геометрия не правит существующие prefabs или сцены.

## Plan of Work


Добавить StreetMapLayout.cs с постоянными комнатами/коридорами и проверкой клетки пола, BoundedFollowCamera.cs с ограничением видимой области по размерам viewport. В Core разрешить один новый профиль, в Home добавить отдельную кнопку и подпись результата 1-1 без изменения прежних обработчиков. StreetMapScreen.cs привязывает героя, обычных врагов, HUD, паузу/смерть/выход и активирует только посещённую комнату. Создать Editor/Stage4MapAssets.cs: до любой записи проверять отсутствие новых scene/prefab/meta; строить только новые объекты в новой сцене, добавить сцену в конец EditorBuildSettings. У стен слой World, SpriteRenderer и BoxCollider2D. Видимые полосы/таблички обозначают входы и направление, elite явно предупреждает о заглушке.

После кода добавить EditMode тесты связности всех клеток и границ/проходов, профиля/приоритета смерти, сериализованной сцены и сохранности старых сцен. PlayMode проверяет запуск кнопкой из Bootstrap, обычный Rigidbody2D проход по всем изгибам коридоров, физическое препятствие на стене, следование камеры, изоляцию до входа, паузу, смерть и повторную попытку. Тестовый источник команд используется только тестами, действующий ввод не подменяется в продукте.

S4-04A использует Game.Encounters.RoomPlan из полученного S4-01. RoomEncounterState.cs хранит конечные pending/alive слоты и последовательность волн; RoomController.cs связывает реальные Died/IsDead, безопасный spawn retry, cancel и однократный Cleared. DoorController.cs проверяет свободный створ физикой до запирания. StreetMapScreen.CreateRooms создаёт эти компоненты при Bind вокруг прежних шести actor ссылок; geometry/scene assets не пересоздаются. В combat_b одна цель на волну, задержка второй 2 секунды после смерти первой. Cleared-прогресс Ordinary не выдаёт финальной победы. RunId/RoomId событие предназначено будущему обработчику наград, фактических подборов ещё нет.

## Milestones S4-04A


Первый milestone — принять проверенный контракт 5a78ec6 локальным ff и реализовать чистый RoomEncounterState. Его полезный результат — при alive0/pending1 нельзя получить Cleared, конечные дети остаются обязательными после смерти владельца. Четыре RoomEncounterStateTests проверяют это настоящим производственным C# без сцены.

Второй milestone — связать этот реестр с существующей картой через RoomController/DoorController. Новых игровых assets не нужно: шесть World-створов создаются runtime, до входа соседние актёры выключены, закрытие требует свободного створа/полного входа. Результат наблюдается в Bootstrap: красный проход удерживает героя до всех волн, затем становится проходимым.

Третий milestone — полные EditMode/PlayMode в новой папке результатов и передача S4_04A_ROOMS_HANDOFF с реальными XML/числами. Приёмка требует реальных физических проверок диагонального входа/hero knockback/удержания врага, delay/pause/cancel и однократности Cleared. Два полных набора по103 прошли; manual/Player/Android не присваиваются этим тестам.

## Concrete Steps


Все команды выполняются из C:\Users\user\projects\soul_game, PowerShell без профиля. Перед Unity проверить отсутствие процесса Unity. При существующем Game/Temp/UnityLockfile проверить эксклюзивное открытие через System.IO.File.Open(..., Open, ReadWrite, None) и сразу Dispose; при занятом файле не запускать batch. Lock не удалять. Разрешён только указанный пользователем Unity.exe 6000.3.28f1. На Windows запускать через Start-Process -WindowStyle Hidden -PassThru и WaitForExit, записывая фактический ExitCode: прямой вызов GUI exe может вернуть управление до окончания.

    & 'C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe' -batchmode -nographics -quit -projectPath Game -executeMethod Game.Editor.Stage4MapAssets.Create -logFile Game/TestResults/<new-id>/create.log

    & 'C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe' -batchmode -nographics -projectPath Game -runTests -testPlatform EditMode -testResults Game/TestResults/<new-id>/EditMode.xml -logFile Game/TestResults/<new-id>/edit.log

    & 'C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe' -batchmode -nographics -projectPath Game -runTests -testPlatform PlayMode -testResults Game/TestResults/<new-id>/PlayMode.xml -logFile Game/TestResults/<new-id>/play.log

Пути XML/log в фактическом запуске передавать абсолютными внутри проекта. В тестовом запуске -quit не добавлять: Test Runner завершает процесс сам. Считать test-case по XML, отдельно записать discovered/executed/passed/failed/skipped/inconclusive и exit code. Ноль тестов не успех.

## Validation and Acceptance


В Unity открыть Bootstrap, Play, выбрать «1-1: постоянная карта · прототип». Герой появляется в безопасном entry, WASD/стрелки ведут вправо, затем вверх, затем вправо через три обычные комнаты к elite. Space/Q/E/Esc и экранные кнопки остаются прежними. Камера показывает окрестности и не выходит за прямоугольные границы всей карты. Стены останавливают физическое тело и блокируют бой; проходы различимы. Элита подписана как НЕ ГОТОВА, здесь нет победы/награды. Через Esc → Home получается результат выхода 1-1, новая попытка создаёт новый RunId/героя. Прежние кнопки запускают свои сцены. Автоматические проверки не заменяют ручную оценку читаемости, боя/баланса и проверку Android на телефоне.

В S4-04A дополнительно увидеть закрытие обоих проходов после полного входа, убить две цели combat_a/c. В combat_b убить одну цель: двери должны оставаться закрытыми при пустой комнате, Esc замораживает оставшуюся задержку; после продолжения появляется вторая цель, лишь её смерть открывает проходы. Повторное посещение не создаёт врагов. Программные проверки этой последовательности прошли; обычный ручной бой остаётся NOT RUN. Конкретные XML/кейсы и технические ограничения записаны в Game/Docs/S4_04A_ROOMS_HANDOFF.md.

## Idempotence and Recovery


Генератор новых assets отказывается при любом совпадении scene/prefab/meta до записи. Повторно существующие сцены не пересоздавать, открыть их для проверки. При частичном результате не удалять и не заменять assets без разрешения: диагностировать сохранённые файлы и продолжить отдельным безопасным дополнением. Не завершать процессы Unity и не удалять lock. Не выполнять pull/reset/clean. Кодовые точечные исправления разрешены текущим заданием, исходные игровые assets сохраняются.

## Artifacts and Notes


Передача: Game/Docs/S4_02_MAP_HANDOFF.md с координатами, API, файлами/meta, базой и ограничениями; статус: Coordination/ROLE_2_STATUS.md. XML/log остаются в Game/TestResults/<new-id>/ и не добавляются в Git. Локальная фиксация только явными собственными путями после diff, push/merge текущим заданием не запрошены.

Текущая передача S4-04A: Game/Docs/S4_04A_ROOMS_HANDOFF.md. Контракт Game/Docs/S4_01_CONTRACT.md принадлежит hash5a78ec6, его API не переопределяются в StreetMap. Результаты s401-20261118-a832/edit1 и s404a-20261118-f18c/edit1,play1 — отдельные, focused числа не прибавляются к полным.

## Interfaces and Dependencies


Game.StreetMap.StreetMapLayout содержит версию геометрии, неизменяемый доступ к описаниям комнат (Id, Bounds, SpawnPoints), коридорам и WalkableCells. Это описание Unity-географии У 2, не копия Game.Encounters. Game.StreetMap.StreetMapScreen.Configure(PlayerCombatFacade, SandboxEnemy[], TargetRegistry, HudPresenter) задаётся новым editor builder. BoundedFollowCamera.Configure(Transform, Rect) задаёт цель и внешние границы. Будущая передача S4-01 преобразует эти точки в точные контрактные типы У 1; до физической проверки новых типов clearance заявлен только для существующего героя/melee. Никаких новых пакетов, сетевой/сохранительной инфраструктуры или вторых сервисов Bootstrap не требуется.

Теперь S4-01 получен. RoomController.Configure(string runId, EncounterRoom description, RoomPlan plan, PlayerCombatFacade hero, TargetRegistry registry, SandboxEnemy[] actors, DoorController[] gates) принимает эти точные данные. RoomController.Cleared и StreetMapScreen.RoomCleared передают однократный run/room результат; Clear не из OnDisable. RoomEncounterState.Begin/Tick/MarkMaterialized/ConfirmDeath/Cancel проверяют конечные SpawnId. Детали дочерних слотов/параметров — в checked-in S4_01_CONTRACT и S4_04A_ROOMS_HANDOFF. Изменять Actors/Combat/Input на этом этапе не требуется.

Revision 2026-11-18: начальный план до реализации S4-02, фиксирует размеры, встречи и честную границу готовности.

Revision 2026-11-18: профиль/геометрия созданы, реальный EditMode 87/87; добавлено обнаруженное правило batch-сцен и безопасная проверка свободного lock, PlayMode выполняется.

Revision 2026-11-18: завершён полный PlayMode 97/97, записаны реальные ограничения ручной/платформенной приёмки; добавлен read-only редакторский рендер геометрии для визуального осмотра.

Revision 2026-11-18: три реальных PNG осмотрены, передача и статус закончены; остаётся локальная фиксация собственных файлов без пользовательских изменений и scratch.

Revision 2026-11-18: кодовая фиксация cdb5578 завершена, 22 явных пути; staged diff --check прошёл после удаления конечных пробелов только из пяти новых Unity YAML/meta. Передача содержит фактический hash. Документация фиксируется отдельно.

Revision 2026-11-18: начат запрошенный S4-04A после выполненного S4-01; фиксируются размеры дверей, 1+1 волна, .05 clearance, границы scope и pure state/Unity executor до кода. К существующим этапам добавлены три новых milestone: реализация конечного ledger, интеграция физических дверей/активации и содержательные тесты/передача. Ledger означает конечный реестр обязательств по каждому SpawnId; его проверки выполняются EditMode, физика/переходы — PlayMode. Ожидаемый результат: после убийства первой волны combat_b двери остаются закрытыми 2 s, на pause ожидание замерзает, после второй они открываются однократно; повторный вход ничего не создаёт. Команды полного EditMode/PlayMode выше применяются с новой папкой s404a-*; -executeMethod Create не запускать, assets уже существуют.

Revision 2026-11-18: S4-04A завершён, полные103/103 EditMode и103/103 PlayMode прошли с первого прогона. Обновлены purpose/milestones/outcomes/interfaces/validation и передача; manual/Players/Android остаются открыты, следующий S4-04B не выполняется без отдельного поручения.

Revision 2026-11-18: кодовый S4-04A зафиксирован в576f6282dd0ece47f0f31d4b91e5d5196b748949 после staged diff/check, 11 явных путей. S4-01 hash5a78ec6 сохранён на отдельной ветке. Документация/статус фиксируются отдельным коммитом; исходные четыре пользовательских dirty файла не включены.
