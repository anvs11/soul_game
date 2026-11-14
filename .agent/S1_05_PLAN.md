# Связать настоящий бой с основным циклом


ExecPlan по .agent/PLANS.md. Корень soul_game, Unity-проект Game/. Сохранять изменения предыдущих задач. Пользователь разрешил точечное изменение CombatSandbox; prefab У1 и контракт v0.1 не заменять.

## Purpose / Big Picture


Из Bootstrap игрок попадает в дом, начинает бой с тремя врагами, после зачистки идёт к выходу и нажимает E. Смерть или победа показываются отдельным экраном результата перед возвращением к стартовому экрану дома. Новая попытка создаёт новые объекты. Генерация, награды, сохранения и мобильные элементы не добавляются.

## Progress


- [x] Прочитаны текущие Core/SceneFlow, контракт и боевые компоненты, рабочие изменения сохранены.
- [x] Реализованы ArenaController и настройка CombatSandbox без замены prefab.
- [x] Результат показан на отдельном экране Home перед возвращением к старту, тестовый экран снят только с основной арены.
- [x] Обновлены проверки из Bootstrap: десять попыток, закрытый/удалённый выход, зачистка, повторный выход, свежие ресурсы, реальная смерть и приоритет Defeat.
- [x] EditMode 24/24 и PlayMode 19/19, failed=0, skipped=0, оба exit=0.
- [ ] Визуальная приёмка человеком; Android и сборка основного боевого цикла относятся к последующим шагам.

## Surprises & Discoveries


SceneFlow ожидает один FlowScreen. ArenaController наследует его с отключённым тестовым полноэкранным представлением. RunCoordinator уже разрешает исходы в LateUpdate с приоритетом Defeat и грузит Home. Поэтому результат будет состоянием отображения Home: сначала итог и кнопка «В убежище», затем обычный старт. Это два экрана в одной сцене, без изменения контракта или вычисления исходов.

## Decision Log


Decision: сохранить RunCoordinator и его приоритеты. Rationale: добавить явный экран результата в HomeScreen вместо изменения состояния Core и лишней миграции. Date/Author: 2026-11-14, роль 2.

Decision: уникальные смерти считать HashSet<IDefeatable>, проверять расстояние до выхода в момент Interact. Rationale: выключение объекта не является смертью, повторное событие не открывает дверь преждевременно. Date/Author: 2026-11-14, роль 2.

## Outcomes & Retrospective


Интеграция выполнена и проверена через Bootstrap. Новые доказательства: Game/TestResults/s105-final-51781108e3d54dfb9085d25d10cd0cba/EditMode.xml и PlayMode.xml. Оба прогона успешны, 24 и 19 тестов. Предыдущий s105-b890bb31aa65470d807ea33a9753a0f4 содержит успешную запись сцены и первый неуспешный PlayMode: устранена NullReference при выборе отсутствующей кнопки арены в FlowScreen.Bind. Старые отчёты сохранены. Prefab У1, контракт и Packages не изменены. Результат и дом являются двумя экранами одной сцены Home. Пауза, мобильные кнопки, публикация и отдельная сборка здесь не реализованы. Визуально эту версию человек ещё не принимал.

## Context and Orientation


GameBootstrap запускается из Bootstrap и владеет RunCoordinator. SceneFlow грузит сцены и Bind одного FlowScreen. PlayerCombatFacade даёт InteractRequested и Died; SandboxEnemy реализует IDefeatable. Урон и оружие работают; выносливость с ножом восстанавливается после отдыха. HudPresenter уже читает настоящий снимок. CombatSandbox содержит только EmptyArenaScreen и подлежит разрешённой интеграции.

## Plan of Work


Добавить Scripts/Sandbox/ArenaController.cs, Scripts/Editor/IntegrateCombatScene.cs. Редакторский инструмент открывает существующую сцену, меняет только EmptyArenaScreen на ArenaController, добавляет свободную арену, реестр, игрока, три prefab врагов, HUD и выход. Сохраняет камеру, EventSystem, GUID сцены, Build Settings. FlowScreen получает OnBound и ShowView для привязки арены без фонового экрана. HomeScreen показывает результат перед обычным домом. HUD получает настраиваемую подсказку. Уничтожение сцены снимает подписки. Враги и игрок блокируются при смене фазы на ReturningHome.

## Concrete Steps


Рабочая папка корень soul_game. Запуск разрешённого Unity.exe: -batchmode -nographics -quit -projectPath <корень>/Game -executeMethod Game.Editor.IntegrateCombatScene.Apply -logFile <новый каталог>/scene.log. Затем последовательно -runTests -testPlatform EditMode и PlayMode с отдельными XML и логами, без -quit. Наличие открытого UnityLockfile требует закрытия пользовательского редактора, не удаления блокировки. Отдельная сборка не является частью этой задачи S1-07.

## Validation and Acceptance


Все игровые проверки запускаются из Bootstrap. До трёх смертей выход закрыт; после них E вдали не даёт победу, у выхода — даёт один Victory. Повторное событие смерти не меняет счётчик. Смерть героя в кадре запроса победы даёт Defeat. После результата кнопка «В убежище» открывает обычный старт; десять попыток имеют новые runId и новые объекты, старт 100/30/8/16. HUD сразу читает снимок. Исчезновение арены не вызывает победу. XML имеют ненулевой total, failed=0 и skipped=0. Визуальную и мобильную приёмку отмечать отдельно.

## Idempotence and Recovery


Изменения сцены разрешены пользователем. Инструмент повторно не добавляет объекты при наличии ArenaController. При непредвиденной структуре прекращает работу до сохранения. Не удалять файлы, prefab, .meta, Library, не применять reset/clean. Сохранить пользовательские изменения предыдущих задач.

## Artifacts and Notes


Новые результаты сохраняются в Game/TestResults/s105-<id>/. Исходники У1 и контракт должны совпасть с версией перед интеграцией. Личный план .agent/NEXT_ROLE_2.md обновить по факту. Основной путь остаётся Bootstrap, Home, CombatSandbox.

## Interfaces and Dependencies


ArenaController.Configure(PlayerCombatFacade, SandboxEnemy[], TargetRegistry, HudPresenter, Transform), TryExit(), RemainingEnemies и ExitPosition. FlowScreen.OnBound(RunCoordinator), ShowView, Refresh(). Новых пакетов нет. RunCoordinator.FinishRun и контракт v0.1 сохраняются.

## Revision Note


2026-11-14: план согласованной S1-05, результат реализуется как экран Home перед стартовым экраном убежища.

2026-11-14, проверка: обновлены Progress и Outcomes по реальным XML. FlowScreen допускает экран без PrimaryButton, очистка подписок явная. Встроенные Foundation-тесты адаптированы к настоящей арене, их сценарии сохранены и расширены. Полная Stage 1 не объявлена принятой.
