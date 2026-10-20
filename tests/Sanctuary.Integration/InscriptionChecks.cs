using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Engines.Craft;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Targeting;

namespace Sanctuary.Integration;

public static class InscriptionChecks
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(InscriptionChecks));
    internal sealed class Roll(double success = 0, params int[] values) : Random
    {
        private int _index;
        public override double NextDouble() => success;
        public override int Next(int max) => _index < values.Length ? Math.Min(max - 1, values[_index++]) : max - 1;
    }
    private sealed record Saved(uint Owner, uint Item, int Charges, int Capacity, CraftQuality Quality, ItemOrigin Origin, string Type);
    private static string Manifest => Path.Join(Core.BaseDirectory,"inscription-checks.json");
    public static void Configure() { Register("inscription-check",Check); Register("inscription-reload-check",Reload); }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name,"Utility scroll/tome recipes, charges, bonus and portals",_ => Core.LoopContext.Post(() =>
    { try { action(); _logger.Information("{Command}: passed",name); } catch(Exception e) { _logger.Error(e,"{Command}: failed",name); } }));
    internal static void Require(bool v,string why) => RuneChecks.Require(v,why);
    internal static PlayerMobile Player()
    {
        var p=ImbuingChecks.Player(SkillName.Inscribe); p.RawDex=1; p.RawInt=1; p.Hits=p.HitsMax;
        FixtureSpace.Place(p,new Point3D(5650,1100,0),Map.Trammel);
        CraftFacilities.Register(new(SkillName.Inscribe,"Trammel",p.X+1,p.Y,p.Z,0xB4A)); p.Skills.Inscribe.SetLockNoRelay(SkillLock.Locked); return p;
    }
    internal static CraftTransactions.Request Ticket(InscriptionRequest q) => new(q.Owner,DefInscription.CraftSystem,q.Recipe,null,q.Tool,0) { Scribing=q };
    internal static void Inputs(PlayerMobile p,InscriptionRecipe row)
    { foreach(var i in row.Ingredients) { var item=EquipmentCrafting.Resolve(i.Type).CreateInstance<Item>(); item.Amount=i.Amount; p.Backpack.DropItem(item); } }
    private static void Check()
    {
        var saved=new List<Saved>();
        foreach(var row in InscriptionCrafting.Recipes)
        {
            var p=Player(); var pen=new ScribesPen(100); p.Backpack.DropItem(pen); Inputs(p,row);
            if(row.Skill>0) { p.Skills.Inscribe.Base=row.Skill-.1; Require(InscriptionCrafting.Quote(p,pen,row)==null,"Under-skilled recipe accepted"); }
            p.Skills.Inscribe.Base=Math.Min(120,row.Skill+50); var q=InscriptionCrafting.Quote(p,pen,row);
            Require(q!=null && q.Chance>0,"Recipe unavailable "+row.Id); var ticket=Ticket(q);
            Require(CraftTransactions.Complete(ticket,new Roll()) && !CraftTransactions.Complete(ticket),"Craft failed/replayed");
            var output=p.Backpack.Items.Single(i=>i.GetType()==EquipmentCrafting.Resolve(row.Type));
            Require(output.Origin==ItemOrigin.Crafted && output.PlayerConstructed && pen.UsesRemaining==99,"Craft provenance/cost wrong");
            foreach(var i in row.Ingredients) { Require(p.Backpack.GetAmount(EquipmentCrafting.Resolve(i.Type))==0,"Craft input not consumed"); }
            if(output is UtilityTome t) { Require(t.Charges==row.Charges && t.Capacity==row.Charges && t.Quality==CraftQuality.Average,"Wrong tome ladder"); saved.Add(new(p.Serial.Value,t.Serial.Value,t.Charges,t.Capacity,t.Quality,t.Origin,t.GetType().Name)); }
            Inputs(p,row); q=InscriptionCrafting.Quote(p,pen,row); Require(CraftTransactions.Complete(Ticket(q),new Roll(1)),"Failure not committed");
            foreach(var i in row.Ingredients) { Require(p.Backpack.GetAmount(EquipmentCrafting.Resolve(i.Type))==i.Amount-(i.Amount+1)/2,"Failure loss wrong"); }
            Require(pen.UsesRemaining==98,"Failure pen use wrong");
        }
        QualityAndBonus(saved); Stale(); Identify(saved); Portals();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    }
    private static void QualityAndBonus(List<Saved> saved)
    {
        var row=InscriptionCrafting.Recipes.First(r=>r.Type=="IdentifyTome" && r.Skill==100);
        foreach(var (skill,roll,extra) in new[]{(109.9,0,0),(110.0,0,2),(110.0,1,1),(120.0,11,0)})
        {
            var p=Player(); var pen=new ScribesPen(20); p.Backpack.DropItem(pen); p.Skills.Inscribe.Base=skill; Inputs(p,row);
            Require(CraftTransactions.Complete(Ticket(InscriptionCrafting.Quote(p,pen,row)),new Roll(0,roll,99)),"Quality craft failed");
            var t=p.Backpack.Items.OfType<UtilityTome>().Single(); Require(t.Charges==10+extra,"Quality eligibility/charge bonus wrong");
            var copy=new IdentifyTome(); t.Dupe(copy); p.Backpack.DropItem(copy); Require(copy.Charges==t.Charges && copy.Capacity==t.Capacity && copy.Quality==t.Quality,"Dupe refilled/lost tome identity");
            saved.Add(new(p.Serial.Value,copy.Serial.Value,copy.Charges,copy.Capacity,copy.Quality,copy.Origin,copy.GetType().Name));
        }
        foreach(int bonus in new[]{0,1})
        {
            var p=Player(); p.Skills.Inscribe.Base=120; var pen=new ScribesPen(20); p.Backpack.DropItem(pen); Inputs(p,row);
            Require(CraftTransactions.Complete(Ticket(InscriptionCrafting.Quote(p,pen,row)),new Roll(0,99,bonus)),"Bonus attempt failed");
            Require(p.Backpack.Items.OfType<TeleportScroll>().Count()==(bonus==0?1:0),"Bonus 1% boundary wrong");
        }
        var full=Player(); var tool=new ScribesPen(20); full.Backpack.DropItem(tool); full.Skills.Inscribe.Base=120;
        full.Backpack.DropItem(new Leather(2)); full.Backpack.DropItem(new BlankScroll(11)); full.Backpack.DropItem(new BlackPearl(11)); full.Backpack.MaxItems=full.Backpack.TotalItems;
        Require(!CraftTransactions.Complete(Ticket(InscriptionCrafting.Quote(full,tool,row)),new Roll(0,0,0)) && tool.UsesRemaining==20 && full.Backpack.GetAmount(typeof(BlackPearl))==11,"Full pack lost potential bonus/resources");
    }
    private static void Stale()
    {
        var p=Player(); var row=InscriptionCrafting.Recipes[1]; var pen=new ScribesPen(20); p.Backpack.DropItem(pen); Inputs(p,row);
        var q=InscriptionCrafting.Quote(p,pen,row); pen.UsesRemaining--; Require(!CraftTransactions.Complete(Ticket(q)) && p.Backpack.GetAmount(typeof(Leather))==1,"Altered tool accepted");
        q=InscriptionCrafting.Quote(p,pen,row); var bag=new Bag(); p.Backpack.DropItem(bag); bag.DropItem(p.Backpack.FindItemByType<Leather>()); Require(!CraftTransactions.Complete(Ticket(q)),"Moved input accepted");
        q=InscriptionCrafting.Quote(p,pen,row); InscriptionCrafting.Invalidate(p); Require(!CraftTransactions.Complete(Ticket(q)),"Canceled quote accepted");
        var forged=new CraftItem(typeof(IdentifyTome),"Forged","Forged"); forged.AddSkill(SkillName.Inscribe,0,50);
        Require(!CraftTransactions.Complete(new(p,DefInscription.CraftSystem,forged,null,pen,0)),"Unbound tome produced");
    }
    private static void Identify(List<Saved> saved)
    {
        var p=Player(); var t=new IdentifyTome(); t.Finish(3,CraftQuality.Superior); p.Backpack.DropItem(t);
        var known=new Longsword(); ClassGear.Profiles.Convert(known,99); p.Backpack.DropItem(known);
        Require(!Identification.Reveal(p,t,known) && t.Charges==5,"Known gear spent tome charge");
        t.OnDoubleClick(p); p.Target.Cancel(p,TargetCancelType.Canceled); Require(t.Charges==5,"Cancel consumed charge");
        for(int i=0;i<5;i++)
        {
            var gear=IdentificationChecks.Unknown(p,new Longsword()); t.OnDoubleClick(p); var target=p.Target; target.Invoke(p,gear); target.Invoke(p,gear);
            Require(ClassGear.State(gear).Identified && (i==4?t.Deleted:t.Charges==4-i),"Tome charged twice or last charge retained");
        }
        var scroll=new IdentifyScroll(); p.Backpack.DropItem(scroll); Require(PotionBelts.Load(p,0,scroll) && PotionBelts.Use(p,0),"Identify scroll could not use belt");
        p.Target.Invoke(p,IdentificationChecks.Unknown(p,new Longsword())); Require(scroll.Deleted && PotionBelts.Entry(p,0)==null,"Belt identify did not consume/compact");
        t=new IdentifyTome(); p.Backpack.DropItem(t); Require(!PotionBelts.Load(p,0,t),"Tome loaded into single-use belt slot");
        var partial=IdentificationChecks.Unknown(p,new Longsword()); Require(Identification.Reveal(p,t,partial) && t.Charges==2,"Partial tome use failed");
        saved.Add(new(p.Serial.Value,t.Serial.Value,t.Charges,t.Capacity,t.Quality,t.Origin,t.GetType().Name));
    }
    private static void Portals()
    {
        var p=Player(); p.MoveToWorld(CombatPractice.FindFloor(Map.Trammel,1382,1495),Map.Trammel); var item=new TownPortalTome(); p.Backpack.DropItem(item);
        var mana=p.Mana; Require(TownPortals.Open(p,item) && item.Charges==2 && p.Mana==mana,"Portal use/charge wrong");
        var pair=TownPortals.For(p); var stranger=Player(); stranger.MoveToWorld(pair.Field.Location,p.Map);
        Require(!TownPortals.Travel(stranger,pair.Field),"Foreign portal access allowed"); stranger.Delete();
        p.Location=new Point3D(pair.Field.X+1,pair.Field.Y,pair.Field.Z);
        p.Direction=Direction.West;
        Require(!p.Move(Direction.West) && p.Location==pair.Town.Location && TownPortals.For(p)==pair,"Native step overwrote outbound portal travel");
        Require(TownPortals.Travel(p,pair.Town) && TownPortals.For(p)==null && pair.Field.Deleted && pair.Town.Deleted,"Return trip did not close pair");
        ClassCombat.For(p).NextAction=Core.TickCount; Require(TownPortals.Open(p,item),"Second portal failed"); pair=TownPortals.For(p);
        ClassCombat.For(p).NextAction=Core.TickCount; Require(TownPortals.Open(p,item) && item.Deleted && pair.Field.Deleted && pair.Town.Deleted,"Replacement/last charge wrong");
        pair=TownPortals.For(p); p.Kill(); Require(pair.Field.Deleted && pair.Town.Deleted && TownPortals.For(p)==null,"Death retained portal");
        p=Player(); p.MoveToWorld(CombatPractice.FindFloor(Map.Trammel,1382,1495),Map.Trammel); var s=new TownPortalScroll(); p.Backpack.DropItem(s);
        Require(PotionBelts.Load(p,0,s) && PotionBelts.Use(p,0) && s.Deleted && p.Target==null,"Belt Town Portal failed");
        pair=TownPortals.For(p); EventSink.InvokeLogout(p); Require(pair.Field.Deleted && pair.Town.Deleted,"Logout cleanup failed");
        ClassCombat.For(p).NextAction=Core.TickCount; s=new TownPortalScroll(); p.Backpack.DropItem(s); Require(TownPortals.Open(p,s),"Disconnect fixture failed"); pair=TownPortals.For(p); EventSink.InvokeDisconnected(p); Require(pair.Field.Deleted && pair.Town.Deleted,"Disconnect retained portal");
        p.Internalize(); s=new TownPortalScroll(); p.Backpack.DropItem(s); Require(!TownPortals.Open(p,s) && !s.Deleted,"Invalid travel consumed scroll"); p.Delete();
        p=Player(); p.MoveToWorld(CombatPractice.FindFloor(Map.Trammel,1382,1495),Map.Trammel); s=new TownPortalScroll(); p.Backpack.DropItem(s); Require(TownPortals.Open(p,s),"Restart portal fixture failed"); pair=TownPortals.For(p);
        File.WriteAllText(Path.Join(Core.BaseDirectory,"portal-restart.json"),JsonSerializer.Serialize(new[]{pair.Field.Serial.Value,pair.Town.Serial.Value}));
    }
    private static void Reload()
    {
        foreach(uint id in JsonSerializer.Deserialize<uint[]>(File.ReadAllText(Path.Join(Core.BaseDirectory,"portal-restart.json")))) { Require(World.FindItem((Serial)id)==null,"Restart retained saved portal endpoint"); }
        foreach(var q in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        {
            var p=World.FindMobile((Serial)q.Owner) as PlayerMobile; var t=World.FindItem((Serial)q.Item) as UtilityTome;
            Require(t!=null && t.Parent==p.Backpack && t.GetType().Name==q.Type && t.Charges==q.Charges && t.Capacity==q.Capacity && t.Quality==q.Quality && t.Origin==q.Origin,"Saved tome changed/refilled");
        }
    }
}
