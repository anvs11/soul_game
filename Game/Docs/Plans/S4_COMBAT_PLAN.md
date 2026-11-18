# S4-05 — состояния, кувырок, мини-боссы и ресурсы

Этот ExecPlan ведётся по `.agent/PLANS.md`. Первая версия описывает передачу движения S4-05A0 для У2/S4-04C. Дополнение от 19 ноября 2026 охватывает S4-05A1–A3, S4-05B и их интеграцию в карту S4-06B по договорённости с У1.

## Purpose / Big Picture


Герой получает единые разрешения действий: root оставляет атаку, bind/stun блокируют её, poison/slow оставляют возможность отвечать. Тестовый источник PlayerCommand вызывает кувырок через физический Rigidbody2D с остановкой у стен. У2 сможет подключить существующий ввод без копирования механики. Существующий DamagePacket остаётся единственным путём урона.

## Progress

- [x] (2026-11-19) Реализованы S4-05A1–A3 и S4-05B по договорённости с У1. Созданы отдельные prefab, тренировочная сцена и [передача API](../S4_05_COMBAT_RESOURCES_HANDOFF.md).
- [x] (2026-11-19) К 1-1 подключены реальные элитные prefab, обязательные дети и одноразовые награды. Проверены физический экспорт и десять циклов с разными исходами. На первом включении дочернего prefab Unity могла брать позицию Transform вместо позиции Rigidbody2D. Runtime теперь задаёт обе позиции до активации.


- [x] (2026-11-18) Прочитаны правила/PLANS, S4-01/Stage4CombatContracts, S4-04 handoff и локальный S4-00 аудит. Fetch подтверждает main 7cb16232d21b59c333de6e043c7dc611c39809c2.
- [x] (2026-11-18) Создана feat/stage4-movement-status в Game/TestResults/s405a0-worktree от main; локальные A/B и корневые черновики сохранены без переключения.
- [x] (2026-11-18) Записаны предложения параметров и таблица permissions до кода; пользователь подтвердил применение этих параметров прототипа.
- [x] (2026-11-18) Добавлены состояния/ограничения, совместимая DodgePressed и opt-in dodge; новые содержательные тесты написаны.
- [x] (2026-11-18) Выполнены просмотр исходников/diff, diff --check, meta/GUID/ownership; DamagePacket и файлы У2 сохранены.
- [x] (2026-11-18) Unity-компиляция и focused EditMode10/10, focused PlayMode2 15/15 прошли; первый PlayMode10/15 сохранён с5 ошибками заготовки.
- [x] (2026-11-18, до разрешения) Записан NOT RUN; исходный summary сохранён как before-unity-summary.json, чтобы не приписывать поздние прогоны предыдущему состоянию.
- [x] (2026-11-18) Получено явное разрешение Unity для S4-05A0 и подтверждение параметров прототипа.
- [x] (2026-11-18) Полный EditMode118/118, exit0, failed/skipped/inconclusive0.
- [x] (2026-11-18) Полный PlayMode120/120, exit0, failed/skipped/inconclusive0; проверены XML/counts/unique fullname/исходники по манифесту.
- [x] (2026-11-18) Сохранён S4_05_MOVEMENT_STATUS_HANDOFF с source fingerprint и ограничениями; добавлена только запись У1. После разрешённого Unity-прогона передача дополнена реальными118/120 и ограничениями: это проверенная Editor-версия на тестовом вводе, не Player/UI-интеграция.

## Context and Orientation


Основа ветки 7cb1623 содержит S4-01 интерфейсы в Game/Assets/_Game/Scripts/Contracts/Stage4CombatContracts.cs. ActorMotor2D принимает IPlayerInputSource и публикует CommandSampled; WeaponController выполняет атаку; PlayerCombatFacade публикует PauseRequested даже при отключённых controls. DamageReceiver/HealthState применяют единственный Combat/DamagePacket. На исходной основе старый Stun блокировал только движение. LocalInputSource, Player.inputactions, UI и карта принадлежат У2 и не изменяются. S4-03A/B выполнялись отдельно и уже входят в основу ветки передачи 843d279. CITY_BOSSES_AND_DODGE_CONCEPT отсутствует в полученном Git и не прочитан.

## Permissions и параметры прототипа


Разрешения объединяются запретом любого активного эффекта, а не выбором последнего источника. Пауза — системный запрос, он не фильтруется боевыми permissions. Таблица дополняет принятый S4-01.

| Состояние | Move | Attack | Dodge | Switch | PauseRequested |
|---|---|---|---|---|---|
| Обычное / poison | да | да | если готов | да | да |
| Slow | множитель минимального slow | да | если готов | да | да |
| Root 3 s | нет | да | нет | да | да |
| Bind 3 s | нет | нет | нет | да | да |
| Stun, туман 2 s | нет | нет | нет | да | да |
| Knockback | только толчок до стены | да | нет | да | да |
| Dodge | только кувырок | да | нет | да | да |
| Пауза / смерть / controls disabled | нет | нет | нет | нет | да |

Предложение v0.1 для тестового среза: speed 12 world units/s, duration 0.20 scaled s, свободная дальность 2.4 units; cooldown 0.8 scaled s от принятия; стоимость 0; invulnerability 0; атака/смена оружия разрешены. Это новые явно названные параметры, не числа отсутствующей концепции и не принятый финальный баланс. Они вынесены пользователю до кода. Пользователь подтвердил «Использовать эти параметры прототипа». Это согласие относится к текущему тестовому срезу, не к окончательному балансу. Slow изменяет обычное движение, не фиксированную скорость кувырка. Root/bind/stun прерывают текущий dodge; knockback прерывает dodge и имеет приоритет физического толчка даже при stun (сохраняет смысл взрыва Stage2). Прерывание не возвращает уже использованный cooldown. Нулевое направление: последнее ненулевое движение, затем текущий/последний aim оружия, затем down; NaN/Infinity отклоняются.

## Plan of Work


Первый milestone добавляет чистый ActorStatusState с ключом (SourceId,EffectId), конечными эффектами root/bind/stun и timed либо until-source-death poison/slow. Один EffectId повторно не продлевает эффект: туман использует тот же ключ, новый отдельный cast — новый EffectId. Для памяти устанавливается конечный предел ключей на encounter, отказ явный. ReleaseSource снимает только until-source poison/slow указанного источника; другие эффекты и другие источники сохраняются. Reset на переход/disable/смерть очищает всё. Poison использует явно переданную величину/интервал и тот же DamagePacket, без нового damage type или баланса nurse.

Второй milestone добавляет ActorStatusController и PlayerDodgeController, интегрирует gate в ActorMotor2D/WeaponController и добавляет последний optional аргумент PlayerCommand.dodgePressed=false. Motor — единственный писатель Rigidbody velocity. Status таймеры идут scaled time; dodge/knockback шаги через FixedUpdate. Контроллер кувырка подключается opt-in, только тестами и позднее У2; prefab не меняется. Старый Stun теперь блокирует атаку даже без компонента dodge. SetControlsEnabled(false), OnDisable, смерть и RestoreForEncounter сбрасывают transient-состояние; Time.timeScale=0 замораживает его. PauseRequested проходит независимо от запретов.

Третий milestone добавляет EditMode/PlayMode содержательные проверки. Старый HazardBossTests должен проверять не только неподвижность, но и запрет реального выстрела во время stun, затем восстановление. Это сознательная смена контракта S4-01, не ослабление Foundation assertions. Handoff перечисляет API, meta, базу, точные результаты/ограничения.

## Concrete Steps


Все git/code команды выполняются из Game/TestResults/s405a0-worktree. Проверка основы: git rev-parse HEAD origin/main; git status --short; git diff --cached. Тесты после разрешения внешнего executable:

    /Applications/Unity/Hub/Editor/6000.3.28f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath "$PWD/Game" -runTests -testPlatform EditMode -testResults "$PWD/Game/TestResults/s405a0-validation/EditMode.xml" -logFile "$PWD/Game/TestResults/s405a0-validation/EditMode.log"

Аналогично PlayMode с отдельными именами, без -quit (runner завершает процесс сам). Нельзя запускать одновременно на одной копии. Открытый Editor пользователя и lock не трогать. После завершения разобрать test-run/test-case в XML, сохранить exit code, проверить failed/skipped/inconclusive. До запуска результаты были NOT RUN; теперь реальные XML/counts приведены в Artifacts и handoff. Для нового запуска выбирать новую папку/суффикс, не затирать сохранённые отчёты.

## Validation and Acceptance


Проверить одноимённые эффекты двух SourceId, разные EffectId одного источника, повторный туман без бесконечного stun, source death только своей poison/slow; expiry и reset. При root герой стреляет, при bind/stun — нет, switch/PauseRequested доступны. Poison действительно проходит DamagePacket и не блокирует ответ, slow уменьшает скорость. Тестовый edge вызывает один dodge; zero выбирает fallback направления; false не меняет ресурсы/cooldown. Проверить старт у стены, остановку во время движения, diagonal/knockback, pause freeze, death/unload/reset, атаки во время dodge и отсутствие dodge immunity. Полные старые Edit/Play наборы не ослаблять. Проверки устройств/ручного ввода/новой UI кнопки/мини-боссов NOT RUN и вне шага.

## Idempotence and Recovery


Не переключать/не stash/не clean грязные соседние worktree. Изменять только принадлежащие роли1 файлы и тесты; новые файлы с уникальными .meta. Повторный прогон использует новую папку/суффикс и не затирает XML ошибки. Library — локальный ignored cache; прежние результаты сохраняются. Первоначальное поручение A0 ограничивалось локальной реализацией и проверкой.

## Interfaces and Dependencies


Game.Contracts.IActorActionGate и IDodgeCommandSink сохраняются. ActorMotor2D.Permissions объединяет status, controls/death/pause и knockback/dodge. ActorStatusController предоставляет bool TryApply(ActorStatusEffect effect), int ReleaseSource(int sourceId), bool Contains(int sourceId,string effectId), void ResetForEncounter(), read-only ActiveCount/MovementMultiplier/StunRemaining и Permissions только эффектов. Полный IActorActionGate реализует только motor. ActorStatusEffect(int sourceId,string effectId,ActorStatusKind kind,float duration,bool untilSourceDeath=false,float slowMultiplier=.5f,float poisonDamage=0,float tickInterval=1) неизменяем; ActorStatusKind={Root,Bind,Stun,Poison,Slow}. ActorStatusState — чистая модель с TryApply, Advance(deltaTime,Action<int,float> poisonTick=null), ReleaseSource, Contains, Reset, Remaining(kind), Permissions/MovementMultiplier/Count/RecordedKeys. Лимит256 ключей до Reset; повтор ключа и исчерпание дают false. Максимум8 poison ticks на эффект за Advance, остаток долга сохраняется; callback пользуется прежним DamagePacket. Legacy Stun хранит отдельное max(existing,seconds) в той же модели, не исчерпывает бюджет ключей; туману нужен TryApply. PlayerDodgeController.TryRequestDodge(Vector2) возвращает false при запрете; диагностические IsDodging/Direction/CooldownRemaining доступны только для чтения. PlayerCommand(Vector2 move, bool attackHeld=false, bool switchPressed=false, bool interactPressed=false, bool pausePressed=false, bool dodgePressed=false) сохраняет старые вызовы.

## Surprises & Discoveries


Старые сцены не содержат status/dodge компонентов. Для общего исправления Stun motor должен дать status-граничный путь без изменения prefab; dodge остаётся opt-in. До отдельного ответа Unity был NOT RUN; затем пользователь явно разрешил S4-05A0. Разрешение прошлых S4-03A/B не переиспользовалось. При ревью обнаружено, что прежний SetControlsEnabled(true) очищал stun/knockback: сохранение такой семантики дало бы повторным enable обходить новый cooldown. Теперь только false/disable/death/reset очищают transient-состояние. CurrentCommand сохраняет edge запроса после принятия dodge, хотя CanDodge сразу становится false. Оба риска отражены в новых PlayMode-тестах и прошли в полном наборе120/120.

## Decision Log

19 ноября 2026. Параметры реализации мини-боссов. Это первые игровые значения для проверки баланса. Единица равна клетке.

Гранатомётчик 200 HP, порог 100. Бросок раз в 2,5 с, дальность 8, скорость 9. Граната останавливается у World или в намеченной точке. Фитиль 1 с от остановки. Урон 12 в радиусе 1,6 и пятно радиуса 1,15 на 3 с, урон 3 каждую секунду. Большая 22, радиусы 2,8 и 2, пятно 4 с. Три заранее созданных маркера на владельца. Порог пытается материализовать до 4 плановых слизней в безопасных точках и выпускает большую гранату с приоритетом. Слизень 12 HP, прыжки на 1,4 клетки, плевок 3 из пула 3. Призванные дети обязательны, неиспользованные слоты закрываются явно, живые дети остаются после смерти родителя. Гранаты/пятна принадлежат владельцу и прекращаются при его смерти/disable.

Щитоносец 300 HP, порог 100. Поворот 65°/с, фронтальный сектор ±90°, известный удар от героя спереди получает 35% урона. Дробовик 5 трасс в ±24°, до 5 клеток, совокупный урон 2–4 за попавшую трассу по расстоянию. Один DamagePacket сохраняет защиту от повторного попадания. Обычный темп 1,25 с, щит на 1 клетку наносит 8 и толкает на 1. На пороге предупреждение 0,6 с, таран шириной 3 со скоростью 10 до World (не более 4 с), урон 16, один толчок 4,5 и stun 1 с. Затем 3 с ускорения и серия из 5 попыток выстрела с интервалом 0,4 с. Выстрел расходуется при видимой фронтальной цели, невыполненные попытки не переносятся в обычный режим.

Медсестра 120 HP, порог 60. Розовый снаряд до 5 клеток, скорость 11, короткое предупреждение 0,25 с. Прямое попадание вяжет на 3 с, шприц даёт poison 2/с и slow 0,75 на 4 с. Телепорт требует свободного collider и линии подхода. Царапание 1,5 с, радиус 1,5, урон 4 с интервалом 0,5 с. Пороговая форма 6 с, скорость 7. В пределах 1 клетки при видимости выходит из формы и выпускает туман. Stun 2 с, poison 2/с и множитель скорости 0,65 до смерти её источника. Индивидуальные SourceId не смешиваются.

Ресурсы. Предел резерва нового пополнения 96. Награды обычных комнат по порядку: +6 в резерв, +10 HP, +5 брони. Финальная площадка содержит все три. E забирает ближайший полезный предмет до 1,5 клетки, остаток частично принятого предмета остаётся на полу, магазин не меняется. Один набор на пару RunId/RoomId, нулевой эффект не удаляет предмет. Для игры 1-1 применяются исходные 8+16 и нож. После завершения попытки ресурсы остаются только в её объектах. Целевые XML/меры игрового баланса записать после реализации.


18 ноября 2026, У1: отдельная ветка от fetched main, потому что A/B ещё локальны и не должны незаметно попасть в передачу движения. Предложенная нулевая цена/нулевая неуязвимость ограничивает изменение экономики/урона раннего прототипа; финальный баланс требует отдельного решения. Смена stun задокументирована до кода по принятому S4-01.

## Outcomes & Retrospective


S4-05A0 проверен автоматическими Mac Editor-тестами на отдельной основе7cb1623. Полные EditMode118/118 и PlayMode120/120 прошли с exit0, failed/skipped/inconclusive0; compile PASS. Прежние108/106 cases сохранены, новые10/14 включены в итог, focused не прибавляются. Ошибка5 выстрелов в первом focused оказалась неполной тестовой заготовкой; реальный projectile template подключён без правки production или ослабления assertions. Первый failed XML и оба последующих успешных набора сохранены.

Передача S4_05_MOVEMENT_STATUS_HANDOFF содержит API,19 относящихся файлов,15 source/test/meta fingerprint, точные XML/hashes и ограничения. Параметры прототипа явно подтверждены пользователем. Новый Git-коммит не создавался, HEAD7cb1623; публикация не выполнялась. Сборки Players, Android, ручной бой и подключение кнопки NOT RUN. Следующий ограниченный шаг У2/S4-04C — существующий input/UI поверх готового DodgePressed/API, со своими тестами focus/edge/touch/переходов. Это не готовность всей Stage4 и не двухличная независимая приёмка.

## Artifacts and Notes


База 7cb16232d21b59c333de6e043c7dc611c39809c2. Папка новых доказательств Game/TestResults/s405a0-validation/. Первая редакция 18 ноября 2026: записана перед кодом для ранней передачи У2.

Редакция 18 ноября 2026 после реализации: конкретизирован API/границы, отражены проверки статического diff и отсутствие Unity evidence. Параметры прототипа остаются явными предложениями, не выданными за согласованный баланс.

18 ноября 2026, продолжение после ответа пользователя: параметры прототипа подтверждены, разрешён Unity6000.3.28f1 на s405a0-worktree/Game; начаты реальные проверки. Предыдущие NOT RUN отражают состояние до этого разрешения.

В первом focused PlayMode5/15 сценариев не получили выстрел: новый fixture создавал ProjectilePool без prefab. Исправлена только Setup теста — подключён настоящий синтетический Projectile template; требования и production-код не менялись. FocusedPlay.xml/log (exit2) сохранены; FocusedPlay2.xml/log15/15, exit0.

Финальное обновление 18 ноября 2026: реальные проверки вместо прежнего NOT RUN. Папка Game/TestResults/s405a0-validation: FocusedEdit10/10 exit0; FocusedPlay10/15 exit2; FocusedPlay2 15/15 exit0; EditMode118/118 exit0 duration.795056s; PlayMode120/120 exit0 duration160.9380556s. validated-source-manifest.sha256 hash0efff88d311865214b3d411d961ffd87fba4c143dda58d76336d0a8297b6d0e1. История до разрешения/ошибки сохранена, общий статус обновлён новой записью У1.

Редакция 19 ноября 2026. Подготовлена ветка feat/stage4-movement-handoff от 843d279 с S4-03A/B и побайтово перенесёнными проверенными файлами A0. История предыдущих результатов сохранена. Результаты 118/120 подтверждают версию A0, прогон совмещённой ветки NOT RUN.
