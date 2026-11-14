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

public static class BloodBoilWorldChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(BloodBoilWorldChecks));
    private sealed record Pet(uint Serial,string Birth,int Life,NpcEquipmentSeed Random);
    private sealed record Saved(int Generation,uint Owner,Pet[] Pets);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"blood-boil-world-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static string Birth(SkeletonGuardian b)=>JsonSerializer.Serialize(b switch
    {WarlockGoatmanBody g=>(object)g.Prepared.Snapshot,WarlockTaintedBody t=>t.Prepared.Snapshot,WarlockDefilerBody d=>d.Prepared.Snapshot,_=>throw new NotSupportedException()});
    private static Pet Save(SkeletonGuardian b)=>new(b.Serial.Value,Birth(b),SkillHealth.Balance256(b),NpcEquipmentRandom.State(b));
    private static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,p.ManaMax*256);}
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"blood-boil-world-reload-check":"blood-boil-world-check";
            ConsoleInputHandler.RegisterCommand(name,"Prepared Blood Boil life cost,area,overlap,clock and original saves",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>
            {try{if(reload){Reload(n);}else{Scene(n);}Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});}));}
    }
    private static void Numbers()
    {
        var row=SkillMechanics.For(CombatAbility.BloodBoil);
        foreach(var (rank,min,max)in new[]{(1,10,20),(8,66,90),(9,76,102),(16,146,186),(17,158,204),(22,218,294),(23,234,320),(28,314,450),(29,334,480),(40,554,810)})
        {
            var v=new SkillValues(row,key=>key=="Blood Boil"?rank:255,_=>0,99);var n=BloodBoilPrepared.Numbers(v);
            Require(n.PhysicalMinimum256==min*256&&n.PhysicalMaximum256==max*256&&n.FireMinimum256==min*256&&n.FireMaximum256==max*256,"Independent damage-tier endpoints or soft synergy leaked");
        }
        for(int rank=1;rank<=255;rank++)
        {
            var n=BloodBoilPrepared.Numbers(new(row,_=>rank,_=>0,99));int linear=15-rank+1;
            Require(n.Mana256==(15+rank-1)*64&&n.Radius==(rank>=10?12:rank>=5?9:6)&&n.DelayFrames==3
                &&n.CostOne==Math.Max(linear,5)&&n.CostTwo==Math.Max(linear/2,5)&&n.CostThree==Math.Max(linear/3,5),"Mana/radius/count-division/five-percent floor");
        }
        var hard=new SkillValues(row,key=>key=="Blood Boil"?2:255,key=>key=="Engorge"?1:0,99);var phys=BloodBoilPrepared.Numbers(hard);
        Require(phys.PhysicalMinimum256==21*256&&phys.FireMinimum256==18*256,"Hard Engorge must affect physical only with pre-shift truncation");
        var fire=BloodBoilPrepared.Numbers(new(row,key=>key=="Blood Boil"?2:255,key=>key=="Blood Oath"?1:0,99));
        Require(fire.PhysicalMinimum256==18*256&&fire.FireMinimum256==5529,"Hard Oath must affect fire only with FP8 synergy precision");
        Log.Information("Prepared Blood Boil:10independent tier endpoints,255cost/radius contexts and separate hard physical/fire synergy-rounding controls pass");
    }
    private static GoatmanNpcContactChecks.Recipient Target(PlayerMobile p,int x)
    {
        var t=new GoatmanNpcContactChecks.Recipient{Frozen=true,CantWalk=true,NoKillAwards=true};t.SetHits(50000);t.Hits=t.HitsMax;
        t.MoveToWorld(new(p.X+x,p.Y,p.Z),p.Map);t.AIObject?.AITimer.Stop();t.SetResistance(ResistanceType.Physical,0);t.SetResistance(ResistanceType.Fire,0);return t;
    }
    private static void Scene(NetState network)
    {
        Numbers();var p=ClassSkillChecks.Player(DiabloClass.Warlock);p.Name="BloodBoilWorldFixture";p.AddItem(new Backpack());
        ClassSkillChecks.Train(p,"warlock.bloodboil",19);ClassSkillChecks.Train(p,"warlock.engorge",4);ClassSkillChecks.Train(p,"warlock.bloodoath",7);ClassSkillChecks.Train(p,"warlock.demonicmastery",10);
        foreach(var key in new[]{"warlock.summongoatman","warlock.summontainted","warlock.summondefiler"}){ClassSkillChecks.Train(p,key,6);}
        FixtureSpace.Corridor(p,6900,34,3300);var account=FixtureNetwork.Authenticate(network,p,"blood-boil-");SkeletonGuardian[] bodies=[];bool kept=false;
        var original=BloodBoilPrepared.SeedLow;int seeds=0;BloodBoilPrepared.SeedLow=()=>{seeds++;return 31337;};
        GoatmanNpcContactChecks.Recipient target=null,outside=null;
        try
        {
            Require(!BloodBoilPrepared.Approved&&!WarlockCombat.Implemented(CombatAbility.BloodBoil),"Unapproved Blood Boil enabled");
            Require(!BloodBoilPrepared.ReleasePrepared(p,TimedEffects.Now).Released&&seeds==0,"No-demon cast committed/RNG");
            bodies=new[]{CombatAbility.SummonGoatman,CombatAbility.SummonTainted,CombatAbility.SummonDefiler}
                .Select((ability,i)=>WarlockGoatmanWorld.Create(p,new(p.X+2+i*2,p.Y,p.Z),TimedEffects.Now,ability)).ToArray();foreach(var b in bodies){WarlockGoatmanWorld.Admit(b);}
            var births=bodies.Select(Birth).ToArray();var rng=bodies.Select(NpcEquipmentRandom.State).ToArray();
            foreach(var b in bodies){SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)*5/100);}
            Require(!BloodBoilPrepared.ReleasePrepared(p,TimedEffects.Now).Released&&BloodBoilCasting.BeginPrepared(p,Core.TickCount)==null&&seeds==0,"Five-percent-or-less demon spent life/mana/RNG");
            foreach(var b in bodies){SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b));}NecromancerEffects.SetMana256(p,1);
            Require(!BloodBoilPrepared.ReleasePrepared(p,TimedEffects.Now).Released&&seeds==0,"Low-mana cast committed");Ready(p);
            PoisonDaggerChecks.Named(p,"Tarnhelm");ClassGear.Refresh(p);var numbers=BloodBoilPrepared.Numbers(SkillMechanics.Values(p,CombatAbility.BloodBoil));
            Require(numbers is{Rank:20,Mana256:2176,PhysicalMinimum256:89344,PhysicalMaximum256:118784,FireMinimum256:119193,FireMaximum256:158515},"Actual Tarnhelm own rank/hard synergy capture");
            foreach(var b in bodies.Skip(1)){SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)*5/100);}
            var mixed=BloodBoilPrepared.ReleasePrepared(p,TimedEffects.Now);
            Require(mixed is{Released:true,DemonCount:3,Bursts.Length:1,Fault:null}&&mixed.Bursts[0].LifePaid256==(long)SkillHealth.Maximum256(bodies[0])*5/100,"Low-life bodies stopped counting in the shared three-demon divisor");
            foreach(var b in bodies){SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b));}Ready(p);
            Log.Information("Prepared Blood Boil:mixed three-body roster retains three-demon5percent divisor while only the healthy body erupts");
            int mana=NecromancerEffects.ManaBalance256(p);var before=bodies.Select(SkillHealth.Balance256).ToArray();var empty=BloodBoilPrepared.ReleasePrepared(p,TimedEffects.Now);
            Require(empty is{Released:true,DemonCount:3,Bursts.Length:3,Fault:null}&&empty.Bursts.All(b=>b.Recipients.Length==0)&&NecromancerEffects.ManaBalance256(p)==mana-2176,"Empty-area release did not pay once and erupt all demons");
            for(int i=0;i<3;i++){Require(before[i]-SkillHealth.Balance256(bodies[i])==(long)SkillHealth.Maximum256(bodies[i])*5/100&&NpcEquipmentRandom.State(bodies[i])==rng[i],"Raw life cost or owner/pet RNG coupling");}
            Ready(p);var cancelled=BloodBoilCasting.BeginPrepared(p,Core.TickCount);Require(cancelled!=null,"SC entry failed");ClassCombat.Stop(p);Require(cancelled.Cancelled,"Stop did not cancel Blood Boil");
            Ready(p);var moved=BloodBoilCasting.BeginPrepared(p,Core.TickCount);Require(moved!=null,"Movement cancellation setup");var point=p.Location;p.Location=new(p.X,p.Y+1,p.Z);p.Location=point;Require(moved.Cancelled,"Move-away-and-back revived Blood Boil");
            Ready(p);var lost=BloodBoilCasting.BeginPrepared(p,Core.TickCount);Require(lost!=null,"Late eligibility setup");foreach(var b in bodies){SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)*5/100);}
            mana=NecromancerEffects.ManaBalance256(p);Require(!ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt)&&lost.Released&&NecromancerEffects.ManaBalance256(p)==mana,"Late low life cost mana");
            foreach(var b in bodies){SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b));}Ready(p);target=Target(p,3);outside=Target(p,16);
            var at=TimedEffects.Now;int life=SkillHealth.Balance256(target),far=SkillHealth.Balance256(outside),selfEvents=0,enemyEvents=0;
            void Notice(SkillDamageContact c){if(bodies.Contains(c.Target)){selfEvents++;}if(c.Target==target){enemyEvents++;}}
            SkillDamageEvents.BeforeDirectDamage+=Notice;BloodBoilRelease cast;
            try{cast=BloodBoilPrepared.ReleasePrepared(p,at);}finally{SkillDamageEvents.BeforeDirectDamage-=Notice;}
            Require(cast.Fault==null&&cast.Bursts.Length==3&&cast.Bursts.Select(b=>b.Recipients.Length).SequenceEqual(new[]{1,0,0})&&enemyEvents==1&&selfEvents==0,"Three-frame shared overlap or self-cost attack event");
            Require(life-SkillHealth.Balance256(target)==cast.Bursts[0].Physical256+cast.Bursts[0].Fire256&&SkillHealth.Balance256(outside)==far,"Typed damage or captured circle membership");
            ulong stream=SourceRandom.InitLow(31337);foreach(var burst in cast.Bursts)
            {int phys=SorceressSpellMissiles.Roll(numbers.PhysicalMinimum256,numbers.PhysicalMaximum256,stream,out var next);stream=next;int heat=SorceressSpellMissiles.Roll(numbers.FireMinimum256,numbers.FireMaximum256,stream,out next);stream=next;Require(burst.Physical256==phys&&burst.Fire256==heat,"Per-cast source roll order");}
            Require(cast.InitialSeed==SourceRandom.InitLow(31337)&&cast.FinalSeed==stream,"Transient caster stream provenance");
            life=SkillHealth.Balance256(target);Require(!BloodBoilPrepared.Ready(p,target.Serial.Value,at.AddMilliseconds(119)),"Early lease boundary");
            var blocked=BloodBoilPrepared.ReleasePrepared(p,at.AddMilliseconds(119));Require(blocked.Released&&blocked.Bursts.All(b=>b.Recipients.Length==0)&&SkillHealth.Balance256(target)==life,"Early new cast bypassed three-frame delay");
            var nextCast=BloodBoilPrepared.ReleasePrepared(p,at.AddMilliseconds(120));Require(nextCast.Bursts.Sum(b=>b.Recipients.Length)==1&&SkillHealth.Balance256(target)<life,"Exclusive three-frame release boundary");
            target.SetResistance(ResistanceType.Physical,100);target.SetResistance(ResistanceType.Fire,100);life=SkillHealth.Balance256(target);
            var immune=BloodBoilPrepared.ReleasePrepared(p,at.AddMilliseconds(240));Require(immune.Bursts.Sum(b=>b.Recipients.Length)==1&&SkillHealth.Balance256(target)==life,"Immune area contact did not retain delay without damage");
            Ready(p);var timed=BloodBoilCasting.BeginPrepared(p,Core.TickCount);Require(timed!=null,"Timed release setup");mana=NecromancerEffects.ManaBalance256(p);
            Require(!ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt-1)&&!timed.Released&&NecromancerEffects.ManaBalance256(p)==mana,"SC windup spent resources");
            Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt)&&timed.Result is{Released:true,ManaPaid256:2176,Fault:null},"SC release failed");
            Require(!ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt+1)&&NecromancerEffects.ManaBalance256(p)==mana-2176,"Released action replayed");
            for(int i=0;i<3;i++){Require(Birth(bodies[i])==births[i]&&NpcEquipmentRandom.State(bodies[i])==rng[i],"Spell rewrote pet birth/RNG");SkillHealth.SetBalance256(bodies[i],SkillHealth.Maximum256(bodies[i])-513);}
            PersistentMinions.Suspend(p);File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,bodies.Select(Save).ToArray())));kept=true;
            Log.Information("Prepared Blood Boil:actual three-demon FP8 costs,empty-area payment,typed packet/rolls,shared120ms overlap/immune delay,SC cancellation/late loss/no replay and real Tarnhelm hard synergies pass");
        }
        finally{BloodBoilPrepared.SeedLow=original;target?.Delete();outside?.Delete();ClassCombat.Stop(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();if(!kept){foreach(var b in bodies){b.Delete();}p.Delete();}}
        Lethal(network);
    }
    private static void Lethal(NetState network)
    {
        var p=new PlayerMobile{Body=0x191,Female=true,Name="BloodBoilWitch",Player=true};ClassCreation.Profiles.Assign(p,DiabloClass.Warlock);ClassCreation.Profiles.AdvanceLevel(p,99);p.AddItem(new Backpack());
        ClassSkillChecks.Train(p,"warlock.summongoatman",6);ClassSkillChecks.Train(p,"warlock.demonicmastery",10);ClassSkillChecks.Train(p,"warlock.bloodboil");
        FixtureSpace.Corridor(p,7000,26,3500);var account=FixtureNetwork.Authenticate(network,p,"blood-boil-witch-");var body=WarlockGoatmanWorld.Create(p,new(p.X+2,p.Y,p.Z),TimedEffects.Now,CombatAbility.SummonGoatman);WarlockGoatmanWorld.Admit(body);var target=Target(p,3);
        try
        {
            Require(SkillMechanics.Rank(p,CombatAbility.Engorge)==0,"Engorge-free caster control");Ready(p);
            ClassSkillChecks.Train(p,"warlock.summontainted");var second=WarlockGoatmanWorld.Create(p,new(p.X+5,p.Y,p.Z),TimedEffects.Now,CombatAbility.SummonTainted);WarlockGoatmanWorld.Admit(second);
            var at=TimedEffects.Now;int one=SkillHealth.Balance256(body),two=SkillHealth.Balance256(second);
            try
            {
                var pair=BloodBoilPrepared.ReleasePrepared(p,at);
                Require(pair is{Released:true,DemonCount:2,Bursts.Length:2,ManaPaid256:960,Fault:null}&&one-SkillHealth.Balance256(body)==(long)SkillHealth.Maximum256(body)*7/100
                    &&two-SkillHealth.Balance256(second)==(long)SkillHealth.Maximum256(second)*7/100,"Two-demon rank1 cost did not truncate15/2to7percent for each body");
            }
            finally{second.Delete();}
            Log.Information("Prepared Blood Boil:actual rank1two-demon cast spends3.75mana once and7percent maximum life on each original body");
            Ready(p);int before=SkillHealth.Maximum256(body)/10;SkillHealth.SetBalance256(body,before);int life=SkillHealth.Balance256(target);
            var released=BloodBoilPrepared.ReleasePrepared(p,at.AddMilliseconds(120));
            Require(released is{Released:true,DemonCount:1,ManaPaid256:960,Bursts.Length:1,Fault:null}&&released.Bursts[0].LifePaid256==before
                &&(body.Deleted||!body.Alive)&&body.LastKiller==body&&SkillHealth.Balance256(target)<life,"Lethal15percent cost lost its captured explosion or native death cleanup");
            Log.Information("Prepared Blood Boil:rank1Witch with no Engorge spends3.75mana;one demon at10percent dies to15percent maximum-life resource cost and its captured explosion still hits");
        }
        finally{body.Delete();target.Delete();ClassCombat.Stop(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();p.Delete();}
    }
    private static void Reload(NetState network)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Generation<2,"Too many Blood Boil reloads");var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        var bodies=saved.Pets.Select(x=>(SkeletonGuardian)World.FindMobile((Serial)x.Serial)).ToArray();
        for(int i=0;i<3;i++){var b=bodies[i];Require(b.Map==Map.Internal&&Birth(b)==saved.Pets[i].Birth&&SkillHealth.Balance256(b)==saved.Pets[i].Life
            &&NpcEquipmentRandom.State(b)==saved.Pets[i].Random&&DemonNativeState.Controller(b)is{Fault:null},"Original births/wounds/RNG/controllers changed or healed offline");}
        var account=FixtureNetwork.Authenticate(network,p,"blood-boil-reload-");GoatmanNpcContactChecks.Recipient target=null;
        try
        {
            PersistentMinions.Resume(p);WarlockGoatmanWorld.Tick(TimedEffects.Now);Ready(p);target=Target(p,3);int life=SkillHealth.Balance256(target);var result=BloodBoilPrepared.ReleasePrepared(p,TimedEffects.Now);
            Require(result is{Released:true,Bursts.Length:3,Fault:null}&&result.Bursts.Sum(b=>b.Recipients.Length)==1&&SkillHealth.Balance256(target)<life,"Restored original roster failed to explode again");
            PersistentMinions.Suspend(p);File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1,Pets=bodies.Select(Save).ToArray()}));
            Log.Information("Prepared Blood Boil:original three demon births,fractional wounds,RNG/hosts survive reload without offline recovery or replay,then release another costed explosion");
        }
        finally{target?.Delete();ClassCombat.Stop(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
    }
}
