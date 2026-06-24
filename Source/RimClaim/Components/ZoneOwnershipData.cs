using System.Collections.Generic;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace RimClaim
{
    /// <summary>
    /// MapComponent — per-map zone ownership.
    /// RimWorld zones are not ThingWithComps, so we track their ownership
    /// in a parallel dictionary keyed by Zone.ID.
    ///
    /// Access: map.GetComponent&lt;ZoneOwnershipData&gt;()
    ///         or use static helper ZoneOwnershipData.For(map).
    /// </summary>
    public class ZoneOwnershipData : MapComponent
    {
        // ── State ──────────────────────────────────────────────────────────────
        private Dictionary<int, int>  zoneOwners = new(); // zoneID → playerIndex
        private Dictionary<int, bool> zoneShared = new(); // zoneID → teamShared

        public ZoneOwnershipData(Map map) : base(map) { }

        // ── Static accessor ────────────────────────────────────────────────────
        public static ZoneOwnershipData? For(Map map)
            => map?.GetComponent<ZoneOwnershipData>();

        // ── Public read API ────────────────────────────────────────────────────
        public int GetOwner(Zone zone)
        {
            if (zone == null) return -1;
            return zoneOwners.TryGetValue(zone.ID, out int idx) ? idx : -1;
        }

        public bool IsShared(Zone zone)
        {
            if (zone == null) return false;
            return zoneShared.TryGetValue(zone.ID, out bool s) && s;
        }

        public bool IsOwnedByLocal(Zone zone)
            => MP.IsInMultiplayer && GetOwner(zone) == RcLocal.PlayerIndex;

        public bool IsUnclaimed(Zone zone)
            => GetOwner(zone) < 0;

        public bool LocalPlayerCanAccess(Zone zone)
        {
            if (!MP.IsInMultiplayer)    return true;
            if (IsUnclaimed(zone)) return true;
            if (IsOwnedByLocal(zone)) return true;
            if (!IsShared(zone)) return false;

            var teams = Current.Game.GetComponent<TeamRegistry>();
            if (teams == null) return false;
            var perms = teams.GetPermissions(RcLocal.PlayerIndex, GetOwner(zone));
            return perms.shareAreas || perms.shareStorage;
        }

        // ── Synced mutations ───────────────────────────────────────────────────

        public void SetZoneOwner(int zoneId, int playerIndex, bool shared)
        {
            if (playerIndex < 0)
            {
                zoneOwners.Remove(zoneId);
                zoneShared.Remove(zoneId);
            }
            else
            {
                zoneOwners[zoneId] = playerIndex;
                zoneShared[zoneId] = shared;
            }
        }

        public void SetZoneShared(int zoneId, bool shared)
        {
            if (zoneOwners.ContainsKey(zoneId))
                zoneShared[zoneId] = shared;
        }

        // ── Cleanup: remove data for deleted zones ─────────────────────────────
        public void OnZoneDeleted(Zone zone)
        {
            // Called from Patch_Zone_Delete — no sync needed, zone deletion is
            // already a synced player action upstream.
            if (zone == null) return;
            zoneOwners.Remove(zone.ID);
            zoneShared.Remove(zone.ID);
        }

        // ── Serialization ──────────────────────────────────────────────────────
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref zoneOwners, "zoneOwners", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref zoneShared, "zoneShared", LookMode.Value, LookMode.Value);
            zoneOwners ??= new Dictionary<int, int>();
            zoneShared ??= new Dictionary<int, bool>();
        }
    }
}
