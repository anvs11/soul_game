# S4-03A — детерминированный EncounterPlanner

18 ноября2026, У1. Ветка feat/stage4-encounters от main7cb16232d21b59c333de6e043c7dc611c39809c2, отдельный worktree Game/TestResults/s4-plan-review-20261118. Корневая грязная feat/stage3-layout не менялась. S4-05A0/боевые типы/карта этим поручением не реализуются. Никаких новых GameObject/сцен/planning side effects.

## API и воспроизводимость

Game.Encounters.EncounterPlanner:IEncounterPlanner, Generate(EncounterRequest)->EncounterResult. Конструкторы S4-01 не меняются. Алгоритм1/контент1; неподдерживаемые версии дают Failure. CreateEliteSchedule(int seed,int generatorVersion=1,int contentVersion=1)->ReadOnlyCollection<EliteStageSelection>. У записи StageId/FirstType/SecondType/DuplicateType только getters; внешний вызывающий не может создать невалидную запись через public constructor. Допустимые MapId fixed_street_1_1/1_2/1_4, фиксированные RoomId entry/combat_a/combat_b/combat_c/elite, география берётся только из входа.

Случайность: unchecked uint Weyl increment0x9e3779b9, затем xor>>16/multiply0x85ebca6b/xor>>13/multiply0xc2b2ae35/xor>>16; в документах обозначена SplitMix32, эти точные операции являются спецификацией. Seed смешивается XOR со стабильным FNV-1a по двум явным байтам каждого UTF-16 code unit ключа MapId/MapVersion/RoomId, версиями и номером попытки. Выбор Below(n)=Next()%n, permutation Fisher-Yates, не криптографический источник/не требование идеальной равномерности. UnityEngine.Random/System.Random/GetHashCode не используются. Нормализованный ordinal порядок RoomId/PointId, AllowedType проверяется по membership, порядок коллекций не влияет. Потоки локальны вызову, состояния на экземпляре planner нет.

В графике три elite-Id переставляются. Пары стадий циклические (a,b),(b,c),(c,a); дубликат выбирается только из пары. Так три будущие стадии гарантируют покрытие всех3 видов, даже если посещать/проверять их в обратном порядке. Геометрия1-2/1-4 не производится, это только данные расписания. В elite3 основных обязательных SpawnId,2 типа/2+1. ReservedSummons для grenadier не больше4/экземпляр и общего MaxSummons; это прежний технический ориентир S4-01, не приёмка боевого числа/радиусов реальных слизней.

Ordinary число ограничено Min/MaxEnemies и MaxCost, состав циклический из перемешанных3 типов (count>=3 содержит все3). По MaxWaves используются1 или2; combat_b гарантированно2 либо отказ неподходящему бюджету. Первая задержка0, вторая2 scaled seconds после завершения обязательств предыдущей волны, не от загрузки. Точки не HeroEntry, имеют разрешённый EnemyTypeId и достаточный clearance; выбранные круги одной волны не пересекаются/не касаются героя ближе суммы радиусов. Переиспользование безопасной точки между последовательными волнами допустимо. SpawnId room/wave/index/spawn/index уникален на плане попытки.

## Конечность и ошибки

Не более20 попыток и1024 проверок кандидатных точек на комнату/попытку. После каждой полной схемы существующий EncounterPlanValidator вызывается повторно. Если схема не найдена, Failure с Plan=null/UsedFallback=false и diagnostic seed/G/C/MapVersion/attempts/причиной. Success не возвращает непроверенную схему. Это ограниченный поиск, не доказательство существования раскладки для любого произвольного банка; допустимый сложный банк может исчерпать поиск. Fallback, расширенная валидация и production1000 seed остаются S4-03B. Статическая clearance-информация предполагает честный Unity-экспорт: не является проверкой реальных collider/путей или AI.

## Файлы и результаты

Новые Scripts/Encounters/EncounterPlanner.cs/.meta и Tests/EditMode/EncounterPlannerTests.cs/.meta относительно Game/Assets/_Game. Обновлены Game/Docs/Plans/S4_ENCOUNTER_PLAN.md, этот отчёт, добавочная граница S4_01_CONTRACT и только новая запись ROLE_1_STATUS. Существующие данные/validator/Example/StreetMap/Actors/Core/Input/боевые prefab не меняются. Сырые XML/логи/Library вне Git.

Unity6000.3.28f1 отдельно разрешён пользователем для S4-03A на этом worktree. Результаты проверены в Game/TestResults/s403a-validation, точные counts и SHA ниже. Старые Windows108/106 принадлежат У2 и не подтверждают новую реализацию.

Первый запуск Focused.log остановлен до тестов на Initial Refresh, exit143, XML не появился. Кэш пакетов переиспользован внутри проекта при побайтно совпавшем package lock; логи повторов сохраняются отдельно. Новый synthetic банк в тестах:5 комнат, ordinary6–8/8 точек, elite3/2; существующий EncounterExample2–4 и его runtime-потребители не менялись.

## Открытые условия

Ручной бой/Players/Android/физическая аттестация новой elite не исполнялись. Runtime У2 ещё ограничен fixed melee; этот planner в карту не подключён. S4-03B нужен для fallback/расширенной валидации/1000 seed, затем передача У2 S4-06A. Механика dodge S4-05A0 остаётся отдельным заданием, несмотря на изменённый пользователем порядок. Commit/push/merge этим поручением не выполняются.

## Подтверждённые результаты текущей версии

После полного рабочего cache Focused4.xml:12 discovered/executed/passed, failed/skipped/inconclusive0, exit0, duration0.078172с. Затем добавлены frozen seed0, совместимость исходного малого Example и проверка точного cap1024 (без превышения счётчика) — итог15 новых случаев. Полный EditMode.xml: **123/123/123**, failed/skipped/inconclusive0, exit0, duration0.9034375с. Эти15 входят в123, не прибавляются. Полный PlayMode.xml: **106/106/106**, failed/skipped/inconclusive0, exit0, duration158.9103471с. Это новый собственный Mac-прогон текущих исходников, отдельно от отчётных Windows108/106 У2. Пути относительно worktree: Game/TestResults/s403a-validation/{Focused4,EditMode,PlayMode}.{xml,log}, exits.json.

Проверка64 seed дала64 различных допустимых состава/точки на синтетическом каталоге; уникальность каждого seed не является условием, проверяется только несколько вариантов. Для каждого seed проверены пары и реальные pure-планы трёх будущих StageId, в том числе в обратном порядке. Это не1000 seed и не исполнение новых физических стадий.

Пример seed0/G1/C1/synthetic MapVersion2: combat_a8 врагов в волнах5+3, combat_b6 в2+4, combat_c7 в одной волне; elite одна nurse + два shield_shotgun. Все проверки бюджета/коллизий проходят. Полный Snapshot закреплён SHA256 a7d96878cb3f798444225410b6646eb9dcdcd7639f5ccebb65a8f7cc7fdaa222 в тесте VersionOneSeedZeroMatchesFrozenCompletePlan. Это золотой результат версии1, не запись состояния Unity Random.

Пример отказа: seed=-1/G1/C1, combat_a с clearance.1 не допускает обычные радиусы.5. Generate возвращает Failure, Plan=null, UsedFallback=false, Diagnostic содержит seed/версии/map/attempts=20/EXHAUSTED/POINT_CAPACITY. Повтор ошибки даёт ту же строку и не портит последующий успех. Плотный банк64 разных Id в одной позиции останавливается на searchNodes=1024, не зависает и не создаёт успех. Ошибка версии2 даёт UNSUPPORTED_VERSION до попыток.

История окружения: Focused.log и Focused2.log остановлены своими batch PID8850/9028 до Runner, оба exit143/без XML. Focused3.log с -noUpm закончился exit1 на отсутствующих пакетных UI/InputSystem assemblies, тоже без XML; это не failed test-case нового planner. Полная старая Library скопирована при идентичном packages-lock, прежняя сохранена в TestResults/s403a-validation/Library-initial; assets/Packages не перезаписывались. Focused4 и текущие полные наборы — обычный запуск с PackageManager. Все начальные логи сохранены, пользовательский Editor/lock не трогались.

## Окончательная сверка

Счётчики XML/total/testcasecount/уникальные fullname совпали, процессы exit0 сохранены в exits.json. verified-summary.json содержит основу/времена/SHA XML и исходников; сырые результаты исключаются из будущего Git payload. Все pre-existing Assets и ROLE_2_STATUS побайтно прежние; история ROLE_1_STATUS сохранена добавлением записи. Новые GUID уникальны. Diff --check пройден, индекс пуст, кода/сцен У2 нет в diff. Ни full123+106, ни focused12 не складываются в выдуманный единый набор. NOT RUN: Players/Android/ручной бой и физическая аттестация новой элиты.

SHA256 EditMode.xml: 122522d5a1ac4a7cba20e2a36d31604e5e423410deba47480c82668c5e36e840.

SHA256 PlayMode.xml: b4dec6134e8a544529b55f16f93e39c4522fae8519f7bdbc45e61b27ec399d05.

SHA256 Game/Assets/_Game/Scripts/Encounters/EncounterPlanner.cs: 8df18ddd09317aca3f637738156301136d614044a8db64545cbdc9bc3e22322a.

SHA256 Game/Assets/_Game/Tests/EditMode/EncounterPlannerTests.cs: 4c4eb34078503ca0a6943d1677f432ca6c973ed3174fe31c4c4ef001c7fd6f45.
