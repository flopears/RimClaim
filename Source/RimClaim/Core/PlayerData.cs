using System;
using Multiplayer.API;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimClaim
{
    /// <summary>
    /// Persistent identity record for one player in a RimClaim session.
    /// Survives disconnects, host migration, and save/load cycles.
    /// Stored inside PlayerRegistry (GameComponent).
    /// </summary>
    public class PlayerData : IExposable
    {
        // ── Identity ───────────────────────────────────────────────────────────
        /// <summary>
        /// Index assigned by Zetrith's MP mod (RcLocal.PlayerIndex on that client).
        /// This is stable for the lifetime of the save.
        /// </summary>
        public int playerIndex = -1;

        /// <summary>Steam/username display name. Cosmetic only.</summary>
        public string displayName = "Unknown";

        /// <summary>
        /// The loadID of the RimWorld Faction object that belongs to this player.
        /// Resolved at runtime via Find.FactionManager.
        /// </summary>
        public int factionLoadId = -1;

        // ── Visuals ────────────────────────────────────────────────────────────
        /// <summary>Per-player color used for UI tinting, map overlays, etc.</summary>
        public Color playerColor = Color.white;

        // ── Connection ─────────────────────────────────────────────────────────
        /// <summary>True while the player is connected in the current session.</summary>
        public bool isConnected = false;

        /// <summary>Last tick we received a heartbeat from this player.</summary>
        public int lastSeenTick = 0;

        // ── Pause budget ───────────────────────────────────────────────────────
        /// <summary>Emergency pauses used today. Resets each in-game day.</summary>
        public int emergencyPausesToday = 0;

        /// <summary>The in-game day on which emergencyPausesToday was last reset.</summary>
        public int emergencyPauseResetDay = -1;

        // ── Convenience ────────────────────────────────────────────────────────
        public bool IsValid => playerIndex >= 0;

        public Faction? GetFaction()
        {
            if (factionLoadId < 0) return null;
            foreach (var f in Find.FactionManager.AllFactions)
                if (f.loadID == factionLoadId) return f;
            return null;
        }

        // ── Serialization ──────────────────────────────────────────────────────
        public void ExposeData()
        {
            Scribe_Values.Look(ref playerIndex,            "playerIndex",            -1);
            Scribe_Values.Look(ref displayName,            "displayName",            "Unknown");
            Scribe_Values.Look(ref factionLoadId,          "factionLoadId",          -1);
            Scribe_Values.Look(ref isConnected,            "isConnected",            false);
            Scribe_Values.Look(ref lastSeenTick,           "lastSeenTick",           0);
            Scribe_Values.Look(ref emergencyPausesToday,   "emergencyPausesToday",   0);
            Scribe_Values.Look(ref emergencyPauseResetDay, "emergencyPauseResetDay", -1);

            // Color doesn't have a built-in Scribe — store as separate RGBA floats
            float r = playerColor.r, g = playerColor.g, b = playerColor.b, a = playerColor.a;
            Scribe_Values.Look(ref r, "colorR", 1f);
            Scribe_Values.Look(ref g, "colorG", 1f);
            Scribe_Values.Look(ref b, "colorB", 1f);
            Scribe_Values.Look(ref a, "colorA", 1f);
            playerColor = new Color(r, g, b, a);
        }
    }
}
