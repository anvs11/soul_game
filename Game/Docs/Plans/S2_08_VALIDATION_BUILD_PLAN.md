# S2-08 — проверяемые сборки и измерения Stage 2


ExecPlan по .agent/PLANS.md, 16 ноября 2026, основа64a5f9f плюс локальные исправления ревью. Предыдущие40/81 Windows не относятся к сегодняшним правкам.

Передача16 ноября2026: отдельная ветка feat/s2-08-lifecycle-validation создана от актуального origin/main03d8873287062c28081ce0a9227942cef2262b23. Исходная реализация64a5f9f уже входит в main. Нынешний объём — передать существующие локальные исправления/инструменты на ревью У2 без слияния и без объявления платформ принятой.

## Purpose / Big Picture


Получить отдельные Development Stage2 Mac/Windows/Android со строгим списком runtime-сцен, отчётами BuildReport и воспроизводимыми тестами/измерениями. На доступной платформе выполнить реальный Player и проверить десять маршрутов; недоступные платформы не подменять Editor/другим устройством.

## Progress


- [x] (2026-11-16) Прочитаны BuildCommands, Stage1 smoke, Stage2 интеграционные проверки и ограничения доступа.
- [x] (2026-11-16) Подготовлены отдельные BuildStage2Mac/Windows/Android без изменения существующего Stage1 builder.
- [x] (2026-11-16) Подготовлены два Stage2BuildTests, opt-in Stage2Measurements и Setup/Stage2/run_validation.py; наличие кода не означает успешный сборочный прогон или замер.
- [x] (2026-11-16) Выполнены доступные статические проверки/--help; подготовлены точный состав, API, команды и ограничения в Game/Docs/S2_08_LOCAL_CHANGES_HANDOFF.md.
- [ ] Довести подготовленные helpers: ограничения output/обязательные метаданные, Android-запуск/экспорт, смысл счётчика пулов и полные логи, переносимость lock-проверки и полнота проверки XML/BuildReport. Эти пункты остаются открытыми, описаны в handoff.
- [ ] Получить путь/разрешение внешнего Unity и отсутствие активного Editor-lock.
- [ ] Выполнить новые EditMode/PlayMode, реальную Mac-сборку и запуск Player, десять циклов и измерения.
- [ ] Windows Player/Android-устройство проверить отдельно при наличии сред/модулей, иначе оставить открытыми.

## Surprises & Discoveries


В проекте есть UnityLockfile. Установленный Editor вне разрешённого корня не искался и не запускался. Пользователю задан вопрос о пути и разовом доступе. Наличие lock не доказывает живой процесс и не разрешает удалять его. Stage1 smoke специфичен для одной арены и не является готовым Stage2 Player-прогоном.

На момент передачи16 ноября lock снова присутствует. Поэтому предварительная защита отрицательной проверки отсутствующего Editor остановилась до запуска helper; сама отрицательная проверка НЕ выполнена. Реально выполнен только --help (exit0), синтаксический разбор Python, JSON четырёх asmdef, проверка новых meta/GUID, списка сцен и неизменности Stage1 builder. Статический отчёт находится в Game/TestResults/stage2-transfer-20261116-180433-520fc074/static-checks.json и не включается в Git. Старые XML16/16 и29/32 не подтверждают нынешнюю передачу.

## Decision Log


Решение16ноября: сохранить существующие Stage1 entry points и приватную реализацию, добавить самостоятельный Stage2 builder с новым buildId, точным списком сцен, JSON manifest и сообщениями BuildReport. Не менять активный target автоматически; при отсутствии модуля/неподходящем target дать ошибку до сборки. Новые stage2 build/test helpers не устанавливают SDK.

Решение16ноября: измерения только по явному флагу Development Player, без постоянной нагрузки обычной игры; frame time, GC allocated per frame, число созданных/активных снарядов и контекст сцены/железа. Не создавать фиктивные measurements. Реальные маршруты проверяются отдельным ручным прогоном/интеграционными тестами, измеритель не выдаёт себя за smoke.

Решение16ноября, передача У1: не расширять подготовку передачи до нового блока доработки helpers и не выдавать исходную заготовку за завершённый измерительный workflow. Зафиксировать существующий код с явными известными ограничениями; ревью/Unity-проверки и решение о слиянии выполняет У2. Внешний Editor и пользовательские процессы не затрагивать.

## Outcomes & Retrospective


Подготовка начата. Успешные тесты/сборки/цикл/измерения не заявляются до появления реальных артефактов.

На передачу подготовлены builder, два EditMode-теста, измеритель и последовательный Python helper. Выполнены только доступные проверки структуры/синтаксиса, успешных Unity/BuildReport/measurements нет. Подробности и порядок приёмки — S2_08_LOCAL_CHANGES_HANDOFF.md. Межплатформенная часть остаётся незавершённой.

## Context and Orientation


Game/Unity6000.3.28f1. В main до передачи Assets/_Game/Scripts/Editor/BuildCommands.cs содержал только Stage1; передаваемый diff добавляет Stage2 API. Runtime-сцены Stage2: Bootstrap, Home, Stage2CombatSandbox, Stage2BossSandbox. Preview исключены. ProjectilePool предоставляет ActiveCount/TotalCount. Ревью добавило3 PlayMode теста, builder —2 EditMode; текущий ориентир исходников42/84, не результат Runner. Общий контракт и prefab/сцены не меняются.

## Plan of Work


Milestone1: BuildCommands получает Stage2 API и функцию списка сцен с независимой копией массива для проверки. Preflight проверяет редактор, сценовые файлы, модуль и выбранную платформу; вывод в Game/Builds/Stage2/<platform>/<id>/. Сохранить build-report.json с ошибками/предупреждениями/сообщениями и длительностью, при Exception сохранить failure manifest и выбросить ошибку.

Milestone2: добавить EditMode-проверки списка/исключения preview и защиты от изменения возвращённого списка. Добавить Development-only Stage2Measurements с флагом --stage2-measure и обязательным --stage2-output в пределах проекта для результатов; прибор фиксирует фактические кадры и counters без управляемых аллокаций каждый кадр. Добавить Python helper тестов с явным editor-path и проверкой XML, не запускать его до разрешения/закрытия проекта.

Milestone3: после допуска выполнить оба набора и Mac build, разобрать XML/BuildReport. В настоящем .app пройти десять исходов с паузой и промежуточным переходом, записать ручное/автоматическое происхождение каждого подтверждения. Экспорт измерений по флагу не считается доказательством боевых циклов. Windows/Android факты отдельно.

## Concrete Steps


В Editor после закрытия Play/сохранения сцен выбрать активную платформу Mac, меню Game/Build/Stage 2 Mac. Для batch: явный разрешённый Unity -batchmode -quit -projectPath <root>/Game -buildTarget StandaloneOSX -executeMethod Game.Editor.BuildCommands.BuildStage2Mac -logFile <new-run>/build.log. Тесты без -quit, по одному процессу, XML каждого в новой Game/TestResults/stage2-<id>/. Не запускать второй Editor на том же проекте. Для иной платформы использовать соответствующие BuildStage2Windows/BuildStage2Android; helper с lock на Windows до исправления lsof-зависимости не считать переносимым.

## Validation and Acceptance


Список сборки ровно четыре сцены в заданном порядке, без CombatSandbox/Preview. BuildReport.result=Succeeded, errors=0, warning details сохранены; executable/номер возврата сами по себе не успех. Новые XML имеют реальные ненулевые test-case без failed/skipped/inconclusive. Десять реальных маршрутов проходят через обе арены с одним RunId и перенесённым ресурсом, Victory/Defeat/Abandoned, паузой, активными пулями, новым стартом; проверить старый Stage1. Measurements указывают устройство/OS/редактор/player/debug/resolution/scenes/sample count и фактические frame/GC/pool данные. Неподдерживаемая метрика помечена unavailable, не нулём как измеренным значением.

## Idempotence and Recovery


Новые UUID каталоги, существующие артефакты не заменяются. Не удалять lock/Library/meta, не завершать процессы пользователя, не устанавливать модули. При ошибке сохранить manifest/log/XML и устранить причину до повтора. Пользовательские незавершённые изменения и Git-история сохраняются.

## Artifacts and Notes


Действительные пути/числа дополняются в Game/Docs/S2_08_BUILD_VALIDATION.md и Coordination/ROLE_1_STATUS.md. Пока артефактов нет, писать NOT RUN/недоступно, а не Passed.

Фактическая передача этой ветки — Game/Docs/S2_08_LOCAL_CHANGES_HANDOFF.md. S2_08_BUILD_VALIDATION пока не создан и не является доказательством запуска. Статический отчёт локальный, путь приведён выше; экспорт XML/BuildReport/measurements отсутствует.

## Interfaces and Dependencies


BuildStage2Mac/Windows/Android и Stage2ScenePaths() в существующем Game.Editor.BuildCommands. UnityEditor.Build.Reporting.BuildReport для результата, Unity.Profiling.ProfilerRecorder для GC allocated per frame, runtime ProjectilePool counters. Стандартный Python XML для приёмки Runner, новых пакетов нет.

## Revision Note


2026-11-16: план создан до правок; реальный запуск зависит от явно разрешённого Editor и устройства, это не отменяет подготовку инструментов.

2026-11-16: актуализированы подготовленные пункты Progress, основа03d8873, ориентир42/84 и реальные статические проверки; известные ограничения helpers сохранены открытыми. Оформлена отдельная передача У2, без merge.
