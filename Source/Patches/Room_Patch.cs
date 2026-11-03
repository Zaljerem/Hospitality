using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Hospitality.Utilities;
using RimWorld;
using Verse;

namespace Hospitality.Patches
{
    [HarmonyPatch(typeof(Room), "get_Owners")]
    public static class Room_Owners_Patch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var targetField = AccessTools.Field(typeof(RoomRoleDefOf), "Bedroom");
            var guestRoomField = AccessTools.Field(typeof(InternalDefOf), nameof(InternalDefOf.GuestRoom));
            var getRoleMethod = AccessTools.Property(typeof(Room), "Role").GetGetMethod();

            for (int i = 0; i < codes.Count; i++)
            {
                yield return codes[i];

                if (
                    i > 1 &&
                    codes[i - 1].LoadsField(targetField) &&
                    codes[i].opcode == OpCodes.Beq_S // look for branch to skip if not Bedroom
                )
                {
                    var branchTarget = codes[i].operand;

                    // Inject GuestRoom equivalency
                    yield return new CodeInstruction(OpCodes.Ldarg_0); // load 'this'
                    yield return new CodeInstruction(OpCodes.Call, getRoleMethod); // get Role
                    yield return new CodeInstruction(OpCodes.Ldsfld, guestRoomField); // GuestRoom
                    yield return new CodeInstruction(OpCodes.Beq_S, branchTarget); // if equal, jump
                }
            }
        }
    }

}
