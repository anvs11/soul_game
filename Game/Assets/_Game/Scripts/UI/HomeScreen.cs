using Game.Core;

namespace Game.UI
{
    public sealed class HomeScreen : FlowScreen
    {
        protected override string Title => "Убежище";
        protected override string Description => "Проверка первого игрового маршрута.\nНачните вылазку и вернитесь с пустой арены.";
        protected override string ButtonLabel => "Начать проверочную вылазку";
        protected override bool CanAct(RunCoordinator value) => value.Phase == RunPhase.Home;
        protected override void Act(RunCoordinator value) => value.StartRun(RunRequest.Sandbox());
        protected override string Message(RunCoordinator value)
        {
            if (value.Phase != RunPhase.Home) return "Переходим на арену…";
            return value.LastOutcome.HasValue
                ? "Попытка завершена. Можно начать снова.\nНаграды на этом этапе не начисляются."
                : "Управление героем и бой будут подключены следующим этапом.";
        }
    }
}
