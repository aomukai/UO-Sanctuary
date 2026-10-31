// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary.Integration;

public static class ConfuseCastChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ConfuseCastChecks));
    private sealed record Saved(uint Owner,uint Target,uint Legacy,NecromancerCurse Curse,NecromancerCurse Old,int Mana,int Map,Point3D Point,int Generation,MindAiState Mind=null);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"confuse-cast-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("confuse-cast-check",Check);Register("confuse-cast-reload-check",Reload);
        foreach(bool quick in new[]{false,true})
        {bool captured=quick;string label=quick?"confuse-cast-quick":"confuse-cast-right";Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,captured)));}
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Confuse reference, learned casting and saved state",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static PlayerMobile Owner(int rank=1)
    {
        var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureConfuse";
        void Train(string id,int wanted)
        {
            TalentCatalog.TryGet(DiabloClass.Necromancer,id,out var talent);
            foreach(string prior in talent.Prerequisites){if(ClassCreation.Profiles.GetRank(p.Serial,prior)==0){Train(prior,1);}}
            while(ClassCreation.Profiles.GetRank(p.Serial,id)<wanted){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}
        }
        Train("necromancer.confuse",rank);
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.Confuse)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Confuse),"Terror right/quick bindings");
        FixtureSpace.Corridor(p,7050,24,3500);return p;
    }
    private static GraveyardSkeleton Enemy(PlayerMobile p,int x=1,int y=0)
    {var t=PoisonDaggerChecks.Target(p);t.MoveToWorld(new(p.X+x,p.Y+y,p.Z),p.Map);foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Energy,ResistanceType.Poison}){t.SetResistance(type,0);}return t;}
    private static void Ready(PlayerMobile p){PoisonDaggerChecks.Ready(p);ClassCombat.For(p).RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,p.ManaMax*256);}
    private static void Differential()
    {
        foreach(int rank in new[]{1,2,3,20,21,255})
        {
            var v=new SkillValues(SkillMechanics.For(CombatAbility.Confuse),_=>rank,_=>0,99);
            Require(v.Mana256==3328&&v.Eval("aurarangecalc")==6+rank-1&&v.Eval("auralencalc")==250+50*(rank-1),"Pinned Confuse cost/radius/duration");
        }
        Require(NecromancerCurseCasts.AiFrames(350,MonsterDifficulty.Nightmare)==175&&NecromancerCurseCasts.AiFrames(350,MonsterDifficulty.Hell)==87,"Confuse integer difficulty division");
    }
    private static ulong Seed(bool monsters)
    {
        for(int low=0;low<100;low++){ulong seed=SourceRandom.InitLow(low),copy=seed;if(((SourceRandom.Roll(ref copy)&1)!=0)==monsters){return seed;}}
        throw new InvalidOperationException("No deterministic Confuse selection seed");
    }
    private static void Select(GraveyardSkeleton t,bool monsters)
    {
        var state=NecromancerEffects.Profiles.Find(t);NecromancerEffects.Profiles.Store(t,state with{Mind=state.Mind with{Seed=Seed(monsters),Focus=0,Draws=0}});
        t.Frozen=t.CantWalk=false;t.NextReacquireTime=Core.TickCount;SourceMind.Think(t);
    }
    private static void Mechanics(PlayerMobile p,GraveyardSkeleton t)
    {
        Require(NecromancerEffects.Apply(p,t,CombatAbility.Confuse,3),"Initial Confuse3");var first=NecromancerEffects.Profiles.Find(t);
        Require(SourceMind.Neutral(t)&&t.GetMaster()==null&&!t.Controlled&&!t.Summoned&&p.CanBeHarmful(t,false),"Confuse was converted/friendly rather than neutral and attackable");
        Require(!PaladinConversion.MindBlastEligible(p,t)&&!PaladinConversion.ApplyMindBlast(p,t,150),"Mind Blast converted a neutral Confuse recipient");
        Require(!NecromancerEffects.Apply(p,t,CombatAbility.Confuse,4)&&NecromancerEffects.Profiles.Find(t)==first,"Confuse refreshed an already neutral recipient");
        Require(!NecromancerEffects.Apply(p,t,CombatAbility.DimVision,1)&&!NecromancerEffects.Apply(p,t,CombatAbility.Terror,1)&&!NecromancerEffects.Apply(p,t,CombatAbility.Attract,1),"AI casts ignored runtime neutral alignment");
        Require(NecromancerEffects.Apply(p,t,CombatAbility.Weaken,1)&&!SourceMind.Neutral(t)&&NecromancerEffects.Profiles.Find(t).Mind==first.Mind,"Curse replacement cleared independent override or failed alignment restoration");
        NecromancerEffects.Profiles.Clear(t);Require(NecromancerEffects.Apply(p,t,CombatAbility.Attract,1)&&!NecromancerEffects.Apply(p,t,CombatAbility.Confuse,20),"Confuse replaced protected Attract");NecromancerEffects.Profiles.Clear(t);
        var facets=SummonDifficulties.Facets();
        try
        {
            foreach(var d in Enum.GetValues<MonsterDifficulty>())
            {
                SummonDifficulties.SetFacets(facets.Where(x=>x.Map!=p.Map.MapID).Append(new(p.Map.MapID,d)).ToArray());
                var variant=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true};
                try
                {
                    MonsterVariants.Assign(variant,d==MonsterDifficulty.Hell?MonsterDifficulty.Normal:MonsterDifficulty.Hell);variant.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);variant.AIObject.AITimer.Stop();
                    var at=TimedEffects.Now;Require(NecromancerEffects.Apply(p,variant,CombatAbility.Confuse,3),"Facet Confuse cast");var state=NecromancerEffects.Profiles.Find(variant);
                    Require(Math.Abs((state.Curse.UntilUtc-at).TotalMilliseconds-NecromancerCurseCasts.AiFrames(350,d)*40)<100&&state.Mind.UntilUtc==state.Curse.UntilUtc,"Confuse facet/spawn/deadline contract");
                }
                finally{variant.Delete();}
            }
        }
        finally{SummonDifficulties.SetFacets(facets);}
    }
    private static void WorldTargets(PlayerMobile p)
    {
        var t=Enemy(p,6);var other=Enemy(p,8);
        try
        {
            Require(NecromancerEffects.Apply(p,t,CombatAbility.Confuse,1),"Selection Confuse");Select(t,true);var mind=NecromancerEffects.Profiles.Find(t).Mind;ulong expected=Seed(true);SourceRandom.Roll(ref expected);
            Require(t.Combatant==other&&mind.Focus==other.Serial.Value&&mind.Draws==1&&mind.Seed==expected&&SourceMind.Neutral(t)&&t.IsEnemy(p)&&other.IsEnemy(t)&&!t.IsFriend(p),
                $"Monster-faction selection/RNG/alignment restoration: mind={JsonSerializer.Serialize(mind)}, seed={expected}, combatant={t.Combatant?.Serial}, target={other.Serial}, neutral={SourceMind.Neutral(t)}, playerEnemy={t.IsEnemy(p)}, reverseEnemy={other.IsEnemy(t)}, friend={t.IsFriend(p)}, harmful={t.CanBeHarmful(other,false)}, source={PaladinConversion.SourceRule(other)?.Source}, gate={t.NextReacquireTime-Core.TickCount}");
            t.Delete();t=Enemy(p,6);Require(NecromancerEffects.Apply(p,t,CombatAbility.Confuse,1),"Player selection Confuse");Select(t,false);
            Require(t.Combatant==p&&NecromancerEffects.Profiles.Find(t).Mind.Draws==1&&SourceMind.Neutral(t),
                $"Player-faction selection was omitted: mind={JsonSerializer.Serialize(NecromancerEffects.Profiles.Find(t).Mind)}, combatant={t.Combatant?.Serial}, owner={p.Serial}, ownerAt={p.Location}, targetAt={t.Location}, visible={t.CanSee(p)}, los={t.InLOS(p)}, harmful={t.CanBeHarmful(p,false)}, neutral={SourceMind.Neutral(t)}, gate={t.NextReacquireTime-Core.TickCount}, mode={t.FightMode}");
            t.Delete();t=Enemy(p,6);Require(NecromancerEffects.Apply(p,t,CombatAbility.Confuse,1),"No-target selection Confuse");var origin=p.Location;p.MoveToWorld(new(p.X+100,p.Y,p.Z),p.Map);Select(t,false);p.MoveToWorld(origin,p.Map);
            Require(NecromancerEffects.Profiles.Find(t).Mind is{Mode:0,Draws:1}&&SourceMind.Neutral(t),"Empty selection failed to clear override independently of visible curse");
        }
        finally{t.Delete();other.Delete();}
    }
    private static void Casting(PlayerMobile p)
    {
        GraveyardSkeleton near=Enemy(p,8),edge=Enemy(p,11),far=Enemy(p,11,1);var center=new Point3D(p.X+8,p.Y,p.Z);
        try
        {
            Ready(p);int mana=NecromancerEffects.ManaBalance256(p);
            Require(NecromancerCombat.Release(p,CombatAbility.Confuse,Serial.Zero,center),"Ground area release");
            Require(NecromancerEffects.ManaBalance256(p)==mana-3328&&NecromancerEffects.Curse(near)!=null&&NecromancerEffects.Curse(edge)!=null&&NecromancerEffects.Curse(far)==null,"Area center,radius or once-only payment");
            foreach(var t in new[]{near,edge,far}){NecromancerEffects.Profiles.Clear(t);}
            Ready(p);mana=NecromancerEffects.ManaBalance256(p);
            Require(ClassCombat.Perform(p,CombatAbility.Confuse,near,near.Location,Core.TickCount),"Targeted windup");
            Require(NecromancerEffects.ManaBalance256(p)==mana&&NecromancerEffects.Curse(near)==null,"Curse applied/paid during windup");
            near.MoveToWorld(new(p.X-1,p.Y,p.Z),p.Map);
            Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt)&&NecromancerEffects.Curse(near)!=null&&NecromancerEffects.Curse(edge)==null,"Release did not follow selected target position");
            Ready(p);NecromancerEffects.Profiles.Clear(near);mana=NecromancerEffects.ManaBalance256(p);
            Require(ClassCombat.Perform(p,CombatAbility.Confuse,null,center,Core.TickCount),"Cancelled windup");long effect=ClassCombat.For(p).EffectAt;
            var origin=p.Location;p.MoveToWorld(new(p.X,p.Y+1,p.Z),p.Map);Require(!ClassCombat.ResolveAction(p,effect)&&NecromancerEffects.ManaBalance256(p)==mana,"Movement cancellation spent mana");p.MoveToWorld(origin,p.Map);
            Ready(p);NecromancerEffects.SetMana256(p,3327);Require(!NecromancerCombat.Begin(p,CombatAbility.Confuse,Serial.Zero,center,Core.TickCount),"Insufficient mana began a cast");
            near.Delete();edge.Delete();far.Delete();Ready(p);mana=NecromancerEffects.ManaBalance256(p);Require(NecromancerCombat.Release(p,CombatAbility.Confuse,Serial.Zero,new(p.X+16,p.Y,p.Z))&&NecromancerEffects.ManaBalance256(p)==mana-3328,"Empty area did not pay once");
        }
        finally{ClassCombat.Stop(p);near.Delete();edge.Delete();far.Delete();}
    }
    private static void LineOfSight()
    {
        var p=Owner(3);var t=Enemy(p,10);var center=new Point3D(p.X+8,p.Y,p.Z);var wall=new Static(0x80);
        try
        {
            wall.MoveToWorld(new(p.X+9,p.Y,p.Z),p.Map);
            Require(!p.Map.LineOfSight(new Point3D(center.X,center.Y,center.Z+12),new Point3D(t.X,t.Y,t.Z+12)),"Recipient LOS obstruction fixture");
            Ready(p);Require(NecromancerCombat.Release(p,CombatAbility.Confuse,Serial.Zero,center)&&NecromancerEffects.Curse(t)!=null,"Filter2 gained an unrequested recipient LOS gate");
        }
        finally{wall.Delete();t.Delete();p.Delete();}
    }
    private static void Check()
    {
        Differential();var p=Owner();var t=Enemy(p);Mechanics(p,t);Casting(p);LineOfSight();
        // The tracked-ground area cast can also cover this inspection actor.
        // Remove that unrelated neutral recipient before faction controls.
        NecromancerEffects.Profiles.Clear(t);WorldTargets(p);
        while(ClassCreation.Profiles.GetRank(p.Serial,"necromancer.confuse")<20){Require(ClassCreation.Profiles.TryTrain(p.Serial,"necromancer.confuse",out _),"Train Confuse20");}
        PoisonDaggerChecks.Named(p,"Wormskull");Require(SkillMechanics.Rank(p,CombatAbility.Confuse)==21,"Effective Confuse item rank");
        Require(NecromancerEffects.Apply(p,t,CombatAbility.Confuse,21),"Save learned Confuse");var at=TimedEffects.Now.AddDays(1);var state=NecromancerEffects.Profiles.Find(t);
        state=state with{Curse=state.Curse with{UntilUtc=at},Mind=state.Mind with{UntilUtc=at,Seed=Seed(true)}};NecromancerEffects.Profiles.Store(t,state);
        var legacy=Enemy(p,3);var old=new NecromancerCurse(CombatAbility.Confuse,p.Serial.Value,1,at,p.X,p.Y);NecromancerEffects.Profiles.Store(legacy,new(Curse:old));Ready(p);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,t.Serial.Value,legacy.Serial.Value,state.Curse,old,NecromancerEffects.ManaBalance256(p),p.Map.MapID,p.Location,0,state.Mind)));
        t.Internalize();legacy.Internalize();p.Internalize();
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var mind=s.Mind;var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var t=(BaseCreature)World.FindMobile((Serial)s.Target);var legacy=(BaseCreature)World.FindMobile((Serial)s.Legacy);
        Require(NecromancerEffects.Curse(t)==s.Curse&&NecromancerEffects.Profiles.Find(t).Mind==mind&&NecromancerEffects.Curse(legacy)==s.Old&&NecromancerEffects.Profiles.Find(legacy).Mind==null,"Saved Confuse curse/override/RNG/legacy changed");
        p.MoveToWorld(s.Point,Map.Maps[s.Map]);Require(SkillMechanics.Rank(p,CombatAbility.Confuse)==21&&SkillBindings.Instance.Get(p).Right==CombatAbility.Confuse,"Saved Confuse rank/binding changed");
        ulong seed=mind.Seed;SourceRandom.Roll(ref seed);var state=NecromancerEffects.Profiles.Find(t);NecromancerEffects.Profiles.Store(t,state with{Mind=mind with{Seed=seed}});
        t.Internalize();legacy.Internalize();p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Mind=NecromancerEffects.Profiles.Find(t).Mind,Generation=s.Generation+1}));
    }
    private static void Input(NetState network,bool quick)
    {
        var p=Owner();var t=Enemy(p,6);var victim=Enemy(p,7);Ready(p);int mana=NecromancerEffects.ManaBalance256(p);string label=quick?"confuse-cast-quick":"confuse-cast-right";
        // The inspection target has50000life, which also scales its custom
        // regeneration. Reuse the existing PMH fixture control to observe hits.
        TimedEffects.PreventHealing(victim,TimedEffects.Now);
        var account=FixtureNetwork.Authenticate(network,p,"confuse-cast-");
        bool began=false,finished=false;long deadline=Core.TickCount+25000;
        void Fail(Exception e){if(finished){return;}finished=true;t.Delete();victim.Delete();p.Delete();account.Delete();Log.Error(e,"{Label}-result: failed",label);}
        void Inspect()
        {
            if(finished){return;}
            try
            {
                Require(Core.TickCount<deadline,"Confuse input timeout");var session=ClassCombat.For(p);
                if(session.PendingAbility==CombatAbility.Confuse&&!began){Require(NecromancerEffects.Curse(t)==null&&NecromancerEffects.ManaBalance256(p)==mana,"Input paid/applied during windup");began=true;Log.Information("{Label}: windup",label);}
                if(NecromancerEffects.Curse(t) is not{} curse){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Inspect);return;}
                Require(began&&curse.Rank==1&&NecromancerEffects.ManaBalance256(p)==mana-3328,"Real input capture/payment");ClassCombat.Stop(p);
                Require((curse.UntilUtc-TimedEffects.Now).TotalMilliseconds is >9900 and <=10000,"Rank1Confuse duration is not250frames");
                int victimHp=victim.Hits;DateTime movementAt=TimedEffects.Now.AddMilliseconds(4000);bool escaped=false;
                var controlled=NecromancerEffects.Profiles.Find(t);NecromancerEffects.Profiles.Store(t,controlled with{Mind=controlled.Mind with{Seed=Seed(true)}});
                t.Frozen=t.CantWalk=false;t.AIObject.AITimer.Prod();
                void Escaping()
                {
                    if(finished){return;}
                    if(TimedEffects.Now<movementAt){Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Escaping));return;}
                    try{Require(victim.Hits<victimHp&&SourceMind.Neutral(t)&&p.CanBeHarmful(t,false)&&t.GetMaster()==null,
                        $"Real Confuse cast failed autonomous NPC combat/neutral attackability: hp={victim.Hits}/{victimHp}, mind={JsonSerializer.Serialize(NecromancerEffects.Profiles.Find(t)?.Mind)}, combatant={t.Combatant?.Serial}, victim={victim.Serial}, positions={t.Location}/{victim.Location}, action={t.AIObject.Action}, running={t.AIObject.AITimer.Running}, sector={t.Map.GetSector(t.Location).Active}, returning={t.ReturningHome}, enemy={t.IsEnemy(victim)}, harmful={t.CanBeHarmful(victim,false)}, attackChance={t.NormalStats.AttackChance}, frozen={t.Frozen}/{t.CantWalk}");escaped=true;}catch(Exception e){Fail(e);}
                }
                Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Escaping));
                void Expired()
                {
                    if(finished){return;}
                    if(TimedEffects.Now<curse.UntilUtc.AddMilliseconds(100)){Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Expired));return;}
                    try{Require(escaped&&NecromancerEffects.Curse(t)==null&&!SourceMind.Neutral(t)&&NecromancerEffects.Profiles.Find(t).Mind.Mode==0&&NecromancerEffects.Profiles.Find(t).Curse==null,"Natural rank1 Confuse did not retire its actual control state");finished=true;t.Delete();victim.Delete();p.Delete();account.Delete();Log.Information("{Label}-result: passed",label);}catch(Exception e){Fail(e);}
                }
                Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Expired));
            }
            catch(Exception e){Fail(e);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.Confuse,(int)DiabloClass.Necromancer,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
