// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Runtime.CompilerServices;
using Server;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

internal static class MountTaming
{
    internal static bool IsNecromancer(PlayerMobile owner) =>
        ClassCreation.Profiles?.TryGet(owner.Serial, out var profile) == true && profile.Class == DiabloClass.Necromancer;

    internal static double RequiredSkill(BaseCreature creature) => creature switch
    {
        Nightmare or Kirin or Unicorn => 105,
        LesserHiryu => 110,
        Hiryu or CuSidhe => 115,
        FireSteed or HellSteed => 120,
        _ => creature.MinTameSkill
    };

    private static bool Supported(BaseCreature creature) => creature is Horse or RidableLlama or DesertOstard or ForestOstard
        or Ridgeback or SwampDragon or Beetle or FireBeetle or FrenziedOstard or Nightmare or Kirin or Unicorn
        or LesserHiryu or Hiryu or CuSidhe or FireSteed;

    internal static bool CanUse(Mobile mobile)
    {
        if (mobile is not PlayerMobile owner || owner.AccessLevel > AccessLevel.Player
            || YewStory.Has(owner, YewProgress.Taming))
            return true;
        owner.SendMessage("You have yet to learn the art of animal taming.");
        return false;
    }

    internal static bool CanTame(Mobile mobile, BaseCreature creature)
    {
        if (!CanUse(mobile))
            return false;
        if (creature is BaseMount && !Supported(creature))
        {
            mobile.SendMessage("This creature cannot be tamed as a mount.");
            return false;
        }
        if (mobile is PlayerMobile owner && Supported(creature) && MountTravel.Active(owner) != null)
        {
            owner.SendMessage("Stable or list your active mount before taming another.");
            return false;
        }
        return true;
    }

    internal static bool Completed(Mobile mobile, BaseCreature creature)
    {
        if (mobile is not PlayerMobile owner)
            return false;
        if (!CanTame(owner, creature))
            return true;
        if (creature is BaseMount mount)
        {
            if (MountTravel.Adopt(owner, mount))
                owner.SendMessage($"You have tamed {mount.Name}.");
        }
        else
        {
            creature.Animate(creature.Body.IsAnimal ? 3 : 17, 5, 1, true, false, 0);
            creature.PlaySound(creature.GetIdleSound());
            owner.SendMessage("You dismiss the animal, as you have no use for it.");
            creature.Blessed = true;
            creature.Tamable = false;
            Timer.StartTimer(TimeSpan.FromSeconds(1), creature.Delete);
        }
        CheckUnlock(owner);
        return true;
    }

    internal static bool Feed(BaseCreature creature, Mobile feeder)
    {
        if (MountTravel.Find(creature) is not { } record)
            return false;
        if (feeder is not PlayerMobile owner || record.Owner != owner.Serial || !MountTravel.IsActive(record)
            || creature.IsBonded || owner.Skills.AnimalTaming.Base < RequiredSkill(creature))
            return true;

        if (record.BondAt == DateTime.MinValue)
        {
            MountTravel.Profiles.Mounts[creature.Serial] = record with { BondAt = Core.Now.AddHours(7) };
            owner.SendMessage("Feed your mount again after seven hours to offer it a permanent bond.");
        }
        else if (Core.Now >= record.BondAt)
        {
            owner.SendGump(new MountConfirmation(owner, $"Bond with {creature.Name}? A bonded mount can be resurrected, and the bond is permanent.",
                "Bond Mount", () =>
                {
                    if (!MountTravel.Nearby(owner, creature) || MountTravel.Find(creature) is not { } current
                        || current.Owner != owner.Serial || !MountTravel.IsActive(current) || current.BondAt != record.BondAt
                        || creature.IsBonded || owner.Skills.AnimalTaming.Base < RequiredSkill(creature))
                        return;
                    creature.IsBonded = true;
                    MountTravel.Profiles.Mounts[creature.Serial] = current with { BondAt = DateTime.MinValue };
                    owner.SendMessage("Your mount has bonded with you.");
                }));
        }
        return true;
    }

    internal static void CheckUnlock(PlayerMobile owner)
    {
        if (IsNecromancer(owner) && owner.Skills.AnimalTaming.Base >= 120 && YewStory.Has(owner, YewProgress.Taming)
            && YewStory.Add(owner, YewProgress.Hellsteed))
            owner.SendMessage("You can now raise a fallen horse as a hellsteed.");
    }

    private static bool CanRaise(PlayerMobile owner, Corpse corpse) =>
        IsNecromancer(owner) && YewStory.Has(owner, YewProgress.Taming) && owner.Skills.AnimalTaming.Base >= 120
        && owner.Alive && owner.Map == corpse.Map && owner.InRange(corpse, 2) && owner.InLOS(corpse)
        && !corpse.Deleted && !corpse.Carved && corpse.Owner is Horse or Nightmare or FireSteed or Unicorn;

    internal static bool OfferRaise(Mobile mobile, Corpse corpse)
    {
        if (mobile is not PlayerMobile owner || !CanRaise(owner, corpse))
            return false;
        owner.SendGump(new MountConfirmation(owner, "Raise this creature as a hellsteed?", "Raise Hellsteed", () =>
        {
            if (!CanRaise(owner, corpse))
                return;
            if (MountTravel.Active(owner) != null)
            {
                owner.SendMessage("Stable your active mount before raising a hellsteed.");
                return;
            }
            if (!MountTravel.FindLanding(owner, out var spot))
                return;
            var steed = new HellSteed();
            if (!MountTravel.Adopt(owner, steed))
            {
                steed.Delete();
                return;
            }
            steed.IsBonded = true;
            steed.MoveToWorld(spot, owner.Map);
            corpse.Carved = true;
            owner.SendMessage("Your hellsteed rises already bound to you.");
        }));
        return true;
    }
}

internal sealed class MountConfirmation(PlayerMobile owner, string prompt, string action, Action accept) : DynamicGump(100, 75)
{
    public override bool Singleton => true;

    protected override void BuildLayout(ref DynamicGumpBuilder b)
    {
        b.AddPage();
        b.AddBackground(0, 0, 480, 160, 5054);
        b.AddHtml(23, 23, 430, 58, $"<BASEFONT COLOR=#FFFFFF>{prompt}</BASEFONT>");
        b.AddButton(23, 103, 4005, 4007, 1);
        b.AddLabel(58, 103, 0x481, action);
        b.AddButton(337, 103, 4017, 4019, 0);
        b.AddLabel(372, 103, 0x481, "Cancel");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile == owner && owner.Alive && info.ButtonID == 1)
            accept();
    }
}
