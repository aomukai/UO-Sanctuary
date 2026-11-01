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
namespace Sanctuary.Integration;

public static class MageStaticFieldChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(MageStaticFieldChecks));
    private sealed record Saved(uint Owner,uint Mage,uint Target,int Life,int TargetLife,ulong Seed,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"mage-static-fixtures.json");
    public static void Configure()
    {
        Register("mage-static-check",Check);Register("mage-static-reload-check",Reload);
        ConsoleInputHandler.RegisterCommand("mage-static-live","Natural learned Mage fixed5 Static proc",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("mage-static-live-result",Live)));
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Shared Static Field and approved Mage secondary action",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner(int rank=20)=>MagePetBuffChecks.Owner(rank,7000,3300);
    private static GraveyardSkeleton Target(PlayerMobile p,MageWorldBody b,int dx=4,int resistance=0)
    {var t=PoisonDaggerChecks.Target(p);t.MoveToWorld(new(b.X+dx,b.Y,b.Z),p.Map);t.SetHits(1000);t.Hits=t.HitsMax;t.SetResistance(ResistanceType.Energy,resistance);TimedEffects.PreventHealing(t,TimedEffects.Now);return t;}
    private static ulong Seed(int minimum,int maximum,bool ai=true)
    {
        for(ulong n=1;n<100000;n++){ulong s=n;if(ai&&(SourceRandom.Roll(ref s)%100<15||SourceRandom.Roll(ref s)%100>=80)){continue;}int p=(int)(SourceRandom.Roll(ref s)%100);if(p>=minimum&&p<=maximum){return n;}}
        throw new InvalidOperationException("No Static proc seed");
    }
    private static bool Bolt(MageWorldBody b)=>NecromancerMageMissiles.Snapshot().Bolts.Any(x=>x.Actor==b.Serial.Value);
    private static void Numeric()
    {
        foreach(int life in new[]{0,1,255,256,257,511,512,1023,1024,12817,256017})
        {int whole=life/256;Require(StaticFields.Raw256(life,25,0)==(whole<1?0:Math.Min(whole-1,whole/4)*256),"Static source whole-HP preparation");}
        foreach(int floor in new[]{0,33,50})foreach(int life in new[]{256,257,12817,84480,128000,128257,256000})foreach(int damage in new[]{0,1,256,12800,int.MaxValue})
        {int minimum=Math.Max(256,1000*floor/100*256);Require(StaticFields.Limit256(life,256000,floor,damage)==Math.Min(damage,Math.Max(0,life-minimum)),"Static fractional difficulty/nonlethal floor");}
        int contexts=0;
        foreach(int element in new[]{0,1,2,3})foreach(int hard in new[]{0,4,5,9,10,19,20})for(int mask=0;mask<16;mask++)
        {
            int first=0,second=0,draws=0;
            bool Ready(CombatAbility a)=>(mask&(a switch{CombatAbility.Meteor=>1,CombatAbility.GlacialSpike=>2,CombatAbility.ChainLightning=>4,CombatAbility.StaticField=>8,_=>0}))!=0;
            var a=hard<10?CombatAbility.None:element switch{1=>CombatAbility.GlacialSpike,2=>CombatAbility.Meteor,3=>CombatAbility.ChainLightning,_=>CombatAbility.None};
            var b=element==3&&hard>=20?CombatAbility.StaticField:CombatAbility.None;
            bool canA=a!=CombatAbility.None&&Ready(a),canB=b!=CombatAbility.None&&Ready(b);
            for(int roll=0;roll<100;roll++)
            {var choice=MageOffensiveSkills.Select(element,hard,Ready,()=>{draws++;return roll;});Require(choice==CombatAbility.None||choice==a&&canA||choice==b&&canB,"Mage selected unavailable secondary");if(choice!=CombatAbility.None){if(choice==a){first++;}else{second++;}}contexts++;}
            Require(first==(canA?5:0)&&second==(canB?5:0)&&draws==(canA||canB?100:0),"Mage five-percent slots/one draw/disabled no-roll");
        }
        Require(contexts==44800,"Mage proc policy inventory");
    }
    private static void Casts()
    {
        var p=Owner(19);var mage=MagePetBuffChecks.Pet(p,3);var t=Target(p,mage);var outside=Target(p,mage,5);var immune=Target(p,mage,2,100);
        var old=SummonDifficulties.Facets();
        try
        {
            var helm=PoisonDaggerChecks.Named(p,"Wormskull");Require(SkillMechanics.Rank(p,CombatAbility.RaiseSkeletalMage)==20&&MagePetBuffs.HardRank(mage)==19,"Static soft fixture");
            ulong seed=Seed(5,9,false);NecromancerMageMissiles.SetActorSeed(mage,seed);int hp=SkillHealth.Balance256(t);
            Require(mage.Launch(p,t,TimedEffects.Now)&&Bolt(mage)&&NecromancerMageMissiles.ActorSeed(mage).Seed==seed&&SkillHealth.Balance256(t)==hp,"Soft19+1 unlocked proc or consumed an actor draw");
            var bolts=NecromancerMageMissiles.Snapshot();NecromancerMageMissiles.Restore(bolts with{Bolts=bolts.Bolts.Where(x=>x.Actor!=mage.Serial.Value).ToArray()});
            p.Backpack.DropItem(helm);ClassGear.Refresh(p);MagePetBuffChecks.Train(p,"necromancer.skeletalmage",20);int mana=NecromancerEffects.ManaBalance256(p),npcMana=mage.Mana;
            NecromancerMageMissiles.SetActorSeed(mage,seed);ulong expected=seed;SourceRandom.Roll(ref expected);
            int distant=SkillHealth.Balance256(outside),resistant=SkillHealth.Balance256(immune);
            Require(!mage.Launch(p,p,TimedEffects.Now)&&NecromancerMageMissiles.ActorSeed(mage).Seed==seed,"Invalid target consumed proc RNG");
            Require(mage.Launch(p,t,TimedEffects.Now)&&!Bolt(mage)&&SkillHealth.Balance256(t)==hp-hp/4&&SkillHealth.Balance256(outside)==distant
                &&SkillHealth.Balance256(immune)==resistant&&NecromancerMageMissiles.ActorSeed(mage).Seed==expected
                &&NecromancerEffects.ManaBalance256(p)==mana&&mage.Mana==npcMana,"Fixed5 caster-radius/no missile/immune/no mana/one actor draw Static");
            foreach(var difficulty in new[]{MonsterDifficulty.Normal,MonsterDifficulty.Nightmare,MonsterDifficulty.Hell})
            {
                SummonDifficulties.SetFacets([new(p.Map.MapID,difficulty)]);int floor=StaticFields.FloorPercent(difficulty);
                SkillHealth.SetBalance256(t,256000);for(int i=0;i<60;i++){Require(StaticFields.Cast(mage,p,MagePetBuffs.Values(CombatAbility.StaticField,5,mage.Level)),"Static field direct cast");}
                Require(SkillHealth.Balance256(t)==(floor==0?3*256:1000*floor/100*256)&&t.Alive,"Facet difficulty cap or nonlethal Static");
            }
            SummonDifficulties.SetFacets(old);t.SetResistance(ResistanceType.Energy,50);SkillHealth.SetBalance256(t,128017);StaticFields.Cast(mage,p,MagePetBuffs.Values(CombatAbility.StaticField,5,mage.Level));
            Require(SkillHealth.Balance256(t)==128017-125*256/2,"Static source rounding/fractional mitigation");
            // Real player entry shares the same receiver, retaining once-only mana.
            var sorc=ClassSkillChecks.Player(DiabloClass.Sorceress);sorc.MoveToWorld(new(mage.X,mage.Y+1,mage.Z),p.Map);sorc.Mana=sorc.ManaMax;t.SetResistance(ResistanceType.Energy,0);SkillHealth.SetBalance256(t,256000);
            try{int cost=SkillMechanics.Values(sorc,CombatAbility.StaticField).Mana256,before=NecromancerEffects.ManaBalance256(sorc);t.MoveToWorld(new(sorc.X+1,sorc.Y,sorc.Z),p.Map);
                Require(SorceressCombat.Release(sorc,CombatAbility.StaticField,Serial.Zero,sorc.Location)&&NecromancerEffects.ManaBalance256(sorc)==before-cost&&SkillHealth.Balance256(t)==192000,"Shared real Sorceress Static release/once payment/mastery exclusion");}
            finally{sorc.Delete();}
        }
        finally{SummonDifficulties.SetFacets(old);t.Delete();outside.Delete();immune.Delete();mage.Delete();p.Delete();}
    }
    private static void Check()
    {
        Numeric();Casts();var p=Owner();var mage=MagePetBuffChecks.Pet(p,3);var t=Target(p,mage);NecromancerMageMissiles.SetActorSeed(mage,Seed(5,9));
        MageWorldChecks.Through(mage,34);Require(mage.WorldState.Controller is{Mode:4,ActionAt:35}&&!Bolt(mage)&&SkillHealth.Balance256(t)==256000,"Pending Static cast fired early");
        SkillHealth.SetBalance256(mage,SkillHealth.Maximum256(mage)-257);TimedEffects.PreventHealing(mage,TimedEffects.Now);mage.Ground.Store(mage.WorldState.Controller with{NextUtc=TimedEffects.Now.AddMinutes(3)});
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,mage.Serial.Value,t.Serial.Value,SkillHealth.Balance256(mage),SkillHealth.Balance256(t),NecromancerMageMissiles.ActorSeed(mage).Seed,0)));
        Log.Information("Mage Static:44800 one-draw/exclusive five-percent policy contexts;actual hard19+soft1 refusal/hard20 release,fixed5 caster radius,immune/nonlethal/facet caps,fractional HP,no NPC mana/mastery,real player once payment and saved pre-proc windup");
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var mage=(MageWorldBody)World.FindMobile((Serial)s.Mage);var t=(GraveyardSkeleton)World.FindMobile((Serial)s.Target);
        Require(mage.ControlMaster==p&&mage.WorldState.Controller is{Mode:1,ActionAt:0}&&SkillHealth.Balance256(mage)==s.Life&&SkillHealth.Balance256(t)==s.TargetLife
            &&NecromancerMageMissiles.ActorSeed(mage).Seed==s.Seed&&!Bolt(mage),"Saved canceled Static changed wounds/seed/HP or replayed");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
    }
    private static void Live(NetState network)
    {
        var p=Owner();FixtureNetwork.Authenticate(network,p,"mage-static");var mage=MagePetBuffChecks.Pet(p,3,future:false);var t=Target(p,mage);NecromancerMageMissiles.SetActorSeed(mage,Seed(5,9));int mana=NecromancerEffects.ManaBalance256(p);long deadline=Core.TickCount+7000;
        void End(){t.Delete();mage.Delete();p.Delete();}
        void Wait()
        {
            try
            {
                Require(Core.TickCount<deadline&&mage.WorldState.Controller.Fault==null,"Natural Static timeout/fault");
                if(mage.WorldState.Controller.Frame<35){Require(SkillHealth.Balance256(t)==256000&&!Bolt(mage),"Static before action35");Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);return;}
                Require(SkillHealth.Balance256(t)==192000&&!Bolt(mage)&&mage.WorldState.Controller is{Attempts:1,Hits:1}
                    &&NecromancerEffects.ManaBalance256(p)==mana&&network.Running,"Natural Static did not replace exactly one basic missile at its action");End();Log.Information("mage-static-live-result: passed");
            }
            catch(Exception e){End();Log.Error(e,"mage-static-live-result: failed");}
        }
        Log.Information("mage-static-live: ready");Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);
    }
}
