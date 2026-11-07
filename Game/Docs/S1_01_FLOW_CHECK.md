# Проверка пустого цикла S1-01

Состояние на 2026-11-07: локальная проверка Editor/Windows завершена; основа готова к передаче. Общая приёмка ожидает Recipient Mac launch.
Новый цикл Foundation проверен отдельно от BaselineSmoke. Ручные сценарии подтверждены пользователем; XML и summary проверены агентом.

## Сценарии повторной проверки (локально уже выполнены)

1. Дождаться импорта скриптов. При красной ошибке скопировать её полный текст, не удалять Library или .meta.
2. Сохранить текущую сцену вручную, выйти из Play. Выполнить Game → Setup → Create foundation scenes.
3. Открыть Assets/_Game/Scenes/Bootstrap.unity. Не запускать Home или CombatSandbox напрямую.
4. Нажать Play. Должны появиться дом, описание проверочного маршрута и кнопка старта.
5. Дважды быстро нажать старт: открывается одна арена. На арене герой и враги явно не подключены.
6. Нажать тестовый выход: снова дом, новая попытка доступна. Повторить десять раз. В устойчивой сцене один GameServices, одна камера и один EventSystem, Console без красных ошибок.
7. Открыть Window → General → Test Runner: EditMode Run All, затем PlayMode Run All. В новых файлах предусмотрены 9 EditMode и 3 PlayMode теста; фактическое количество и результат взять из Runner. Сохранить XML в новой папке Game/TestResults/<runId>/ без замены прежнего отчёта. Ноль найденных тестов — не успех.
8. Выйти из Play. Game → Build → Build S1 foundation Windows. Ожидается S1_FOUNDATION_BUILD_OK и новая папка Game/Builds/Foundation/Windows/<buildId>/.
9. Запустить полученный Game.exe и повторить десять циклов. Старую Baseline-сборку не путать с новой.
10. Передать ту же основу товарищу; он открывает Bootstrap в той же версии редактора.

## Фактический результат 2026-11-07

editor: 6000.3.28f1
buildId: 20261107-100815-e8c398c4
Editor ten round trips: 10+, подтверждено пользователем; двойной старт проверен, Console без красных ошибок
EditMode count/passed/failed: 9/9/0, skipped=0; проверен XML Game/TestResults/2026-11-07-1/TestResults_20261107_130750.xml
PlayMode count/passed/failed: 3/3/0, skipped=0; проверен XML Game/TestResults/2026-11-07-1/TestResults_20261107_130619.xml
Windows build report: Succeeded, errors=0, warnings=0; проверен foundation-build-summary.txt в Builds/Foundation/Windows/20261107-100815-e8c398c4/
Windows exe ten round trips: 10+, подтверждено пользователем; дефектов не заявлено
Recipient Mac launch: ожидается передача и подтверждение получателем
Android device: ещё не предоставлен

Build-команда пишет foundation-build-summary.txt рядом с exe, с результатом BuildReport и версией редактора. Наличие .dll или файлов сцены само по себе не доказывает ручные сценарии.
