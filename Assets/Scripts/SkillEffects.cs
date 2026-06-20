using UnityEngine;

public static class SkillEffects
{
    public static float DamageMultiplier { get; private set; } = 1f;
    public static int DamageBonus { get; private set; } = 0;
    public static float FireIntervalMultiplier { get; private set; } = 1f;
    public static float OxygenOnKillBonus { get; private set; } = 0f;
    public static float OxygenOnKillMissingRatio { get; private set; } = 0f;
    public static float MaxOxygenBonus { get; private set; } = 0f;
    public static float OxygenDecayMultiplier { get; private set; } = 1f;
    public static float ForgeCooldownReduction { get; private set; } = 0f;
    public static int ForgeStabilityLevel { get; private set; } = 0;
    public static float ValueMultiplier { get; private set; } = 1f;
    public static bool CopperUnlocked { get; private set; } = false;
    public static float ForgeBonusChance { get; private set; } = 0f;
    public static float ForgeBonusMultiplier { get; private set; } = 1f;
    public static float ReviveOxygenPercent { get; private set; } = 0f;
    public static int ShieldCount { get; private set; } = 0;

    public static void SetDamageLevel(int level)
    {
        ApplyFromTable("atk1", level);
    }

    public static void SetFireRateLevel(int level)
    {
        ApplyFromTable("firerate1", level);
    }

    public static void SetOxygenOnKillLevel(int level)
    {
        ApplyFromTable("oxygenkill1", level);
    }

    public static void SetMaxOxygenLevel(int level)
    {
        ApplyFromTable("oxygenmax1", level);
    }

    public static void SetOxygenDecayLevel(int level)
    {
        ApplyFromTable("oxygendecay1", level);
    }

    public static void SetForgeCooldownLevel(int level)
    {
        ApplyFromTable("forge1", level);
    }

    public static void SetForgeStabilityLevel(int level)
    {
        ApplyFromTable("anvil", level);
    }

    public static void SetValueLevel(int level)
    {
        ApplyFromTable("value1", level);
    }

    public static void SetCopperLevel(int level)
    {
        ApplyFromTable("copper", level);
    }

    public static void ApplyAllFromTable(System.Func<string, int> levelForSkillId)
    {
        ResetDefaults();
        var entries = GameData.GetSkillEffects();
        if (entries == null || entries.Count == 0) return;
        foreach (var e in entries)
        {
            int level = levelForSkillId != null ? levelForSkillId(e.skillId) : 0;
            ApplyEffect(e, level);
        }
    }

    public static void ApplyFromTable(string skillId, int level)
    {
        var entries = GameData.GetSkillEffects();
        if (entries == null || entries.Count == 0) return;
        bool applied = false;
        foreach (var e in entries)
        {
            if (e.skillId != skillId) continue;
            ApplyEffect(e, level);
            applied = true;
        }
        if (!applied)
            Debug.LogWarning($"SkillEffects: Missing skillEffect entry for skillId '{skillId}'.");
    }

    static void ResetDefaults()
    {
        DamageMultiplier = 1f;
        DamageBonus = 0;
        FireIntervalMultiplier = 1f;
        OxygenOnKillBonus = 0f;
        OxygenOnKillMissingRatio = 0f;
        MaxOxygenBonus = 0f;
        OxygenDecayMultiplier = 1f;
        ForgeCooldownReduction = 0f;
        ForgeStabilityLevel = 0;
        ValueMultiplier = 1f;
        CopperUnlocked = false;
        ForgeBonusChance = 0f;
        ForgeBonusMultiplier = 1f;
        ReviveOxygenPercent = 0f;
        ShieldCount = 0;
    }

    static void ApplyEffect(GameData.SkillEffectDef e, int level)
    {
        if (e == null) return;
        float value = e.baseVal;
        if (e.calcType == "add")
        {
            value = e.baseVal + (e.perLevel * level);
        }
        else if (e.calcType == "pow")
        {
            value = e.baseVal * Mathf.Pow(e.perLevel, level);
        }
        else if (e.calcType == "bool")
        {
            value = level > 0 ? 1f : 0f;
        }
        else if (e.calcType == "mul")
        {
            float multiplier = Mathf.Pow(e.perLevel, level);
            value = Mathf.Approximately(e.baseVal, 0f)
                ? multiplier
                : e.baseVal * multiplier;
        }

        float min = e.minVal;
        float max = e.maxVal;
        if (min <= max)
            value = Mathf.Clamp(value, min, max);

        switch (e.targetStat)
        {
            case "damageMult":
            case "damageBonus":
                float damageMultiplier = e.calcType == "mul"
                    ? Mathf.Max(0f, value)
                    : Mathf.Max(0f, 1f + value);
                DamageMultiplier *= damageMultiplier;
                DamageBonus = 0;
                break;
            case "fireIntervalMult":
                FireIntervalMultiplier *= Mathf.Max(0.01f, value);
                break;
            case "oxygenOnKill":
                OxygenOnKillBonus += value;
                break;
            case "oxygenOnKillMissing":
                OxygenOnKillMissingRatio += Mathf.Max(0f, value);
                break;
            case "maxOxygen":
                MaxOxygenBonus += value;
                break;
            case "oxygenDecayMult":
                OxygenDecayMultiplier *= Mathf.Max(0f, value);
                break;
            case "forgeCooldownReduce":
                ForgeCooldownReduction += value;
                break;
            case "forgeStabilityLevel":
                ForgeStabilityLevel += Mathf.RoundToInt(value);
                break;
            case "valueMult":
                ValueMultiplier *= Mathf.Max(0f, value);
                break;
            case "copperUnlocked":
                CopperUnlocked = value > 0.5f;
                break;
            case "forgeBonusChance":
                ForgeBonusChance += Mathf.Max(0f, value);
                break;
            case "forgeBonusMultiplier":
                if (level > 0)
                    ForgeBonusMultiplier = Mathf.Max(1f, value);
                break;
            case "reviveOxygenPercent":
                ReviveOxygenPercent = Mathf.Max(ReviveOxygenPercent, value);
                break;
            case "shieldCount":
                ShieldCount += Mathf.Max(0, Mathf.RoundToInt(value));
                break;
            default:
                // Mineral unlock effects are checked from their matching skill level.
                if (!e.targetStat.EndsWith("Unlocked", System.StringComparison.OrdinalIgnoreCase))
                    Debug.LogWarning($"SkillEffects: Unknown targetStat '{e.targetStat}'.");
                break;
        }
    }
}
