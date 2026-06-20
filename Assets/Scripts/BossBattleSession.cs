using UnityEngine;

public static class BossBattleSession
{
    public static bool IsBossBattle { get; private set; }
    public static bool IsCombatPaused { get; private set; }
    public static float OxygenDecayMultiplier { get; private set; } = 1f;
    public static float DamageMultiplier { get; private set; } = 1f;
    public static float FireIntervalMultiplier { get; private set; } = 1f;

    public static void EnterBossBattle()
    {
        IsBossBattle = true;
        IsCombatPaused = false;
        OxygenDecayMultiplier = 1f;
        ClearDebuff();
    }

    public static void EnterNormalRun()
    {
        IsBossBattle = false;
        IsCombatPaused = false;
        OxygenDecayMultiplier = 1f;
        ClearDebuff();
    }

    public static void SetCombatPaused(bool paused)
    {
        IsCombatPaused = paused;
    }

    public static void EnterSecondPhase()
    {
        OxygenDecayMultiplier = 2f;
    }

    public static void SetDamageDebuff(float reductionRatio)
    {
        DamageMultiplier = Mathf.Clamp01(1f - reductionRatio);
        FireIntervalMultiplier = 1f;
    }

    public static void SetFireRateDebuff(float reductionRatio)
    {
        DamageMultiplier = 1f;
        FireIntervalMultiplier = 1f + Mathf.Clamp01(reductionRatio);
    }

    public static void ClearDebuff()
    {
        DamageMultiplier = 1f;
        FireIntervalMultiplier = 1f;
    }
}
