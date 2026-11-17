# S3-06A — интеграция генератора У1 и seed-профиля

16 ноября2026, роль2, ветка feat/stage3-runtime. Получена ветка У1 feat/stage3-layout по07fa6454f732492de949612fa319a6205e3d0f81, включая S3-01/03A/03B/05. Локальное слияние6450940 сохранило наш маршрут0ec3cf4/cef048e. Два add/add-конфликта личных планов разрешены сохранением наших поздних версий с новым пользовательским направлением. Исходники Generation У1 объединены без изменений. Main не обновлялся этой интеграцией.

## Готовое поведение

Из Bootstrap/Home доступен StreetRun с полем seed. Явное целое int32, включая отрицательные и крайние значения, воспроизводит выбор5 Id при каталоге/версиях1/1. Пустое поле получает новый Guid-derived int. Ошибочный текст или выход за int32 оставляет игрока в Home с сообщением. Повреждённая форма каталога/непроходимый Boss отклоняются до загрузки, RunId новой попытки не создаётся. Успех загружает единственный Stage3StreetSandbox с пятью выбранными модулями, сохраняет прежние RunId/ресурсы/портал/приоритет смерти.

Кнопка явно называет режим seed-прототипом. Генератор этой передачи выбирает последовательность геометрических модулей по прежнему контракту. Новое требование постоянной многокомнатной карты и переменных столкновений остаётся целевым направлением из STREET_MAP_AND_BOSS_STRATEGY.md. Сейчас интегрирован именно прототип из текущего поручения S3-06A. Перед следующей итерацией У1 должен прочитать уточнение и согласовать Encounter-контракт.

HUD показывает seed, G1/C1, текущий сегмент и ModuleId. При UsedFallback=true показывает резерв и краткую причину последнего отклонения до100символов. Полная Diagnostic сохраняется в RunCoordinator.StreetGeneration и строке STREET_GENERATED лога. Естественная демонстрация fallback использует64 непроходимые bad-альтернативы и seed0, после20 отказов включается валидный a,b,c,a,boss. В нормальном принятом каталоге все варианты проходимы, поэтому fallback не требуется. Поле seed скрывается на экране результата и возвращается при переходе в убежище.

## API и единая граница

Game.Street.StreetCatalogAsset в Resources/StreetCatalog.asset содержит ссылки на6 принятых StreetModuleDefinition. StreetGenerationAdapter.BuildCatalog(StreetModuleDefinition[]) создаёт неизменяемые ModuleDescriptor/StageCatalog У1 из живых Unity-данных, проверяет форму и наличие prefab. Enum Kind соответствует Entry/Combat/Rest/Boss. Вход/выход/spawn переводятся в Cell без изменения y*width+x. Метод TryGenerate(string,StageCatalog,out GenerationResult,out string) обрабатывает seed/ошибки, вызывает RunGenerator.Generate(seed,catalog,1,1), повторно LayoutValidator.Validate и преобразование пяти выбранных Id через ToRoute(RunLayout). Shape/null/range ошибки становятся отказом до боя. Семантическая проходимость проверяется у выбранных модулей по политике генератора У1, чтобы допустимый fallback оставался возможен. Неиспользованные Entry/Rest прошли отдельную физическую/BFS-проверку S3-04A и reference moduleReview.

RunCoordinator.StartGeneratedStreetRun(GenerationResult,StageCatalog) повторно проверяет результат и только затем вызывает прежнюю границу StartStreetRun(seed,StreetRoute). StreetGeneration удерживает immutable результат текущей попытки. Смена на другой профиль очищает эти метаданные. Fixed API сохранён для отдельной проверки переходов. StartRun по-прежнему игнорирует дубли после ухода из Home, второй координатор не создаётся. Все реальные prefab и данные сцены остаются прежними, Resources ссылается на те же6 asset, которые использует загрузчик.

## Экспорт для У1

Game/Docs/Exports/S3_STREET_CATALOG.json имеет schemaVersion/generatorVersion/contentVersion1/1/1, entry/exit массивы[x,y], spawnCells объекты{x,y}. Содержит все6 Id и дополнительную геометрическую информацию origin(-18,-10), cellSize1, actorClearance.45. SHA256 текущих байтов экспорта `c82a869a9f71d0ed61766ceb1b2c0e3e6f6eeaef983e040cbf907a256574251f`. После переноса между системами с разными окончаниями строк пересчитать byte SHA вместе с отчётом. Старый Game/Docs/S3_STREET_CATALOG.json сохранён как физическая передача прежнего формата.

Строгая reference-проверка выполнена командой python Setup/Stage3/review_layout_reference.py --catalog Game/Docs/Exports/S3_STREET_CATALOG.json. Отчёт Game/TestResults/s305-reference-c173e8ce/review.json. Python3.13/WindowsAMD64, seed0..999,1000 успехов,0 отказов,0 fallback,81 различная последовательность, moduleReview всех6 без замечаний. Длительность reference3.7144552с, не frame time и не производительность Unity.

Production C# sweep на тех же реальных данных выполнен в StreetGenerationTests.ActualExportMatchesUnityAssetsAndProductionThousandSeedReviewPasses. Он сначала сопоставляет все поля описаний Unity и JSON, затем GenerationSweepReview.Review даёт1000 успехов/0 отказов. Summary/elapsedMs находятся в TestContext вывода XML/лога EditMode. Старые У1 LayoutReviewTests эталона и отрицательные1000 seed также исполнены в полном наборе.

## Реальные Unity-результаты

Windows Editor6000.3.28f1. Создание Resources/экспорта успешно, create.log exit0 в Game/TestResults/s306-verify-07fd71e7603542f995a0df0d4ad13f26/. Первый подготовительный Python-скрипт встретил системную cp1251 при чтении UTF-8, исправлен явным encoding; первый Unity executeMethod поэтому не нашёл ещё не созданный helper. Первичный create.log сохранён в папке по .agent/S306_RESULTS.txt. Это подготовительная ошибка, первые тестовые XML не подменялись.

Полные EditMode discovered/executed/passed78/78/78 и PlayMode91/91/91, failed/skipped/inconclusive0, exit0. XML Game/TestResults/s306-verify-07fd71e7603542f995a0df0d4ad13f26/EditMode.xml и PlayMode.xml, логи рядом. После ограничения текста причины и добавления финальной демонстрации отдельно выполнен весь GeneratedStreetRunTests:3/3/3, failed/skipped/inconclusive0, exit0, XML Game/TestResults/s306-ui-fad6a747157a4d4c977d6764ff164a1a/PlayMode.xml. Два этих сценария повторяют случаи полного91, третий новый. Всего92 различных PlayMode-случая проверены по двум XML, единого полного XML с total92 не заявляем. EditMode после малой правки UI-текста не повторялся.

Проверены строгий seed, пустое поле, ошибка переполнения, одинаковый маршрут при повторе, реально отличный от фиксированной схемы seed0, испорченный Boss до старта, естественный fallback с видимой причиной в настоящем HUD, forged success до загрузки, сохранение метаданных и5 реальных переходов/один результат/повтор. Старые StreetRunTests меняют только запуск — явно fixed API вместо теперь сгенерированной кнопки Home. Их прежние assertions и Foundation tests сохранены.

## Ручная проверка и остаток

Ручная приёмка этой интеграции ещё открыта. Открыть Bootstrap, Play, ввести0 рядом с кнопкой StreetRun, пройти маршрут. Повторить0 и сравнить Id, попробовать123 и пустое поле, ввести abc и увидеть отказ в Home. Проверить seed/G1/C1 в HUD и прежний ближний портал. Порядок модулей меняется в этом прототипе, состав врагов пока прежний. Full-map география, Encounter-вариативность, мини-боссы/лут остаются отдельной работой. Standalone/Mac/Android и замеры не выполнены.

Для позднего S3-07 У1 получить нашу ветку/hash с этим документом и экспортом, повторить строгую проверку и C# sweep на своей платформе, затем ручной Bootstrap-маршрут и ревью изменения требований. Компактный источник доказательств — этот handoff, исходные XML локальны вне Git. Фиксация интеграции по .agent/S306_COMMIT_PATHS.txt. Для запрошенной передачи У1 отправляется feat/stage3-runtime. Слияние в main выполняется отдельным поручением.

## Продолжение S3-06B — 16 ноября2026

Исторические результаты S3-06A выше не переписываются. Текущая работа роли2 на Mac ведётся от d9dc3b5 в отдельной ветке feat/stage3-runtime-validation, рабочая копия Game/TestResults/s306b-worktree. Передача исправления P2 портала, десяти generated-попыток и новые фактические XML/ограничения — Game/Docs/S3_06B_LIFECYCLE_HANDOFF.md. Ручной маршрут обычным боезапасом/ножом и устройства остаются открытыми; этот абзац не означает приёмку всей Stage3 или merge runtime.
