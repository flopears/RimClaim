using Multiplayer.API;

namespace RimClaim
{
    /// <summary>
    /// Registers custom sync workers with the Multiplayer API for types
    /// that are passed as SyncMethod parameters but aren't primitives,
    /// Things, or Defs.
    ///
    /// Called once from ModInit when MP.enabled is true (Multiplayer DLL loaded).
    ///
    /// NOTE: UnityEngine.Color is already handled by Multiplayer's built-in
    /// sync workers, so we do NOT register one for it (doing so logs a warning
    /// and is ignored). Enums like DiplomacyState are synced automatically as
    /// their underlying int, so they don't strictly need a worker either, but
    /// we register it explicitly for clarity and forward-compatibility.
    /// </summary>
    public static class SyncWorkerRegistration
    {
        public static void RegisterAll()
        {
            // DiplomacyState enum — explicit for clarity.
            MP.RegisterSyncWorker<DiplomacyState>(
                SyncDiplomacyState, shouldConstruct: false);
        }

        private static void SyncDiplomacyState(SyncWorker sync, ref DiplomacyState state)
        {
            if (sync.isWriting)
                sync.Write((int)state);
            else
                state = (DiplomacyState)sync.Read<int>();
        }
    }
}
