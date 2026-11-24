// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

internal static class HeartwoodTown
{
    private static readonly (string Name, string Title, Point3D Post, bool Female, int Hue)[] _residents =
    [
        ("Thalanil", "the woodcarver", new(6990, 342, 0), false, 0x045C),
        ("Elaris", "the herbalist", new(7063, 350, 0), true, 0x0591),
        ("Nimriel", "the orchard keeper", new(7082, 367, 0), true, 0x0491),
        ("Varenn", "the watchman", new(7031, 409, 7), false, 0x0455)
    ];

    internal static void Install(Map map)
    {
        Place("Lethara", new(6994, 346, 0), () => new Banker { CantWalk = true }, true, 0x2FCF, 0x034);
        Place("Aelwyn", new(7010, 363, 0), () => new Healer { CantWalk = true }, false, 0x2FC0, 0x386);
        foreach (var (name, title, post, female, hue) in _residents)
            Place(name, post, () => new HeartwoodResident(title, post, hue), female, 0x2FC1, 0x038);

        void Place<T>(string name, Point3D post, Func<T> create, bool female, int hair, int hairHue) where T : Mobile
        {
            if (World.Mobiles.Values.OfType<T>().Any(npc => npc.Map == map && npc.Name == name && npc.InRange(post, 3)))
                return;

            var npc = create();
            Style(npc, name, female, hair, hairHue, 0x83E6);
            npc.Direction = Direction.South;
            npc.MoveToWorld(post, map);
        }
    }

    internal static void StyleMerchant(BaseVendor vendor, MarketSite site)
    {
        switch (site.Id)
        {
            case "Trammel/Heartwood/Bowyer":
                Style(vendor, "Caelira", true, 0x2FCF, 0x321, 0x8376);
                break;
            case "Trammel/Heartwood/Carpenter":
                Style(vendor, "Orren", false, 0x2FC1, 0x034, 0x8375);
                break;
            case "Trammel/Heartwood/Provisioner":
                Style(vendor, "Syllen", true, 0x2FCE, 0x038, 0x8353);
                vendor.Title = "the provisioner";
                break;
        }
    }

    private static void Style(Mobile npc, string name, bool female, int hair, int hairHue, int skinHue)
    {
        npc.Name = name;
        npc.Female = female;
        npc.Race = Race.Elf;
        npc.Body = Race.Elf.AliveBody(female);
        npc.Hue = skinHue;
        npc.HairItemID = hair;
        npc.HairHue = hairHue;
        npc.FacialHairItemID = 0;
    }
}

[SerializationGenerator(0)]
public partial class HeartwoodResident : BaseCreature
{
    internal HeartwoodResident(string title, Point3D home, int hue) : base(AIType.AI_Vendor, FightMode.None, 6, 1)
    {
        Title = title;
        Blessed = true;
        Home = home;
        RangeHome = 1;
        AddItem(new ElvenShirt(hue));
        AddItem(new ElvenPants(0x0455));
        AddItem(new ElvenBoots(0x045B));
    }
}
