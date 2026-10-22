// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Server;

namespace Sanctuary;

public sealed record TalentDefinition(string Id, string Name, int RequiredLevel, string[] Prerequisites);
public sealed record TalentTree(string Name, string Source, TalentDefinition[] Skills);
public sealed record ClassTalents(DiabloClass Class, TalentTree[] Trees);

public static class TalentCatalog
{
    public const int MaxRank = 20;
    private static readonly Dictionary<DiabloClass, ClassTalents> _classes = new();
    private static readonly Dictionary<string, (DiabloClass Class, TalentDefinition Talent)> _talents = new(StringComparer.Ordinal);

    public static ClassTalents ForClass(DiabloClass characterClass) => _classes[characterClass];

    public static bool TryGet(DiabloClass characterClass, string id, out TalentDefinition talent)
    {
        if (_talents.TryGetValue(id, out var entry) && entry.Class == characterClass)
        {
            talent = entry.Talent;
            return true;
        }

        talent = null;
        return false;
    }

    public static void Load()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter<DiabloClass>());
        var path = Path.Join(Core.BaseDirectory, "Data", "Sanctuary", "talents.json");
        var classes = JsonSerializer.Deserialize<ClassTalents[]>(File.ReadAllText(path), options)
            ?? throw new InvalidDataException("The talent catalog is empty.");
        _classes.Clear();
        _talents.Clear();
        if (classes.Length != Enum.GetValues<DiabloClass>().Length)
        {
            throw new InvalidDataException("The catalog must contain all playable classes.");
        }

        foreach (var entry in classes)
        {
            if (!DiabloClasses.TryFromWire((int)entry.Class, out _) || entry.Trees.Length != 3)
            {
                throw new InvalidDataException("Each playable class must have three talent trees.");
            }

            _classes.Add(entry.Class, entry);
            foreach (var tree in entry.Trees)
            {
                if (tree.Skills.Length != 10 || string.IsNullOrWhiteSpace(tree.Name))
                {
                    throw new InvalidDataException("Each talent tree must contain ten named skills.");
                }

                foreach (var talent in tree.Skills)
                {
                    if (string.IsNullOrWhiteSpace(talent.Id) || string.IsNullOrWhiteSpace(talent.Name)
                        || talent.RequiredLevel is not (1 or 6 or 12 or 18 or 24 or 30))
                    {
                        throw new InvalidDataException("Invalid talent definition.");
                    }

                    _talents.Add(talent.Id, (entry.Class, talent));
                }
            }
        }

        foreach (var (_, entry) in _talents)
        {
            ValidatePrerequisites(entry.Class, entry.Talent, new HashSet<string>());
        }
    }

    private static void ValidatePrerequisites(DiabloClass characterClass, TalentDefinition talent, HashSet<string> path)
    {
        if (!path.Add(talent.Id))
        {
            throw new InvalidDataException("Cyclic talent prerequisites.");
        }

        foreach (var id in talent.Prerequisites)
        {
            if (!TryGet(characterClass, id, out var prerequisite) || prerequisite.RequiredLevel > talent.RequiredLevel)
            {
                throw new InvalidDataException($"Invalid prerequisite {id} for {talent.Id}.");
            }

            ValidatePrerequisites(characterClass, prerequisite, path);
        }

        path.Remove(talent.Id);
    }
}
