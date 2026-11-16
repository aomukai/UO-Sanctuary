// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class PlayerItemProcWorldChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(PlayerItemProcWorldChecks));
    private sealed record Saved(uint Owner,uint Item,uint Target,Point3D Point,string Gear,string Profile,NpcWorldAllocationState World,int Life,int Mana,int TargetLife,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"player-item-proc-world-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        WireCommands();
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"player-item-proc-world-reload-check":"player-item-proc-world-check";
            ConsoleInputHandler.RegisterCommand(name,"Approved ordered player melee proc callers,shared world allocation and saves",_=>Core.LoopContext.Post(()=>
            {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
    }
    private static void WireCommands()
    {
        foreach(string mode in new[]{"right","quick"})
        {string label="player-item-proc-zeal-"+mode;ConsoleInputHandler.RegisterCommand(label+"-check","Authenticated learned Zeal with approved equipped item procs",_=>Core.LoopContext.Post(()=>
            {try{FixtureNetwork.Connected(label+"-result",n=>Input(n,label));Log.Information("{Label}-check: passed",label);}catch(Exception e){Log.Error(e,"{Label}-check: failed",label);}}));}
    }
    public static void Initialize()=>ConsoleInputHandler.RegisterCommand("player-item-proc-world-save-check","Bind final shared world seed after all component allocations",_=>Core.LoopContext.Post(()=>
    {try{var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));File.WriteAllText(Manifest,Json(saved with{World=NpcWorldAllocation.Capture()}));Log.Information("player-item-proc-world-save-check: passed");}catch(Exception e){Log.Error(e,"player-item-proc-world-save-check: failed");}}));
    private static PlayerProcSave Own(PlayerMobile p,Item item)
    {var all=PlayerItemProcs.Capture();return all with{Items=all.Items.Where(i=>i.Item==item.Serial.Value).ToArray(),Owners=all.Owners.Where(o=>o.Actor==p.Serial.Value).ToArray()};}
    private static ulong Seed(PlayerMobile p)=>PlayerItemProcs.Capture().Owners.Single(o=>o.Actor==p.Serial.Value).Seed;
    private static Item Carrier(PlayerMobile p)
    {
        var item=new Dagger();ClassGear.Profiles.Convert(item,99,GearRarity.Normal,new Random(173));p.AddToBackpack(item);
        var values=new EquipmentStatValues(_=>4,_=>null,_=>null,(_,_,_,_,_)=>{});var graph=new ItemPropertyCreationContext(item,values,default,false,false,false,SourceRandom.InitLow(17),[]);
        var properties=new[]{"att-skill","hit-skill"}.Select(code=>new NamedProperty(code,code,code,true,JsonSerializer.SerializeToElement("Fire Ball"),100,20)).ToArray();
        Require(EquipmentRawProperties.ProjectRecorded(item,graph.Unit,values,default,false,false,false,properties.Select(prop=>(prop,NamedPropertyRuntime.Roll(prop.Code,prop,new Random(173))))).Length==0,"Candidate carrier fields omitted");
        PlayerItemProcs.Register(item,4,graph.Capture() with{HasProperties=true,Properties=graph.Unit.Stats.Last.BaseValues.ToArray()});Require(p.EquipItem(item),"Equip original melee proc carrier");return item;
    }
    private static PlayerWeaponContactPrepared Prepared(PlayerMobile p,BaseCreature target)
        =>PlayerWeaponContact.PrepareOrdinary(p,target,0,0,()=>true,()=>0,()=>0,_=>0,_=>0,new Random(17));
    private static int TickFlights(PlayerMobile p,long after)
    {
        var flights=SorceressSpellMissiles.Snapshot().Flights.Where(f=>f.Owner==p.Serial.Value&&f.Id>after).ToArray();
        foreach(var flight in flights){for(int i=0;i<60&&FireBoltChecks.State(flight.Id)is{} live;i++){SorceressSpellMissiles.Tick(live.NextUtc,true,live.Id);}}
        return flights.Length;
    }
    private static void Check(bool reload)
    {
        Require(PlayerItemProcWorld.Approved&&NamedGear.Available("unique/254"),"Retained complete Hellslayer admission regressed");
        PlayerMobile p;Item item;IronGolemFireBallChecks.Recipient t;int generation=0;
        if(!reload)
        {
            p=ClassSkillChecks.Player(DiabloClass.Paladin);p.Name="PlayerItemFireBallFixture";p.AddItem(new Backpack());FixtureSpace.Corridor(p,6280,32,3700);
            item=Carrier(p);t=new IronGolemFireBallChecks.Recipient();t.SetHits(60000);t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}TimedEffects.PreventHealing(t,TimedEffects.Now);
            var world=NpcWorldAllocation.Capture();Require(PlayerItemProcWorld.Capture(p,t,TimedEffects.Now)==null&&!PlayerItemProcs.Initialized(p)&&NpcWorldAllocation.Capture()==world,"Offline player initialized or consumed world RNG");
        }
        else
        {
            var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=s.Generation+1;Require(generation<=2,"Extra world-proc reload");
            p=(PlayerMobile)World.FindMobile((Serial)s.Owner);item=World.FindItem((Serial)s.Item);t=(IronGolemFireBallChecks.Recipient)World.FindMobile((Serial)s.Target);
            Require(item.Parent==p&&Json(ClassGear.State(item))==s.Gear,"Saved native proc original/gear changed");
            Require(Json(Own(p,item))==s.Profile,"Saved player proc profile differs: actual="+Json(Own(p,item))+" expected="+s.Profile);
            Require(NpcWorldAllocation.Capture()==s.World,"Saved shared world differs: actual="+Json(NpcWorldAllocation.Capture())+" expected="+Json(s.World));
            Require(SkillHealth.Balance256(p)==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillHealth.Balance256(t)==s.TargetLife,
                "Saved health/mana differs: owner="+SkillHealth.Balance256(p)+"/"+s.Life+" mana="+NecromancerEffects.ManaBalance256(p)+"/"+s.Mana+" target="+SkillHealth.Balance256(t)+"/"+s.TargetLife);
            p.MoveToWorld(s.Point,Map.Trammel);
        }
        var block=SourceBlocking.BlockRoll;var hit=PaladinZeal.HitRoll;var observer=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();
        try
        {
            SourceBlocking.BlockRoll=n=>n-1;
            using(PlayerItemProcWorld.Candidate(p))
            {
                var world=NpcWorldAllocation.RequireStream().Value;bool initialized=PlayerItemProcs.Initialized(p);ulong expected=world;ulong expectedActor=initialized?Seed(p):SourceRandom.InitLow(unchecked((int)SourceRandom.Roll(ref expected)));
                var ticket=PlayerItemProcWorld.Capture(p,t,TimedEffects.Now);Require(ticket!=null&&Seed(p)==expectedActor&&NpcWorldAllocation.Capture().Seed==expected,"First contact source allocation/stream independence differs");
                Require(PlayerItemProcWorld.Capture(p,t,TimedEffects.Now)!=null&&Seed(p)==expectedActor&&NpcWorldAllocation.Capture().Seed==expected,"Repeated contact reset player RNG or allocated a second actor seed");
                var prepared=Prepared(p,t);int life=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p);long first=SorceressSpellMissiles.Snapshot().NextId;
                PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits.Add(v);if(v.Phase=="domeleedamage"){Require(SkillHealth.Balance256(t)==life,"Damage proc fired after HP");}}};
                SourceRandom.Roll(ref expectedActor);SourceRandom.Roll(ref expectedActor);SourceRandom.Roll(ref expected);SourceRandom.Roll(ref expected);
                var result=PlayerWeaponContact.Release(p,t,prepared,TimedEffects.Now);
                Require(result.Exit==MonsterContactExit.Finished&&visits.Select(v=>v.Phase).SequenceEqual(new[]{"domeleedamage","domeleeattack"})
                    &&visits.All(v=>v.Outcomes.Length==1&&v.Outcomes[0] is{CallbackResult:1,CastAttempts:1,CastResults:1})
                    &&Seed(p)==expectedActor&&NpcWorldAllocation.Capture().Seed==expected&&NecromancerEffects.ManaBalance256(p)==mana,"Actual ordered contact lost phases,separate streams or free item casting");
                int before=SkillHealth.Balance256(t);Require(TickFlights(p,first)==2&&SkillHealth.Balance256(t)<before&&t.Source==p.Serial.Value,"World allocation failed real item spell damage/ownership");
                PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits.Add(v);}};
                foreach(int flags in new[]{0,16,128,256,32768})
                {
                    visits.Clear();world=NpcWorldAllocation.Capture().Seed;var seed=Seed(p);first=SorceressSpellMissiles.Snapshot().NextId;
                    PlayerWeaponContact.Release(p,t,prepared with{Flags=flags},TimedEffects.Now);
                    Require(visits.Count==1&&visits[0].Phase=="domeleeattack"&&visits[0].Outcomes.Single().CastAttempts==0&&Seed(p)==seed&&NpcWorldAllocation.Capture().Seed==world&&TickFlights(p,first)==0,"Miss/block/dodge fabricated HitFlags32 or consumed proc RNG");
                }
                visits.Clear();life=SkillHealth.Balance256(t);first=SorceressSpellMissiles.Snapshot().NextId;
                var zero=prepared with{Impact=prepared.Impact with{Parts=new(0,0,0,0,0),LifeDamage256=0,Absorbed256=0,AllocatedPoison=null}};
                PlayerWeaponContact.Release(p,t,zero,TimedEffects.Now);Require(visits.Count==2&&SkillHealth.Balance256(t)==life&&TickFlights(p,first)==2,"Landed zero-life contact lost its qualified source events");
                visits.Clear();world=NpcWorldAllocation.Capture().Seed;var same=Seed(p);first=SorceressSpellMissiles.Snapshot().NextId;
                PlayerWeaponContact.Release(p,t,prepared with{RangeCheck=_=>false},TimedEffects.Now);Require(visits.Count==0&&Seed(p)==same&&NpcWorldAllocation.Capture().Seed==world&&TickFlights(p,first)==0,"Out-of-range contact dispatched item phases");
                bool duplicate=false;ticket.Dispatch("domeleeattack",new(0,0,0,0,0));try{ticket.Dispatch("domeleeattack",new(0,0,0,0,0));}catch(InvalidOperationException){duplicate=true;}Require(duplicate,"Contact ticket replayed an attempt phase");
                // A real learned Zeal release reaches these same hooks;the item
                // cast never replaces its active sequence or pays extra mana.
                visits.Clear();PaladinZeal.HitRoll=()=>0;ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,529);
                first=SorceressSpellMissiles.Snapshot().NextId;
                Require(ClassCombat.Perform(p,CombatAbility.Zeal,t,t.Location,Core.TickCount),"Learned Zeal did not begin");
                Require(ClassCombat.ResolveAction(p,s.EffectAt)&&visits.Count==2&&s.PendingAbility==CombatAbility.None&&s.Zeal!=null&&NecromancerEffects.ManaBalance256(p)==17,"Learned Zeal lost ordinary action or paid item mana");
                Require(TickFlights(p,first)==2,"Learned Zeal did not emit both actual item phases");ClassCombat.Stop(p);
            }
            var count=visits.Count;var savedWorld=NpcWorldAllocation.Capture();var savedSeed=Seed(p);PlayerWeaponContact.Release(p,t,Prepared(p,t),TimedEffects.Now);
            Require(visits.Count==count&&Seed(p)==savedSeed&&NpcWorldAllocation.Capture()==savedWorld,"Qualification scope leaked into ordinary offline combat");
        }
        finally{SourceBlocking.BlockRoll=block;PaladinZeal.HitRoll=hit;PlayerItemProcWorld.Observed=observer;ClassCombat.Stop(p);}
        var point=p.Location;p.Internalize();
        File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,item.Serial.Value,t.Serial.Value,point,Json(ClassGear.State(item)),Json(Own(p,item)),NpcWorldAllocation.Capture(),SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(t),generation)));
    }
    private static void Input(NetState network,string label)
    {
        var p=ClassSkillChecks.Player(DiabloClass.Paladin);p.Name="PlayerItemFireBallFixture";p.AddItem(new Backpack());FixtureSpace.Corridor(p,6280,32,3700);
        var item=Carrier(p);var t=new IronGolemFireBallChecks.Recipient();t.SetHits(60000);t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}TimedEffects.PreventHealing(t,TimedEffects.Now);
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.Zeal)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Zeal),"Wire proc Zeal bindings");
        var account=FixtureNetwork.Authenticate(network,p,"player-proc-zeal-");NecromancerEffects.SetMana256(p,529);
        var hit=PaladinZeal.HitRoll;var block=SourceBlocking.BlockRoll;var observed=PlayerItemProcWorld.Observed;PaladinZeal.HitRoll=()=>0;SourceBlocking.BlockRoll=n=>n-1;
        var visits=new List<PlayerProcWorldVisit>();var emitted=new HashSet<long>();long first=SorceressSpellMissiles.Snapshot().NextId;
        ulong expectedWorld=NpcWorldAllocation.RequireStream().Value;ulong expectedActor=SourceRandom.InitLow(unchecked((int)SourceRandom.Roll(ref expectedWorld)));
        SourceRandom.Roll(ref expectedActor);SourceRandom.Roll(ref expectedActor);SourceRandom.Roll(ref expectedWorld);SourceRandom.Roll(ref expectedWorld);
        int before=SkillHealth.Balance256(t);bool began=false;long deadline=Core.TickCount+5000;
        PlayerItemProcWorld.Observed=v=>
        {
            if(v.Owner!=p.Serial.Value){return;}visits.Add(v);
            foreach(var flight in SorceressSpellMissiles.Snapshot().Flights.Where(f=>f.Owner==p.Serial.Value&&f.Id>first)){emitted.Add(flight.Id);}
            if(v.Phase=="domeleedamage"){Require(SkillHealth.Balance256(t)==before,"Wire proc damage phase ran after life commitment");}
        };
        void Cleanup()
        {
            ClassCombat.Stop(p);PaladinZeal.HitRoll=hit;SourceBlocking.BlockRoll=block;PlayerItemProcWorld.Observed=observed;
            SorceressSpellMissiles.Cancel(p);t.Delete();item.Delete();p.Delete();account.Delete();
        }
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline,"Wire item proc Zeal timeout");var state=ClassCombat.For(p);
                if(!began&&state.PendingAbility==CombatAbility.Zeal)
                {
                    Require(!PlayerItemProcs.Initialized(p)&&visits.Count==0&&SkillHealth.Balance256(t)==before&&NecromancerEffects.ManaBalance256(p)==17,"Wire windup initialized procs,rolled damage or missed once-only payment");
                    began=true;Log.Information("{Label}: windup",label);
                }
                if(visits.Count<2){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(began&&network.Running&&p.NetState==network&&visits.Select(v=>v.Phase).SequenceEqual(new[]{"domeleedamage","domeleeattack"})
                    &&visits.All(v=>v.Outcomes.Single() is{CallbackResult:1,CastAttempts:1,CastResults:1})&&emitted.Count==2
                    &&Seed(p)==expectedActor&&NpcWorldAllocation.Capture().Seed==expectedWorld&&NecromancerEffects.ManaBalance256(p)==17
                    &&state.Zeal!=null&&state.Zeal.Remaining==1&&state.PendingTarget==t.Serial&&NativeMonsterReturn.Session(t).Packets==1,"Authenticated input lost exact proc streams,ordinary Zeal series,target or mana");
                ClassCombat.Stop(p);int life=SkillHealth.Balance256(t);TickFlights(p,first);
                Require(SkillHealth.Balance256(t)<life&&t.Source==p.Serial.Value,"Authenticated equipment proc did not cause native player-attributed damage");
                Cleanup();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.Zeal,(int)DiabloClass.Paladin,t.Serial.Value,t.X,t.Y,t.Z);Observe();});
    }

}
