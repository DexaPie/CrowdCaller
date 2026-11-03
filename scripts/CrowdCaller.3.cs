// Crowd Caller - GTA V single-player script (ScriptHookVDotNet 3)
// Press one key and nearby pedestrians gather around you. Everything is configurable in CrowdCaller.ini.
// License: MIT (see LICENSE)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;

public sealed class CrowdCaller : Script
{
    // ---- settings (overridden by CrowdCaller.ini) ----
    Keys callKey = Keys.D8;
    Keys altKey = Keys.NumPad8;
    int count = 55;
    float gatherRadius = 300f;
    float minDist = 6f, maxDist = 22f;
    int holdMs = 90000;
    bool spawnExtra = true;
    bool showMessage = true;
    int cooldownMs = 2000;

    readonly Random rng = new Random();
    readonly List<KeyValuePair<Ped, int>> held = new List<KeyValuePair<Ped, int>>();
    readonly string folder = Path.Combine(Path.GetDirectoryName(typeof(Game).Assembly.Location), "scripts");
    bool keyHeld; int nextAllowed;

    public CrowdCaller()
    {
        LoadSettings();
        KeyDown += OnKeyDown;
        KeyUp += delegate (object s, KeyEventArgs e) { if (e.KeyCode == callKey || e.KeyCode == altKey) keyHeld = false; };
        Tick += OnTick;
        Interval = 500;
        Aborted += delegate { ReleaseAll(); };
    }

    void Log(string m) { try { File.AppendAllText(Path.Combine(folder, "CrowdCaller.log"), DateTime.Now.ToString("HH:mm:ss") + " " + m + Environment.NewLine); } catch { } }

    void LoadSettings()
    {
        string ini = Path.Combine(folder, "CrowdCaller.ini");
        if (!File.Exists(ini)) { Log("No CrowdCaller.ini found, using defaults"); return; }
        try
        {
            foreach (string raw in File.ReadAllLines(ini))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#") || line.StartsWith("[")) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string k = line.Substring(0, eq).Trim().ToLowerInvariant();
                string v = line.Substring(eq + 1).Trim();
                switch (k)
                {
                    case "key": Keys a; if (Enum.TryParse(v, true, out a)) callKey = a; break;
                    case "altkey": Keys b; if (Enum.TryParse(v, true, out b)) altKey = b; break;
                    case "count": count = Clamp(ParseInt(v, count), 1, 120); break;
                    case "gatherradius": gatherRadius = Clamp(ParseFloat(v, gatherRadius), 20f, 1000f); break;
                    case "mindistance": minDist = Clamp(ParseFloat(v, minDist), 1f, 100f); break;
                    case "maxdistance": maxDist = Clamp(ParseFloat(v, maxDist), 2f, 150f); break;
                    case "holdseconds": holdMs = Clamp(ParseInt(v, holdMs / 1000), 0, 3600) * 1000; break;
                    case "spawnextra": spawnExtra = ParseBool(v, spawnExtra); break;
                    case "showmessage": showMessage = ParseBool(v, showMessage); break;
                    case "cooldownseconds": cooldownMs = (int)(Clamp(ParseFloat(v, cooldownMs / 1000f), 0f, 60f) * 1000); break;
                }
            }
            if (maxDist < minDist + 1f) maxDist = minDist + 1f;
            Log("Loaded settings: key=" + callKey + " count=" + count + " radius=" + gatherRadius);
        }
        catch (Exception ex) { Log("Settings error: " + ex.Message); }
    }

    static int ParseInt(string s, int d) { int r; return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out r) ? r : d; }
    static float ParseFloat(string s, float d) { float r; return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out r) ? r : d; }
    static bool ParseBool(string s, bool d) { s = s.ToLowerInvariant(); if (s == "true" || s == "1" || s == "yes" || s == "on") return true; if (s == "false" || s == "0" || s == "no" || s == "off") return false; return d; }
    static int Clamp(int v, int lo, int hi) { return v < lo ? lo : v > hi ? hi : v; }
    static float Clamp(float v, float lo, float hi) { return v < lo ? lo : v > hi ? hi : v; }

    void OnKeyDown(object s, KeyEventArgs e)
    {
        if (e.KeyCode != callKey && e.KeyCode != altKey) return;
        if (e.Modifiers != Keys.None || keyHeld) return;
        keyHeld = true;
        if (Game.IsPaused || Function.Call<int>(Hash.UPDATE_ONSCREEN_KEYBOARD) == 0) return;   // ignore while typing
        if (Game.GameTime < nextAllowed) return;
        nextAllowed = Game.GameTime + cooldownMs;
        try { CallCrowd(); } catch (Exception ex) { Log("Error: " + ex.Message); }
    }

    void CallCrowd()
    {
        Ped me = Game.Player.Character;
        if (me == null || !me.Exists() || me.IsDead) return;
        Vector3 c = me.Position;

        // 1) pedestrians already walking around (on foot, alive, not owned by missions or other mods)
        var picked = new List<Ped>();
        var all = new List<Ped>(World.GetNearbyPeds(me, gatherRadius));
        all.Sort((p1, p2) => p1.Position.DistanceToSquared(c).CompareTo(p2.Position.DistanceToSquared(c)));
        foreach (Ped p in all)
        {
            if (picked.Count >= count) break;
            if (p == null || !p.Exists() || p.IsPlayer || p.IsDead || !p.IsHuman) continue;
            if (p.IsInVehicle() || p.IsRagdoll) continue;
            if (p.IsPersistent && !Mine(p)) continue;   // never touch peds that belong to missions / other mods
            picked.Add(p);
        }
        int existing = picked.Count;

        // 2) not enough people around? create random pedestrians to fill up
        int made = 0;
        if (spawnExtra)
            for (int i = picked.Count; i < count; i++)
            {
                Ped np = World.CreateRandomPed(SpotNear(c));
                if (np == null || !np.Exists()) continue;
                picked.Add(np); made++;
            }

        // 3) place everyone around the player and keep them wandering there
        int now = Game.GameTime;
        held.RemoveAll(kv => picked.Exists(x => x.Handle == kv.Key.Handle));
        foreach (Ped p in picked)
        {
            Vector3 spot = SpotNear(c);
            Function.Call(Hash.CLEAR_PED_TASKS_IMMEDIATELY, p.Handle);
            p.Position = spot;
            p.Heading = HeadingTo(spot, c) + rng.Next(-60, 60);
            if (holdMs > 0) { p.IsPersistent = true; held.Add(new KeyValuePair<Ped, int>(p, now + holdMs)); }
            Function.Call(Hash.TASK_WANDER_IN_AREA, p.Handle, c.X, c.Y, c.Z, maxDist, 2f, 3f);
        }
        if (showMessage) GTA.UI.Screen.ShowSubtitle("~g~Crowd Caller: " + picked.Count + " people nearby", 2500);
        Log("Called " + picked.Count + " (existing " + existing + ", created " + made + ")");
    }

    bool Mine(Ped p) { foreach (var kv in held) if (kv.Key != null && kv.Key.Handle == p.Handle) return true; return false; }

    // a safe spot on the ground between minDist and maxDist from the player (not inside buildings or on roofs)
    Vector3 SpotNear(Vector3 c)
    {
        for (int t = 0; t < 6; t++)
        {
            double ang = rng.NextDouble() * Math.PI * 2;
            float d = minDist + (float)rng.NextDouble() * (maxDist - minDist);
            Vector3 q = new Vector3(c.X + (float)Math.Cos(ang) * d, c.Y + (float)Math.Sin(ang) * d, c.Z);
            var outPos = new OutputArgument();
            if (Function.Call<bool>(Hash.GET_SAFE_COORD_FOR_PED, q.X, q.Y, q.Z, true, outPos, 16))
            {
                Vector3 r = outPos.GetResult<Vector3>();
                if (r.DistanceTo(c) < maxDist + 10f && Math.Abs(r.Z - c.Z) < 6f) return r;
            }
            float g = World.GetGroundHeight(new Vector3(q.X, q.Y, c.Z + 3f));
            if (g != 0f && Math.Abs(g - c.Z) < 3f) return new Vector3(q.X, q.Y, g + 1f);
        }
        return c + new Vector3((float)(rng.NextDouble() * 8 - 4), (float)(rng.NextDouble() * 8 - 4), 0.5f);
    }

    static float HeadingTo(Vector3 from, Vector3 to)
    {
        return (float)(Math.Atan2(-(to.X - from.X), to.Y - from.Y) * 180.0 / Math.PI);
    }

    void OnTick(object s, EventArgs e)
    {
        int now = Game.GameTime;
        for (int i = held.Count - 1; i >= 0; i--)
        {
            Ped p = held[i].Key;
            if (p == null || !p.Exists()) { held.RemoveAt(i); continue; }
            if (now >= held[i].Value) { p.IsPersistent = false; held.RemoveAt(i); }   // back to a normal ambient ped
        }
    }

    void ReleaseAll()
    {
        foreach (var kv in held) if (kv.Key != null && kv.Key.Exists()) kv.Key.IsPersistent = false;
        held.Clear();
    }
}
