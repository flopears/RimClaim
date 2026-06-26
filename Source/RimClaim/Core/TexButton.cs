using UnityEngine;
using Verse;

namespace RimClaim
{
    /// <summary>
    /// Lazy-loaded texture references. Uses BaseContent.BadTex as fallback
    /// so missing textures don't crash the mod — they just show a pink square.
    /// Replace placeholder PNGs in Textures/RC/UI/ with real art when ready.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class TexButton
    {
        public static readonly Texture2D RC_Claim     = LoadTex("RC/UI/Claim");
        public static readonly Texture2D RC_Unclaim   = LoadTex("RC/UI/Unclaim");
        public static readonly Texture2D RC_Share     = LoadTex("RC/UI/Share");
        public static readonly Texture2D RC_Unshare   = LoadTex("RC/UI/Unshare");
        public static readonly Texture2D RC_Team      = LoadTex("RC/UI/Team");
        public static readonly Texture2D RC_Enemy     = LoadTex("RC/UI/Enemy");
        public static readonly Texture2D RC_Peace     = LoadTex("RC/UI/Peace");
        public static readonly Texture2D RC_Player    = LoadTex("RC/UI/Player");
        public static readonly Texture2D RC_SpeedHook = LoadTex("RC/UI/SpeedHook");
        public static readonly Texture2D RC_Warning      = LoadTex("RC/UI/Warning");
        public static readonly Texture2D RC_ClaimOverlay = LoadTex("RC/UI/ClaimOverlay");

        private static Texture2D LoadTex(string path)
        {
            var tex = ContentFinder<Texture2D>.Get(path, reportFailure: false);
            if (tex == null)
            {
                Log.Warning($"[RimClaim] Texture not found: {path} — using fallback.");
                return BaseContent.BadTex;
            }
            return tex;
        }
    }
}
