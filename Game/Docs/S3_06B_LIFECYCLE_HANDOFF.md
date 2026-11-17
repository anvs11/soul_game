# S3-06B — generated-попытки и восстановление выхода

16 ноября2026, роль2. Основа runtime d9dc3b545daa89a6c40fd93752a4e2f5bf9794a1. Ветка feat/stage3-runtime-validation, отдельный worktree Game/TestResults/s306b-worktree в корне исходной копии soul_game. Исходная feat/stage3-layout и её незакоммиченные документы сохранены. Ревью У1 S3-07 принято как источник P2; его прежние числа остаются историей У1, не результатом этой правки. Новых commit/push/merge этим поручением нет.

## Изменения и API

Game/Assets/_Game/Scripts/Sandbox/ArenaController.cs: TryExit() остаётся void без новой публичной границы. При Interact повторяет StreetExitPortal.TryOpen(PlayerCombatFacade) только после phase/InArena, pause, alive и RemainingEnemies guards; после открытия сохраняет proximity1.5. Если размещение всё ещё невозможно, HUD объясняет повтор по E. Нет Update-поиска каждый кадр, обхода AI или изменения геометрии/баланса. Существующая .meta не меняется.

Новый Game/Assets/_Game/Tests/PlayMode/GeneratedStreetLifecycleTests.cs и собственная .cs.meta: десять generated-попыток через Home, плюс восстановление P2 при удалении временной World-помехи. Сохранены старые StreetRunTests, GeneratedStreetRunTests и Foundation assertions. Game/Docs/Plans/S3_RUNTIME_PLAN.md ведёт этот шаг; Coordination/ROLE_2_STATUS.md получает только новую запись. S3_06_RUNTIME_HANDOFF.md получает ссылку на эту передачу без изменения прежней истории.

## Матрица автоматики

Попытки seed0/123/-1/-2147483648/2147483647/42/7/-123/99/123. Конечные сегменты5/1/2/3/4/5/2/3/4/5; исходы Victory/Defeat/Abandoned/Defeat/Abandoned/Defeat/Abandoned/Defeat/Abandoned/Victory. Победы проходят все5 сегментов; обычный выход и смерть запрашиваются в одном кадре на1/3, смерть босса и героя — на5. Активные пули создаются перед каждым переходом/исходом, двойные Interact/Leave, ресурсы и новый RunId проверяются без подмены ручного боя. ModuleId сравниваются с независимым вызовом production генератора на живом каталоге. HUD содержит сегмент/seed/G1/C1 и условие выхода, один EventSystem/registry/hero, SafeArea anchors сверяются с фактическим Screen.safeArea.

Проверяется уничтожение старых героя/арены/HUD/пулов, отсутствие активных Projectile и ShieldMinion, отсутствие промежуточного результата и возврат timeScale. Это поведенческая проверка утечек; полного memory-profiler/GC-root анализа подписок не проводилось. Код DetachCombat по-прежнему снимает Changed/Died/Interact/Pause и HUD.Unbind; его смысл не менялся.

## Реальные результаты

Focused2.xml: discovered/executed/passed2/2/2, failed/skipped/inconclusive0, exit0; duration5.9118496с. В нём десять подтверждённых исходов/34 загрузки сегментов. Полный EditMode.xml уже получен:78/78/78, failed/skipped/inconclusive0, exit0, duration0.3888148с. Полный PlayMode.xml:94/94/94, failed/skipped/inconclusive0, exit0, duration120.2601617с. Все полные наборы выполнены на текущих исходниках d9dc3b5 + перечисленный diff. Unity6000.3.28f1 Mac, отдельное разрешение пользователя на внешний executable получено для S3-06B; исходный Editor/lock не трогаются. Команды без -quit: -batchmode -projectPath <worktree>/Game -runTests -testPlatform PlayMode -testFilter Game.Tests.GeneratedStreetLifecycleTests -testResults <worktree>/Game/TestResults/s306b-validation/Focused.xml -logFile .../Focused.log; полные наборы без testFilter, EditMode/PlayMode соответственно. XML/логи/Library остаются вне Git.

## Ручной маршрут и ограничения

**NOT RUN**: ручной бой обычным боезапасом8+16 и ножом по всему маршруту; оценка застреваний AI/сложности по сегментам; минимум3 ручных seed включая fallback. В сессии нет управления native Unity/Game View. Телепортации/урон1000 в автоматике не являются прохождением игроком. Нет доказанных ручных замечаний к геометрии, поэтому prefab/сцены не исправляются; баланс не меняется и новая настройка не предлагается без наблюдений.

Для человека на этой копии: открыть Bootstrap→Home, пройти0 (b,a,b,b,boss при G1/C1) и123 (a,b,c,a,boss), отметить для каждого1.1–1.5 остаток HP/armor/magazine/reserve/stamina, применение ножа, достижимость E, врага/позицию/препятствие/время застревания и причину поражения. Третий проход — controlled fallback из GeneratedStreetRunTests с64 bad-вариантами, отдельно видимая причина резерва; обычный production-каталог fallback не вызывает. Не редактировать общий Resources-asset ради демонстрации. Затем Stage1/Stage2 обычным вводом, пауза/смерть/повтор. Ручной баланс по сегментам остаётся незаполненным, а не «пройдено без застреваний».

**NOT RUN**: standalone/Mac Player/Windows/Android, multitouch, вырезы экрана/смена ориентации на устройстве, нагрев/FPS, memory-profiler. Screen.safeArea в batch проверяет лишь текущее значение Mac, не доказывает работу на телефоне. S3-06B частично завершена до ручного маршрута; вся Stage3 не Done. Следующий ограниченный шаг — ручная приёмка этой версии с протоколом по сегментам, затем review исправления P2 У1. Постоянную карту/экономику не начинать.

## История проверки

Первый Focused.xml: discovered/executed2/2, passed1/failed1, skipped/inconclusive0, exit2. P2 recovery прошёл. Десять циклов остановились по timeout при попытке1: подготовка через ApplyDamage(50) активировала защиту от следующего попадания, поэтому смертельный удар в том же кадре не применялся. Исправлен только setup нового теста: RestoreForEncounter(80,0), не механика DamageReceiver. Смысл проверок смерти сохранён; добавлены проверки knife/stamina и вывод каждого сегмента. Первый XML не удалён/не перезаписан.

## Итог проверки текущего diff

Полные EditMode78/78 и PlayMode94/94 подтверждены уникальными test-case XML; отдельные Focused2 2/2 входят в94, не дают96. В полном PlayMode зафиксированы все10 S306B outcome-строк и34 загруженных сегмента. Прежние FoundationFlow3, Stage2Arena7, Stage2RouteIntegration10, fixed StreetRun5 и GeneratedStreetRun3 прошли вместе с новой парой. Все отслеживаемые Game/Assets кроме ArenaController байт-в-байт равны d9dc3b5; прежние assertions/сцены/prefab/данные/Generation не менялись. Новый test GUID fb7bcfe8ad3f4339bb5def427c3a7200 уникален. git diff --check прошёл. Индекс пуст; локального коммита/публикации нет.

Локальные XML/логи: Game/TestResults/s306b-validation/{Focused,Focused2,EditMode,PlayMode}.{xml,log} относительно worktree. process-exits.json содержит фактические exit0 двух полных процессов. verified-summary.json содержит counts, SHA XML и трёх исходников исполнения. SHA256 EditMode.xml b55ef8ad7a7557475b8ae47c3311e530de4a5499e48ff9bc9fec353742c6bbbd; PlayMode.xml265de03aa0868383dc3dcdd650869909b68b5b11d2994bf2b685445b2a1afe5a. Длительности Runner не являются FPS/производительностью телефона. Логи содержат служебные licensing/debugger сообщения; отсутствие любых сообщений Console не заявляется.

Точный набор передачи: ArenaController.cs (существующая meta без изменений); GeneratedStreetLifecycleTests.cs + .cs.meta; Game/Docs/Plans/S3_RUNTIME_PLAN.md; Game/Docs/S3_06_RUNTIME_HANDOFF.md; Game/Docs/S3_06B_LIFECYCLE_HANDOFF.md; Coordination/ROLE_2_STATUS.md. Других изменений для включения нет; временная Runner-сцена InitTestScene и её meta, если остались, не относятся к передаче, не удаляются агентом и не включаются в будущий коммит.

P2 закрыт этим изменением по новой поведенческой регрессии на Mac Editor, требуется отдельное ревью У1; это не повторный диагностический Passed старого дефекта. Ручная приёмка/AI-сложность и устройства остаются открытыми. Общая стадия не объявляется завершённой.
