// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Collections.ObjectModel;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public sealed record CharacterProfile(DiabloClass Class, int Level, long Experience);

public sealed record ClassHistorySnapshot(CharacterProfile Profile, StatAllocation Allocation,
    IReadOnlyDictionary<string, byte> Ranks);

public sealed class CharacterProfiles : GenericPersistence
{
    private sealed class History(CharacterProfile profile)
    {
        internal CharacterProfile Profile = profile;
        internal StatAllocation Stats;
        internal readonly Dictionary<string, byte> Ranks = new(StringComparer.Ordinal);
    }
    private sealed class Character(DiabloClass active)
    {
        internal DiabloClass Active = active;
        internal byte Face;
        internal ushort AppearanceTemplate;
        internal bool ArrivalBriefed;
        internal bool MetIolo;
        internal bool NewHavenGateUnlocked;
        internal bool DepartedNewHaven;
        internal Serial IoloCompanion;
        internal bool VisitedHavenMine;
        internal bool DefeatedKrulg;
        internal Serial KrulgWitness;
        internal StoryDiscoveries BritainDiscoveries;
        internal bool IoloGreetedBritain;
        internal bool ReceivedIoloRunebook;
        internal bool WolfgangOpenedUp;
        internal HedgeMazeProgress HedgeMaze;
        internal readonly Dictionary<DiabloClass, History> Classes = new();
    }
    private Dictionary<Serial, Character> _characters = new();

    public CharacterProfiles() : base("SanctuaryCharacters", 100) { }

    private History Active(Serial serial) => _characters.TryGetValue(serial, out var character)
        ? character.Classes[character.Active] : null;

    public bool TryGet(Serial serial, out CharacterProfile profile)
    {
        profile = Active(serial)?.Profile;
        return profile != null;
    }

    // Value copies prevent inspection tools from mutating either active or inactive history.
    public ClassHistorySnapshot[] Histories(Serial serial)
    {
        if (!_characters.TryGetValue(serial, out var character)) { return []; }
        var snapshots = new List<ClassHistorySnapshot>();
        foreach (var history in character.Classes.Values)
        {
            snapshots.Add(new(history.Profile, history.Stats,
                new ReadOnlyDictionary<string, byte>(new Dictionary<string, byte>(history.Ranks, StringComparer.Ordinal))));
        }
        snapshots.Sort((a, b) => a.Profile.Class.CompareTo(b.Profile.Class));
        return snapshots.ToArray();
    }

    public bool HasHistory(Serial serial, DiabloClass characterClass) =>
        _characters.TryGetValue(serial, out var character) && character.Classes.ContainsKey(characterClass);

    public StatAllocation Stats(Serial serial) => Active(serial)?.Stats ?? default;

    public int AvailableStatPoints(Serial serial) => TryGet(serial, out var profile)
        ? (profile.Level - 1) * 5 - Stats(serial).Total : 0;

    public bool TrySpendStat(PlayerMobile mobile, CharacterStat stat)
    {
        if (mobile == null || mobile.Deleted || !mobile.Alive || !AlliedOverlap.Allowed(mobile) || (byte)stat > (byte)CharacterStat.Vitality
            || !TryGet(mobile.Serial, out var profile) || AvailableStatPoints(mobile.Serial) <= 0)
        {
            return false;
        }
        int hits = mobile.Hits, mana = mobile.Mana, stam = mobile.Stam;
        int maxHits = mobile.HitsMax, maxMana = mobile.ManaMax, maxStam = mobile.StamMax;
        Active(mobile.Serial).Stats = Stats(mobile.Serial).Add(stat);
        ClassStats.ApplyBase(mobile, profile, Stats(mobile.Serial));
        mobile.Hits = hits + Math.Max(0, mobile.HitsMax - maxHits);
        mobile.Mana = mana + Math.Max(0, mobile.ManaMax - maxMana);
        mobile.Stam = stam + Math.Max(0, mobile.StamMax - maxStam);
        return true;
    }

    public int GetRank(Serial serial, string talentId) =>
        Active(serial)?.Ranks.GetValueOrDefault(talentId) ?? 0;

    public int AvailablePoints(Serial serial)
    {
        if (!TryGet(serial, out var profile))
        {
            return 0;
        }

        var spent = 0;
        if (Active(serial) is { Ranks: var ranks })
        {
            foreach (var rank in ranks.Values)
            {
                spent += rank;
            }
        }

        return profile.Level - 1 - spent;
    }

    public string TrainingRequirement(Serial serial, string talentId)
    {
        if (!TryGet(serial, out var profile) || !TalentCatalog.TryGet(profile.Class, talentId, out var talent))
        {
            return "This talent does not belong to your class.";
        }

        return TrainingRequirement(profile, talent, GetRank(serial, talentId), AvailablePoints(serial),
            id => GetRank(serial, id));
    }

    private static string TrainingRequirement(CharacterProfile profile, TalentDefinition talent, int rank, int points,
        Func<string, int> getRank)
    {
        if (rank >= TalentCatalog.MaxRank)
        {
            return "Maximum rank reached.";
        }
        if (profile.Level < talent.RequiredLevel + rank)
        {
            return $"Requires character level {talent.RequiredLevel + rank}.";
        }
        foreach (var prerequisite in talent.Prerequisites)
        {
            if (getRank(prerequisite) == 0)
            {
                TalentCatalog.TryGet(profile.Class, prerequisite, out var required);
                return $"Requires {required.Name}.";
            }
        }
        return points > 0 ? null : "No unspent talent points. Gain a level to earn one.";
    }

    public bool TryTrain(Serial serial, string talentId, out string reason) =>
        TryTrainBatch(serial, new Dictionary<string, int> { [talentId] = 1 }, out reason);

    internal bool TryTrainBatch(Serial serial, IReadOnlyDictionary<string, int> allocations, out string reason)
    {
        if (World.FindMobile(serial) is { } actor && !AlliedOverlap.Allowed(actor))
        {
            reason = "Walk clear of your ally before training a talent.";
            return false;
        }
        var history = Active(serial);
        if (history == null || allocations.Count == 0)
        {
            reason = "Choose talents to learn.";
            return false;
        }

        if (!CanTrainBatch(serial, allocations, out reason))
        {
            return false;
        }

        foreach (var (id, count) in allocations)
        {
            history.Ranks[id] = (byte)(history.Ranks.GetValueOrDefault(id) + count);
        }
        if (World.FindMobile(serial) is PlayerMobile player)
        {
            if (history.Profile.Class == DiabloClass.Barbarian)
            {
                BarbarianCombat.Refresh(player);
            }
            else
            {
                StatNetwork.Send(player);
            }
        }
        reason = null;
        return true;
    }

    internal bool CanTrainBatch(Serial serial, IReadOnlyDictionary<string, int> allocations, out string reason)
    {
        var history = Active(serial);
        if (history == null)
        {
            reason = "Character profile unavailable.";
            return false;
        }
        int points = AvailablePoints(serial);
        int total = 0;
        foreach (var (id, count) in allocations)
        {
            if (count is < 1 or > TalentCatalog.MaxRank || !TalentCatalog.TryGet(history.Profile.Class, id, out _))
            {
                reason = "Invalid talent allocation.";
                return false;
            }
            total += count;
        }
        if (total > points)
        {
            reason = "You do not have enough unspent talent points.";
            return false;
        }

        int FinalRank(string id) => history.Ranks.GetValueOrDefault(id) + allocations.GetValueOrDefault(id);
        foreach (var (id, _) in allocations)
        {
            TalentCatalog.TryGet(history.Profile.Class, id, out var talent);
            reason = TrainingRequirement(history.Profile, talent, FinalRank(id) - 1, points, FinalRank);
            if (reason != null)
            {
                reason = $"{talent.Name}: {reason}";
                return false;
            }
        }

        reason = null;
        return true;
    }

    internal void ResetAllocatedPoints(PlayerMobile player, CharacterProfile profile)
    {
        int hits = SkillHealth.Balance256(player), mana = NecromancerEffects.ManaBalance256(player), stam = BattleOrders.Stamina256(player);
        using var equipment = Server.Collections.PooledRefList<Item>.Create();
        foreach (var item in player.Items)
        {
            if (item.Layer is not (Layer.Backpack or Layer.Bank or Layer.Hair or Layer.FacialHair or Layer.Mount)
                && item.Movable) { equipment.Add(item); }
        }
        // Keep real gear in the pack even if the reset lowers capacity or equipment requirements fail.
        foreach (var item in equipment) { player.Backpack.DropItem(item); }
        NecromancerEffects.Profiles.Clear(player);
        NecromancerSpellMissiles.Cancel(player);
        SorceressEffects.Profiles.Clear(player);
        ChargedStrikeBolts.Cancel(player);
        AmazonJavelins.Cancel(player);
        BladeFuryMissiles.Cancel(player);
        BladeFuryModes.Cancel(player);PlayerInfernoModes.Cancel(player);
        AmazonImpale.Cancel(player);
        AmazonFend.Cancel(player);
        AmazonDecoys.Cancel(player);
        PaladinAuras.Clear(player);
        HolyShields.Clear(player);
        DruidCycloneArmor.Clear(player);
        PsychicWard.Clear(player);
        EldritchBlastWorld.Clear(player);
        HexBane.Clear(player);
        HexSiphon.Clear(player);
        HexPurge.Clear(player);
        DruidHurricane.Clear(player);
        DruidArmageddon.Clear(player);
        AssassinBuffs.Clear(player);
        AssassinCharges.Clear(player);TigerStrikes.Cancel(player);AssassinClaws.Cancel(player);
        DruidAttackCharges.Clear(player);DruidChargedAttacks.Cancel(player);DruidFireClaws.Cancel(player);DruidHunger.Cancel(player);DruidRabies.Cancel(player);DruidShockWave.Cancel(player);DruidFury.Cancel(player);PoisonDaggerCombat.Cancel(player);
        CloakShadows.Clear(player);Venoms.Clear(player);
        BarbarianBerserk.Clear(player);
        BarbarianFrenzyEffects.Clear(player);
        BarbarianShout.Clear(player);
        BattleOrders.Clear(player);
        BattleCommand.Clear(player);
        Active(player.Serial).Stats = default;
        Active(player.Serial).Ranks.Clear();
        ClassStats.ApplyBase(player, profile, default);
        for (int pass = 0; pass < equipment.Count; pass++)
        {
            bool changed = false;
            foreach (var item in equipment)
            {
                if (item.Parent == player.Backpack && player.EquipItem(item)) { changed = true; }
            }
            if (!changed) { break; }
        }
        SkillHealth.SetBalance256(player,Math.Min(hits,SkillHealth.Maximum256(player)));
        NecromancerEffects.SetMana256(player,Math.Min(mana,NecromancerEffects.ManaMaximum256(player)));
        BattleOrders.SetStamina256(player,Math.Min(stam,BattleOrders.StaminaMaximum256(player)));
        ClassGear.Changed(player);
        StatNetwork.Send(player);
    }

    public bool AwardExperience(PlayerMobile mobile, int amount)
    {
        if (mobile == null || mobile.Deleted || !mobile.Alive || amount <= 0
            || !TryGet(mobile.Serial, out var profile) || profile.Level >= 99)
        {
            return false;
        }
        long experience = Math.Min(ExperienceLevels.Threshold(99), profile.Experience + amount);
        int level = profile.Level;
        while (level < 99 && experience >= ExperienceLevels.Threshold(level + 1))
        {
            level++;
        }
        Active(mobile.Serial).Profile = profile with { Experience = experience };
        if (level > profile.Level)
        {
            AdvanceLevel(mobile, level);
            mobile.SendMessage($"Level {level}! +{level - profile.Level} skill points, +{(level - profile.Level) * 5} stat points.");
            mobile.FixedParticles(0x375A, 10, 20, 5017, EffectLayer.Waist);
            mobile.PlaySound(0x1F7);
        }
        return true;
    }

    // Combat progression can award levels through this API without trusting client values.
    public bool AdvanceLevel(Mobile mobile, int level)
    {
        if (!TryGet(mobile.Serial, out var profile) || level <= profile.Level || level > 99)
        {
            return false;
        }

        Active(mobile.Serial).Profile = profile with { Level = level };
        mobile.Title = $"Level {level} {DiabloClasses.DisplayName(profile.Class, mobile.Female)}";
        ClassStats.Refresh(mobile);
        mobile.Hits = mobile.HitsMax;
        mobile.Mana = mobile.ManaMax;
        mobile.Stam = mobile.StamMax;
        if (mobile is PlayerMobile player)
        {
            MercenaryCare.SyncOwner(player);
            StatNetwork.Send(player);
            TownRecognition.Refresh(player);
            if (!EarnedTitles.Refresh(player)) { EarnedTitles.Changed(player); }
        }
        return true;
    }

    public byte Face(Serial serial) => _characters.TryGetValue(serial, out var character) ? character.Face : (byte)0;
    public ushort AppearanceTemplate(Serial serial) =>
        _characters.TryGetValue(serial, out var character) ? character.AppearanceTemplate : (ushort)0;
    internal bool NeedsArrival(Serial serial) => _characters.TryGetValue(serial, out var character) && !character.ArrivalBriefed;
    internal void Briefed(Serial serial)
    {
        if (_characters.TryGetValue(serial, out var character))
            character.ArrivalBriefed = true;
    }

    internal bool HasMetIolo(Serial serial) => _characters.TryGetValue(serial, out var character) && character.MetIolo;
    internal bool MeetIolo(Serial serial)
    {
        if (!_characters.TryGetValue(serial, out var character) || character.MetIolo)
            return false;
        character.MetIolo = true;
        return true;
    }

    internal Serial IoloCompanion(Serial serial) =>
        _characters.TryGetValue(serial, out var character) ? character.IoloCompanion : default;

    internal void SetIoloCompanion(Serial serial, Serial companion) => _characters[serial].IoloCompanion = companion;

    internal StoryDiscoveries Discoveries(Serial serial) => _characters.TryGetValue(serial, out var character)
        ? character.BritainDiscoveries | (character.MetIolo ? StoryDiscoveries.UnexpectedSummons : StoryDiscoveries.None)
        : StoryDiscoveries.None;

    internal bool Discover(Serial serial, StoryDiscoveries discoveries)
    {
        if (!_characters.TryGetValue(serial, out var character)
            || (character.BritainDiscoveries & discoveries) == discoveries)
            return false;
        character.BritainDiscoveries |= discoveries;
        return true;
    }

    internal bool HasHedgeMazeProgress(Serial serial, HedgeMazeProgress flag) =>
        _characters.TryGetValue(serial, out var character) && (character.HedgeMaze & flag) != 0;

    internal bool AdvanceHedgeMaze(Serial serial, HedgeMazeProgress flag)
    {
        if (!_characters.TryGetValue(serial, out var character) || (character.HedgeMaze & flag) != 0)
            return false;
        character.HedgeMaze |= flag;
        return true;
    }

    internal bool GreetedBritain(Serial serial) =>
        _characters.TryGetValue(serial, out var character) && character.IoloGreetedBritain;

    internal void RecordBritainGreeting(Serial serial) => _characters[serial].IoloGreetedBritain = true;

    internal bool ReceivedIoloRunebook(Serial serial) =>
        _characters.TryGetValue(serial, out var character) && character.ReceivedIoloRunebook;

    internal void RecordIoloRunebook(Serial serial) => _characters[serial].ReceivedIoloRunebook = true;

    internal bool WolfgangOpenedUp(Serial serial) =>
        _characters.TryGetValue(serial, out var character) && character.WolfgangOpenedUp;

    internal void RecordWolfgangTrust(Serial serial) => _characters[serial].WolfgangOpenedUp = true;

    internal void RecordHavenMineVisit(Serial serial)
    {
        if (_characters.TryGetValue(serial, out var character))
            character.VisitedHavenMine = true;
    }

    internal void RecordKrulgDefeat(Serial serial, Serial witness)
    {
        if (!_characters.TryGetValue(serial, out var character))
            return;

        character.DefeatedKrulg = true;
        if (witness.IsMobile)
            character.KrulgWitness = witness;
    }

    internal bool HasInvestigatedHavenMine(Serial serial, out Serial witness)
    {
        witness = default;
        if (!_characters.TryGetValue(serial, out var character) || !character.VisitedHavenMine || !character.DefeatedKrulg)
            return false;

        witness = character.KrulgWitness;
        return true;
    }

    internal bool NewHavenGateUnlocked(Serial serial) =>
        _characters.TryGetValue(serial, out var character) && character.NewHavenGateUnlocked;

    internal bool HasDepartedNewHaven(Serial serial) =>
        _characters.TryGetValue(serial, out var character) && character.DepartedNewHaven;

    internal bool RecordNewHavenDeparture(Serial serial)
    {
        if (!_characters.TryGetValue(serial, out var character) || !character.NewHavenGateUnlocked || character.DepartedNewHaven)
            return false;

        character.DepartedNewHaven = true;
        return true;
    }

    internal bool UnlockNewHavenGate(Serial serial)
    {
        if (!_characters.TryGetValue(serial, out var character) || character.NewHavenGateUnlocked
            || !character.MetIolo || !HasReached(serial, Iolo.RequiredLevel))
            return false;
        character.NewHavenGateUnlocked = true;
        return true;
    }

    internal bool HasReached(Serial serial, int level)
    {
        if (!_characters.TryGetValue(serial, out var character))
            return false;
        foreach (var history in character.Classes.Values)
            if (history.Profile.Level >= level)
                return true;
        return false;
    }

    public void Assign(Mobile mobile, DiabloClass characterClass, byte face = 0, ushort appearanceTemplate = 0)
    {
        if (!DiabloClasses.TryFromWire((int)characterClass, out _) || mobile is not PlayerMobile)
        {
            throw new ArgumentException("A playable class and player character are required.");
        }

        ((PlayerMobile)mobile).Young = false;
        var character = new Character(characterClass)
        {
            Face = face < PortraitProtocol.FaceCount ? face : (byte)0,
            AppearanceTemplate = appearanceTemplate
        };
        character.Classes.Add(characterClass, new(new(characterClass, 1, 0)));
        _characters.Add(mobile.Serial, character);
        mobile.Title = $"Level 1 {DiabloClasses.DisplayName(characterClass, mobile.Female)}";
        ClassStats.ApplyBase(mobile, Active(mobile.Serial).Profile, default);
        mobile.Hits = mobile.HitsMax;
        mobile.Mana = mobile.ManaMax;
        mobile.Stam = mobile.StamMax;
    }

    internal IEnumerable<PlayerMobile> Owners()
    {
        foreach (var serial in _characters.Keys)
        { if (World.FindMobile(serial) is PlayerMobile { Deleted: false } player) { yield return player; } }
    }

    internal readonly record struct Selection(DiabloClass Previous, DiabloClass Next, bool Fresh);
    internal Selection SelectHistory(PlayerMobile player, DiabloClass target)
    {
        if (!DiabloClasses.TryFromWire((int)target, out _) || !_characters.TryGetValue(player.Serial, out var character))
        { throw new InvalidOperationException("Invalid class selection."); }
        var result = new Selection(character.Active, target, !character.Classes.ContainsKey(target));
        if (result.Fresh) { character.Classes.Add(target, new(new(target, 1, 0))); }
        character.Active = target;
        return result;
    }
    internal void UndoSelection(PlayerMobile player, Selection selection)
    {
        var character = _characters[player.Serial]; character.Active = selection.Previous;
        if (selection.Fresh) { character.Classes.Remove(selection.Next); }
    }
    internal void ApplyActive(PlayerMobile player)
    {
        var history = Active(player.Serial);
        ClassStats.ApplyBase(player, history.Profile, history.Stats);
        player.Title = $"Level {history.Profile.Level} {DiabloClasses.DisplayName(history.Profile.Class, player.Female)}";
    }

    // Restricted fixture helper. Gameplay uses the complete wardrobe transaction.
    internal bool TryActivateHistory(PlayerMobile player, DiabloClass characterClass)
    {
        if (!DiabloClasses.TryFromWire((int)characterClass, out _) || !ClassProfessions.Ready(player)
            || !TryGet(player.Serial, out var old) || player.Combatant != null || player.Spell != null || player.Target != null
            || player.Followers != 0) { return false; }
        foreach (var item in player.Items)
        {
            if (item is not PotionBeltStorage && item.Layer is not (Layer.Backpack or Layer.Bank or Layer.Hair or Layer.FacialHair)) { return false; }
        }
        foreach (var follower in player.AllFollowers ?? [])
        {
            if (!follower.Deleted) { return false; }
        }
        var session = ClassCombat.For(player); long now = Core.TickCount;
        if (session.Target != Serial.Zero || session.CastTarget != Serial.Zero || session.HeldAttack
            || session.PendingAbility != CombatAbility.None || session.HeldAbility != CombatAbility.None
            || now - session.NextAction < 0 || now - session.RecoveryUntil < 0 || HitRecovery.Active(player, now)
            || now - player.NextSkillTime < 0 || now - player.NextSpellTime < 0
            || !player.CanBeginAction<Server.Engines.Craft.CraftSystem>()) { return false; }
        foreach (var aggression in player.Aggressors)
        {
            if (!aggression.Expired && Core.Now - aggression.LastCombatTime < TimeSpan.FromSeconds(10)) { return false; }
        }
        foreach (var aggression in player.Aggressed)
        {
            if (!aggression.Expired && Core.Now - aggression.LastCombatTime < TimeSpan.FromSeconds(10)) { return false; }
        }
        if (old.Class == characterClass) { return true; }
        TimedEffects.Settle(player, TimedEffects.Now);
        if (!ClassProfessions.Ready(player)) { return false; }
        int hits = player.Hits, mana = player.Mana, stam = player.Stam;
        int maxHits = player.HitsMax, maxMana = player.ManaMax, maxStam = player.StamMax;
        var character = _characters[player.Serial];
        bool fresh = !character.Classes.TryGetValue(characterClass, out var next);
        if (fresh) { character.Classes.Add(characterClass, next = new(new(characterClass, 1, 0))); }
        character.Active = characterClass;
        ClassStats.ApplyBase(player, next.Profile, next.Stats);
        int life = CarryPool(hits, maxHits, player.HitsMax);
        // Do not invent a free one-hit-point heal when downward rounding reaches zero.
        if (life == 0)
        {
            character.Active = old.Class;
            if (fresh) { character.Classes.Remove(characterClass); }
            ClassStats.ApplyBase(player, old, Stats(player.Serial));
            player.Hits = hits; player.Mana = mana; player.Stam = stam;
            return false;
        }
        player.Hits = life;
        player.Mana = CarryPool(mana, maxMana, player.ManaMax);
        player.Stam = CarryPool(stam, maxStam, player.StamMax);
        DruidCycloneArmor.Clear(player);
        PsychicWard.Clear(player);
        EldritchBlastWorld.Clear(player);
        EchoingStrikeWorld.Clear(player);
        MiasmaWorld.Clear(player);
        FlameWaveWorld.Clear(player);
        WeaponMissiles.CancelMirrored(player);
        HexBane.Clear(player);
        HexSiphon.Clear(player);
        HexPurge.Clear(player);
        DruidHurricane.Clear(player);
        DruidArmageddon.Clear(player);
        AssassinBuffs.Clear(player);
        AssassinCharges.Clear(player);TigerStrikes.Cancel(player);AssassinClaws.Cancel(player);
        DruidAttackCharges.Clear(player);DruidChargedAttacks.Cancel(player);DruidFireClaws.Cancel(player);DruidHunger.Cancel(player);DruidRabies.Cancel(player);DruidShockWave.Cancel(player);DruidFury.Cancel(player);PoisonDaggerCombat.Cancel(player);
        CloakShadows.Clear(player);Venoms.Clear(player);
        BarbarianBerserk.Clear(player);
        BarbarianShout.Clear(player);
        BattleOrders.Clear(player);
        BattleCommand.Clear(player);
        player.Title = $"Level {next.Profile.Level} {DiabloClasses.DisplayName(characterClass, player.Female)}";
        var bindings = SkillBindings.Instance.Get(player);
        session.Left = bindings.Left; session.Right = bindings.Right;
        ClassGear.Changed(player);
        StatNetwork.Send(player);
        return true;
    }

    internal static int CarryPool(int current, int oldMaximum, int newMaximum) =>
        (int)((long)Math.Clamp(current, 0, oldMaximum) * newMaximum / Math.Max(1, oldMaximum));

    public void PruneDeleted()
    {
        var deleted = new List<Serial>();
        foreach (var serial in _characters.Keys)
        {
            if (World.FindMobile(serial) is not PlayerMobile { Deleted: false }) { deleted.Add(serial); }
        }
        foreach (var serial in deleted) { _characters.Remove(serial); }
    }

    public override void Serialize(IGenericWriter writer)
    {
        writer.Write(14);
        writer.Write(_characters.Count);
        var serials = new List<Serial>(_characters.Keys); serials.Sort();
        foreach (var serial in serials)
        {
            var character = _characters[serial];
            writer.Write(serial.Value);
            writer.Write((byte)character.Active);
            writer.Write(character.Face);
            writer.Write(character.ArrivalBriefed);
            writer.Write(character.AppearanceTemplate);
            writer.Write(character.MetIolo);
            writer.Write(character.IoloCompanion.Value);
            writer.Write(character.NewHavenGateUnlocked);
            writer.Write(character.DepartedNewHaven);
            writer.Write(character.VisitedHavenMine);
            writer.Write(character.DefeatedKrulg);
            writer.Write(character.KrulgWitness.Value);
            writer.Write((uint)character.BritainDiscoveries);
            writer.Write(character.IoloGreetedBritain);
            writer.Write(character.ReceivedIoloRunebook);
            writer.Write(character.WolfgangOpenedUp);
            writer.Write((byte)character.HedgeMaze);
            writer.Write(character.Classes.Count);
            var classes = new List<DiabloClass>(character.Classes.Keys); classes.Sort();
            foreach (var id in classes) { WriteHistory(writer, character.Classes[id]); }
        }
    }

    private static void WriteHistory(IGenericWriter writer, History history)
    {
        writer.Write((byte)history.Profile.Class);
        writer.Write(history.Profile.Level);
        writer.Write(history.Profile.Experience);
        writer.Write(history.Stats.Strength); writer.Write(history.Stats.Dexterity);
        writer.Write(history.Stats.Energy); writer.Write(history.Stats.Vitality);
        writer.Write(history.Ranks.Count);
        var ids = new List<string>(history.Ranks.Keys); ids.Sort(StringComparer.Ordinal);
        foreach (var id in ids) { writer.Write(id); writer.Write(history.Ranks[id]); }
    }

    public override void Deserialize(IGenericReader reader)
    {
        int version = reader.ReadInt();
        if (version is < 0 or > 14) { throw new InvalidDataException($"Unsupported Sanctuary character data version {version}."); }
        int count = reader.ReadInt();
        if (count is < 0 or > 1000000) { throw new InvalidDataException("Invalid Sanctuary character count."); }
        // Decode and validate the complete replacement before changing the loaded authority.
        var characters = new Dictionary<Serial, Character>();
        for (int i = 0; i < count; i++)
        {
            var serial = (Serial)reader.ReadUInt();
            if (!serial.IsMobile) { throw new InvalidDataException("Invalid character serial."); }
            Character character;
            if (version < 3)
            {
                var history = ReadHistory(reader, version);
                character = new(history.Profile.Class) { ArrivalBriefed = true };
                character.Classes.Add(history.Profile.Class, history);
            }
            else
            {
                if (!DiabloClasses.TryFromWire(reader.ReadByte(), out var active)) { throw new InvalidDataException("Invalid active class."); }
                byte face = version >= 4 ? reader.ReadByte() : (byte)0;
                if (face >= PortraitProtocol.FaceCount) { throw new InvalidDataException("Invalid saved portrait face."); }
                character = new(active)
                {
                    Face = face, ArrivalBriefed = version < 5 || reader.ReadBool(),
                    AppearanceTemplate = version >= 6 ? reader.ReadUShort() : (ushort)0
                };
                if (character.AppearanceTemplate != 0 && !AppearanceTemplates.TryGet(character.AppearanceTemplate, out _))
                    throw new InvalidDataException("Invalid saved appearance template.");
                if (version >= 7)
                {
                    character.MetIolo = reader.ReadBool();
                    character.IoloCompanion = (Serial)reader.ReadUInt();
                    character.NewHavenGateUnlocked = version >= 8 ? reader.ReadBool() : character.IoloCompanion.IsMobile;
                    character.DepartedNewHaven = version >= 9 && reader.ReadBool();
                    if (version >= 10)
                    {
                        character.VisitedHavenMine = reader.ReadBool();
                        character.DefeatedKrulg = reader.ReadBool();
                        character.KrulgWitness = (Serial)reader.ReadUInt();
                    }
                    if (version >= 11)
                    {
                        character.BritainDiscoveries = (StoryDiscoveries)(version switch
                        {
                            >= 14 => reader.ReadUInt(),
                            >= 12 => reader.ReadUShort(),
                            _ => reader.ReadByte()
                        });
                        character.IoloGreetedBritain = reader.ReadBool();
                        character.ReceivedIoloRunebook = reader.ReadBool();
                        character.WolfgangOpenedUp = reader.ReadBool();
                    }
                }
                if (version >= 13)
                {
                    character.HedgeMaze = (HedgeMazeProgress)reader.ReadByte();
                    const HedgeMazeProgress known = HedgeMazeProgress.PassageOpened | HedgeMazeProgress.DaemonBanished
                        | HedgeMazeProgress.DaemonMet | HedgeMazeProgress.DaemonDefeated;
                    if ((character.HedgeMaze & ~known) != 0)
                        throw new InvalidDataException("Invalid Hedge Maze progress.");
                    if (version == 13 && (character.HedgeMaze & HedgeMazeProgress.DaemonBanished) != 0)
                        character.BritainDiscoveries |= StoryDiscoveries.HedgeOrvath;
                }
                int classes = reader.ReadInt();
                if (classes < 1 || classes > Enum.GetValues<DiabloClass>().Length) { throw new InvalidDataException("Invalid class history count."); }
                for (int j = 0; j < classes; j++)
                {
                    var history = ReadHistory(reader, version);
                    if (!character.Classes.TryAdd(history.Profile.Class, history)) { throw new InvalidDataException("Duplicate class history."); }
                }
                if (!character.Classes.ContainsKey(active)) { throw new InvalidDataException("Active class history is missing."); }
            }
            if (!characters.TryAdd(serial, character)) { throw new InvalidDataException("Duplicate character serial."); }
        }
        _characters = characters;
    }

    private static History ReadHistory(IGenericReader reader, int version)
    {
        byte classId = reader.ReadByte(); int level = reader.ReadInt(); long experience = reader.ReadLong();
        if (!DiabloClasses.TryFromWire(classId, out var characterClass) || level is < 1 or > 99 || experience < 0)
        { throw new InvalidDataException("Invalid Sanctuary character profile."); }
        // Preserve legacy staff-granted levels and XP exactly; they need not agree with XP thresholds.
        var history = new History(new(characterClass, level, experience));
        if (version >= 2)
        {
            history.Stats = new(reader.ReadInt(), reader.ReadInt(), reader.ReadInt(), reader.ReadInt());
            if (!history.Stats.Valid || history.Stats.Total > (level - 1) * 5)
            { throw new InvalidDataException("Invalid saved attribute allocations."); }
        }
        if (version >= 1)
        {
            int count = reader.ReadInt(), spent = 0;
            if (count is < 0 or > 30) { throw new InvalidDataException("Invalid talent allocation count."); }
            for (int i = 0; i < count; i++)
            {
                string id = reader.ReadString(); byte rank = reader.ReadByte();
                if (id == null || !TalentCatalog.TryGet(characterClass, id, out var talent)
                    || rank is < 1 or > TalentCatalog.MaxRank || rank > level - talent.RequiredLevel + 1
                    || !history.Ranks.TryAdd(id, rank)) { throw new InvalidDataException("Invalid saved talent rank."); }
                spent += rank;
            }
            if (spent > level - 1) { throw new InvalidDataException("Saved talent allocations exceed earned points."); }
            foreach (var id in history.Ranks.Keys)
            {
                TalentCatalog.TryGet(characterClass, id, out var talent);
                foreach (var prerequisite in talent.Prerequisites)
                {
                    if (!history.Ranks.ContainsKey(prerequisite)) { throw new InvalidDataException("Saved talent prerequisites are missing."); }
                }
            }
        }
        return history;
    }

    public override void PostDeserialize()
    {
        PruneDeleted();
        foreach (var (serial, character) in _characters)
        {
            var profile = character.Classes[character.Active].Profile;
            var mobile = World.FindMobile(serial);
            ((PlayerMobile)mobile).Young = false;
            mobile.Title = $"Level {profile.Level} {DiabloClasses.DisplayName(profile.Class, mobile.Female)}";
            ClassStats.ApplyBase(mobile, profile, Stats(serial));
            mobile.Hits = Math.Min(mobile.Hits, mobile.HitsMax);
            mobile.Mana = Math.Min(mobile.Mana, mobile.ManaMax);
            mobile.Stam = Math.Min(mobile.Stam, mobile.StamMax);
            ClassEquipment.Prepare(mobile as PlayerMobile);
            ClassGear.Changed((PlayerMobile)mobile);
        }
    }
}
