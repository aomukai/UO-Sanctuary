// SPDX-License-Identifier: GPL-3.0-or-later
using System.Linq;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public static class NewHavenInn
{
    public const string Name = "The Bountiful Harvest Inn";
    internal static readonly Point3D IoloSeat = new(3506, 2524, 27);
    internal static readonly Point3D EntranceSign = new(3507, 2527, 38);

    public static void Configure()
    {
        EventSink.ServerStarted += Install;
        ClassMapHooks.StoryMobileVisible = (viewer, target) =>
        {
            if (viewer is PlayerMobile campaignViewer && !BritanniaCampaign.MobileVisible(campaignViewer, target))
                return false;
            if (viewer is PlayerMobile coveViewer && !CoveWorld.MobileVisible(coveViewer, target))
                return false;
            if (viewer is PlayerMobile ghostViewer && target is HedgeHouseGhost ghost && !ghost.VisibleTo(ghostViewer))
                return false;
            if (target is Celeste celeste)
                return celeste.VisibleTo(viewer);
            if (target is not Iolo { Recruited: false } || viewer is not PlayerMobile player)
                return BritainStory.MobileVisible(viewer, target);
            var profiles = ClassCreation.Profiles;
            return BritanniaCampaign.TownsEmpty(player)
                || !profiles.HasDepartedNewHaven(player.Serial)
                && (World.FindMobile(profiles.IoloCompanion(player.Serial)) is not Iolo companion
                    || !MercenaryCare.IsCompanion(player, companion));
        };
    }

    private static void Install()
    {
        var map = Map.Trammel;
        if (!World.Mobiles.Values.OfType<Iolo>().Any(iolo => !iolo.Deleted && !iolo.Recruited))
            new Iolo().MoveToWorld(IoloSeat, map);
        if (!World.Items.Values.OfType<BaseSign>().Any(sign => sign.Map == map
            && sign.InRange(EntranceSign, 5) && sign.Name == Name))
            new Sign(SignType.Inn, SignFacing.North) { Name = Name }.MoveToWorld(EntranceSign, map);
        if (!World.Mobiles.Values.OfType<HarvestBarkeeper>().Any())
            new HarvestBarkeeper().MoveToWorld(new Point3D(3497, 2516, 27), map);

        PlaceResident("Nell", "the maid", true, false, new Point3D(3504, 2520, 27), Direction.South);
        PlaceResident("Gwen", "the traveler", true, true, new Point3D(3505, 2515, 27), Direction.East);
        PlaceResident("Osric", "the traveler", false, true, new Point3D(3499, 2515, 27), Direction.West);
    }

    private static void PlaceResident(string name, string title, bool female, bool patron, Point3D position, Direction direction)
    {
        if (World.Mobiles.Values.OfType<HarvestResident>().Any(resident => resident.Name == name))
            return;
        var resident = new HarvestResident
        {
            Name = name, Title = title, Female = female, Body = female ? 0x191 : 0x190, Direction = direction,
            HairItemID = female ? 0x2046 : 0x203B, HairHue = 0x045A
        };
        resident.AddItem(new FancyShirt(patron ? 0x0530 : 0x0481));
        if (female)
            resident.AddItem(new Skirt(patron ? 0x0591 : 0x0455));
        else
            resident.AddItem(new LongPants(0x0590));
        if (!patron)
            resident.AddItem(new HalfApron(0x0481));
        resident.AddItem(new Shoes(0x0455));
        resident.MoveToWorld(position, Map.Trammel);
    }
}

[SerializationGenerator(0)]
public partial class HarvestBarkeeper : Barkeeper
{
    [Constructible]
    public HarvestBarkeeper()
    {
        Name = "Mara";
        CantWalk = true;
        Direction = Direction.East;
    }
}

[SerializationGenerator(0)]
public partial class HarvestResident : BaseCreature
{
    [Constructible]
    public HarvestResident() : base(AIType.AI_Vendor, FightMode.None, 10, 1)
    {
        CantWalk = true;
        Blessed = true;
        Hue = 0x83EF;
    }
}
