using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Logging;

namespace Sanctuary.Integration;

public static class StarterAudit
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(StarterAudit));
    public static void Configure() => ConsoleInputHandler.RegisterCommand("starter-audit", "Read original starting inventory from an archived world", _ =>
        Core.LoopContext.Post(() =>
        {
            try
            {
                var player = World.FindMobile((Serial)uint.Parse(Environment.GetEnvironmentVariable("SANCTUARY_STARTER_SERIAL"))) as PlayerMobile;
                if (player == null)
                {
                    throw new InvalidOperationException("No archived character for starter audit");
                }
                var original = Environment.GetEnvironmentVariable("SANCTUARY_STARTER_ORIGINAL_IDS");
                if (original != null)
                {
                    var ids = JsonDocument.Parse(File.ReadAllText(original)).RootElement.GetProperty("Items");
                    var found = new List<object>();
                    foreach (var id in ids.EnumerateArray())
                    {
                        var item = World.FindItem((Serial)id.GetUInt32());
                        found.Add(new { Serial = id.GetUInt32(), Present = item != null && !item.Deleted,
                            Type = item?.GetType().Name, Protected = item?.LootType == LootType.Blessed });
                    }
                    File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_STARTER_AUDIT"), JsonSerializer.Serialize(new { Items = found }));
                    _logger.Information("starter-audit: passed ({Count} inspected identities)", found.Count);
                    return;
                }
                var items = new List<uint>();
                foreach (var item in player.Items)
                {
                    if (item.Movable && item.Layer is not (Layer.Bank or Layer.Backpack or Layer.Hair or Layer.FacialHair)
                        && item is not Gold and not DeathRobe)
                    {
                        items.Add(item.Serial.Value);
                    }
                }
                if (player.Backpack != null)
                {
                    foreach (var item in player.Backpack.FindItems(true))
                    {
                        if (item is not Gold)
                        {
                            items.Add(item.Serial.Value);
                        }
                    }
                }
                ClassCreation.Profiles.TryGet(player.Serial, out var profile);
                File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_STARTER_AUDIT"), JsonSerializer.Serialize(new { Owner = player.Serial.Value, Name = player.Name, Class = (byte)profile.Class, Items = items }));
                _logger.Information("starter-audit: passed ({Count} original items)", items.Count);
            }
            catch (Exception error)
            {
                _logger.Error(error, "starter-audit: failed");
            }
        }));
}
