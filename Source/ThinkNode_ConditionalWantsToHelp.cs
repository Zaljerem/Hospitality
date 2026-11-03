using Verse;
using Verse.AI;

namespace Hospitality
{
    public class ThinkNode_ConditionalWantsToHelp : ThinkNode_Conditional
    {
        public float requiredHappiness = 0.65f;
        
        public override bool Satisfied(Pawn pawn)
        {
            if (ModSettings_Hospitality.disableWork) return false;
            if (pawn.needs?.mood == null) return false;
            var isHappy = pawn.needs.mood.CurLevel > requiredHappiness;
            // See JobGiver_Work_Patch for more complex formula
            return isHappy;
        }
    }
}