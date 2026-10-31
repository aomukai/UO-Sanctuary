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

public static class DimVisionCastChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DimVisionCastChecks));
    private sealed record Saved(uint Owner,uint Target,uint Legacy,NecromancerCurse Curse,NecromancerCurse Old,int Mana,int Map,Point3D Point,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"dim-vision-cast-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("dim-vision-cast-check",Check);Register("dim-vision-cast-reload-check",Reload);
        foreach(bool quick in new[]{false,true})
        {bool captured=quick;string label=quick?"dim-vision-cast-quick":"dim-vision-cast-right";Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,captured)));}
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"DimVision reference, learned casting and saved state",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static PlayerMobile Owner(int rank=1)
    {
        var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureDimVision";
        void Train(string id,int wanted)
        {
            TalentCatalog.TryGet(DiabloClass.Necromancer,id,out var talent);
            foreach(string prior in talent.Prerequisites){if(ClassCreation.Profiles.GetRank(p.Serial,prior)==0){Train(prior,1);}}
            while(ClassCreation.Profiles.GetRank(p.Serial,id)<wanted){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}
        }
        Train("necromancer.dimvision",rank);
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.DimVision)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.DimVision),"Dim Vision right/quick bindings");
        FixtureSpace.Corridor(p,6900,24,3500);return p;
    }
    private static GraveyardSkeleton Enemy(PlayerMobile p,int x=1,int y=0)
    {var t=PoisonDaggerChecks.Target(p);t.MoveToWorld(new(p.X+x,p.Y+y,p.Z),p.Map);foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Energy,ResistanceType.Poison}){t.SetResistance(type,0);}return t;}
    private static void Ready(PlayerMobile p){PoisonDaggerChecks.Ready(p);ClassCombat.For(p).RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,p.ManaMax*256);}
    private static void Differential()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"necromancer-ai-curse-source-cases.json")));int count=0;DateTime at=DateTime.UnixEpoch.AddSeconds(4);
        foreach(var r in doc.RootElement.EnumerateArray())
        {
            int V(int i)=>r[i].GetInt32();if(V(0)!=71||V(2)<=0){continue;}
            var difficulty=V(2)==4?MonsterDifficulty.Hell:V(2)==2?MonsterDifficulty.Nightmare:MonsterDifficulty.Normal;
            int rank=V(1),kind=V(7),oldRank=kind==0?0:kind is 1 or 4?rank:kind==2?rank+1:rank-1;
            NecromancerCurse old=kind==0?null:new(kind==4?CombatAbility.LifeTap:CombatAbility.DimVision,999,oldRank,DateTime.UnixEpoch.AddMilliseconds(777*40),0,0);
            bool duration=SourceCurseDuration.Reduce(NecromancerCurseCasts.AiFrames(175+50*(rank-1),difficulty),true,V(6),out int frames);
            var next=V(3)!=0&&V(4)!=0&&V(5)==0&&duration?NecromancerCurseCasts.Replacement(old,CombatAbility.DimVision,7,rank,null,at.AddMilliseconds(frames*40L),at,0,0):null;
            var current=next??old;
            Require((next!=null)==(V(8)!=0)&&(current?.Rank??0)==V(9)&&(current?.Source??0)==(uint)V(10)&&(current?.UntilUtc??DateTime.UnixEpoch)==DateTime.UnixEpoch.AddMilliseconds(V(11)*40L),"Dim Vision source receiver/difficulty arithmetic differential");count++;
        }
        Require(count==3600,"Incomplete supported Dim Vision source differential");
        foreach(int rank in new[]{1,2,3,20,21,255})
        {
            var v=new SkillValues(SkillMechanics.For(CombatAbility.DimVision),_=>rank,_=>0,99);
            Require(v.Mana256==2304&&v.Eval("aurarangecalc")==4+rank-1&&v.Eval("auralencalc")==175+50*(rank-1),"Pinned Dim Vision ranks");
        }
        Require(NecromancerCurseCasts.AiFrames(275,MonsterDifficulty.Nightmare)==137&&NecromancerCurseCasts.AiFrames(275,MonsterDifficulty.Hell)==68,"Difficulty division lost source frame truncation");
    }
    private static void Mechanics(PlayerMobile p,GraveyardSkeleton t)
    {
        var other=Owner();
        try
        {
            t.Combatant=p;t.AIObject.Action=ActionType.Combat;
            Require(NecromancerEffects.Apply(p,t,CombatAbility.DimVision,3),"Initial Dim Vision3");
            var first=NecromancerEffects.Curse(t) with{UntilUtc=TimedEffects.Now.AddMinutes(1)};NecromancerEffects.Profiles.Store(t,new(Curse:first));
            Require(first.Motion?.Target==p.Serial.Value,"Blind attachment lost pending chase");
            Require(!NecromancerEffects.Apply(other,t,CombatAbility.DimVision,1)&&NecromancerEffects.Curse(t)==first,"Weaker blind rank rewrote state");
            Require(NecromancerEffects.Apply(other,t,CombatAbility.DimVision,3)&&NecromancerEffects.Curse(t).Source==p.Serial.Value&&NecromancerEffects.Curse(t).Motion==first.Motion&&NecromancerEffects.Curse(t).UntilUtc<first.UntilUtc,"Equal blind refresh lost creator/motion/expiry");
            Require(NecromancerEffects.Apply(other,t,CombatAbility.DimVision,4)&&NecromancerEffects.Curse(t).Source==other.Serial.Value,"Higher blind rank did not replace creator");
            other.Delete();Require(NamedControl.Blind(t),"Deleting caster removed target-bound blind");
            Require(NecromancerEffects.Apply(p,t,CombatAbility.Confuse,1)&&!NecromancerEffects.Apply(p,t,CombatAbility.DimVision,1),"Learned blind ignored Confuse neutral alignment");
            Require(NecromancerEffects.Apply(p,t,CombatAbility.Weaken,1)&&!SourceMind.Neutral(t),"Ordinary curse did not restore Confuse alignment");
            Require(NecromancerEffects.Apply(p,t,CombatAbility.Attract,1)&&!NecromancerEffects.Apply(p,t,CombatAbility.DimVision,20),"Blind overwrote Attract");
        }
        finally{other.Delete();NecromancerEffects.Profiles.Clear(t);}
        var facets=SummonDifficulties.Facets();
        try
        {
            foreach(var difficulty in Enum.GetValues<MonsterDifficulty>())
            {
                SummonDifficulties.SetFacets(facets.Where(x=>x.Map!=p.Map.MapID).Append(new(p.Map.MapID,difficulty)).ToArray());
                var spawn=difficulty==MonsterDifficulty.Hell?MonsterDifficulty.Normal:MonsterDifficulty.Hell;
                var variant=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true};
                try
                {
                    // Variant assignment is a birth operation, before placement.
                    MonsterVariants.Assign(variant,spawn);variant.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);variant.AIObject.AITimer.Stop();
                    DateTime at=TimedEffects.Now;Require(NecromancerEffects.Apply(p,variant,CombatAbility.DimVision,3),"Facet duration cast");
                    double ms=(NecromancerEffects.Curse(variant).UntilUtc-at).TotalMilliseconds;int frames=NecromancerCurseCasts.AiFrames(275,difficulty);
                    Require(Math.Abs(ms-frames*40)<100,"Duration followed target spawn instead of configured facet");
                }
                finally{variant.Delete();}
            }
        }
        finally{SummonDifficulties.SetFacets(facets);}
        foreach(var grade in Enum.GetValues<EncounterGrade>())foreach(var champion in new[]{ChampionKind.Champion,ChampionKind.Possessed})
        {
            var e=new EncounterCreature{Frozen=true,CantWalk=true,NoKillAwards=true};e.Assign(EncounterProfiles.Profile("crypt-sentinel-60"),60,MonsterDifficulty.Normal,grade,champion,[],EliteAura.None,null,new Random(173));e.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);e.AIObject.AITimer.Stop();
            try
            {
                bool expected=grade is not(EncounterGrade.Unique or EncounterGrade.SuperUnique)&&!(grade==EncounterGrade.Champion&&champion==ChampionKind.Possessed);
                Require(NecromancerEffects.Apply(p,e,CombatAbility.DimVision,1)==expected,"Native encounter AI eligibility adapter "+grade+"/"+champion);
            }
            finally{e.Delete();}
        }
    }
    private static void Casting(PlayerMobile p)
    {
        GraveyardSkeleton near=Enemy(p,8),edge=Enemy(p,10),far=Enemy(p,10,1);var center=new Point3D(p.X+8,p.Y,p.Z);
        try
        {
            Ready(p);int mana=NecromancerEffects.ManaBalance256(p);
            Require(NecromancerCombat.Release(p,CombatAbility.DimVision,Serial.Zero,center),"Ground area release");
            Require(NecromancerEffects.ManaBalance256(p)==mana-2304&&NecromancerEffects.Curse(near)!=null&&NecromancerEffects.Curse(edge)!=null&&NecromancerEffects.Curse(far)==null,"Area center,radius or once-only payment");
            foreach(var t in new[]{near,edge,far}){NecromancerEffects.Profiles.Clear(t);}
            Ready(p);mana=NecromancerEffects.ManaBalance256(p);
            Require(ClassCombat.Perform(p,CombatAbility.DimVision,near,near.Location,Core.TickCount),"Targeted windup");
            Require(NecromancerEffects.ManaBalance256(p)==mana&&NecromancerEffects.Curse(near)==null,"Curse applied/paid during windup");
            near.MoveToWorld(new(p.X-1,p.Y,p.Z),p.Map);
            Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt)&&NecromancerEffects.Curse(near)!=null&&NecromancerEffects.Curse(edge)==null,"Release did not follow selected target position");
            Ready(p);NecromancerEffects.Profiles.Clear(near);mana=NecromancerEffects.ManaBalance256(p);
            Require(ClassCombat.Perform(p,CombatAbility.DimVision,null,center,Core.TickCount),"Cancelled windup");long effect=ClassCombat.For(p).EffectAt;
            var origin=p.Location;p.MoveToWorld(new(p.X,p.Y+1,p.Z),p.Map);Require(!ClassCombat.ResolveAction(p,effect)&&NecromancerEffects.ManaBalance256(p)==mana,"Movement cancellation spent mana");p.MoveToWorld(origin,p.Map);
            Ready(p);NecromancerEffects.SetMana256(p,2303);Require(!NecromancerCombat.Begin(p,CombatAbility.DimVision,Serial.Zero,center,Core.TickCount),"Insufficient mana began a cast");
            near.Delete();edge.Delete();far.Delete();Ready(p);mana=NecromancerEffects.ManaBalance256(p);Require(NecromancerCombat.Release(p,CombatAbility.DimVision,Serial.Zero,new(p.X+16,p.Y,p.Z))&&NecromancerEffects.ManaBalance256(p)==mana-2304,"Empty area did not pay once");
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
            Ready(p);Require(NecromancerCombat.Release(p,CombatAbility.DimVision,Serial.Zero,center)&&NecromancerEffects.Curse(t)!=null,"Filter2 gained an unrequested recipient LOS gate");
        }
        finally{wall.Delete();t.Delete();p.Delete();}
    }
    private static void Check()
    {
        Differential();var p=Owner();var t=Enemy(p);Mechanics(p,t);Casting(p);LineOfSight();
        while(ClassCreation.Profiles.GetRank(p.Serial,"necromancer.dimvision")<20){Require(ClassCreation.Profiles.TryTrain(p.Serial,"necromancer.dimvision",out _),"Train rank20");}
        PoisonDaggerChecks.Named(p,"Wormskull");Require(SkillMechanics.Rank(p,CombatAbility.DimVision)==21,"Real effective item rank");
        NecromancerEffects.Profiles.Clear(t);t.Combatant=p;t.AIObject.Action=ActionType.Combat;
        Require(NecromancerEffects.Apply(p,t,CombatAbility.DimVision,21),"Effective-rank blind");
        var curse=NecromancerEffects.Curse(t) with{UntilUtc=TimedEffects.Now.AddDays(1)};Require(curse.Motion?.Target==p.Serial.Value,"Saved pending chase missing");NecromancerEffects.Profiles.Store(t,new(Curse:curse));
        var legacy=Enemy(p,3);var old=new NecromancerCurse(CombatAbility.DimVision,p.Serial.Value,1,TimedEffects.Now.AddDays(1),p.X,p.Y);NecromancerEffects.Profiles.Store(legacy,new(Curse:old));Ready(p);
        var saved=new Saved(p.Serial.Value,t.Serial.Value,legacy.Serial.Value,curse,old,NecromancerEffects.ManaBalance256(p),p.Map.MapID,p.Location,0);
        // Preserve the saved motion inspection without a frozen world actor's
        // autonomous blocked-step decision clearing it before the next reload.
        t.Internalize();legacy.Internalize();p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
        Log.Information("Dim Vision cast:3600source rank/creator/difficulty arithmetic comparisons;facet-vs-spawn,encounter eligibility,chase retention,area/payment/LOS/input and legacy saves pass;exact blind tick remains separate");
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var t=(BaseCreature)World.FindMobile((Serial)s.Target);var legacy=(BaseCreature)World.FindMobile((Serial)s.Legacy);
        Require(NecromancerEffects.Curse(t)==s.Curse&&NecromancerEffects.Curse(legacy)==s.Old&&NecromancerEffects.ManaBalance256(p)==s.Mana,"Reload changed blind source/rank/motion/deadline/legacy/mana");
        p.MoveToWorld(s.Point,Map.Maps[s.Map]);Require(SkillMechanics.Rank(p,CombatAbility.DimVision)==21&&SkillBindings.Instance.Get(p).Right==CombatAbility.DimVision,"Reload changed item rank or binding");
        t.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);t.Frozen=false;t.CantWalk=false;t.AIObject.Action=ActionType.Combat;t.AIObject.NextMove=Core.TickCount;int before=t.X;
        Require(NecromancerEffects.Think(t)&&t.X==before-1&&t.Y==p.Y&&NecromancerEffects.Curse(t)==s.Curse,"Loaded blind pending chase did not resume one native step");
        t.Frozen=true;t.CantWalk=true;t.Internalize();legacy.Internalize();p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
    }
    private static void Input(NetState network,bool quick)
    {
        var p=Owner();var t=Enemy(p,6);Ready(p);int mana=NecromancerEffects.ManaBalance256(p);string label=quick?"dim-vision-cast-quick":"dim-vision-cast-right";
        network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var protocol=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);protocol.SetValue(network,Enum.Parse(protocol.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        bool began=false;long deadline=Core.TickCount+25000;
        void Fail(Exception e){t.Delete();p.Delete();Log.Error(e,"{Label}-result: failed",label);}
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"DimVision input timeout");var session=ClassCombat.For(p);
                if(session.PendingAbility==CombatAbility.DimVision&&!began){Require(NecromancerEffects.Curse(t)==null&&NecromancerEffects.ManaBalance256(p)==mana,"Input paid/applied during windup");began=true;Log.Information("{Label}: windup",label);}
                if(NecromancerEffects.Curse(t) is not{} curse){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Inspect);return;}
                Require(began&&curse.Rank==1&&NecromancerEffects.ManaBalance256(p)==mana-2304,"Real input capture/payment");ClassCombat.Stop(p);
                Require((curse.UntilUtc-TimedEffects.Now).TotalMilliseconds is >6900 and <=7000,"Rank1duration is not175frames");
                Timer.StartTimer(curse.UntilUtc-TimedEffects.Now+TimeSpan.FromMilliseconds(100),()=>
                {try{Require(NecromancerEffects.Curse(t)==null,"Natural rank1curse did not expire");Require(!NamedControl.Blind(t),"Expired curse still blinded");t.Delete();p.Delete();Log.Information("{Label}-result: passed",label);}catch(Exception e){Fail(e);}});
            }
            catch(Exception e){Fail(e);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.DimVision,(int)DiabloClass.Necromancer,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
