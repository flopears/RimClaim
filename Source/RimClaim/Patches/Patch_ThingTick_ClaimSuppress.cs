namespace RimClaim.Patches
{
    public static class TickSuppressor
    {
        [System.ThreadStatic]
        public static bool InExtraTick;
    }
}
