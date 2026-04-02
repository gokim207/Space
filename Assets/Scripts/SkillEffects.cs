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
        ApplyFromTable("atk", level);
    }

    public static void SetFireRateLevel(int level)
    {
        ApplyFromTable("firerate", level);
    }

    public static void SetOxygenOnKillLevel(int level)
    {
        ApplyFromTable("oxygenkill", level);
    }

    public static void SetMaxOxygenLevel(int level)
    {
        ApplyFromTable("oxygenmax", level);
    }

    public static void SetOxygenDecayLevel(int level)
    {
        ApplyFromTable("oxygendecay", level);
    }

    public static void SetForgeCooldownLevel(int level)
    {
        ApplyFromTable("forge", level);
    }

    public static void SetForgeStabilityLevel(int level)
    {
        ApplyFromTable("anvil", level);
    }

    public static void SetValueLevel(int level)
    {
        ApplyFromTable("value", level);
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
        DamageBonus = 0;
        FireIntervalMultiplier = 1f;
        OxygenOnKillBonus = 0f;
        MaxOxygenBonus = 0f;
        OxygenDecayMultiplier = 1f;
        ForgeCooldownReduction = 0f;
        ForgeStabilityLevel = 0;
        ValueMultiplier = 1f;
        CopperUnlocked = false;
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
            value = e.baseVal * (e.perLevel * level);
        }

        float min = e.minVal;
        float max = e.maxVal;
        if (min <= max)
            value = Mathf.Clamp(value, min, max);

        switch (e.targetStat)
        {
            case "damageBonus":
                DamageBonus = Mathf.RoundToInt(value);
                break;
            case "fireIntervalMult":
                FireIntervalMultiplier = value;
                break;
            case "oxygenOnKill":
                OxygenOnKillBonus = value;
                break;
            case "maxOxygen":
                MaxOxygenBonus = value;
                break;
            case "oxygenDecayMult":
                OxygenDecayMultiplier = value;
                break;
            case "forgeCooldownReduce":
                ForgeCooldownReduction = value;
                break;
            case "forgeStabilityLevel":
                ForgeStabilityLevel = Mathf.RoundToInt(value);
                break;
            case "valueMult":
                ValueMultiplier = value;
                break;
            case "copperUnlocked":
                CopperUnlocked = value > 0.5f;
                break;
            default:
                Debug.LogWarning($"SkillEffects: Unknown targetStat '{e.targetStat}'.");
                break;
        }
    }
}
