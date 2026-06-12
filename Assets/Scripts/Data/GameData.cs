using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public static class GameData
{
    public class MineralDef
    {
        public string id;
        public string name;
        public float value;
        public int dropWeight;
        public Color color;
        public int minWave;
        public string reqSkill;
        public string desc;
    }

    public class EnemyDef
    {
        public string id;
        public int baseHP;
        public int hpPerWave;
        public float baseSpeed;
        public float speedPerWave;
        public Color color;
        public int spawnWeight;
        public string dropOreId;
        public int minWave;
        public string reqSkill;
        public string desc;
    }

    public class WaveDef
    {
        public int wave;
        public float duration;
        public int spawnCountMin;
        public int spawnCountMax;
        public float difficultyMultiplier;
        public bool isBossWave;
        public string desc;
    }

    public class ForgeEntry
    {
        public string forgeId;
        public float multiplier;
        public int baseWeight;
        public string desc;
    }

    public class EnemySpawnDef
    {
        public string spawnId;
        public float minInterval;
        public float maxInterval;
        public int minAmount;
        public int maxAmount;
        public float minDist;
        public float maxDist;
        public string spawnPattern;
        public string desc;
    }

    public class WeaponDef
    {
        public string weaponId;
        public string weaponName;
        public int damage;
        public float fireInterval;
        public float bulletSpeed;
        public float detectRange;
        public int pierceCount;
        public int projCount;
        public string iconKey;
        public string desc;
    }

    public class ProjectileDef
    {
        public string projectileId;
        public float speed;
        public float lifeTime;
        public float damageMult;
        public int pierceCount;
        public float hitRadius;
        public string prefabId;
        public string desc;
    }

    public class OxygenDef
    {
        public string oxygenId;
        public float startOxygen;
        public float maxOxygen;
        public float decreaseInterval;
        public float decayMin;
        public float decayMax;
        public float killReward;
        public string desc;
    }

    public class PlayerDef
    {
        public string id;
        public float baseMoveSpeed;
        public int maxHp;
        public float invincibilityTime;
        public float radius;
        public float fixedY;
        public string desc;
    }

    public class SkillEffectDef
    {
        public string skillId;
        public string targetStat;
        public string calcType;
        public float baseVal;
        public float perLevel;
        public float minVal;
        public float maxVal;
        public string desc;
    }

    static bool loaded = false;
    static Dictionary<string, MineralDef> minerals = new Dictionary<string, MineralDef>();
    static List<EnemyDef> enemies = new List<EnemyDef>();
    static Dictionary<int, WaveDef> waves = new Dictionary<int, WaveDef>();
    static List<ForgeEntry> forgeEntries = new List<ForgeEntry>();
    static Dictionary<string, EnemySpawnDef> enemySpawns = new Dictionary<string, EnemySpawnDef>();
    static Dictionary<string, WeaponDef> weapons = new Dictionary<string, WeaponDef>();
    static List<WeaponDef> weaponList = new List<WeaponDef>();
    static Dictionary<string, ProjectileDef> projectiles = new Dictionary<string, ProjectileDef>();
    static Dictionary<string, OxygenDef> oxygenDefs = new Dictionary<string, OxygenDef>();
    static Dictionary<string, PlayerDef> players = new Dictionary<string, PlayerDef>();
    static List<SkillEffectDef> skillEffects = new List<SkillEffectDef>();
    static HashSet<string> warnedSkills = new HashSet<string>();

    public static bool IsSkillUnlocked(string reqSkillId)
    {
        if (string.IsNullOrEmpty(reqSkillId)) return true;
        if (reqSkillId == "none") return true;
        if (reqSkillId == "copper1" || reqSkillId == "copper") return SkillEffects.CopperUnlocked;
        if (!warnedSkills.Contains(reqSkillId))
        {
            warnedSkills.Add(reqSkillId);
            Debug.LogWarning($"GameData: Unknown reqSkill '{reqSkillId}'. Treating as locked.");
        }
        return false;
    }

    public static MineralDef GetMineral(string id)
    {
        EnsureLoaded();
        minerals.TryGetValue(id, out var m);
        return m;
    }

    public static float GetMineralValue(string id, float fallback)
    {
        var m = GetMineral(id);
        return m != null ? m.value : fallback;
    }

    public static IEnumerable<EnemyDef> GetAvailableEnemies(int wave)
    {
        EnsureLoaded();
        foreach (var e in enemies)
        {
            if (e == null) continue;
            if (wave < e.minWave) continue;
            if (!IsSkillUnlocked(e.reqSkill)) continue;
            yield return e;
        }
    }

    public static WaveDef GetWave(int wave)
    {
        EnsureLoaded();
        waves.TryGetValue(wave, out var w);
        return w;
    }

    public static List<ForgeEntry> GetForgeEntries()
    {
        EnsureLoaded();
        return forgeEntries;
    }

    public static void ReloadForgeEntries()
    {
        EnsureLoaded();
        forgeEntries.Clear();
        LoadForgeTable();
    }

    public static EnemySpawnDef GetEnemySpawn(string spawnId)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(spawnId)) return null;
        enemySpawns.TryGetValue(spawnId, out var s);
        return s;
    }

    public static WeaponDef GetWeapon(string weaponId)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(weaponId)) return null;
        weapons.TryGetValue(weaponId, out var w);
        return w;
    }

    public static List<WeaponDef> GetWeapons()
    {
        EnsureLoaded();
        return weaponList;
    }

    public static ProjectileDef GetProjectile(string projectileId)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(projectileId)) return null;
        projectiles.TryGetValue(projectileId, out var p);
        return p;
    }

    public static OxygenDef GetOxygen(string oxygenId)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(oxygenId)) return null;
        oxygenDefs.TryGetValue(oxygenId, out var o);
        return o;
    }

    public static PlayerDef GetPlayer(string playerId)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(playerId)) return null;
        players.TryGetValue(playerId, out var p);
        return p;
    }

    public static List<SkillEffectDef> GetSkillEffects()
    {
        EnsureLoaded();
        return skillEffects;
    }

    static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;
        LoadMinerals();
        LoadEnemies();
        LoadWaves();
        LoadForgeTable();
        LoadEnemySpawns();
        LoadWeapons();
        LoadProjectiles();
        LoadOxygen();
        LoadPlayers();
        LoadSkillEffects();
    }

    static void LoadMinerals()
    {
        var table = LoadCsv("data/stone");
        if (table == null) return;
        foreach (var row in table.Rows)
        {
            var m = new MineralDef();
            m.id = table.Get(row, "id");
            m.name = table.Get(row, "name");
            m.value = table.GetFloat(row, "value");
            m.dropWeight = table.GetInt(row, "dropWeight");
            m.color = ParseColorRgb(table.Get(row, "colorRGB"), Color.white);
            m.minWave = table.GetInt(row, "minWave");
            m.reqSkill = table.Get(row, "reqSkill");
            m.desc = table.Get(row, "desc");
            if (!string.IsNullOrEmpty(m.id)) minerals[m.id] = m;
        }
    }

    static void LoadEnemies()
    {
        var table = LoadCsv("data/enemy");
        if (table == null) return;
        foreach (var row in table.Rows)
        {
            var e = new EnemyDef();
            e.id = table.Get(row, "id");
            e.baseHP = table.GetInt(row, "baseHP");
            e.hpPerWave = table.GetInt(row, "hpPerWave");
            e.baseSpeed = table.GetFloat(row, "baseSpeed");
            e.speedPerWave = table.GetFloat(row, "speedPerWave");
            e.color = ParseColorRgb(table.Get(row, "colorRGB"), Color.white);
            e.spawnWeight = table.GetInt(row, "spawnWeight");
            e.dropOreId = table.Get(row, "dropOreId");
            e.minWave = table.GetInt(row, "minWave");
            e.reqSkill = table.Get(row, "reqSkill");
            e.desc = table.Get(row, "desc");
            if (!string.IsNullOrEmpty(e.id)) enemies.Add(e);
        }
    }

    static void LoadWaves()
    {
        var table = LoadCsv("data/wave");
        if (table == null) return;
        foreach (var row in table.Rows)
        {
            var w = new WaveDef();
            w.wave = table.GetInt(row, "wave");
            w.duration = table.GetFloat(row, "duration");
            w.spawnCountMin = table.GetInt(row, "spawnCountMin");
            w.spawnCountMax = table.GetInt(row, "spawnCountMax");
            w.difficultyMultiplier = table.GetFloat(row, "difficultyMultiplier");
            if (w.difficultyMultiplier == 0f)
                w.difficultyMultiplier = table.GetFloat(row, "difficultyMult");
            w.isBossWave = table.GetBool(row, "isBossWave");
            w.desc = table.Get(row, "desc");
            if (w.wave > 0) waves[w.wave] = w;
        }
    }

    static void LoadForgeTable()
    {
        var table = LoadCsv("data/forge");
        if (table == null) return;
        foreach (var row in table.Rows)
        {
            var f = new ForgeEntry();
            f.forgeId = table.Get(row, "forgeId");
            if (string.IsNullOrEmpty(f.forgeId))
                f.forgeId = table.Get(row, "id");
            f.multiplier = table.GetFloat(row, "multiplier");
            f.baseWeight = table.GetInt(row, "baseWeight");
            f.desc = table.Get(row, "desc");
            if (!string.IsNullOrEmpty(f.forgeId)) forgeEntries.Add(f);
        }
    }

    static void LoadEnemySpawns()
    {
        var table = LoadCsv("data/enemySpawn");
        if (table == null) return;
        foreach (var row in table.Rows)
        {
            var s = new EnemySpawnDef();
            s.spawnId = table.Get(row, "spawnId");
            s.minInterval = table.GetFloat(row, "minInterval");
            s.maxInterval = table.GetFloat(row, "maxInterval");
            s.minAmount = table.GetInt(row, "minAmount");
            s.maxAmount = table.GetInt(row, "maxAmount");
            s.minDist = table.GetFloat(row, "minDist");
            s.maxDist = table.GetFloat(row, "maxDist");
            s.spawnPattern = table.Get(row, "spawnPattern");
            s.desc = table.Get(row, "desc");
            if (!string.IsNullOrEmpty(s.spawnId))
                enemySpawns[s.spawnId] = s;
        }
    }

    static void LoadWeapons()
    {
        var table = LoadCsv("data/weapon");
        if (table == null) return;
        foreach (var row in table.Rows)
        {
            var w = new WeaponDef();
            w.weaponId = table.Get(row, "weaponId");
            w.weaponName = table.Get(row, "weaponName");
            w.damage = table.GetInt(row, "damage");
            w.fireInterval = table.GetFloat(row, "fireInterval");
            w.bulletSpeed = table.GetFloat(row, "bulletSpeed");
            w.detectRange = table.GetFloat(row, "detectRange");
            w.pierceCount = table.GetInt(row, "pierceCount");
            w.projCount = table.GetInt(row, "projCount");
            w.iconKey = table.Get(row, "iconKey");
            w.desc = table.Get(row, "desc");
            if (!string.IsNullOrEmpty(w.weaponId))
            {
                weapons[w.weaponId] = w;
                weaponList.Add(w);
            }
        }
    }

    static void LoadProjectiles()
    {
        var table = LoadCsv("data/projectTile");
        if (table == null) return;
        foreach (var row in table.Rows)
        {
            var p = new ProjectileDef();
            p.projectileId = table.Get(row, "projectileId");
            p.speed = table.GetFloat(row, "speed");
            p.lifeTime = table.GetFloat(row, "lifeTime");
            p.damageMult = table.GetFloat(row, "damageMult");
            p.pierceCount = table.GetInt(row, "pierceCount");
            p.hitRadius = table.GetFloat(row, "hitRadius");
            p.prefabId = table.Get(row, "prefabId");
            p.desc = table.Get(row, "desc");
            if (!string.IsNullOrEmpty(p.projectileId))
                projectiles[p.projectileId] = p;
        }
    }

    static void LoadOxygen()
    {
        var table = LoadCsv("data/oxygen");
        if (table == null) return;
        foreach (var row in table.Rows)
        {
            var o = new OxygenDef();
            o.oxygenId = table.Get(row, "oxygenId");
            o.startOxygen = table.GetFloat(row, "startOxygen");
            o.maxOxygen = table.GetFloat(row, "maxOxygen");
            o.decreaseInterval = table.GetFloat(row, "decreaseInterval");
            o.decayMin = table.GetFloat(row, "decayMin");
            o.decayMax = table.GetFloat(row, "decayMax");
            o.killReward = table.GetFloat(row, "killReward");
            o.desc = table.Get(row, "desc");
            if (!string.IsNullOrEmpty(o.oxygenId))
                oxygenDefs[o.oxygenId] = o;
        }
    }

    static void LoadPlayers()
    {
        var table = LoadCsv("data/player");
        if (table == null) return;
        foreach (var row in table.Rows)
        {
            var p = new PlayerDef();
            p.id = table.Get(row, "id");
            p.baseMoveSpeed = table.GetFloat(row, "baseMoveSpeed");
            p.maxHp = table.GetInt(row, "maxHp");
            p.invincibilityTime = table.GetFloat(row, "invincibilityTime");
            p.radius = table.GetFloat(row, "radius");
            p.fixedY = table.GetFloat(row, "fixedY");
            p.desc = table.Get(row, "desc");
            if (!string.IsNullOrEmpty(p.id))
                players[p.id] = p;
        }
    }

    static void LoadSkillEffects()
    {
        var table = LoadCsv("data/skillEffect");
        if (table == null) return;
        foreach (var row in table.Rows)
        {
            var s = new SkillEffectDef();
            s.skillId = table.Get(row, "skillId");
            s.targetStat = table.Get(row, "targetStat");
            s.calcType = table.Get(row, "calcType");
            s.baseVal = table.GetFloat(row, "baseVal");
            s.perLevel = table.GetFloat(row, "perLevel");
            s.minVal = table.GetFloat(row, "minVal");
            s.maxVal = table.GetFloat(row, "maxVal");
            s.desc = table.Get(row, "desc");
            if (!string.IsNullOrEmpty(s.skillId) && !string.IsNullOrEmpty(s.targetStat))
                skillEffects.Add(s);
        }
    }

    static Color ParseColorRgb(string value, Color fallback)
    {
        if (string.IsNullOrEmpty(value)) return fallback;
        var parts = value.Split('|');
        if (parts.Length != 3) return fallback;
        if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var r)) return fallback;
        if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var g)) return fallback;
        if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var b)) return fallback;
        return new Color(r, g, b, 1f);
    }

    static CsvTable LoadCsv(string resourcePath)
    {
        var asset = Resources.Load<TextAsset>(resourcePath);
        if (asset == null)
        {
            // Fallback: search within the folder for a matching name (case-insensitive)
            int slash = resourcePath.LastIndexOf('/');
            string folder = slash >= 0 ? resourcePath.Substring(0, slash) : "";
            string name = slash >= 0 ? resourcePath.Substring(slash + 1) : resourcePath;
            var all = Resources.LoadAll<TextAsset>(folder);
            if (all != null)
            {
                foreach (var a in all)
                {
                    if (a != null && string.Equals(a.name, name, System.StringComparison.OrdinalIgnoreCase))
                    {
                        asset = a;
                        break;
                    }
                }
            }
        }
        if (asset == null)
        {
            var list = Resources.LoadAll<TextAsset>("data");
            if (list != null)
            {
                var names = new System.Text.StringBuilder();
                for (int i = 0; i < list.Length; i++)
                {
                    if (list[i] == null) continue;
                    if (names.Length > 0) names.Append(", ");
                    names.Append(list[i].name);
                }
                Debug.LogWarning($"GameData: Missing CSV at Resources/{resourcePath}.csv. Found in Resources/data: [{names}]");
            }
            else
            {
                Debug.LogWarning($"GameData: Missing CSV at Resources/{resourcePath}.csv");
            }
            return null;
        }
        return CsvTable.Parse(asset.text);
    }

    class CsvTable
    {
        public readonly List<Dictionary<string, string>> Rows = new List<Dictionary<string, string>>();
        readonly string[] headers;

        CsvTable(string[] headers)
        {
            this.headers = headers;
        }

        public static CsvTable Parse(string text)
        {
            var rows = ParseCsv(text);
            if (rows.Count == 0) return null;
            // normalize headers (trim + strip BOM)
            var rawHeaders = rows[0];
            for (int i = 0; i < rawHeaders.Length; i++)
            {
                if (rawHeaders[i] == null) continue;
                var h = rawHeaders[i].Trim();
                if (h.Length > 0 && h[0] == '\uFEFF')
                    h = h.Substring(1);
                rawHeaders[i] = h;
            }
            var table = new CsvTable(rawHeaders);
            for (int i = 1; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row.Length == 0) continue;
                var dict = new Dictionary<string, string>();
                for (int c = 0; c < table.headers.Length; c++)
                {
                    var key = table.headers[c];
                    var val = c < row.Length ? row[c] : "";
                    dict[key] = val;
                }
                table.Rows.Add(dict);
            }
            return table;
        }

        public string Get(Dictionary<string, string> row, string key)
        {
            if (row.TryGetValue(key, out var v)) return v;
            return "";
        }

        public int GetInt(Dictionary<string, string> row, string key)
        {
            var v = Get(row, key);
            if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) return i;
            return 0;
        }

        public float GetFloat(Dictionary<string, string> row, string key)
        {
            var v = Get(row, key);
            if (float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var f)) return f;
            return 0f;
        }

        public bool GetBool(Dictionary<string, string> row, string key)
        {
            var v = Get(row, key).Trim().ToLowerInvariant();
            return v == "true" || v == "1" || v == "yes";
        }

        static List<string[]> ParseCsv(string text)
        {
            var result = new List<string[]>();
            var row = new List<string>();
            var field = "";
            bool inQuotes = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            field += '"';
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        field += c;
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else if (c == ',')
                    {
                        row.Add(field);
                        field = "";
                    }
                    else if (c == '\n')
                    {
                        row.Add(field);
                        field = "";
                        result.Add(row.ToArray());
                        row = new List<string>();
                    }
                    else if (c == '\r')
                    {
                        // ignore
                    }
                    else
                    {
                        field += c;
                    }
                }
            }
            if (field.Length > 0 || row.Count > 0)
            {
                row.Add(field);
                result.Add(row.ToArray());
            }
            return result;
        }
    }
}
