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

public static class DecrepifyChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DecrepifyChecks));
    private sealed record Saved(uint Owner,uint Target,uint Legacy,NecromancerCurse Curse,NecromancerCurse Old,int Mana,int Map,Point3D Point,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"decrepify-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("decrepify-check",Check);Register("decrepify-reload-check",Reload);
        foreach(bool quick in new[]{false,true})
        {bool captured=quick;string label=quick?"decrepify-quick":"decrepify-right";Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,captured)));}
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Decrepify reference, learned casting and saved state",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static PlayerMobile Owner(int rank=1)
    {
        var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureDecrepify";
        void Train(string id,int wanted)
        {
            TalentCatalog.TryGet(DiabloClass.Necromancer,id,out var talent);
            foreach(string prior in talent.Prerequisites){if(ClassCreation.Profiles.GetRank(p.Serial,prior)==0){Train(prior,1);}}
            while(ClassCreation.Profiles.GetRank(p.Serial,id)<wanted){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}
        }
        Train("necromancer.decrepify",rank);
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.Decrepify)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Decrepify),"Amplify right/quick bindings");
        FixtureSpace.Corridor(p,6500,24,3500);return p;
    }
    private static GraveyardSkeleton Enemy(PlayerMobile p,int x=1,int y=0)
    {var t=PoisonDaggerChecks.Target(p);t.MoveToWorld(new(p.X+x,p.Y+y,p.Z),p.Map);t.SetResistance(ResistanceType.Physical,0);return t;}
    private static void Ready(PlayerMobile p){PoisonDaggerChecks.Ready(p);ClassCombat.For(p).RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,p.ManaMax*256);}
    private static void Differential()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"decrepify-source-cases.json")));
        DateTime at=DateTime.UnixEpoch.AddSeconds(4);int count=0;
        foreach(var r in doc.RootElement.EnumerateArray())
        {
            int type=r[0].GetInt32(),hire=r[1].GetInt32(),basis=r[2].GetInt32(),oldRank=r[3].GetInt32(),rank=r[4].GetInt32(),other=r[5].GetInt32(),resistance=r[6].GetInt32(),attract=r[7].GetInt32();
            NecromancerCurse old=oldRank==0?null:new(other==1?CombatAbility.Weaken:CombatAbility.Decrepify,999,oldRank,DateTime.UnixEpoch.AddMilliseconds(777*40),0,0,PhysicalDelta:other==1?null:basis>=100?-50:-10);
            bool duration=SourceCurseDuration.Reduce(100+15*(rank-1),true,resistance,out int frames);
            var next=duration&&attract==0?NecromancerCurseCasts.Replacement(old,CombatAbility.Decrepify,7,rank,NecromancerCurseCasts.PhysicalPenalty(-50,basis,type==1&&hire==0),at.AddMilliseconds(frames*40L),at,0,0):null;
            var current=next??old;bool decrep=current?.Ability==CombatAbility.Decrepify;
            int damage=decrep?-50:current?.Ability==CombatAbility.Weaken?-32-current.Rank:0;
            Require((next!=null)==(r[8].GetInt32()!=0)&&(current?.Rank??0)==r[9].GetInt32()&&(current?.Source??0)==r[10].GetUInt32()
                &&(current?.UntilUtc??DateTime.UnixEpoch)==DateTime.UnixEpoch.AddMilliseconds(r[11].GetInt32()*40L)
                &&(decrep?-50:0)==r[12].GetInt32()&&damage==r[13].GetInt32()&&(current?.PhysicalDelta??0)==r[14].GetInt32()&&(decrep?-50:0)==r[15].GetInt32(),"Four-stat source receiver/factory differential");count++;
        }
        Require(count==8960,"Incomplete Decrepify fixture");
        foreach(int rank in new[]{1,2,3,20,21,255})
        {var v=new SkillValues(SkillMechanics.For(CombatAbility.Decrepify),_=>rank,_=>0,99);Require(v.Mana256==2816&&v.Eval("aurarangecalc")==6&&v.Eval("auralencalc")==100+15*(rank-1)&&v.Param(5)==-50,"Pinned Decrepify rank values");}
        Require(NecromancerEffects.ValidPhysicalDelta(CombatAbility.Decrepify,-50)&&NecromancerEffects.ValidPhysicalDelta(CombatAbility.Decrepify,-10)&&!NecromancerEffects.ValidPhysicalDelta(CombatAbility.Decrepify,-100),"Captured Decrepify save domain");
    }
    private static void ResetLife(PlayerMobile p)=>SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p));
    private static void Mechanics(PlayerMobile p,GraveyardSkeleton t)
    {
        foreach(int basis in new[]{0,99,100,109,110,200})
        {
            NecromancerEffects.Profiles.Clear(t);t.SetResistance(ResistanceType.Physical,basis);t.SetResistance(ResistanceType.Cold,100);
            Require(NecromancerEffects.Apply(p,t,CombatAbility.Decrepify,1),"Decrepify application");int delta=basis>=100?-10:-50;
            Require(NecromancerEffects.Curse(t).PhysicalDelta==delta&&NecromancerEffects.DamageStatPercent(t)==-50&&GearEffects.MovementSlow(t)==50&&GearEffects.AttackSlow(t)==50,"Four stats or cold/physical immunity distinction");
            int before=SkillHealth.Balance256(t);SkillCombat.Hit(p,t,100,"physical",recover:false);
            Require(before-SkillHealth.Balance256(t)==Math.Clamp(100-basis-delta,0,200)*256,"Physical immunity break boundary");
            before=SkillHealth.Balance256(t);SkillCombat.Hit(p,t,10,"fire",recover:false);Require(before-SkillHealth.Balance256(t)==10*256,"Decrepify changed incoming fire resistance");
        }
        Require(Math.Abs(ClassGearHooks.MovementDelay(t,.4)-.8)<.000001&&ClassGearHooks.AttackDelay(t,TimeSpan.FromMilliseconds(400)).TotalMilliseconds==800,"Native movement/attack delay hooks did not halve speed");
        Require(SourceAttackRate.WolfRate(t)==50&&SourceAttackRate.Speed(256,0,SourceAttackRate.WolfRate(t))==128,"Shared source attack-rate consumer");
        t.SetResistance(ResistanceType.Physical,0);ResetLife(p);int life=SkillHealth.Balance256(p);AOS.Damage(p,t,100,false,100,0,0,0,0,weaponDamage:true);Require(life-SkillHealth.Balance256(p)==50*256,"Native outgoing weapon damage");
        ResetLife(p);life=SkillHealth.Balance256(p);AOS.Damage(p,t,100,false,50,50,0,0,0,weaponDamage:true);Require(life-SkillHealth.Balance256(p)==75*256,"Decrepify scaled weapon fire damage");
        ResetLife(p);life=SkillHealth.Balance256(p);AOS.Damage(p,t,100,false,100,0,0,0,0,spellDamage:true);Require(life-SkillHealth.Balance256(p)==100*256,"Decrepify scaled a physical spell");
        // Observe a real ordinary NPC windup; retry its native stall decision.
        var timer=typeof(GraveyardUndead).GetField("_actionTimer",BindingFlags.Instance|BindingFlags.NonPublic);var melee=typeof(GraveyardUndead).GetField("_melee",BindingFlags.Instance|BindingFlags.NonPublic);bool started=false;t.Frozen=false;
        try
        {
            Require(t.NormalStats.Attack1.Speed==t.NormalStats.Attack2.Speed,"Independent attack-speed fixture");
            for(int i=0;i<30&&!started;i++)
            {
                t.StopCombatActions();t.BeginMelee(p,Core.TickCount+i*10000L+10000);
                if(((TimerExecutionToken)timer.GetValue(t)).Running)
                {var held=(MonsterMelee)melee.GetValue(t);Require(held.Speed==t.NormalStats.Attack1.Speed/2,"Actual NPC windup did not consume Decrepify attackrate");started=true;}
            }
            Require(started,"Native stall decisions never reached an attack");
        }
        finally{t.StopCombatActions();t.Frozen=true;}
        NecromancerEffects.Profiles.Clear(t);var other=Owner();
        try
        {
            Require(NecromancerEffects.Apply(p,t,CombatAbility.Decrepify,3),"Initial Decrepify3");var first=NecromancerEffects.Curse(t) with{UntilUtc=TimedEffects.Now.AddMinutes(1)};NecromancerEffects.Profiles.Store(t,new(Curse:first));t.SetResistance(ResistanceType.Physical,120);
            Require(!NecromancerEffects.Apply(other,t,CombatAbility.Decrepify,1)&&NecromancerEffects.Curse(t)==first,"Weaker rank rewrote state");
            Require(NecromancerEffects.Apply(other,t,CombatAbility.Decrepify,3),"Equal Decrepify");var equal=NecromancerEffects.Curse(t);
            Require(equal.Source==p.Serial.Value&&equal.PhysicalDelta==-10&&equal.UntilUtc<first.UntilUtc,"Equal refresh must keep caster and rewrite secondary resistance");
            t.SetResistance(ResistanceType.Physical,0);Require(NecromancerEffects.Apply(other,t,CombatAbility.Decrepify,3)&&NecromancerEffects.Curse(t).PhysicalDelta==-50&&NecromancerEffects.Curse(t).Source==p.Serial.Value,"Reverse equal-rank secondary rewrite");
            Require(NecromancerEffects.Apply(other,t,CombatAbility.Decrepify,4)&&NecromancerEffects.Curse(t).Source==other.Serial.Value,"Higher rank did not replace caster");other.Delete();Require(NecromancerEffects.Slow(t)==50,"Caster deletion erased Decrepify");
        }
        finally{other.Delete();}
        Require(NecromancerEffects.Apply(p,t,CombatAbility.Weaken,1)&&NecromancerEffects.Curse(t).PhysicalDelta==null&&NecromancerEffects.Slow(t)==0,"Different curse retained Decrepify fields");
        Require(NecromancerEffects.Apply(p,t,CombatAbility.Attract,1)&&!NecromancerEffects.Apply(p,t,CombatAbility.Decrepify,20),"Decrepify overwrote Attract");NecromancerEffects.Profiles.Clear(t);
        var possessed=new EncounterCreature{Frozen=true,CantWalk=true};possessed.Assign(EncounterProfiles.Profile("crypt-sentinel-60"),60,MonsterDifficulty.Normal,EncounterGrade.Champion,ChampionKind.Possessed,[],EliteAura.None,null,new Random(173));possessed.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);possessed.AIObject.AITimer.Stop();
        try{Require(!NecromancerEffects.Apply(p,possessed,CombatAbility.Decrepify,1),"Possessed accepted Decrepify");}finally{possessed.Delete();}
        ResetLife(p);t.SetResistance(ResistanceType.Physical,0);
    }
    private static void Casting(PlayerMobile p)
    {
        GraveyardSkeleton near=Enemy(p,8),edge=Enemy(p,11),far=Enemy(p,11,1);var center=new Point3D(p.X+8,p.Y,p.Z);
        try
        {
            Ready(p);int mana=NecromancerEffects.ManaBalance256(p);
            Require(NecromancerCombat.Release(p,CombatAbility.Decrepify,Serial.Zero,center),"Ground area release");
            Require(NecromancerEffects.ManaBalance256(p)==mana-2816&&NecromancerEffects.Curse(near)!=null&&NecromancerEffects.Curse(edge)!=null&&NecromancerEffects.Curse(far)==null,"Area center,radius or once-only payment");
            foreach(var t in new[]{near,edge,far}){NecromancerEffects.Profiles.Clear(t);}
            Ready(p);mana=NecromancerEffects.ManaBalance256(p);
            Require(ClassCombat.Perform(p,CombatAbility.Decrepify,near,near.Location,Core.TickCount),"Targeted windup");
            Require(NecromancerEffects.ManaBalance256(p)==mana&&NecromancerEffects.Curse(near)==null,"Curse applied/paid during windup");
            near.MoveToWorld(new(p.X-1,p.Y,p.Z),p.Map);
            Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt)&&NecromancerEffects.Curse(near)!=null&&NecromancerEffects.Curse(edge)==null,"Release did not follow selected target position");
            Ready(p);NecromancerEffects.Profiles.Clear(near);mana=NecromancerEffects.ManaBalance256(p);
            Require(ClassCombat.Perform(p,CombatAbility.Decrepify,null,center,Core.TickCount),"Cancelled windup");long effect=ClassCombat.For(p).EffectAt;
            var origin=p.Location;p.MoveToWorld(new(p.X,p.Y+1,p.Z),p.Map);Require(!ClassCombat.ResolveAction(p,effect)&&NecromancerEffects.ManaBalance256(p)==mana,"Movement cancellation spent mana");p.MoveToWorld(origin,p.Map);
            Ready(p);NecromancerEffects.SetMana256(p,2815);Require(!NecromancerCombat.Begin(p,CombatAbility.Decrepify,Serial.Zero,center,Core.TickCount),"Insufficient mana began a cast");
            near.Delete();edge.Delete();far.Delete();Ready(p);mana=NecromancerEffects.ManaBalance256(p);Require(NecromancerCombat.Release(p,CombatAbility.Decrepify,Serial.Zero,new(p.X+16,p.Y,p.Z))&&NecromancerEffects.ManaBalance256(p)==mana-2816,"Empty area did not pay once");
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
            Ready(p);Require(NecromancerCombat.Release(p,CombatAbility.Decrepify,Serial.Zero,center)&&NecromancerEffects.Curse(t)!=null,"Filter3 gained an unrequested recipient LOS gate");
        }
        finally{wall.Delete();t.Delete();p.Delete();}
    }
    private static void Check()
    {
        Differential();var p=Owner();var t=Enemy(p);Mechanics(p,t);Casting(p);LineOfSight();
        while(ClassCreation.Profiles.GetRank(p.Serial,"necromancer.decrepify")<20){Require(ClassCreation.Profiles.TryTrain(p.Serial,"necromancer.decrepify",out _),"Train rank20");}
        PoisonDaggerChecks.Named(p,"Wormskull");Require(SkillMechanics.Rank(p,CombatAbility.Decrepify)==21,"Real effective item rank");
        NecromancerEffects.Profiles.Clear(t);Require(NecromancerEffects.Apply(p,t,CombatAbility.Decrepify,21),"Effective-rank curse");
        var curse=NecromancerEffects.Curse(t) with{UntilUtc=TimedEffects.Now.AddDays(1)};NecromancerEffects.Profiles.Store(t,new(Curse:curse));
        var legacy=Enemy(p,3);legacy.SetResistance(ResistanceType.Physical,100);var old=new NecromancerCurse(CombatAbility.Decrepify,p.Serial.Value,1,TimedEffects.Now.AddDays(1),p.X,p.Y);
        Require(!JsonSerializer.Serialize(old).Contains("PhysicalDelta"),"Legacy nullable field was manufactured");NecromancerEffects.Profiles.Store(legacy,new(Curse:old));
        Ready(p);File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,t.Serial.Value,legacy.Serial.Value,curse,old,NecromancerEffects.ManaBalance256(p),p.Map.MapID,p.Location,0)));
        Log.Information("Decrepify:8960four-stat comparisons,native damage/speed/windup,immune boundaries,secondary refresh,rank/caster retention,area/payment/LOS/tracking/cancel and saved-state controls pass");
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var t=(BaseCreature)World.FindMobile((Serial)s.Target);var legacy=(BaseCreature)World.FindMobile((Serial)s.Legacy);
        Require(NecromancerEffects.Curse(t)==s.Curse&&NecromancerEffects.Curse(legacy)==s.Old&&NecromancerEffects.ManaBalance256(p)==s.Mana,"Reload altered captured/legacy state,mana or original deadline");
        Require(NecromancerEffects.Curse(legacy).PhysicalDelta==null&&NecromancerEffects.Resistance(legacy,"physical",100)==90&&NecromancerEffects.DamageStatPercent(legacy)==-50,"Legacy migration guessed a captured penalty");
        p.MoveToWorld(s.Point,Map.Maps[s.Map]);Require(SkillMechanics.Rank(p,CombatAbility.Decrepify)==21&&SkillBindings.Instance.Get(p).Right==CombatAbility.Decrepify,"Reload changed item rank or binding");
        int before=SkillHealth.Balance256(t);SkillCombat.Hit(p,t,10,"physical",recover:false);Require(before-SkillHealth.Balance256(t)==15*256,"Restored Decrepify resistance penalty");Require(NecromancerEffects.DamageStatPercent(t)==-50,"Reload lost effective-rank weapon penalty");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
    }
    private static void Input(NetState network,bool quick)
    {
        var p=Owner();var t=Enemy(p,6);Ready(p);int mana=NecromancerEffects.ManaBalance256(p);string label=quick?"decrepify-quick":"decrepify-right";
        network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var protocol=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);protocol.SetValue(network,Enum.Parse(protocol.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        bool began=false;long deadline=Core.TickCount+12000;
        void Fail(Exception e){t.Delete();p.Delete();Log.Error(e,"{Label}-result: failed",label);}
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Decrepify input timeout");var session=ClassCombat.For(p);
                if(session.PendingAbility==CombatAbility.Decrepify&&!began){Require(NecromancerEffects.Curse(t)==null&&NecromancerEffects.ManaBalance256(p)==mana,"Input paid/applied during windup");began=true;Log.Information("{Label}: windup",label);}
                if(NecromancerEffects.Curse(t) is not{} curse){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Inspect);return;}
                Require(began&&curse.PhysicalDelta==-50&&NecromancerEffects.DamageStatPercent(t)==-50&&curse.Rank==1&&NecromancerEffects.ManaBalance256(p)==mana-2816,"Real input capture/payment");ClassCombat.Stop(p);
                Require((curse.UntilUtc-TimedEffects.Now).TotalMilliseconds is >3900 and <=4000,"Rank1duration is not100frames");
                Timer.StartTimer(curse.UntilUtc-TimedEffects.Now+TimeSpan.FromMilliseconds(100),()=>
                {try{Require(NecromancerEffects.Curse(t)==null,"Natural rank1curse did not expire");int before=SkillHealth.Balance256(t);SkillCombat.Hit(p,t,10,"physical",recover:false);Require(before-SkillHealth.Balance256(t)==10*256&&NecromancerEffects.DamageStatPercent(t)==0,"Expired curse still affected damage");t.Delete();p.Delete();Log.Information("{Label}-result: passed",label);}catch(Exception e){Fail(e);}});
            }
            catch(Exception e){Fail(e);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.Decrepify,(int)DiabloClass.Necromancer,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
