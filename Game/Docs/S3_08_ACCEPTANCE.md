# S3-08 — частичная приёмка Stage 3

17 ноября2026, роль2, локальная ветка feat/stage3-runtime-validation. Основа d9dc3b545daa89a6c40fd93752a4e2f5bf9794a1 + сохранённые локальные изменения S3-06B. Worktree внутри исходного проекта: Game/TestResults/s306b-worktree; все пути ниже относительно него, если не указано иное. Main/чужие незакоммиченные документы не меняются. Ревью У1 взято из основной копии Game/Docs/S3_07_ROLE1_REVIEW.md по be0ff19; исторический P2 исправлен в S3-06B и ждёт ревью У1. Одно выполнение агентом не является независимой приёмкой двух людей.

## Статус обязательных условий

| Условие | Факт / статус |
| --- | --- |
| Контракт/детерминизм/версии/конечный подбор | Реализовано в Generation У1; прежний эталон и реальный каталог1000 seed проходят C#/Python по S3-05/06; текущий C# Runner ниже. Seed-прототип, не целевая постоянная карта. |
| Пять модулей Combat×4/Boss последний | Новые generated10-попыток/34 сегмента в S3-06B, автоматические победы0/123 и ресурсы; текущие повторные проверки ниже. |
| Геометрия и реальные клетки | StreetModuleTests сверяют4320 клеток/кандидаты босса, ресурсы прежние; ручные застревания AI/сложность NOT RUN. |
| P2 портала | TryExit повторяет размещение по Interact после guards; recovery-регрессия вместо диагностического Passed. Ревью У1 нового исправления OPEN. |
| UI/контролы | HUD/seed/G1/C1/Id, один EventSystem и фактические SafeArea anchors проверены batch. Вырезы/ориентация/мультитач телефона NOT RUN. |
| BuildStage3Windows/Android/Mac | Добавлены отдельные entry points с новым buildId/BuildReport; фактические артефакты ниже. Наличие метода не означает проверку платформы. |
| Состав сборки и Bootstrap | Builder требует точные6 сцен; реальный список/seed проверяет opt-in Development Player smoke. Факт запуска ниже, пока не заменять план результатом. |
| Прежние Stage1/Stage2/Foundation | Прежние assertions не менялись; полные текущие XML ниже. Ручные старые профили NOT RUN. |
| Ручной цикл с8+16/ножом | NOT RUN: native Game View управление отсутствует. Ни teleport, ни damage1000 не являются ручным боем. |
| Три ручных seed, включая fallback | OPEN. Контролируемый fallback проверен только автоматикой64 bad-варианта; реальный каталог не требует fallback. |
| Дверь во время отложенной волны | OPEN / не реализовано: нынешние площадки имеют фиксированный стартовый состав, отдельного wave scheduler/test нет. Начальный alive-count не доказывает закрытие между волнами. |
| Карта посещённых узлов/доступных направлений | OPEN / не реализовано; SegmentIndex не является картой. Нет явного решения пользователя о переносе. |
| Безопасная остановка после одного из первых3 сегментов | OPEN / не реализовано как отдельный Rest flow; наличие street_rest asset не доказывает игровой остановки. |
| Windows Player | NOT RUN на этой Mac-среде; отсутствие Windows прогона не заменяется Editor-тестом. |
| Android Player/реальный телефон | NOT RUN; устройство/его модель не предоставлены, замеры/ввод/lifecycle/нагрев OPEN. |
| Бюджет производительности/10 Player-проходов | OPEN. Автоматические Editor10 не равны10 ручным Windows Player-проходам; текущая desktop-инструментация только сценарий ниже. |

Stage3 не Done: межплатформенная и ручная часть не завершена. Новое направление постоянной многокомнатной карты, Encounter, экономика/сохранения не реализуются этой стадией автоматически.

## API, состав и новые meta

Game.Editor.BuildCommands сохранён и стал partial. Stage3ScenePaths() возвращает новый массив6 путей: Assets/_Game/Scenes/Bootstrap.unity, Home.unity, CombatSandbox.unity, Stage2CombatSandbox.unity, Stage2BossSandbox.unity, Stage3StreetSandbox.unity в этом каталоге. Legacy-арены намеренно включены: Home сохраняет основной Stage1 action и кнопкуStage2. Preview исключены. Старые Stage1/Stage2 builders не изменены.

Stage3BuildCommands.cs добавляет BuildStage3Windows(), BuildStage3Android(), BuildStage3Mac(): Development + DetailedBuildReport, уникальная Builds/Stage3/<platform>/s3-<UTC>-<suffix>/, manifest с requested scenes/packedScenes/result/errors/warnings/buildGuid/size/duration/messages, проверка Player output. PreflightFailed/BuildStartedFailed различают этап исключения. Успех требует BuildReport.Succeeded/errors0; packedAssets может быть неполным источником списка сцен, окончательная проверка — SceneUtility реального Player.

Stage3PlayerValidation.cs запускается исключительно с --stage3-smoke в Development Player, не в Editor/обычной игре. Пишет модель/OS/CPU/GPU/buildGUID,6 фактических сцен,10 сегментов seed0/123 и S308_SMOKE_OK; двойные Interact, победа и Home. Через30 кадров на сегмент программно убивает врагов, поэтому не проверяет обычную сложность. Fail пишет S308_SMOKE_FAILED/exit2, timeout20с. Это диагностический инструмент, не новая механика.

Новые C# assets Stage3BuildCommands, Stage3PlayerValidation и Stage3BuildTests сопровождаются собственными .meta. Предыдущие GeneratedStreetLifecycleTests/.meta и ArenaController — зависимость S3-06B. Documents: S3_RUNTIME_PLAN, S3_06_RUNTIME_HANDOFF, S3_06B_LIFECYCLE_HANDOFF, этот acceptance и только добавленные записи ROLE_2_STATUS. История У1/чужие сцены/Generation/баланс не меняются.

## Ограничения S2-08: конкретная процедура desktop-замера

Используется Stage2Measurements без изменения: --stage2-measure, --stage2-output <абсолютный путь этого worktree>/Game/TestResults/s308-validation/measure-output, --stage2-build-id <точно manifest.buildId>, --stage2-scenario automated-stage3-seeds0-123-not-manual, --stage2-measure-seconds 5. Перед запуском Python resolve проверяет output внутри TestResults этого проекта и отсутствие выхода через symlink за этот корень; папка создаётся явно. BuildId/scenario не unspecified, значения затем сверяются с JSON. Ограничение1 закрывается процедурой этого desktop-запуска, не объявляется исправлением общей валидации инструмента.

Ограничение2 Android остаётся OPEN: desktop-путь не используется на телефоне, воспроизводимый Android transport/output/extraction не реализован. Ограничение3: createdProjectiles трактуется только как сумма TotalCount текущих обнаруженных пулов, не накопительный spawn и не гарантированный пик; поиск раз/сек может пропускать короткоживущие пулы. Такие пики/count за маршрут не принимаются этими данными. Для точного пика нужен Profiler с Timeline и отдельный прогон, OPEN.

Ограничение4: frame interval включает pacing/загрузку/прибор, GC включает прибор, это не CPU frame time. Ошибки JSON охватывают лишь окно подписки: полный Player.log сохраняется с начала процесса и отдельно анализируется на Error/Exception/Assert. Ограничения5/6 helper обходятся прямыми Unity-командами на отдельной закрытой копии; lock не удаляется, lsof не используется, числа XML/уникальные fullname/согласование total и фактический manifest+executable+6 сцен сверяются отдельно. Эти процедуры не закрывают непроведённые ручные/телефонные проверки.

## Команды и фактические результаты

Unity6000.3.28f1 разрешён пользователем специально для S3-08, как и созданный Mac Player. Прямые команды из worktree: Unity -batchmode -projectPath <worktree>/Game -runTests -testPlatform EditMode|PlayMode -testResults <worktree>/Game/TestResults/s308-validation/<mode>.xml -logFile .../<mode>.log. Без -quit для Runner. Сборка: Unity -batchmode -quit -projectPath <worktree>/Game -buildTarget StandaloneOSX -executeMethod Game.Editor.BuildCommands.BuildStage3Mac -logFile .../build-Mac.log. Windows/Android аналогично с StandaloneWindows64/Android и своими entry points; необходимые модули не устанавливаются автоматически.

Текущий полный Runner: EditMode80/80/80, PlayMode94/94/94 (discovered/executed/passed), failed/skipped/inconclusive0, процессы exit0. Runner duration0.3408319с и119.402883с. Прежний S3-06B78/94 не переименовывается в новые результаты. Полные XML/Builds/Library/Player.log/замеры исключаются из коммита, проверенные сводки записываются ниже.

## Ограниченный следующий шаг

У1 проверяет recovery и сборочный diff. Человек проходит минимум0/123/fallback обычным вводом с протоколом HP/armor/ammo/stamina по1.1–1.5 и конкретными координатами застреваний AI, затем Stage1/Stage2. После этого отдельно закрыть Windows/Android на выбранном устройстве и принять решение об оставшихся карте/Rest/отложенной волне. Баланс предлагать только по наблюдениям; автоматического перехода к генератору/сохранениям/монетизации нет.

## Фактическая приёмка 17 ноября2026

XML Game/TestResults/s308-validation/EditMode.xml и PlayMode.xml; логи рядом, exits.json и runner-summary.json. Уникальные fullname/узлы/total/testcasecount согласованы. SHA256 EditMode e156259e7a0ebdd8bded7f4abd652b42c53769c1485b5aa814259a5b64c0197c; PlayMode af361ec77b8f820d3bd93dba1d3fd84fd5a7245e381da8a2606e378c125a569b. Реальный каталог1000 C# успехов/0 отказов/0 fallback за85.8063мс процедуры; эталон1000 успехов38.1526мс, отрицательный Boss1000 отказов74.4615мс и повтор эталона1000 успехов47.6399мс. Это время тестовой процедуры, не нагрузка Player. Десять generated-попыток и recovery P2 входят в полный94; старые Foundation/Stage1/Stage2 assertions сохранены.

Mac Development: **Succeeded**, errors0/warnings1, exit0. buildId **s3-20261116-210525-1d5ceb6e**, buildGUID251d6ca7195f4434bf4e5177e54f2755, size318059946 bytes, BuildReport duration23.808871с. Идентификатор использует UTC-время сборки. Путь Game/Builds/Stage3/Mac/<buildId>/build-report.json и ZombieExpedition.app; полный лог Game/TestResults/s308-validation/build-Mac.log. Единственное предупреждение: empty Unity Cloud access token, native symbols не загружены. BuildReport.packedScenes оказался пустым; по нему фактический состав не заявляется. **Реальный Player** через SceneUtility подтвердил ровно6 сцен в указанном порядке и Bootstrap index0; Application.buildGUID совпал с manifest. Resources/модули реально загружены, оба маршрута0(b,a,b,b,boss) и123(a,b,c,a,boss) завершились Victory/Home. Player.log и Player-windowed.log содержат6 S308_SCENE,10 S308_SEGMENT G1/C1 и S308_SMOKE_OK. Оба process exit0, сведения player-exit.json/player-windowed-exit.json. Эти два автоматических прохода не заменяют ручное управление.

Windows builder: **PreflightFailed**, process exit1, модуль StandaloneWindows64 недоступен; buildId s3-20261116-210647-b4bbd5bd. Android builder: **PreflightFailed**, process exit1, модуль Android недоступен; buildId s3-20261116-210651-5792b350. Manifest в соответствующих Game/Builds/Stage3/<platform>/<id>/, полные build-Windows-preflight.log/build-Android-preflight.log и platform-builds.json в TestResults/s308-validation. BuildPipeline.BuildPlayer для них не выполнялся: BuildReport/Player/APK нет; поле errors0 в preflight manifest не означает успешную сборку. Модули не устанавливались. Windows/Android Player и телефон **NOT RUN**.

Оконный Mac Player: модель **Mac16,8**, CPU/GPU **Apple M4 Pro**, ОС **Mac OS X26.2.0**, Unity6000.3.28f1/OSXPlayer, разрешение1280×720. Scenario **automated-windowed-seeds0-123-not-manual**, тот же buildId. Измерение Game/TestResults/s308-validation/measure-windowed/measure-20261117-153620-7e9851af/measurements.json:512 samples, последнее elapsed5.004356384с, средний интервал13.89755344мс, p50 8.33512688мс, p95 14.06416512мс, GC-counter available, максимальный наблюдённый GC509209 bytes/frame включая прибор/переходы. Сцены измерения Bootstrap/Home/Stage3StreetSandbox, errors0 только внутри окна прибора. Это короткие переходы/программная зачистка, не заданная нагрузка из врагов/пуль, не CPU frame time и не выполнение бюджета телефона.

Первый batch Player:640×480,18000 samples, достиг лимита раньше5с. Фактический elapsed/метаданные сохранены в verified-summary.json и measure-output/measure-20261116-210628-f9c9b837/measurements.json; не использовать его средние как приёмочный performance. Повторное оконное измерение обходит ограничение для этого запуска, общий инструмент/лимит не исправлены.

**Открытое замечание runtime/shutdown:** оба полных Player-лога содержат `GarbageCollector disposing of ComputeBuffer` и native `MemoryLeaks` report при завершении (около2.39MB allocations). Нет атрибуции к gameplay/движку/пакету, memory-profiler не выполнен; не считать доказанным gameplay leak или чистой памятью. errors0 измерителя не покрывает shutdown. Отдельный следующий диагностический шаг — сравнить с пустой Bootstrap/Home Development сборкой того же Unity/renderer, снять allocation stacks/Memory Profiler до/после unload и shutdown, затем исправлять только установленный источник. Без такого сравнения утверждать причину нельзя.

Проверены diff, уникальность новых GUID, неизменность прежних builders/тестов/сцен, состав индекса явными путями. Локальный коммит включает зависимость S3-06B и этот S3-08, без XML/Builds/Library/секретов/корневых черновиков. После фиксации hash доступен из git log этой ветки; self-hash в документ не выдумывается. Push/merge не выполняются.
