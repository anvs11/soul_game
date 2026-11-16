# S2-08 — передача локальных lifecycle-исправлений и инструментов У2

16 ноября 2026, У1. Репозиторий `anvs11/soul_game`, ветка **`feat/s2-08-lifecycle-validation`**. Основа — актуальный полученный `origin/main`, **`03d8873287062c28081ce0a9227942cef2262b23`**. Дерево этой основы совпадает с прежней опубликованной веткой `2f0bb12`; новая ветка создана прямо от main, без повторной реализации S2-03–07, stash/reset и потери локальной работы.

Передача предназначена для ревью У2. **Unity-проверки НЕ ВЫПОЛНЕНЫ; Stage 2 не объявляется завершённой.** Новый запрос разрешает commit/push этой отдельной ветки, слияние в main остаётся до ревью У2. Авторство и исторические Windows-результаты У2 не меняются. Один агент не является независимой приёмкой двух людей.

## Состав передачи

Ровно следующие15 путей входят в коммит этой работы; игровые пути приведены полностью относительно корня репозитория.

| Путь | Назначение |
| --- | --- |
| `Game/Assets/_Game/Scripts/AI/PoliceBoss.cs` | Очистка пула без включения извне отключённого компонента; завершение фактической смерти при OnEnable; отсутствие сигнала подготовки мёртвому. |
| `Game/Assets/_Game/Scripts/AI/ShootingEnemy.cs` | Очистка активных пуль через ClearActive без переключения enabled. |
| `Game/Assets/_Game/Scripts/Editor/BuildCommands.cs` | Отдельные Development builders Stage2 Mac/Windows/Android и JSON из BuildReport; прежний Stage1 builder сохранён. |
| `Game/Assets/_Game/Scripts/Sandbox/Stage2Measurements.cs` | Новый opt-in измеритель Development Player. |
| `Game/Assets/_Game/Scripts/Sandbox/Stage2Measurements.cs.meta` | GUID `ad0b75d86b52479c92d9bae9e9f0871e`, проверен на уникальность среди meta Assets/_Game. |
| `Game/Assets/_Game/Tests/EditMode/Stage2BuildTests.cs` | Два новых теста контракта списка сцен. |
| `Game/Assets/_Game/Tests/EditMode/Stage2BuildTests.cs.meta` | GUID `81114ffc72984c248c131c7f19b5d780`, проверен на уникальность среди meta Assets/_Game. |
| `Game/Assets/_Game/Tests/PlayMode/HazardBossTests.cs` | Две новые lifecycle-регрессии босса. |
| `Game/Assets/_Game/Tests/PlayMode/ShootingEnemyTests.cs` | Новая регрессия очистки стрелка с отсутствующей целью. |
| `Setup/Stage2/run_validation.py` | Последовательный CLI-helper для Runner и необязательной сборки. |
| `Game/Docs/Plans/S2_08_ROLE1_REVIEW_PLAN.md` | План и история минимальных lifecycle-исправлений, уточнение передачи. |
| `Game/Docs/Plans/S2_08_VALIDATION_BUILD_PLAN.md` | План builder/измерителя, фактический Progress и открытые условия. |
| `Game/Docs/S2_08_ROLE1_REVIEW.md` | Исходное статическое ревью64a5f9f с актуальным дополнением о передаче. |
| `Game/Docs/S2_08_LOCAL_CHANGES_HANDOFF.md` | Этот handoff, точный состав/API/проверки/приёмка У2. |
| `Coordination/ROLE_1_STATUS.md` | Только новая запись этой передачи; предшествующие локальные черновые вставки статуса исключены из индекса. |

Существующие meta пяти изменённых C# файлов уже отслеживаются в main, не заменяются и сохраняют GUID. Новые Unity-assets получают свои две meta выше. `Setup/` и `Game/Docs/` находятся вне Assets и не требуют Unity meta. Сцены/prefab/данные, Contracts/v0.1, Core/UI, Packages и ProjectSettings этой передачей не меняются. Личные `.agent/START*`, `.agent/STAGE_2_FINISH_ROLE_1.md`, Mac setup, старые XML, TestResults/Builds/Library/Temp и прочие черновики не включаются. Статус/история У2 сохраняются.

## Lifecycle: поведение и API

Публичные `Configure`/`Initialize(TargetRegistry, Transform)`, `IEnemyTarget`, `Died` и `ProjectilePool.ClearActive()` не меняют сигнатуры. Нового расчёта урона, реестра или ввода нет. Политика «disable — не смерть» сохраняется.

`PoliceBoss.CleanupOwned` и `ShootingEnemy.ClearShots` теперь вызывают существующий `pool.ClearActive()`. До правки они делали enabled=false→true и снимали внешнюю блокировку пула при Initialize/disable/недоступной цели. Теперь очищаются активные пули, прежний enabled сохраняется; TrySpawn по-прежнему сам отклоняет выключенный пул. Политика очистки при смерти/смене или потере цели не ослабляется.

Если активный DamageReceiver получил смертельный урон при выключенном только AI босса, следующий OnEnable вызывает однократный Die вместо простого присваивания Dead. Отключаются физика/collider/визуал, очищаются принадлежащие пули/миньоны, снимается регистрация, Died публикуется один раз. Повторное включение не оживляет. BeginWindup при мёртвом HP гасит предупреждение и не создаёт новый сигнал. Пороговые стадии600/400/200, урон/защита пуль, призывы2+4, баланс и направление оружия не менялись.

Новые PlayMode-сценарии: `HazardBossTests.BossCleanupPreservesExternalPoolDisable`, `HazardBossTests.LethalDamageWhileBossAiDisabledIsFinalizedOnceOnEnable`, `ShootingEnemyTests.ShooterCleanupPreservesExternalPoolDisableWithMissingTarget`. Используются имеющиеся prefab-фикстуры. Assertions проверяют состояние пула, ActiveCount, Died/registry, физику/видимость/отсутствие выстрелов. Это подготовленные тесты, а не успешно исполненные регрессии.

## Builder и список сцен

Новые публичные entry points в `Game.Editor.BuildCommands`: `Stage2ScenePaths(): string[]`, `BuildStage2Mac()`, `BuildStage2Windows()`, `BuildStage2Android()`. ScenePaths каждый раз возвращает новый массив. Точный порядок: `Assets/_Game/Scenes/Bootstrap.unity`, `Home.unity`, `Stage2CombatSandbox.unity`, `Stage2BossSandbox.unity` в том же каталоге. CombatSandbox/preview/BaselineSmoke исключены.

Меню: `Game → Build → Stage 2 Mac`, `Stage 2 Windows`, `Stage 2 Android`. Требуются Unity6000.3.28f1, сохранённые сцены/неактивный Play, установленный модуль и заранее выбранный нужный target. Builder модуль не устанавливает и target сам не переключает. Создаётся уникальный `Game/Builds/Stage2/<Mac|Windows|Android>/<UTC-id>-<suffix>/`, Development Player `ZombieExpedition.app`/`.exe`/`.apk` и `build-report.json`.

Manifest содержит buildId/platform/result/editor/executable/scenes/messages/errors/warnings/durationSeconds/sizeBytes. Из BuildReport копируются результат и сообщения warning/error/exception/assert; неуспех или errors>0 приводит к исключению. При исключении до получения результата manifest сохраняет PreflightFailed. Полный build.log нужно сохранять отдельно; имя этого статуса не доказывает, что исключение произошло именно до вызова BuildPlayer. Наличие manifest без результата Succeeded/фактического Player не является успешной сборкой.

Новые EditMode-тесты: `Stage2BuildTests.Stage2ContainsExactlyFourRuntimeScenesInOrder`, `CallerCannotChangeLaterBuildSceneLists`. Test asmdef уже ссылается на Game.Editor, правка asmdef не требуется.

## Измеритель и helper

`Game.Sandbox.Stage2Measurements` компилируется под `DEVELOPMENT_BUILD || UNITY_EDITOR`; автозапуск в Editor запрещён, в Player требуется `--stage2-measure`. В обычном Player без флага объект измерителя не создаётся. Передавать `--stage2-output <absolute-project-results-directory>`, `--stage2-build-id <real-buildId>`, `--stage2-scenario <actual-actions-label>` и при необходимости `--stage2-measure-seconds <5..300>`; по умолчанию60с, максимум18000 отсчётов. Уникальная папка `measure-<id>/measurements.json` создаётся под указанным output. Public gameplay API не добавляется.

JSON содержит устройство/OS/Unity/CPU/GPU/разрешение/режим, buildId/scenario, sampleCount, сцены, по каждому кадру unscaled frame interval в мс, GC bytes и текущие счётчики найденных пулов; также среднее/p50/p95 интервала. `gcCounterAvailable=false` и `gcAllocatedBytes=-1` означают unavailable. Поле `createdProjectiles` пока означает сумму TotalCount текущих пулов, не общее число созданий за попытку. Лимиты точности/безопасного запуска ниже обязательны для ревью.

Python helper запускается из корня: `python3 Setup/Stage2/run_validation.py --editor <authorized-Unity-executable> [--build Mac|Windows|Android]`. Он последовательно запускает EditMode и PlayMode без `-quit`, останавливается при ненулевом exit, отсутствии/пустом/неуспешном XML или любом non-Passed случае; сохраняет XML/логи и validation-summary.json в новую `Game/TestResults/stage2-validation-<id>/`. При --build затем запускает соответствующий entry point с `-quit`/`-buildTarget`, требует новый единственный manifest, exit0, Succeeded/errors0. Это CLI заготовка проверки, не автоматический проход боевого маршрута.

## Фактические результаты этой передачи

| Проверка | Результат |
| --- | --- |
| Unity импорт/компиляция, EditMode/PlayMode | **NOT RUN**. Discovered/executed/passed/failed/skipped/inconclusive новой версии не измерены; не записывать «0 failed» вместо отсутствующего прогона. |
| Состав тестов по исходникам | Ориентир42 EditMode (40 Test+2 TestCase) и84 PlayMode (2 Test+82 UnityTest), это не Runner counts. |
| Python `--help` | Реально выполнен, exit0, usage/options показаны, Unity не запускается. |
| Python AST и asmdef JSON | Синтаксис helper разобран без исполнения; четыре asmdef разбираются как JSON. Это не компиляция C#. |
| Meta/GUID | Две новые meta существуют, GUID имеют правильную форму и уникальны среди Assets/_Game/meta. Unity-импорт этим не подтверждён. |
| Build contract и Stage1 | Статически извлечён точный массив четырёх существующих runtime-сцен с meta; тело прежнего Stage1 builder совпадает с origin/main. Новые build-тесты в Unity не исполнялись. |
| Отрицательная проверка отсутствующего Editor | **NOT RUN**: предварительная защита обнаружила UnityLockfile и остановилась до вызова helper. Не путать exit1 защиты с успешным отрицательным тестом helper. |
| Старые XML | Повторно разобраны только прежние16 случаев (15 passed/1 failed, затем16 passed) и Mac S2-01 29/32. Счётчики/уникальные узлы согласованы; к текущему diff они не относятся. Windows40/81 — история У2, этих XML здесь нет. |
| Development Players/BuildReport/устройства/ручные10/измерения | **NOT RUN**, новых соответствующих артефактов нет. |
| Рабочий/staged diff и состав индекса | Проверяются перед коммитом: только15 явных путей, без удаления файлов и посторонних настроек; `git diff --check` и `git diff --cached --check`. |

Локальный отчёт статических проверок: `Game/TestResults/stage2-transfer-20261116-180433-520fc074/static-checks.json`. Он исключён из Git, основные результаты перенесены в таблицу, чтобы handoff читался после чистого checkout. На момент проверки `Game/Temp/UnityLockfile` присутствует; он не удалялся, процессы не завершались, второй Editor не запускался. Разрешённый исполняемый путь Unity для этой проверки не предоставлен; внешние установки/редактор не использовались. Это не утверждение об отсутствии Unity на компьютере.

## Известные ограничения перед приёмочными замерами

Передаётся существующая подготовка инструментов; следующие пункты **не исправлены** и не должны считаться закрытыми S2-08.

1. Output проверяется лишь на абсолютность, принадлежность разрешённому каталогу код не проверяет; buildId/scenario допускают unspecified. До доработки использовать только явно заданную существующую папку внутри `Game/TestResults/` и настоящие метаданные; не проверять внешние пути запуском. Обязательную валидацию output/metadata принять отдельным пунктом ревью/доработки.
2. Для Android не реализован/не проверен воспроизводимый способ включить прибор, задать доступный на телефоне output и выгрузить JSON обратно в проект. Путь desktop-клона на телефоне не предполагать. Наличие BuildStage2Android не закрывает ни телефон, ни Android-замеры.
3. Пулы повторно ищутся раз в секунду; новый пул может быть пропущен до обновления. createdProjectiles — снимок размера текущих пулов; после выгрузки старый пул из суммы исчезает. Не выдавать показатель за накопительный spawn count или гарантированный пик. Нужно уточнение семантики/имени либо настоящий счётчик с проверкой переходов.
4. Измеряется интервал кадров с frame pacing, не изолированное CPU-время. Сам прибор, обновление списка пулов и экспорт JSON дают нагрузку; GC включает эту нагрузку. Errors в JSON относится лишь к периоду после подписки и до однократной записи, не всему запуску. Полный Player-лог обязателен.
5. При существующем lock helper вызывает lsof без проверки платформы/доступности команды; Windows без lsof не поддержан этим guard. До доработки использовать ручной Test Runner/прямые Unity-команды в закрытом проекте, не удалять lock ради запуска.
6. Helper пока не согласовывает итоговые XML-счётчики/уникальные имена/полный ожидаемый состав, не сверяет все поля manifest и наличие выходного executable. После helper вручную проверить эти условия и полный лог; `VERIFIED` означает только перечисленные проверки самого helper. Отсутствие настоящего Unity/Player-прогона отдельно остаётся ограничением передачи.

## Действия У2 до решения о merge

Получить ветку, проверить diff от03d8873 и точные15 путей; сохранить собственную локальную работу перед переключением. Проверить новые метаданные и отсутствие сценовых/контрактных изменений. Ревьюировать lifecycle отдельно от незавершённости tools, не ослабляя проверки пула/однократной смерти.

В разрешённом Unity6000.3.28f1 открыть Game/, дождаться компиляции. В Test Runner сначала выполнить три новых PlayMode-метода, затем оба полных набора. Ориентир42/84 пересчитать при изменении состава; экспортировать XML/логи в новый каталог, согласовать testcasecount/total/узлы/уникальные fullname, реальные counts и нулевые failed/skipped/inconclusive исполненного полного набора. При провале сохранить неуспешные материалы, не подставлять старые40/81 или29/32.

Для прямого batch, только после закрытия Editor и с разрешённым executable, шаблон команды из корня: `<Unity> -batchmode -projectPath <repo>/Game -runTests -testPlatform EditMode -testResults <new-results>/EditMode.xml -logFile <new-results>/EditMode.log`; затем отдельно PlayMode. Для тестов не добавлять -quit. Новую папку результатов создать внутри проекта. Не запускать helper с lock, пока его guard не проверен на данной платформе.

После успешных тестов вручную пройти Bootstrap→Home→Stage2CombatSandbox→Stage2BossSandbox→один результат→Home с паузой/смертью/отказом/повтором, проверить RunId/истощённые ресурсы/очистку пулов/миньонов и отдельно прежний Stage1. Исполнителя/платформу записать явно. Сам этот handoff не подтверждает эти действия.

Затем отдельно принять/доработать helpers по ограничениям выше, собрать доступный Development Player с новым buildId/BuildReport, проверить четыре runtime-сцены и реальный запуск. Для batch Mac builder использовать `<Unity> -batchmode -quit -projectPath <repo>/Game -buildTarget StandaloneOSX -executeMethod Game.Editor.BuildCommands.BuildStage2Mac -logFile <new-results>/build-Mac.log`; Windows target StandaloneWindows64, Android target Android и соответствующий entry point. Наличие модуля проверять на своей среде, ничего автоматически не устанавливается.

Desktop-измерение после ревью: запустить реальный executable из новой сборки с `--stage2-measure --stage2-output <absolute-repo>/Game/TestResults/<new-measure-run> --stage2-build-id <manifest-buildId> --stage2-scenario <actual-actions> --stage2-measure-seconds 60`. Сохранить полный Player-лог и JSON, проверить sampleCount>0, метаданные, доступность GC, фактические сцены и действие ограничений прибора. Измерение не заменяет10 ручных Windows Player-проходов или Android-устройство.

Вопросы новых Players/устройств/производительности из S2_08_ACCEPTANCE остаются открытыми до фактических доказательств. Никакого автоматического перехода к генератору, сохранениям или монетизации. Решение о слиянии этой ветки в main принимает У2 после ревью; в рамках передачи merge не выполняется.
