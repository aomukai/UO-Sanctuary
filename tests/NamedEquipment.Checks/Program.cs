using Sanctuary;

static void Require(bool condition, string message)
{
    if (!condition) { throw new InvalidOperationException(message); }
}
var catalog = NamedEquipmentCatalog.Load(args.Single());
Require(catalog.Items.Count(i => i.Rarity == "Unique") == 433 && catalog.Items.Count(i => i.Rarity == "Set") == 140
    && catalog.Sets.Count == 35, "Source identity inventory was truncated");
Require(catalog.Items.Count(i => i.Rarity == "Unique" && i.SourceSpawnable) == 403
    && catalog.Sets.Count(s => s.SourceAvailable) == 34, "Source eligibility differs");
Require(catalog.Items.Count(i => i.SourceKey == "Rainbow Facet") == 8, "Same-name facet variants collapsed");
Require(catalog.Item("piece/107").Name == "Whitstan's Guard", "English display localization was lost");
var gnasher = catalog.Item("unique/0");
Require(gnasher.Properties.Single(p => p.Code == "dmg%").SourceMin == 60
    && gnasher.Properties.Single(p => p.Code == "dmg%").SourceMax == 70, "Fixed-property template changed");
Require(!catalog.SourceCandidates("crs", "Unique", 99).Any(i => i.SourceId == 29), "Retired Azurewrath became a source candidate");
Require(!catalog.SourceCandidates("hax", "Unique", 6).Any()
    && catalog.SourceCandidates("hax", "Unique", 7).Single().Id == "unique/0", "Unique quality-level gate changed");
Require(!catalog.SourceCandidates("cm1", "Unique", 99).Any(i => i.Name == "Annihilus")
    && !catalog.SourceCandidates("cm2", "Unique", 99).Any(i => i.Name == "Hellfire Torch"),
    "Special source quality level110 was ignored");
Require(catalog.Items.Where(i => i.Rarity == "Unique" && i.SourceAvailability == "SourceRecipe")
    .All(i => !i.SourceSpawnable && i.Recipes.Length > 0), "Recipe-only uniques leaked into random selection");
Require(catalog.UnresolvedProperties("unique/427").SequenceEqual(new[] { "Gelid-Affix5" })
    && catalog.UnresolvedProperties("unique/0").Length == 0, "Source inconsistency was hidden or invented");

// Civerb's Ward uses specific siblings, not just the number of pieces.
Require(catalog.PreviewSetBonuses(new[] { "piece/0" }).Length == 0, "Single-piece Civerb bonus activated");
var icon = catalog.PreviewSetBonuses(new[] { "piece/0", "piece/1" });
Require(icon.Any(b => b.OwnerId == "piece/0" && b.Property.Code == "mana")
    && !icon.Any(b => b.OwnerId == "piece/0" && b.Property.Code == "res-pois"), "Civerb Icon activation differs");
var cudgel = catalog.PreviewSetBonuses(new[] { "piece/0", "piece/2" });
Require(cudgel.Any(b => b.OwnerId == "piece/0" && b.Property.Code == "res-pois")
    && !cudgel.Any(b => b.OwnerId == "piece/0" && b.Property.Code == "mana"), "Civerb Cudgel activation differs");
Require(catalog.PreviewSetBonuses(new[] { "piece/0", "piece/0" }).Length == 0, "Duplicate pieces increased set count");
Require(!catalog.PreviewSetBonuses(new[] { "piece/76", "piece/77" }).Any(b => b.Scope == "Partial" && b.Property.Code == "mag%")
    && catalog.PreviewSetBonuses(new[] { "piece/76", "piece/77", "piece/78" })
        .Single(b => b.Scope == "Partial" && b.Property.Code == "mag%").Property.SourceMin == 65,
    "Tal Rasha three-piece MF threshold differs");
Require(!catalog.PreviewSetBonuses(new[] { "piece/0", "piece/76" }).Any(), "Unrelated set pieces activated bonuses");

// Exercise every subset of every source set: cumulative partials, full bonuses,
// per-piece thresholds/specific partners and removal all use distinct pieces.
int subsets = 0;
foreach (var set in catalog.Sets)
{
    for (int mask = 0; mask < 1 << set.PieceIds.Length; mask++)
    {
        var active = set.PieceIds.Where((_, index) => (mask & (1 << index)) != 0).ToArray();
        var preview = catalog.PreviewSetBonuses(active);
        Require(preview.Count(b => b.Scope == "Full") == (active.Length == set.PieceIds.Length
            ? set.FullBonuses.Count(p => p.SourceEnabled) : 0), "Full-set threshold differs: " + set.Id);
        Require(preview.Count(b => b.Scope == "Partial") == set.PartialBonuses.Count(p => p.SourceEnabled
            && p.RequiredPieces <= active.Length), "Partial-set threshold differs: " + set.Id);
        foreach (string id in active)
        {
            var item = catalog.Item(id);
            int expected = item.AdditionalProperties.Count(p => p.SourceEnabled && (p.Activation == "Always"
                || p.Activation == "PieceCount" && active.Length >= p.RequiredPieces
                || p.Activation == "SpecificPiece" && active.Contains(p.RequiredPieceId)));
            Require(preview.Count(b => b.Scope == "Piece" && b.OwnerId == id) == expected, "Piece activation differs: " + id);
        }
        Require(preview.Length == catalog.PreviewSetBonuses(active.Concat(active)).Length, "Duplicate set counting: " + set.Id);
        subsets++;
    }
}
bool rejected = false;
try { catalog.PreviewSetBonuses(new[] { "unique/0" }); }
catch (ArgumentException) { rejected = true; }
Require(rejected, "Unique identity accepted as a set piece");
Require(catalog.PreviewSetBonuses(Array.Empty<string>()).Length == 0, "Removed equipment retained bonuses");
foreach (var item in catalog.Items)
{
    _ = catalog.UnresolvedProperties(item.Id);
    _ = catalog.SourceRequiredLevel(item.Id);
}
Console.WriteLine($"PASS: {catalog.Items.Count} named rows, {catalog.Sets.Count} sets, {catalog.Properties.Count} property definitions; {subsets} set subsets; source eligibility, variants, localization, activation and unresolved-source checks.");
