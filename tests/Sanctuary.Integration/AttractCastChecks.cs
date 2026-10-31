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

public static class AttractCastChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(AttractCastChecks));
    private sealed record Saved(uint Owner,uint Selected,uint Neighbor,uint Legacy,NecromancerCurse Curse,NecromancerCurse Old,
        MindAiState Mind,int Map,Point3D Point,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"attract-cast-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("attract-cast-check",Check);Register("attract-cast-reload-check",()=>FixtureNetwork.Connected("attract-cast-reload-result",Reload));
        foreach(bool quick in new[]{false,true})
        {bool captured=quick;string label=quick?"attract-cast-quick":"attract-cast-right";Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,captured)));}
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Attract casting and independent target overrides",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static PlayerMobile Owner()
    {
        var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureAttract";
        void Train(string id)
        {
            TalentCatalog.TryGet(DiabloClass.Necromancer,id,out var talent);
            foreach(string prior in talent.Prerequisites){if(ClassCreation.Profiles.GetRank(p.Serial,prior)==0){Train(prior);}}
            if(ClassCreation.Profiles.GetRank(p.Serial,id)==0){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}
        }
        Train("necromancer.attract");Require(SkillBindings.Instance.Assign(p,false,CombatAbility.Attract)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Attract),"Attract bindings");
        FixtureSpace.Corridor(p,7100,24,3500);return p;
    }
    private static GraveyardSkeleton Enemy(PlayerMobile p,int x,int y=0)
    {
        var t=PoisonDaggerChecks.Target(p);t.MoveToWorld(new(p.X+x,p.Y+y,p.Z),p.Map);
        foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Energy,ResistanceType.Poison}){t.SetResistance(type,0);}return t;
    }
    private static void Ready(PlayerMobile p){PoisonDaggerChecks.Ready(p);ClassCombat.For(p).RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,p.ManaMax*256);}
    private static void Mechanics(PlayerMobile p)
    {
        foreach(int rank in new[]{1,2,3,20,21,255})
        {
            var v=new SkillValues(SkillMechanics.For(CombatAbility.Attract),_=>rank,_=>0,99);
            Require(v.Mana256==4352&&v.Eval("aurarangecalc")==9&&v.Eval("auralencalc")==300+90*(rank-1),"Pinned Attract cost/radius/duration");
        }
        var selected=Enemy(p,8);var neighbor=Enemy(p,9);var edge=Enemy(p,12);var outside=Enemy(p,12,3);var behind=Enemy(p,3);
        try
        {
            Require(NecromancerEffects.Apply(p,neighbor,CombatAbility.Weaken,1),"Neighbor curse fixture");var old=NecromancerEffects.Curse(neighbor);
            Require(NecromancerEffects.Apply(p,selected,CombatAbility.Attract,1),"Attract apply");var curse=NecromancerEffects.Curse(selected);var mind=NecromancerEffects.Profiles.Find(neighbor).Mind;
            Require(SourceMind.Neutral(selected)&&selected.GetMaster()==null&&selected.IsEnemy(p)&&p.CanBeHarmful(selected,false),"Selected target not neutral/attackable");
            Require(NecromancerEffects.Profiles.Find(selected).Mind==null&&mind is{Mode:2}&&mind.Focus==selected.Serial.Value&&mind.UntilUtc==curse.UntilUtc&&NecromancerEffects.Curse(neighbor)==old,"Selected self-exclusion or independent neighbor lease/curse");
            Require(NecromancerEffects.Profiles.Find(edge)?.Mind?.Mode==2&&NecromancerEffects.Profiles.Find(outside)?.Mind==null&&NecromancerEffects.Profiles.Find(behind)?.Mind==null,"Target-centered source radius boundary");
            outside.MoveToWorld(new(selected.X,selected.Y+1,selected.Z),p.Map);outside.Frozen=outside.CantWalk=false;
            NecromancerEffects.Think(outside);
            Require(NecromancerEffects.Profiles.Find(outside)?.Mind==null&&outside.Combatant!=selected,"Later arrival gained an uncast override or continuous-pull target");
            outside.Frozen=outside.CantWalk=true;
            Require(!NecromancerEffects.Apply(p,selected,CombatAbility.Attract,20)&&!NecromancerEffects.Apply(p,selected,CombatAbility.Weaken,20)&&!PaladinConversion.MindBlastEligible(p,selected),"Protected neutral target accepted replacement/conversion");
            neighbor.Frozen=neighbor.CantWalk=false;neighbor.NextReacquireTime=Core.TickCount+100000;SourceMind.Think(neighbor);
            Require(neighbor.Combatant==selected&&neighbor.IsEnemy(selected)&&!neighbor.IsFriend(selected)&&NecromancerEffects.Profiles.Find(neighbor).Mind==mind,"Explicit override used scan delay/RNG or failed combat");
            neighbor.Frozen=neighbor.CantWalk=true;neighbor.AIObject.AITimer.Stop();
            Require(NecromancerEffects.Apply(p,neighbor,CombatAbility.LifeTap,1)&&NecromancerEffects.Profiles.Find(neighbor).Mind==mind,"Neighbor curse replacement cleared override");
            var state=NecromancerEffects.Profiles.Find(selected);NecromancerEffects.Profiles.Store(selected,state with{Curse=null});
            Require(SourceMind.AttractedTarget(neighbor)==selected&&!SourceMind.Neutral(selected)&&neighbor.IsEnemy(selected),"Independent override ended with selected curse");
            selected.MoveToWorld(new(p.X+50,p.Y,p.Z),p.Map);Require(SourceMind.AttractedTarget(neighbor)==selected,"Explicit target gained generic scan-range gate");
            selected.Delete();Require(SourceMind.AttractedTarget(neighbor)==null&&NecromancerEffects.Profiles.Find(neighbor).Mind is{Mode:0,Focus:0},"Deleted target did not clear generic override");
            var next=Enemy(p,9);try
            {
                var kept=NecromancerEffects.Profiles.Find(neighbor).Mind;
                Require(NecromancerEffects.Apply(p,next,CombatAbility.Attract,2),"Second target Attract");var updated=NecromancerEffects.Profiles.Find(neighbor).Mind;
                Require(updated is{Mode:2}&&updated.Focus==next.Serial.Value&&updated.Seed==kept.Seed&&updated.Draws==kept.Draws&&updated.UntilUtc>mind.UntilUtc,"Latest target/deadline did not replace generic slot while retaining RNG");
            }
            finally{next.Delete();}
        }
        finally{foreach(var t in new[]{selected,neighbor,edge,outside,behind}){t.Delete();}}
        var facets=SummonDifficulties.Facets();
        try
        {
            foreach(var d in Enum.GetValues<MonsterDifficulty>())
            {
                SummonDifficulties.SetFacets(facets.Where(x=>x.Map!=p.Map.MapID).Append(new(p.Map.MapID,d)).ToArray());
                var t=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true};var n=Enemy(p,9);
                try
                {
                    MonsterVariants.Assign(t,d==MonsterDifficulty.Hell?MonsterDifficulty.Normal:MonsterDifficulty.Hell);t.MoveToWorld(new(p.X+8,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();var at=TimedEffects.Now;
                    Require(NecromancerEffects.Apply(p,t,CombatAbility.Attract,3),"Facet Attract");int frames=NecromancerCurseCasts.AiFrames(480,d);
                    Require(Math.Abs((NecromancerEffects.Curse(t).UntilUtc-at).TotalMilliseconds-frames*40)<100&&NecromancerEffects.Profiles.Find(n).Mind.UntilUtc==NecromancerEffects.Curse(t).UntilUtc,"Attract owner facet division/spawn/neighbor deadline");
                }
                finally{t.Delete();n.Delete();}
            }
        }
        finally{SummonDifficulties.SetFacets(facets);}
    }
    private static void SpecialAi(PlayerMobile p)
    {
        var selected=Enemy(p,8);var blind=Enemy(p,9);var fear=Enemy(p,10);
        try
        {
            Require(NecromancerEffects.Apply(p,blind,CombatAbility.DimVision,1)&&NecromancerEffects.Apply(p,fear,CombatAbility.Terror,1),"Special-state neighbor fixtures");
            Require(NecromancerEffects.Apply(p,selected,CombatAbility.Attract,1),"Attract special neighbors");
            blind.Frozen=blind.CantWalk=false;SourceBlind.Think(blind,NecromancerEffects.Curse(blind));
            Require(NecromancerEffects.Profiles.Find(blind).BlindAi.Focus==selected.Serial.Value&&NecromancerEffects.Curse(blind).Ability==CombatAbility.DimVision,"Blind acquisition ignored generic target or lost curse");
            fear.Frozen=fear.CantWalk=false;SourceTerror.Think(fear,NecromancerEffects.Curse(fear));
            Require(SourceTerror.State(fear).Focus==selected.Serial.Value&&SourceTerror.State(fear).Started&&NecromancerEffects.Profiles.Find(fear).Mind.Mode==0,"First fear escape did not use then clear generic override");
        }
        finally{selected.Delete();blind.Delete();fear.Delete();}
    }
    private static void Casting(PlayerMobile p)
    {
        var t=Enemy(p,8);var n=Enemy(p,9);
        try
        {
            Ready(p);int mana=NecromancerEffects.ManaBalance256(p);Require(!NecromancerCombat.Begin(p,CombatAbility.Attract,Serial.Zero,t.Location,Core.TickCount),"Ground-only cast accepted");
            Require(ClassCombat.Perform(p,CombatAbility.Attract,t,t.Location,Core.TickCount)&&NecromancerEffects.Curse(t)==null&&NecromancerEffects.ManaBalance256(p)==mana,"Windup applied/paid early");
            t.MoveToWorld(new(p.X-1,p.Y,p.Z),p.Map);
            Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt)&&NecromancerEffects.Curse(t)!=null&&NecromancerEffects.Profiles.Find(n)?.Mind==null&&NecromancerEffects.ManaBalance256(p)==mana-4352,"Moving selected center/once payment");
            Ready(p);NecromancerEffects.Profiles.Clear(t);mana=NecromancerEffects.ManaBalance256(p);
            Require(ClassCombat.Perform(p,CombatAbility.Attract,t,t.Location,Core.TickCount),"Cancel windup");long effect=ClassCombat.For(p).EffectAt;var origin=p.Location;
            p.MoveToWorld(new(p.X,p.Y+1,p.Z),p.Map);Require(!ClassCombat.ResolveAction(p,effect)&&NecromancerEffects.ManaBalance256(p)==mana,"Movement cancel paid mana");p.MoveToWorld(origin,p.Map);
            Ready(p);NecromancerEffects.SetMana256(p,4351);Require(!NecromancerCombat.Begin(p,CombatAbility.Attract,t.Serial,t.Location,Core.TickCount),"Insufficient mana began cast");
        }
        finally{ClassCombat.Stop(p);t.Delete();n.Delete();}
    }
    private static void Check()
    {
        var p=Owner();Mechanics(p);SpecialAi(p);Casting(p);Ready(p);
        var selected=Enemy(p,8);var n=Enemy(p,9);var legacy=Enemy(p,3);Require(NecromancerEffects.Apply(p,selected,CombatAbility.Attract,1),"Saved Attract fixture");
        var until=TimedEffects.Now.AddDays(1);var curse=NecromancerEffects.Curse(selected) with{UntilUtc=until};NecromancerEffects.Profiles.Store(selected,new(Curse:curse));
        var state=NecromancerEffects.Profiles.Find(n);state=state with{Mind=state.Mind with{UntilUtc=until,Seed=SourceRandom.InitLow(17),Draws=7}};NecromancerEffects.Profiles.Store(n,state);
        var old=new NecromancerCurse(CombatAbility.Attract,p.Serial.Value,1,until,p.X,p.Y);NecromancerEffects.Profiles.Store(legacy,new(Curse:old));
        TimedEffects.PreventHealing(selected,TimedEffects.Now);File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,selected.Serial.Value,n.Serial.Value,legacy.Serial.Value,curse,old,state.Mind,p.Map.MapID,p.Location,0)));
        foreach(var t in new Mobile[]{selected,n,legacy,p}){t.Internalize();}
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var t=(BaseCreature)World.FindMobile((Serial)s.Selected);var n=(BaseCreature)World.FindMobile((Serial)s.Neighbor);var legacy=World.FindMobile((Serial)s.Legacy);
        Require(NecromancerEffects.Curse(t)==s.Curse&&NecromancerEffects.Profiles.Find(n).Mind==s.Mind&&NecromancerEffects.Curse(legacy)==s.Old&&NecromancerEffects.Profiles.Find(legacy).Mind==null,"Saved selected/neighbor/RNG/legacy state changed");
        var account=FixtureNetwork.Authenticate(network,p,"attract-reload-");
        p.MoveToWorld(s.Point,Map.Maps[s.Map]);t.MoveToWorld(new(p.X+8,p.Y,p.Z),p.Map);n.MoveToWorld(new(p.X+9,p.Y,p.Z),p.Map);
        Require(n.Map.GetSector(n.Location).Active,"Connected reload did not activate NPC sector");
        Require(SkillBindings.Instance.Get(p).Right==CombatAbility.Attract,"Saved Attract binding");int hp=t.Hits;n.Frozen=n.CantWalk=false;n.AIObject.AITimer.Prod();var deadline=TimedEffects.Now.AddSeconds(12);
        void Finish()
        {
            if(t.Hits>=hp&&TimedEffects.Now<deadline){Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Finish));return;}
            try
            {
                Require(t.Hits<hp&&n.Combatant==t&&NecromancerEffects.Profiles.Find(n).Mind==s.Mind,$"Reload combat: life={t.Hits}/{hp}, target={n.Combatant?.Serial}/{t.Serial}, mind={JsonSerializer.Serialize(NecromancerEffects.Profiles.Find(n)?.Mind)}, expected={JsonSerializer.Serialize(s.Mind)}, frozen={n.Frozen}/{n.CantWalk}, timer={n.AIObject.AITimer.Running}, action={n.AIObject.Action}, at={n.Location}/{t.Location}, home={n.Home}/{n.HomeMap}, returning={(n as GraveyardUndead)?.ReturningHome}, enemy={n.IsEnemy(t)}, harmful={n.CanBeHarmful(t,false)}, PMH={!TimedEffects.CanRegenerateHits(t)}, sector={n.Map.GetSector(n.Location).Active}, warmode={n.Warmode}, nextSwing={n.NextCombatTime-Core.TickCount}, nextDecision={((GraveyardUndead)n).NextMeleeDecision-Core.TickCount}, hitChance={ClassStats.HitChance(ClassStats.AttackRating(n),ClassStats.Defense(t),ClassStats.Level(n),ClassStats.Level(t))}, chance={((GraveyardUndead)n).NormalStats.AttackChance}, resistance={t.PhysicalResistance}, health256={SkillHealth.Balance256(t)}");
                n.Frozen=n.CantWalk=true;n.AIObject.AITimer.Stop();foreach(var actor in new Mobile[]{p,t,n,legacy}){actor.Internalize();}
                network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();
                File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));Log.Information("attract-cast-reload-result: passed");
            }
            catch(Exception e){Log.Error(e,"attract-cast-reload-result: failed");}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Finish));
    }
    private static void Input(NetState network,bool quick)
    {
        var p=Owner();var t=Enemy(p,6);var n=Enemy(p,7);Ready(p);int mana=NecromancerEffects.ManaBalance256(p);string label=quick?"attract-cast-quick":"attract-cast-right";
        TimedEffects.PreventHealing(t,TimedEffects.Now);var account=FixtureNetwork.Authenticate(network,p,"attract-cast-");bool began=false,finished=false;long timeout=Core.TickCount+27000;
        void Fail(Exception e){if(finished){return;}finished=true;t.Delete();n.Delete();p.Delete();account.Delete();Log.Error(e,"{Label}-result: failed",label);}
        void Inspect()
        {
            if(finished){return;}
            try
            {
                Require(Core.TickCount<timeout,"Attract input timeout");var session=ClassCombat.For(p);
                if(session.PendingAbility==CombatAbility.Attract&&!began){Require(NecromancerEffects.Curse(t)==null&&NecromancerEffects.ManaBalance256(p)==mana,"Input windup paid/applied early");began=true;Log.Information("{Label}: windup",label);}
                if(NecromancerEffects.Curse(t) is not{} curse){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Inspect);return;}
                Require(began&&curse.Rank==1&&NecromancerEffects.ManaBalance256(p)==mana-4352,"Real input/payment");ClassCombat.Stop(p);
                Require((curse.UntilUtc-TimedEffects.Now).TotalMilliseconds is >11900 and <=12000,"Attract rank1 duration");
                var mind=NecromancerEffects.Profiles.Find(n).Mind;Require(mind is{Mode:2}&&mind.Focus==t.Serial.Value,"Real neighbor override");
                int hp=t.Hits;bool damaged=false;DateTime observe=TimedEffects.Now.AddSeconds(8);n.Frozen=n.CantWalk=false;n.AIObject.AITimer.Prod();
                void Damage()
                {
                    if(finished){return;}if(t.Hits>=hp&&TimedEffects.Now<observe){Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Damage));return;}
                    try{Require(t.Hits<hp&&SourceMind.Neutral(t)&&n.Combatant==t&&NecromancerEffects.Profiles.Find(n).Mind==mind,"Real autonomous neighbor damage/target/RNG");damaged=true;}catch(Exception e){Fail(e);}
                }
                void Expired()
                {
                    if(finished){return;}if(TimedEffects.Now<curse.UntilUtc.AddMilliseconds(100)){Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Expired));return;}
                    try{Require(damaged&&NecromancerEffects.Curse(t)==null&&!SourceMind.Neutral(t)&&NecromancerEffects.Profiles.Find(n).Mind is{Mode:0,Focus:0},"Natural selected curse/neighbor lease expiry");finished=true;t.Delete();n.Delete();p.Delete();account.Delete();Log.Information("{Label}-result: passed",label);}catch(Exception e){Fail(e);}
                }
                Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Damage));Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Expired));
            }
            catch(Exception e){Fail(e);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.Attract,(int)DiabloClass.Necromancer,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
