# S3-05 — эталонный каталог и проверка1000 seed

16 ноября2026, У1. Ветка `feat/stage3-layout`, основа шага `ff430ad5efa8d604245fc00b27d067649513ac29`. Production генератор/валидатор/контракт и сцены этим шагом не менялись, предыдущие тесты не ослаблены. Подготовлены исполнимые C# EditMode-тесты и отдельно выполнена Python reference-проверка.

**Статус:** reference-проверка завершена, production C#/Unity sweep — NOT RUN. Каталог У2 ещё не получен; его проверка остаётся открытой. Stage3/платформенная приёмка этим отчётом не закрываются.

## Эталон и воспроизводимость

Общий JSON-эталон для обеих проверок — `Setup/Stage3/reference-catalog-v1.json`, schemaVersion/generatorVersion/contentVersion=1/1/1, fixtureId=s305-reference-v1. SHA256 файла: `905b140dd1962f30e7f99233ba96f92be5fba61347cfea2b9975767668b0661a`. Шесть Id: street_entry/a/b/c/rest/boss; обычных вариантов3, Boss1. Это искусственный каталог для проверки логики, не принятый игровой контент и не подтверждённый clearance босса.

В модуле поля id/kind/width/height/walkable/entry/exit/spawnCells. Walkable — плоский bool-массив длины width*height, индекс y*width+x. Entry/Exit — массивы [x,y]; spawnCells — массив объектов {"x":число,"y":число}. Такой формат поддерживает JsonUtility без вложенных int[][], пример всех полей находится в эталонном JSON. Координаты0-based, строки сеток ниже перечислены y0→y2. Id регистрозависимы/Ordinal; Python сортирует по UTF-16 code units, чтобы соответствовать C# даже для не-ASCII Id.

street_a содержит проход в одну клетку: `### / ... / ###`, Entry(0,1), Exit(2,1), Spawn(1,1). street_b содержит обход стены `... / ##. / ...`, Entry(0,0), Exit(0,2), Spawn(2,1),(1,2). street_c открыт3×3. Boss открыт5×5, Entry(0,2), Exit(4,2), Spawn(2,2). Entry/Rest открыты3×3, без spawn. Одна свободная строка в данных означает доступные центры с уже учтённым clearance: фактическая узкая дверь может блокировать акторов и требует отдельного физического теста. Даже открытый5×5 эталон не доказывает возможность движения реального босса радиуса2 и призыва.

## Реально выполненная reference-проверка

Из корня проекта:

```sh
python3 Setup/Stage3/review_layout_reference.py --catalog Setup/Stage3/reference-catalog-v1.json --reference-examples
```

Runner находится в репозитории, использует только стандартную библиотеку. Минимум1000 seed, по умолчанию0..999, версии1/1; для каждого успеха повторно проверяет5 сегментов/Kind/Id и достижимость, для отказа — непустую причину и отсутствие Id результата. Сохраняет отдельную запись каждого seed, количество успехов/отказов/fallback/различных схем, SHA каталога, окружение, время и замечания к каждому модулю. JSON-ошибки/дубликаты Id/неверная форма/границы дают явный отказ загрузки. Входные данные после sweep сравниваются с исходным снимком. Запускать без `-O`, иначе проверки отключились бы; runner сам запрещает этот режим.

Факты запуска на Python3.10.8/Clang13.0.0, macOS (`sys.platform=darwin`), архитектураarm64:

| Прогон | Результат | Длительность |
| --- | --- | --- |
| Эталон, seed0..999 | 1000 успешных проверенных схем,0 отказов,0 fallback,81 различная последовательность | 0,053870542 с |
| Повтор эталона, seed0..999 | 1000 успехов; все записи совпали с первым прогоном | 0,053467875 с |
| Каталог с отключённым Boss spawn(2,2), seed0..999 | 1000 явных отказов; Id результата пусты, причины непусты | 0,555716459 с |

Время измерено perf_counter вокруг reference Generate и независимой повторной проверки каждого успешного результата. Чтение/запись JSON и отдельные демонстрационные примеры не включены. Время не является CPU/frame time Unity, не сравнивается с C# реализацией и ничего не обещает про телефон; точная модель устройства/нагрузка фоновых процессов не измерялись. Python reference отдельно реализует математику и сетки, а не исполняет исходники production C#.

Первый отчёт: `Game/TestResults/s305-reference-005ba168/review.json`. Повтор/отрицательный прогон: `Game/TestResults/s305-repeat-negative-83b99abb/review.json`; дополнительно проверено отклонение дубликата Id. Оба локальны, в коммит не входят. Предыдущий пробный отчёт с ранним форматом spawn сохранён отдельно и не используется как итог. Показатели по seed описывают результаты генерации, а не1000 Unity test-case и не NUnit Passed.

## Новые C# проверки конкретных рисков

`Game/Assets/_Game/Tests/EditMode/Generation/LayoutReviewTests.cs` добавляет3 Test:

- ReferenceThousandSeedsValidateIncludingSingleCellCorridor — production Generate/Validate для0..999 на том же JSON; проверяет seed/версии/5 индексов/Id/Kind/достижимость и ожидает1000 успехов.
- BlockedCorridorEntryIsRejectedWithExactModuleReason — клетка street_a(0,1) отключена; требуется причина с Id street_a и Entry, а не просто false без контекста.
- DisabledBossSpawnProducesThousandExplicitFailures — клетка street_boss(2,2) отключена, все1000 вызовов должны вернуть Failure с причиной Spawn(2,2); после этого исходный каталог должен снова дать1000 успехов. Это проверяет отсутствие мутации и переноса ошибочного состояния.

Тестовый API `Game.Tests.GenerationSweepReview.LoadExport(string projectRelativePath)` создаёт неизменяемый StageCatalog из JSON, `Review(StageCatalog,int count=1000)` вызывает настоящий C# генератор/валидатор для каждого seed. Summary содержит Successes/Failures/Fallbacks/ElapsedMilliseconds и read-only Records с диагностикой каждого seed. Время Stopwatch охватывает C# генерацию/проверки/assertions/подготовку записей; загрузка JSON исключена. Сводка пишется в TestContext. Не путать этот тестовый helper с новым API production Generation.

**C# компиляция/импорт/EditMode/PlayMode NOT RUN.** Исполняемый путь Editor/компилятора не предоставлен; dotnet/csc/mcs в PATH не найдены, UnityLockfile сохранён. Новых XML/actual discovered/executed/passed/failed/skipped/inconclusive нет. Ориентир после3 новых Test —70 EditMode (63 Test+7 TestCase),84 PlayMode; это не результат Runner. Успех Python не закрывает исполнение этих тестов.

## Каталог У2 и последующая проверка

После fetch origin в этой копии нет передачи S3_04_MODULES_HANDOFF, каталога под Data/Stages или ветки origin/feat/stage3-runtime. Поэтому не делается вывод о готовности чужой рабочей копии и не выдумываются замечания к её клеткам. Здесь проверен только эталон; сцены У2 не открывались/не менялись.

У2 передаёт устойчивые ветку/hash, собственный экспорт `Game/Docs/Exports/S3_STREET_CATALOG.json` по указанной schema1 (все поля обязательны, целочисленные координаты, явные bool). Поля x/y объектов spawn должны присутствовать: JsonUtility сам может подставить0 при пропуске, поэтому сначала выполнять строгую Python-проверку формы. Приложить координаты начала сетки/масштаб/clearance, соответствие Id Unity-assets/GUID, радиус актора и результаты физической проверки дверей/спавна/площадки босса.

1. Записать hash передачи и SHA256 JSON. Проверить все6 согласованных Id и Kind, размеры/массив/координаты, schema/версии; порядок файлов значения не имеет. Генератор поддерживает версии1/1, смена контента требует отдельного согласования поддерживаемой версии, не переписывать номер в экспорте ради прохождения проверки.
2. Запустить `python3 Setup/Stage3/review_layout_reference.py --catalog Game/Docs/Exports/S3_STREET_CATALOG.json` без --reference-examples. Получить новый отчёт. Просмотреть moduleReview для ВСЕХ модулей, включая Entry/Rest и варианты, не встретившиеся в1000 seed; замечания с Id/клетками передать У2, не править сцены. Наличие1000 успешных схем само по себе не означает, что все варианты банка пригодны.
3. В своём разрешённом Unity создать У2-owned EditMode-тест, который вызывает `GenerationSweepReview.Review(GenerationSweepReview.LoadExport("Game/Docs/Exports/S3_STREET_CATALOG.json"))`. Для принятого каталога Assert.AreEqual(0, summary.Failures). Экспортировать XML/логи в новый Game/TestResults/s305-u2-<runId>/; сохранить Summary. Для диагностического негодного каталога отказы допустимы только с явными причинами, их нельзя назвать принятым контентом.
4. Выполнить оставшиеся GenerationContract/RunGenerator/LayoutValidator/LayoutReviewTests и полный набор. Записать actual counts, Unity/OS/устройство/режим, версии и SHA каталога; время sweep указывать как время тестовой процедуры, не обещание FPS. Без разрешённого Editor оставить C# этап NOT RUN.
5. У2 отдельно сопоставляет сетку с реальными collider, входом/выходом, clearance героя/босса и призывом, проходит реальные сцены. BFS не даёт AI навигацию и не доказывает отсутствие застреваний. Итог физической проверки и данные сетки учитываются раздельно.

## Состав шага и остаток

В коммит включаются7 путей: LayoutReviewTests.cs/meta, Setup/Stage3/reference-catalog-v1.json, Setup/Stage3/review_layout_reference.py, этот S3_05_LAYOUT_REVIEW.md, S3_LAYOUT_PLAN.md и только новая запись Coordination/ROLE_1_STATUS.md. Новая meta имеет GUIDb83b8acbb4df4c32af56d2556bec7b10; проверена уникальность. Для Setup/Docs вне Assets Unity meta не требуется. Production код и прежние тесты не менялись; XML/TestResults/Library/Builds и прежние локальные черновики исключены. Проверяются AST/JSON/GUID и рабочий/staged diff.

Эталонная reference-проверка завершена; C#/Unity sweep и экспорт/физический каталог У2 остаются открытыми. Следующий зависимый шаг — получить экспорт У2 по указанной процедуре; позднее S3-07 требует S3_06_RUNTIME_HANDOFF. Push/merge и runtime-интеграция этим шагом не выполняются.
