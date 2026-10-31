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

public static class AmplifyDamageChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(AmplifyDamageChecks));
    private sealed record Saved(uint Owner,uint Target,uint Legacy,NecromancerCurse Curse,NecromancerCurse Old,int Mana,int Map,Point3D Point,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"amplify-damage-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("amplify-damage-check",Check);Register("amplify-damage-reload-check",Reload);
        foreach(bool quick in new[]{false,true})
        {bool captured=quick;string label=quick?"amplify-damage-quick":"amplify-damage-right";Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,captured)));}
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Amplify Damage reference, learned casting and saved state",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static PlayerMobile Owner(int rank=1)
    {
        var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureAmplify";
        while(ClassCreation.Profiles.GetRank(p.Serial,"necromancer.amplifydamage")<rank)
        {Require(ClassCreation.Profiles.TryTrain(p.Serial,"necromancer.amplifydamage",out _),"Train Amplify Damage");}
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.AmplifyDamage)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.AmplifyDamage),"Amplify right/quick bindings");
        FixtureSpace.Corridor(p,6300,24,3500);return p;
    }
    private static GraveyardSkeleton Enemy(PlayerMobile p,int x=1,int y=0)
    {var t=PoisonDaggerChecks.Target(p);t.MoveToWorld(new(p.X+x,p.Y+y,p.Z),p.Map);t.SetResistance(ResistanceType.Physical,0);return t;}
    private static void Ready(PlayerMobile p){PoisonDaggerChecks.Ready(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);}
    private static void Differential()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"amplify-damage-source-cases.json")));
        DateTime at=DateTime.UnixEpoch.AddSeconds(4);int count=0;
        foreach(var r in doc.RootElement.GetProperty("receiver").EnumerateArray())
        {
            int type=r[0].GetInt32(),hire=r[1].GetInt32(),basis=r[2].GetInt32(),oldRank=r[3].GetInt32(),rank=r[4].GetInt32(),other=r[5].GetInt32(),resistance=r[6].GetInt32(),attract=r[7].GetInt32();
            // The arbitrary -33 old stat is a source-service input exercising
            // equal-rank retention, not a valid new pinned Amplify save value.
            NecromancerCurse old=oldRank==0?null:new(other==1?CombatAbility.Decrepify:CombatAbility.AmplifyDamage,999,oldRank,DateTime.UnixEpoch.AddMilliseconds(777*40),0,0,PhysicalDelta:-33);
            bool duration=SourceCurseDuration.Reduce(200+75*(rank-1),true,resistance,out int frames);
            var next=duration&&attract==0?AmplifyDamageCasts.Replacement(old,7,rank,AmplifyDamageCasts.Penalty(basis,type==1&&hire==0),at.AddMilliseconds(frames*40L),at,0,0):null;
            var current=next??old;
            Require((next!=null)==(r[8].GetInt32()!=0)&&(current?.Rank??0)==r[9].GetInt32()&&(current?.Source??0)==r[10].GetUInt32()
                &&(current?.UntilUtc??DateTime.UnixEpoch)==DateTime.UnixEpoch.AddMilliseconds(r[11].GetInt32()*40L)
                &&(current?.PhysicalDelta??0)==r[12].GetInt32(),"Complete source receiver/policy differential");count++;
        }
        Require(count==8960&&doc.RootElement.GetProperty("gates").GetArrayLength()==1024,"Incomplete Amplify source fixture");
        foreach(int rank in new[]{1,2,3,20,21,255})
        {var v=new SkillValues(SkillMechanics.For(CombatAbility.AmplifyDamage),_=>rank,_=>0,99);Require(v.Mana256==1024&&v.Eval("aurarangecalc")==rank+2&&v.Eval("auralencalc")==200+75*(rank-1),"Pinned rank values");}
        Require(!AmplifyDamageCasts.WalkMode("baaltentacle1")&&AmplifyDamageCasts.WalkMode("skeleton1"),"Pinned walk-mode rules");
    }
    private static void Mechanics(PlayerMobile p,GraveyardSkeleton t)
    {
        foreach(int basis in new[]{0,99,100,119,120,200})
        {
            NecromancerEffects.Profiles.Clear(t);t.SetResistance(ResistanceType.Physical,basis);int before=SkillHealth.Balance256(t);
            Require(NecromancerEffects.Apply(p,t,CombatAbility.AmplifyDamage,1),"Native Amplify application");int delta=basis>=100?-20:-100;
            Require(NecromancerEffects.Curse(t).PhysicalDelta==delta,"Wrong captured immunity penalty");
            SkillCombat.Hit(p,t,100,"physical",recover:false);int expected=Math.Clamp(100-basis-delta,0,200)*256;
            Require(before-SkillHealth.Balance256(t)==expected,"Native physical damage/resistance or immunity boundary");
            int after=SkillHealth.Balance256(t);SkillCombat.Hit(p,t,10,"fire",recover:false);
            Require(after-SkillHealth.Balance256(t)==10*256,"Amplify altered another element");
        }
        NecromancerEffects.Profiles.Clear(t);t.SetResistance(ResistanceType.Physical,0);
        var other=Owner();
        try
        {
            Require(NecromancerEffects.Apply(p,t,CombatAbility.AmplifyDamage,3),"Initial rank3");var first=NecromancerEffects.Curse(t) with{UntilUtc=TimedEffects.Now.AddMinutes(1)};NecromancerEffects.Profiles.Store(t,new(Curse:first));
            Require(!NecromancerEffects.Apply(other,t,CombatAbility.AmplifyDamage,1)&&NecromancerEffects.Curse(t)==first,"Weaker caster overwrote rank/source/deadline");
            t.SetResistance(ResistanceType.Physical,120);
            Require(NecromancerEffects.Apply(other,t,CombatAbility.AmplifyDamage,3),"Equal-rank refresh");var equal=NecromancerEffects.Curse(t);
            Require(equal.Source==p.Serial.Value&&equal.PhysicalDelta==-100&&equal.Rank==3&&equal.UntilUtc<first.UntilUtc,"Equal refresh changed original owner/stat");
            Require(NecromancerEffects.Apply(other,t,CombatAbility.AmplifyDamage,4)&&NecromancerEffects.Curse(t) is{Rank:4,PhysicalDelta:-20} next&&next.Source==other.Serial.Value,"Stronger curse did not replace captured stat/caster");
            other.Delete();Require(NecromancerEffects.Curse(t)?.Rank==4,"Caster removal erased an existing curse");
        }
        finally{other.Delete();}
        NecromancerEffects.Profiles.Clear(t);t.SetResistance(ResistanceType.Physical,0);
        Require(NecromancerEffects.Apply(p,t,CombatAbility.Attract,1)&&!NecromancerEffects.Apply(p,t,CombatAbility.AmplifyDamage,20),"Attract overwritten");
        NecromancerEffects.Profiles.Clear(t);
        var possessed=new EncounterCreature{Frozen=true,CantWalk=true};possessed.Assign(EncounterProfiles.Profile("crypt-sentinel-60"),60,MonsterDifficulty.Normal,EncounterGrade.Champion,ChampionKind.Possessed,[],EliteAura.None,null,new Random(173));
        possessed.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);possessed.AIObject.AITimer.Stop();
        try{Require(!NecromancerEffects.Apply(p,possessed,CombatAbility.AmplifyDamage,1),"Possessed champion accepted curse");}finally{possessed.Delete();}
    }
    private static void Casting(PlayerMobile p)
    {
        GraveyardSkeleton near=Enemy(p,8),edge=Enemy(p,9,1),far=Enemy(p,10);var center=new Point3D(p.X+8,p.Y,p.Z);
        try
        {
            Ready(p);int mana=NecromancerEffects.ManaBalance256(p);
            Require(NecromancerCombat.Release(p,CombatAbility.AmplifyDamage,Serial.Zero,center),"Ground area release");
            Require(NecromancerEffects.ManaBalance256(p)==mana-1024&&NecromancerEffects.Curse(near)!=null&&NecromancerEffects.Curse(edge)!=null&&NecromancerEffects.Curse(far)==null,"Area center,radius or once-only payment");
            foreach(var t in new[]{near,edge,far}){NecromancerEffects.Profiles.Clear(t);}
            Ready(p);mana=NecromancerEffects.ManaBalance256(p);
            Require(ClassCombat.Perform(p,CombatAbility.AmplifyDamage,near,near.Location,Core.TickCount),"Targeted windup");
            Require(NecromancerEffects.ManaBalance256(p)==mana&&NecromancerEffects.Curse(near)==null,"Curse applied/paid during windup");
            near.MoveToWorld(new(p.X+14,p.Y,p.Z),p.Map);
            Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt)&&NecromancerEffects.Curse(near)!=null&&NecromancerEffects.Curse(edge)==null,"Release did not follow selected target position");
            Ready(p);NecromancerEffects.Profiles.Clear(near);mana=NecromancerEffects.ManaBalance256(p);
            Require(ClassCombat.Perform(p,CombatAbility.AmplifyDamage,null,center,Core.TickCount),"Cancelled windup");long effect=ClassCombat.For(p).EffectAt;
            var origin=p.Location;p.MoveToWorld(new(p.X,p.Y+1,p.Z),p.Map);Require(!ClassCombat.ResolveAction(p,effect)&&NecromancerEffects.ManaBalance256(p)==mana,"Movement cancellation spent mana");p.MoveToWorld(origin,p.Map);
            Ready(p);NecromancerEffects.SetMana256(p,1023);Require(!NecromancerCombat.Begin(p,CombatAbility.AmplifyDamage,Serial.Zero,center,Core.TickCount),"Insufficient mana began a cast");
            Ready(p);mana=NecromancerEffects.ManaBalance256(p);Require(NecromancerCombat.Release(p,CombatAbility.AmplifyDamage,Serial.Zero,new(p.X-3,p.Y,p.Z))&&NecromancerEffects.ManaBalance256(p)==mana-1024,"Empty area did not pay once");
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
            Ready(p);Require(NecromancerCombat.Release(p,CombatAbility.AmplifyDamage,Serial.Zero,center)&&NecromancerEffects.Curse(t)!=null,"Filter3 gained an unrequested recipient LOS gate");
        }
        finally{wall.Delete();t.Delete();p.Delete();}
    }
    private static void Check()
    {
        Differential();var p=Owner();var t=Enemy(p);Mechanics(p,t);Casting(p);LineOfSight();
        while(ClassCreation.Profiles.GetRank(p.Serial,"necromancer.amplifydamage")<20){Require(ClassCreation.Profiles.TryTrain(p.Serial,"necromancer.amplifydamage",out _),"Train rank20");}
        PoisonDaggerChecks.Named(p,"Wormskull");Require(SkillMechanics.Rank(p,CombatAbility.AmplifyDamage)==21,"Real effective item rank");
        NecromancerEffects.Profiles.Clear(t);Require(NecromancerEffects.Apply(p,t,CombatAbility.AmplifyDamage,21),"Effective-rank curse");
        var curse=NecromancerEffects.Curse(t) with{UntilUtc=TimedEffects.Now.AddDays(1)};NecromancerEffects.Profiles.Store(t,new(Curse:curse));
        var legacy=Enemy(p,3);legacy.SetResistance(ResistanceType.Physical,100);var old=new NecromancerCurse(CombatAbility.AmplifyDamage,p.Serial.Value,1,TimedEffects.Now.AddDays(1),p.X,p.Y);
        Require(!JsonSerializer.Serialize(old).Contains("PhysicalDelta"),"Legacy nullable field was manufactured");NecromancerEffects.Profiles.Store(legacy,new(Curse:old));
        Ready(p);File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,t.Serial.Value,legacy.Serial.Value,curse,old,NecromancerEffects.ManaBalance256(p),p.Map.MapID,p.Location,0)));
        Log.Information("Amplify Damage:8960receiver differentials,pinned ranks,immunity boundaries,captured stats,rank/caster retention,Attract/Possessed,area/payment/moving target/cancel and legacy payload controls pass");
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var t=(BaseCreature)World.FindMobile((Serial)s.Target);var legacy=(BaseCreature)World.FindMobile((Serial)s.Legacy);
        Require(NecromancerEffects.Curse(t)==s.Curse&&NecromancerEffects.Curse(legacy)==s.Old&&NecromancerEffects.ManaBalance256(p)==s.Mana,"Reload altered captured/legacy state,mana or original deadline");
        Require(NecromancerEffects.Curse(legacy).PhysicalDelta==null&&NecromancerEffects.Resistance(legacy,"physical",100)==80,"Legacy migration guessed a captured penalty");
        p.MoveToWorld(s.Point,Map.Maps[s.Map]);Require(SkillMechanics.Rank(p,CombatAbility.AmplifyDamage)==21&&SkillBindings.Instance.Get(p).Right==CombatAbility.AmplifyDamage,"Reload changed item rank or binding");
        int before=SkillHealth.Balance256(t);SkillCombat.Hit(p,t,10,"physical",recover:false);Require(before-SkillHealth.Balance256(t)==20*256,"Restored captured curse did not mitigate damage");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
    }
    private static void Input(NetState network,bool quick)
    {
        var p=Owner();var t=Enemy(p,6);Ready(p);int mana=NecromancerEffects.ManaBalance256(p);string label=quick?"amplify-damage-quick":"amplify-damage-right";
        network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var protocol=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);protocol.SetValue(network,Enum.Parse(protocol.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        bool began=false;long deadline=Core.TickCount+15000;
        void Fail(Exception e){t.Delete();p.Delete();Log.Error(e,"{Label}-result: failed",label);}
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Amplify input timeout");var session=ClassCombat.For(p);
                if(session.PendingAbility==CombatAbility.AmplifyDamage&&!began){Require(NecromancerEffects.Curse(t)==null&&NecromancerEffects.ManaBalance256(p)==mana,"Input paid/applied during windup");began=true;Log.Information("{Label}: windup",label);}
                if(NecromancerEffects.Curse(t) is not{} curse){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Inspect);return;}
                Require(began&&curse.PhysicalDelta==-100&&curse.Rank==1&&NecromancerEffects.ManaBalance256(p)==mana-1024,"Real input capture/payment");ClassCombat.Stop(p);
                Require((curse.UntilUtc-TimedEffects.Now).TotalMilliseconds is >7900 and <=8000,"Rank1duration is not200frames");
                Timer.StartTimer(curse.UntilUtc-TimedEffects.Now+TimeSpan.FromMilliseconds(100),()=>
                {try{Require(NecromancerEffects.Curse(t)==null,"Natural rank1curse did not expire");int before=SkillHealth.Balance256(t);SkillCombat.Hit(p,t,10,"physical",recover:false);Require(before-SkillHealth.Balance256(t)==10*256,"Expired curse still affected damage");t.Delete();p.Delete();Log.Information("{Label}-result: passed",label);}catch(Exception e){Fail(e);}});
            }
            catch(Exception e){Fail(e);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.AmplifyDamage,(int)DiabloClass.Necromancer,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
