// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;
public static class MageElementChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(MageElementChecks));
    private static readonly FieldInfo Element=typeof(NecromancerMinion).GetField("_element",BindingFlags.Instance|BindingFlags.NonPublic);
    private sealed record Pet(uint Serial,string Type,int OriginalElement,int ExpectedElement,int Life,int Maximum,int Level,int Rank,int Mastery,int MissileRank,int Armor,int[] Resistances,long Order,MageCriticalSeed Seed,SkeletonWorldState World,uint Owner);
    private sealed record Saved(uint Owner,Point3D Origin,int Map,Pet[] Pets,uint Target,int TargetLife,MageMissileState PoisonFlight,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"mage-elements-fixtures.json");
    public static void Configure()
    {
        Register("mage-elements-check",Check);Register("mage-elements-reload-check",Reload);
        ConsoleInputHandler.RegisterCommand("mage-elements-live","In-place poison-to-ice during a natural Mage windup",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("mage-elements-live-result",Live)));
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Three Mage elements and identity-preserving poison migration",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()=>MagePetBuffChecks.Owner(20,6500,3000);
    private static int[] Resistances(Mobile m)=>new[]{m.PhysicalResistance,m.FireResistance,m.ColdResistance,m.PoisonResistance,m.EnergyResistance};
    private static GraveyardZombie Target(PlayerMobile p,Mobile caster,int dx=4)
    {var t=new GraveyardZombie{Name="PoisonDaggerFixtureMageElements",NoKillAwards=true,Frozen=true,CantWalk=true};t.MoveToWorld(new(caster.X+dx,caster.Y,caster.Z),p.Map);t.AIObject.AITimer.Stop();t.SetHits(20000);t.Hits=t.HitsMax;foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}TimedEffects.PreventHealing(t,TimedEffects.Now);return t;}
    private static void RawElement(NecromancerMinion mage,int element)=>Element.SetValue(mage,element);
    private static void Clear(NecromancerMinion mage)
    {var all=NecromancerMageMissiles.Snapshot();NecromancerMageMissiles.Restore(all with{Bolts=all.Bolts.Where(x=>x.Actor!=mage.Serial.Value).ToArray()});}
    private static MageMissileState Flight(long id)=>NecromancerMageMissiles.Snapshot().Bolts.SingleOrDefault(x=>x.Id==id);
    private static void Replace(MageMissileState state)
    {var all=NecromancerMageMissiles.Snapshot();NecromancerMageMissiles.Restore(all with{NextId=Math.Max(all.NextId,state.Id),Bolts=all.Bolts.Where(x=>x.Id!=state.Id).Append(state).ToArray()});}
    private static void Step(long id){if(Flight(id) is{} f){NecromancerMageMissiles.Tick(f.NextUtc,true,id);}}
    private static Pet Capture(NecromancerMinion mage,int original)=>new(mage.Serial.Value,mage.GetType().FullName,original,original==0?1:original,SkillHealth.Balance256(mage),SkillHealth.Maximum256(mage),mage.Level,mage.Rank,mage.Mastery,mage.MageMissileRank,mage.CombatProfile.Defense,Resistances(mage),BattleCommand.PetOrder(mage),NecromancerMageMissiles.ActorSeed(mage),(mage as MageWorldBody)?.WorldState,mage.ControlMaster.Serial.Value);
    private static void Unchanged(PlayerMobile p,NecromancerMinion mage,Pet s,bool loaded)
    {
        Require(mage.GetType().FullName==s.Type&&mage.Serial.Value==s.Serial&&mage.ControlMaster==p&&mage.Level==s.Level&&mage.Rank==s.Rank&&mage.Mastery==s.Mastery&&mage.MageMissileRank==s.MissileRank&&mage.CombatProfile.Defense==s.Armor&&Resistances(mage).SequenceEqual(s.Resistances)&&BattleCommand.PetOrder(mage)==s.Order&&NecromancerMageMissiles.ActorSeed(mage)==s.Seed&&SkillHealth.Balance256(mage)==s.Life&&SkillHealth.Maximum256(mage)==s.Maximum
            &&JsonSerializer.Serialize((mage as MageWorldBody)?.WorldState)==JsonSerializer.Serialize(s.World),$"Mage migration identity/resources/state changed loaded={loaded},serial={s.Serial},life={SkillHealth.Balance256(mage)}/{s.Life},max={SkillHealth.Maximum256(mage)}/{s.Maximum}");
    }
    private static void Direct()
    {
        for(int i=0;i<3;i++){Require(NecromancerSummons.MageElementFromSlot(i)==i+1,"Three-element slot mapping");}
        foreach(int i in new[]{-1,3,255}){bool invalid=false;try{NecromancerSummons.MageElementFromSlot(i);}catch(ArgumentOutOfRangeException){invalid=true;}Require(invalid,"Invalid Mage sampler slot accepted");}
        var defaultRoll=NecromancerSummons.ElementRoll;for(int i=0;i<4096;i++){Require(defaultRoll() is >=1 and <=3,"Default sampler produced poison/unknown element");}
        var p=Owner();var mage=MagePetBuffChecks.Pet(p,1);var target=Target(p,mage);var golem=new NecromancerMinion();
        try
        {
            golem.Prepare(p,CombatAbility.ClayGolem);RawElement(golem,0);int life=SkillHealth.Balance256(golem);Require(!golem.MigrateMageElement()&&golem.MageElement==0&&SkillHealth.Balance256(golem)==life,"Non-Mage element or life changed");
            SkillHealth.SetBalance256(mage,SkillHealth.Maximum256(mage)/3+17);RawElement(mage,0);var before=Capture(mage,0);long id=NecromancerMageMissiles.LaunchLegacy(mage,target,TimedEffects.Now);var poison=Flight(id);NecromancerMageMissiles.Impact(mage,target,poison);var packet=JsonSerializer.Serialize(Flight(id));var dot=JsonSerializer.Serialize(TimedEffects.TryActor(target,out var state)?state.PoisonClock:null);before=Capture(mage,0);
            Require(SorceressEffects.Armor(mage)==null&&mage.MigrateMageElement()&&mage.MageElement==1&&SorceressEffects.Armor(mage)==SorceressEffects.PrepareColdArmor(CombatAbility.FrozenArmor,MagePetBuffs.Values(CombatAbility.FrozenArmor,20,mage.Level),DateTime.MaxValue),"Poison did not become intrinsic-armored ice at hard20");Unchanged(p,mage,before,false);Require(JsonSerializer.Serialize(Flight(id))==packet&&JsonSerializer.Serialize(state.PoisonClock)==dot&&!mage.MigrateMageElement(),"Migration rewrote existing poison packet/DOT or was not idempotent");Clear(mage);TimedEffects.Clear(target);
            foreach(int element in new[]{1,2,3}){RawElement(mage,element);before=Capture(mage,element);Require(!mage.MigrateMageElement()&&mage.MageElement==element,"Supported Mage rerolled");Unchanged(p,mage,before,false);}
            RawElement(mage,9);bool unsupported=false;try{mage.MigrateMageElement();}catch(NotSupportedException){unsupported=true;}Require(unsupported&&mage.MageElement==9,"Unknown saved element silently invented");RawElement(mage,1);
        }
        finally{golem.Delete();Clear(mage);target.Delete();mage.Delete();p.Delete();}
        // Actual learned births cover every supported element and the legacy
        // zero alias,without retaining a production poison creation path.
        p=Owner();try{foreach(int element in new[]{0,1,2,3}){mage=MagePetBuffChecks.Pet(p,element);Require(mage.MageElement==(element==0?1:element),"Learned birth retained poison/changed supported identity");mage.Delete();}}finally{p.Delete();}
        Log.Information("Mage elements:3slot/4096default samples,learned three-element births/zero alias,live migration/idempotence/unknown refusal,no wound/rank/owner/order/seed/clock changes,existing poison packet/DOT and non-Mage preservation pass");
    }
    private static void Check()
    {
        Direct();var owners=new[]{Owner(),Owner()};foreach(var owner in owners){MagePetBuffChecks.Train(owner,"necromancer.skeletonmastery",3);}var pets=new System.Collections.Generic.List<Pet>();NecromancerMinion poisonCaster=null;
        for(int type=0;type<2;type++)for(int element=0;element<4;element++)
        {
            var p=owners[type];
            NecromancerMinion mage;
            if(type==1){mage=MagePetBuffChecks.Pet(p,element==0?1:element,3,element-1);mage.SuspendActions();((MageWorldBody)mage).Ground.Store(((MageWorldBody)mage).WorldState.Controller with{NextUtc=TimedEffects.Now.AddHours(1)});}
            else
            {
                mage=new NecromancerMinion{Frozen=true,CantWalk=true};var roll=NecromancerSummons.ElementRoll;try{NecromancerSummons.ElementRoll=()=>element==0?1:element;mage.Prepare(p,CombatAbility.RaiseSkeletalMage);}finally{NecromancerSummons.ElementRoll=roll;}
                Require(mage.SetControlMaster(p),"Legacy Mage owner");mage.MoveToWorld(new(p.X+2,p.Y+element-1,p.Z),p.Map);mage.AIObject.AITimer.Stop();PersistentMinions.Profiles.Track(mage);BattleCommand.Track(mage);NecromancerMageMissiles.SetActorSeed(mage,(ulong)(100+element));
            }
            RawElement(mage,element);int maximum=SkillHealth.Maximum256(mage);int balance=element==0?maximum:element==1?maximum-1:element==2?maximum/2+17:maximum-257;SkillHealth.SetBalance256(mage,balance);TimedEffects.PreventHealing(mage,TimedEffects.Now);
            if(type==1&&element==0){poisonCaster=mage;}
            pets.Add(Capture(mage,element));
        }
        foreach(var owner in owners){Require(NecromancerSkeletons.For(owner,CombatAbility.RaiseSkeletalMage).Length==4&&NecromancerArmyLimits.For(owner,CombatAbility.RaiseSkeletalMage)>=4,"Saved mixed Mage roster exceeded its approved owner-local budget");}var primary=owners[1];var target=Target(primary,poisonCaster,8);long id=NecromancerMageMissiles.LaunchLegacy(poisonCaster,target,TimedEffects.Now);for(int i=0;i<3;i++){Step(id);}var f=Flight(id) with{NextUtc=TimedEffects.Now.AddHours(1)};Replace(f);
        Require(f.Packet.Missile=="necromage1"&&f.Packet.Element=="pois"&&f.Frame==3,"Saved legacy poison flight fixture");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(primary.Serial.Value,primary.Location,primary.Map.MapID,pets.ToArray(),target.Serial.Value,SkillHealth.Balance256(target),f,0)));
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var target=World.FindMobile((Serial)s.Target);
        foreach(var group in s.Pets.GroupBy(x=>x.Owner))
        {var owner=(PlayerMobile)World.FindMobile((Serial)group.Key);Require(group.Count()==4&&NecromancerSkeletons.For(owner,CombatAbility.RaiseSkeletalMage).Select(x=>x.Serial.Value).SequenceEqual(group.OrderBy(x=>x.Order).ThenBy(x=>x.Serial).Select(x=>x.Serial)),"Saved owner-local migration roster/order changed");}
        foreach(var row in s.Pets)
        {
            var mage=(NecromancerMinion)World.FindMobile((Serial)row.Serial);Require(mage.MageElement==row.ExpectedElement&&!mage.MigrateMageElement(),"Saved poison conversion/idempotence failed");Unchanged((PlayerMobile)World.FindMobile((Serial)row.Owner),mage,row,true);
            Require((row.ExpectedElement==1)==(SorceressEffects.Armor(mage)!=null),"Converted Ice armor unlock or other element changed");
        }
        Require(Flight(s.PoisonFlight.Id)==s.PoisonFlight&&SkillHealth.Balance256(target)==s.TargetLife,"Migration changed held poison packet or target");
        var caster=(MageWorldBody)World.FindMobile((Serial)s.PoisonFlight.Actor);p.MoveToWorld(s.Origin,Map.Maps[s.Map]);caster.MoveToWorld(new(s.Origin.X+3,s.Origin.Y-1,s.Origin.Z),p.Map);var pool=NecromancerMageMissiles.Snapshot();var copy=s.PoisonFlight with{Id=pool.NextId+1,NextUtc=TimedEffects.Now};Replace(copy);for(int i=0;i<30&&Flight(copy.Id)!=null;i++){Step(copy.Id);}
        Require(SkillHealth.Balance256(target)<s.TargetLife&&TimedEffects.TryActor(target,out var state)&&state.PoisonClock is{Rate256:>0}&&Flight(s.PoisonFlight.Id)==s.PoisonFlight,"Old poison packet did not finish after caster became ice");
        long cold=NecromancerMageMissiles.Launch(caster,target,TimedEffects.Now);Require(Flight(cold).Packet is{Missile:"icebolt",Element:"cold"},"Converted Mage produced new poison");pool=NecromancerMageMissiles.Snapshot();NecromancerMageMissiles.Restore(pool with{Bolts=pool.Bolts.Where(x=>x.Id<cold&&x.Id!=copy.Id).ToArray()});
        TimedEffects.Clear(target);TimedEffects.PreventHealing(target,TimedEffects.Now);SkillHealth.SetBalance256(target,s.TargetLife);NecromancerMageMissiles.SetActorSeed(caster,s.Pets.Single(x=>x.Serial==caster.Serial.Value).Seed.Seed);caster.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
        Log.Information("Mage elements reload:8 original actors/two poison conversions,full/one-FP/fractional wounds,captured resources/order/seeds/clocks,held poison/copy resumption/new cold packet pass");
    }
    private static void Live(NetState network)
    {
        var p=MagePetBuffChecks.Owner(20,6500,3400);FixtureNetwork.Authenticate(network,p,"elements");var mage=MagePetBuffChecks.Pet(p,1,future:false);RawElement(mage,0);SkillHealth.SetBalance256(mage,SkillHealth.Maximum256(mage)/2+17);var target=Target(p,mage);NecromancerMageMissiles.SetActorSeed(mage,MageStaticFieldChecks.Seed(20,99));int mana=NecromancerEffects.ManaBalance256(p);bool migrated=false,launched=false;long deadline=Core.TickCount+7000;
        void End(){Clear(mage);target.Delete();mage.Delete();p.Delete();}
        void Wait()
        {
            try
            {
                Require(Core.TickCount<deadline&&mage.WorldState.Controller.Fault==null,"Natural element migration timeout/fault");
                var c=mage.WorldState.Controller;if(!migrated&&c is{Mode:4,Frame:>=25 and <35}){var before=Capture(mage,0);Require(mage.MigrateMageElement(),"Natural poison not migrated");Unchanged(p,mage,before,false);migrated=true;}
                if(!launched&&NecromancerMageMissiles.Snapshot().Bolts.FirstOrDefault(x=>x.Actor==mage.Serial.Value) is{} f){Require(NecromancerMageMissiles.Snapshot().Bolts.Count(x=>x.Actor==mage.Serial.Value)==3&&migrated&&mage.WorldState.Controller.Frame>=35&&f.Packet is{Spell:CombatAbility.IceBolt,Element:"cold",Missile:"icebolt"},"Migration reset/changed source windup or retained poison");launched=true;mage.SuspendActions();mage.Ground.Store(mage.WorldState.Controller with{NextUtc=TimedEffects.Now.AddHours(1)});}
                if(SkillHealth.Balance256(target)==20000*256){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Wait);return;}
                Require(migrated&&launched&&network.Running&&NecromancerEffects.ManaBalance256(p)==mana&&TimedEffects.TryActor(target,out var status)&&status.Cold?.Source==mage.Serial.Value,"Natural converted Mage cold impact/allegiance/mana");End();Log.Information("mage-elements-live-result: passed");
            }
            catch(Exception e){End();Log.Error(e,"mage-elements-live-result: failed");}
        }
        Log.Information("mage-elements-live: ready");Timer.StartTimer(TimeSpan.FromMilliseconds(10),Wait);
    }
}
