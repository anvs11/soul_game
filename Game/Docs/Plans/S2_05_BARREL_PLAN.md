# S2-05 — бочка, толчок и оглушение


ExecPlans по .agent/PLANS.md, 15 ноября 2026, база6b08b4a. Пользователь одобрил результат S2-04, поручил оба блока и подтвердил закрытый Unity. Через уточнение выбрал три стадии полицейского со щитоносцами вместо скопированного старого двухфазного запроса. Этот документ задаёт общую ориентацию, ниже два отдельных milestone, передача каждого отдельная.

## Purpose / Big Picture


Игрок взрывает отдельную бочку настоящим оружием, получает урон, отбрасывание и оглушение движения2с со звёздочками, может защититься стеной. Этот ExecPlan ограничен S2-05, соседний S2-06 ведётся в Game/Docs/Plans/S2_06_POLICE_BOSS_PLAN.md. Общий пакет создания ресурсов исполняет совместный запрос пользователя.

## Progress


- [x] Прочитаны S2-04 и будущие требования, результат S2-04 одобрен, три стадии подтверждены пользователем.
- [x] S2-05 компоненты, prefab, отдельная preview и содержательные тесты выполнены.
- [x] EditMode34/34, PlayMode65/65 и дополнительный1/1, XML, две передачи и статус ROLE_1 готовы.
- [ ] Ручной просмотр новых preview, независимое ревью и Android остаются открытыми до фактической проверки.

## Surprises & Discoveries


Для движущихся врагов нужна поздняя физическая обработка толчка, иначе AI перезаписывает скорость. Новый EnemyKnockback.Begin(direction,distance,duration) с DefaultExecutionOrder100 применяется после AI; он не требует изменения AI/prefab, добавляется по попаданию. Общий ActorMotor2D получает Stun(seconds), StunRemaining и маску добровольного движения, собственный толчок по-прежнему имеет приоритет. Список общих изменений заранее зафиксирован — Actors/ActorMotor2D.cs и новые Actors/EnemyKnockback.cs, StunStars.cs. Контракт/Core/сцены У2 не затрагиваются.

## Decision Log


15 ноября — бочка HP1, radius3.5, damage50, knockback3 за.2с, stun2с от принятого попадания. За стеной/за радиусом эффекта нет, дедупликация DamageReceiver, другие бочки исключены. Убитые получают принятую смерть, принудительное перемещение применяется к выжившим. Оглушение блокирует Move, атака/пауза доступны. Повторное оглушение продлевает остаток до max, время scaled, смерть/disable очищают. Визуализация собственными процедурными звёздами и кольцом взрыва.

15 ноября — полицейский HP600, пороги400/200 включительно, переходы монотонны и учитывают перескок обоих порогов. Фаза1 кувырок+обоймы, фаза2 добавляет2 щитоносцев, фаза3 до4 и три оборота с огнём. Лимит живых призванных4, щит15 фиксирован от исходных30 брони. Базовая броня щита действует как DamageReceiver.Armor со всех сторон, фронтальная геометрия пока визуальная. HPщитоносца30, скорость4, подъём.8с. Каждый щитоносец представлен собственным IEnemyTarget, базовый SandboxEnemy внутри инициализируется с null registry, чтобы не было двойной регистрации.

15 ноября — босс root scale1, силуэт3.2×4 (высота5×героя), collider radius2, два пистолета. Windup.7, RollSpeed10/RollDuration.65, контактный урон20 и толчок2 один раз. После кувырка по8 патронов из каждого пистолета, интервалпар.12, reload2с. На фазе3 SpinDuration3с, ровно3 оборота, парапуль каждые.08с. BulletSpeed7/lifetime4/radius.12/damage10, собственный пул128. Таймеры scaled. Направление кувырка фиксировано при подготовке. Текущая атака завершается в своём зафиксированном режиме, новые HP-стадии определяют следующий кувырок. Смерть/disable/смена цели очищают атаки, пули и миньонов. HP при повторном включении не сбрасывается.

## Outcomes & Retrospective


Код и автоматические проверки выполнены. Две отдельные передачи фиксируют фактическую готовность, ручная приёмка остаётся открытой. Механики boss/userизменение приоритетны над старой формулировкой «без призыва». Основной маршрут S2-07 остаётся следующей интеграцией.

## Context and Orientation


DamageReceiver выдаёт DamageApplied, Projectile/MeleeAttack наносят DamagePacket. Barrel использует эти точки без IEnemyTarget. TargetRegistry/AutoAimSelector уже обобщены на IEnemyTarget. SandboxEnemy может обслуживать внутреннее движение щитоносца с registry=null. ProjectilePool позволяет ограничение и очистку при выключении. ActorMotor2D уже умеет контролируемый толчок со стенами. Новые компоненты используют Rigidbody2D/World и штатную UI/ввод героя.

## Plan of Work


Milestone S2-05 добавляет ExplosiveBarrel, EnemyKnockback и StunStars, расширяет ActorMotor2D. По принятой смерти бочки один взрыв, OverlapCircle собирает receivers, World raycast проверяет стену, пакет нейтрален к фракциям, другие бочки пропускаются. Эффекткольцо виден. Новый prefab с .meta и отдельная BarrelPreview с настоящим героем/зомби создаются через Unity.

Отдельный ExecPlan S2_06_POLICE_BOSS_PLAN.md описывает связанный босcовый блок. Здесь реализуются и проверяются бочка, толчок и оглушение.

## Concrete Steps


Из корня soul_game скрыто запускать разрешённый Unity.exe C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe. Assets через Editor-команду -batchmode -quit -projectPath <root>/Game -executeMethod Game.Editor.HazardBossAssets.Create -logFile <новаяпапка>/create.log. Тесты отдельно -batchmode -nographics -runTests -testPlatform EditMode и PlayMode с projectPath и новыми XML/логами. Диагностические сцены в Build Settings не добавлять. Локальный коммит явными путями после diff, push отдельно.

## Validation and Acceptance


Бочка проверяется на нескольких целях/нескольких collider, повторном попадании, стене/радиусе, исключении цепи/счётчика, паузе/оглушении/смерти/выгрузке. Звёзды исчезают после2 scaled seconds и при смерти/disable. Босс проверяется на точных400/200, одном ударе через оба порога, однократных событиях/смерти, pause/disable/repeat, ограничении пула и живых миньонов, фиксированном щите, кувырке со стеной и очистке. Итоговые discovered/executed/passed/failed/skipped берутся из XML, нулевые counts не принимаются.

## Idempotence and Recovery


Создание только новых assets, повтор при частичном результате использует сохранённые definitions и не заменяет прежние prefab. Ошибки/логи остаются. Не удалять Library/lock/историю. При смерти или выгрузке принадлежащие объекты немедленно отключаются и уничтожаются, таймеры/подписки очищаются.

## Artifacts and Notes


Итоги S2_05_HANDOFF.md и S2_06_HANDOFF.md, новые отдельные ExecPlan-копии этого документа с обозначением milestone, ROLE_1_STATUS. Независимое ревью У1 и устройство Android не подтверждены. User-defined future reference FUTURE_POLICE_BOSS_AND_STUN.md сохраняется с выбранными здесь конкретными параметрами.

## Interfaces and Dependencies


ActorMotor2D.Stun(float), EnemyKnockback.Begin(Vector2,float,float), ExplosiveBarrel.Configure(radius,damage,knockback,stun,worldMask), PoliceBoss.Configure(definition,pool,minionPrefab,worldMask), Initialize(registry,player), ShieldMinion.Initialize(registry,player). Новые типы целей реализуют существующий IEnemyTarget. World8, projectilemaskAll, без сторонних пакетов.

## Revision Note


15 ноября — исходный план после явного выбора трёх стадий пользователем. Численные параметры выбраны исполнителем для проверяемого прототипа и вынесены в definition.


## Итоговые доказательства

Windows Unity Editor6000.3.28f1. Полный финальный набор EditMode discovered/executed/passed=34/34/34 и PlayMode=65/65/65, failed/skipped/inconclusive=0, exit0. XML Game/TestResults/s20506-final-6cb97038288144d0a56ab4ebca5deabb/EditMode.xml и PlayMode.xml. Ресурсы созданы успешно, create.log в той же папке. После добавления одного нового теста настоящей пули отдельно выполнен filtered PlayMode discovered/executed/passed=1/1/1, failed/skipped=0, exit0, XML Game/TestResults/s205-projectile-68b88cd2e07c42d38f14abb71c1cc117/PlayMode.xml. Таким образом проверены66 различных PlayMode-сценариев в двух XML, это не один XML с total66. Исходный успешный набор32/63 и первичное создание сохранены в s20506-verify-1afe995348ea437c96c6466526d2ee06 и s20506-832da8e266fa4a2492a99ca691a94a80. Ручные новые preview/Android/standalone ещё не проверены.

Подготовлены визуальные сигналы кувырка и подъёма щитоносцев, их HP/броня фиксированы. Общие старые сцены/Core/prefab/контракт не имеют diff. Смерть/выгрузка очищают принадлежащие пули/миньонов, разгрузка не считается Died. Локальная фиксация выполняется только по .agent/S20506_COMMIT_PATHS.txt, временные копии/бинарники/XML не входят в Git. Номер коммита проверяется git log -1. Новых запусков на Android/standalone нет.

Revision15ноября — оба блока реализованы, результат проверок записан, геометрия полицейского сопоставлена масштабу.8 героя, уточнение пользователя про3стадии выполнено.
