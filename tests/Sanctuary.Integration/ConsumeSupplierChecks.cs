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

public static class ConsumeSupplierChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ConsumeSupplierChecks));
    private sealed record Pet(uint Serial,string Birth,int Life,NpcEquipmentSeed Random);
    private sealed record Saved(int Generation,uint Owner,ConsumeSuppliers Frozen,ConsumeSuppliers Current,Pet[] Pets);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"consume-suppliers-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static string Birth(SkeletonGuardian b)=>JsonSerializer.Serialize(b switch
    {WarlockGoatmanBody g=>(object)g.Prepared.Snapshot,WarlockTaintedBody t=>t.Prepared.Snapshot,WarlockDefilerBody d=>d.Prepared.Snapshot,_=>throw new NotSupportedException()});
    private static ConsumeSuppliers Numbers(int rank,int hard,string monster,MonsterDifficulty difficulty)
        =>ConsumePrepared.Capture(new(SkillMechanics.For(CombatAbility.Consume),_=>rank,_=>0,99),hard,monster,difficulty);
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"consume-suppliers-reload-check":"consume-suppliers-check";
            ConsoleInputHandler.RegisterCommand(name,"Consume authenticated monpet suppliers,real gear and original saves",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>
            {try{if(reload){Reload(n);}else{Scene(n);}Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});}));}
    }
    private static void Values()
    {
        foreach(var (rank,mana,duration,life,speed)in new[]{(1,25,1000,5,15),(5,29,3000,9,28),(10,34,5500,14,34),
            (20,44,10500,24,40),(60,84,30500,64,46),(255,279,128000,259,46)})
        {
            var s=Numbers(rank,0,"wargoatman",MonsterDifficulty.Normal);
            Require(s.Mana256==mana*256&&s.DurationFrames==duration&&s.LifePercent==life&&s.VelocityPercent==speed,"Independent common supplier values");ConsumePrepared.Validate(s);
        }
        foreach(var (difficulty,first,twentieth)in new[]{(MonsterDifficulty.Normal,21,40),(MonsterDifficulty.Nightmare,23,80),(MonsterDifficulty.Hell,25,120)})
        {Require(Numbers(1,0,"wargoatman",difficulty).Bonus==new ConsumeBonusValues(DamagePercent:first,CrushingChance:5),"Goatman difficulty source mapping");
            Require(Numbers(20,20,"wargoatman",difficulty).Bonus==new ConsumeBonusValues(DamagePercent:twentieth,CrushingChance:25),"Goatman effective Consume/hard Death Mark scaling");}
        foreach(var (rank,fire,pierce,magic,magicPierce)in new[]{(1,10,5,10,5),(6,15,10,15,10),(11,20,15,20,10),(40,20,15,20,10)})
        {Require(Numbers(rank,20,"warbighead",MonsterDifficulty.Hell).Bonus==new ConsumeBonusValues(FireMastery:fire,FirePierce:pierce),"Tainted caps/typed stat writers");
            Require(Numbers(rank,20,"warputriddefiler",MonsterDifficulty.Hell).Bonus==new ConsumeBonusValues(MagicMastery:magic,MagicPierce:magicPierce),"Defiler caps/typed stat writers");}
        var one=Numbers(1,0,"wargoatman",MonsterDifficulty.Normal);
        foreach(var invalid in new[]{one with{Rank=0},one with{HardDeathMark=21},one with{Monster="unknown"},one with{Difficulty=(MonsterDifficulty)3},
            one with{LifePercent=6},one with{Bonus=new ConsumeBonusValues(FireMastery:21)}})
        {bool rejected=false;try{ConsumePrepared.Validate(invalid);}catch(InvalidDataException){rejected=true;}Require(rejected,"Malformed captured context silently accepted");}
        Log.Information("Consume suppliers:20literal common/difficulty/typed-bonus/cap contexts and6malformed captures pass;source alias scoped to supplied caster,not global formula grammar");
    }
    private static void Scene(NetState network)
    {
        Values();var p=ClassSkillChecks.Player(DiabloClass.Warlock);p.Female=true;p.Name="ConsumeSupplierWitch";p.AddItem(new Backpack());
        ClassSkillChecks.Train(p,"warlock.consume",19);ClassSkillChecks.Train(p,"warlock.deathmark",4);ClassSkillChecks.Train(p,"warlock.demonicmastery",10);
        foreach(var key in new[]{"warlock.summongoatman","warlock.summontainted","warlock.summondefiler"}){ClassSkillChecks.Train(p,key,6);}
        FixtureSpace.Corridor(p,6900,34,3300);var account=FixtureNetwork.Authenticate(network,p,"consume-suppliers-");SkeletonGuardian[] bodies=[];bool kept=false;
        try
        {
            Require(WarlockCombat.Implemented(CombatAbility.Consume)&&WarlockCombat.Handles(CombatAbility.Consume)&&ConsumeEffects.Approved,"Approved learned Consume unavailable");
            bodies=new[]{CombatAbility.SummonGoatman,CombatAbility.SummonTainted,CombatAbility.SummonDefiler}
                .Select((a,i)=>WarlockGoatmanWorld.Create(p,new(p.X+2+i*2,p.Y,p.Z),TimedEffects.Now,a)).ToArray();foreach(var b in bodies){WarlockGoatmanWorld.Admit(b);}
            var births=bodies.Select(Birth).ToArray();int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(p),maximum=SkillHealth.Maximum256(p);
            var frozen=ConsumePrepared.Capture(p,"wargoatman",MonsterDifficulty.Normal);
            Require(frozen is{Rank:19,HardDeathMark:4,Mana256:11008,DurationFrames:10000,LifePercent:23,VelocityPercent:39}
                &&frozen.Bonus==new ConsumeBonusValues(DamagePercent:39,CrushingChance:9),"Actual rank19 numeric capture");
            Require(NecromancerEffects.ManaBalance256(p)==mana&&SkillHealth.Balance256(p)==life&&SkillHealth.Maximum256(p)==maximum,"Capture spent mana/healed/installed owner buff");
            var helm=PoisonDaggerChecks.Named(p,"Tarnhelm");ClassGear.Refresh(p);var geared=ConsumePrepared.Capture(p,"wargoatman",MonsterDifficulty.Hell);
            Require(geared is{Rank:20,HardDeathMark:4,Mana256:11264,DurationFrames:10500,LifePercent:24,VelocityPercent:40}
                &&geared.Bonus==new ConsumeBonusValues(DamagePercent:120,CrushingChance:9)&&SkillMechanics.Rank(p,CombatAbility.DeathMark)==5,"Soft Death Mark leaked into hard crushing chance or wrong difficulty");
            p.AddToBackpack(helm);ClassGear.Refresh(p);Require(ConsumePrepared.Capture(p,"wargoatman",MonsterDifficulty.Normal)==frozen&&geared.Rank==20,"Gear removal rewrote captured payload");
            p.EquipItem(helm);ClassGear.Refresh(p);ClassSkillChecks.Train(p,"warlock.deathmark",5);var current=ConsumePrepared.Capture(p,"wargoatman",MonsterDifficulty.Normal);
            Require(current is{Rank:20,HardDeathMark:5}&&current.Bonus==new ConsumeBonusValues(DamagePercent:40,CrushingChance:10)&&frozen.Bonus.CrushingChance==9,"Hard training did not affect new capture only");
            Require(ConsumePrepared.Capture(p,"warbighead",MonsterDifficulty.Hell).Bonus==new ConsumeBonusValues(FireMastery:20,FirePierce:15)
                &&ConsumePrepared.Capture(p,"warputriddefiler",MonsterDifficulty.Hell).Bonus==new ConsumeBonusValues(MagicMastery:20,MagicPierce:10),"Actual three-species dispatcher");
            for(int i=0;i<3;i++){Require(Birth(bodies[i])==births[i]&&EngorgeEffects.Saved(bodies[i])==null,"Numeric capture changed original demon/buff");SkillHealth.SetBalance256(bodies[i],SkillHealth.Maximum256(bodies[i])-513);}
            PersistentMinions.Suspend(p);File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,frozen,current,bodies.Select(b=>new Pet(b.Serial.Value,Birth(b),SkillHealth.Balance256(b),NpcEquipmentRandom.State(b))).ToArray())));kept=true;
            Log.Information("Consume suppliers:actual Witch/Tarnhelm19to20,soft Death Mark4to5doesnotraiseCB9,hard4to5raisesnewCB10;original3species births unchanged;no consume/payment/buff/slot activation");
        }
        finally{network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();if(!kept){foreach(var b in bodies){b.Delete();}p.Delete();}}
    }
    private static void Reload(NetState network)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Generation<2,"Too many Consume numeric reloads");ConsumePrepared.Validate(saved.Frozen);ConsumePrepared.Validate(saved.Current);
        var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);foreach(var pet in saved.Pets)
        {var b=(SkeletonGuardian)World.FindMobile((Serial)pet.Serial);Require(b.Map==Map.Internal&&Birth(b)==pet.Birth&&SkillHealth.Balance256(b)==pet.Life
            &&NpcEquipmentRandom.State(b)==pet.Random&&DemonNativeState.Controller(b)is{Fault:null},"Original parked demon changed or recovered on reload");}
        var account=FixtureNetwork.Authenticate(network,p,"consume-supplier-reload-");
        try
        {
            Require(p.Female&&ConsumePrepared.Capture(p,"wargoatman",MonsterDifficulty.Normal)==saved.Current
                &&saved.Frozen is{Rank:19,HardDeathMark:4}&&saved.Current is{Rank:20,HardDeathMark:5},"Actual saved gear/hard points or frozen fixture capture changed");
            File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
            Log.Information("Consume suppliers:original Witch/gear and3parked demon births,wounds,RNG/hosts survive reload;frozen numerical fixture payload is not a production Consume buff");
        }
        finally{network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
    }
}
