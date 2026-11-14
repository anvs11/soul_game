# Исправить реальные границы и завершить паузу и экранное управление


ExecPlan по PLANS.md, 2026-11-14. Корень soul_game, проект Game/. Рабочие изменения прошлых задач сохраняются. Пользователь запросил границы эксперимента и шаги 3–4 роли 2.

## Purpose / Big Picture


Стены удерживают героя и врагов, включая отбрасывания. Esc или экранная кнопка приостанавливают бой, меню позволяет продолжить или выйти домой. Экранный джойстик и кнопки используют существующий Player.inputactions, работают одновременно разными указателями. Итог уже реализован в Home — второй экран результата не создаётся.

## Progress


- [x] Проверены текущие исходники и сохранённая CombatSandbox. У BoxCollider2D стен размер .0001×.0001 вместо 1×1; обнаружена причина дефекта реальной сцены.
- [x] Исправлены четыре стены основной и preview-сцены и генераторы, добавлен ArenaCameraFit; проверка сохранённой сцены проходит.
- [x] Реализованы пауза, продолжение, выход домой и очистка виртуальных нажатий при потере фокуса.
- [x] OnScreenStick/OnScreenButton используют существующие leftStick, buttonSouth/buttonNorth/buttonWest/start. Один EventSystem, Safe Area, UI Submit отключён.
- [x] Из Bootstrap проверены реальные стены, движение/отбрасывание/враг, пауза, два указателя, экранная пауза, фокус, смерть, очистка пуль/устройства и прежние десять циклов.
- [x] EditMode 24/24; итоговый PlayMode 26/26, failed=0, skipped=0, exit=0. Отчёт записан.
- [ ] Ручной просмотр Windows UI и физическая проверка Android. Телефон не предоставлен.

## Surprises & Discoveries


Видимый SpriteRenderer стены имеет размер 1, а физический BoxCollider2D — .0001. Коллайдер добавлялся до назначения спрайта и получал почти нулевую форму. Прежний тест создавал стену вручную с правильным размером, поэтому проходил и не покрывал дефект сохранённой сцены. Новый регрессионный тест обязан загружать реальный игровой путь.

## Decision Log


Decision: задавать форму collider явно, сохранить физические стены вместо невидимого телепортационного зажима координат. Rationale: одинаковые преграды для движения, выстрелов и ударов. Date/Author: 2026-11-14, роль 2.

Decision: пауза через Time.timeScale, не выключение PlayerCombatFacade или сброс состояния оружия. Rationale: сохраняются перезарядка, отбрасывание и таймеры атак, PauseRequested продолжает поступать. Date/Author: 2026-11-14, роль 2.

Decision: выключить навигационные события UI Submit у текущего EventSystem и дать явные pointer-кнопки. Rationale: Space/buttonSouth не должны выбирать пункт меню. Date/Author: 2026-11-14, роль 2.

## Outcomes & Retrospective


Работа выполнена в коде и проверена в Windows Unity Editor 6000.3.28f1. Game/TestResults/controls-verify-0179933aa92a4a6882d9a842775fc9bc/EditMode.xml — 24/24. Последний полный PlayMode: Game/TestResults/controls-complete-0f7bbf82edf94de8b06912a319c19a63/PlayMode.xml — 26/26. Все failed/skipped=0, процессы exit=0. Ранний расширенный сценарий вводил паузу до утверждения о продолжающемся движении джойстика и закономерно получал ноль: пауза очищает нажатия. Порядок теста исправлен: сначала независимость двух указателей, затем очистка в паузе/при фокусе. Рабочую очистку ввода не отключали. Прежние XML сохранены. Отдельная Windows-сборка и Android на устройстве в этом шаге не запускались; нажатия моделировались PointerEventData, а не физическим сенсором. Сцены и код готовы к ручной оценке.

## Context and Orientation


ArenaController уже соединяет бой с RunCoordinator, приоритет Defeat и результат работают. ActorMotor2D продолжает Update при нулевом времени, WeaponController уже отвергает нулевой deltaTime. PlayerCombatFacade пропускает PauseRequested даже при заблокированных атаках. GameBootstrap сбрасывает timeScale при запуске/закрытии, SceneFlow — при переходе. Input Actions имеют Move, Attack, SwitchWeapon, Interact, Pause с клавиатурой и Gamepad. Контракт v0.1 не меняется.

## Plan of Work


Добавить редакторскую BoundaryFix.Apply: открыть CombatSandbox и Stage1ArenaPreview, найти North/South/East/West, задать collider size=1, offset=0, trigger=false и scale.z=1, сохранить GUID и остальное содержимое. Исправить генераторы сцен, чтобы ошибка не повторилась. Камера подстраивает размер под aspect, чтобы рамка целиком помещалась в Game.

Добавить BattleControls с Canvas/Safe Area: левый OnScreenStick, Attack/Switch/Interact/Pause, отдельное меню с Resume/Home. Pointer-элементы не закрывают весь экран лучевым перехватом, меню блокирует ввод только во время паузы. ArenaController управляет TogglePause/Resume/Leave, очищает виртуальные нажатия, подписывается на PauseRequested, при потере фокуса ставит паузу. При закрытии снимает подписки и возвращает timeScale=1. ActorMotor2D при паузе публикует только запрос паузы и не сбрасывает knockback. Добавить regression PlayMode-тесты реальных стен, паузы, двух указателей и снятия нажатий.

## Concrete Steps


В корне soul_game запустить разрешённый Unity.exe: -batchmode -nographics -quit -projectPath <root>/Game -executeMethod Game.Editor.BoundaryFix.Apply -logFile <новый каталог>/fix.log. Затем Test Runner EditMode и PlayMode с XML и журналами в Game/TestResults/boundary-controls-<id>/. Без -quit для тестов. При UnityLockfile дождаться закрытия пользователем, не удалять блокировку. Новая зависимость/новый inputactions не создаются.

## Validation and Acceptance


В загруженной из Bootstrap арене проверить bounds четырёх стен и обычное движение/отбрасывание ко всем краям; враг также остаётся внутри. При паузе не меняются здоровье, положение, патроны и боевые таймеры. Resume сохраняет попытку; Leave даёт один Abandoned и результат, следующий старт timeScale=1. Смерть во время запроса выхода выигрывает по существующему приоритету. При двух указателях движение и атака одновременно видны в реальном вводе; отпускание одного не выключает другое. После потери фокуса/возврата нет зажатой атаки. UI Submit не запускает Resume или Leave. Исходные десять циклов и боевые проверки проходят. Android физически не проверен без устройства.

## Idempotence and Recovery


Исправление размеров идемпотентно; не заменять сцены и prefab целиком, не назначать GUID вручную, не удалять Library. Менять только разрешённые исходники и целевые стены. Тесты создают и уничтожают свои runtime-объекты. Отчёты каждого прогона новые.

## Artifacts and Notes


Большая карта, ограниченное обнаружение врагов, укрытия, машины и бочки с уроном/отбрасыванием всем сторонам сохранены как последующие задачи. Сейчас реализуются только исправление границ, пауза и управление; эти идеи не объявляются готовыми.

## Interfaces and Dependencies


BattleControls.Initialize(System.Action pause, System.Action resume, System.Action leave), ClearHeld(), SetPaused(bool). ArenaController.TogglePause(), Resume(), Leave(), IsPaused. Используются существующие Unity UI и Input System OnScreen-компоненты, один EventSystem. Отдельный ArenaCameraFit подстраивает ортографическую камеру под aspect без изменения размера арены.

## Revision Note


2026-11-14: план по реальной причине выхода за пределы и согласованным шагам 3–4. Нужен тест сохранённой сцены, а не только искусственной стенки.

2026-11-14, итог: размеры collider исправлены через Unity, реализованы BattleControls/ArenaCameraFit/BoundaryFix и пауза ArenaController. Результат S1-05 сохранён. Добавлены четыре BoundaryPauseTouchTests, обновлены Progress/Outcomes по 24+26 успешным тестам. Мобильная приёмка оставлена открытой.
