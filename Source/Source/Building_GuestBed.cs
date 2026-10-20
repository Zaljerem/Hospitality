using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Hospitality.Utilities;
using RimWorld;
//using UnityEngine;
using Waylith.Math;
using Verse;
using BedUtility = Hospitality.Utilities.BedUtility;
using GuestUtility = Hospitality.Utilities.GuestUtility;

// Waylith equivalents needed ... Building_Bed still uses UnityEngine though, so ... - UPDATE

namespace Hospitality;

public class Building_GuestBed : Building_Bed
{
    internal const int FeeStep = 5;
    private static readonly UnityEngine.Color sheetColorForGuests = new(89 / 255f, 55 / 255f, 121 / 255f);

    public int previousRoyaltyUpdate;

    private int rentalFee;

    // Temporary developer A/B test; intentionally not saved so loading restores guest coloring.
    internal bool debugUseBaseSheetColor;

    public int MoodEffect => Mathf.RoundToInt(rentalFee * -0.1f);

    public override UnityEngine.Color DrawColor => def.MadeFromStuff ? base.DrawColor : DrawColorTwo;

    public override UnityEngine.Color DrawColorTwo => debugUseBaseSheetColor ? base.DrawColorTwo : sheetColorForGuests;

    public BedStats Stats { get; } = new();

    internal int RentalFee
    {
        get => rentalFee;
        set => SetRentalFee(value);
    }

    public int GetRentalFee()
    {
        return rentalFee;
    }

    public void SetRentalFee(int value)
    {
        rentalFee = Mathf.Clamp(value, 0, int.MaxValue);
        UpdateStats();
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref rentalFee, "RentalFee");
    }

    public override void TickLong()
    {
        // Optional bedding filters still need maintenance despite this bed's long-tick scheduling.
        SoftWarmBedsCompatibility.TickBed(this);
        UpdateStats();

        // Remove owners that weren't properly cleared
        foreach (var pawn in CompAssignableToPawn.AssignedPawnsForReading.ToArray())
        {
            if (pawn == null || !pawn.Spawned || pawn.Dead) CompAssignableToPawn.TryUnassignPawn(pawn);
        }
    }

    public override void SpawnSetup(Map map, bool respawningAfterLoad)
    {
        base.SpawnSetup(map, respawningAfterLoad);

        UpdateStats();
    }

    private void UpdateStats()
    {
        Stats.lastCalculated = GenTicks.TicksGame;
        try
        {
            var owners = this.Owners().Count == 0 ? (string)"Nobody".Translate() : this.Owners().Select(o => (string)o.NameShortColored).ToCommaList(true);
            Stats.title = $"{def.LabelCap} ({owners})";
            Stats.staticBedValue = BedUtility.StaticBedValue(this, out Stats.room, out _, out _, out _, out _, out _);
            Stats.attractiveness = Mathf.CeilToInt(BedUtility.ScoreFactor * Stats.staticBedValue - rentalFee);
        }
        catch (Exception e)
        {
            Log.ErrorOnce($"Failed to calculate stats: {e}", 834763462);
        }
    }

    public void UpdateRoyaltyStats()
    {
        if (!ModsConfig.RoyaltyActive) return;

        var time = DateTime.Now.Second;

        if (time != previousRoyaltyUpdate)
        {
            Stats.metRoyalTitles = GetMetRoyalTitles(Stats.room);
            Stats.textNextTitleReq = GetNextTitleReq(Stats.room, Stats.metRoyalTitles);

            previousRoyaltyUpdate = time;
        }
    }

    private string GetNextTitleReq(Room room, RoyalTitleDef[] metRoyalTitles)
    {
        if (room == null) return string.Empty;
        if (GuestUtility.AllTitles.NullOrEmpty()) return string.Empty;
        if (!IsSingleBedroom(room)) return "NextTitleRequirements".Translate("BedroomMustBeSingle".Translate());
        foreach (var title in GuestUtility.AllTitles.Where(t => !metRoyalTitles.Contains(t) && t.bedroomRequirements != null).OrderBy(t => t.seniority))
        {
            return "NextTitleRequirements".Translate(title.bedroomRequirements.Where(req => !req.Met(room)).Select(req => req.Label(room)).ToCommaList(true));
        }

        return string.Empty;
    }

    private RoyalTitleDef[] GetMetRoyalTitles(Room room) // This causes the slowdown when there are lots of guest beds. Pref to cache these values. ~30ms per call
    {
        try
        {
            if (room != null)
            {
                // All titles inspect the same room during this refresh. Scan its beds once,
                // rather than once for each title with bedroom requirements.
                var isSingleBedroom = IsSingleBedroom(room);
                return GuestUtility.AllTitles.Where(t => t.bedroomRequirements.NullOrEmpty() || (isSingleBedroom && t.bedroomRequirements.All(req => req.Met(room)))).ToArray();
            }
        }
        catch (Exception e)
        {
            Log.Error($"Failed to read royalty titles or their bedroom requirements. This means you are using a mod that changes these and broke them.\n{e}");
        }

        return Array.Empty<RoyalTitleDef>();
    }

    private bool IsSingleBedroom(Room room)
    {
        return !room.ContainedBeds.Any(containedBed => containedBed != this && containedBed.def.building.bed_humanlike);
    }

    public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
    {
        // Creating copy for iteration, since list changes during loop
        foreach (var owner in this.Owners().ToArray())
        {
            owner.ownership.UnclaimBed();
        }

        var district = this.GetDistrict();
        base.DeSpawn(mode);
        if (district != null)
        {
            district.Notify_RoomShapeOrContainedBedsChanged();
            district.Room.Notify_RoomShapeChanged();
        }
    }

    public override string GetInspectString()
    {
        var stringBuilder = new StringBuilder();

        var fromComps = InspectStringPartsFromComps();
        if (!string.IsNullOrWhiteSpace(fromComps))
        {
            stringBuilder.AppendLine(fromComps);
        }

        var owners = this.Owners();
        if (!owners.Any())
        {
            stringBuilder.Append($"{"Owner".Translate()}: {"Nobody".Translate()}");
        }
        else if (owners.Count == 1)
        {
            stringBuilder.Append($"{"Owner".Translate()}: {owners[0].LabelShortCap}");
        }
        else
        {
            stringBuilder.Append("Owners".Translate() + ": ");
            var notFirst = false;
            foreach (var owner in owners)
            {
                if (owner == null) continue; // should already be filtered by this.Owners
                if (notFirst)
                {
                    stringBuilder.Append(", ");
                }

                notFirst = true;
                stringBuilder.Append(owner.LabelShortCap);
            }
            //if(notFirst) stringBuilder.AppendLine();
        }

        // This override bypasses Building_Bed.GetInspectString, where SWB normally adds its
        // bedding status. Reuse that optional integration without duplicating an existing postfix.
        return SoftWarmBedsCompatibility.AppendInspectString(this, stringBuilder.ToString());
    }

    // Note to whoever wants to add to this method (hi jptrrs!):
    // You can just do
    // foreach (Gizmo c in base.GetGizmos())
    // {
    //    yield return c;
    // }
    // yield return [whatever you want to add];
    //
    // No need to copy this method.
    public override IEnumerable<Gizmo> GetGizmos()
    {
        // Display the original gizmos (includes the swap guest bed button via patch)
        foreach (var gizmo in base.GetGizmos())
        {
            switch (gizmo)
            {
                case Command_SetBedOwnerType o:
                {
                    o.Disable();
                    break;
                }
                case Command_Toggle toggle:
                {
                    // Disable prisoner and medical buttons
                    if (toggle.defaultLabel == "CommandBedSetForPrisonersLabel".Translate() || toggle.defaultLabel == "CommandBedSetAsMedicalLabel".Translate()) gizmo.Disable();
                    break;
                }
                case Command_Action action:
                {
                    // Disable set owner button
                    if (action.defaultLabel == "CommandThingSetOwnerLabel".Translate()) action.Disable();
                    break;
                }
            }

            yield return gizmo;
        }

        // Gizmo for drawing guest room info
        var beds = Find.Selector.SelectedObjects.OfType<Building_GuestBed>().ToArray();
        foreach (var bed in beds)
        {
            if (bed.Stats.lastCalculated == 0 || bed.Stats.room == null) bed.UpdateStats();
        }

        yield return new Gizmo_GuestBed(beds);

        // Optional developer capture for SWB blankets that stay made but stop appearing.
        // Its temporary render probe is installed only when explicitly started with this button.
        if (Prefs.DevMode)
        {
            yield return GuestBeddingDiagnostics.Command(this);
            yield return GuestBeddingDiagnostics.ColorCommand(this);
        }

        // Get base def
        var defName = def.defName.ReplaceFirst("Guest", string.Empty);
        var baseDef = DefDatabase<ThingDef>.GetNamed(defName);

        // Add build copy command
        var buildCopy = BuildCopyCommandUtility.BuildCopyCommand(baseDef, Stuff, null, null, false);
        if (buildCopy != null) yield return buildCopy;
    }

    public override void PostMake()
    {
        base.PostMake();
        PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDef.Named("GuestBeds"), KnowledgeAmount.Total);
    }

    public override void DrawGUIOverlay()
    {
        if (Find.CameraDriver.CurrentZoom == CameraZoomRange.Closest)
        {
            var defaultThingLabelColor = GenMapUI.DefaultThingLabelColor;

            var owners = this.Owners();
            if (!owners.Any())
            {
                GenMapUI.DrawThingLabel(this, ((float)rentalFee).ToStringMoney(), defaultThingLabelColor);
            }
            else if (owners.Count == 1)
            {
                if (owners[0].InBed() && owners[0].CurrentBed() == this) return;
                GenMapUI.DrawThingLabel(this, owners[0].LabelShort, defaultThingLabelColor);
            }
            else
            {
                for (var index = 0; index < owners.Count; ++index)
                {
                    if (!owners[index].InBed() || owners[index].CurrentBed() != this || !(owners[index].Position == GetSleepingSlotPos(index)))
                    {
                        var pos = GetMultiOwnersLabelScreenPosFor(index);
                        GenMapUI.DrawThingLabel(pos, owners[index].LabelShort, defaultThingLabelColor);
                    }
                }
            }
        }
    }

    public static void Swap(Building_Bed bed)
    {
        if (bed == null) return;

        var forPrisoners = bed.ForPrisoners; // Store this, since it changes during spawn

        Building_Bed newBed;
        if (bed.IsGuestBed())
        {
            newBed = (Building_Bed)MakeBed(bed, bed.def.defName.Split(["Guest"], StringSplitOptions.RemoveEmptyEntries)[0]);
            newBed.Medical = false;
            newBed.ForPrisoners = false;
        }
        else
        {
            newBed = (Building_GuestBed)MakeBed(bed, bed.def.defName + "Guest");
            forPrisoners = false; // never for prisoners
        }

        // Prepare bedding preservation before changing owners or spawning the replacement.
        // Removing only this comp from the old bed prevents SWB's despawn hook from unmaking
        // the bedding. The same comp keeps its loaded blanket/material/filter/flick state.
        using var beddingTransfer = BedComponentTransfer.Begin(bed, newBed, SoftWarmBedsCompatibility.ComponentTypeName);

        foreach (var pawn in bed.CurOccupants)
        {
            RestUtility.KickOutOfBed(pawn, bed);
        }
        bed.RemoveAllOwners();

        // Art gets destroyed when new bed spawns
        var compArt = bed.TryGetComp<CompArt>();
        var art = compArt?.Active != null && compArt.taleRef != null ? new { authorName = compArt.authorNameInt, title = compArt.titleInt, taleRef = new TaleReference { tale = compArt.taleRef.tale, seed = compArt.taleRef.seed } } : null;
        var paint = bed.PaintColorDef;
        compArt?.taleRef?.tale?.Notify_NewlyUsed();

        newBed.SetFactionDirect(bed.Faction);
        var spawnedBed = (Building_Bed)GenSpawn.Spawn(newBed, bed.Position, bed.Map, bed.Rotation);
        beddingTransfer?.Commit(); // The old bed has been replaced; keep bedding on the new Thing.
        spawnedBed.HitPoints = bed.HitPoints;
        spawnedBed.ForPrisoners = forPrisoners;
        spawnedBed.Medical = false;

        var compQuality = spawnedBed.TryGetComp<CompQuality>();
        compQuality?.SetQuality(bed.GetComp<CompQuality>().Quality, ArtGenerationContext.Colony);

        // Apply art
        var newArt = spawnedBed.TryGetComp<CompArt>();
        if (newArt != null && art != null)
        {
            newArt.authorNameInt = art.authorName;
            newArt.titleInt = art.title;
            newArt.taleRef = art.taleRef;
        }

        spawnedBed.ChangePaint(paint);
        spawnedBed.StyleDef = bed.StyleDef;

        Find.Selector.Select(spawnedBed, false);
    }

    private static Thing MakeBed(Building_Bed bed, string defName)
    {
        var newDef = DefDatabase<ThingDef>.GetNamed(defName);
        return ThingMaker.MakeThing(newDef, bed.Stuff);
    }

    public bool TryClaimBed(Pawn pawn)
    {
        if (!pawn.ownership.ClaimBedIfNonMedical(this)) return false;
        UpdateStats();
        return CompAssignableToPawn.AssignedPawnsForReading.Contains(pawn);
    }

    public class BedStats
    {
        public int attractiveness;
        public int lastCalculated;
        public RoyalTitleDef[] metRoyalTitles;
        public Room room;
        public int staticBedValue;
        public TaggedString textNextTitleReq;
        public TaggedString title;
    }
}
