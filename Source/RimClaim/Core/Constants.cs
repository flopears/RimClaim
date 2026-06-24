namespace RimClaim
{
    public static class Constants
    {
        // ── Pause system ───────────────────────────────────────────────────────
        public const int SoftPauseTimeoutTicks       = 60 * 5;   // 5 seconds
        public const int MaxEmergencyPausesPerDay    = 3;

        // ── Event scaling ──────────────────────────────────────────────────────
        public const float ThreatScalePerPlayer      = 0.6f;

        // ── Landclaim ──────────────────────────────────────────────────────────
        public const int DefaultClaimRadius          = 25;
        public const int DefaultPostRadius           = 15;
        public const int MaxClaimsPerPlayer          = 3;
        public const int BufferZoneWidth             = 5;
        public const int ClaimGracePeriodTicks       = 2500;     // ~1 in-game hour
        public const int MaxLocalTickRate            = 3;
        public const float ClaimPowerConsumption     = 200f;

        // ── Tick system ────────────────────────────────────────────────────────
        public const int VisibilityCacheRebuildFrames = 60;
        public const int CombatSyncCheckInterval     = 15;
        public const int CombatSyncResolveInterval   = 60;
        public const int CombatSyncInactivityTimeout = 300;      // 5 seconds
        public const int EscapeSyncResolveInterval   = 30;

        // ── Ownership ──────────────────────────────────────────────────────────
        public const bool HaulingInheritsOwnership   = true;
        public const int TempClaimDurationTicks      = 60000;    // ~1 in-game day

        // ── Trade ──────────────────────────────────────────────────────────────
        public const int TradeTimerDefault           = 0;        // 0 = OFF

        // ── Gravship ──────────────────────────────────────────────────────────
        public const int GravshipCountdownTicks           = 1800; // 30s
        public const int GravshipEmergencyCountdownTicks  = 600;  // 10s
        public const int GravshipBoardingBuffer           = 600;  // 10s padding
        public const int CoLaunchMaxWaitTicks             = 36000;// 10 in-game hours
        public const int ExtensionRequestTimeoutTicks     = 3600; // 60s

        // ── UI ─────────────────────────────────────────────────────────────────
        public const float PlayersPanelWidth         = 320f;
        public const float PlayerRowHeight           = 36f;
        public const float PlayerColorSwatchSize     = 20f;
    }
}
