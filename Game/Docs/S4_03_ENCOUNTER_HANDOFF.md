# S4-03 — передача EncounterPlanner и валидатора У2

18 ноября2026, У1. Основа main7cb16232d21b59c333de6e043c7dc611c39809c2 + локальные S4-03A/B. Ветка feat/stage4-encounters, worktree Game/TestResults/s4-plan-review-20261118 в исходном репозитории. Код пока не закоммичен: нового commit hash нет. Исходные ветки/черновики сохранены, Stage4-карта и боевые типы У2 не правились. Алгоритм/контент1/1 достроены до первой публикации; золотой успешный seed0 из A сохраняется. Исторические A/У2 XML не переименовываются в результаты B.

## Состав и API

Game/Assets/_Game/Scripts/Encounters/EncounterPlanner.cs/.meta — planner и read-only EliteStageSelection; EncounterPlanValidator.cs — общий/stage/schedule validator. Game/Assets/_Game/Tests/EditMode/EncounterPlannerTests.cs/.meta сохраняет15 проверок A (только два helper получили internal для повторного использования фикстуры); новый EncounterValidationTests.cs/.meta содержит проверки B и production sweeps. Contracts/EncounterContracts и старые конструкторы, Example, карты/Scene/Actors/Core/Input/Generation не заменяются. Документы: Plans/S4_ENCOUNTER_PLAN.md, S4_01_CONTRACT.md (добавления), S4_03A_PLANNER_REPORT.md (история), этот handoff и только новая запись Coordination/ROLE_1_STATUS.md. XML/Library/Builds/секреты не входят в исходники. Всё вместе с сохранённой A-зависимостью —12 путей, существующие meta не заменяются.

EncounterPlanner:IEncounterPlanner.Generate(EncounterRequest)->EncounterResult. Перегрузка Generate(EncounterRequest,EncounterPlan fallback) использует явно переданный резерв; null включает default reserve. CreateEliteSchedule(seed,g=1,c=1)->ReadOnlyCollection<EliteStageSelection> с StageId/FirstType/SecondType/DuplicateType для fixed_street_1_1/1_2/1_4. Обязательные RoomId entry/combat_a/combat_b/combat_c/elite. Собственная геометрия не создаётся.

EncounterPlanValidator.Validate(map,plan)->EncounterResult — проверка данных одной/нескольких комнат; остаётся совместимой с RoomController, который валидирует отдельную комнату. Проверяет algorithm/content1, MapId/MapVersion, число/Id комнат, глобальные основные/зарезервированные SpawnId, known EnemyTypeId/PointId, allowed/hero_entry/радиусы, overlap внутри волны, бюджеты count/cost/waves/summons, elite3/2. Арифметика расстояний double, hero radius.45, ordinary.5/elite1 — текущие технические размеры данных, не реальные hitbox элиты.

ValidateStage(map,plan)->EncounterResult дополнительно проверяет фиксированные5 комнат и правильные kind, ровно один достаточный hero_entry в каждой, safe entry без врагов,1–2 волн/first delay0, обязательные2 волны combat_b с положительной второй задержкой, все3 ordinary типа при количестве>=3, одну elite-волну и точное соответствие seeded-паре/дубликату. **Перед запуском всей1-1 вызывать ValidateStage, не ограничиваться Validate.** Unknown Room/Enemy/Point, дубли SpawnId, переполнение бюджета и плохой радиус дают явный Failure, Plan=null. Ошибки авторинга вроде NaN/Infinity/повторных PointId/WaveId отсекают прежние immutable конструкторы ArgumentException до runtime.

ValidateSchedule(IEnumerable<EncounterMap>,IEnumerable<EncounterPlan>)->EncounterScheduleValidation с IsValid/Reasons (read-only). Требует ровно3 уникальные стадии1-1/1-2/1-4, общие seed/generator/content, каждый ValidateStage и покрытие всех3 elite-типов. Порядок входных коллекций/посещений не важен; карты1-2/1-4 остаются только pure данными этого графика, не физически созданными сценами. Generic.Validate не знает полного графика, потому для произвольной 3/2 группы может пройти, а Stage/Schedule отклонят неправильную seeded-пару.

## PRNG, конечность и fallback

Точная битовая версия и ordinal порядок остаются S4-03A: локальный SplitMix32/Weyl+Murmur finalizer, FNV по двум байтам UTF16 ключа MapId/MapVersion/RoomId, unchecked uint. UnityEngine.Random/GetHashCode/глобальный Random не участвуют. Случайных попыток максимум20, поиск точек максимум1024 проверенных кандидата на комнату/попытку. Success всегда проходит ValidateStage. Переданный fallback проверяется на соответствие seed/версий/MapId/MapVersion и ValidateStage **до** случайного поиска: повреждённый резерв отклоняется даже если обычная генерация могла бы пройти.

После20 отказов default fallback строит один план: минимальное допустимое ordinary count, фиксированный cycle melee/jumper/shooter, ordinal PointId без shuffle, допустимые1–2 волн с обязательной отложенной combat_b, seed-график elite3/2. На выбор точек тот же конечный cap1024; резерв тоже проходит ValidateStage. Не найденный/невалидный default или supplied резерв возвращает Failure/Plan=null/UsedFallback=false, не «успех с непроверенным планом». Это конечный ограниченный поиск, не доказательство решаемости любого произвольного банка. Статический банк должен честно описывать физику; путь/LOS/телепорт/таран проверяет Unity-владелец карты.

Диагностика успеха/отказа содержит seed/G/C/MapId/MapVersion. Переход на резерв: attempts=20; last rejection=<reason>; validated fallback. Supplied metadata failure может быть preflight без20 попыток; неподдерживаемые версии отказывают до поиска. Повтор ошибки не хранит состояние на planner и не мутирует каталог. Точки между строго последовательными волнами могут повторяться; внутри волны пересечение кругов запрещено. Дочерние слоты никогда не создаются через вторую систему GameObject в planner.

## Семантика времени и исполнение для У2

Запускать только успешный целый stage-план, сохранив seed/версии/MapVersion и Diagnostic. Первая волна имеет DelaySeconds0 и начинается **после** безопасного полного входа героя и закрытия свободных створов. Delay следующей волны в генерируемом плане2 scaled seconds, отсчитывается только после завершения обязательств текущей, включая детей. Runtime использует Time.deltaTime/fixedDeltaTime: пауза замораживает задержку, unscaled/realtime для волны не применять. ValidateStage допускает конечную положительную следующую задержку до60 по constructor, а planner1 пишет2.

Pending — все ещё не материализованные основные спавны будущих волн и зарезервированные дочерние слоты. Alive — уникальные материализованные обязательные SpawnId. Пока есть pending/live в текущей волне, следующая не начинается; временное alive0 при delay не открывает дверь. Слоты дочерних id owner/summon/index заранее конечны. Materialize проверяет владельца/индекс, повтор не считается новым врагом. После смерти владельца отменяются только его неиспользованные слоты; живые дети остаются обязательными. Окончание способности может явно отменить неиспользованные слоты владельца. Ошибка пула/занятая точка не является убийством; сохранять pending либо сообщить читаемый Faulted, не Cleared.

При смерти героя/отказе/выгрузке/disable отменить таймеры/подписки и комнату, не выдавать Cleared/награду. Повтор Died и повторный вход не дублируют clear. Событие Cleared только после всех waves и pending=alive=0; повторно проверить RunId/RoomId в обработчике наград. Существующий RoomController пока принимает SandboxEnemy[]/melee, поэтому У2 должен обобщить исполнение через существующий IEnemyTarget/factory для всех разрешённых типов; pure успех planner не означает готовность runtime. Нет новой системы урона/ввода/AI.

## Пример вызова и исходов

Вызов: var request=new EncounterRequest(0,1,1,map); var result=new EncounterPlanner().Generate(request); if(!result.IsSuccess) показать result.Diagnostic до боя; иначе ValidateStage(map,result.Plan), сохранить метаданные и исполнять RoomPlan/WavePlan. Map берётся из проверенных immutable данных, не строится заново из случайных геометрических ModuleId. Для явного резерва Generate(request,validatedFallback) — seed и версии должны соответствовать request.

Успех synthetic seed0/G1/C1/MapVersion2 из A: combat_a8 в5+3, combat_b6 в2+4, combat_c7 в1 волне, elite nurse1 + shield2. Golden Snapshot SHA256 a7d96878cb3f798444225410b6646eb9dcdcd7639f5ccebb65a8f7cc7fdaa222, старый тест не ослаблялся.

Естественный резерв теста: к combat_a добавлены8192 явно непригодные по clearance.1 точки zzbad, исходные хорошие точки сохранены. Seed0 исчерпывает20 случайных поисков; ordinal резерв выбирает прежние хорошие точки. Реальный diagnostic: seed=0; G=1; C=1; map=fixed_street_1_1/2; attempts=20; last rejection=POINT_CAPACITY: combat_a; searchNodes=1024; validated fallback. Это синтетический authoring-случай, не утверждение о повреждённом каталоге У2.

Отказ: elite без enemy-точек исчерпывает основной поиск и default reserve, Failure с FALLBACK_FAILED. Supplied reserve с seed99 при request0 даёт FALLBACK_METADATA; неизвестный enemy в supplied reserve — FALLBACK_INVALID/UNKNOWN_ENEMY; Plan=null, UsedFallback=false. Тесты повторяют ошибку и затем вызывают валидный planner, не накапливая состояние.

## Проверки и реальный каталог У2

Каталог уже получен: Game/Docs/Exports/S4_FIXED_MAP_V2.json. SHA256 e6e8e11ced9f1a1830104740b074599ac489d9a9d181c0ef7fea74e4f8485ff6, MapVersion2/GeometryVersion1,32 точки, ordinary6–8, elite3/2/12 child slots. Проверка сравнивает каждое поле комнаты/бюджета/точки/allowed типов JSON с StreetMapCatalog.Create(), затем1000 production C# seed0..999 отдельно от синтетики; результаты ниже. Во втором C# вызове на том же seed/Unity-каталоге сверяется полный Snapshot, не только count. Эталон запускается дважды на1000 seed, для каждого seed проверяются все3 будущих stage-плана и ValidateSchedule; повтор Snapshot должен совпасть.

В export elitePrefabsCertified=false: proxy radius1 не аттестует реальных новых mini-boss prefab/таран/телепорт/дочерние точки/укрытия. По Id/числам/клеткам чистых данных замечания записываются ниже; scene/prefab У2 не исправляются. Новый физический экспорт после их реализации/укрытий остаётся обязанностью У2. Python/reference sweep не выдаётся за production C#; в этом шаге проверяется именно C#.

## Реальные результаты S4-03B

Первый Focused.xml:12/12/12 passed, failed/skipped/inconclusive0, exit0, duration1.5808523с. Reference1000 seed ×2 и3 stage-плана/seed:1000 успехов/0 отказов/0 fallback,1054.309мс процедуры. Actual export:1000 успехов/0 отказов/0 fallback,374.951мс, SHA совпал. Затем добавлено полное сравнение всех полей каталога и отдельный тест неверного seeded-дубликата при допустимой3/2 — итог13 новых случаев B. Окончательные полные XML: **EditMode136/136/136 и PlayMode106/106/106**, failed/skipped/inconclusive0, оба exit0.13 новых B входят в136 (108 исходных +15 A +13 B), focused не суммируется с полными наборами.

Unity6000.3.28f1 Mac отдельно разрешён для S4-03B; текущий открытый Editor не трогался. Команды в worktree: разрешённый Unity -batchmode -projectPath <worktree>/Game -runTests -testPlatform EditMode|PlayMode -testResults <worktree>/Game/TestResults/s403b-validation/<mode>.xml -logFile .../<mode>.log, без -quit. Полные числа берутся по уникальным test-case XML, ноль случаев не считается успехом. Все времена — процедуры Editor/test Runner с указанными ограничениями, не производительность на телефоне.

## Открытые условия и передача

Ручное прохождение, реальные mini-boss collider/навигация, Players/Android и их нагрузка NOT RUN. Ни успешная1000 seed-проверка, ни13 новых случаев не означают физической приёмки элиты/полной Stage4. S4-05A0/боевые дополнения/интеграция У2 остаются отдельными. Commit/push/merge в этом поручении не выполнялись; код/handoff готовятся локально, новый hash не выдумывается. После фиксации/передачи передать У2 точный commit hash с этой документацией.

## Окончательная проверка и ограничения каталога

Финальный C# sweep в полном EditMode: эталон1000 уникальных seed ×2 прохода ×3 будущих планировочных StageId,1000 успехов/0 отказов/0 fallback на каждую исходную стадию/проход;1103.522мс всей процедуры. Это6000 валидных stage-планов, не6000 уникальных seed. Actual export1000 seed и1000 соответствующих вызовов на Unity-каталоге:1000 успехов/0 отказов/0 fallback для экспорта, все Snapshot совпали;406.816мс. Все поля32 точек/комнат/бюджетов сверены напрямую, SHA JSON совпал. Замечаний к Id/координатам/декларированным радиусам текущих данных не обнаружено. Нет исходных физических точек/сертификата для новых дочерних слизней, только12 слотов; без этих будущих данных физическую аттестацию не закрывать.

Полные XML/логи: Game/TestResults/s403b-validation/EditMode.xml, PlayMode.xml, логи с теми же именами. Проверены testcasecount/total/узлы/уникальные fullname/Passed; реальные exit в exits.json. Подробный локальный verified-summary.json содержит SHA исходников исполнения/время/sweep outputs. Старые tests, golden A и однокомнатные RoomController assertions сохранены; исходные scene/prefab/StreetMap/Actors/Contracts/Core/Input/Generation и статус У2 не менялись. Новая meta уникальна, diff --check прошёл, индекс пуст.

EditMode: duration 2.3022443с; SHA256 XML d553a33a1c6d846b7a9991c70a57e3b9b0587df60dfb38a773e388690bb66f61.

PlayMode: duration 158.0103428с; SHA256 XML b37fc25a2d1107b350a1b6a039fad9926662271cc2dc381d82ff98095ba24c35.

SHA256 Game/Assets/_Game/Scripts/Encounters/EncounterPlanner.cs: dd40b39f99965496657f76f88e8b616413206ad71ceb11dd6906115e551b7702.

SHA256 Game/Assets/_Game/Scripts/Encounters/EncounterPlanValidator.cs: a26f9fe625cda298b54951a8a21ece22ebda133e7c7e9e9f9d9d25994493f0cd.

SHA256 Game/Assets/_Game/Tests/EditMode/EncounterValidationTests.cs: d542856377a3a2df22fa4593e6e7d9880ec57306532fbbbcf39e067eb81b1719.

SHA256 Game/Assets/_Game/Tests/EditMode/EncounterPlannerTests.cs: 49907e04d5fb7471ccdbb61db0bdd970ddd5c75bd5f971dd732d3dd64f08e3bf.
