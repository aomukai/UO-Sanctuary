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

public static class LifeTapCastChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(LifeTapCastChecks));
    private sealed record Saved(uint Owner,uint Target,uint Legacy,uint Healer,int HealerLife,NecromancerCurse Curse,NecromancerCurse Old,int Mana,int Map,Point3D Point,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"life-tap-cast-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("life-tap-cast-check",Check);Register("life-tap-cast-reload-check",Reload);
        foreach(bool quick in new[]{false,true})
        {bool captured=quick;string label=quick?"life-tap-cast-quick":"life-tap-cast-right";Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,captured)));}
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"LifeTap reference, learned casting and saved state",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static PlayerMobile Owner(int rank=1)
    {
        var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureLifeTap";
        void Train(string id,int wanted)
        {
            TalentCatalog.TryGet(DiabloClass.Necromancer,id,out var talent);
            foreach(string prior in talent.Prerequisites){if(ClassCreation.Profiles.GetRank(p.Serial,prior)==0){Train(prior,1);}}
            while(ClassCreation.Profiles.GetRank(p.Serial,id)<wanted){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}
        }
        Train("necromancer.lifetap",rank);
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.LifeTap)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.LifeTap),"Life Tap right/quick bindings");
        FixtureSpace.Corridor(p,6800,24,3500);return p;
    }
    private static GraveyardSkeleton Enemy(PlayerMobile p,int x=1,int y=0)
    {var t=PoisonDaggerChecks.Target(p);t.MoveToWorld(new(p.X+x,p.Y+y,p.Z),p.Map);foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Energy,ResistanceType.Poison}){t.SetResistance(type,0);}return t;}
    private static void Ready(PlayerMobile p){PoisonDaggerChecks.Ready(p);ClassCombat.For(p).RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,p.ManaMax*256);}
    private static void Differential()
    {
        using var eventDoc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"life-tap-event-cases.json")));int rows=0,accepted=0;
        foreach(var r in eventDoc.RootElement.EnumerateArray())
        {
            int hp=r[1].GetInt32(),maximum=r[2].GetInt32();bool success=r[7].GetInt32()!=0;
            int expected=success?Math.Min(maximum,hp+NecromancerEffects.LifeTapHealing256(r[0].GetInt32(),r[3].GetInt32())):hp;
            Require(expected==r[6].GetInt32(),"Complete Life Tap event/actual percentage comparison");rows++;if(success){accepted++;}
        }
        Require((rows,accepted)==(22464,1536),"Incomplete Life Tap event fixture");
        Require(NecromancerEffects.LifeTapHealing256(1048577,50)==524250&&NecromancerEffects.LifeTapHealing256(1280001,50)==640000,"Large non-divisible source healing boundary");
        using var receiver=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"life-tap-cast-receiver-cases.json")));int count=0;DateTime at=DateTime.UnixEpoch.AddSeconds(4);
        foreach(var r in receiver.RootElement.EnumerateArray())
        {
            int oldRank=r[0].GetInt32(),rank=r[1].GetInt32(),other=r[2].GetInt32(),resistance=r[3].GetInt32(),attract=r[4].GetInt32();
            NecromancerCurse old=oldRank==0?null:new(other==1?CombatAbility.Weaken:CombatAbility.LifeTap,999,oldRank,DateTime.UnixEpoch.AddMilliseconds(777*40),0,0);
            bool duration=SourceCurseDuration.Reduce(400+60*(rank-1),true,resistance,out int frames);
            var next=duration&&attract==0?NecromancerCurseCasts.Replacement(old,CombatAbility.LifeTap,7,rank,null,at.AddMilliseconds(frames*40L),at,0,0):null;var current=next??old;
            Require((next!=null)==(r[5].GetInt32()!=0)&&(current?.Rank??0)==r[6].GetInt32()&&(current?.Source??0)==r[7].GetUInt32()&&(current?.UntilUtc??DateTime.UnixEpoch)==DateTime.UnixEpoch.AddMilliseconds(r[8].GetInt32()*40L)
                &&current?.PhysicalDelta==null&&current?.ElementDeltas==null,"No-stat source receiver/factory differential");count++;
        }
        Require(count==384,"Incomplete no-stat receiver fixture");
        foreach(int rank in new[]{1,2,3,20,21,255})
        {
            var v=new SkillValues(SkillMechanics.For(CombatAbility.LifeTap),_=>rank,_=>0,99);
            Require(v.Mana256==2304&&v.Eval("aurarangecalc")==4+rank-1&&v.Eval("auralencalc")==400+60*(rank-1)&&v.Eval("calc1")==50,"Pinned Life Tap ranks");
        }
    }
    private static void Full(Mobile t)=>SkillHealth.SetBalance256(t,SkillHealth.Maximum256(t));
    private static void Low(Mobile t)=>SkillHealth.SetBalance256(t,2561);
    private static void Mechanics(PlayerMobile p,GraveyardSkeleton t)
    {
        var healer=Enemy(p,2);
        try
        {
            Require(NecromancerEffects.Apply(p,t,CombatAbility.LifeTap,1),"Life Tap application");
            foreach(int physical in new[]{1,255,257,1048577,1048583,1280001,2000001})
            {
                Low(healer);int expected=NecromancerEffects.LifeTapHealing256(physical,50);
                Require(NecromancerEffects.LifeTap(healer,t,physical)==expected&&SkillHealth.Balance256(healer)==2561+expected,"Actual fractional/large healing consumer");
            }
            Full(healer);Require(NecromancerEffects.LifeTap(healer,t,257)==0,"Life Tap exceeded maximum life");
            Low(healer);Require(NecromancerEffects.LifeTap(healer,t,0)==0&&SkillHealth.Balance256(healer)==2561,"Zero event healed");
            var overkill=Enemy(p,3);
            try
            {
                Require(NecromancerEffects.Apply(p,overkill,CombatAbility.LifeTap,1),"Overkill curse");SkillHealth.SetBalance256(overkill,257);Low(healer);
                AOS.Damage(overkill,healer,100,false,100,0,0,0,0,weaponDamage:true);
                Require(SkillHealth.Balance256(healer)==2561+12800&&overkill is not{Alive:true,Deleted:false},"Native healing was capped to victim life or duplicated");
            }
            finally{overkill.Corpse?.Delete();overkill.Delete();}
            Low(healer);NecromancerEffects.Profiles.Clear(t);Require(NecromancerEffects.LifeTap(healer,t,257)==0,"Absent curse healed");
            Require(NecromancerEffects.Apply(p,t,CombatAbility.LifeTap,1),"Restore curse");var active=NecromancerEffects.Curse(t);
            NecromancerEffects.Profiles.Store(t,new(Curse:active with{UntilUtc=TimedEffects.Now.AddMilliseconds(-1)}));Require(NecromancerEffects.LifeTap(healer,t,257)==0,"Expired curse healed");
        }
        finally{healer.Delete();}
        var other=Owner();
        try
        {
            Require(NecromancerEffects.Apply(p,t,CombatAbility.LifeTap,3),"Initial Life Tap3");var first=NecromancerEffects.Curse(t) with{UntilUtc=TimedEffects.Now.AddMinutes(1)};NecromancerEffects.Profiles.Store(t,new(Curse:first));
            Require(!NecromancerEffects.Apply(other,t,CombatAbility.LifeTap,1)&&NecromancerEffects.Curse(t)==first,"Weaker rank rewrote state");
            Require(NecromancerEffects.Apply(other,t,CombatAbility.LifeTap,3)&&NecromancerEffects.Curse(t).Source==p.Serial.Value&&NecromancerEffects.Curse(t).UntilUtc<first.UntilUtc,"Equal refresh lost original caster or expiry");
            Require(NecromancerEffects.Apply(other,t,CombatAbility.LifeTap,4)&&NecromancerEffects.Curse(t).Source==other.Serial.Value,"Higher rank did not replace caster");other.Delete();Low(p);
            Require(NecromancerEffects.LifeTap(p,t,257)==128&&SkillHealth.Balance256(p)==2689,"Caster deletion lost target-bound fractional healing");
        }
        finally{other.Delete();}
        Require(NecromancerEffects.Apply(p,t,CombatAbility.Weaken,1)&&NecromancerEffects.LifeTap(p,t,257)==0,"Different curse retained Life Tap");
        Require(NecromancerEffects.Apply(p,t,CombatAbility.Attract,1)&&!NecromancerEffects.Apply(p,t,CombatAbility.LifeTap,20),"Life Tap overwrote Attract");NecromancerEffects.Profiles.Clear(t);
        var possessed=new EncounterCreature{Frozen=true,CantWalk=true};possessed.Assign(EncounterProfiles.Profile("crypt-sentinel-60"),60,MonsterDifficulty.Normal,EncounterGrade.Champion,ChampionKind.Possessed,[],EliteAura.None,null,new Random(173));possessed.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);possessed.AIObject.AITimer.Stop();
        try{Require(!NecromancerEffects.Apply(p,possessed,CombatAbility.LifeTap,1),"Possessed accepted Life Tap");}finally{possessed.Delete();}
        Full(p);Full(t);
    }
    private static void Casting(PlayerMobile p)
    {
        GraveyardSkeleton near=Enemy(p,8),edge=Enemy(p,10),far=Enemy(p,10,1);var center=new Point3D(p.X+8,p.Y,p.Z);
        try
        {
            Ready(p);int mana=NecromancerEffects.ManaBalance256(p);
            Require(NecromancerCombat.Release(p,CombatAbility.LifeTap,Serial.Zero,center),"Ground area release");
            Require(NecromancerEffects.ManaBalance256(p)==mana-2304&&NecromancerEffects.Curse(near)!=null&&NecromancerEffects.Curse(edge)!=null&&NecromancerEffects.Curse(far)==null,"Area center,radius or once-only payment");
            foreach(var t in new[]{near,edge,far}){NecromancerEffects.Profiles.Clear(t);}
            Ready(p);mana=NecromancerEffects.ManaBalance256(p);
            Require(ClassCombat.Perform(p,CombatAbility.LifeTap,near,near.Location,Core.TickCount),"Targeted windup");
            Require(NecromancerEffects.ManaBalance256(p)==mana&&NecromancerEffects.Curse(near)==null,"Curse applied/paid during windup");
            near.MoveToWorld(new(p.X-1,p.Y,p.Z),p.Map);
            Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt)&&NecromancerEffects.Curse(near)!=null&&NecromancerEffects.Curse(edge)==null,"Release did not follow selected target position");
            Ready(p);NecromancerEffects.Profiles.Clear(near);mana=NecromancerEffects.ManaBalance256(p);
            Require(ClassCombat.Perform(p,CombatAbility.LifeTap,null,center,Core.TickCount),"Cancelled windup");long effect=ClassCombat.For(p).EffectAt;
            var origin=p.Location;p.MoveToWorld(new(p.X,p.Y+1,p.Z),p.Map);Require(!ClassCombat.ResolveAction(p,effect)&&NecromancerEffects.ManaBalance256(p)==mana,"Movement cancellation spent mana");p.MoveToWorld(origin,p.Map);
            Ready(p);NecromancerEffects.SetMana256(p,2303);Require(!NecromancerCombat.Begin(p,CombatAbility.LifeTap,Serial.Zero,center,Core.TickCount),"Insufficient mana began a cast");
            near.Delete();edge.Delete();far.Delete();Ready(p);mana=NecromancerEffects.ManaBalance256(p);Require(NecromancerCombat.Release(p,CombatAbility.LifeTap,Serial.Zero,new(p.X+16,p.Y,p.Z))&&NecromancerEffects.ManaBalance256(p)==mana-2304,"Empty area did not pay once");
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
            Ready(p);Require(NecromancerCombat.Release(p,CombatAbility.LifeTap,Serial.Zero,center)&&NecromancerEffects.Curse(t)!=null,"Filter3 gained an unrequested recipient LOS gate");
        }
        finally{wall.Delete();t.Delete();p.Delete();}
    }
    private static void Check()
    {
        Differential();var p=Owner();var t=Enemy(p);Mechanics(p,t);Casting(p);LineOfSight();
        while(ClassCreation.Profiles.GetRank(p.Serial,"necromancer.lifetap")<20){Require(ClassCreation.Profiles.TryTrain(p.Serial,"necromancer.lifetap",out _),"Train rank20");}
        PoisonDaggerChecks.Named(p,"Wormskull");Require(SkillMechanics.Rank(p,CombatAbility.LifeTap)==21,"Real effective item rank");
        NecromancerEffects.Profiles.Clear(t);Require(NecromancerEffects.Apply(p,t,CombatAbility.LifeTap,21),"Effective-rank curse");
        var curse=NecromancerEffects.Curse(t) with{UntilUtc=TimedEffects.Now.AddDays(1)};NecromancerEffects.Profiles.Store(t,new(Curse:curse));
        var legacy=Enemy(p,3);var old=new NecromancerCurse(CombatAbility.LifeTap,p.Serial.Value,1,TimedEffects.Now.AddDays(1),p.X,p.Y);
        Require(!JsonSerializer.Serialize(old).Contains("PhysicalDelta")&&!JsonSerializer.Serialize(old).Contains("ElementDeltas"),"No-stat curse manufactured a resistance field");NecromancerEffects.Profiles.Store(legacy,new(Curse:old));
        var healer=Enemy(p,5);Low(healer);
        // GraveyardUndead has custom OnThink regeneration independent of the
        // native regen-rate fixture. Block only natural healing across reloads;
        // explicit Life Tap recovery must still work on this actor.
        TimedEffects.PreventHealing(healer,TimedEffects.Now);Ready(p);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,t.Serial.Value,legacy.Serial.Value,healer.Serial.Value,SkillHealth.Balance256(healer),curse,old,NecromancerEffects.ManaBalance256(p),p.Map.MapID,p.Location,0)));
        Log.Information("Life Tap cast:22464complete event/actual-helper comparisons,384no-stat receivers,large FP/native overkill,rank/caster/area/payment/LOS/input and saved fractional state pass");
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var t=(BaseCreature)World.FindMobile((Serial)s.Target);var legacy=(BaseCreature)World.FindMobile((Serial)s.Legacy);var healer=(BaseCreature)World.FindMobile((Serial)s.Healer);
        Require(NecromancerEffects.Curse(t)==s.Curse&&NecromancerEffects.Curse(legacy)==s.Old&&NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillHealth.Balance256(healer)==s.HealerLife,"Reload changed state:curse="+(NecromancerEffects.Curse(t)==s.Curse)+",legacy="+(NecromancerEffects.Curse(legacy)==s.Old)+",mana="+NecromancerEffects.ManaBalance256(p)+"/"+s.Mana+",life="+SkillHealth.Balance256(healer)+"/"+s.HealerLife);
        Require(!TimedEffects.CanRegenerateHits(healer),"Saved fixture natural-regeneration block expired");
        p.MoveToWorld(s.Point,Map.Maps[s.Map]);Require(SkillMechanics.Rank(p,CombatAbility.LifeTap)==21&&SkillBindings.Instance.Get(p).Right==CombatAbility.LifeTap,"Reload changed item rank or binding");
        Require(NecromancerEffects.LifeTap(healer,t,257)==128&&SkillHealth.Balance256(healer)==s.HealerLife+128,"Restored fractional defender event");
        Require(NecromancerEffects.LifeTap(healer,legacy,1)==0,"Legacy one-FP event should truncate to zero healing");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1,HealerLife=SkillHealth.Balance256(healer)}));
    }
    private static void Input(NetState network,bool quick)
    {
        var p=Owner();var t=Enemy(p,6);Ready(p);int mana=NecromancerEffects.ManaBalance256(p);string label=quick?"life-tap-cast-quick":"life-tap-cast-right";
        network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var protocol=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);protocol.SetValue(network,Enum.Parse(protocol.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        bool began=false;long deadline=Core.TickCount+25000;
        void Fail(Exception e){t.Delete();p.Delete();Log.Error(e,"{Label}-result: failed",label);}
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"LifeTap input timeout");var session=ClassCombat.For(p);
                if(session.PendingAbility==CombatAbility.LifeTap&&!began){Require(NecromancerEffects.Curse(t)==null&&NecromancerEffects.ManaBalance256(p)==mana,"Input paid/applied during windup");began=true;Log.Information("{Label}: windup",label);}
                if(NecromancerEffects.Curse(t) is not{} curse){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Inspect);return;}
                Require(began&&curse.Rank==1&&NecromancerEffects.ManaBalance256(p)==mana-2304,"Real input capture/payment");ClassCombat.Stop(p);
                Require((curse.UntilUtc-TimedEffects.Now).TotalMilliseconds is >15900 and <=16000,"Rank1duration is not400frames");
                Timer.StartTimer(curse.UntilUtc-TimedEffects.Now+TimeSpan.FromMilliseconds(100),()=>
                {try{Require(NecromancerEffects.Curse(t)==null,"Natural rank1curse did not expire");Low(p);Require(NecromancerEffects.LifeTap(p,t,257)==0&&SkillHealth.Balance256(p)==2561,"Expired curse still healed");t.Delete();p.Delete();Log.Information("{Label}-result: passed",label);}catch(Exception e){Fail(e);}});
            }
            catch(Exception e){Fail(e);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.LifeTap,(int)DiabloClass.Necromancer,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
