// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary.Integration;

public static class TerrorCastChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(TerrorCastChecks));
    private sealed record Saved(uint Owner,uint Target,uint Legacy,NecromancerCurse Curse,NecromancerCurse Old,int Mana,int Map,Point3D Point,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"terror-cast-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("terror-cast-check",Check);Register("terror-cast-reload-check",Reload);
        foreach(bool quick in new[]{false,true})
        {bool captured=quick;string label=quick?"terror-cast-quick":"terror-cast-right";Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,captured)));}
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Terror reference, learned casting and saved state",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static PlayerMobile Owner(int rank=1)
    {
        var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureTerror";
        void Train(string id,int wanted)
        {
            TalentCatalog.TryGet(DiabloClass.Necromancer,id,out var talent);
            foreach(string prior in talent.Prerequisites){if(ClassCreation.Profiles.GetRank(p.Serial,prior)==0){Train(prior,1);}}
            while(ClassCreation.Profiles.GetRank(p.Serial,id)<wanted){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}
        }
        Train("necromancer.terror",rank);
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.Terror)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Terror),"Terror right/quick bindings");
        FixtureSpace.Corridor(p,7100,24,3500);return p;
    }
    private static GraveyardSkeleton Enemy(PlayerMobile p,int x=1,int y=0)
    {var t=PoisonDaggerChecks.Target(p);t.MoveToWorld(new(p.X+x,p.Y+y,p.Z),p.Map);foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Energy,ResistanceType.Poison}){t.SetResistance(type,0);}return t;}
    private static void Ready(PlayerMobile p){PoisonDaggerChecks.Ready(p);ClassCombat.For(p).RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,p.ManaMax*256);}
    private static void Differential()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"necromancer-ai-curse-source-cases.json")));int count=0;DateTime at=DateTime.UnixEpoch.AddSeconds(4);
        foreach(var r in doc.RootElement.EnumerateArray())
        {
            int V(int i)=>r[i].GetInt32();if(V(0)!=77||V(2)<=0){continue;}
            var difficulty=V(2)==4?MonsterDifficulty.Hell:V(2)==2?MonsterDifficulty.Nightmare:MonsterDifficulty.Normal;
            int rank=V(1),kind=V(7),oldRank=kind==0?0:kind is 1 or 4?rank:kind==2?rank+1:rank-1;
            NecromancerCurse old=kind==0?null:new(kind==4?CombatAbility.LifeTap:CombatAbility.Terror,999,oldRank,DateTime.UnixEpoch.AddMilliseconds(777*40),0,0);
            bool duration=SourceCurseDuration.Reduce(NecromancerCurseCasts.AiFrames(200+25*(rank-1),difficulty),true,V(6),out int frames);
            var next=V(3)!=0&&V(4)!=0&&V(5)==0&&duration?NecromancerCurseCasts.Replacement(old,CombatAbility.Terror,7,rank,null,at.AddMilliseconds(frames*40L),at,0,0):null;
            var current=next??old;
            Require((next!=null)==(V(8)!=0)&&(current?.Rank??0)==V(9)&&(current?.Source??0)==(uint)V(10)&&(current?.UntilUtc??DateTime.UnixEpoch)==DateTime.UnixEpoch.AddMilliseconds(V(11)*40L),"Terror source receiver/difficulty arithmetic differential");count++;
        }
        Require(count==3600,"Incomplete supported Terror source differential");
        foreach(int rank in new[]{1,2,3,20,21,255})
        {
            var v=new SkillValues(SkillMechanics.For(CombatAbility.Terror),_=>rank,_=>0,99);
            Require(v.Mana256==1792&&v.Eval("aurarangecalc")==4&&v.Eval("auralencalc")==200+25*(rank-1),"Pinned Terror ranks");
        }
        Require(NecromancerCurseCasts.AiFrames(250,MonsterDifficulty.Nightmare)==125&&NecromancerCurseCasts.AiFrames(250,MonsterDifficulty.Hell)==62,"Difficulty division lost source frame truncation");
    }
    private static void Mechanics(PlayerMobile p,GraveyardSkeleton t)
    {
        var other=Owner();
        try
        {
            t.Combatant=p;t.AIObject.Action=ActionType.Combat;
            Require(NecromancerEffects.Apply(p,t,CombatAbility.Terror,3),"Initial learned Terror3");
            var first=NecromancerEffects.Curse(t) with{UntilUtc=TimedEffects.Now.AddMinutes(1)};NecromancerEffects.Profiles.Store(t,new(Curse:first));
            Require(first.Terror is{Skill:CombatAbility.Terror,LaunchRank:3,Started:false}&&first.Terror.Override==p.Serial.Value,"Initializer did not capture existing target");
            Require(!NecromancerEffects.Apply(other,t,CombatAbility.Terror,1)&&NecromancerEffects.Curse(t)==first,"Weaker Terror rank rewrote state");
            var active=first with{Terror=first.Terror with{Started=true,Override=0,Focus=p.Serial.Value,Combat=true}};NecromancerEffects.Profiles.Store(t,new(Curse:active));t.Combatant=p;
            Require(NecromancerEffects.Apply(other,t,CombatAbility.Terror,3),"Equal Terror refresh");var equal=NecromancerEffects.Curse(t);
            Require(equal.Source==p.Serial.Value&&equal.UntilUtc<first.UntilUtc&&equal.Terror is{Started:false,Combat:false,Goal:null}&&equal.Terror.Override==p.Serial.Value,"Equal refresh lost creator/expiry or failed to reinitialize AI");
            Require(NecromancerEffects.Apply(other,t,CombatAbility.Terror,4)&&NecromancerEffects.Curse(t).Source==other.Serial.Value,"Higher Terror rank did not replace creator");
            other.Delete();Require(NecromancerEffects.Curse(t)?.Ability==CombatAbility.Terror,"Deleting caster removed target-bound Terror");
            Require(NecromancerEffects.Apply(p,t,CombatAbility.Confuse,1)&&NecromancerEffects.Apply(p,t,CombatAbility.Terror,1),"Learned Terror could not replace Confuse");
            Require(NecromancerEffects.Apply(p,t,CombatAbility.Attract,1)&&!NecromancerEffects.Apply(p,t,CombatAbility.Terror,20),"Terror overwrote Attract");
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
                    MonsterVariants.Assign(variant,spawn);variant.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);variant.AIObject.AITimer.Stop();
                    DateTime at=TimedEffects.Now;Require(NecromancerEffects.Apply(p,variant,CombatAbility.Terror,3),"Facet Terror cast");
                    double ms=(NecromancerEffects.Curse(variant).UntilUtc-at).TotalMilliseconds;int frames=NecromancerCurseCasts.AiFrames(250,difficulty);
                    Require(Math.Abs(ms-frames*40)<100,"Terror duration followed target spawn instead of configured facet");
                }
                finally{variant.Delete();}
            }
        }
        finally{SummonDifficulties.SetFacets(facets);}
        var unknown=new Ogre();unknown.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);unknown.AIObject.AITimer.Stop();
        try{Require(!NecromancerEffects.Apply(p,unknown,CombatAbility.Terror,1),"Unknown native AI gained invented Terror transition");}finally{unknown.Delete();}
    }
    private static void WorldTargets(PlayerMobile p)
    {
        var other=Owner();other.MoveToWorld(new(p.X+6,p.Y,p.Z),p.Map);var t=Enemy(p,4);
        try
        {
            t.Combatant=other;Require(NecromancerEffects.Apply(p,t,CombatAbility.Terror,1),"Existing-target Terror");
            Require(SourceTerror.State(t).Override==other.Serial.Value,"Learned Terror substituted caster for held target");
            t.Frozen=t.CantWalk=false;t.AIObject.NextMove=Core.TickCount;int x=t.X,hp=t.Hits;
            Require(NecromancerEffects.Think(t)&&t.X==x-1&&t.Hits==hp&&SourceTerror.State(t) is{Started:true,Override:0} moved&&moved.Focus==other.Serial.Value,"Initial escape was not away from existing target");
            Require(NecromancerEffects.Apply(p,t,CombatAbility.Weaken,1)&&SourceTerror.State(t)==null&&!t.AIObject.TryGetMoveWake(out _),"Replacement retained fear movement");
            t.Delete();t=Enemy(p,4);t.Combatant=null;
            Require(NecromancerEffects.Apply(p,t,CombatAbility.Terror,1)&&SourceTerror.State(t).Override==0,"No-target initializer manufactured caster override");
            t.Frozen=t.CantWalk=false;t.NextReacquireTime=t.AIObject.NextMove=Core.TickCount;x=t.X;
            Require(NecromancerEffects.Think(t)&&t.X==x-1&&SourceTerror.State(t).Focus==other.Serial.Value,"No-target Terror did not naturally acquire the nearer threat");
            // Howl has a different caller contract: its caster override remains.
            NecromancerEffects.Profiles.Clear(t);t.Combatant=other;
            Require(SourceTerror.Apply(p,t,1,75,TimedEffects.Now)&&SourceTerror.State(t) is{Skill:CombatAbility.Howl} howl&&howl.Override==p.Serial.Value,"Learned initializer changed Howl caster assignment");
        }
        finally{t.Delete();other.Delete();}
    }
    private static void Casting(PlayerMobile p)
    {
        GraveyardSkeleton near=Enemy(p,8),edge=Enemy(p,10),far=Enemy(p,10,1);var center=new Point3D(p.X+8,p.Y,p.Z);
        try
        {
            Ready(p);int mana=NecromancerEffects.ManaBalance256(p);
            Require(NecromancerCombat.Release(p,CombatAbility.Terror,Serial.Zero,center),"Ground area release");
            Require(NecromancerEffects.ManaBalance256(p)==mana-1792&&NecromancerEffects.Curse(near)!=null&&NecromancerEffects.Curse(edge)!=null&&NecromancerEffects.Curse(far)==null,"Area center,radius or once-only payment");
            foreach(var t in new[]{near,edge,far}){NecromancerEffects.Profiles.Clear(t);}
            Ready(p);mana=NecromancerEffects.ManaBalance256(p);
            Require(ClassCombat.Perform(p,CombatAbility.Terror,near,near.Location,Core.TickCount),"Targeted windup");
            Require(NecromancerEffects.ManaBalance256(p)==mana&&NecromancerEffects.Curse(near)==null,"Curse applied/paid during windup");
            near.MoveToWorld(new(p.X-1,p.Y,p.Z),p.Map);
            Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt)&&NecromancerEffects.Curse(near)!=null&&NecromancerEffects.Curse(edge)==null,"Release did not follow selected target position");
            Ready(p);NecromancerEffects.Profiles.Clear(near);mana=NecromancerEffects.ManaBalance256(p);
            Require(ClassCombat.Perform(p,CombatAbility.Terror,null,center,Core.TickCount),"Cancelled windup");long effect=ClassCombat.For(p).EffectAt;
            var origin=p.Location;p.MoveToWorld(new(p.X,p.Y+1,p.Z),p.Map);Require(!ClassCombat.ResolveAction(p,effect)&&NecromancerEffects.ManaBalance256(p)==mana,"Movement cancellation spent mana");p.MoveToWorld(origin,p.Map);
            Ready(p);NecromancerEffects.SetMana256(p,1791);Require(!NecromancerCombat.Begin(p,CombatAbility.Terror,Serial.Zero,center,Core.TickCount),"Insufficient mana began a cast");
            near.Delete();edge.Delete();far.Delete();Ready(p);mana=NecromancerEffects.ManaBalance256(p);Require(NecromancerCombat.Release(p,CombatAbility.Terror,Serial.Zero,new(p.X+16,p.Y,p.Z))&&NecromancerEffects.ManaBalance256(p)==mana-1792,"Empty area did not pay once");
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
            Ready(p);Require(NecromancerCombat.Release(p,CombatAbility.Terror,Serial.Zero,center)&&NecromancerEffects.Curse(t)!=null,"Filter2 gained an unrequested recipient LOS gate");
        }
        finally{wall.Delete();t.Delete();p.Delete();}
    }
    private static void Check()
    {
        Differential();var p=Owner();var t=Enemy(p);Mechanics(p,t);Casting(p);LineOfSight();WorldTargets(p);
        while(ClassCreation.Profiles.GetRank(p.Serial,"necromancer.terror")<20){Require(ClassCreation.Profiles.TryTrain(p.Serial,"necromancer.terror",out _),"Train rank20");}
        PoisonDaggerChecks.Named(p,"Wormskull");Require(SkillMechanics.Rank(p,CombatAbility.Terror)==21,"Real effective item rank");
        NecromancerEffects.Profiles.Clear(t);t.Combatant=p;t.AIObject.Action=ActionType.Combat;
        Require(NecromancerEffects.Apply(p,t,CombatAbility.Terror,21),"Effective-rank Terror");
        var curse=NecromancerEffects.Curse(t) with{UntilUtc=TimedEffects.Now.AddDays(1)};Require(curse.Terror is{Skill:CombatAbility.Terror,LaunchRank:21}&&curse.Terror.Override==p.Serial.Value,"Saved learned initializer missing");NecromancerEffects.Profiles.Store(t,new(Curse:curse));
        var legacy=Enemy(p,3);var old=new NecromancerCurse(CombatAbility.Terror,p.Serial.Value,1,TimedEffects.Now.AddDays(1),p.X,p.Y);NecromancerEffects.Profiles.Store(legacy,new(Curse:old));Ready(p);
        var saved=new Saved(p.Serial.Value,t.Serial.Value,legacy.Serial.Value,curse,old,NecromancerEffects.ManaBalance256(p),p.Map.MapID,p.Location,0);
        t.Internalize();legacy.Internalize();p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
        Log.Information("Learned Terror:3600source rank/creator/difficulty comparisons;existing-target vs caster,no-target natural acquisition,real escape,Howl distinction,refresh/replacement,facet/payment/LOS and legacy saves pass");
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var t=(BaseCreature)World.FindMobile((Serial)s.Target);var legacy=(BaseCreature)World.FindMobile((Serial)s.Legacy);
        Require(NecromancerEffects.Curse(t)==s.Curse&&NecromancerEffects.Curse(legacy)==s.Old&&NecromancerEffects.ManaBalance256(p)==s.Mana,"Reload changed Terror source/rank/phase/deadline/legacy/mana");
        p.MoveToWorld(s.Point,Map.Maps[s.Map]);Require(SkillMechanics.Rank(p,CombatAbility.Terror)==21&&SkillBindings.Instance.Get(p).Right==CombatAbility.Terror,"Reload changed item rank or binding");
        t.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);t.Frozen=false;t.CantWalk=false;t.AIObject.NextMove=t.NextReacquireTime=Core.TickCount;int before=t.X;
        Require(NecromancerEffects.Think(t)&&t.X==before+1&&SourceTerror.State(t) is{Skill:CombatAbility.Terror,Started:true,Override:0} ai&&ai.Focus==p.Serial.Value,"Loaded learned Terror did not resume a real native escape step");
        var curse=NecromancerEffects.Curse(t);t.Frozen=t.CantWalk=true;t.Internalize();legacy.Internalize();p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Curse=curse,Generation=s.Generation+1}));
    }
    private static void Input(NetState network,bool quick)
    {
        var p=Owner();var t=Enemy(p,6);Ready(p);int mana=NecromancerEffects.ManaBalance256(p);string label=quick?"terror-cast-quick":"terror-cast-right";
        network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;
        var protocol=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);protocol.SetValue(network,Enum.Parse(protocol.FieldType,"GameServer_LoggedIn"));
        var account=new Account("terror-cast-"+Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"));account[0]=p;network.Account=account;network.Mobile=p;p.NetState=network;network.ResetMovementState();network.SendMobileUpdate(p);
        bool began=false,finished=false;long deadline=Core.TickCount+25000;
        void Fail(Exception e){if(finished){return;}finished=true;t.Delete();p.Delete();account.Delete();Log.Error(e,"{Label}-result: failed",label);}
        void Inspect()
        {
            if(finished){return;}
            try
            {
                Require(Core.TickCount<deadline,"Terror input timeout");var session=ClassCombat.For(p);
                if(session.PendingAbility==CombatAbility.Terror&&!began){Require(NecromancerEffects.Curse(t)==null&&NecromancerEffects.ManaBalance256(p)==mana,"Input paid/applied during windup");began=true;Log.Information("{Label}: windup",label);}
                if(NecromancerEffects.Curse(t) is not{} curse){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Inspect);return;}
                Require(began&&curse.Rank==1&&NecromancerEffects.ManaBalance256(p)==mana-1792,"Real input capture/payment");ClassCombat.Stop(p);
                Require((curse.UntilUtc-TimedEffects.Now).TotalMilliseconds is >7900 and <=8000,"Rank1duration is not200frames");
                int startDistance=SourceTerror.Distance(t,p),hp=t.Hits;DateTime movementAt=TimedEffects.Now.AddMilliseconds(1500);bool escaped=false;
                t.Frozen=t.CantWalk=false;t.AIObject.AITimer.Prod();
                void Escaping()
                {
                    if(finished){return;}
                    if(TimedEffects.Now<movementAt){Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Escaping));return;}
                    try{Require(SourceTerror.State(t) is{Started:true,Override:0,Combat:false}&&SourceTerror.Distance(t,p)>startDistance&&t.Hits==hp&&!t.ReturningHome,"Real learned cast failed autonomous non-damaging escape");escaped=true;}catch(Exception e){Fail(e);}
                }
                Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Escaping));
                void Expired()
                {
                    if(finished){return;}
                    if(TimedEffects.Now<curse.UntilUtc.AddMilliseconds(100)){Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Expired));return;}
                    try{Require(escaped&&NecromancerEffects.Curse(t)==null&&SourceTerror.State(t)==null&&NecromancerEffects.Profiles.Find(t).Curse==null,"Natural rank1 Terror did not retire its actual control state");finished=true;t.Delete();p.Delete();account.Delete();Log.Information("{Label}-result: passed",label);}catch(Exception e){Fail(e);}
                }
                Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Expired));
            }
            catch(Exception e){Fail(e);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.Terror,(int)DiabloClass.Necromancer,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
