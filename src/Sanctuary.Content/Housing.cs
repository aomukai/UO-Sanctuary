// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using Server;
using Server.Accounting;
using Server.Commands;
using Server.Items;
using Server.Mobiles;
using Server.Multis;

namespace Sanctuary;

public static class Housing
{
    private const string AccountRight = "Sanctuary.HousingRecognized";
    public static int IndoorRate { get; private set; }
    public static int GardenRate { get; private set; }
    public static int WardrobePrice { get; private set; }
    public static HousingProfiles Profiles { get; private set; }

    [CallPriority(500)]
    public static void Configure()
    {
        Profiles = new HousingProfiles();
        BaseHouse.DecayEnabled = false;
        IndoorRate = ServerConfiguration.GetSetting("sanctuary.housing.indoorGoldPerTile", 1000);
        GardenRate = ServerConfiguration.GetSetting("sanctuary.housing.gardenGoldPerTile", 250);
        WardrobePrice = ServerConfiguration.GetSetting("sanctuary.housing.wardrobeGold", 1000);
        if (IndoorRate is < 1 or > 100000 || GardenRate is < 0 or > 100000 || WardrobePrice is < 1 or > 1000000)
        {
            throw new InvalidDataException("Invalid housing prices.");
        }
        HousingContentHooks.AcquisitionAllowed = HasRights;
        HousingContentHooks.AccountOwners = true;
        HousingContentHooks.OwnershipChanged = (house, previous) =>
        {
            if (house is TownHouse town) { town.ChangedOwner(previous); }
        };
        HousingContentHooks.DemolitionRequested = (from, house) =>
        {
            if (house is not TownHouse town) { return false; }
            TownHouseGump.DisplayTo(from, town);
            return true;
        };
        EventSink.Connected += from => { HasRights(from); };
        CommandSystem.Register("MarkHouse", AccessLevel.GameMaster, e => HouseMeasureGump.Start(e.Mobile));
    }

    [CallPriority(300)]
    public static void Initialize()
    {
        BaseHouse.DecayEnabled = false;
        foreach (var owner in TownRecognition.Profiles.RecognizedOwners())
        {
            Grant(World.FindMobile(owner));
        }
        if (!ClassProfessions.Enabled) { return; }
        foreach (var hall in TownRecognition.Halls)
        {
            if (World.FindMobile(Profiles.Architect(hall.Id)) is Architect { Deleted: false }) { continue; }
            var office = TownRecognition.Registrar(hall.Id);
            if (office?.Deleted != false) { continue; }
            Point3D? floor = null;
            for (int radius = 2; radius <= 8 && floor == null; radius++)
            {
                for (int dx = -radius; dx <= radius && floor == null; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        var point = new Point3D(office.X + dx, office.Y + dy, office.Z);
                        if (office.Map.CanFit(point, 16, false, true, true)) { floor = point; break; }
                    }
                }
            }
            if (floor == null) { throw new InvalidDataException("No clear architect location at a civic office."); }
            var architect = new Architect();
            architect.MoveToWorld(floor.Value, office.Map);
            architect.Home = floor.Value; architect.RangeHome = 2;
            Profiles.Architect(hall.Id, architect.Serial);
        }
    }

    public static void Grant(Mobile from)
    {
        if (from?.Account is Account account && account.GetTag(AccountRight) != "true") { account.SetTag(AccountRight, "true"); }
    }

    public static bool HasRights(Mobile from)
    {
        if (from is not { Deleted: false }) { return false; }
        if (from.AccessLevel >= AccessLevel.GameMaster) { return true; }
        if (from.Account is not Account account) { return false; }
        if (account.GetTag(AccountRight) == "true") { return true; }
        for (int i = 0; i < account.Length; i++)
        {
            if (account[i] is { Deleted: false } character && TownRecognition.HasAccess(character))
            {
                Grant(character);
                return true;
            }
        }
        return false;
    }

    public static bool CanBuy(Mobile from) => from is { Deleted: false, Alive: true, Holding: null }
        && HasRights(from) && !BaseHouse.HasAccountHouse(from);

    public static bool ReplaceWardrobe(PlayerMobile player, string site)
    {
        if (!ClassProfessions.Ready(player) || !TownRecognition.HasAccess(player)
            || !TownRecognition.AtOffice(player, site)) { return false; }
        var item = new EarnedWardrobe(player);
        if (!player.Backpack.CheckHold(player, item, false) || !Banker.Withdraw(player, WardrobePrice))
        {
            item.Delete();
            player.SendMessage("You need room in your backpack and enough gold in your bank for a replacement wardrobe.");
            return false;
        }
        player.Backpack.DropItem(item);
        player.SendMessage($"Replacement wardrobe purchased for {WardrobePrice} gold from your bank.");
        return true;
    }
}
