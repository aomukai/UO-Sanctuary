// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public sealed record CharacterProfile(DiabloClass Class, int Level, long Experience);

public sealed class CharacterProfiles : GenericPersistence
{
    private readonly Dictionary<Serial, StatAllocation> _stats = new();
    private readonly Dictionary<Serial, CharacterProfile> _profiles = new();
    private readonly Dictionary<Serial, Dictionary<string, byte>> _ranks = new();

    public CharacterProfiles() : base("SanctuaryCharacters", 100)
    {
    }

    public bool TryGet(Serial serial, out CharacterProfile profile) => _profiles.TryGetValue(serial, out profile);

    public StatAllocation Stats(Serial serial) => _stats.GetValueOrDefault(serial);

    public int AvailableStatPoints(Serial serial) => TryGet(serial, out var profile)
        ? (profile.Level - 1) * 5 - Stats(serial).Total : 0;

    public bool TrySpendStat(PlayerMobile mobile, CharacterStat stat)
    {
        if (mobile == null || mobile.Deleted || !mobile.Alive || (byte)stat > (byte)CharacterStat.Vitality
            || !TryGet(mobile.Serial, out var profile) || AvailableStatPoints(mobile.Serial) <= 0)
        {
            return false;
        }
        int hits = mobile.Hits, mana = mobile.Mana, stam = mobile.Stam;
        int maxHits = mobile.HitsMax, maxMana = mobile.ManaMax, maxStam = mobile.StamMax;
        _stats[mobile.Serial] = Stats(mobile.Serial).Add(stat);
        ClassStats.ApplyBase(mobile, profile, Stats(mobile.Serial));
        mobile.Hits = hits + Math.Max(0, mobile.HitsMax - maxHits);
        mobile.Mana = mana + Math.Max(0, mobile.ManaMax - maxMana);
        mobile.Stam = stam + Math.Max(0, mobile.StamMax - maxStam);
        return true;
    }

    public int GetRank(Serial serial, string talentId) =>
        _ranks.TryGetValue(serial, out var ranks) ? ranks.GetValueOrDefault(talentId) : 0;

    public int AvailablePoints(Serial serial)
    {
        if (!TryGet(serial, out var profile))
        {
            return 0;
        }

        var spent = 0;
        if (_ranks.TryGetValue(serial, out var ranks))
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

        var rank = GetRank(serial, talentId);
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
            if (GetRank(serial, prerequisite) == 0)
            {
                TalentCatalog.TryGet(profile.Class, prerequisite, out var required);
                return $"Requires {required.Name}.";
            }
        }

        return AvailablePoints(serial) > 0 ? null : "No unspent talent points. Gain a level to earn one.";
    }

    public bool TryTrain(Serial serial, string talentId, out string reason)
    {
        reason = TrainingRequirement(serial, talentId);
        if (reason != null)
        {
            return false;
        }

        if (!_ranks.TryGetValue(serial, out var ranks))
        {
            _ranks[serial] = ranks = new Dictionary<string, byte>(StringComparer.Ordinal);
        }

        ranks[talentId] = (byte)(ranks.GetValueOrDefault(talentId) + 1);
        return true;
    }

    internal void ResetAllocatedPoints(PlayerMobile player, CharacterProfile profile)
    {
        int hits = player.Hits, mana = player.Mana, stam = player.Stam;
        using var equipment = Server.Collections.PooledRefList<Item>.Create();
        foreach (var item in player.Items)
        {
            if (item.Layer is not (Layer.Backpack or Layer.Bank or Layer.Hair or Layer.FacialHair or Layer.Mount)
                && item.Movable) { equipment.Add(item); }
        }
        // Keep real gear in the pack even if the reset lowers capacity or equipment requirements fail.
        foreach (var item in equipment) { player.Backpack.DropItem(item); }
        _stats.Remove(player.Serial);
        _ranks.Remove(player.Serial);
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
        player.Hits = Math.Min(hits, player.HitsMax);
        player.Mana = Math.Min(mana, player.ManaMax);
        player.Stam = Math.Min(stam, player.StamMax);
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
        _profiles[mobile.Serial] = profile with { Experience = experience };
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

        _profiles[mobile.Serial] = profile with { Level = level };
        mobile.Title = $"Level {level} {DiabloClasses.DisplayName(profile.Class, mobile.Female)}";
        ClassStats.Refresh(mobile);
        mobile.Hits = mobile.HitsMax;
        mobile.Mana = mobile.ManaMax;
        mobile.Stam = mobile.StamMax;
        if (mobile is PlayerMobile player)
        {
            StatNetwork.Send(player);
            TownRecognition.Refresh(player);
        }
        return true;
    }

    public void Assign(Mobile mobile, DiabloClass characterClass)
    {
        if (!DiabloClasses.TryFromWire((int)characterClass, out _) || mobile is not PlayerMobile)
        {
            throw new ArgumentException("A playable class and player character are required.");
        }

        ((PlayerMobile)mobile).Young = false;
        _profiles.Add(mobile.Serial, new CharacterProfile(characterClass, 1, 0));
        mobile.Title = $"Level 1 {DiabloClasses.DisplayName(characterClass, mobile.Female)}";
        ClassStats.ApplyBase(mobile, _profiles[mobile.Serial], default);
        mobile.Hits = mobile.HitsMax;
        mobile.Mana = mobile.ManaMax;
        mobile.Stam = mobile.StamMax;
    }

    public void PruneDeleted()
    {
        var deleted = new List<Serial>();
        foreach (var serial in _profiles.Keys)
        {
            if (World.FindMobile(serial) is not PlayerMobile { Deleted: false })
            {
                deleted.Add(serial);
            }
        }

        foreach (var serial in deleted)
        {
            _stats.Remove(serial);
            _profiles.Remove(serial);
            _ranks.Remove(serial);
        }
    }

    public override void Serialize(IGenericWriter writer)
    {
        writer.Write(2);
        writer.Write(_profiles.Count);
        foreach (var (serial, profile) in _profiles)
        {
            writer.Write(serial.Value);
            writer.Write((byte)profile.Class);
            writer.Write(profile.Level);
            writer.Write(profile.Experience);
            var allocation = Stats(serial);
            writer.Write(allocation.Strength);
            writer.Write(allocation.Dexterity);
            writer.Write(allocation.Energy);
            writer.Write(allocation.Vitality);
            var ranks = _ranks.GetValueOrDefault(serial);
            writer.Write(ranks?.Count ?? 0);
            if (ranks != null)
            {
                foreach (var (id, rank) in ranks)
                {
                    writer.Write(id);
                    writer.Write(rank);
                }
            }
        }
    }

    public override void Deserialize(IGenericReader reader)
    {
        var version = reader.ReadInt();
        if (version is not (0 or 1 or 2))
        {
            throw new InvalidDataException($"Unsupported Sanctuary character data version {version}.");
        }

        var count = reader.ReadInt();
        if (count < 0)
        {
            throw new InvalidDataException("Invalid Sanctuary character count.");
        }

        _stats.Clear();
        _profiles.Clear();
        _ranks.Clear();
        for (var i = 0; i < count; i++)
        {
            var serial = (Serial)reader.ReadUInt();
            var classId = reader.ReadByte();
            var level = reader.ReadInt();
            var experience = reader.ReadLong();
            if (!DiabloClasses.TryFromWire(classId, out var characterClass) || level is < 1 or > 99 || experience < 0)
            {
                throw new InvalidDataException("Invalid Sanctuary character profile.");
            }

            _profiles.Add(serial, new CharacterProfile(characterClass, level, experience));
            if (version >= 2)
            {
                var allocation = new StatAllocation(reader.ReadInt(), reader.ReadInt(), reader.ReadInt(), reader.ReadInt());
                if (!allocation.Valid || allocation.Total > (level - 1) * 5)
                {
                    throw new InvalidDataException("Invalid saved attribute allocations.");
                }
                _stats[serial] = allocation;
            }
            if (version >= 1)
            {
                var allocations = reader.ReadInt();
                if (allocations is < 0 or > 30)
                {
                    throw new InvalidDataException("Invalid talent allocation count.");
                }

                var ranks = new Dictionary<string, byte>(StringComparer.Ordinal);
                for (var j = 0; j < allocations; j++)
                {
                    var id = reader.ReadString();
                    var rank = reader.ReadByte();
                    if (!TalentCatalog.TryGet(characterClass, id, out var talent)
                        || rank is < 1 or > TalentCatalog.MaxRank || rank > level - talent.RequiredLevel + 1)
                    {
                        throw new InvalidDataException("Invalid saved talent rank.");
                    }

                    ranks.Add(id, rank);
                }

                _ranks[serial] = ranks;
                if (AvailablePoints(serial) < 0)
                {
                    throw new InvalidDataException("Saved talent allocations exceed earned points.");
                }

                foreach (var id in ranks.Keys)
                {
                    TalentCatalog.TryGet(characterClass, id, out var talent);
                    foreach (var prerequisite in talent.Prerequisites)
                    {
                        if (!ranks.ContainsKey(prerequisite))
                        {
                            throw new InvalidDataException("Saved talent prerequisites are missing.");
                        }
                    }
                }
            }
        }
    }

    public override void PostDeserialize()
    {
        PruneDeleted();
        foreach (var (serial, profile) in _profiles)
        {
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
