# S4-05A0 — передача базовых состояний и движения У1 → У2

18 ноября 2026. Продолжение после явного подтверждения параметров прототипа и разрешения Unity для S4-05A0. **Компиляция и полные наборы прошли: EditMode118/118, PlayMode120/120, оба exit0.** Это собственный автоматический Mac Editor-прогон, не ручное прохождение и не независимая приёмка двух людей. Результаты других шагов не подставляются вместо текущих прогонов.

## Основа и идентификация

Unity-проверка выполнена 18 ноября 2026 на основе **7cb16232d21b59c333de6e043c7dc611c39809c2** в `Game/TestResults/s405a0-worktree`. Для передачи У2 19 ноября собрана отдельная ветка **feat/stage4-movement-handoff** от **843d2795af2e490dc0f7f6531b6eb85d53f8dcb1**, где уже есть EncounterPlanner. Все 15 файлов кода, тестов и meta перенесены с проверенной копии побайтово. Между двумя основами изменились планировщик, его тесты и документация. Повторный Unity-прогон всей объединённой основы пока не проводился.

Исходные материалы S4-01 и S4-04B прочитаны на проверявшейся основе. В полученной истории файл CITY_BOSSES_AND_DODGE_CONCEPT отсутствует.

SHA256 манифеста 15 изменённых/новых файлов кода, тестов и meta: **0efff88d311865214b3d411d961ffd87fba4c143dda58d76336d0a8297b6d0e1**. Это fingerprint файлов, **не Git commit hash** и не доказательство компиляции. Проверенный манифест — Game/TestResults/s405a0-validation/validated-source-manifest.sha256; исходный source-manifest.sha256 сохранён как история до исправления fixture. При последующих изменениях пересчитать его; весь набор ниже нужен вместе.

## Состав файлов

Все пути ниже относительно рабочей копии. Новые runtime-файлы: Game/Assets/_Game/Scripts/Actors/ActorStatusState.cs, ActorStatusController.cs, PlayerDodgeController.cs и каждый соседний .cs.meta. Изменены Actors/ActorMotor2D.cs, Actors/PlayerCombatFacade.cs, Combat/WeaponController.cs и Input/PlayerCommand.cs. Их существующие meta сохранены. Новые тесты Game/Assets/_Game/Tests/EditMode/ActorStatusTests.cs и PlayMode/MovementStatusTests.cs с .meta; точечно дополнен PlayMode/HazardBossTests.cs. Документы: Game/Docs/Plans/S4_COMBAT_PLAN.md, добавление в Game/Docs/S4_01_CONTRACT.md, этот handoff и только новая запись Coordination/ROLE_1_STATUS.md.

| Новый .meta | GUID |
|---|---|
| Actors/ActorStatusState.cs.meta | 2cfdda74be314c80b9ca508fade5cd6f |
| Actors/ActorStatusController.cs.meta | f295262b9c564f8cbded9700405daf97 |
| Actors/PlayerDodgeController.cs.meta | 1527b4b6475d47e29e87a2e567ac527b |
| Tests/EditMode/ActorStatusTests.cs.meta | 98741c15d7ec4a2090394cd43b741fb1 |
| Tests/PlayMode/MovementStatusTests.cs.meta | 2493a7c345ef47f993349bbbf86b0a37 |

Scenes/prefab/карта/Core/Generation/LocalInputSource/Player.inputactions/UI не изменены. DamagePacket.cs, DamageReceiver/HealthState, CombatContracts/Stage4CombatContracts и ресурсы/баланс оружия не изменены. История У2 не переписывается. XML/Library/Builds/машинные файлы не входят в набор.

## API для подключения У2

Существующий PlayerCommand теперь имеет сигнатуру:

    PlayerCommand(Vector2 move, bool attackHeld=false, bool switchPressed=false,
        bool interactPressed=false, bool pausePressed=false, bool dodgePressed=false)
    bool DodgePressed { get; }

Старые позиционные вызовы совместимы. У2 добавляет edge через WasPressedThisFrame своего существующего action map. Сам motor обрабатывает DodgePressed: второй вызов TryRequestDodge из UI поверх той же команды не нужен. Удержание кнопки не должно выдавать новый true каждый кадр. Проверка удержания/focus/touch принадлежит S4-04C, в A0 используются только синтетические команды.

Единственный полный gate — ActorMotor2D : IActorActionGate. `Permissions` включает controls/death/timeScale/status/knockback/dodge/cooldown; `CombatActive` обозначает controls+enabled+alive, пауза проверяется отдельно. PausePressed всегда передаётся в CommandSampled и существующий PlayerCombatFacade.PauseRequested, даже при запрете боя. WeaponController повторно сверяет gate перед выстрелом/ударом/сменой. Существующие таймеры reload/выносливости идут по прежним правилам при активном герое; запрещённая атака отфильтрована и не считается удержанием атаки для knife recovery.

PlayerDodgeController : IDodgeCommandSink — opt-in компонент на том же объекте, что ActorMotor2D/Rigidbody2D и collider. У2 добавляет его при подключении, prefab в A0 не меняется. API `bool TryRequestDodge(Vector2 direction)`, read-only `IsDodging`, `Direction`, `CooldownRemaining`, `void ResetForEncounter()`. Motor пишет единственную физическую скорость в FixedUpdate; Rigidbody2D.Cast по реальной форме останавливает движение перед нетриггерными стенами/дверями, как прежний knockback, игнорируя ActorIdentity. Это не проход сквозь врагов и не гарантия отсутствия телесных столкновений: их сохраняет обычная physics matrix. Герой требует настроенного физического collider.

Нулевое направление использует последнее принятое ненулевое движение → последнее aim оружия → down. NaN/Infinity/переполнение величины отвергаются. Полностью закрытый старт возвращает false без cooldown. Принятый dodge при встрече стены прекращается, уже использованный cooldown сохраняется. Root/bind/stun прерывают dodge. Knockback прерывает dodge и идёт до стены даже при stun/root/bind; атака при нём зависит от эффектов. Повторный SetControlsEnabled(true) не очищает эффекты/cooldown. SetControlsEnabled(false), motor.OnDisable, смерть через facade и успешный RestoreForEncounter сбрасывают transient-состояние; явный motor.ResetForEncounter доступен исполнителю перехода. При timeScale0 таймеры/движение замораживаются, dodge не принимается. Новый объект встречи начинает без прежних эффектов. Если У2 сохраняет того же героя между комнатами, он должен вызвать согласованный reset на границе encounter; карта здесь не переподключалась.

ActorStatusController автоматически добавляется motor в Awake, чтобы старый Stun получил общие restrictions без правки prefab. Его собственное Permissions — только набор эффектов, не полный IActorActionGate. API:

    bool TryApply(ActorStatusEffect effect)
    int ReleaseSource(int sourceId)
    bool Contains(int sourceId, string effectId)
    void ResetForEncounter()
    int ActiveCount; float MovementMultiplier; float StunRemaining

Чистые данные (namespace Game.Actors):

    enum ActorStatusKind { Root, Bind, Stun, Poison, Slow }
    ActorStatusEffect(int sourceId, string effectId, ActorStatusKind kind, float duration,
        bool untilSourceDeath=false, float slowMultiplier=.5f,
        float poisonDamage=0, float tickInterval=1)
    ActorStatusState.TryApply(effect), Advance(float deltaTime, Action<int,float> poisonTick=null),
        ReleaseSource(sourceId), Contains(sourceId,effectId), Reset(), Permissions,
        MovementMultiplier, Remaining(kind), Count, RecordedKeys

Конструктор отвергает пустые Id, неизвестный kind, нечисловые/неположительные duration, slow вне(0,1], отрицательный poisonDamage и interval<.02. UntilSourceDeath разрешён только poison/slow; положительный duration в этом режиме не отсчитывается. Составной ключ SourceId/EffectId означает один эффект одного применения. Два эффекта одного cast используют разные EffectId (например fog/7/stun и fog/7/slow). SourceId должен различать живые экземпляры, не переиспользоваться после возврата в пул внутри encounter. Один ключ не обновляется повторным callback даже после expiry. Ограничение256 ключей до Reset возвращает false, дальнейшее наложение нельзя считать успешным. Это конечная техническая защита; переполнение не должно незаметно трактоваться интегратором как применённый stun.

ReleaseSource снимает только UntilSourceDeath poison/slow указанного SourceId. Timed root/bind/stun сохраняют собственные сроки; другие источники не затрагиваются. У2/будущие боссы вызывают ReleaseSource из своей единственной привязки Died/cancel; новых мини-боссов/подписок на них здесь нет. Предпочтение slow — минимальный multiplier, без перемножения. Пример принятого root: new ActorStatusEffect(sourceId,"vines/17",Root,3); bind аналогично3; туман stun2 с одним стабильным EffectId на применение. Отдельный новый cast — новый Id.

Poison ничего не запрещает; явно заданные damage/interval производят Normal DamagePacket через существующий DamageReceiver. Значения poisonDamage=0/.5 slow — значения конструктора прототипа, не спецификация nurse. Сохраняются броня и hit protection: tick внутри protection может быть отвергнут, не переносится как гарантированный урон. За один Advance максимум8 ticks на эффект; долг после длинного кадра сохраняется на следующие Advance, пауза не создаёт ticks. Lethal tick может синхронно Reset из facade без продолжения собственного списка. `DamageApplied/Changed/Died` идут через прежний путь, новый канал урона не вводится.

## Permissions и предложения баланса

| Состояние | Движение | Атака | Кувырок | Switch | PauseRequested |
|---|---|---|---|---|---|
| Обычно / poison | да | да | готовый | да | да |
| Slow | минимальный множитель | да | готовый, скорость фиксирована | да | да |
| Root3 | нет | да | нет | да | да |
| Bind3 / stun2 | нет | нет | нет | да | да |
| Knockback | только толчок | если нет stun/bind | нет | да | да |
| Dodge | только кувырок | да | нет | да | да |
| Пауза / смерть / disabled controls | нет | нет | нет | нет | да |

**Подтверждённые пользователем параметры тестового прототипа**: скорость12 units/s, длительность.20 scaled s, свободная дальность2.4 units, cooldown.8 scaled s от старта, стоимость0, неуязвимость0; атака и смена оружия разрешены. Они записаны до кода, затем пользователь ответил «Использовать эти параметры прототипа». Constants PlayerDodgeController отражают подтверждённый тестовый срез, не окончательный баланс всей игры. Ресурсный API/новая цена/защита от урона не добавлялись. False не изменяет cooldown/ресурсы; бесплатный dodge работает и при нулевой stamina.

Старый ActorMotor2D.Stun(seconds) намеренно изменяет атаку по S4-01: раньше блокировал только перемещение, теперь блокирует и оружие. Он сохраняет max(existing,seconds) для отдельных legacy попаданий без расходования256 ключей; для тумана нужен TryApply со стабильным Id. HazardBossTests.BarrelDamagesMultipleReceiversOnceWithoutChainAndStunsHero дополнен проверками неизменности магазина во время stun и выстрела после expiry. Старые assertions урона/стены/перемещения/паузы/звёзд сохранены. Это изменение семантики Stage2 stun объявлено явно, не замаскировано новым балансом.

## Фактические проверки

Разрешённый Unity6000.3.28f1 (8f082d2fe476), macOS26.2 (25C56), arm64, batchmode/nographics, отдельный s405a0-worktree/Game. Открытый пользовательский Editor не завершался, lock не удалялся. Таблица содержит реальные данные XML и exit процессов; длительность — процедура тестирования, не измерение игрового FPS/телефона.

Все пути XML/полных логов относительно рабочей копии, папка **Game/TestResults/s405a0-validation/**:

| Прогон | Discovered / executed / passed | Failed / skipped / inconclusive | Exit | XML / полный лог | Duration s |
|---|---|---|---|---|---|
| Focused EditMode | 10 / 10 / 10 | 0 / 0 / 0 | 0 | FocusedEdit.xml / FocusedEdit.log | 0.2735036 |
| Первый focused PlayMode | 15 / 15 / 10 | 5 / 0 / 0 | 2 | FocusedPlay.xml / FocusedPlay.log | 4.3205667 |
| Исправленный focused PlayMode | 15 / 15 / 15 | 0 / 0 / 0 | 0 | FocusedPlay2.xml / FocusedPlay2.log | 5.7805854 |
| Полный EditMode | **118 / 118 / 118** | **0 / 0 / 0** | **0** | **EditMode.xml / EditMode.log** | 0.795056 |
| Полный PlayMode | **120 / 120 / 120** | **0 / 0 / 0** | **0** | **PlayMode.xml / PlayMode.log** | 160.9380556 |

118 =108 прежних +10 новых cases ActorStatusTests;120 =106 прежних +14 новых MovementStatusTests. Наборы старых fullname сверены: ни одного пропавшего старого теста. Из старых assertions только содержательно дополнен HazardBossTests, прежние сохранены. Focused результаты уже входят в полные и не суммируются с ними. Старые136/106 S4-03B относятся к другой ветке с planner/validator-тестами; не используются как показатели A0. Полный PlayMode включает прежние Stage1/Stage2/Stage3/Stage4 регрессии, но это не ручной бой и не Player.

История исправлений: первый focused PlayMode не получил выстрелы в5 новых случаях (DodgeAllowsSwitchAndDoesNotRequireStamina, EdgeCommandMovesExactlyOneFiniteDodgeAndAllowsAttackWithoutImmunity, NewRestrictionCancelsDodgeAndKnockbackRetainsWallLimitedPriority, PoisonAndSlowAllowResponseAndSourceRemovalIsIndependent, RootBindStunRejectWithoutSpendingAndPreservePauseAndSwitch). В Setup создан ProjectilePool без prefab, его штатный TrySpawn возвращал false и магазин оставался8 вместо7. Подключён настоящий синтетический Projectile template к pool.Configure; production-код и требования тестов не менялись. FocusedPlay2 и полные наборы прошли. Первый XML/лог сохранены; ошибка не скрыта как «всё с первого раза Passed».

Проверенные новые риски: permissions/expiry и root3/bind3/fog-stun2; разные SourceId и EffectId; повторный туман без продления; снятие только своего persistent poison/slow; ограниченный poison tick loop и синхронная смерть; invalid data/key budget; optional command. PlayMode реально проверил Rigidbody-движение, свободные2.4 units, zero/last-movement/aim, diagonal wall/blocked-start, attack/switch во время dodge и отсутствие invulnerability, root/bind/stun с отказом без цены/cooldown, knockback до стены с возможностью атаковать, slow с измеренной velocity/poison через DamagePacket, паузу/PauseRequested, смерть, настоящий SceneManager unload, disable/re-enable/RestoreForEncounter, нулевую stamina и невозможность повторным enable обойти cooldown. Старый barrel stun теперь проверен на запрет выстрела и восстановление после expiry.

SHA256 полного EditMode.xml: **29f7d0d520cd51946917c44c47d31f0cfb0df984c292f97b524d8bcf115b9a13**. SHA256 полного PlayMode.xml: **94b925b42073369facdcf0d29469f90e8e848985977f7dbeb94b532fbb830378**. Сводка всех пяти XML, их hashes, exit, counts и исполняемых source hashes — verified-summary.json; исходный NOT RUN сохранён отдельно в before-unity-summary.json. Манифест validated-source-manifest.sha256 содержит15 строк вида «SHA256 файловых байтов, два пробела, repository-relative path», отсортированных по пути, UTF8/LF. Его hash в начале handoff идентифицирует эту Mac-копию, не новый Git-коммит. После всех прогонов исходники повторно совпали с манифестом.

Статические проверки: git diff --check PASS; все новые .cs имеют .meta, GUID уникальны; DamagePacket/файлы У2/сцены/карта/Core/Generation/ROLE_2_STATUS не изменены. На момент исходной проверки индекс был пуст. Ранние правки из собственного code review (до первого прогона) убрали очистку cooldown при повторном enable и потерю edge из CurrentCommand; их поведение теперь покрыто новыми Passed тестами.

Ручной бой с обычным боезапасом, ощущение управления/сложность, Windows/Mac Players, Android/телефон, UI/два пальца/SafeArea/focus release, реальные новые мини-боссы/подборы — **NOT RUN**. Кувырок ещё не подключён к Player.inputactions/LocalInputSource/экранному вводу/prefab; поэтому на пользовательской карте его нельзя объявлять уже доступным. Reset на сохранённом герое между комнатами, подписки источников эффектов и интеграцию проверяет следующий исполнитель по API выше. S4-05A0 не завершает Stage4 и не утверждает баланс всей игры.

Команды повторения — S4_COMBAT_PLAN; выполнять из корня этого worktree и использовать новую папку/суффикс результатов, чтобы сохранить историю. Runner завершает процесс сам, -quit не добавлять. XML/логи/cache остаются локальными ignored артефактами, не частью передачи кода.

## Ограниченный следующий шаг

Для S4-04C взять ветку передачи, подключить edge DodgePressed через существующий action map, добавить PlayerDodgeController герою и проверить экранную кнопку, SafeArea и сброс ввода при потере фокуса. Параметры прототипа подтверждены; механику заново не копировать. Сводный прогон на ветке после подключения ввода будет результатом S4-04C. Исходный hash передачи можно получить через `git log -1 --format=%H -- Game/Docs/S4_05_MOVEMENT_STATUS_HANDOFF.md`.
