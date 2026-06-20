public static class BossBattleSession
{
    public static bool IsBossBattle { get; private set; }
    public static bool IsCombatPaused { get; private set; }
    public static float OxygenDecayMultiplier { get; private set; } = 1f;

    public static void EnterBossBattle()
    {
        IsBossBattle = true;
        IsCombatPaused = false;
        OxygenDecayMultiplier = 1f;
    }

    public static void EnterNormalRun()
    {
        IsBossBattle = false;
        IsCombatPaused = false;
        OxygenDecayMultiplier = 1f;
    }

    public static void SetCombatPaused(bool paused)
    {
        IsCombatPaused = paused;
    }

    public static void EnterSecondPhase()
    {
        OxygenDecayMultiplier = 1.1f;
    }
}
