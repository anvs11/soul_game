# S3-01 — Game.Generation v0.1, передача У1 → У2

Актуализация S3-03B,16 ноября2026: LayoutValidator теперь проверяет структуру пятёрки и BFS-достижимость выбранных модулей. Generate проверяет каждый кандидат, максимум20, затем валидирует fallback street_a/b/c/a/boss; при неуспехе возвращает Failure. MaxAttempts=20 — новая константа, сигнатуры/конструкторы сохранены. Текущий источник правил/примеров/результатов — S3_03_GENERATION_HANDOFF.md; старые записи ниже о заглушках описывают предыдущие состояния.

Актуализация S3-03A, 16 ноября2026: исходное состояние S3-01 ниже сохранено как история. Generate теперь возвращает кандидат4 Combat+1 Boss для поддерживаемых версий1/1; полная спецификация и фактические проверки — S3_03A_GENERATOR_REPORT.md. Публичные конструкторы/сигнатуры не изменены; добавлены константы RunGenerator.SupportedGeneratorVersion и SupportedContentVersion (обе1). LayoutValidator по-прежнему возвращает неуспех до S3-03B; кандидат не разрешает бой без валидации.

16 ноября 2026. Основа main `cc6777330efa36f254b37840f93a671728c99124`, ветка `feat/stage3-layout`. Минимальный каркас типов; алгоритмы генерации/достижимости ещё НЕ реализованы. Компиляция C#/Unity и Runner **NOT RUN**: предоставлен путь проекта Game/, не исполняемый путь Editor; dotnet/csc/mcs в PATH не найдены, UnityLockfile сохранён. Считать передачу исходников подготовленной, компиляционную приёмку — открытой.

## Файлы и размещение

Новые `Game/Assets/_Game/Scripts/Generation/GenerationContracts.cs` и `GenerationServices.cs` входят в существующую Game.Runtime. Новые `Game/Assets/_Game/Tests/EditMode/Generation/GenerationContractTests.cs` входят в существующую Game.Tests.EditMode. Все три файла имеют meta, обе новые папки Generation — folder meta. GUID приведены в самих meta; перед фиксацией проверяется уникальность. Новых asmdef/пакетов/сцен/боевых контрактов нет. Два START_STAGE_3_ROLE_* и S3_00_READINESS передаются как договорённости; план — `Game/Docs/Plans/S3_LAYOUT_PLAN.md`.

## Полный публичный API

Namespace всех типов — `Game.Generation`. Классы sealed, свойства только get. Коллекции представлены IReadOnlyList, входные IEnumerable копируются в ReadOnlyCollection; наружу изменяемый массив не возвращается. Вложенные элементы также неизменяемы. Это plain C# данные, не ScriptableObject/Unity-сериализуемые поля: У2 экспортирует собственные Unity-assets через конструкторы.

```csharp
readonly struct Cell : IEquatable<Cell> {
    Cell(int x, int y); int X { get; } int Y { get; }
    bool Equals(Cell other); override bool Equals(object obj); override int GetHashCode();
}
enum ModuleKind { Entry = 0, Combat = 1, Rest = 2, Boss = 3 }
sealed class ModuleDescriptor {
    ModuleDescriptor(string id, ModuleKind kind, int width, int height,
        IEnumerable<bool> walkable, Cell entry, Cell exit, IEnumerable<Cell> spawnCells);
    string Id { get; } ModuleKind Kind { get; } int Width { get; } int Height { get; }
    IReadOnlyList<bool> Walkable { get; } Cell Entry { get; } Cell Exit { get; }
    IReadOnlyList<Cell> SpawnCells { get; }
}
sealed class StageCatalog {
    StageCatalog(IEnumerable<ModuleDescriptor> modules);
    IReadOnlyList<ModuleDescriptor> Modules { get; }
}
sealed class SegmentLayout {
    SegmentLayout(int index, string moduleId);
    int Index { get; } string ModuleId { get; }
}
sealed class RunLayout {
    RunLayout(int seed, int generatorVersion, int contentVersion,
        IEnumerable<SegmentLayout> segments);
    int Seed { get; } int GeneratorVersion { get; } int ContentVersion { get; }
    IReadOnlyList<SegmentLayout> Segments { get; }
}
sealed class GenerationResult {
    static GenerationResult Success(RunLayout layout, bool usedFallback, string diagnostic);
    static GenerationResult Failure(string diagnostic);
    bool IsSuccess { get; } RunLayout Layout { get; }
    bool UsedFallback { get; } string Diagnostic { get; }
}
sealed class ValidationResult {
    ValidationResult(IEnumerable<string> reasons);
    bool IsValid { get; } IReadOnlyList<string> Reasons { get; }
}
sealed class RunGenerator {
    GenerationResult Generate(int seed, StageCatalog catalog, int generatorVersion, int contentVersion);
}
sealed class LayoutValidator {
    ValidationResult Validate(RunLayout layout, StageCatalog catalog);
}
```

Блок выше — справочник сигнатур, не самостоятельный компилируемый файл: public для перечисленных членов задан в исходниках. GenerationResult имеет только приватный конструктор; создание через фабрики. Сервисы создаются `new RunGenerator()`/`new LayoutValidator()` и не имеют Unity/GameObject-зависимостей.

## Проверки конструкторов и границы валидатора

Id — непустая и не состоящая только из пробелов строка, без автоматического trim/смены регистра. Сравнение Id — Ordinal, регистр значим. ModuleKind должен быть объявленным enum-значением. Размеры положительные, произведение не превышает int.MaxValue, Walkable.Count равен Width*Height; Entry/Exit/SpawnCells внутри границ. Null коллекции → ArgumentNullException, неверная форма/дубликат Id/null элемент каталога → ArgumentException, неверные размеры/координаты/Index/версии → ArgumentOutOfRangeException. Cell отдельно допускает любые int; его границы проверяет ModuleDescriptor. Пустой SpawnCells допустим для Entry/Rest; дубликаты точек и семантика Combat/Boss проверяются будущим валидатором.

StageCatalog копирует порядок входных модулей и запрещает повтор точного Id. Будущий генератор обрабатывает каталог в сортировке StringComparer.Ordinal, чтобы экспортный порядок не менял выбор. Пустой каталог допустим как объект данных, но будущая Generate вернёт отказ при отсутствии нужного контента. Null segments/элементы запрещены, Index в1..5, версии положительные. RunLayout намеренно допускает неверное число/порядок сегментов и неизвестный ModuleId: это кандидат для проверки, не удостоверение валидности.

`Success` требует ненулевой Layout и ненулевой Diagnostic (пустой допустим). `Failure` требует непустую причину, задаёт Layout=null, IsSuccess=false, UsedFallback=false. Фабрика Success сама не проверяет достижимость: runtime обязан проверять IsSuccess, затем результат LayoutValidator. ValidationResult с пустым списком причин означает IsValid=true; причины копируются, пустые/null причины запрещены. Никакая фабрика не должна использоваться как обход проверок.

## Правила для следующих S3-03A/B

Успешный маршрут содержит ровно5 сегментов в порядке Index1..5: четыре Combat и один Boss последним. Combat могут повторяться. Entry/Rest — вспомогательные посещения вне этой пятёрки; в RunLayout отдельных слотов для них нет, У2 задаёт политику посещения в runtime-плане. Рекомендация остановки после1.2 остаётся предложением, не реализованным поведением этого API.

Координаты сетки локальные, X вправо/Y вверх, индекс y*Width+x, шаг в экспортируемом описании —1 world unit; Unity-адаптер задаёт начало сетки и преобразование из локальных координат модуля. Клетка означает допустимое положение центра актора с заранее учтённым clearance. Проверка по четырём соседям не допускает диагонали. Для Boss Exit — достижимая логическая конечная клетка, не требование физической двери после смерти босса. Реальный collider босса радиуса2, места призыва, фактические проходы/безопасные точки отдельно проверяет У2; маленькая сетка примера ниже этого не доказывает.

Политика версий: положительные версии записываются в RunLayout; версия1 резервируется для первого алгоритма/эталонного контента. Сейчас алгоритмов нет, поэтому все положительные версии дают NOT IMPLEMENTED failure. В S3-03 зафиксировать поддерживаемые значения и возвращать явный отказ на неподдерживаемую generatorVersion; contentVersion должна соответствовать согласованному снимку каталога, не только произвольной подписи. Семантическая проверка геометрии/контента и соответствия версий пока не реализована.

Seed — любой int, включая отрицательные и крайние значения. Будущий локальный PRNG не использует время/Guid/UnityEngine.Random; одинаковые входы воспроизводят5 Id, не физику боя. Подбор ограничен20 попытками, затем a,b,c,a,boss с UsedFallback=true и диагностикой. Невалидный каталог/fallback — Failure без запуска сцены. Структурные ошибки адаптера отсеиваются конструкторами до Generate; семантические ошибки возвращаются результатом сервиса. Алгоритм повторов и тестовый сценарий fallback уточняются в S3-03, не имитируются случайным отказом S3-01.

## Пример каталога из6 Id и вызов

```csharp
ModuleDescriptor Example(string id, ModuleKind kind) => new ModuleDescriptor(
    id, kind, 2, 2, new[] { true, true, true, true },
    new Cell(0, 0), new Cell(1, 1), new[] { new Cell(1, 0) });
var catalog = new StageCatalog(new[] {
    Example("street_entry", ModuleKind.Entry), Example("street_a", ModuleKind.Combat),
    Example("street_b", ModuleKind.Combat), Example("street_c", ModuleKind.Combat),
    Example("street_rest", ModuleKind.Rest), Example("street_boss", ModuleKind.Boss)
});
var result = new RunGenerator().Generate(123, catalog, 1, 1);
if (!result.IsSuccess) { /* показать result.Diagnostic, бой не запускать */ }
else {
    var validation = new LayoutValidator().Validate(result.Layout, catalog);
    if (!validation.IsValid) { /* показать Reasons, бой не запускать */ }
    else { /* У2 загружает принятый маршрут */ }
}
```

Это иллюстрация формы экспорта и тестовая фикстура, не принятый игровой Boss-модуль. На S3-01 result всегда Failure с диагностикой `generation is not implemented`; Validate ненулевого кандидата всегда IsValid=false с причиной `reachability validation is not implemented`. Пустой результат НЕ заменяется фиксированной схемой внутри Generation. У2 может отдельно продолжать собственный фиксированный маршрут до передачи алгоритма.

## Проверки и передача

Добавлены4 EditMode-теста: GridAndSpawnInputsAreSnapshotsAndCannotBeEditedThroughViews; InvalidShapeBoundsAndDuplicateIdsFailAtConstruction; ResultFailureAndLayoutSnapshotsHaveUnambiguousState; SixIdExampleCanBeExportedButStubsNeverAuthorizeCombat. Тесты проверяют мутацию входов, запрет записи через view, неверные данные и безопасный отказ. Ориентир полного набора после добавления —46 EditMode/84 PlayMode; эти числа не являются Runner counts.

Фактически выполнены статическая проверка двух Generation-файлов на отсутствие UnityEngine/UnityEditor, проверка пяти новых meta/GUID на уникальность среди Assets/_Game, разбор существующих asmdef как JSON и git diff --check. Локальный отчёт — Game/TestResults/s301-static-bf96bd46/checks.json, в Git не входит. Из тестовых исходников получены44 Test+2 TestCase EditMode и2 Test+82 UnityTest PlayMode. Новые XML отсутствуют, discovered/executed/passed/failed/skipped/inconclusive не измерены; эти проверки не заменяют C#-компилятор.

Для У2: получить итоговый hash этой ветки, проверить diff отcc67773, открыть Game/ в разрешённом Unity6000.3.28f1, дождаться компиляции; выполнить EditMode GenerationContractTests, затем полный набор с новым экспортом XML. Не запускать второй Editor при lock. При недоступности Unity сохранять NOT RUN; прежние42/84 относятся к Stage2 и не подтверждают эти новые типы. Дальше работать через указанные конструкторы, не создавать копии Game.Generation в Street. Согласование API подтверждать конкретным diff; push/merge этой задачи не выполняются.
