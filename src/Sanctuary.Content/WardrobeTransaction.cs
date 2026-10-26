// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary;

public static class WardrobeTransaction
{
    private static readonly ILogger _log = LogFactory.GetLogger(typeof(WardrobeTransaction));

    // Health and elapsed timers deliberately aren't quoted: their clocks keep running.
    internal static string Quote(PlayerMobile player) => JsonSerializer.Serialize(new
    {
        Histories = ClassCreation.Profiles.Histories(player.Serial),
        Worn = player.Items.Where(WardrobeEquipment.Equipment).OrderBy(i => i.Serial.Value)
            .Select(i => new { i.Serial.Value, i.Layer, i.Movable, i.QuestItem }),
        Sets = Enum.GetValues<DiabloClass>().Select(c => new
        {
            Class = c, Reference = WardrobeEquipment.Instance.HasReference(player.Serial, c),
            Set = WardrobeEquipment.Instance.Find(player.Serial, c) is { } set ? new
            {
                set.Serial.Value, set.Owner, set.CharacterClass, set.Saved,
                Parent = set.Parent is IEntity entity ? entity.Serial.Value : 0,
                Map = set.Map?.MapID,
                Items = set.Items.OrderBy(i => i.Serial.Value).Select(i => new { i.Serial.Value, i.Layer, i.Movable, i.QuestItem })
            } : null,
            Left = SkillBindings.Instance.Get(player.Serial, c).Left,
            Right = SkillBindings.Instance.Get(player.Serial, c).Right,
            Quick = Enumerable.Range(0, SkillBindingProtocol.Slots).Select(i => SkillBindings.Instance.GetQuick(player.Serial, c, i))
        }),
        Followers = (player.AllFollowers ?? []).OfType<BaseCreature>().Where(p => !p.Deleted).OrderBy(p => p.Serial.Value)
            .Select(p => new { p.Serial.Value, Master = p.ControlMaster?.Serial.Value, p.ControlSlots, State = PersistentMinions.Profiles.Find(p) })
    });

    internal static bool ChangeClass(PlayerMobile player, DiabloClass target, out int missing, out int packed)
    {
        missing = packed = 0;
        var profiles = ClassCreation.Profiles;
        if (!profiles.TryGet(player.Serial, out var previous) || previous.Class == target) { return false; }
        var storage = WardrobeEquipment.Instance;
        var source = storage.Find(player.Serial, previous.Class);
        var destination = storage.Find(player.Serial, target);
        if (storage.HasReference(player.Serial, previous.Class) && source == null
            || storage.HasReference(player.Serial, target) && destination == null
            || !WardrobeEquipment.Valid(player, previous.Class, source) || !WardrobeEquipment.Valid(player, target, destination)
            || source is { Items.Count: > 0 } || !PersistentMinions.CanActivate(player, target)) { return false; }
        var worn = player.Items.Where(WardrobeEquipment.Equipment).ToArray();
        if (worn.Any(i => !WardrobeEquipment.Storable(i) || !i.VerifyMove(player))) { return false; }
        var returning = destination?.Items.ToArray() ?? [];
        missing = (destination?.Saved.Length ?? 0) - returning.Length;
        int hits = player.Hits, mana = player.Mana, stam = player.Stam;
        int maxHits = player.HitsMax, maxMana = player.ManaMax, maxStam = player.StamMax;
        bool created = source == null;
        var sourceSaved = source?.Saved ?? [];
        var destinationSaved = destination?.Saved ?? [];
        var locations = worn.Concat(returning).ToDictionary(i => i, i => i.Location);
        CharacterProfiles.Selection? selection = null;
        try
        {
            source ??= storage.Create(player, previous.Class);
            foreach (var item in worn) { source.AddItem(item); }
            source.Remember(worn.Select(i => i.Serial.Value).ToArray());
            selection = profiles.SelectHistory(player, target);
            profiles.ApplyActive(player);
            ClassGear.Refresh(player);
            var pending = new List<Item>(returning);
            // A stat-granting item may make a later item wearable. Retry after each pass.
            bool progress;
            do
            {
                progress = false;
                for (int i = pending.Count - 1; i >= 0; i--)
                {
                    if (player.EquipItem(pending[i])) { pending.RemoveAt(i); ClassGear.Refresh(player); progress = true; }
                }
            } while (progress && pending.Count > 0);
            // Recheck the final equipment together after all native equip side effects.
            bool removed;
            do
            {
                removed = false;
                foreach (var item in returning)
                {
                    if (item.Parent == player && ClassGear.CanEquip(item, player) == false)
                    { destination.AddItem(item); ClassGear.Refresh(player); pending.Add(item); removed = true; }
                }
            } while (removed);
            foreach (var item in pending)
            {
                if (!player.Backpack.TryDropItem(player, item, false))
                { throw new InvalidOperationException("Your backpack cannot hold the equipment that you cannot wear."); }
            }
            packed = pending.Count;
            ClassGear.Refresh(player);
            int life = CharacterProfiles.CarryPool(hits, maxHits, player.HitsMax);
            if (life <= 0) { throw new InvalidOperationException("Recover more life before switching to this class."); }
            destination?.Remember([]);
            player.Hits = life;
            player.Mana = CharacterProfiles.CarryPool(mana, maxMana, player.ManaMax);
            player.Stam = CharacterProfiles.CarryPool(stam, maxStam, player.StamMax);
        }
        catch (Exception error)
        {
            // Restore real identities and parents without running equip requirements again.
            foreach (var item in returning) { destination.AddItem(item); item.Location = locations[item]; }
            foreach (var item in worn) { player.AddItem(item); item.Location = locations[item]; }
            if (selection is { } changed) { profiles.UndoSelection(player, changed); }
            profiles.ApplyActive(player);
            ClassGear.Refresh(player);
            source?.Remember(sourceSaved); destination?.Remember(destinationSaved);
            if (created && source != null) { storage.RemoveCreated(source); }
            player.Hits = hits; player.Mana = mana; player.Stam = stam;
            ClassGear.Changed(player);
            if (error is InvalidOperationException) { player.SendMessage(error.Message); }
            player.SendMessage("The wardrobe swap was cancelled; your class and equipment were restored.");
            _log.Debug(error, "Wardrobe transaction cancelled for {Player}", player.Serial);
            return false;
        }
        // No fallible gear or capacity work follows this point. Ordinary followers stay put.
        if (NecromancerEffects.Profiles.Find(player) is { } skillState)
        { NecromancerEffects.Profiles.Store(player, skillState with { BoneArmor = 0 }); }
        SorceressEffects.Profiles.Clear(player);
        ChargedStrikeBolts.Cancel(player);
        AmazonJavelins.Cancel(player);
        BladeFuryMissiles.Cancel(player);
        BladeFuryModes.Cancel(player);
        AmazonImpale.Cancel(player);
        AmazonFend.Cancel(player);
        AmazonDecoys.Cancel(player);
        PaladinAuras.Clear(player);
        HolyShields.Clear(player);
        DruidCycloneArmor.Clear(player);
        DruidHurricane.Clear(player);
        AssassinBuffs.Clear(player);
        CloakShadows.Clear(player);Venoms.Clear(player);
        BarbarianBerserk.Clear(player);
        BarbarianShout.Clear(player);
        BattleOrders.Clear(player);
        BattleCommand.Clear(player);
        PersistentMinions.ParkClass(player, previous.Class);
        PersistentMinions.Resume(player);
        return true;
    }

    internal static bool Retrieve(PlayerMobile player, Item wardrobe, long token, WardrobeSet set, Item item)
    {
        if (!TownRecognition.SessionValid(player, token) || !WardrobeAccess.Ready(player, wardrobe)
            || set == null || item == null || item.Parent != set || set.Owner != player.Serial.Value
            || WardrobeEquipment.Instance.Find(player.Serial, set.CharacterClass) != set
            || !WardrobeEquipment.Valid(player, set.CharacterClass, set)
            || !player.Backpack.TryDropItem(player, item, true)) { return false; }
        set.Remember(set.Saved.Where(id => id != item.Serial.Value).ToArray());
        TownRecognition.EndSession(player);
        return true;
    }
}
