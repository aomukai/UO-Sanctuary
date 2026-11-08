// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class ApocalypseCastChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ApocalypseCastChecks));
    private const CombatAbility Ability=CombatAbility.Apocalypse;
    private sealed record Saved(uint Owner,ApocalypseGroundField Field,ulong Seed,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"apocalypse-cast-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Learned Apocalypse SC/startup mana/ground/timing/input",_=>Core.LoopContext.Post(()=>
    {try{Log.Information("{Name}: passed",name);action();}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure()
    {
        foreach(bool reload in new[]{false,true})
        {
            bool again=reload;string name=again?"apocalypse-cast-reload-check":"apocalypse-cast-check";
            Register(name,()=>FixtureNetwork.Connected(name+"-result",network=>{try{if(again){Reload(network);}else{Check(network);}Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}}));
        }
        foreach(string suffix in new[]{"left","right","quick"}){string choice=suffix;Register("apocalypse-cast-"+choice+"-check",()=>FixtureNetwork.Connected("apocalypse-cast-"+choice+"-result",network=>Input(network,choice)));}
    }
    private static ApocalypseGroundField[] Fields(PlayerMobile p)=>ApocalypseGround.Snapshot().Fields.Where(s=>s.Owner==p.Serial.Value).ToArray();
    private static PlayerMobile Owner()
    {
        var p=ClassSkillChecks.Player(DiabloClass.Warlock);p.AddItem(new Backpack());FixtureSpace.Corridor(p,6920,24,3520);Ready(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);return p;
    }
    private static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;FireGround.Delay(p,Ability,TimedEffects.Now.AddSeconds(-2));}
    private static void Check(NetState network)
    {
        var p=Owner();FixtureNetwork.Authenticate(network,p,"ApocalypseCast");var point=new Point3D(p.X+3,p.Y,p.Z);int cost=SkillMechanics.Values(p,Ability).Mana256,mana=NecromancerEffects.ManaBalance256(p);
        Require(cost==8192&&ClassCombat.Available(p,Ability)&&SkillBindings.Instance.Assign(p,true,Ability)&&SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Apocalypse learned rank1 mana/bindings");
        var old=ApocalypseGround.SeedLow;int seedCalls=0;ApocalypseGround.SeedLow=()=>{seedCalls++;return 42;};
        try
        {
            Require(ClassCombat.Perform(p,Ability,null,point,Core.TickCount),"Apocalypse SC start");var s=ClassCombat.For(p);long effect=s.EffectAt;
            Require(s.Timing==WarlockCastClock.Calculate(CombatTiming.Fcr(p))&&NecromancerEffects.ManaBalance256(p)==mana&&Fields(p).Length==0,"Windup paid or marker allocated early");
            ClassCombat.Stop(p);Require(!ClassCombat.ResolveAction(p,effect)&&Fields(p).Length==0&&NecromancerEffects.ManaBalance256(p)==mana&&seedCalls==0,"Canceled windup paid or allocated");
            Ready(p);NecromancerEffects.SetMana256(p,mana);Require(ClassCombat.Perform(p,Ability,null,point,Core.TickCount),"Apocalypse paid start");effect=ClassCombat.For(p).EffectAt;
            Require(ClassCombat.ResolveAction(p,effect)&&!ClassCombat.ResolveAction(p,effect)&&Fields(p).Single()is{Rank:1,Remaining:50}&&NecromancerEffects.ManaBalance256(p)==mana-cost&&seedCalls==1,"Once-only startup payment/field/seed init");
            var cd=FireGround.Snapshot().Cooldowns.Single(c=>c.Owner==p.Serial.Value&&c.Ability==Ability);
            Require(!FireGround.Ready(p,Ability,cd.UntilUtc.AddTicks(-1))&&FireGround.Ready(p,Ability,cd.UntilUtc)&&cd.UntilUtc>=Fields(p).Single().BornUtc.AddMilliseconds(1800),"45frame localdelay boundary");
            var original=JsonSerializer.Serialize(ApocalypseGround.Snapshot());Ready(p);NecromancerEffects.SetMana256(p,cost-1);
            Require(!ClassCombat.Perform(p,Ability,null,point,Core.TickCount)&&JsonSerializer.Serialize(ApocalypseGround.Snapshot())==original&&NecromancerEffects.ManaBalance256(p)==cost-1,"Underfunded startup mutated roster/stream/resource");
            Ready(p);NecromancerEffects.SetMana256(p,mana);Require(ClassCombat.Perform(p,Ability,null,new(point.X+2,point.Y,point.Z),Core.TickCount),"Late resource control start");effect=ClassCombat.For(p).EffectAt;NecromancerEffects.SetMana256(p,0);
            Require(!ClassCombat.ResolveAction(p,effect)&&Fields(p).Length==1&&NecromancerEffects.ManaBalance256(p)==0,"Late mana failure allocated or changed old roster");
            Ready(p);NecromancerEffects.SetMana256(p,mana);var blocked=new Point3D(point.X+4,point.Y,point.Z);Require(ClassCombat.Perform(p,Ability,null,blocked,Core.TickCount),"Late terrain control start");effect=ClassCombat.For(p).EffectAt;original=JsonSerializer.Serialize(ApocalypseGround.Snapshot());
            int id=Enumerable.Range(0,0x4000).First(i=>TileData.ItemTable[i].Impassable&&TileData.ItemTable[i].Height>=16);var wall=new Static(id);wall.MoveToWorld(blocked,p.Map);
            try{Require(!ClassCombat.ResolveAction(p,effect)&&NecromancerEffects.ManaBalance256(p)==mana&&JsonSerializer.Serialize(ApocalypseGround.Snapshot())==original,"Failed placement paid or allocated");}finally{wall.Delete();}
            var target=FireBoltChecks.Target(p,3);Ready(p);NecromancerEffects.SetMana256(p,mana);Require(ClassCombat.Perform(p,Ability,target,target.Location,Core.TickCount),"Selected target start");effect=ClassCombat.For(p).EffectAt;target.MoveToWorld(new(point.X+1,point.Y,point.Z),p.Map);
            Require(ClassCombat.ResolveAction(p,effect)&&Fields(p).Any(f=>f.Origin==target.Location)&&Fields(p).Length==2&&seedCalls==1,"Release target/current point or saved stream reuse");
            int life=SkillHealth.Balance256(target),paid=NecromancerEffects.ManaBalance256(p);ulong seed=ApocalypseGround.Seed(p);for(int i=0;i<2;i++){SourceRandom.Limited(ref seed,24*256);}
            ApocalypseGround.Tick(TimedEffects.Now.AddSeconds(3),true,p.Serial.Value);
            Require(Fields(p).Length==0&&SkillHealth.Balance256(target)<life&&NecromancerEffects.ManaBalance256(p)==paid&&ApocalypseGround.Seed(p)==seed,"Learned explosions did not share caster stream or billed again");target.Delete();
            Ready(p);NecromancerEffects.SetMana256(p,mana);Require(ClassCombat.Perform(p,Ability,null,point,Core.TickCount)&&ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt),"Saved learned cast");
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,Fields(p).Single(),ApocalypseGround.Seed(p))));
        }
        finally{ApocalypseGround.SeedLow=old;}
        Log.Information("Learned Apocalypse:pinned start0,successful-release mana32 once;free cancellation/late terrain,underfunded/late mana preserve roster;45frame localdelay,current selected point,saved owner stream,actual delayed HP and three bindings. Current generic caller remains a reference limit.");
    }
    private static void Reload(NetState network)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);Require(p!=null&&saved.Generation<2,"Saved Apocalypse owner");
        var now=TimedEffects.Now;var expiry=saved.Field.BornUtc.AddMilliseconds(2000);ApocalypseGround.Tick(now>expiry?now:expiry,false,p.Serial.Value);
        Require(Fields(p).Length==0&&World.FindItem((Serial)saved.Field.Marker)==null&&ApocalypseGround.Seed(p)==saved.Seed&&ClassCombat.For(p).PendingAbility==CombatAbility.None
            &&SkillBindings.Instance.Get(p).Right==Ability&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Warlock,7)==Ability,"Saved learned binding/seed or offline expiry replay");
        FixtureNetwork.Authenticate(network,p,"ApocalypseReload");File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
    }
    private static void Input(NetState network,string choice)
    {
        var p=Owner();p.Female=choice=="quick";
        if(p.Female)
        {while(p.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Witch Strength");}var gloves=NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey=="Magefist").Id,99,new Random(173),true);p.AddToBackpack(gloves);Require(p.EquipItem(gloves),"Witch FCR gear");ClassGear.Refresh(p);}
        Require(SkillBindings.Instance.Assign(p,choice=="left",Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Apocalypse socket bindings");var account=FixtureNetwork.Authenticate(network,p,"ApocalypseWire");Ready(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);
        int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,Ability).Mana256,rank=SkillMechanics.Rank(p,Ability);var target=FireBoltChecks.Target(p,3);var point=target.Location;int life=SkillHealth.Balance256(target);
        string label="apocalypse-cast-"+choice;long deadline=Core.TickCount+8000;bool began=false,emitted=false,finished=false;int afterCommit=0;
        void Cleanup(){ClassCombat.Stop(p);ApocalypseGround.Clear(p);target.Delete();p.Delete();account.Delete();}
        void Inspect()
        {
            if(finished){return;}try
            {
                if(Core.TickCount>=deadline){throw new InvalidOperationException("Apocalypse input timeout");}var s=ClassCombat.For(p);
                if(s.PendingAbility==Ability&&!began)
                {Require(s.Timing==WarlockCastClock.Calculate(CombatTiming.Fcr(p))&&NecromancerEffects.ManaBalance256(p)==mana&&Fields(p).Length==0,"Socket windup paid/allocated or SC timing changed");began=true;Log.Information("{Label}: windup",label);}
                var fields=Fields(p);if(!emitted&&fields.Length==1)
                {Require(began&&fields[0].Rank==rank&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Socket field/rank/once-only release payment");emitted=true;afterCommit=NecromancerEffects.ManaBalance256(p);}
                if(!emitted||fields.Length!=0||SkillHealth.Balance256(target)>=life){Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Inspect));return;}
                Require(NecromancerEffects.ManaBalance256(p)>=afterCommit,"Explosion charged a second fee");finished=true;Cleanup();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){finished=true;Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target=0 x={X} y={Y} z={Z}",label,(int)Ability,(int)DiabloClass.Warlock,point.X,point.Y,point.Z);Inspect();}));
    }
}
