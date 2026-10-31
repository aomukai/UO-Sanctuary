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

public static class IronMaidenChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(IronMaidenChecks));
    private sealed record Saved(uint Owner,uint Target,uint Legacy,NecromancerCurse Curse,NecromancerCurse Old,int Mana,int Map,Point3D Point,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"iron-maiden-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("iron-maiden-check",Check);Register("iron-maiden-reload-check",Reload);
        foreach(bool quick in new[]{false,true})
        {bool captured=quick;string label=quick?"iron-maiden-quick":"iron-maiden-right";Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,captured)));}
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"IronMaiden reference, learned casting and saved state",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static PlayerMobile Owner(int rank=1)
    {
        var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureIronMaiden";
        void Train(string id,int wanted)
        {
            TalentCatalog.TryGet(DiabloClass.Necromancer,id,out var talent);
            foreach(string prior in talent.Prerequisites){if(ClassCreation.Profiles.GetRank(p.Serial,prior)==0){Train(prior,1);}}
            while(ClassCreation.Profiles.GetRank(p.Serial,id)<wanted){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}
        }
        Train("necromancer.ironmaiden",rank);
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.IronMaiden)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.IronMaiden),"Lower Resist right/quick bindings");
        FixtureSpace.Corridor(p,6700,24,3500);return p;
    }
    private static GraveyardSkeleton Enemy(PlayerMobile p,int x=1,int y=0)
    {var t=PoisonDaggerChecks.Target(p);t.MoveToWorld(new(p.X+x,p.Y+y,p.Z),p.Map);foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Energy,ResistanceType.Poison}){t.SetResistance(type,0);}return t;}
    private static void Ready(PlayerMobile p){PoisonDaggerChecks.Ready(p);ClassCombat.For(p).RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,p.ManaMax*256);}
    private static void Differential()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"iron-maiden-source-cases.json")));int ordinary=0,tail=0,compared=0;
        foreach(var r in doc.RootElement.EnumerateArray())
        {
            if(r[0].GetInt32()==1){tail++;continue;}ordinary++;
            var i=r[1];var o=r[2];int rank=i[4].GetInt32();
            if(o[0].GetInt32()==0||rank==0){continue;}
            rank=Math.Max(1,rank);int percent=IronMaiden.Percent(rank,i[0].GetInt32()==0||i[1].GetInt32()!=0);
            Require(IronMaiden.Returned256(i[3].GetInt32(),percent)==o[4].GetInt32(),"Complete event fractional percentage differential");compared++;
        }
        Require((ordinary,tail,compared)==(8640,15552,1008),"Incomplete Iron Maiden source fixture");
        foreach(int rank in new[]{1,2,3,20,21,255})
        {
            var v=new SkillValues(SkillMechanics.For(CombatAbility.IronMaiden),_=>rank,_=>0,99);
            Require(v.Mana256==1280&&v.Eval("aurarangecalc")==7&&v.Eval("auralencalc")==300+60*(rank-1)&&IronMaiden.Percent(rank,false)==200+25*(rank-1)&&IronMaiden.Percent(rank,true)==(200+25*(rank-1))/4,"Pinned Iron Maiden ranks");
        }
        using var receiver=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"iron-maiden-receiver-cases.json")));int receivers=0;DateTime at=DateTime.UnixEpoch.AddSeconds(4);
        foreach(var r in receiver.RootElement.EnumerateArray())
        {
            int oldRank=r[0].GetInt32(),rank=r[1].GetInt32(),other=r[2].GetInt32(),resistance=r[3].GetInt32(),attract=r[4].GetInt32();
            NecromancerCurse old=oldRank==0?null:new(other==1?CombatAbility.Weaken:CombatAbility.IronMaiden,999,oldRank,DateTime.UnixEpoch.AddMilliseconds(777*40),0,0);
            bool duration=SourceCurseDuration.Reduce(300+60*(rank-1),true,resistance,out int frames);
            var next=duration&&attract==0?NecromancerCurseCasts.Replacement(old,CombatAbility.IronMaiden,7,rank,null,at.AddMilliseconds(frames*40L),at,0,0):null;var current=next??old;
            Require((next!=null)==(r[5].GetInt32()!=0)&&(current?.Rank??0)==r[6].GetInt32()&&(current?.Source??0)==r[7].GetUInt32()&&(current?.UntilUtc??DateTime.UnixEpoch)==DateTime.UnixEpoch.AddMilliseconds(r[8].GetInt32()*40L)
                &&current?.PhysicalDelta==null&&current?.ElementDeltas==null,"No-stat source receiver/factory differential");receivers++;
        }
        Require(receivers==384,"Incomplete no-stat receiver fixture");
        bool refused=false;try{IronMaiden.Returned256(1000000,6550);}catch(NotSupportedException){refused=true;}Require(refused,"Signed-overflow source context was silently wrapped");
    }
    private static void Curse(Mobile t,PlayerMobile caster,CombatAbility ability,int rank=1)
        =>NecromancerEffects.Profiles.Store(t,new(Curse:new(ability,caster.Serial.Value,rank,TimedEffects.Now.AddMinutes(5),caster.X,caster.Y)));
    private static void Full(Mobile t)=>SkillHealth.SetBalance256(t,SkillHealth.Maximum256(t));
    private sealed class Ordered:MonsterReturnChecks.Release
    {
        private readonly Mobile Actor;private readonly BaseCreature Target;private readonly int Initial;internal bool DefenderEvent;
        internal Ordered(Mobile actor,BaseCreature target):base(new MonsterReturnChecks.WorldServices(target)){Actor=actor;Target=target;Initial=SkillHealth.Balance256(target);}
        internal override PreparedMonsterDamagePacket TookDamage(PreparedMonsterDamagePacket p)
        {Require(Actor is not{Alive:true,Deleted:false}&&SkillHealth.Balance256(Target)==Initial,"Iron Maiden must precede defender events and incoming HP");DefenderEvent=true;return p;}
        internal override bool AttackerInActiveMode()=>Actor is{Alive:true,Deleted:false};
    }
    private static void PreparedDeath(PlayerMobile p)
    {
        var pet=Enemy(p,3);var victim=Enemy(p,4);
        try
        {
            Require(pet.SetControlMaster(p),"Prepared pet ownership fixture");Curse(pet,p,CombatAbility.IronMaiden);
            SkillHealth.SetBalance256(pet,256);SkillHealth.SetBalance256(victim,2560);var services=new Ordered(pet,victim);
            var result=PreparedMonsterDamage.Release(pet,victim,new(2560,2560,0,0,1),new(false,()=>false,()=>5,()=>false),_=>0,services,TimedEffects.Now);
            Require(services.DefenderEvent&&result.DeathModeAttempted&&pet is not{Alive:true,Deleted:false}&&victim is not{Alive:true,Deleted:false},"Reflected death canceled original prepared hit/death");
            Require(victim.LastKiller==pet&&ClassLoot.Killer(victim)==p&&victim.FindDamageEntryFor(pet)?.Responsible?.Any(e=>e.Damager==p&&e.DamageGiven==10)==true,"Original lethal packet lost responsible pet owner after pre-HP reflection");
        }
        finally{pet.Corpse?.Delete();victim.Corpse?.Delete();pet.Delete();victim.Delete();}
        var actor=Enemy(p,3);victim=Enemy(p,4);
        try
        {
            Curse(actor,p,CombatAbility.IronMaiden);int before=SkillHealth.Balance256(actor);
            var services=new MonsterReturnChecks.Release(new MonsterReturnChecks.WorldServices(victim));
            PreparedMonsterDamage.Release(actor,victim,new(256,256,0,0,33),new(false,()=>false,()=>5,()=>false),_=>0,services,TimedEffects.Now);
            Require(SkillHealth.Balance256(actor)==before,"Suppressed damage-event packet triggered Iron Maiden");
        }
        finally{actor.Delete();victim.Delete();}
    }
    private static void Mechanics(PlayerMobile p,GraveyardSkeleton t)
    {
        Require(NecromancerEffects.Apply(p,t,CombatAbility.IronMaiden,1),"Iron Maiden application");
        foreach(int raw in new[]{1,255,256,257,511,10000,1048577})
        {Full(t);int before=SkillHealth.Balance256(t);IronMaiden.MeleeDamage(t,p,raw,false);Require(before-SkillHealth.Balance256(t)==IronMaiden.Returned256(raw,200),"Fractional reflection or source early division");}
        Full(t);t.SetResistance(ResistanceType.Physical,50);int life=SkillHealth.Balance256(t);IronMaiden.MeleeDamage(t,p,100*256,false);Require(life-SkillHealth.Balance256(t)==100*256,"Returned physical mitigation");
        t.SetResistance(ResistanceType.Physical,100);life=SkillHealth.Balance256(t);IronMaiden.MeleeDamage(t,p,100*256,false);Require(SkillHealth.Balance256(t)==life,"Physical immunity ignored on return");t.SetResistance(ResistanceType.Physical,0);
        foreach(string mode in new[]{"ranged","spell","fire","zero"})
        {
            Full(p);Full(t);life=SkillHealth.Balance256(t);
            if(mode=="zero"){IronMaiden.MeleeDamage(t,p,0,false);}
            else{AOS.Damage(p,t,10,false,mode=="fire"?0:100,mode=="fire"?100:0,0,0,0,archer:mode=="ranged",weaponDamage:mode!="spell",spellDamage:mode=="spell");}
            Require(SkillHealth.Balance256(t)==life,"Ineligible damage reflected: "+mode);
        }
        Full(p);Full(t);life=SkillHealth.Balance256(t);AOS.Damage(p,t,1,false,32,68,0,0,0,weaponDamage:true);
        Require(life-SkillHealth.Balance256(t)==162,"Native fractional physical event was floored or reflected twice");
        // Legacy Bone Armor payload exercises the same physical-pool consumer.
        Full(t);NecromancerEffects.Profiles.Store(t,(NecromancerEffects.Profiles.Find(t)??new()) with{BoneArmor=1});life=SkillHealth.Balance256(t);IronMaiden.MeleeDamage(t,p,256,false);
        Require(life-SkillHealth.Balance256(t)==256&&NecromancerEffects.Armor(t)==0,"Return did not consume physical armor before resistance");
        Full(p);Full(t);SkillHealth.SetBalance256(t,100*256);Curse(p,p,CombatAbility.LifeTap);
        AOS.Damage(p,t,10,false,100,0,0,0,0,weaponDamage:true);
        Require(SkillHealth.Balance256(t)==85*256,"Iron Maiden did not precede capped Life Tap healing");NecromancerEffects.Profiles.Clear(p);
        var victim=Owner();var actor=Enemy(p,2);victim.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);
        try
        {
            Curse(actor,p,CombatAbility.IronMaiden);Full(actor);SkillHealth.SetBalance256(victim,257);life=SkillHealth.Balance256(actor);
            AOS.Damage(victim,actor,100,false,100,0,0,0,0,weaponDamage:true);
            Require(life-SkillHealth.Balance256(actor)==200*256&&victim is not{Alive:true,Deleted:false},"Reflection was capped to actual victim life loss");
        }
        finally{actor.Delete();victim.Corpse?.Delete();victim.Delete();}
        victim=Owner();actor=Enemy(p,2);victim.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);
        try
        {
            Curse(actor,p,CombatAbility.IronMaiden);Curse(victim,p,CombatAbility.LifeTap);SkillHealth.SetBalance256(actor,256);Full(victim);life=SkillHealth.Balance256(victim);
            AOS.Damage(victim,actor,10,false,100,0,0,0,0,weaponDamage:true);
            Require(actor is not{Alive:true,Deleted:false}&&life-SkillHealth.Balance256(victim)==10*256,"Reflected attacker death canceled original native hit or Life Tap revived it");
        }
        finally{actor.Corpse?.Delete();actor.Delete();victim.Delete();}
        var warlock=ClassSkillChecks.Player(DiabloClass.Warlock);var enemy=Enemy(p,2);warlock.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);
        try
        {
            Require(PsychicWard.CastPrepared(warlock),"Prepared Ward fixture");Curse(warlock,p,CombatAbility.IronMaiden);var ward=PsychicWard.State(warlock);Full(warlock);life=SkillHealth.Balance256(warlock);
            Require(ward.Remaining256>2560,"Ward capacity fixture");IronMaiden.MeleeDamage(warlock,enemy,20*256,false);
            Require(SkillHealth.Balance256(warlock)==life&&PsychicWard.State(warlock).Remaining256==ward.Remaining256-2560,"Quarter-rate player return did not share Ward absorption");
        }
        finally{PsychicWard.Clear(warlock);warlock.Delete();enemy.Delete();}
        PreparedDeath(p);NecromancerEffects.Profiles.Clear(t);Full(t);Full(p);
        var other=Owner();
        try
        {
            Require(NecromancerEffects.Apply(p,t,CombatAbility.IronMaiden,3),"Initial Iron Maiden3");var first=NecromancerEffects.Curse(t) with{UntilUtc=TimedEffects.Now.AddMinutes(1)};NecromancerEffects.Profiles.Store(t,new(Curse:first));
            Require(!NecromancerEffects.Apply(other,t,CombatAbility.IronMaiden,1)&&NecromancerEffects.Curse(t)==first,"Weaker rank rewrote state");
            Require(NecromancerEffects.Apply(other,t,CombatAbility.IronMaiden,3)&&NecromancerEffects.Curse(t).Source==p.Serial.Value&&NecromancerEffects.Curse(t).UntilUtc<first.UntilUtc,"Equal refresh lost caster or expiry");
            Require(NecromancerEffects.Apply(other,t,CombatAbility.IronMaiden,4)&&NecromancerEffects.Curse(t).Source==other.Serial.Value,"Higher rank did not replace caster");other.Delete();life=SkillHealth.Balance256(t);IronMaiden.MeleeDamage(t,p,256,false);Require(life-SkillHealth.Balance256(t)==704,"Original caster deletion lost target-bound curse");
        }
        finally{other.Delete();}
        Require(NecromancerEffects.Apply(p,t,CombatAbility.Weaken,1),"Different curse replacement");life=SkillHealth.Balance256(t);IronMaiden.MeleeDamage(t,p,256,false);Require(life==SkillHealth.Balance256(t),"Replaced Iron Maiden remained active");
        Require(NecromancerEffects.Apply(p,t,CombatAbility.Attract,1)&&!NecromancerEffects.Apply(p,t,CombatAbility.IronMaiden,20),"Iron Maiden overwrote Attract");NecromancerEffects.Profiles.Clear(t);Full(t);Full(p);
    }
    private static void Casting(PlayerMobile p)
    {
        GraveyardSkeleton near=Enemy(p,8),edge=Enemy(p,11,1),far=Enemy(p,11,2);var center=new Point3D(p.X+8,p.Y,p.Z);
        try
        {
            Ready(p);int mana=NecromancerEffects.ManaBalance256(p);
            Require(NecromancerCombat.Release(p,CombatAbility.IronMaiden,Serial.Zero,center),"Ground area release");
            Require(NecromancerEffects.ManaBalance256(p)==mana-1280&&NecromancerEffects.Curse(near)!=null&&NecromancerEffects.Curse(edge)!=null&&NecromancerEffects.Curse(far)==null,"Area center,radius or once-only payment");
            foreach(var t in new[]{near,edge,far}){NecromancerEffects.Profiles.Clear(t);}
            Ready(p);mana=NecromancerEffects.ManaBalance256(p);
            Require(ClassCombat.Perform(p,CombatAbility.IronMaiden,near,near.Location,Core.TickCount),"Targeted windup");
            Require(NecromancerEffects.ManaBalance256(p)==mana&&NecromancerEffects.Curse(near)==null,"Curse applied/paid during windup");
            near.MoveToWorld(new(p.X-1,p.Y,p.Z),p.Map);
            Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt)&&NecromancerEffects.Curse(near)!=null&&NecromancerEffects.Curse(edge)==null,"Release did not follow selected target position");
            Ready(p);NecromancerEffects.Profiles.Clear(near);mana=NecromancerEffects.ManaBalance256(p);
            Require(ClassCombat.Perform(p,CombatAbility.IronMaiden,null,center,Core.TickCount),"Cancelled windup");long effect=ClassCombat.For(p).EffectAt;
            var origin=p.Location;p.MoveToWorld(new(p.X,p.Y+1,p.Z),p.Map);Require(!ClassCombat.ResolveAction(p,effect)&&NecromancerEffects.ManaBalance256(p)==mana,"Movement cancellation spent mana");p.MoveToWorld(origin,p.Map);
            Ready(p);NecromancerEffects.SetMana256(p,1279);Require(!NecromancerCombat.Begin(p,CombatAbility.IronMaiden,Serial.Zero,center,Core.TickCount),"Insufficient mana began a cast");
            near.Delete();edge.Delete();far.Delete();Ready(p);mana=NecromancerEffects.ManaBalance256(p);Require(NecromancerCombat.Release(p,CombatAbility.IronMaiden,Serial.Zero,new(p.X+16,p.Y,p.Z))&&NecromancerEffects.ManaBalance256(p)==mana-1280,"Empty area did not pay once");
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
            Ready(p);Require(NecromancerCombat.Release(p,CombatAbility.IronMaiden,Serial.Zero,center)&&NecromancerEffects.Curse(t)!=null,"Filter3 gained an unrequested recipient LOS gate");
        }
        finally{wall.Delete();t.Delete();p.Delete();}
    }
    private static void Check()
    {
        Differential();var p=Owner();var t=Enemy(p);Mechanics(p,t);Casting(p);LineOfSight();
        while(ClassCreation.Profiles.GetRank(p.Serial,"necromancer.ironmaiden")<20){Require(ClassCreation.Profiles.TryTrain(p.Serial,"necromancer.ironmaiden",out _),"Train rank20");}
        PoisonDaggerChecks.Named(p,"Wormskull");Require(SkillMechanics.Rank(p,CombatAbility.IronMaiden)==21,"Real effective item rank");
        NecromancerEffects.Profiles.Clear(t);Require(NecromancerEffects.Apply(p,t,CombatAbility.IronMaiden,21),"Effective-rank curse");
        var curse=NecromancerEffects.Curse(t) with{UntilUtc=TimedEffects.Now.AddDays(1)};NecromancerEffects.Profiles.Store(t,new(Curse:curse));
        var legacy=Enemy(p,3);var old=new NecromancerCurse(CombatAbility.IronMaiden,p.Serial.Value,1,TimedEffects.Now.AddDays(1),p.X,p.Y);
        Require(!JsonSerializer.Serialize(old).Contains("PhysicalDelta")&&!JsonSerializer.Serialize(old).Contains("ElementDeltas"),"No-stat curse manufactured a resistance field");NecromancerEffects.Profiles.Store(legacy,new(Curse:old));
        Ready(p);File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,t.Serial.Value,legacy.Serial.Value,curse,old,NecromancerEffects.ManaBalance256(p),p.Map.MapID,p.Location,0)));
        Log.Information("Iron Maiden:1008fractional source comparisons,native/prepared event order,overkill,reflected/mutual death,pet owner credit,Ward/physical armor/resistance,rank/caster/area/input and saved state controls pass");
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var t=(BaseCreature)World.FindMobile((Serial)s.Target);var legacy=(BaseCreature)World.FindMobile((Serial)s.Legacy);
        Require(NecromancerEffects.Curse(t)==s.Curse&&NecromancerEffects.Curse(legacy)==s.Old&&NecromancerEffects.ManaBalance256(p)==s.Mana,"Reload altered curse,mana or original deadline");
        p.MoveToWorld(s.Point,Map.Maps[s.Map]);Require(SkillMechanics.Rank(p,CombatAbility.IronMaiden)==21&&SkillBindings.Instance.Get(p).Right==CombatAbility.IronMaiden,"Reload changed item rank or binding");
        Full(p);int before=SkillHealth.Balance256(t);AOS.Damage(p,t,1,false,100,0,0,0,0,weaponDamage:true);
        Require(before-SkillHealth.Balance256(t)==7*256,"Restored effective-rank event or duplicate native return");
        before=SkillHealth.Balance256(legacy);IronMaiden.MeleeDamage(legacy,p,1,false);Require(before-SkillHealth.Balance256(legacy)==2,"Restored legacy fractional event");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
    }
    private static void Input(NetState network,bool quick)
    {
        var p=Owner();var t=Enemy(p,6);Ready(p);int mana=NecromancerEffects.ManaBalance256(p);string label=quick?"iron-maiden-quick":"iron-maiden-right";
        network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var protocol=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);protocol.SetValue(network,Enum.Parse(protocol.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        bool began=false;long deadline=Core.TickCount+20000;
        void Fail(Exception e){t.Delete();p.Delete();Log.Error(e,"{Label}-result: failed",label);}
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"IronMaiden input timeout");var session=ClassCombat.For(p);
                if(session.PendingAbility==CombatAbility.IronMaiden&&!began){Require(NecromancerEffects.Curse(t)==null&&NecromancerEffects.ManaBalance256(p)==mana,"Input paid/applied during windup");began=true;Log.Information("{Label}: windup",label);}
                if(NecromancerEffects.Curse(t) is not{} curse){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Inspect);return;}
                Require(began&&curse.Rank==1&&NecromancerEffects.ManaBalance256(p)==mana-1280,"Real input capture/payment");ClassCombat.Stop(p);
                Require((curse.UntilUtc-TimedEffects.Now).TotalMilliseconds is >11900 and <=12000,"Rank1duration is not300frames");
                Timer.StartTimer(curse.UntilUtc-TimedEffects.Now+TimeSpan.FromMilliseconds(100),()=>
                {try{Require(NecromancerEffects.Curse(t)==null,"Natural rank1curse did not expire");int before=SkillHealth.Balance256(t);IronMaiden.MeleeDamage(t,p,256,false);Require(before==SkillHealth.Balance256(t),"Expired curse still reflected");t.Delete();p.Delete();Log.Information("{Label}-result: passed",label);}catch(Exception e){Fail(e);}});
            }
            catch(Exception e){Fail(e);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.IronMaiden,(int)DiabloClass.Necromancer,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
