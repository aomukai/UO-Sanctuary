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

public static class InscriptionUiChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(InscriptionUiChecks));
    private static PlayerMobile _p;
    private static ScribesPen _pen;
    private static IdentifyTome _tome;
    private static TownPortalScroll _scroll;
    private static Item _gear;
    private static int _charges;
    private static TownPortals.Pair _pair;
    public static void Configure()
    {
        Register("setup",()=>
        {
            _p=(PlayerMobile)Accounts.GetAccount("combatui")[0]; ClassCombat.Stop(_p); _p.Target=null;
            foreach(var i in _p.Backpack.Items.ToArray()) { i.Delete(); }
            ClassCreation.Profiles.AdvanceLevel(_p,99); _p.RawStr=1000; TimedEffects.Clear(_p);
            ClassProfessions.Profiles.Select(_p,SkillName.Inscribe); _p.Skills.Inscribe.Cap=120; _p.Skills.Inscribe.Base=120; _p.Skills.Inscribe.SetLockNoRelay(SkillLock.Locked);
            _p.MoveToWorld(new Point3D(5650,1100,0),Map.Trammel); CraftFacilities.Register(new(SkillName.Inscribe,"Trammel",5651,1100,0,0xB4A));
            _pen=new ScribesPen(20); _p.Backpack.DropItem(_pen); _p.Backpack.DropItem(new Leather()); _p.Backpack.DropItem(new BlankScroll(3)); _p.Backpack.DropItem(new BlackPearl(3)); _pen.OnDoubleClick(_p);
        });
        Register("quoted",()=>Require(_p.FindGump<InscriptionGump>()!=null && _pen.UsesRemaining==20,"Recipe quote missing"));
        Register("cancelled",()=> { Require(_p.FindGump<InscriptionGump>()==null && _pen.UsesRemaining==20 && _p.Backpack.GetAmount(typeof(BlackPearl))==3,"Cancel consumed materials"); _pen.OnDoubleClick(_p); });
        Register("crafted",()=>
        {
            _tome=_p.Backpack.Items.OfType<IdentifyTome>().Single(); Require(_tome.Charges is >=3 and <=5 && _tome.PlayerConstructed && _pen.UsesRemaining==19,"Native tome craft wrong");
            _charges=_tome.Charges; _gear=IdentificationChecks.Unknown(_p,new PlateChest()); _scroll=new TownPortalScroll(); _p.Backpack.DropItem(_scroll);
            _tome.Location=new Point3D(45,65,0); _gear.Location=new Point3D(85,65,0); _scroll.Location=new Point3D(125,65,0); _pen.Location=new Point3D(115,120,0);
            _p.Backpack.OnDoubleClick(_p);
        });
        Register("target",()=>Require(_p.Target!=null && _tome.Charges==_charges,$"Tome right-click target missing: ready={UtilityScrolls.Ready(_p,_tome)} hue={_tome.Hue} held={_p.Holding} frozen={_p.Frozen} target={_p.Target}"));
        Register("cancel-target",()=> { Require(_p.Target==null && _tome.Charges==_charges,"Canceled target consumed charge"); _p.Backpack.OnDoubleClick(_p); });
        Register("identified",()=>Require(ClassGear.State(_gear).Identified && _tome.Charges==_charges-1,"UI tome did not identify/spend once"));
        Register("loaded",()=>Require(PotionBelts.Entry(_p,0)==_scroll,"Portal scroll did not load"));
        Register("field",()=> { _p.Target=null; ClassCombat.Stop(_p); _p.MoveToWorld(CombatPractice.FindFloor(Map.Trammel,1382,1495),Map.Trammel); });
        Register("portal",()=> { _pair=TownPortals.For(_p); Require(_pair!=null && _scroll.Deleted && _p.Target==null,"Belt portal creation failed"); });
        Register("town",()=>Require(_p.Location==_pair.Town.Location && !_pair.Field.Deleted,"Portal did not reach town"));
        Register("final",()=>Require(_pair.Field.Deleted && _pair.Town.Deleted && TownPortals.For(_p)==null && _tome.Charges==_charges-1 && _pen.UsesRemaining==19,"Portal return/tome identity failed"));
    }
    private static void Require(bool v,string why)=>InscriptionChecks.Require(v,why);
    private static void Register(string suffix,Action action)
    {
        string name="inscription-ui-"+suffix;
        ConsoleInputHandler.RegisterCommand(name,"Actual tome crafting/targets and portal belt/return",_=>Core.LoopContext.Post(()=>
        { try { action(); var gate=TownPortals.For(_p)?.Field; File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE"),JsonSerializer.Serialize(new { charges=_tome?.Charges, dx=gate==null?0:gate.X-_p.X,dy=gate==null?0:gate.Y-_p.Y,dz=gate==null?0:gate.Z-_p.Z })); _logger.Information("{Command}: passed",name); } catch(Exception e) { _logger.Error(e,"{Command}: failed",name); } }));
    }
}
