// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
using Server.Spells;
namespace Sanctuary.Integration;

public static class LightningStrikeChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(LightningStrikeChecks));
    private sealed class Rolls:Random{public override int Next(int n)=>Math.Max(0,n-1);}
    private sealed record Saved(uint Owner,uint Weapon,uint Primary,uint Secondary,int Map,Point3D Point,int Mana,int Life,int PrimaryLife,int SecondaryLife,ChainFlight Flight,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"lightning-strike-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("lightning-strike-check",Check);Register("lightning-strike-reload-check",Reload);
        foreach(bool quick in new[]{false,true})
        {bool q=quick;string label=q?"lightning-strike-quick":"lightning-strike-right";Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,q)));}
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Approved Lightning Strike melee and shared saved chain",_=>Core.LoopContext.Post(()=>
    {try{Controlled(run);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Controlled(Action run)
    {
        var hit=AmazonMelee.HitRoll;var elemental=AmazonMelee.ElementRoll;var physical=AmazonMelee.PhysicalRoll;var lightning=AmazonMelee.LightningRoll;var effects=AmazonMelee.Effects;var wear=AmazonMelee.WearRoll;
        try{AmazonMelee.HitRoll=()=>0;AmazonMelee.ElementRoll=()=>0;AmazonMelee.PhysicalRoll=_=>0;AmazonMelee.LightningRoll=_=>0;AmazonMelee.Effects=new Rolls();AmazonMelee.WearRoll=new Rolls();run();}
        finally{AmazonMelee.HitRoll=hit;AmazonMelee.ElementRoll=elemental;AmazonMelee.PhysicalRoll=physical;AmazonMelee.LightningRoll=lightning;AmazonMelee.Effects=effects;AmazonMelee.WearRoll=wear;}
    }
    private static PlayerMobile Owner()
    {
        var p=ChargedStrikeChecks.Fresh();FixtureSpace.Corridor(p,6200,20,3000);ChargedStrikeChecks.Train(p,"amazon.lightningstrike");ChargedStrikeChecks.Equip(p,new Spear());
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.LightningStrike)&&SkillBindings.Instance.Assign(p,false,CombatAbility.LightningStrike)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.LightningStrike),"Lightning Strike bindings");return p;
    }
    private static BaseCreature Enemy(PlayerMobile p,int dx=1,int dy=0)
    {var t=ChargedStrikeChecks.Enemy(p);t.MoveToWorld(new(p.X+dx,p.Y+dy,p.Z),p.Map);t.SetHits(50000);t.Hits=t.HitsMax;TimedEffects.PreventHealing(t,TimedEffects.Now);return t;}
    private static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,p.ManaMax*256-97);}
    private static ChainFlight Flight(long id)=>ChainLightning.Snapshot().SingleOrDefault(f=>f.Cast==id);
    private static ChainFlight[] Flights(PlayerMobile p)=>ChainLightning.Snapshot().Where(f=>f.Owner==p.Serial.Value).ToArray();
    private static void Step(long id){if(Flight(id)is{} f){ChainLightning.Tick(f.NextUtc,true,id);}}
    private static void Through(long id,int count){for(int i=0;i<count&&Flight(id)!=null;i++){Step(id);}}
    private static void Replace(ChainFlight f)=>ChainLightning.Restore(ChainLightning.Snapshot().Where(x=>x.Cast!=f.Cast).Append(f).ToArray());
    private static void Clean(PlayerMobile p,params Mobile[] targets){ChainLightning.Cancel(p);foreach(var t in targets){t?.Delete();}p.Delete();}
    private static string Json(object value)=>JsonSerializer.Serialize(value);
    private static AmazonMelee.Prepared Begin(PlayerMobile p,Mobile t)
    {Require(ClassCombat.Perform(p,CombatAbility.LightningStrike,t,t.Location,Core.TickCount),"Learned Lightning Strike start");ClassCombat.ReleaseCast(p);return ClassCombat.For(p).AmazonAttack;}
    private static void Commit(PlayerMobile p,AmazonMelee.Prepared prepared)
    {Require(!ClassCombat.ResolveAction(p,prepared.EffectAt-1)&&ClassCombat.ResolveAction(p,prepared.EffectAt)&&!ClassCombat.ResolveAction(p,prepared.EffectAt),"Lightning Strike once-only effect");}
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"lightning-strike-damage-source.json")));int count=0;
        var shape=SkillMechanics.For(CombatAbility.LightningStrike);
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var fields=shape.SourceFields.ToDictionary(x=>x.Key,x=>x.Value);fields["EDmgSymPerCalc"]=JsonSerializer.SerializeToElement(row[1].GetInt32()*11);
            var values=new SkillValues(shape with{SourceFields=fields},_=>row[0].GetInt32(),_=>0,99);var range=AmazonMelee.LightningRange(values,row[2].GetInt32());
            Require(range.Min==row[8].GetInt32()&&range.Max==row[9].GetInt32()&&row[6].GetInt32()==0&&row[7].GetInt32()==0&&row[10].GetInt32()==0&&row[11].GetInt32()==0&&row[12].GetInt32()==0,"Complete source spell-only creator/endpoint differential");count++;
        }
        Require(count==36720,"Missing Lightning Strike creator source cases");
        using var filter=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"chain-child-filter-source.json")));
        Require(filter.RootElement.GetArrayLength()==32&&filter.RootElement.EnumerateArray().Any(r=>r[0].GetInt32()==0x88583&&r[1].GetInt32()==1&&r[5].GetInt32()==1&&r[6].GetInt32()==0),"Source child no-LOS control missing");
        foreach(int rank in new[]{1,20,255})
        {var v=new SkillValues(shape,_=>rank,_=>0,99);Require(v.Mana256==2304&&v.Damage256(false)==256&&v.Eval("calc2")==rank+1&&AmazonMelee.PhysicalBonus(CombatAbility.LightningStrike,v,false)==20,"Pinned start10 counters/cost/ED/minimum");}
    }
    private static void Mechanics()
    {
        var p=Owner();var primary=Enemy(p);var seed=ChainLightning.SeedLow;int seeds=0;ChainLightning.SeedLow=()=>{seeds++;return 73;};
        try
        {
            Ready(p);int mana=NecromancerEffects.ManaBalance256(p),hp=SkillHealth.Balance256(primary);var prepared=Begin(p,primary);
            Require(prepared.Hit&&!prepared.Basic&&prepared.Lightning256>0&&NecromancerEffects.ManaBalance256(p)==mana-2304&&SkillHealth.Balance256(primary)==hp,"Start10 preparation/start payment");
            Commit(p,prepared);Require(hp-SkillHealth.Balance256(primary)==prepared.Impact.LifeDamage256&&Flights(p).Length==0&&seeds==0,$"Solo loss={hp-SkillHealth.Balance256(primary)},prepared={prepared.Impact.LifeDamage256},seeds={seeds},flights={Json(Flights(p))},owner={p.Location},primary={primary.Location}");
            var secondary=Enemy(p,7);
            try
            {
                Ready(p);AmazonMelee.HitRoll=()=>1;hp=SkillHealth.Balance256(primary);int other=SkillHealth.Balance256(secondary);prepared=Begin(p,primary);Commit(p,prepared);
                var f=Flights(p).Single();long id=f.Cast;var values=SkillMechanics.Values(p,CombatAbility.LightningStrike);
                Require(!prepared.Hit&&f.StrikeContext?.OpeningPrimary==primary.Serial.Value&&f.Remaining==values.Eval("calc2")&&f.X==primary.X&&f.Y==primary.Y
                    &&f.Min256==values.Damage256(false)&&f.Max256==values.Damage256(true)&&f.AimTarget==secondary.Serial.Value&&SkillHealth.Balance256(primary)==hp,"Miss emission/origin/spell-only source/counter");
                for(int i=0;i<25&&SkillHealth.Balance256(secondary)==other;i++){Step(id);Require(SkillHealth.Balance256(primary)==hp,"Opening chain re-hit its melee primary");}
                Require(SkillHealth.Balance256(secondary)<other&&Flight(id)is{StrikeContext.OpeningPrimary:0,Remaining:1},"Delayed neighbor hit/shared child countdown");Through(id,50);Require(Flight(id)==null,"Finite Lightning Strike chain");
                Ready(p);NecromancerEffects.SetMana256(p,17);mana=NecromancerEffects.ManaBalance256(p);prepared=Begin(p,primary);Commit(p,prepared);Require(prepared.Basic&&prepared.Lightning256==0&&Flights(p).Length==0&&NecromancerEffects.ManaBalance256(p)==mana,"Low-mana ordinary Attack fallback emitted/paid");
                AmazonMelee.HitRoll=()=>0;Ready(p);prepared=Begin(p,primary);ClassCombat.Stop(p);Require(!ClassCombat.ResolveAction(p,prepared.EffectAt)&&Flights(p).Length==0,"Stop retained Lightning Strike packet");
                Ready(p);prepared=Begin(p,primary);ClassMovement.Moved(p,Direction.South);Require(!ClassCombat.ResolveAction(p,prepared.EffectAt)&&ClassCombat.For(p).AmazonAttack==null,"Movement retained prepared Lightning Strike");
                Ready(p);prepared=Begin(p,primary);Require(HitRecovery.Apply(p,p.HitsMax,Core.TickCount)&&!ClassCombat.ResolveAction(p,prepared.EffectAt),"Interruptible A1 copied a multihit lock");
                Ready(p);var origin=p.Location;p.MoveToWorld(new(1432,1697,0),Map.Trammel);primary.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);mana=NecromancerEffects.ManaBalance256(p);
                Require(!ClassCombat.Perform(p,CombatAbility.LightningStrike,primary,primary.Location,Core.TickCount)&&NecromancerEffects.ManaBalance256(p)==mana,"Town Lightning Strike cast/payment");p.MoveToWorld(origin,Map.Trammel);primary.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);
                Ready(p);SkillHealth.SetBalance256(primary,256);prepared=Begin(p,primary);var point=primary.Location;Commit(p,prepared);f=Flights(p).Single();Require(!primary.Alive&&f.X==point.X&&f.Y==point.Y,"Lethal melee lost retained primary coordinates");ChainLightning.Cancel(p);
            }
            finally{secondary.Delete();}
        }
        finally{ChainLightning.SeedLow=seed;Clean(p,primary);}
    }
    private static void Gear()
    {
        var p=Owner();var primary=Enemy(p);var secondary=Enemy(p,7);
        try
        {
            foreach(string id in new[]{"amazon.powerstrike","amazon.chargedstrike","amazon.lightningbolt"}){ChargedStrikeChecks.Train(p,id,20);}
            if(CombatTiming.Equipped(p)is{} old){p.AddToBackpack(old);}var weapon=(BaseWeapon)PoisonDaggerChecks.Named(p,"Titan's Revenge");var helm=PoisonDaggerChecks.Named(p,"Griffon's Eye");
            Ready(p);int quantity=NamedAmmunition.Remaining(weapon);var prepared=Begin(p,primary);Commit(p,prepared);var f=Flights(p).Single();int rank=f.Rank;
            Require(rank>1&&SkillMechanics.Values(p,CombatAbility.LightningStrike).Eval("EDmgSymPerCalc")==660&&NamedAmmunition.Remaining(weapon)==quantity
                &&f.PlayerPacket.Modifiers.LightningMastery>0,"Real gear/soft rank/hard synergies/melee quantity");
            var raw=AmazonMelee.LightningRange(SkillMechanics.Values(p,CombatAbility.LightningStrike),NamedElements.MasteryPercent(p,"ltng"));Require(f.Min256==raw.Min&&f.Max256==raw.Max,"Chain imported weapon stats or missed mastery");
            p.Backpack.DropItem(helm);ClassGear.Refresh(p);Require(Json(Flights(p).Single())==Json(f),"Issued chain reread removed gear");
            for(int i=0;i<25&&Flight(f.Cast)?.Remaining==f.Remaining;i++){Step(f.Cast);}
            var child=Flight(f.Cast);Require(child!=null&&child.Rank==rank&&child.Min256==256&&child.Max256==SkillMechanics.Values(p,CombatAbility.LightningStrike,rank).Damage256(true),"Child failed retained rank/live mastery recapture");ChainLightning.Cancel(p);
        }
        finally{Clean(p,primary,secondary);}
    }
    private static void Save()
    {
        var p=Owner();var primary=Enemy(p);var secondary=Enemy(p,9);Ready(p);AmazonMelee.HitRoll=()=>1;var prepared=Begin(p,primary);Commit(p,prepared);var f=Flights(p).Single();Step(f.Cast);Step(f.Cast);f=Flight(f.Cast);
        var held=TimedEffects.Now.Add(FixtureSpace.Retention);f=f with{NextUtc=held,UntilUtc=held.AddSeconds(1)};Replace(f);
        foreach(var bad in new[]{f with{StrikeContext=f.StrikeContext with{OpeningPrimary=p.Serial.Value}},f with{StrikeContext=f.StrikeContext with{Profile="unknown"}},f with{Remaining=f.Rank+2},f with{PlayerPacket=null}})
        {string before=Json(ChainLightning.Snapshot());bool refused=false;try{Replace(bad);}catch(InvalidDataException){refused=true;}Require(refused&&Json(ChainLightning.Snapshot())==before,"Invalid strike context restore was not atomic");}
        SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)/2-17);var saved=new Saved(p.Serial.Value,CombatTiming.Equipped(p).Serial.Value,primary.Serial.Value,secondary.Serial.Value,p.Map.MapID,p.Location,
            NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(p),SkillHealth.Balance256(primary),SkillHealth.Balance256(secondary),f,0);
        p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    }
    private static void Check(){Source();Mechanics();Gear();Save();}
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var primary=World.FindMobile((Serial)s.Primary);var secondary=World.FindMobile((Serial)s.Secondary);
        Require(Json(Flight(s.Flight.Cast))==Json(s.Flight)&&SkillHealth.Balance256(p)==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana
            &&SkillHealth.Balance256(primary)==s.PrimaryLife&&SkillHealth.Balance256(secondary)==s.SecondaryLife&&World.FindItem((Serial)s.Weapon)?.Parent==p
            &&SkillBindings.Instance.Get(p).Right==CombatAbility.LightningStrike&&ClassCombat.Existing(p)?.AmazonAttack==null,"Original Lightning Strike save/owner/weapon/wounds/resources/binding/transient state");
        p.MoveToWorld(s.Point,Map.Maps[s.Map]);Step(s.Flight.Cast);Step(s.Flight.Cast);var f=Flight(s.Flight.Cast);
        Require(f!=null&&f.Frame>s.Flight.Frame&&f.X>s.Flight.X&&f.StrikeContext==s.Flight.StrikeContext&&f.PlayerPacket.Seed==s.Flight.PlayerPacket.Seed
            &&SkillHealth.Balance256(primary)==s.PrimaryLife&&SkillHealth.Balance256(secondary)==s.SecondaryLife,"Actual original-flight restart aging/movement changed opening identity/seed or replayed damage");
        var held=TimedEffects.Now.Add(FixtureSpace.Retention);f=f with{NextUtc=held,UntilUtc=held.AddSeconds(1)};Replace(f);p.Internalize();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Flight=f,Generation=s.Generation+1}));
    }
    private static void Input(NetState network,bool quick)
    {
        var p=Owner();var primary=Enemy(p);var secondary=Enemy(p,7);Ready(p);int mana=NecromancerEffects.ManaBalance256(p),other=SkillHealth.Balance256(secondary);string label=quick?"lightning-strike-quick":"lightning-strike-right";
        var account=FixtureNetwork.Authenticate(network,p,label);bool began=false,emitted=false;string last=null;var trace=new System.Collections.Generic.List<string>();long deadline=Core.TickCount+8000;
        void End(){ChainLightning.Cancel(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();Clean(p,primary,secondary);}
        void Observe()
        {
            try
            {
                var session=ClassCombat.For(p);string current=$"pending={session.PendingAbility},attack={session.AmazonAttack!=null},target={session.PendingTarget},primary={primary.Location},secondary={secondary.Location},mana={NecromancerEffects.ManaBalance256(p)},other={SkillHealth.Balance256(secondary)},flights={Json(Flights(p))}";
                if(current!=last&&trace.Count<160){trace.Add(current);last=current;}
                Require(Core.TickCount<deadline&&network.Running,$"Lightning Strike input timeout began={began},emitted={emitted},trace={string.Join(";",trace)}");
                if(session.AmazonAttack is{Ability:CombatAbility.LightningStrike}&&!began)
                {Require(NecromancerEffects.ManaBalance256(p)==mana-2304&&Flights(p).Length==0,"Real input start payment/early emission");began=true;Log.Information("{Label}: windup",label);}
                if(Flights(p).Any(f=>f.StrikeContext!=null)){emitted=true;ClassCombat.Stop(p);}
                if(SkillHealth.Balance256(secondary)==other){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Observe);return;}
                Require(began&&emitted&&NecromancerEffects.ManaBalance256(p)==mana-2304,"Real right/quick emission/delayed neighbor/payment");End();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){End();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>
        {Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.LightningStrike,(int)DiabloClass.Amazon,primary.Serial.Value,primary.X,primary.Y,primary.Z);Observe();});
    }
}
