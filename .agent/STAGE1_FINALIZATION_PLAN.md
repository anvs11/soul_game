# Проверить и зафиксировать первую арену перед передачей


ExecPlan по PLANS.md, 2026-11-14. Корень soul_game, проект Game/, ветка feat/stage1-flow-ui от 1154253. Пользователь подтвердил ручной просмотр Windows Editor и экранных элементов и запросил шаги 5–6. Не отправлять изменения или приложение наружу.

## Purpose / Big Picture


Получить воспроизводимые сборки только Bootstrap, Home, CombatSandbox; пройти реальный боевой цикл в отдельном Windows exe, сохранить доказательства и локальный коммит. Отделить кодовую готовность, Editor, standalone и физические Mac/Android сценарии. Полный MVP здесь не принимается: это одна арена.

## Progress


- [x] Прочитаны Stage 1, ревью У1, текущие исходники и git diff; изменения прошлых задач сохранены.
- [x] Пользователь подтвердил: Unity закрыта, Android-телефона рядом нет.
- [x] Добавлены BuildStage1Windows/BuildStage1Android с новым buildId, точными тремя сценами и проверкой BuildReport.
- [x] Новые EditMode 24/24 и PlayMode 27/27: discovered=executed=passed, failed/skipped=0, XML сохранены.
- [x] Windows 20261114-203727-d470e717 Succeeded/0 errors/0 warnings. Отдельный Player: 10 побед/1 смерть, loggedErrors=0, exit=0.
- [x] APK 20261114-203749-f6901730 Succeeded/0 errors/1 warning. Проверка на телефоне явно открыта.
- [x] Подготовлены S1_06_ROLE2_REVIEW, S1_08_ACCEPTANCE, обновлены STAGE_1_PLAN, ROLE_2_STATUS, NEXT_ROLE_2.
- [x] Проверен явный список .agent/ROLE_2_COMMIT_PATHS.txt: 65 файлов накопленной работы роли 2 и её зависимостей. Локальная фиксация выполняется без push; номер последнего коммита проверяется через git log -1.

## Surprises & Discoveries

Реальная стрельба в Windows Player выявила исключение при разгрузке ProjectilePool: независимый контейнер с пулами может быть уничтожен до владельца ObjectPool. Исправлен callback уничтожения, добавлен DestroyedReturnedProjectileDoesNotBreakPoolOwnerTeardown. Ранний runner не учитывал ошибки вне своей coroutine; теперь считает все Error/Exception/Assert. Неуспешные попытки не приняты. CaptureScreenshot скрытого окна вызывает Failed to capture screen shot; скриншот выделен в явную опцию --stage1-screenshot и не входит в скрытый строгий прогон. Визуальный standalone результат этим не подтверждён.


Ревью У1 S1_06_REVIEW.md относится к версии до нашей интеграции. Его блокер отсутствия HUD/ArenaController устранён; его старые expected counts не являются текущими результатами. Накопленные изменения роли 2 ещё не закоммичены, включая новые исходники и .meta. Для воспроизводимого коммита нужно включить рабочие зависимости, но не архивы временной доставки и не чужие материалы.

## Decision Log


Decision: явный список из трёх сцен в BuildPlayer. Rationale: диагностические сцены не должны попасть в обычную сборку из-за состояния Build Settings. Date/Author: 2026-11-14, роль 2.

Decision: Development-only runner с явным флагом --stage1-smoke. Rationale: проверить именно exe через существующие команды, настоящее оружие/AI, меню и переходы; без флага обычная игра не меняется. Автоматический прогон не называть ручной проверкой. Date/Author: 2026-11-14, роль 2.

## Outcomes & Retrospective


Новые тесты и обе сборки выполнены, реальный Windows Player прошёл 10 побед и одну естественную смерть без loggedErrors. Протокол Game/Docs/S1_08_ACCEPTANCE.md содержит пути XML и buildId, включая неуспешные попытки. Проверка Windows Editor подтверждена человеком; самостоятельного ручного осмотра финального exe нет. Android-устройство отсутствует, Mac и независимое ревью У1 не подтверждены. Полная общая Stage 1 остаётся открыта, полный MVP не достигнут. Урок: переходы без активного оружия не проверяют порядок уничтожения настоящего пула.

## Context and Orientation


GameBootstrap создаёт RunCoordinator. HomeScreen содержит результат и старт. ArenaController связывает три врага и игрока, выход/пауза работают. Ввод проходит через ActorMotor2D и PlayerCommand. TestResults и Builds исключены из Git. Контракт v0.1 и prefab У1 не заменять. Согласованные правки выносливости, удара и границ входят в роль 2 и должны передаваться вместе с интеграцией.

## Plan of Work


Создать Scripts/Editor/BuildCommands.cs: версии, список сцен, наличие модулей, выход в новую Builds/Stage1/<platform>/<id>, BuildReport и JSON manifest. Создать Development-only Scripts/Sandbox/Stage1StandaloneSmoke.cs: при явном флаге запустить из Bootstrap 10 побед с настоящей стрельбой и перемещением к выходу, затем естественную смерть, проверить свежие объекты/ресурсы, паузу и отсутствие пуль после выхода; сохранить JSON и screenshot. В обычном запуске компонент не создаётся. Выполнить тесты, собрать Windows, запустить exe скрыто с собственным логом. Проверить Android модуль и по возможности собрать APK, без устройства runtime оставить открытым. Свести review и протоколы, выбрать явные пути рабочего кода, .meta и документов для локального коммита.

## Concrete Steps


Рабочий каталог — корень soul_game. Разрешённый Unity.exe 6000.3.28f1: -batchmode -nographics -projectPath <root>/Game -runTests -testPlatform EditMode, затем PlayMode; новые XML/логи в TestResults/stage1-final-<id>. Сборки: -batchmode -quit -buildTarget Win64 -executeMethod Game.Editor.BuildCommands.BuildStage1Windows; Android отдельно с -buildTarget Android, если модуль доступен. Exe запускать с --stage1-smoke -screen-width 1280 -screen-height 720 -windowed -logFile <папка сборки>/smoke-player.log. Не подменять запуск exe Editor-тестом. Git: diff --check, затем add -- <явные пути>, diff --cached --stat, commit; push запрещён этой задачей.

## Validation and Acceptance


XML имеют ненулевой discovered/executed и failed/skipped=0. BuildReport.result=Succeeded, summary содержит фактические errors/warnings. Список сцен ровно Bootstrap/Home/CombatSandbox. Standalone runner даёт exit=0 и JSON с 10 победами/1 смертью, новыми runId, живым HUD и реальным расходом патронов. Screenshot проверить отдельно, если получен. Наличие APK не означает Android runtime; нет телефона — явное открытое условие. Mac также открытый. Коммит не содержит Library, Builds, TestResults, ключей и архивных копий доставки.

## Idempotence and Recovery


Новый buildId и каталог результатов каждый раз. Не удалять исходники или прежние сборки, не перезаписывать assets и не выполнять reset/clean. При ошибке остановиться, сохранить лог, исправить относящийся дефект. Собственные runtime-объекты smoke-runner уничтожаются сменой сцен; обычные профили не читаются и не создаются.

## Artifacts and Notes


Протокол Game/Docs/S1_08_ACCEPTANCE.md, обновление ревью Game/Docs/S1_06_ROLE2_REVIEW.md, текущий Coordination/ROLE_2_STATUS.md. В Git компактные доказательства с относительными путями и числами; XML/бинарные артефакты остаются локально.

Финальный каталог тестов Game/TestResults/stage1-final-pool-68a93d40245a447a8b9fa3141156a7b9/: EditMode.xml и PlayMode.xml, соответствующие логи и build-Win64-accepted.log/build-Android-accepted.log. Итоговый Windows результат: Game/Builds/Stage1/Windows/20261114-203727-d470e717/smoke-33d04057c02f48adb2d0be18b1f11c91/result.json (passed=true, victories=10, defeats=1, loggedErrors=0). Android APK: Game/Builds/Stage1/Android/20261114-203749-f6901730/ZombieExpedition.apk; предупреждение касается отсутствия символов для crash diagnostics.

Проверка индекса: diff --cached --check чистый после удаления только хвостовых пробелов из автоматически созданного preview .meta, GUID сохранён. 65 явных путей, 4610 добавленных строк до этой записи; значительную часть составляют YAML двух сцен. Это накопленная интеграция роли 2, а не только последние две служебные команды. Prefab У1, контракт, Packages и ProjectSettings не имеют diff. Временные .agent/*Delivery*, .agent/*.txt с черновиками, .agent/ZombieMeleeTests.cs и Game/.utmp/ не включены и не удалены. Команда фиксации: git commit -m "Complete Stage 1 arena integration and local validation". Push и публикация не выполняются.

## Interfaces and Dependencies


Game.Editor.BuildCommands.BuildStage1Windows(), BuildStage1Android(). IPlayerInputSource/PlayerCommand и существующие игровые классы. Новых пакетов и внешних сервисов нет.

## Revision Note


2026-11-14: план последних двух шагов с отдельным Windows exe и разделением неподтверждённых платформ.

2026-11-14, финализация: внесены реальные результаты, порядок уничтожения пула и строгий подсчёт ошибок, скриншот сделан дополнительной опцией; открытая платформенная приёмка не скрыта.
