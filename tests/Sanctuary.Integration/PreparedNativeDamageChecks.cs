// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using Server;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class PreparedNativeDamageChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(PreparedNativeDamageChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("prepared-native-damage-check","Native notification order without HP/death replay",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("prepared-native-damage-check: passed");}catch(Exception e){Log.Error(e,"prepared-native-damage-check: failed");}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public sealed class Recipient:BaseCreature
    {
        internal readonly List<string> Trace=new();
        internal Mobile Source;internal string MutateAt;internal bool Armed,NotificationComplete;internal int IncomingCalls,DamageNotifications;
        [Constructible] public Recipient():base(AIType.AI_Melee,FightMode.Closest,10,1)
        {Name="Native notification recipient [fixture]";Body=0x190;NoKillAwards=true;Summoned=true;CantWalk=true;SetHits(100);}
        public Recipient(Serial serial):base(serial){}
        public override bool CanRegenHits=>false;
        internal void Observe(string phase,int amount=0,bool kill=false)
        {
            if(!Armed||NotificationComplete){return;}
            Trace.Add($"{phase}:{amount}:{kill}:{SkillHealth.Balance256(this)}:{FindDamageEntryFor(Source)?.DamageGiven??0}:{Paralyzed}:{Meditating}");
            if(MutateAt==phase){Hits--;}
        }
        public override void DisruptiveAction(){Observe("disrupt");base.DisruptiveAction();}
        public override void Damage(int amount,Mobile from=null,bool informMount=true,bool ignoreEvilOmen=false)
        {IncomingCalls++;base.Damage(amount,from,informMount,ignoreEvilOmen);}
        public override void OnDamage(int amount,Mobile from,bool willKill)
        {DamageNotifications++;Observe("damage",amount,willKill);base.OnDamage(amount,from,willKill);if(Mount==null){NotificationComplete=true;}}
        public override void Serialize(IGenericWriter writer){base.Serialize(writer);writer.Write(0);}
        public override void Deserialize(IGenericReader reader){base.Deserialize(reader);if(reader.ReadInt()!=0){throw new InvalidDataException();}}
    }
    public sealed class Mount:Horse
    {
        internal Recipient Observed;
        [Constructible] public Mount(){}
        public Mount(Serial serial):base(serial){}
        public override void OnRiderDamaged(int amount,Mobile from,bool willKill)
        {Observed.Observe("mount",amount,willKill);base.OnRiderDamaged(amount,from,willKill);Observed.NotificationComplete=true;}
        public override void Serialize(IGenericWriter writer){base.Serialize(writer);writer.Write(0);}
        public override void Deserialize(IGenericReader reader){base.Deserialize(reader);if(reader.ReadInt()!=0){throw new InvalidDataException();}}
    }
    private sealed class Spell(Recipient target):ISpell
    {
        public bool BlocksMovement=>false;public bool IsCasting=>false;
        public bool OnCasterEquipping(Item item)=>true;
        public void OnCasterHurt()=>target.Observe("spell");
        public void OnCasterKilled(){}public void OnConnectionChanged(){}public void FinishSequence(){}
        public bool OnCasterMoving(Direction d)=>true;public bool OnCasterUsingObject(IEntity e)=>true;public bool OnCastInTown(Region r)=>true;
    }
    private sealed class DamageRegion(Map map,Rectangle2D area):Region("Prepared native damage fixture",map,1000,area)
    {
        internal int Rule;
        public override bool OnDamage(Mobile mobile,ref int damage)
        {
            if(mobile is Recipient target){target.Observe("region");if(Rule==3){target.Hits--;}}
            if(Rule==1){return false;}if(Rule==2){damage++;}return true;
        }
    }
    private sealed class Services(BaseCreature target):MonsterReturnChecks.Release(new MonsterReturnChecks.WorldServices(target))
    {
        private readonly MonsterHitClassCycle _cycle=new();
        internal override void BeforeHealthCommit(BaseCreature source,BaseCreature recipient,int after256)
            =>PreparedMonsterNativeDamage.Notify(source,recipient,after256);
        internal override void Return(ref PreparedMonsterDamagePacket packet,Action death)
        {World.Death=death;PreparedMonsterReturn.Dispatch(ref packet,_cycle,World,true);}
    }
    private static Recipient Target(BaseCreature source,int life,bool mounted,out Mount mount)
    {
        var target=new Recipient();target.MoveToWorld(source.Location,source.Map);target.AIObject.AITimer.Stop();SkillHealth.SetBalance256(target,life);
        mount=mounted?new Mount{Observed=target}:null;if(mount!=null){mount.Rider=target;Require(target.Mount==mount,"Fixture mount binding");}
        target.Source=source;target.Spell=new Spell(target);target.Paralyzed=true;target.Meditating=true;target.Armed=true;return target;
    }
    private static PreparedMonsterDamageResult Release(BaseCreature source,BaseCreature target,int damage)
        =>PreparedMonsterDamage.Release(source,target,new(damage,damage,0,0,1),new(false,()=>false,()=>5,()=>false),_=>0,new Services(target),TimedEffects.Now);
    private static void Clean(Recipient target,Mount mount)
    {target.Armed=false;target.Spell=null;target.Corpse?.Delete();target.Delete();mount?.Delete();}
    private static void Check()
    {
        var source=new NpcPoisonRecipient();source.MoveToWorld(new(6200,2100,0),Map.Trammel);source.AIObject.AITimer.Stop();
        var region=new DamageRegion(source.Map,new(source.X-2,source.Y-2,5,5));region.Register();
        var visible=Mobile.VisibleDamageType;int cases=0;
        try
        {
            foreach(var mode in Enum.GetValues<VisibleDamageType>())foreach(bool mounted in new[]{false,true})
            foreach(int life in new[]{257,1001,5000,100*256})foreach(int damage in new[]{1,17,255,256,257,300,2000,1000*256})
            {
                Mobile.VisibleDamageType=mode;
                var native=Target(source,life,mounted,out var nativeMount);var ordered=Target(source,life,mounted,out var orderedMount);
                try
                {
                    Require(native.Region==region&&ordered.Region==region,"Fixture damage region");
                    SkillHealth.Damage(source,native,damage);var result=Release(source,ordered,damage);
                    Require(string.Join('|',native.Trace)==string.Join('|',ordered.Trace),"Native notification sequence differs "+cases+" native="+string.Join('|',native.Trace)+" ordered="+string.Join('|',ordered.Trace));
                    Require(SkillHealth.Balance256(native)==SkillHealth.Balance256(ordered)&&(!native.Alive||native.Deleted)==result.DeathModeAttempted,"Native notification HP/death differs "+cases);
                    bool notified=native.Trace.Count>0;
                    Require(native.IncomingCalls==(notified?1:0)&&ordered.IncomingCalls==0,"Incoming native Damage was replayed "+cases);
                    Require(ordered.Paralyzed==!notified&&ordered.Meditating==!notified,"Native paralysis/meditation disruption differs "+cases);
                    Require(ordered.Trace.FindAll(x=>x.StartsWith("damage:")).Count==(notified?1:0),"OnDamage duplicated "+cases);cases++;
                    Require(native.DamageNotifications==(notified?1:0)&&ordered.DamageNotifications==(notified?1:0),"OnDamage duplicated outside notification trace");
                }
                finally{Clean(native,nativeMount);Clean(ordered,orderedMount);}
            }
            int refused=0;
            foreach(int rule in new[]{1,2,3})
            {
                region.Rule=rule;var target=Target(source,100*256,false,out var mount);
                try
                {
                    bool rejected=false;try{Release(source,target,5*256);}catch(NotSupportedException){rejected=true;}
                    Require(rejected&&target.Alive&&!target.Deleted&&SkillHealth.Balance256(target)==(100-(rule==3?1:0))*256
                        &&target.FindDamageEntryFor(source)==null&&target.Trace.Count==1&&target.Paralyzed,"Region policy silently bypassed or committed HP");refused++;
                }
                finally{Clean(target,mount);}
            }
            region.Rule=0;
            foreach(string phase in new[]{"spell","disrupt","damage","mount"})
            {
                var target=Target(source,100*256,phase=="mount",out var mount);target.MutateAt=phase;
                try
                {
                    bool rejected=false;try{Release(source,target,5*256);}catch(NotSupportedException){rejected=true;}
                    Require(rejected&&target.Alive&&!target.Deleted&&SkillHealth.Balance256(target)==99*256,"Reentrant native callback was silently followed by incoming HP/death");refused++;
                }
                finally{Clean(target,mount);}
            }
            Require(cases==256&&refused==7,"Incomplete native notification controls");
            Log.Information("Native notifications:256native/ordered fractional-overkill spell-credit-disruption-paralysis-OnDamage/mount comparisons across4visibility modes;3region/4reentrant refusals pass;no incoming Damage replay,live Iron migration remains pending");
        }
        finally{Mobile.VisibleDamageType=visible;region.Unregister();source.Delete();}
    }
}
