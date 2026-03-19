using UnityEngine;

public static class SkillEffects
{
    public static int DamageBonus { get; private set; } = 0;
    public static float FireIntervalMultiplier { get; private set; } = 1f;
    public static float OxygenOnKillBonus { get; private set; } = 0f;
    public static float MaxOxygenBonus { get; private set; } = 0f;
    public static float OxygenDecayMultiplier { get; private set; } = 1f;
    public static float ForgeCooldownReduction { get; private set; } = 0f;
    public static float ValueMultiplier { get; private set; } = 1f;

    public static void SetDamageLevel(int level)
    {
        DamageBonus = Mathf.Max(0, level); // +1 per level
    }

    public static void SetFireRateLevel(int level)
    {
        float mult = 1f - (0.05f * Mathf.Max(0, level));
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
        float mult = 1f - (0.05f * Mathf.Max(0, level));
        OxygenDecayMultiplier = Mathf.Clamp(mult, 0.1f, 1f);
    }

    public static void SetForgeCooldownLevel(int level)
    {
        ForgeCooldownReduction = Mathf.Max(0, level) * 0.2f;
    }

    public static void SetValueLevel(int level)
    {
        int l = Mathf.Max(0, level);
        ValueMultiplier = Mathf.Pow(2f, l); // 2x per level
    }
}
