# S3-03A — воспроизводимый выбор модулей

16 ноября 2026, У1, ветка feat/stage3-layout; основа S3-01 `7ebd99457cce46301491984137ffa31aff4abb6b`. Публичный API Generate(int seed, StageCatalog catalog, int generatorVersion, int contentVersion) сохранён. Новая логика не загружает сцен, не меняет ресурсы и не вызывает Unity API.

## Правило версии1

Поддерживаются только generatorVersion=1/contentVersion=1, доступны одноимённые константы SupportedGeneratorVersion/SupportedContentVersion. Остальные положительные значения → Failure без Layout; неположительные/null аргументы сохраняют исключения контракта. ContentVersion — согласованная метка снимка контента, не hash: вызывающая сторона обязана менять версию при изменении принятого каталога. Другая метка пока потребует явного расширения поддержки, не принимается молча.

Combat и Boss выбираются из каталога по Kind в отдельные локальные списки, каждый сортируется StringComparer.Ordinal по Id. Entry/Rest не участвуют и не расходуют случайные числа. При отсутствии Combat или Boss — явный Failure. Выбор с возвращением: последовательно четыре Combat, затем один Boss; допускаются повторы. Когда Boss один, последний сегмент всегда его Id, при нескольких — один из отсортированного набора. Segment.Index равен1..5, seed и версии переносятся в RunLayout, UsedFallback=false. Каталог и его порядок не мутируют.

PRNG — собственный SplitMix32 с локальным uint состоянием на один Generate. Начало: unchecked((uint)seed), включая ноль и крайние int без Math.Abs. Для каждого Next все операции modulo2^32: state+=0x9E3779B9; z=state; z=(z^(z>>16))*0x85EBCA6B; z=(z^(z>>13))*0xC2B2AE35; результат z^(z>>16). Для bound>0 индекс выбирается без modulo bias: threshold=(unchecked(0u-bound))%bound, Next повторяется пока value<threshold, затем value%bound. Отбраковка отдельных чисел PRNG не является20 попытками будущего подбора маршрута. Для одного элемента draw также расходуется. Любое изменение последовательности/сортировки/математики требует новой generatorVersion.

Generate.IsSuccess сейчас означает только сформированный кандидат. Diagnostic явно требует проверки достижимости; LayoutValidator пока всегда IsValid=false. Нельзя подключать кандидат к бою без S3-03B. Поиск пути,20 попыток/fallback и1000 seed S3-05 этим шагом не выполнялись.

## Тесты

Добавлен RunGeneratorTests:5 TestCase golden vectors для0/-1/int.MinValue/int.MaxValue/123;5 Test-сценариев проверяют перестановку/повторные и перемежающиеся вызовы, новое состояние экземпляра, неизменность порядка входа, глобальный Unity Random (с восстановлением состояния в finally),4 Combat+1 Boss для64 seed/вариативность, повтор одного Combat и отказ при отсутствующих типах/версиях. Маленький каталог тестов собственный:3 Combat,2 Boss и Entry/Rest, без сцен У2.

Прежний S3-01 SixIdExampleCanBeExportedButStubsNeverAuthorizeCombat переименован SixIdExampleGeneratesCandidateButValidatorStillBlocksCombat: ожидание отказа Generate заменено ожиданием кандидата, запрет запуска через Validate сохранён. Другие проверки исходного контракта не ослаблены.

Фиксированные векторы для отсортированных Combat a/b/c и Boss street_boss/street_boss_z:

| Seed | Четыре street-модуля | Boss |
| --- | --- | --- |
| 0 | b,a,b,b | street_boss_z |
| -1 | b,a,c,c | street_boss_z |
| int.MinValue | b,b,a,c | street_boss |
| int.MaxValue | a,b,b,b | street_boss |
| 123 | a,b,c,a | street_boss |

## Фактические проверки

**C# компиляция и Unity Runner NOT RUN.** Новых XML нет; discovered/executed/passed/failed/skipped/inconclusive не измерены. Сохраняется ограничение S3-01: предоставлен путь проекта, не исполняемый путь Editor/компилятора. Ориентир исходников56 EditMode/84 PlayMode (49 Test+7 TestCase и2 Test+82 UnityTest) не является фактически пройденным набором.

Реально выполнена отдельная Python reference 32-битной формулы:5 golden vectors согласованы,64 seed дали54 различных схемы. Это проверка эталонных чисел/вариативности математической модели, не исполнение C# или эквивалент Unity-тестов. Также проверены отсутствие Unity/глобального Random-вызова в production коде, уникальность новой meta и diff --check. Локальный отчёт Game/TestResults/s303a-static-f058e64b/checks.json, не включается в Git.

Новая meta RunGeneratorTests.cs.meta имеет GUID9cb771ec3c6447a29d65fd91bae50164. Передача меняет только GenerationServices.cs, один S3-01 тест, новый RunGeneratorTests.cs/meta, S3_LAYOUT_PLAN, актуальное дополнение S3_01_LAYOUT_CONTRACT, этот отчёт и новую запись статуса У1. Боевые Contracts/сцены/prefab/Core/UI, статус У2 и личные черновики не меняются.

Приёмка в разрешённом Unity6000.3.28f1: после компиляции выполнить RunGeneratorTests и GenerationContractTests, затем полный EditMode; экспортировать XML в новый Game/TestResults/s303a-<id>/. Глобальный Random-тест должен оставлять Unity state неизменным, старые тесты боя сохраняться. При провале записать лог, seed, версии и каталог. Следующий этап — S3-03B после проверки этих исходников; публикация/слияние здесь не выполняются.
