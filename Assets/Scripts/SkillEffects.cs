using UnityEngine;

public static class SkillEffects
{
    public static int DamageBonus { get; private set; } = 0;
    public static float FireIntervalMultiplier { get; private set; } = 1f;
    public static float OxygenOnKillBonus { get; private set; } = 0f;
    public static float MaxOxygenBonus { get; private set; } = 0f;
    public static float OxygenDecayMultiplier { get; private set; } = 1f;
    public static float ForgeCooldownReduction { get; private set; } = 0f;
    public static int ForgeStabilityLevel { get; private set; } = 0;
    public static float ValueMultiplier { get; private set; } = 1f;
    public static bool CopperUnlocked { get; private set; } = false;

    public static void SetDamageLevel(int level)
    {
        DamageBonus = Mathf.Max(0, level); // +1 per level
    }

    public static void SetFireRateLevel(int level)
    {
        float mult = 1f - (0.03f * Mathf.Max(0, level));
        FireIntervalMultiplier = Mathf.Clamp(mult, 0.2f, 1f);
    }

    public static void SetOxygenOnKillLevel(int level)
    {
        OxygenOnKillBonus = Mathf.Max(0, level) * 3f;
    }

    public static void SetMaxOxygenLevel(int level)
    {
        MaxOxygenBonus = Mathf.Max(0, level) * 10f;
    }

    public static void SetOxygenDecayLevel(int level)
    {
        float mult = 1f - (0.03f * Mathf.Max(0, level));
        OxygenDecayMultiplier = Mathf.Clamp(mult, 0.1f, 1f);
    }

    public static void SetForgeCooldownLevel(int level)
    {
        ForgeCooldownReduction = Mathf.Max(0, level) * 0.1f;
    }

    public static void SetForgeStabilityLevel(int level)
    {
        ForgeStabilityLevel = Mathf.Clamp(level, 0, 3);
    }

    public static void SetValueLevel(int level)
    {
        int l = Mathf.Max(0, level);
        ValueMultiplier = Mathf.Pow(1.5f, l); // 1.5x per level
    }

    public static void SetCopperLevel(int level)
    {
        CopperUnlocked = level > 0;
    }
}
