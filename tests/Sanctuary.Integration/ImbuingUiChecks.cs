using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class ImbuingUiChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(ImbuingUiChecks));
    private static PlayerMobile _p;
    private static TinkerTools _tool;
    private static ImbuingLadle _ladle;
    private static ScribesPen _pen;
    private static IronIngot _iron;
    private static GoldRing _ring;
    private static Item _unknown;
    private static IdentifyScroll _scroll;
    private static GearState _before;
    private static int _gold, _essence, _price;
    public static void Configure()
    {
        Register("setup", Setup); Register("material", () => Target(_iron));
        Register("crafted", () =>
        {
            _ring = _p.Backpack.Items.OfType<GoldRing>().Single(i => i.Name == "Shoddy ring");
            Require(ClassGear.State(_ring) is { CraftedEquipment: true } s && s.Roll.ItemLevel == 20 && _iron.Amount == 96 && _tool.UsesRemaining == 19,
                "Client jewellery craft failed");
            _ladle = Put(new ImbuingLadle()); ClassProfessions.Profiles.Select(_p, SkillName.Imbuing);
            _p.Skills.Imbuing.Cap = 120; _p.Skills.Imbuing.Base = 120;
            _gold = _p.Backpack.GetAmount(typeof(Gold)); _essence = _p.Backpack.GetAmount(typeof(MagicEssence));
            _ladle.OnDoubleClick(_p);
        });
        Register("imbue-target", () => { Target(_ring); Require(_p.HasGump<EssenceGump>(), "No Imbuing confirmation"); });
        Register("cancelled", () =>
        {
            Require(ClassGear.State(_ring).Roll.Rarity == GearRarity.Normal && _ladle.UsesRemaining == 25
                && _p.Backpack.GetAmount(typeof(Gold)) == _gold, "Cancel consumed"); _ladle.OnDoubleClick(_p);
        });
        Register("imbued", () =>
        {
            Require(ClassGear.State(_ring).Roll.Rarity is GearRarity.Magic or GearRarity.Rare && _ladle.UsesRemaining == 24
                && _p.Backpack.GetAmount(typeof(Gold)) == _gold - 100 && _p.Backpack.GetAmount(typeof(MagicEssence)) == _essence - 5,
                "Client Imbuing result/cost failed");
            ClassProfessions.Profiles.Select(_p, SkillName.Inscribe); _p.Skills.Inscribe.Cap = 120; _p.Skills.Inscribe.Base = 120;
            _pen = Put(new ScribesPen(20)); Put(new BlankScroll(5)); Put(new BlackPearl(5)); _pen.OnDoubleClick(_p);
        });
        Register("scribed", () =>
        {
            _scroll = _p.Backpack.Items.OfType<IdentifyScroll>().Single();
            Require(_scroll.Origin == ItemOrigin.Crafted && _pen.UsesRemaining == 19 && _p.Backpack.GetAmount(typeof(BlankScroll)) == 4
                && _p.Backpack.GetAmount(typeof(BlackPearl)) == 4, "Actual client scribe route failed");
            _unknown = IdentificationChecks.Unknown(_p, new Longsword()); _price = TradePrices.Quote(_unknown);
            _before = ClassGear.State(_unknown); IdentificationChecks.Hidden(_unknown);
            _scroll.OnDoubleClick(_p);
        });
        Register("invalid", () =>
        {
            Target(_ring); Require(!_scroll.Deleted && !ClassGear.State(_unknown).Identified, "Invalid target consumed scroll");
            _scroll.OnDoubleClick(_p);
        });
        Register("identify", () => Target(_unknown));
        Register("live-final", Final); Register("final", Final);
    }
    private static T Put<T>(T item) where T : Item { _p.Backpack.DropItem(item); return item; }
    private static void Setup()
    {
        _p = (PlayerMobile)Accounts.GetAccount("combatui")[0]; ClassCombat.Stop(_p); _p.Target = null;
        _p.Frozen = _p.Paralyzed = false; _p.MoveToWorld(CombatPractice.FindFloor(Map.Trammel, 5450, 1160), Map.Trammel);
        _p.RawStr = 500; ClassCreation.Profiles.AdvanceLevel(_p, 99);
        ClassProfessions.Profiles.Select(_p, SkillName.Tinkering); _p.Skills.Tinkering.Cap = 120; _p.Skills.Tinkering.Base = 120;
        _tool = Put(new TinkerTools(20)); _iron = Put(new IronIngot(100)); Put(new Gold(5000)); Put(new MagicEssence(100));
        _tool.OnDoubleClick(_p);
    }
    private static void Target(Item item)
    { Require(_p.Target != null, "Actual action did not request a target"); _p.Target.Invoke(_p, item); }
    private static void Final() => Require(_scroll.Deleted && ClassGear.State(_unknown) == (_before with { Identified = true })
        && TradePrices.Quote(_unknown) == _before.IdentifiedSaleOffer && ClassGear.State(_ring).Roll.Rarity is GearRarity.Magic or GearRarity.Rare,
        "Identify changed the item/roll/quote or did not consume exactly one scroll");
    private static void Require(bool value, string message) => ImbuingChecks.Require(value, message);
    private static void Register(string suffix, Action action)
    {
        string name = "imbuing-ui-" + suffix;
        ConsoleInputHandler.RegisterCommand(name, "Actual-client jewellery, Imbuing and identification fixture", _ => Core.LoopContext.Post(() =>
        {
            try
            {
                action(); File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE"), JsonSerializer.Serialize(new
                { ring = _ring?.Serial.Value, scroll = _scroll?.Deleted, identified = _unknown == null ? false : ClassGear.State(_unknown).Identified }));
                _logger.Information("{Command}: passed", name);
            }
            catch (Exception error) { _logger.Error(error, "{Command}: failed", name); }
        }));
    }
}
