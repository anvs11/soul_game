using Game.Contracts;

namespace Game.Progression
{
    public static class RanchProgression
    {
        public const string RegionId = "ranch";
        public const string SegmentId = "ranch/5-1";
        public const string YardId = "yard";
        public const string PenId = "pen";
        public const string BarnId = "barn";

        public static bool TryCheckpoint(string roomId, out ProgressionCheckpoint checkpoint)
        {
            int index = roomId switch { YardId => 0, PenId => 1, BarnId => 2, _ => -1 };
            checkpoint = index < 0 ? default : new ProgressionCheckpoint(SegmentId, roomId, index);
            return index >= 0;
        }

        internal static bool IsCheckpoint(ProgressionCheckpoint checkpoint) =>
            checkpoint.StageId == SegmentId && TryCheckpoint(checkpoint.RoomId, out var expected) &&
            checkpoint.Index == expected.Index;
    }
}
