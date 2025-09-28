using System.Collections.Generic;
using System.Linq;
using Hospitality.Utilities;
using JetBrains.Annotations;
using LudeonTK;
using RimWorld;
using Verse;
using Verse.AI.Group;
using GuestUtility = Hospitality.Utilities.GuestUtility;

namespace Hospitality;

public class Hospitality_MapComponent : MapComponent
{
    public bool askForSafety = true;
    public Area defaultAreaRestriction;
    public Area defaultAreaShopping;
    public bool defaultEntertain;
    public bool defaultMakeFriends;
    public DrugPolicy drugPolicy;
    public bool guestsAreWelcome = true;
    public bool guestsCanTakeFoodForFree;
    private IncidentQueue incidentQueue = new();
    private int nextGuestListCheck;
    private int nextQueueInspection;
    private int nextRogueGuestCheck;
    public bool refuseGuestsUntilWeHaveBeds;

    public Hospitality_MapComponent(Map map) : base(map)
    {
        defaultAreaRestriction = map.areaManager.Home;
        RelationsCache = new RelationsCache(map);
    }

    [NotNull] public List<Lord> PresentLords { get; } = [];
    [NotNull] public HashSet<Pawn> PresentGuests { get; } = [];
    [NotNull] public RelationsCache RelationsCache { get; }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref defaultEntertain, "defaultEntertain");
        Scribe_Values.Look(ref defaultMakeFriends, "defaultMakeFriends");
        Scribe_Values.Look(ref guestsAreWelcome, "guestsAreWelcome", true);
        Scribe_Values.Look(ref askForSafety, "askForSafety", true);
        Scribe_References.Look(ref defaultAreaRestriction, "defaultAreaRestriction");
        Scribe_References.Look(ref defaultAreaShopping, "defaultAreaShopping");
        Scribe_Deep.Look(ref incidentQueue, "incidentQueue");
        Scribe_Values.Look(ref refuseGuestsUntilWeHaveBeds, "refuseGuestsUntilWeHaveBeds");
        Scribe_Values.Look(ref guestsCanTakeFoodForFree, "guestsCanTakeFoodForFree");
        Scribe_Values.Look(ref nextQueueInspection, "nextQueueInspection");
        Scribe_Deep.Look(ref drugPolicy, "drugPolicy");
        defaultAreaRestriction ??= map.areaManager.Home;
    }

    public override void FinalizeInit()
    {
        MapComponentCache.Register(this);
        Mod_Hospitality.ToggleTabIfNeeded();
        Mod_Hospitality.UpdateMainButtonIcon();
    }

    public void RefreshGuestListTotal()
    {
        PresentLords.Clear();

        // Add comprehensive null checking for all potential null references
        if (map?.lordManager?.lords != null)
        {
            PresentLords.AddRange(map.lordManager.lords
                .Where(l => l != null &&
                           l.LordJob != null &&
                           l.LordJob is LordJob_VisitColony visit &&
                           !visit.leaving));
        }

        MainTabWindowUtility.NotifyAllPawnTables_PawnsChanged();

        PresentGuests.Clear();
        if (PresentLords.Count > 0)
        {
            // Add null checking for lord.ownedPawns as well
            PresentGuests.AddRange(PresentLords
                .Where(l => l?.ownedPawns != null)
                .SelectMany(l => l.ownedPawns)
                .Where(p => p != null)); // Also ensure individual pawns aren't null
        }
    }




    public void OnLordSpawned(Lord lord)
    {
        if (lord == null) return;

        PresentLords.AddDistinct(lord);

        PresentGuests.Clear();
        PresentGuests.AddRange(PresentLords
            .Where(l => l?.ownedPawns != null)
            .SelectMany(l => l.ownedPawns)
            .Where(p => p != null));
    }

    public void OnLordDespawned(Lord lord)
    {
        if (lord == null) return;

        PresentLords.Remove(lord);

        PresentGuests.Clear();
        PresentGuests.AddRange(PresentLords
            .Where(l => l?.ownedPawns != null)
            .SelectMany(l => l.ownedPawns)
            .Where(p => p != null));
    }

    public void OnGuestAdopted(Pawn guest)
    {
        if (guest == null) return;
        PresentGuests.Remove(guest);
    }

    public void OnGuestJoinedLate(Pawn guest)
    {
        if (guest == null) return;
        PresentGuests.Add(guest);
    }

    public void OnWorldLoaded()
    {
        try
        {
            RefreshGuestListTotal();
            CheckForCorrectDrugPolicies();
            ApplyCorrectFoodRestrictions();
            AddOrRemoveNeedsAsAppropriate();
            AddDynamicComponents();
        }
        catch (System.Exception e)
        {
            Log.Error($"[Hospitality] Error in OnWorldLoaded: {e}");
        }
    }

    [DebugAction("Pawns", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static void ShowFoodRestrictions(Pawn pawn)
    {
        Log.Message($"Game restrictions = {Current.Game.foodRestrictionDatabase.AllFoodRestrictions.Select(r => r.label).ToCommaList()}");
        Log.Message($"Default restriction = {Current.Game.GetComponent<Hospitality_GameComponent>().defaultFoodRestriction?.label}");

        if (pawn.foodRestriction == null) Log.Message($"{pawn.NameShortColored}: Food restriction is null.");
        else if (pawn.foodRestriction.curPolicy == null) Log.Message($"{pawn.NameShortColored}: curRestriction is null.");
        else
        {
            Log.Message($"{pawn.NameShortColored}: curRestriction is {pawn.foodRestriction.curPolicy.label}");
            var dialog = new Dialog_ManageFoodPolicies(pawn.foodRestriction.CurrentFoodPolicy);
            Find.WindowStack.Add(dialog);
            dialog.policyInt = pawn.foodRestriction.CurrentFoodPolicy;
        }
    }

    private void AddDynamicComponents()
    {
        foreach (var pawn in PresentGuests.ToList()) // ToList to avoid modification during iteration
        {
            if (pawn?.reading == null)
            {
                try
                {
                    pawn.reading = new Pawn_ReadingTracker(pawn);
                }
                catch (System.Exception e)
                {
                    Log.Error($"[Hospitality] Failed to add reading tracker to {pawn}: {e}");
                }
            }
        }
    }

    private void AddOrRemoveNeedsAsAppropriate()
    {
        foreach (var pawn in PresentGuests.ToList())
        {
            if (pawn?.needs != null)
            {
                try
                {
                    pawn.needs.AddOrRemoveNeedsAsAppropriate();
                }
                catch (System.Exception e)
                {
                    Log.Error($"[Hospitality] Failed to update needs for {pawn}: {e}");
                }
            }
        }
    }

    private void ApplyCorrectFoodRestrictions()
    {
        var defaultPolicy = Current.Game?.GetComponent<Hospitality_GameComponent>()?.defaultFoodRestriction;

        foreach (var pawn in PresentGuests.ToList())
        {
            if (pawn == null) continue;

            try
            {
                pawn.foodRestriction ??= new Pawn_FoodRestrictionTracker(pawn);
                if (defaultPolicy != null)
                {
                    pawn.foodRestriction.CurrentFoodPolicy = defaultPolicy;
                }
            }
            catch (System.Exception e)
            {
                Log.Error($"[Hospitality] Failed to set food restriction for {pawn}: {e}");
            }
        }
    }

    public void CheckForCorrectDrugPolicies()
    {
        var policy = GetDrugPolicy();
        foreach (var pawn in PresentGuests)
        {
            if (pawn?.drugs == null) continue;
            pawn.drugs.CurrentPolicy = policy;
        }
    }

    public override void MapComponentTick()
    {
        base.MapComponentTick();

        incidentQueue ??= new IncidentQueue();
        incidentQueue.IncidentQueueTick();

        if (GenTicks.TicksGame > nextQueueInspection)
        {
            nextQueueInspection = GenTicks.TicksGame + GenDate.TicksPerDay;
            if (incidentQueue.Count <= 1) GenericUtility.FillIncidentQueue(map);
            GenericUtility.CheckTooManyIncidentsAtOnce(incidentQueue);
        }

        if (GenTicks.TicksGame > nextRogueGuestCheck)
        {
            nextRogueGuestCheck = GenTicks.TicksGame + GenDate.TicksPerHour;
            GuestUtility.CheckForRogueGuests(map);
        }

        if (GenTicks.TicksGame > nextGuestListCheck)
        {
            nextGuestListCheck = GenTicks.TicksGame + GenDate.TicksPerDay / 4;
            RefreshGuestListTotal();
        }
    }

    public void QueueIncident(FiringIncident incident, float afterDays)
    {
        var qi = new QueuedIncident(incident, (int)(Find.TickManager.TicksGame + GenDate.TicksPerDay * afterDays));
        incidentQueue.Add(qi);
        //Log.Message("Queued Hospitality incident after " + afterDays + " days. Queue has now " + incidentQueue.Count + " items.");
    }

    public QueuedIncident GetNextVisit(Faction faction)
    {
        QueuedIncident nearest = null;

        // Find earliest
        foreach (QueuedIncident incident in incidentQueue)
        {
            if (incident.FiringIncident.parms.faction == faction)
            {
                if (nearest == null || incident.FireTick < nearest.FireTick) nearest = incident;
            }
        }

        return nearest;
    }

    public static void RefuseGuestsUntilWeHaveBeds(Map map)
    {
        if (map == null) return;

        var mapComp = map.GetMapComponent();
        mapComp.refuseGuestsUntilWeHaveBeds = true;
        LessonAutoActivator.TeachOpportunity(ConceptDef.Named("GuestBeds"), null, OpportunityType.Important);
    }

    public DrugPolicy GetDrugPolicy()
    {
        drugPolicy ??= new DrugPolicy(map.uniqueID, "GuestDrugPolicy");
        drugPolicy.InitializeIfNeeded();

        for (var i = 0; i < drugPolicy.Count; i++)
        {
            var entry = drugPolicy[i];
            var properties = entry.drug.GetCompProperties<CompProperties_Drug>();
            if (entry.drug.IsPleasureDrug && properties?.addictiveness < 0.025f && !properties.CanCauseOverdose)
            {
                entry.allowedForJoy = true;
                //Log.Message($"Hospitality: Guests may use {entry.drug.label} for joy.");
            }
        }

        return drugPolicy;
    }

    public IEnumerable<QueuedIncident> GetQueuedVisits(float withinDays)
    {
        return incidentQueue.queuedIncidents.Where(i => (i.FireTick - GenTicks.TicksGame + 0f) / GenDate.TicksPerDay < withinDays);
    }
}