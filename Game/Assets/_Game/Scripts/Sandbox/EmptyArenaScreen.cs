using Game.Core;
using Game.UI;

namespace Game.Sandbox
{
    public sealed class EmptyArenaScreen : FlowScreen
    {
        protected override string Title => "Проверочная арена";
        protected override string Description => "Герой и враги пока не подключены.\nЭта сцена проверяет только переходы.";
        protected override string ButtonLabel => "Тестовый выход — вернуться домой";
        protected override bool CanAct(RunCoordinator value) => value.Phase == RunPhase.InArena;
        protected override void Act(RunCoordinator value) => value.FinishRun(RunOutcome.Abandoned);
        protected override string Message(RunCoordinator value) =>
            value.Phase == RunPhase.InArena ? "Выход не считается боевой победой." : "Возвращаемся домой…";
    }
}
