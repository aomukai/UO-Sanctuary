// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

// Read-only world inspection for a disposable copy of an existing save.
// Never load the integration assembly into the live server.
public static class WorkingBaselineChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(WorkingBaselineChecks));

    public static void Configure() => ConsoleInputHandler.RegisterCommand(
        "working-baseline-inspect", "Inspect a disposable baseline without changing game state", _ =>
            Core.LoopContext.Post(() =>
            {
                try
                {
                    var snapshot = new
                    {
                        Players = World.Mobiles.Values.OfType<PlayerMobile>().OrderBy(p => p.Serial.Value).Select(p => new
                        {
                            Serial = p.Serial.Value,
                            Access = p.AccessLevel.ToString(),
                            Profile = ClassCreation.Profiles.TryGet(p.Serial, out var profile) ? profile : null,
                            Profession = ClassProfessions.Affected(p) ? ClassProfessions.Profiles.Find(p) : null,
                            Allocation = ClassCreation.Profiles.Stats(p.Serial),
                            p.Name, p.Title, p.Fame, p.Karma, p.Kills, p.ShowFameTitle,
                            EarnedTitles = Sanctuary.EarnedTitles.Applies(p) ? Sanctuary.EarnedTitles.Profiles.Find(p.Serial) : null,
                            ClassHistories = ClassCreation.Profiles.Histories(p.Serial),
                            ClassBindings = Enum.GetValues<DiabloClass>().Where(c => ClassCreation.Profiles.HasHistory(p.Serial, c))
                                .Select(c => new { Class = c, Left = SkillBindings.Instance.Get(p.Serial, c).Left,
                                    Right = SkillBindings.Instance.Get(p.Serial, c).Right,
                                    Quick = Enumerable.Range(0, SkillBindingProtocol.Slots).Select(slot => SkillBindings.Instance.GetQuick(p.Serial, c, slot)).ToArray() }).ToArray(),
                            Recognition = TownRecognition.Profiles.Find(p),
                            p.RawStr, p.RawDex, p.RawInt,
                            SkillsCap = p.Skills.Cap,
                            Skills = Enumerable.Range(0, p.Skills.Length).Select(id => p.Skills[id]).Select(s => new
                            {
                                Id = (int)s.SkillName, s.BaseFixedPoint, s.CapFixedPoint, Lock = s.Lock.ToString()
                            }).ToArray(),
                            Items = p.Items.Select(i => i.Serial.Value).OrderBy(s => s).ToArray()
                        }).ToArray(),
                        Materials = World.Items.Values.Where(i => i is BaseOre or BaseIngot or Log or Board or BaseHides or BaseLeather
                            or Cloth or UncutCloth or BoltOfCloth or Wool or BaseClothMaterial or MetalScraps or WoodenChips or LeatherStrips or ClothScraps or Bandage)
                            .OrderBy(i => i.Serial.Value).Select(i => new
                            {
                                Serial = i.Serial.Value, Type = i.GetType().Name, i.Amount, i.ItemID, i.Hue, i.Origin, i.Weight,
                                CanonicalWeight = MaterialProcessing.Weight(i),
                                Resource = i switch { BaseOre ore => (int?)ore.Resource, BaseIngot ingot => (int)ingot.Resource,
                                    Log log => (int)log.Resource, Board board => (int)board.Resource, BaseHides hides => (int)hides.Resource,
                                    BaseLeather leather => (int)leather.Resource, _ => null }
                            }).ToArray(),
                        Architects = TownRecognition.Halls.Select(h => new
                            { h.Id, Mobile = World.FindMobile(Housing.Profiles.Architect(h.Id)) as Architect })
                            .Where(e => e.Mobile is { Deleted: false }).Select(e => new
                            {
                                Hall = e.Id, Serial = e.Mobile.Serial.Value,
                                Backpack = e.Mobile.Backpack?.Serial.Value ?? 0,
                                GoldSerials = e.Mobile.Backpack?.Items.OfType<Gold>().Select(i => i.Serial.Value).ToArray() ?? []
                            }).ToArray(),
                        Gold = World.Items.Values.OfType<Gold>().OrderBy(i => i.Serial.Value)
                            .Select(i => new { Serial = i.Serial.Value, i.Amount, i.Origin,
                                Parent = i.Parent is IEntity entity ? entity.Serial.Value : 0 }).ToArray(),
                        Origins = World.Items.Values.GroupBy(i => i.Origin).OrderBy(g => g.Key)
                            .Select(g => new { Origin = g.Key.ToString(), Count = g.Count() }).ToArray(),
                        Gear = World.Items.Values.Where(i => ClassGear.State(i) != null).OrderBy(i => i.Serial.Value)
                            .Select(i => new { Serial = i.Serial.Value, Origin = i.Origin.ToString(), State = ClassGear.State(i) }).ToArray(),
                        Corpses = World.Items.Values.OfType<Corpse>().OrderBy(i => i.Serial.Value)
                            .Select(c => new { Serial = c.Serial.Value, c.SanctuaryNoRewards, c.SanctuaryDeathCharged }).ToArray(),
                        Raised = World.Mobiles.Values.OfType<GraveyardUndead>().Where(m => m.Raised).OrderBy(m => m.Serial.Value)
                            .Select(m => new { Serial = m.Serial.Value, m.NoKillAwards }).ToArray()
                    };
                    File.WriteAllText(Path.Join(Core.BaseDirectory, "working-baseline.json"),
                        JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
                    _logger.Information("working-baseline-inspect: passed");
                }
                catch (Exception error)
                {
                    _logger.Error(error, "working-baseline-inspect: failed");
                }
            }));
}
