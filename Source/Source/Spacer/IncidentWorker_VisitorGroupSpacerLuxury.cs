using Verse;

namespace Hospitality.Spacer
{
    public class IncidentWorker_VisitorGroupSpacerLuxury : IncidentWorker_VisitorGroupSpacer
    {
        protected override int GetGroupSize()
        {
            return Rand.Range(2, 6);
        }
    }
}
