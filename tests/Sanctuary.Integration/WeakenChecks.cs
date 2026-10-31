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

public static class WeakenChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(WeakenChecks));
    private sealed record Saved(uint Owner,uint Target,uint Legacy,NecromancerCurse Curse,NecromancerCurse Old,int Mana,int Map,Point3D Point,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"weaken-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("weaken-check",Check);Register("weaken-reload-check",Reload);
        foreach(bool quick in new[]{false,true})
        {bool captured=quick;string label=quick?"weaken-quick":"weaken-right";Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,captured)));}
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Weaken reference, learned casting and saved state",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static PlayerMobile Owner(int rank=1)
    {
        var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureWeaken";
        Require(ClassCreation.Profiles.GetRank(p.Serial,"necromancer.amplifydamage")>0||ClassCreation.Profiles.TryTrain(p.Serial,"necromancer.amplifydamage",out _),"Train prerequisite");
        while(ClassCreation.Profiles.GetRank(p.Serial,"necromancer.weaken")<rank)
        {Require(ClassCreation.Profiles.TryTrain(p.Serial,"necromancer.weaken",out _),"Train Weaken");}
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.Weaken)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Weaken),"Amplify right/quick bindings");
        FixtureSpace.Corridor(p,6400,24,3500);return p;
    }
    private static GraveyardSkeleton Enemy(PlayerMobile p,int x=1,int y=0)
    {var t=PoisonDaggerChecks.Target(p);t.MoveToWorld(new(p.X+x,p.Y+y,p.Z),p.Map);t.SetResistance(ResistanceType.Physical,0);return t;}
    private static void Ready(PlayerMobile p){PoisonDaggerChecks.Ready(p);ClassCombat.For(p).RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,p.ManaMax*256);}
    private static void Differential()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"weaken-source-cases.json")));
        DateTime at=DateTime.UnixEpoch.AddSeconds(4);int count=0;
        foreach(var r in doc.RootElement.GetProperty("receiver").EnumerateArray())
        {
            int oldRank=r[3].GetInt32(),rank=r[4].GetInt32(),other=r[5].GetInt32(),resistance=r[6].GetInt32(),attract=r[7].GetInt32();
            NecromancerCurse old=oldRank==0?null:new(other==1?CombatAbility.AmplifyDamage:CombatAbility.Weaken,999,oldRank,DateTime.UnixEpoch.AddMilliseconds(777*40),0,0,PhysicalDelta:other==1?-100:null);
            bool duration=SourceCurseDuration.Reduce(350+60*(rank-1),true,resistance,out int frames);
            var next=duration&&attract==0?NecromancerCurseCasts.Replacement(old,CombatAbility.Weaken,7,rank,null,at.AddMilliseconds(frames*40L),at,0,0):null;
            var current=next??old;int value=current?.Ability==CombatAbility.Weaken?new SkillValues(SkillMechanics.For(CombatAbility.Weaken),_=>current.Rank,_=>0,99).Linear(5,6):0;
            Require((next!=null)==(r[8].GetInt32()!=0)&&(current?.Rank??0)==r[9].GetInt32()&&(current?.Source??0)==r[10].GetUInt32()
                &&(current?.UntilUtc??DateTime.UnixEpoch)==DateTime.UnixEpoch.AddMilliseconds(r[11].GetInt32()*40L)&&value==r[12].GetInt32(),"Complete Weaken receiver/factory differential");count++;
        }
        Require(count==8960,"Incomplete receiver fixture");count=0;
        foreach(var r in doc.RootElement.GetProperty("damage").EnumerateArray())
        {
            int rank=r[0].GetInt32(),aura=r[1].GetInt32(),strength=r[2].GetInt32(),source=r[4].GetInt32();bool weapon=r[3].GetInt32()!=0,high=r[5].GetInt32()!=0;
            int weakened=new SkillValues(SkillMechanics.For(CombatAbility.Weaken),_=>rank,_=>0,99).Linear(5,6);int bound=0;
            int actual=NpcEquipmentDamage.Roll256(new(50,70,0,aura+weakened,strength,0,weapon,100),n=>{bound=n;return high?n-1:0;},source);
            Require(actual==r[6].GetInt32()&&bound==r[7].GetInt32(),"Weaken NPC additive/floor/stat/roll differential");count++;
        }
        Require(count==240,"Incomplete damage fixture");
        foreach(int rank in new[]{1,2,3,20,21,90,255})
        {var v=new SkillValues(SkillMechanics.For(CombatAbility.Weaken),_=>rank,_=>0,99);Require(v.Mana256==1024&&v.Eval("aurarangecalc")==rank+8&&v.Eval("auralencalc")==350+60*(rank-1)&&v.Linear(5,6)==-32-rank,"Pinned Weaken rank values");}
    }
    private static void ResetLife(PlayerMobile p)=>SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p));
    private static void Mechanics(PlayerMobile p,GraveyardSkeleton t)
    {
        foreach(int rank in new[]{1,20,90,255})
        {
            NecromancerEffects.Profiles.Clear(t);t.SetResistance(ResistanceType.Physical,rank==20?100:0);
            Require(NecromancerEffects.Apply(p,t,CombatAbility.Weaken,rank)&&NecromancerEffects.DamageStatPercent(t)==-32-rank&&NecromancerEffects.Slow(t)==0,"Weaken stat/immune/slow application");
            int percent=Math.Max(10,100-32-rank);ResetLife(p);int before=SkillHealth.Balance256(p);
            AOS.Damage(p,t,100,false,100,0,0,0,0,weaponDamage:true);
            Require(before-SkillHealth.Balance256(p)==percent*256,"Actual native weapon Weaken/floor consumer");
            ResetLife(p);before=SkillHealth.Balance256(p);AOS.Damage(p,t,100,false,50,50,0,0,0,weaponDamage:true);
            Require(before-SkillHealth.Balance256(p)==(50*percent*256/100+50*256),"Weaken changed native weapon fire component");
            ResetLife(p);before=SkillHealth.Balance256(p);AOS.Damage(p,t,100,false,100,0,0,0,0,spellDamage:true);
            Require(before-SkillHealth.Balance256(p)==100*256,"Weaken scaled a physical spell");
        }
        NecromancerEffects.Profiles.Clear(t);t.SetResistance(ResistanceType.Physical,0);var other=Owner();
        try
        {
            Require(NecromancerEffects.Apply(p,t,CombatAbility.Weaken,3),"Initial Weaken3");var first=NecromancerEffects.Curse(t) with{UntilUtc=TimedEffects.Now.AddMinutes(1)};NecromancerEffects.Profiles.Store(t,new(Curse:first));
            Require(!NecromancerEffects.Apply(other,t,CombatAbility.Weaken,1)&&NecromancerEffects.Curse(t)==first,"Weaker Weaken overwrote rank/source/deadline");
            Require(NecromancerEffects.Apply(other,t,CombatAbility.Weaken,3),"Equal Weaken refresh");var equal=NecromancerEffects.Curse(t);
            Require(equal.Source==p.Serial.Value&&equal.UntilUtc<first.UntilUtc&&equal.Rank==3&&equal.PhysicalDelta==null,"Equal refresh changed creator/stat representation");
            Require(NecromancerEffects.Apply(other,t,CombatAbility.Weaken,4)&&NecromancerEffects.Curse(t).Source==other.Serial.Value,"Stronger Weaken did not replace creator");
            other.Delete();Require(NecromancerEffects.DamageStatPercent(t)==-36,"Caster deletion removed Weaken");
        }
        finally{other.Delete();}
        Require(NecromancerEffects.Apply(p,t,CombatAbility.AmplifyDamage,1)&&NecromancerEffects.Curse(t).PhysicalDelta==-100,"Different ordinary curse replacement");
        Require(NecromancerEffects.Apply(p,t,CombatAbility.Weaken,1)&&NecromancerEffects.Curse(t).PhysicalDelta==null&&NecromancerEffects.Resistance(t,"physical",0)==0,"Weaken retained replaced Amplify resistance");
        Require(NecromancerEffects.Apply(p,t,CombatAbility.Attract,1)&&!NecromancerEffects.Apply(p,t,CombatAbility.Weaken,20),"Weaken overwrote Attract");NecromancerEffects.Profiles.Clear(t);
        var elite=new EncounterCreature{CantWalk=true};elite.Assign(EncounterProfiles.Profile("crypt-sentinel-60"),60,MonsterDifficulty.Normal,EncounterGrade.Ordinary,ChampionKind.Champion,[],EliteAura.None,null,new Random(173));
        elite.MoveToWorld(new(p.X,p.Y+1,p.Z),p.Map);elite.AIObject.AITimer.Stop();
        try
        {
            Require(NecromancerEffects.Apply(p,elite,CombatAbility.Weaken,1),"Native encounter Weaken");ResetLife(p);int before=SkillHealth.Balance256(p);
            EliteCombat.Hit(elite,p,100,ResistanceType.Physical,random:new Random(173));Require(before-SkillHealth.Balance256(p)==67*256,"Encounter physical weapon consumer");
            ResetLife(p);before=SkillHealth.Balance256(p);EliteCombat.Hit(elite,p,100,ResistanceType.Fire,random:new Random(173));Require(before-SkillHealth.Balance256(p)==100*256,"Encounter elemental attack was weakened");
        }
        finally{elite.Delete();}
        var possessed=new EncounterCreature{Frozen=true,CantWalk=true};possessed.Assign(EncounterProfiles.Profile("crypt-sentinel-60"),60,MonsterDifficulty.Normal,EncounterGrade.Champion,ChampionKind.Possessed,[],EliteAura.None,null,new Random(173));possessed.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);possessed.AIObject.AITimer.Stop();
        try{Require(!NecromancerEffects.Apply(p,possessed,CombatAbility.Weaken,1),"Possessed accepted Weaken");}finally{possessed.Delete();}
        ResetLife(p);
    }
    private static void Casting(PlayerMobile p)
    {
        GraveyardSkeleton near=Enemy(p,8),edge=Enemy(p,12,2),far=Enemy(p,13);var center=new Point3D(p.X+8,p.Y,p.Z);
        try
        {
            Ready(p);int mana=NecromancerEffects.ManaBalance256(p);
            Require(NecromancerCombat.Release(p,CombatAbility.Weaken,Serial.Zero,center),"Ground area release");
            Require(NecromancerEffects.ManaBalance256(p)==mana-1024&&NecromancerEffects.Curse(near)!=null&&NecromancerEffects.Curse(edge)!=null&&NecromancerEffects.Curse(far)==null,"Area center,radius or once-only payment");
            foreach(var t in new[]{near,edge,far}){NecromancerEffects.Profiles.Clear(t);}
            Ready(p);mana=NecromancerEffects.ManaBalance256(p);
            Require(ClassCombat.Perform(p,CombatAbility.Weaken,near,near.Location,Core.TickCount),"Targeted windup");
            Require(NecromancerEffects.ManaBalance256(p)==mana&&NecromancerEffects.Curse(near)==null,"Curse applied/paid during windup");
            near.MoveToWorld(new(p.X-1,p.Y,p.Z),p.Map);
            Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt)&&NecromancerEffects.Curse(near)!=null&&NecromancerEffects.Curse(edge)==null,"Release did not follow selected target position");
            Ready(p);NecromancerEffects.Profiles.Clear(near);mana=NecromancerEffects.ManaBalance256(p);
            Require(ClassCombat.Perform(p,CombatAbility.Weaken,null,center,Core.TickCount),"Cancelled windup");long effect=ClassCombat.For(p).EffectAt;
            var origin=p.Location;p.MoveToWorld(new(p.X,p.Y+1,p.Z),p.Map);Require(!ClassCombat.ResolveAction(p,effect)&&NecromancerEffects.ManaBalance256(p)==mana,"Movement cancellation spent mana");p.MoveToWorld(origin,p.Map);
            Ready(p);NecromancerEffects.SetMana256(p,1023);Require(!NecromancerCombat.Begin(p,CombatAbility.Weaken,Serial.Zero,center,Core.TickCount),"Insufficient mana began a cast");
            near.Delete();edge.Delete();far.Delete();Ready(p);mana=NecromancerEffects.ManaBalance256(p);Require(NecromancerCombat.Release(p,CombatAbility.Weaken,Serial.Zero,new(p.X+16,p.Y,p.Z))&&NecromancerEffects.ManaBalance256(p)==mana-1024,"Empty area did not pay once");
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
            Ready(p);Require(NecromancerCombat.Release(p,CombatAbility.Weaken,Serial.Zero,center)&&NecromancerEffects.Curse(t)!=null,"Filter3 gained an unrequested recipient LOS gate");
        }
        finally{wall.Delete();t.Delete();p.Delete();}
    }
    private static void Check()
    {
        Differential();var p=Owner();var t=Enemy(p);Mechanics(p,t);Casting(p);LineOfSight();
        while(ClassCreation.Profiles.GetRank(p.Serial,"necromancer.weaken")<20){Require(ClassCreation.Profiles.TryTrain(p.Serial,"necromancer.weaken",out _),"Train rank20");}
        PoisonDaggerChecks.Named(p,"Wormskull");Require(SkillMechanics.Rank(p,CombatAbility.Weaken)==21,"Real effective item rank");
        NecromancerEffects.Profiles.Clear(t);Require(NecromancerEffects.Apply(p,t,CombatAbility.Weaken,21),"Effective-rank curse");
        var curse=NecromancerEffects.Curse(t) with{UntilUtc=TimedEffects.Now.AddDays(1)};NecromancerEffects.Profiles.Store(t,new(Curse:curse));
        var legacy=Enemy(p,3);legacy.SetResistance(ResistanceType.Physical,100);var old=new NecromancerCurse(CombatAbility.Weaken,p.Serial.Value,1,TimedEffects.Now.AddDays(1),p.X,p.Y);
        Require(!JsonSerializer.Serialize(old).Contains("PhysicalDelta"),"Legacy nullable field was manufactured");NecromancerEffects.Profiles.Store(legacy,new(Curse:old));
        Ready(p);File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,t.Serial.Value,legacy.Serial.Value,curse,old,NecromancerEffects.ManaBalance256(p),p.Map.MapID,p.Location,0)));
        Log.Information("Weaken:8960receiver and240complete NPC damage comparisons,native AOS and encounter consumers,element/spell exclusion,rank/caster retention,area/payment/LOS/tracking/cancel and saved-state controls pass");
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var t=(BaseCreature)World.FindMobile((Serial)s.Target);var legacy=(BaseCreature)World.FindMobile((Serial)s.Legacy);
        Require(NecromancerEffects.Curse(t)==s.Curse&&NecromancerEffects.Curse(legacy)==s.Old&&NecromancerEffects.ManaBalance256(p)==s.Mana,"Reload altered captured/legacy state,mana or original deadline");
        Require(NecromancerEffects.Curse(legacy).PhysicalDelta==null&&NecromancerEffects.Resistance(legacy,"physical",100)==100&&NecromancerEffects.DamageStatPercent(legacy)==-33,"Legacy migration guessed a captured penalty");
        p.MoveToWorld(s.Point,Map.Maps[s.Map]);Require(SkillMechanics.Rank(p,CombatAbility.Weaken)==21&&SkillBindings.Instance.Get(p).Right==CombatAbility.Weaken,"Reload changed item rank or binding");
        int before=SkillHealth.Balance256(t);SkillCombat.Hit(p,t,10,"physical",recover:false);Require(before-SkillHealth.Balance256(t)==10*256,"Weaken altered incoming damage after reload");Require(NecromancerEffects.DamageStatPercent(t)==-53,"Reload lost effective-rank weapon penalty");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
    }
    private static void Input(NetState network,bool quick)
    {
        var p=Owner();var t=Enemy(p,6);Ready(p);int mana=NecromancerEffects.ManaBalance256(p);string label=quick?"weaken-quick":"weaken-right";
        network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var protocol=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);protocol.SetValue(network,Enum.Parse(protocol.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        bool began=false;long deadline=Core.TickCount+22000;
        void Fail(Exception e){t.Delete();p.Delete();Log.Error(e,"{Label}-result: failed",label);}
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Weaken input timeout");var session=ClassCombat.For(p);
                if(session.PendingAbility==CombatAbility.Weaken&&!began){Require(NecromancerEffects.Curse(t)==null&&NecromancerEffects.ManaBalance256(p)==mana,"Input paid/applied during windup");began=true;Log.Information("{Label}: windup",label);}
                if(NecromancerEffects.Curse(t) is not{} curse){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Inspect);return;}
                Require(began&&curse.PhysicalDelta==null&&NecromancerEffects.DamageStatPercent(t)==-33&&curse.Rank==1&&NecromancerEffects.ManaBalance256(p)==mana-1024,"Real input capture/payment");ClassCombat.Stop(p);
                Require((curse.UntilUtc-TimedEffects.Now).TotalMilliseconds is >13900 and <=14000,"Rank1duration is not350frames");
                Timer.StartTimer(curse.UntilUtc-TimedEffects.Now+TimeSpan.FromMilliseconds(100),()=>
                {try{Require(NecromancerEffects.Curse(t)==null,"Natural rank1curse did not expire");int before=SkillHealth.Balance256(t);SkillCombat.Hit(p,t,10,"physical",recover:false);Require(before-SkillHealth.Balance256(t)==10*256&&NecromancerEffects.DamageStatPercent(t)==0,"Expired curse still affected damage");t.Delete();p.Delete();Log.Information("{Label}-result: passed",label);}catch(Exception e){Fail(e);}});
            }
            catch(Exception e){Fail(e);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.Weaken,(int)DiabloClass.Necromancer,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
