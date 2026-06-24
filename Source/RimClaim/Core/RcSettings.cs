using RimWorld;
using UnityEngine;
using Verse;

namespace RimClaim
{
    public class RcSettings : ModSettings
    {
        // ── Territory ──────────────────────────────────────────────────────────
        public bool  StrictZoneBorders       = true;
        public bool  TemporaryClaimBubbles   = true;
        public int   MaxClaimsPerPlayer      = 3;
        public int   DefaultClaimRadius      = 25;
        public int   BufferZoneWidth         = 5;

        // ── Trade ──────────────────────────────────────────────────────────────
        public int   TradeTimerSeconds       = 0; // 0 = OFF

        // ── Gravship ──────────────────────────────────────────────────────────
        public int   GravshipCountdownSeconds     = 30;
        public int   GravshipEmergencyCountdown   = 10;

        // ── Events ────────────────────────────────────────────────────────────
        public float ThreatScalePerPlayer    = 0.6f;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref StrictZoneBorders,     "strictZoneBorders",     true);
            Scribe_Values.Look(ref TemporaryClaimBubbles, "temporaryClaimBubbles", true);
            Scribe_Values.Look(ref MaxClaimsPerPlayer,    "maxClaimsPerPlayer",    3);
            Scribe_Values.Look(ref DefaultClaimRadius,    "defaultClaimRadius",    25);
            Scribe_Values.Look(ref BufferZoneWidth,       "bufferZoneWidth",       5);
            Scribe_Values.Look(ref TradeTimerSeconds,     "tradeTimerSeconds",     0);
            Scribe_Values.Look(ref GravshipCountdownSeconds,   "gravshipCountdown", 30);
            Scribe_Values.Look(ref GravshipEmergencyCountdown, "gravshipEmergency", 10);
            Scribe_Values.Look(ref ThreatScalePerPlayer,  "threatScale",           0.6f);
        }
    }

    public class RcMod : Mod
    {
        public static RcSettings Settings = null!;

        public RcMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<RcSettings>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            { Text.Font = GameFont.Medium; listing.Label("Territory Settings"); Text.Font = GameFont.Small; }
            listing.Gap(6f);

            listing.CheckboxLabeled(
                "Strict Zone Borders — buildings must fit entirely within one claim",
                ref Settings.StrictZoneBorders);

            listing.CheckboxLabeled(
                "Temporary Claim Bubbles — protect trade/caravan drops outside claims",
                ref Settings.TemporaryClaimBubbles);

            listing.Gap(4f);
            listing.Label($"Max claims per player: {Settings.MaxClaimsPerPlayer}");
            Settings.MaxClaimsPerPlayer = (int)listing.Slider(
                Settings.MaxClaimsPerPlayer, 1, 10);

            listing.Label($"Default claim radius: {Settings.DefaultClaimRadius} tiles");
            Settings.DefaultClaimRadius = (int)listing.Slider(
                Settings.DefaultClaimRadius, 10, 50);

            listing.Label($"Buffer zone width: {Settings.BufferZoneWidth} tiles");
            Settings.BufferZoneWidth = (int)listing.Slider(
                Settings.BufferZoneWidth, 0, 20);

            listing.Gap(12f);
            { Text.Font = GameFont.Medium; listing.Label("Trade Settings"); Text.Font = GameFont.Small; }
            listing.Gap(6f);

            string timerLabel = Settings.TradeTimerSeconds == 0
                ? "Trade time limit: OFF"
                : $"Trade time limit: {Settings.TradeTimerSeconds}s";
            listing.Label(timerLabel);
            Settings.TradeTimerSeconds = (int)listing.Slider(
                Settings.TradeTimerSeconds, 0, 300);

            listing.Gap(12f);
            { Text.Font = GameFont.Medium; listing.Label("Gravship Settings"); Text.Font = GameFont.Small; }
            listing.Gap(6f);

            listing.Label($"Launch countdown: {Settings.GravshipCountdownSeconds}s");
            Settings.GravshipCountdownSeconds = (int)listing.Slider(
                Settings.GravshipCountdownSeconds, 10, 120);

            listing.Label($"Emergency launch countdown: {Settings.GravshipEmergencyCountdown}s");
            Settings.GravshipEmergencyCountdown = (int)listing.Slider(
                Settings.GravshipEmergencyCountdown, 5, 30);

            listing.Gap(12f);
            { Text.Font = GameFont.Medium; listing.Label("Event Settings"); Text.Font = GameFont.Small; }
            listing.Gap(6f);

            listing.Label($"Threat scale per additional player: {Settings.ThreatScalePerPlayer:F1}×");
            Settings.ThreatScalePerPlayer = listing.Slider(
                Settings.ThreatScalePerPlayer, 0f, 1.5f);

            listing.End();
        }

        public override string SettingsCategory() => "RimClaim";
    }
}
