using System;
using System.Linq;
using Game.Contracts;

namespace Game.Progression
{
    public sealed partial class CityProgressionSession
    {
        private static readonly CommandOutcome Applied = new(ProgressionStatus.Applied, ProgressionFailure.None);
        private static readonly CommandOutcome Replayed = new(ProgressionStatus.Replayed, ProgressionFailure.None);

        private static CommandOutcome Reject(ProgressionFailure failure) => new(ProgressionStatus.Rejected, failure);
        private static CommandOutcome Retry(ProgressionFailure failure) => new(ProgressionStatus.RetryableFailure, failure);

        private ProgressionResult Active(ProgressionCommandKey key, string name, object data, Func<CommandOutcome> action) =>
            Execute(key, name, data, () => run != null && key.RunId == run.Id
                ? action() : Reject(ProgressionFailure.WrongPhase));

        private ProgressionResult Execute(ProgressionCommandKey key, string name, object data,
            Func<CommandOutcome> action, bool home = false)
        {
            lock (commandGate)
                return ExecuteLocked(key, name, data, action, home);
        }

        private ProgressionResult ExecuteLocked(ProgressionCommandKey key, string name, object data,
            Func<CommandOutcome> action, bool home)
        {
            if (!Id(key.ActionId) || (home ? key.RunId != null : !Id(key.RunId)))
                return Reject(ProgressionFailure.InvalidRequest).ToResult(Snapshot);
            if (executing)
                return Retry(ProgressionFailure.OperationInProgress).ToResult(Snapshot);

            var journalKey = (key.RunId, key.ActionId);
            if (commands.TryGetValue(journalKey, out var previous))
            {
                if (previous.Name != name || !SamePayload(previous.Data, data))
                    return Reject(ProgressionFailure.Conflict).ToResult(Snapshot);
                if (previous.Result.Status != ProgressionStatus.RetryableFailure)
                    return previous.Result.ToResult(Snapshot, replay: true);
            }

            executing = true;
            try
            {
                var result = action();
                // Conflicts can also be terminal when they refer to a distinct domain identity.
                commands[journalKey] = new CommandEntry(name, data, result);
                return result.ToResult(Snapshot);
            }
            finally { executing = false; }
        }

        private static bool SamePayload(object first, object second)
        {
            if (first is StartRequest a && second is StartRequest b)
                return a.Seed == b.Seed && (a.Points == null || b.Points == null
                    ? a.Points == b.Points
                    : a.Points.SequenceEqual(b.Points));
            return Equals(first, second);
        }

        private readonly struct StartRequest
        {
            public readonly int Seed;
            public readonly ProgressionTransitionPoint[] Points;

            public StartRequest(int seed, ProgressionTransitionPoint[] points)
            {
                Seed = seed;
                Points = points;
            }
        }

        private readonly struct CommandEntry
        {
            public readonly string Name;
            public readonly object Data;
            public readonly CommandOutcome Result;

            public CommandEntry(string name, object data, CommandOutcome result)
            {
                Name = name;
                Data = data;
                Result = result;
            }
        }

        private readonly struct CommandOutcome
        {
            public readonly ProgressionStatus Status;
            private readonly ProgressionFailure failure;

            public CommandOutcome(ProgressionStatus status, ProgressionFailure failure)
            {
                Status = status;
                this.failure = failure;
            }

            public ProgressionResult ToResult(ProgressionSnapshot snapshot, bool replay = false)
            {
                var result = Status switch
                {
                    ProgressionStatus.Rejected => ProgressionResult.Rejected(failure, snapshot),
                    ProgressionStatus.RetryableFailure => ProgressionResult.RetryableFailure(failure, snapshot),
                    _ => ProgressionResult.Applied(snapshot)
                };
                return (replay || Status == ProgressionStatus.Replayed) && failure != ProgressionFailure.Conflict
                    ? result.AsReplay(snapshot) : result;
            }
        }
    }
}
