using Game.Core;
using UnityEngine;
namespace Game.UI
{
    public sealed class HomeScreen : FlowScreen
    {
        public bool IsShowingResult { get; private set; }
        private bool diagnostic;
        private RunOutcome? outcome;
        protected override void OnBound(RunCoordinator value)
        { outcome=value.LastOutcome; IsShowingResult=outcome.HasValue; diagnostic=value.CurrentRequest.IsStage2Diagnostic; }
        protected override string Title => IsShowingResult
            ? (diagnostic ? "Stage 2: проверка завершена" : outcome==RunOutcome.Victory ? "Победа" : outcome==RunOutcome.Defeat ? "Поражение" : "Вылазка завершена") : "Убежище";
        protected override string Description => IsShowingResult
            ? (diagnostic ? "Диагностическая арена, без босса и наград. Итог: " + OutcomeLabel : "Попытка завершена. Награды на этом этапе не начисляются.")
            : "Зачистите арену, подойдите к выходу и нажмите E.";
        private string OutcomeLabel => outcome==RunOutcome.Victory ? "зачистка" : outcome==RunOutcome.Defeat ? "гибель героя" : "выход домой";
        protected override string ButtonLabel => IsShowingResult ? "В убежище" : "Начать вылазку";
        protected override bool CanAct(RunCoordinator value) => value.Phase==RunPhase.Home;
        protected override void Act(RunCoordinator value)
        {
            if (IsShowingResult) { IsShowingResult=false; Refresh(); }
            else value.StartRun(RunRequest.Sandbox());
        }
        protected override string SecondaryLabel => Application.CanStreamedLevelBeLoaded(ScenePaths.Stage2CombatSandbox)
            ? "Stage 2: проверка обнаружения" : null;
        protected override bool ShowSecondary => !IsShowingResult;
        protected override bool CanSecondaryAct(RunCoordinator value) => value.Phase==RunPhase.Home && !IsShowingResult;
        protected override void ActSecondary(RunCoordinator value) => value.StartRun(RunRequest.Stage2Diagnostic());
        protected override string Message(RunCoordinator value) => value.Phase==RunPhase.Home ? "WASD · Space · Q · E" : "Переходим на арену…";
    }
}
