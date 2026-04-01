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
        public string id;
        public float multiplier;
        public int baseWeight;
        public string desc;
    }

    static bool loaded = false;
    static Dictionary<string, MineralDef> minerals = new Dictionary<string, MineralDef>();
    static List<EnemyDef> enemies = new List<EnemyDef>();
    static Dictionary<int, WaveDef> waves = new Dictionary<int, WaveDef>();
    static List<ForgeEntry> forgeEntries = new List<ForgeEntry>();
    static HashSet<string> warnedSkills = new HashSet<string>();

    public static bool IsSkillUnlocked(string reqSkillId)
    {
        if (string.IsNullOrEmpty(reqSkillId)) return true;
        if (reqSkillId == "copper1") return SkillEffects.CopperUnlocked;
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

    static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;
        LoadMinerals();
        LoadEnemies();
        LoadWaves();
        LoadForgeTable();
    }

    static void LoadMinerals()
    {
        var table = LoadCsv("data/minerals");
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
        var table = LoadCsv("data/enemies");
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
        var table = LoadCsv("data/waves");
        if (table == null) return;
        foreach (var row in table.Rows)
        {
            var w = new WaveDef();
            w.wave = table.GetInt(row, "wave");
            w.duration = table.GetFloat(row, "duration");
            w.spawnCountMin = table.GetInt(row, "spawnCountMin");
            w.spawnCountMax = table.GetInt(row, "spawnCountMax");
            w.difficultyMultiplier = table.GetFloat(row, "difficultyMultiplier");
            w.isBossWave = table.GetBool(row, "isBossWave");
            w.desc = table.Get(row, "desc");
            if (w.wave > 0) waves[w.wave] = w;
        }
    }

    static void LoadForgeTable()
    {
        var table = LoadCsv("data/forgeTable");
        if (table == null) return;
        foreach (var row in table.Rows)
        {
            var f = new ForgeEntry();
            f.id = table.Get(row, "id");
            f.multiplier = table.GetFloat(row, "multiplier");
            f.baseWeight = table.GetInt(row, "baseWeight");
            f.desc = table.Get(row, "desc");
            if (!string.IsNullOrEmpty(f.id)) forgeEntries.Add(f);
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
            Debug.LogWarning($"GameData: Missing CSV at Resources/{resourcePath}.csv");
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
            var table = new CsvTable(rows[0]);
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
