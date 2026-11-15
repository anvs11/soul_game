# S2-02: новая арена с укрытиями и отдельный диагностический профиль


ExecPlan по .agent/PLANS.md, 15 ноября 2026, корень soul_game, Unity-проект Game/. Ветка feat/stage2-arenas основана на d60dbbb (S2-01 код 9639117). Пользователь поручил получить передачу и реализовать S2-02; Unity закрыта, разрешён запуск установленного редактора.

## Purpose / Big Picture


Из Bootstrap игрок попадает в Home, выбирает прежнюю Stage 1 или отдельную кнопку «Stage 2: проверка обнаружения». В новой фиксированной арене один зомби видит близкого героя, второй скрыт укрытием, третий далеко и ждёт. Стены ограничивают движение и пули. После зачистки выход возвращает диагностический результат в Home. Это не полный маршрут Stage 2: босса и промежуточного перехода пока нет.

## Progress


- [x] (2026-11-15) Проверены локальные файлы, получен origin/feat/stage2-enemies, создана feat/stage2-arenas от d60dbbb. S2-01 в origin/main пока отсутствует; main не менялась.
- [x] Прочитаны S2_01_HANDOFF и API/код/тесты, Unity закрыта по ответу пользователя.
- [x] Повторены тесты передачи S2-01 на Windows: EditMode 29/29, PlayMode 32/32, failed/skipped/inconclusive=0, XML сохранены.
- [x] Реализованы Stage2 diagnostic RunRequest и вторичная кнопка Home без нового координатора; старый профиль сохранён.
- [x] Unity создала отдельные prefab variant SensingZombie и Stage2CombatSandbox с World-укрытиями.
- [x] Реальная сцена проверена из Bootstrap: EditMode 30/30, PlayMode 36/36, failed/skipped/inconclusive=0, оба exit=0. Foundation-тесты не изменялись.
- [x] Подготовлены S2_02_HANDOFF.md и текущий статус с ограничениями/следующим шагом У1, явный список для локальной фиксации.
- [ ] Ручная визуальная приёмка пользователем нового профиля Stage 2; отдельные Android/сборочные сценарии не проверялись.

## Surprises & Discoveries


В main есть merge Stage 1 (893d68c), но нет двух новых коммитов S2-01. Передача У1 содержит дополнение о реальных Mac 29/29 и 32/32, тогда как начало того же документа исторически говорит «не выполнено»; учитываем дополнение, но локальные XML У1 не приезжают через Git. World-слой в текущем TagManager отсутствует, его надо добавить без смены существующих слоёв и collision matrix.

Первая компиляция Editor-команды выявила недостающий using Game.Combat (исправлен до создания). В первом PlayMode 35/36: тест телепортировал Rigidbody2D и сразу читал Transform при проверке выхода. Изменена только тестовая телепортация на Transform как в Foundation; полный PlayMode повторён, 36/36. Рабочий выход не изменён. Unity при добавлении World пересохранила TagManager в штатном формате v3; старые индексы слоёв и матрица сохранены.

## Decision Log


Decision: RunRequest.Stage2Diagnostic(seed) выбирает отдельное ArenaId, RunCoordinator разрешает только старую и новую известные сцены. Rationale: без новых фаз, скрытого debug shortcut, произвольных scene paths и второго координатора. Date/Author: 2026-11-15, роль 2.

Decision: вторичная pointer-кнопка Home показывается только при наличии новой сцены в доступном списке сборки и обычном Home. В результате она скрыта. Rationale: BuildStage1* по-прежнему содержит ровно три старые сцены и не предлагает отсутствующий профиль. Date/Author: 2026-11-15, роль 2.

Decision: новая сцена — отдельная копия принятой CombatSandbox через Unity, исходник не сохраняется/не изменяется; в копии заменяются только экземпляры врагов на отдельный variant. Rationale: сохранить проверенные UI/физику/реестр и первоначальные prefab. Date/Author: 2026-11-15, роль 2.

## Outcomes & Retrospective


Код S2-02 и собственная автоматическая приёмка завершены. Новая арена доступна из Bootstrap/Home, три экземпляра одного варианта демонстрируют разные условия обнаружения. Передача содержит схему сцены, API профиля, параметры и согласованное направление IEnemyTarget для продолжения прыгуна У1. Это не завершённая Stage 2; отдельная сборка, Android runtime и ручной осмотр нового профиля не подтверждены. Main/удалённые ветки не менялись, черновики сохранены.

## Context and Orientation


Core/RunTypes.cs задаёт ScenePaths и RunRequest, RunCoordinator.StartRun сейчас разрешает только CombatSandbox. SceneFlow загружает сцену и Bind единственного FlowScreen. UI/HomeScreen показывает Home/результат; FlowScreen строит Canvas и кнопку. Sandbox/ArenaController связывает ровно 3 SandboxEnemy, героя, один TargetRegistry, HUD и выход. EnemyPerception на новом варианте получает цель только через SandboxEnemy.Initialize, Tick вызывается только врагом в FixedUpdate. Его Configure(8,11,2,World) вызывать на неактивном компоненте; отсутствие компонента сохраняет поведение Stage 1. Общий Contracts/CombatContracts.cs v0.1 не меняется.

## Plan of Work


Milestone 1: проверить передачу новыми EditMode/PlayMode на Windows. Сохранить неуспешные результаты, не выдавать чужие Mac counts за собственные. Изучить diff относительно 893d68c, не менять AI У1 без обнаруженного относящегося дефекта.

Milestone 2: добавить ScenePaths.Stage2CombatSandbox, RunRequest.Stage2Diagnostic и разрешённую загрузку request.ArenaId. FlowScreen получает необязательную вторую кнопку с обычными кликами и без Navigation; HomeScreen использует её для диагностического профиля и сохраняет старую первичную кнопку/результат. Диагностический результат явно помечен, босса/награды не обещает. В сборке без Stage2 сцены кнопка отсутствует. ArenaCameraFit получает сериализуемые halfExtents с прежними default 12/8; новый профиль использует 19/11, чтобы вся физическая арена была видна.

Milestone 3: Scripts/Editor/Stage2ArenaScene.cs создаёт ресурсы только по новым путям. Добавить World в свободный слой через SerializedObject TagManager, без перенумерации старых. Создать Prefabs/Actors/Stage2/SensingZombie.prefab как variant исходного SandboxEnemy, EnemyPerception 8/11/2, маска только World. Скопировать CombatSandbox в новую Stage2CombatSandbox; заменить в новой копии три экземпляра. Герой (-12,-5), видимый NearZombie (-7,-7), HiddenZombie (-6,-1), FarZombie (13,5). Укрытия CoverA (-9,-3), размер (2,3), CoverB (4,2), размер (3,2). Стены на x=±18/y=±10, явные collider.size=1 и scale.z=1, выход (15,-7). Все стены/укрытия World, без trigger/ActorIdentity/DamageReceiver. Сцена содержит прежний EventSystem и один registry. Добавить новую сцену в EditorBuildSettings для Editor-перехода; BuildStage1* с фиксированными тремя путями не менять.

Milestone 4: добавить отдельные unit-проверки запроса нового профиля и PlayMode Stage2ArenaTests. Стартовать только через Bootstrap/Home secondary button. Проверить структуру, raycast/боевую пулю в укрытие, выбор видимой цели, ожидание дальнего/скрытого и обнаружение после выхода на видимость. Проверить реальные collider границ движением/knockback и врагом, паузу памяти, один диагностический результат/повтор и старый профиль без EnemyPerception. Тесты Foundation не переписывать. Передать фактические XML и сценарий ручного просмотра.

## Concrete Steps


Из корня soul_game запускать разрешённый C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe скрыто, -batchmode -nographics -projectPath <root>/Game -runTests -testPlatform EditMode (затем отдельно PlayMode) -testResults <новая папка>/EditMode.xml -logFile <та же папка>/EditMode.log. Не добавлять -quit к runTests. Создание сцены: -batchmode -quit -projectPath <root>/Game -executeMethod Game.Editor.Stage2ArenaScene.Create -logFile <новая папка>/create.log. Сначала импортировать C# и дать Unity создать .meta; вручную GUID не назначать. Тесты/логи — Game/TestResults/s202-<runId>/. Переходы проверяются загрузкой Bootstrap в PlayMode-тестах; отдельную сборку этот шаг не обещает.

## Validation and Acceptance


Передача S2-01 имеет ожидаемые ненулевые 29 EditMode/32 PlayMode, но записываются только реальные XML. После наших тестов число увеличится. failed/skipped/inconclusive=0, нет исключений очистки пула. Из Bootstrap обычный Start ведёт в прежнюю CombatSandbox, новая кнопка — в Stage2CombatSandbox. Близкий видимый преследует, укрытый/дальний не двигаются до обнаружения; укрытие блокирует настоящую пулю/цель. После трёх смертей выход доступен только в зоне; результат помечен диагностическим, повтор даёт свежие экземпляры/ресурсы, timeScale=1. Diff исходной CombatSandbox, базовых prefab и контракта нулевой. Android/ручная Stage2 приёмка не подменяется тестами Windows.

## Idempotence and Recovery


Команда создания не перезаписывает уже существующие новые ресурсы: при частичном выполнении сохраняет их и сообщает, что осталось проверить. Не удалять старые сцены, .meta, Library, UnityLockfile, папки результатов и локальные черновики. Изменения исправлять точечно через Unity; если потребуется перезаписать новую сцену после создания, это только собственная сцена данной задачи, исходная Stage 1 не меняется. Git push/merge main не входят в текущий шаг; локальная ветка содержит передачу У1 без подмены main.

## Artifacts and Notes


Итоговые рабочие документы — Game/Docs/Plans/S2_02_ARENA_PLAN.md, Game/Docs/S2_02_HANDOFF.md, Coordination/ROLE_2_STATUS.md. Временная доставка C# из .agent/S202Work в Git не включается. XML находятся локально в TestResults; исходный отчёт У1 сохраняется историческим с отдельной приёмкой У2.

Полученная S2-01: Game/TestResults/s201-review-2ec54daded104aee8494468c51dc1caf/EditMode.xml и PlayMode.xml (29/32). Финальный EditMode: Game/TestResults/s202-verify-5a52ee05f9254af2b72473f0283fa229/EditMode.xml (30/30). Финальный PlayMode: Game/TestResults/s202-play-49c33928dcfd49879d4d03090b6d19c5/PlayMode.xml (36/36). XML содержат discovered=executed=passed, failed/skipped/inconclusive=0. Создание сцены: Game/TestResults/s202-create-5e6662e9609a4c98bf9edf7151b3afac/create.log, exit=0. Ранняя неуспешная компиляция и PlayMode сохранены по путям из передачи.

## Interfaces and Dependencies


RunRequest.Stage2Diagnostic(int seed=0), ScenePaths.Stage2CombatSandbox; RunCoordinator сохраняет свои фазы/исходы. FlowScreen.SecondaryButton — необязательная Button, вторичное действие переопределяет HomeScreen. ArenaCameraFit.Configure(Vector2 halfExtents) используется создателем сцены. Stage2ArenaScene.Create() создаёт variant/сцену через UnityEditor. Никаких новых пакетов, навигации, генератора, босса или ввода.

## Revision Note


2026-11-15: план S2-02 после реального fetch/review, с отдельным профилем и сохранением Stage 1.

2026-11-15, итог: создана сцена/variant, внесены фактические результаты и исправление тестовой телепортации, ручная приёмка оставлена открытой. Локальный коммит выполняется явным списком .agent/S2_02_COMMIT_PATHS.txt, push/merge не выполняются.
