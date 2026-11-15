// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class BoundDemonLifeChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(BoundDemonLifeChecks));
    private sealed record Saved(int Generation,uint Owner,uint Body,uint Supplier,BoundDemonState State,BattleOrdersState Orders,int Life,int Maximum,int Mana,int Stamina);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"bound-demon-life-fixtures.json");
    private static bool Creating;
    private static void Rates(){var old=Mobile.HitsRegenRateHandler;Mobile.HitsRegenRateHandler=m=>Creating||m.Name=="BoundLifeFixture"?TimeSpan.FromHours(1):old?.Invoke(m)??Mobile.DefaultHitsRate;}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Rates();foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"bound-demon-life-reload-check":"bound-demon-life-check";
            ConsoleInputHandler.RegisterCommand(name,"Existing native BO/Oak base capture,ratio,buff provenance and saves",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>
            {try{if(reload){Reload(n);}else{Scene(n);}Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});}));}
    }
    private static Daemon Body(PlayerMobile owner,int maximum=50000)
    {
        Daemon b;Creating=true;try{b=new Daemon();}finally{Creating=false;}
        b.Name="BoundLifeFixture";b.SetHits(maximum);b.MoveToWorld(new(owner.X+4,owner.Y,owner.Z),owner.Map);b.AIObject.AITimer.Stop();
        MonsterVariantBaselines.Restore(MonsterVariantBaselines.Snapshot().Where(r=>r.Actor!=b.Serial.Value).ToArray());
        SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)/2-71);b.Mana=Math.Max(0,b.ManaMax-7);b.Stam=Math.Max(0,b.StamMax-9);return b;
    }
    private static void Ratio(BaseCreature b,int before,int maximum,string why)
    {Require(Math.Abs(SkillHealth.Balance256(b)-(long)before*SkillHealth.Maximum256(b)/maximum)<=1,why);}
    private static void Bind(PlayerMobile p,Daemon b,int original,BattleOrdersState orders)
    {
        int life=SkillHealth.Balance256(b),maximum=SkillHealth.Maximum256(b),mana=NecromancerEffects.ManaBalance256(b),stamina=BattleOrders.Stamina256(b);var ai=b.AIObject;var serial=b.Serial;
        Require(BoundDemonHosts.BindPrepared(p,b,out var why),"Buffed bind refused: "+why);b.AIObject.AITimer.Stop();
        Require(BoundDemonHosts.State(b).Normal.Life256==original*256&&MonsterVariantBaselines.Find(b).Life256==original*256
            &&BoundDemonHosts.State(b).BaseMaximum256==Math.Min(original,20000)*256,"Existing life bonus became permanent base or cap input");
        Require(BattleOrders.State(b)==orders&&NecromancerEffects.ManaBalance256(b)==mana&&BattleOrders.Stamina256(b)==stamina
            &&ReferenceEquals(ai,b.AIObject)&&b.Serial==serial,"Binding rewrote buff source/deadline,native AI or nonlife resource pools");
        Ratio(b,life,maximum,"Buffed binding changed wound percentage");
    }
    private static void Refusals(PlayerMobile p,PlayerMobile supplier)
    {
        var b=Body(p);
        try
        {
            BattleOrders.Apply(supplier,b,1,TimedEffects.Now);b.AddStatMod(new StatMod(StatType.Str,"BoundLifeUnknown",5,TimeSpan.Zero));
            int life=SkillHealth.Balance256(b);var orders=BattleOrders.State(b);
            Require(!BoundDemonHosts.BindPrepared(p,b,out var why)&&why.Contains("unbuffed baseline")&&!b.Controlled&&BattleOrders.State(b)==orders&&SkillHealth.Balance256(b)==life,"Unknown native strength context was accepted or mutated");
            b.RemoveStatMod("BoundLifeUnknown");
            var old=MonsterVariantBaselines.Capture(b)with{Life256=45000*256};MonsterVariantBaselines.Restore(MonsterVariantBaselines.Snapshot().Append(old).ToArray());
            life=SkillHealth.Balance256(b);Require(!BoundDemonHosts.BindPrepared(p,b,out why)&&why.Contains("disagrees")&&!b.Controlled&&SkillHealth.Balance256(b)==life&&MonsterVariantBaselines.Find(b)==old,"Conflicting historical Normal baseline was silently overwritten");
            bool refused=false;var getter=ClassCombatHooks.CreatureResourceMaximum;
            try
            {
                ClassCombatHooks.CreatureResourceMaximum=(actor,resource,basis)=>actor==b&&resource==0?(getter(actor,resource,basis)??basis)+17:getter(actor,resource,basis);
                try{_=BattleOrders.NativeLifeBase256(b);}catch(NotSupportedException){refused=true;}
            }
            finally{ClassCombatHooks.CreatureResourceMaximum=getter;}
            Require(refused&&BattleOrders.NativeLifeBase256(b)==50000*256,"Opaque maximum getter accepted or observation scope leaked");
            Log.Information("Bound life:unknown strength/opaque maximum and conflicting historical Normal baseline refuse;observation scope restores without ownership or resource mutation");
        }
        finally{BattleOrders.Clear(b);b.Delete();}
    }
    private static void Scene(NetState network)
    {
        Creating=true;PlayerMobile p;try{p=ClassSkillChecks.Player(DiabloClass.Warlock);}finally{Creating=false;}
        p.Name="BoundLifeFixture";FixtureSpace.Corridor(p,7040,35,3300);var account=FixtureNetwork.Authenticate(network,p,"bound-life-");
        var supplier=ClassSkillChecks.Player(DiabloClass.Barbarian);var druid=ClassSkillChecks.Player(DiabloClass.Druid);var facets=SummonDifficulties.Facets();
        Daemon b=null;DruidOakBody oak=null;bool kept=false;
        try
        {
            Require(!WarlockCombat.Implemented(CombatAbility.BindDemon),"Prepared life test opened learned Bind");Refusals(p,supplier);
            foreach(int mode in new[]{0,1,2})
            {
                b=Body(p);if(mode!=0){MonsterVariantBaselines.RememberNormal(b);}
                if(mode==2){MonsterVariantBaselines.Restore(MonsterVariantBaselines.Snapshot().Select(r=>r.Actor==b.Serial.Value?r with{Life256=null}:r).ToArray());}
                BattleOrders.Apply(supplier,b,1,TimedEffects.Now);var orders=BattleOrders.State(b);
                Require(orders is{Percent:35}&&b.HitsMax==65000,"Actual native BO control/cap differs");Bind(p,b,50000,orders);
                Require(SkillHealth.Maximum256(b)==27000*256,"Preexisting BO applied twice or before bound cap");
                int life=SkillHealth.Balance256(b),max=SkillHealth.Maximum256(b);BattleOrders.Clear(b);Require(SkillHealth.Maximum256(b)==20000*256,"BO leaked into permanent Normal maximum");Ratio(b,life,max,"BO removal after binding healed wounds");b.Delete();b=null;
            }
            Log.Information("Bound life:actual BO35 before binding retains creator/supplier/deadline;fresh/cached/old-null Normal bases capture50000 not native capped65000;bound cap20000then BO27000;removal preserves wounds and native mana/stamina");
            druid.MoveToWorld(new(p.X+8,p.Y,p.Z),p.Map);oak=DruidOakBody.CreatePrepared(druid,new(p.X+7,p.Y,p.Z),TimedEffects.Now);
            foreach(bool combined in new[]{false,true})
            {
                b=Body(p);if(combined){BattleOrders.Apply(supplier,b,1,TimedEffects.Now);}
                Require(DruidOakAuras.Apply(oak,b,1,TimedEffects.Now),"Actual Oak application failed");var lease=DruidOakAuras.State(b);var orders=BattleOrders.State(b);
                Require(BattleOrders.LifePercent(b)==(combined?65:30),"Oak/BO shared life pool differs");Bind(p,b,50000,orders);
                Require(DruidOakAuras.State(b)==lease&&SkillHealth.Maximum256(b)==(combined?33000:26000)*256,"Oak provenance or combined postcap pool differs");
                int life=SkillHealth.Balance256(b),max=SkillHealth.Maximum256(b);DruidOakAuras.Clear(b);Ratio(b,life,max,"Oak removal healed bound wounds");
                life=SkillHealth.Balance256(b);max=SkillHealth.Maximum256(b);BattleOrders.Clear(b);Ratio(b,life,max,"Combined BO removal healed bound wounds");b.Delete();b=null;
            }
            Log.Information("Bound life:actual Oak30 and BO35plus Oak30 retain shared leases once above cap;Oak-only26000/combined33000;independent removals preserve relative health without stripping/recasting buffs");
            oak.Delete();oak=null;b=Body(p,12345);BattleOrders.Apply(supplier,b,1,TimedEffects.Now);var savedOrders=BattleOrders.State(b);Bind(p,b,12345,savedOrders);
            Require(SkillHealth.Maximum256(b)==12345*256+ClassResources.Percentage(12345*256,35),"Fractional bound maximum was rounded/inverted");
            foreach(var difficulty in new[]{MonsterDifficulty.Nightmare,MonsterDifficulty.Hell,MonsterDifficulty.Normal})
            {
                int life=SkillHealth.Balance256(b),maximum=SkillHealth.Maximum256(b);
                SummonDifficulties.SetFacets(facets.Where(f=>f.Map!=p.Map.MapID).Append(new FacetDifficulty(p.Map.MapID,difficulty)).ToArray());p.Y++;
                Require(BoundDemonHosts.State(b)is{} state&&state.Difficulty==difficulty&&state.Normal.Life256==12345*256&&BattleOrders.State(b)==savedOrders
                    &&SkillHealth.Maximum256(b)==state.BaseMaximum256+ClassResources.Percentage(state.BaseMaximum256,35),"Facet rescale rewrote native life base or counted BO twice");Ratio(b,life,maximum,"Buffed facet travel reset wounds");b.AIObject.AITimer.Stop();
            }
            SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)/2-17);PersistentMinions.Suspend(p);
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,b.Serial.Value,supplier.Serial.Value,BoundDemonHosts.State(b),savedOrders,SkillHealth.Balance256(b),SkillHealth.Maximum256(b),NecromancerEffects.ManaBalance256(b),BattleOrders.Stamina256(b))));kept=true;
            Log.Information("Bound life:unbuffed12345 remains exact through BO floor/cap boundary and three facets;native saved original identity and active BO snapshot parked without recovery");
        }
        finally
        {
            SummonDifficulties.SetFacets(facets);oak?.Delete();druid.Delete();network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();
            if(!kept){if(b!=null){BattleOrders.Clear(b);b.Delete();}supplier.Delete();p.Delete();BoundDemonHosts.Tick();}
        }
    }
    private static void Reload(NetState network)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Generation<2,"Too many life reloads");
        var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var b=(Daemon)World.FindMobile((Serial)saved.Body);
        Require(b.Map==Map.Internal&&BoundDemonHosts.State(b)==saved.State&&BattleOrders.State(b)==saved.Orders&&b.ControlMaster==p
            &&SkillHealth.Balance256(b)==saved.Life&&SkillHealth.Maximum256(b)==saved.Maximum&&NecromancerEffects.ManaBalance256(b)==saved.Mana&&BattleOrders.Stamina256(b)==saved.Stamina,"Reload changed original buffed body/base/provenance/wounds/resources");
        var account=FixtureNetwork.Authenticate(network,p,"bound-life-reload-");
        try
        {
            PersistentMinions.Resume(p);b.AIObject.AITimer.Stop();Require(BoundDemonHosts.CanAct(b)&&BattleOrders.State(b)==saved.Orders&&SkillHealth.Balance256(b)==saved.Life,"Resume reset buffed life");
            int life=SkillHealth.Balance256(b),maximum=SkillHealth.Maximum256(b);BattleOrders.Clear(b);Require(SkillHealth.Maximum256(b)==12345*256,"Reload baked BO into base");Ratio(b,life,maximum,"Reload BO removal reset wounds");
            BattleOrders.Apply((PlayerMobile)World.FindMobile((Serial)saved.Supplier),b,1,TimedEffects.Now);PersistentMinions.Suspend(p);
            File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1,State=BoundDemonHosts.State(b),Orders=BattleOrders.State(b),Life=SkillHealth.Balance256(b),Maximum=SkillHealth.Maximum256(b),Mana=NecromancerEffects.ManaBalance256(b),Stamina=BattleOrders.Stamina256(b)}));
            Log.Information("Bound life:original active BO/base/native identity and exact wounds/resources survive reload;resume and genuine remove/reapply preserve ratios without permanent buff capture");
        }
        finally{network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
    }
}
