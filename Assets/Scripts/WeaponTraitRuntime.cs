using System;
using System.Collections.Generic;
using UnityEngine;

public static class WeaponTraitRuntime
{
    public struct ShotModifiers
    {
        public float damageMultiplier;
        public bool destroyImmediately;
    }

    static readonly Dictionary<int, int> concentratedStacks = new Dictionary<int, int>();
    static int shotSequence;
    static float lastShotTime = -999f;
    static float sustainedFireTime;
    static float stationaryTime;
    static bool overheatHolding;
    static float overheatPhaseTimer;
    static bool overheatPenalty;

    public static void ResetRun()
    {
        concentratedStacks.Clear();
        shotSequence = 0;
        lastShotTime = -999f;
        sustainedFireTime = 0f;
        stationaryTime = 0f;
        overheatHolding = false;
        overheatPhaseTimer = 0f;
        overheatPenalty = false;
    }

    public static void UpdatePlayerMovement(bool isMoving, float deltaTime)
    {
        stationaryTime = isMoving ? 0f : stationaryTime + Mathf.Max(0f, deltaTime);
    }

    public static void UpdateCombat(float deltaTime)
    {
        if (Time.time - lastShotTime > 0.75f)
            sustainedFireTime = 0f;

        if (!overheatHolding && !overheatPenalty)
            return;

        overheatPhaseTimer += Mathf.Max(0f, deltaTime);
        float duration = GetSpecialParam("OverheatPower", 3, 2f);
        if (overheatPhaseTimer < duration)
            return;

        overheatPhaseTimer = 0f;
        if (overheatHolding)
        {
            overheatHolding = false;
            overheatPenalty = true;
        }
        else
        {
            overheatPenalty = false;
            sustainedFireTime = 0f;
        }
    }

    public static float ApplyStatEffects(string weaponId, string targetStat, float baseValue)
    {
        float value = baseValue;
        var traitIds = WeaponPanelManager.GetActiveTraitIds(weaponId);
        for (int i = 0; i < traitIds.Count; i++)
        {
            var effects = GameData.GetTraitEffects(traitIds[i]);
            for (int e = 0; e < effects.Count; e++)
            {
                var effect = effects[e];
                if (!string.Equals(effect.targetStat, targetStat, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (string.Equals(effect.calcType, "mul", StringComparison.OrdinalIgnoreCase))
                    value *= effect.value;
                else if (string.Equals(effect.calcType, "add", StringComparison.OrdinalIgnoreCase))
                    value += effect.value;
            }
        }
        return value;
    }

    public static float GetDynamicFireIntervalMultiplier(string weaponId, OxygenSystem oxygenSystem)
    {
        float multiplier = 1f;
        var specials = GetActiveSpecials(weaponId);
        for (int i = 0; i < specials.Count; i++)
        {
            var special = specials[i];
            switch (special.specialType)
            {
                case "AttackSpeedByMissingOxygen":
                    if (oxygenSystem != null && oxygenSystem.MaxOxygen > 0f)
                    {
                        float missingRatio = 1f - Mathf.Clamp01(oxygenSystem.currentOxygen / oxygenSystem.MaxOxygen);
                        multiplier *= 1f / Mathf.Max(0.05f, 1f + special.param1 * missingRatio);
                    }
                    break;
                case "RampUpFireRate":
                    {
                        float progress = Mathf.Clamp01(sustainedFireTime / Mathf.Max(0.01f, special.param2));
                        multiplier *= 1f / Mathf.Max(0.05f, 1f + special.param1 * progress);
                    }
                    break;
                case "StationaryBonus":
                    if (stationaryTime >= special.param1)
                        multiplier *= 1f / Mathf.Max(0.05f, 1f + special.param3);
                    break;
                case "OverheatPower":
                    if (overheatPenalty)
                        multiplier *= 1f / 0.7f;
                    break;
            }
        }
        return multiplier;
    }

    public static ShotModifiers OnWeaponFired(string weaponId)
    {
        float now = Time.time;
        float elapsed = now - lastShotTime;
        sustainedFireTime = elapsed <= 0.75f ? sustainedFireTime + Mathf.Max(0f, elapsed) : 0f;
        lastShotTime = now;
        shotSequence++;

        var result = new ShotModifiers { damageMultiplier = 1f };
        var specials = GetActiveSpecials(weaponId);
        for (int i = 0; i < specials.Count; i++)
        {
            var special = specials[i];
            switch (special.specialType)
            {
                case "Unstable_core":
                    if (UnityEngine.Random.value < special.param3)
                        result.destroyImmediately = true;
                    else if (UnityEngine.Random.value < special.param1)
                        result.damageMultiplier *= special.param2;
                    break;
                case "CriticalShot":
                    if (UnityEngine.Random.value < special.param1)
                        result.damageMultiplier *= special.param2;
                    break;
                case "EveryNthBulletDamage":
                    if (Mathf.Max(1, Mathf.RoundToInt(special.param1)) > 0 &&
                        shotSequence % Mathf.Max(1, Mathf.RoundToInt(special.param1)) == 0)
                        result.damageMultiplier *= special.param2;
                    break;
                case "StationaryBonus":
                    if (stationaryTime >= special.param1)
                        result.damageMultiplier *= 1f + special.param2;
                    break;
                case "OverheatPower":
                    if (!overheatPenalty)
                    {
                        float progress = Mathf.Clamp01(sustainedFireTime / Mathf.Max(0.01f, special.param2));
                        result.damageMultiplier *= 1f + special.param1 * progress;
                        if (progress >= 1f && !overheatHolding)
                        {
                            overheatHolding = true;
                            overheatPhaseTimer = 0f;
                        }
                    }
                    break;
            }
        }
        return result;
    }

    public static float ModifyHitDamage(
        string weaponId,
        Enemy enemy,
        float baseDamage,
        int hitIndex,
        float travelledDistance,
        float maxRange)
    {
        float damage = baseDamage;
        var specials = GetActiveSpecials(weaponId);
        for (int i = 0; i < specials.Count; i++)
        {
            var special = specials[i];
            switch (special.specialType)
            {
                case "Concentrated":
                    if (enemy != null)
                    {
                        int id = enemy.GetInstanceID();
                        concentratedStacks.TryGetValue(id, out int stacks);
                        stacks = Mathf.Min(Mathf.Max(1, Mathf.RoundToInt(special.param2)), stacks + 1);
                        concentratedStacks[id] = stacks;
                        damage *= 1f + special.param1 * 0.01f * stacks;
                    }
                    break;
                case "CloseRangeBonus":
                    {
                        float distanceRatio = maxRange > 0f ? Mathf.Clamp01(travelledDistance / maxRange) : 1f;
                        damage *= 1f + special.param1 * (1f - distanceRatio);
                    }
                    break;
                case "PierceFallOffDamage":
                    if (hitIndex == 0)
                        damage *= special.param1;
                    else
                        damage *= Mathf.Max(0f, special.param1 * Mathf.Pow(1f - special.param2, hitIndex));
                    break;
            }
        }
        return Mathf.Max(0.01f, damage);
    }

    public static bool ShouldExecute(string weaponId, Enemy enemy)
    {
        if (enemy == null || enemy.IsBoss)
            return false;

        var specials = GetActiveSpecials(weaponId);
        for (int i = 0; i < specials.Count; i++)
        {
            if (specials[i].specialType == "ExecuteShot" &&
                enemy.HealthRatio <= specials[i].param1)
                return true;
        }
        return false;
    }

    public static float GetOxygenOnKillMissingRatio(string weaponId)
    {
        return GetSpecialParam(weaponId, "OxygenOnKillMissing", 1);
    }

    public static float GetOreDropMultiplier(string weaponId)
    {
        return Mathf.Max(0f, ApplyStatEffects(weaponId, "oreDropRate", 1f));
    }

    public static float GetWaveOreRewardMultiplier(string weaponId)
    {
        return Mathf.Max(0f, ApplyStatEffects(weaponId, "waveOreReward", 1f));
    }

    static List<GameData.TraitSpecialDef> GetActiveSpecials(string weaponId)
    {
        var result = new List<GameData.TraitSpecialDef>();
        var traitIds = WeaponPanelManager.GetActiveTraitIds(weaponId);
        for (int i = 0; i < traitIds.Count; i++)
        {
            var special = GameData.GetTraitSpecial(traitIds[i]);
            if (special != null)
                result.Add(special);
        }
        return result;
    }

    static float GetSpecialParam(string specialType, int paramIndex, float fallback)
    {
        return GetSpecialParam(WeaponPanelManager.GetEquippedWeaponId(), specialType, paramIndex, fallback);
    }

    static float GetSpecialParam(string weaponId, string specialType, int paramIndex, float fallback = 0f)
    {
        var specials = GetActiveSpecials(weaponId);
        for (int i = 0; i < specials.Count; i++)
        {
            var special = specials[i];
            if (special.specialType != specialType) continue;
            if (paramIndex == 1) return special.param1;
            if (paramIndex == 2) return special.param2;
            if (paramIndex == 3) return special.param3;
        }
        return fallback;
    }
}
