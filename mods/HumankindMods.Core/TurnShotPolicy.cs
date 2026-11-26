namespace HumankindMods.Core
{
    /// <summary>Which turns get an unattended screenshot (0 = never).</summary>
    public static class TurnShotPolicy
    {
        public static bool ShouldCapture(int turn, int everyTurns) => everyTurns > 0 && turn % everyTurns == 0;
    }
}
