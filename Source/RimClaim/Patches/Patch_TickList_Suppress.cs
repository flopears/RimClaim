using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace RimClaim.Patches
{
    [HarmonyPatch(typeof(TickList), nameof(TickList.Tick))]
    public static class Patch_TickList_Suppress
    {
        static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            var tickMethod = AccessTools.Method(typeof(Entity), "Tick");
            var tickRareMethod = AccessTools.Method(typeof(Entity), "TickRare");
            var tickLongMethod = AccessTools.Method(typeof(Entity), "TickLong");

            var shouldSkip = AccessTools.Method(
                typeof(Patch_TickList_Suppress), nameof(ShouldSkipEntity));

            var codes = new List<CodeInstruction>(instructions);
            int patched = 0;

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Callvirt &&
                    codes[i].operand is MethodInfo mi &&
                    (mi == tickMethod || mi == tickRareMethod || mi == tickLongMethod))
                {
                    // IL before this point: the Entity/Thing is on the stack
                    // Insert: dup, call ShouldSkipEntity, brtrue skip, [original callvirt], br done, skip: pop, done:
                    var skipLabel = il.DefineLabel();
                    var doneLabel = il.DefineLabel();

                    var insert = new List<CodeInstruction>
                    {
                        new(OpCodes.Dup),
                        new(OpCodes.Call, shouldSkip),
                        new(OpCodes.Brtrue_S, skipLabel),
                        codes[i], // original callvirt
                        new(OpCodes.Br_S, doneLabel),
                        new(OpCodes.Pop) // skip: remove the dup'd entity
                    };

                    insert[5].labels.Add(skipLabel);

                    // The instruction AFTER the original callvirt gets the done label
                    if (i + 1 < codes.Count)
                        codes[i + 1].labels.Add(doneLabel);

                    codes.RemoveAt(i);
                    codes.InsertRange(i, insert);
                    i += insert.Count;
                    patched++;
                }
            }

            Log.Message($"[RimClaim] TickList.Tick transpiler: {patched} call sites patched.");
            return codes;
        }

        public static bool ShouldSkipEntity(Entity entity)
        {
            if (!LandclaimRegistry.AnyPausedZones) return false;
            if (TickSuppressor.InExtraTick) return false;
            if (!MP.IsInMultiplayer) return false;
            if (entity is not Thing thing || !thing.Spawned) return false;
            var reg = LandclaimRegistry.For(thing.Map);
            if (reg == null) return false;
            var zone = reg.GetZoneAt(thing.Position);
            return zone != null && zone.localTickRate == 0;
        }
    }
}
