using Game.Core;
namespace Game.UI
{
    public sealed class HomeScreen : FlowScreen
    {
        public bool IsShowingResult { get; private set; }
        private RunOutcome? outcome;
        protected override void OnBound(RunCoordinator value) { outcome=value.LastOutcome; IsShowingResult=outcome.HasValue; }
        protected override string Title => IsShowingResult ? (outcome==RunOutcome.Victory ? "Победа" : outcome==RunOutcome.Defeat ? "Поражение" : "Вылазка завершена") : "Убежище";
        protected override string Description => IsShowingResult ? "Попытка завершена. Награды на этом этапе не начисляются." : "Зачистите арену, подойдите к выходу и нажмите E.";
        protected override string ButtonLabel => IsShowingResult ? "В убежище" : "Начать вылазку";
        protected override bool CanAct(RunCoordinator value) => value.Phase==RunPhase.Home;
        protected override void Act(RunCoordinator value)
        {
            if (IsShowingResult) { IsShowingResult=false; Refresh(); }
            else value.StartRun(RunRequest.Sandbox());
        }
        protected override string Message(RunCoordinator value) => value.Phase==RunPhase.Home ? "WASD · Space · Q · E" : "Переходим на арену…";
    }
}
