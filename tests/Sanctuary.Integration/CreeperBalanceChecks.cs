// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class CreeperBalanceChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(CreeperBalanceChecks));
    private sealed record Saved(uint Owner,uint Body,uint Helm,int Mana,int Life,CreeperBodyState BodyState,CreeperGround[] Grounds,uint Target,int TargetLife,TimedEffectState Poison,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"creeper-balance-fixtures.json");
    private static string Json(object value)=>JsonSerializer.Serialize(value);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("creeper-balance-check",Check);Register("creeper-balance-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Approved Creeper base curve and mixed saved damage profiles",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static CreeperPoisonContext Context(int hard=0)=>new(99,hard){Profile=DruidCreeperPoison.SnapshotProfile};
    private static void Curve()
    {
        // Independent goldens include every source tier boundary and the
        // explicit approved anchor. Raw D2R getters remain separately tested.
        foreach(var (rank,min,max) in new[]{(1,203,304),(8,1622,1724),(9,1825,1952),(16,3244,3549),(17,3650,4005),(20,4867,5373),
            (21,5272,5830),(22,5678,6286),(23,6489,7148),(28,10544,11457),(29,11558,12521),(255,240691,253110)})
        {
            Require(DruidCreeperPoison.Damage(rank,Context(),100)==new CreeperPoison(min,max,100),"Approved curve/tier golden "+rank);
            var twenty=DruidCreeperPoison.Damage(rank,Context(20),100);
            Require(twenty==new CreeperPoison(min*3,max*3,100),"Rabies sole hard synergy or duration changed");
        }
        var anchor=DruidCreeperPoison.Damage(20,Context(),100);
        Require((anchor.Minimum256+anchor.Maximum256)*100L==2000L*512,"Rank20 average must be exactly2000before synergy");
        Require(DruidCreeperPoison.Damage(20,Context(1),100)==new CreeperPoison(5353,5910,100)
            &&DruidCreeperPoison.Damage(20,Context(1),33)==new CreeperPoison(1766,1950,100)
            &&DruidCreeperPoison.Damage(20,Context(20),0)==new CreeperPoison(0,0,100),"Base normalization must precede synergy truncation and slow");
        Require(DruidCreeperPoison.Damage(20,new(99,0),100)==new CreeperPoison(768,848,100),"Historical source context silently upgraded");
        var absent=JsonSerializer.Deserialize<CreeperPoisonContext>("{\"OwnerLevel\":99,\"RabiesHardRank\":0}");
        Require(absent.Profile==DruidCreeperPoison.SourceProfile&&DruidCreeperPoison.Damage(20,absent,100)==new CreeperPoison(768,848,100),"Missing-profile JSON changed historical rates");
    }
    private static PlayerMobile Owner()
    {
        var p=new RecyclerFixturePlayer{Body=0x190,Player=true,Name="CreeperBalanceFixture"};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(p,99);ClassSkillChecks.Train(p,"druid.poisoncreeper",20);
        FixtureSpace.Corridor(p,6700,24,1900);NecromancerEffects.SetMana256(p,p.ManaMax*256-97);return p;
    }
    private static GraveyardZombie Enemy(PlayerMobile p,int resistance=0)
    {
        var t=new GraveyardZombie{CantWalk=true,Frozen=true,NoKillAwards=true};t.AIObject?.AITimer.Stop();t.SetHits(10000);t.Hits=t.HitsMax;
        t.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);t.SetResistance(ResistanceType.Poison,resistance);TimedEffects.PreventHealing(t,TimedEffects.Now);return t;
    }
    private static TimedEffectState Poison(Mobile target)=>TimedEffects.Snapshot().Single(s=>s.Owner==target.Serial.Value);
    private static void Hold(Mobile target)
    {var state=Poison(target);TimedEffects.Load(TimedEffects.Snapshot().Where(s=>s.Owner!=target.Serial.Value).Append(state with{UpdatedUtc=TimedEffects.Now.Add(FixtureSpace.Retention)}).ToArray());}
    private static CreeperMat Mat(PlayerMobile p,int rank,CreeperPoisonContext context)
        =>DruidCreeperPoison.Initialize(new(1,rank,p.X*2+6,p.Y*2,100+rank*25,100+rank*25){Z=p.Z},context,100);
    private static void Delivery(PlayerMobile p)
    {
        var at=TimedEffects.Now.AddDays(2);var mat=Mat(p,20,Context());
        foreach(int resist in new[]{0,50,100,-100})
        {
            var t=Enemy(p,resist);try
            {
                int before=SkillHealth.Balance256(t);var delivered=DruidCreeperPoison.Deliver(p,t,mat,at);
                Require(delivered.Poison.Seed!=mat.Poison.Seed,"Fresh balanced contact failed to consume its source roll");
                if(resist==100){Require(!t.Poisoned&&SkillHealth.Balance256(t)==before,"Immune recipient received balanced poison");continue;}
                var state=Poison(t);int rate=state.PoisonClock.Rate256;
                Require(state.PoisonMs==4000&&state.PoisonSource==p.Serial.Value&&SkillHealth.Balance256(t)==before,"Balance changed poison duration/source or added immediate damage");
                int raw=WeaponMissileDamage.RollValue(mat.Poison.Damage.Minimum256,mat.Poison.Damage.Maximum256,0,n=>
                {ulong seed=mat.Poison.Seed;return(int)SourceRandom.Limited(ref seed,n);});
                Require(rate==(resist==0?raw:resist==50?raw/2:raw*2),"Balanced poison resistance order");
                TimedEffects.Settle(t,at.AddSeconds(4));Require(before-SkillHealth.Balance256(t)==rate*100&&!t.Poisoned,"Actual balanced four-second total/expiry");
            }
            finally{t.Delete();}
        }
        var cured=Enemy(p);try
        {DruidCreeperPoison.Deliver(p,cured,mat,at);TimedEffects.Settle(cured,at.AddMilliseconds(40));TimedEffects.Clear(cured);int life=SkillHealth.Balance256(cured);TimedEffects.Settle(cured,at.AddSeconds(4));Require(!cured.Poisoned&&SkillHealth.Balance256(cured)==life,"Cured balanced poison continued ticking");}
        finally{cured.Delete();}
    }
    private static void Check()
    {
        Curve();var p=Owner();Delivery(p);var at=TimedEffects.Now.AddDays(2);
        var first=DruidCreeperBody.CreatePrepared(p,new(p.X+2,p.Y,p.Z),at);var captured=DruidCreeperPoison.Capture(first.Prepared.Snapshot);
        Require(captured==Context()&&first.Prepared.Snapshot.Birth.Rank==20,"Fresh actor failed approved profile/effective rank");
        ClassSkillChecks.Train(p,"druid.rabies");var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.Name=="Tarnhelm").Id,99,new Random(173),true);
        Require(p.EquipItem(helm),"Real soft-rank gear");ClassGear.Refresh(p);
        Require(DruidCreeperPoison.Capture(first.Prepared.Snapshot)==captured,"Existing birth Rabies context reread training/gear");first.Delete();
        var body=DruidCreeperBody.CreatePrepared(p,new(p.X+2,p.Y,p.Z),at);var context=DruidCreeperPoison.Capture(body.Prepared.Snapshot);
        Require(body.Prepared.Snapshot.Birth.Rank==21&&SkillMechanics.Rank(p,CombatAbility.Rabies)==2&&context.RabiesHardRank==1
            &&DruidCreeperPoison.Damage(21,context,100)==new CreeperPoison(5799,6413,100),"Real soft Creeper rank/hard-only Rabies context");
        long id=DruidCreeperGround.Create(body,21,DruidCreeperVines.Heads(21,(p.X+6)*2,p.Y*2),at);
        for(int i=0;i<12;i++){DruidCreeperGround.Tick(at.AddMilliseconds((i+1)*40),true,p.Serial.Value);}
        var fresh=DruidCreeperGround.Get(id);Require(fresh.PoisonContext==context&&fresh.Fields.Mats.Length>0&&fresh.Fields.Mats.All(m=>m.Poison.Damage==new CreeperPoison(5799,6413,100)),"Real head/trail creation bypassed approved packet capture");
        var legacyContext=new CreeperPoisonContext(99,0);var legacyMat=Mat(p,20,legacyContext);long legacyId=DruidCreeperGround.Snapshot().NextId+1;
        var old=new CreeperGround(legacyId,body.Serial.Value,p.Serial.Value,p.Map.MapID,20,new([], [legacyMat],1),fresh.NextUtc){PoisonContext=legacyContext};
        var pool=DruidCreeperGround.Snapshot();DruidCreeperGround.Restore(pool with{Flights=[..pool.Flights,old],NextId=legacyId});
        string before=Json(DruidCreeperGround.Snapshot());bool refused=false;
        try{DruidCreeperGround.Restore(DruidCreeperGround.Snapshot()with{Flights=[fresh with{PoisonContext=context with{Profile="unknown"}}]});}catch(InvalidDataException){refused=true;}
        Require(refused&&Json(DruidCreeperGround.Snapshot())==before,"Unknown balance profile mutated mixed pool");
        SkillHealth.SetBalance256(body,1111);SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)/2-17);var target=Enemy(p);
        DruidCreeperPoison.Deliver(p,target,Mat(p,20,Context()),TimedEffects.Now);Hold(target);
        var s=new Saved(p.Serial.Value,body.Serial.Value,helm.Serial.Value,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(p),body.Prepared,[DruidCreeperGround.Get(id),DruidCreeperGround.Get(legacyId)],target.Serial.Value,SkillHealth.Balance256(target),Poison(target),0);
        p.Internalize();File.WriteAllText(Manifest,Json(s));
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var body=(DruidCreeperBody)World.FindMobile((Serial)s.Body);var target=World.FindMobile((Serial)s.Target);
        Require(body.ControlMaster==p&&Json(body.Prepared)==Json(s.BodyState)&&SkillHealth.Balance256(body)==1111&&World.FindItem((Serial)s.Helm)?.Parent==p
            &&SkillHealth.Balance256(p)==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillHealth.Balance256(target)==s.TargetLife&&Json(Poison(target))==Json(s.Poison),"Original balance actor/gear/wounds/mana/poison changed on reload");
        foreach(var ground in s.Grounds){Require(Json(DruidCreeperGround.Get(ground.Id))==Json(ground),"Original mixed-profile field changed on reload");}
        var at=s.Grounds.Min(g=>g.NextUtc);DruidCreeperGround.Tick(at,false,p.Serial.Value);
        var next=s.Grounds.Select(g=>DruidCreeperGround.Get(g.Id)).ToArray();
        for(int i=0;i<next.Length;i++)
        {var old=s.Grounds[i];var current=next[i];Require(current.PoisonContext==old.PoisonContext&&current.Fields.Mats.Length==old.Fields.Mats.Length&&current.Fields.Mats.Zip(old.Fields.Mats).All(pair=>pair.First.Remaining==pair.Second.Remaining-1&&pair.First.Poison==pair.Second.Poison),"Inactive original field failed to age or recaptured profile/rate/seed");}
        var now=TimedEffects.Now;TimedEffects.Load(TimedEffects.Snapshot().Where(x=>x.Owner!=target.Serial.Value).Append(s.Poison with{UpdatedUtc=now}).ToArray());TimedEffects.Settle(target,now.AddMilliseconds(40));
        Require(s.TargetLife-SkillHealth.Balance256(target)==s.Poison.PoisonClock.Rate256,"Resumed actual balanced poison changed its rate");Hold(target);
        File.WriteAllText(Manifest,Json(s with{Grounds=next,Poison=Poison(target),TargetLife=SkillHealth.Balance256(target),Generation=s.Generation+1}));
    }
}
