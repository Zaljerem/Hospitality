using Hospitality.Utilities;
using RimWorld;
using Verse;
using Verse.AI.Group;
using GuestUtility = Hospitality.Utilities.GuestUtility;

namespace Hospitality;

public class LordToil_Obsolete : LordToil
{
    public override void UpdateAllDuties()
    {
    }
}

public class LordJob_VisitColony : LordJob
{
    private int checkEventId = -1;
    private IntVec3 chillSpot;
    private Faction faction;
    public bool getUpsetWhenLost;
    public bool leaving;
    // Transport integrations must not treat recovery on this map as a fresh inbound arrival.
    // Persist the origin so they can make the same distinction when restoring a saved visit.
    public bool recoveredOnMap;
    private int stayDuration;

    public LordJob_VisitColony()
    {
        // Required
    }

    public LordJob_VisitColony(Faction faction, IntVec3 chillSpot, int stayDuration, bool getUpsetWhenLost)
    {
        this.faction = faction;
        this.chillSpot = chillSpot;
        this.stayDuration = stayDuration;
        this.getUpsetWhenLost = getUpsetWhenLost;
        leaving = false;
    }

    public override bool NeverInRestraints => true;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_References.Look(ref faction, "faction");
        Scribe_Values.Look(ref chillSpot, "chillSpot");
        Scribe_Values.Look(ref checkEventId, "checkEventId", -1);
        Scribe_Values.Look(ref stayDuration, "stayDuration", GenDate.TicksPerDay);
        Scribe_Values.Look(ref getUpsetWhenLost, "getUpsetWhenLost", true);
        Scribe_Values.Look(ref leaving, "leaving");
        Scribe_Values.Look(ref recoveredOnMap, "recoveredOnMap", false);
    }

    public void OnLeaveTriggered()
    {
        leaving = true;
    }

    public override void Notify_LordDestroyed()
    {
        GuestUtility.OnLordDespawned(lord);
        base.Notify_LordDestroyed();
    }

    public override StateGraph CreateGraph()
    {
        var graphArrive = new StateGraph();
        var graphExit = new LordJob_TravelAndExit(IntVec3.Invalid).CreateGraph();
        var travelGraph = new LordJob_Travel(chillSpot).CreateGraph();
        travelGraph.StartingToil = new LordToil_CustomTravel(chillSpot, 0.49f, 50);
        // Arriving
        var toilArriving = graphArrive.AttachSubgraph(travelGraph).StartingToil;
        // Visiting
        var toilVisiting = new LordToil_VisitPoint();
        graphArrive.lordToils.Add(toilVisiting);
        // Exit
        var toilExit = graphArrive.AttachSubgraph(graphExit).StartingToil;
        // Leave map
        var toilLeaveMap = graphExit.lordToils[1];
        // Take wounded
        LordToil toilTakeWounded = new LordToil_TakeWoundedGuest { lord = lord }; // This fixes the issue of missing lord when showing leave message
        graphExit.AddToil(toilTakeWounded);
        // Kept to avoid breaking saves
        graphArrive.AddToil(new LordToil_Obsolete());

        // Arrived
        {
            var t1 = new Transition(toilArriving, toilVisiting);
            t1.triggers.Add(new Trigger_Memo("TravelArrived"));
            t1.AddPreAction(new TransitionAction_EndAllJobs());
            graphArrive.transitions.Add(t1);
        }
        // Too cold / hot
        {
            var t6 = new Transition(toilArriving, toilExit);
            t6.AddTrigger(new Trigger_PawnExperiencingDangerousTemperatures());
            // Suppress this temperature message on space maps, including orbital sites
            // with another biome. Actual heatstroke/hypothermia still triggers departure.
            // Retain the action/trigger positions so existing saved graphs stay compatible.
            t6.AddPreAction(new TransitionAction_Message("MessageVisitorsDangerousTemperature".Translate(faction?.def.pawnsPlural.CapitalizeFirst(), faction?.Name),
                MessageTypeDefOf.NeutralEvent, canSendMessage: () => !SpaceMapUtility.IsSpaceMap(Map)));
            t6.AddPreAction(new TransitionAction_EnsureHaveNearbyExitDestination());
            t6.AddPostAction(new TransitionAction_EndAllJobs());
            graphArrive.AddTransition(t6);
        }
        // Became enemy while arriving
        {
            var t3 = new Transition(toilVisiting, toilLeaveMap);
            t3.triggers.Add(new Trigger_BecamePlayerEnemy());
            t3.preActions.Add(new TransitionAction_SetDefendLocalGroup());
            t3.postActions.Add(new TransitionAction_WakeAll());
            graphArrive.transitions.Add(t3);
        }
        // Leave if became angry
        {
            var t4 = new Transition(toilArriving, toilExit);
            t4.triggers.Add(new Trigger_BecamePlayerEnemy());
            t4.triggers.Add(new Trigger_VisitorsAngeredMax(IncidentWorker_VisitorGroup.MaxAngerAmount(faction?.PlayerGoodwill ?? 0)));
            t4.preActions.Add(new TransitionAction_EnsureHaveNearbyExitDestination());
            t4.postActions.Add(new TransitionAction_WakeAll());
            graphArrive.transitions.Add(t4);
        }
        // Leave if stayed long enough
        {
            var t5 = new Transition(toilVisiting, toilExit);
            t5.triggers.Add(new Trigger_TicksPassedAndOkayToLeave(stayDuration));
            t5.triggers.Add(new Trigger_SentAway()); // Sent away during stay
            t5.preActions.Add(new TransitionAction_Message("VisitorsLeaving".Translate(faction?.Name)));
            t5.preActions.Add(new TransitionAction_EnsureHaveNearbyExitDestination());
            t5.postActions.Add(new TransitionAction_WakeAll());
            graphArrive.transitions.Add(t5);
        }
        // Leave if sent away (before fully arriving!)
        {
            var t7 = new Transition(toilArriving, toilExit);
            t7.triggers.Add(new Trigger_SentAway());
            t7.preActions.Add(new TransitionAction_Message("VisitorsLeaving".Translate(faction?.Name)));
            t7.preActions.Add(new TransitionAction_WakeAll());
            t7.preActions.Add(new TransitionAction_SendAway());
            graphArrive.transitions.Add(t7);
        }
        // Take wounded guest when arriving
        {
            var t8 = new Transition(toilArriving, toilTakeWounded);
            t8.AddTrigger(new Trigger_WoundedGuestPresent());
            t8.AddPreAction(new TransitionAction_Message("MessageVisitorsTakingWounded".Translate(faction?.def.pawnsPlural.CapitalizeFirst(), faction?.Name)));
            graphArrive.AddTransition(t8);
        }
        // Take wounded guest when leaving - couldn't get this to work
        //{
        //Transition t9 = new Transition(toilExit, toilTakeWounded);
        //t9.AddTrigger(new Trigger_WoundedGuestPresent());
        //t9.AddPreAction(new TransitionAction_Message("MessageVisitorsTakingWounded".Translate(faction.def.pawnsPlural.CapitalizeFirst(), faction.Name)));
        //graphExit.AddTransition(t9);
        //}

        return graphArrive;
    }

    public override void Notify_PawnLost(Pawn pawn, PawnLostCondition condition)
    {
        if (condition == PawnLostCondition.ExitedMap) return;

        pawn.ownership.UnclaimAll();

        if (!lord.ownedPawns.Any())
        {
            GuestUtility.OnLostEntireGroup(lord);
        }
    }
}
