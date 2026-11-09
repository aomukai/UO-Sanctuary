// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

// Runs with SkillDamageEventChecks on each boot; its original fractional-life
// fixture supplies the reload/preservation check. No production subscriber.
internal static class OrderedDamageEventChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(OrderedDamageEventChecks));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private sealed class Services(BaseCreature target,SkillDamageKind kind=SkillDamageKind.Direct):PreparedMonsterDamageServices
    {
        internal int Entry,Loss,NativeDelta,Commits,Cold,Survived,Kills,Returns,Frees,OwnDamage;
        internal override SkillDamageKind DamageKind=>kind;
        internal override void StoreLastAttacker()=>Entry=target.Hits;
        internal override PreparedMonsterDamagePacket MitigateMissile(PreparedMonsterDamagePacket p)=>p;
        internal override PreparedMonsterDamagePacket DealtDamage(PreparedMonsterDamagePacket p)=>p;
        internal override PreparedMonsterDamagePacket TookDamage(PreparedMonsterDamagePacket p)=>p;
        internal override void BeforeHealth(PreparedMonsterDamagePacket p)
        {if(OwnDamage>0){SkillHealth.SetBalance256(target,SkillHealth.Balance256(target)-OwnDamage);}}
        internal override void DamageReaction(int delta){NativeDelta+=delta;Entry+=delta;}
        internal override void BeforeHealthCommit(Mobile attacker,BaseCreature recipient,int after256)
        {Commits++;Loss=Math.Max(0,Entry-(after256+255)/256);}
        internal override void ApplyCold(int frames)=>Cold++;
        internal override void SurvivedDamage()=>Survived++;
        internal override PreparedMonsterDamagePacket Killed(PreparedMonsterDamagePacket p){Kills++;return p;}
        internal override PreparedMonsterDamagePacket Kill(PreparedMonsterDamagePacket p)=>p;
        internal override void PresentOverlay(int overlay)=>throw new InvalidOperationException("Unexpected presentation");
        internal override int BaseHitClass()=>throw new InvalidOperationException("Unexpected hit class");
        internal override PreparedMonsterDamagePacket MeleeAttack(PreparedMonsterDamagePacket p)=>throw new InvalidOperationException("Unexpected melee attempt");
        internal override PreparedMonsterDamagePacket AttackedInMelee(PreparedMonsterDamagePacket p)=>throw new InvalidOperationException("Unexpected incoming attempt");
        internal override bool AttackerInActiveMode()=>throw new InvalidOperationException("Unexpected active-mode check");
        internal override void BeforeThorns(PreparedMonsterDamagePacket p)=>throw new InvalidOperationException("Unexpected Thorns");
        internal override void Return(ref PreparedMonsterDamagePacket p,Action death){Returns++;if((p.ResultFlags&2)!=0){death();}}
        internal override void FreePacket()=>Frees++;
    }
    private static ShockWaveTarget Enemy(PlayerMobile owner)
    {
        var t=new ShockWaveTarget{Frozen=true,CantWalk=true,NoKillAwards=true};t.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);
        t.AIObject.AITimer.Stop();t.SetHits(50000);SkillHealth.SetBalance256(t,1000*256);return t;
    }
    private static PreparedMonsterDamagePacket Packet(int amount=512,int flags=1)=>new(0,amount,0,0,flags,Magic256:amount);
    private static PreparedMonsterDamageResult Deliver(PlayerMobile owner,BaseCreature target,Services services,PreparedMonsterDamagePacket packet=null)
        =>PreparedMonsterDamage.ReleaseMissile(owner,target,packet??Packet(),new(false,()=>false,()=>1,()=>false),_=>0,services,TimedEffects.Now);
    internal static void Exercise(PlayerMobile owner)
    {
        var target=Enemy(owner);int seen=0,observed=0;Action<SkillDamageContact> react=null;
        void Observe(SkillDamageContact c){seen++;observed=SkillHealth.Balance256(c.Target);react?.Invoke(c);}
        var services=new Services(target);var baseline=Deliver(owner,target,services);
        Require(baseline.AfterHealth256==998*256&&services.Loss==2&&services.Commits==1&&services.Returns==1&&services.Frees==1,"No-listener ordered baseline changed");
        SkillDamageEvents.BeforeDirectDamage+=Observe;
        try
        {
            SkillHealth.SetBalance256(target,1000*256);services=new(target){OwnDamage=100*256};
            react=c=>{using(CombatItemEvents.Suppress(c.Source,c.Target)){SkillHealth.Damage(c.Source,c.Target,50*256+129);}};
            var result=Deliver(owner,target,services);react=null;
            Require(seen==1&&observed==900*256&&result.AfterHealth256==848*256-129&&SkillHealth.Balance256(target)==result.AfterHealth256
                &&services.NativeDelta==-50&&services.Loss==102&&services.Frees==1,"Ordered nested fractional damage overwrote HP or attributed secondary loss to the parent");
            seen=0;SkillHealth.SetBalance256(target,1000*256);services=new(target){OwnDamage=100*256};
            react=c=>SkillHealth.Heal(c.Target,30*256+129);result=Deliver(owner,target,services);react=null;
            Require(seen==1&&SkillHealth.Balance256(target)==928*256+129&&services.NativeDelta==31&&services.Loss==102,"Nested healing was overwritten or removed earlier owned damage from accounting");

            seen=0;
            foreach(var kind in new[]{SkillDamageKind.Periodic,SkillDamageKind.Return,SkillDamageKind.SelfCost})
            {services=new(target,kind);Deliver(owner,target,services);Require(services.Commits==1&&services.Frees==1,"Excluded kind lost damage");}
            Deliver(owner,target,new(target),Packet(flags:33));Deliver(owner,target,new(target),Packet(0));
            using(CombatItemEvents.Suppress(owner,target)){Deliver(owner,target,new(target));}
            Require(seen==0,"Ordered periodic/return/self-cost/zero/ResultFlags32/suppressed packet notified");
            SourceFireFields.Deliver(owner,target,new(0,256,0,0,1,HitClass:32,HitClassActive:true,Fire256:256),new(0,0,0,0,0),TimedEffects.Now);
            Require(seen==0,"Actual ground fire inherited direct notification");

            react=c=>target.Blessed=true;services=new(target);int before=SkillHealth.Balance256(target);result=Deliver(owner,target,services);react=null;target.Blessed=false;
            Require(result.Packet.ReactionSettled&&SkillHealth.Balance256(target)==before&&services.Commits==0&&services.Cold==0&&services.Returns==0&&services.Frees==1,"New invulnerability failed to cancel pending health/status/return");
            Require(!JsonSerializer.Serialize(result.Packet).Contains("ReactionSettled"),"Transient reaction delivery state leaked into packet JSON");
            bool refused=false;try{Deliver(owner,target,new(target),result.Packet);}catch(InvalidOperationException){refused=true;}
            Require(refused,"Settled packet could be recommitted");

            SkillHealth.SetBalance256(target,256);react=c=>{using(CombatItemEvents.Suppress(c.Source,c.Target)){SkillHealth.Damage(c.Source,c.Target,256);}};
            services=new(target);result=Deliver(owner,target,services);react=null;
            Require(!target.Alive&&result.Packet.ReactionSettled&&!result.DeathModeAttempted&&services.Commits==0&&services.Cold==0&&services.Kills==0&&services.Returns==0&&services.Frees==1,"Nested lethal reaction replayed parent HP/status/death/return");
        }
        finally{SkillDamageEvents.BeforeDirectDamage-=Observe;react=null;target.Delete();SkillHealth.Clear(target);}
        // Exercise the real learned controller and player weapon receiver too.
        var p=CleaveActionChecks.Fresh();var t=new CleaveActionTarget{Frozen=true,CantWalk=true,NoKillAwards=true};t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();t.SetHits(50000);
        var hit=CleaveActionProposal.HitRoll;var block=SourceBlocking.BlockRoll;seen=0;int nested=0;
        void CleaveReaction(SkillDamageContact c)
        {
            Require(c.Source==p&&c.Target==t&&c.Damage256>0,"Actual Cleave notification identity");seen++;
            if(nested==0){using(CombatItemEvents.Suppress(p,t)){SkillHealth.Damage(p,t,129);}}
            else{using(CombatItemEvents.Suppress(p,t)){SkillHealth.Damage(p,t,int.MaxValue);}}
        }
        SkillDamageEvents.BeforeDirectDamage+=CleaveReaction;
        try
        {
            CleaveActionProposal.HitRoll=()=>0;SourceBlocking.BlockRoll=n=>n-1;int life=SkillHealth.Balance256(t);
            var a=CleaveActionProposal.BeginPrepared(p,t.Serial,t.Location,Core.TickCount);Require(a!=null,"Actual Cleave did not begin");
            CleaveActionProposal.Advance(a,a.EffectAt);Require(a.Fault==null&&a.Successful==1&&seen==1&&SkillHealth.Balance256(t)<life-129,"Actual Cleave bypassed bridge or overwrote secondary HP");
            CleaveActionChecks.Ready(p);nested=1;a=CleaveActionProposal.BeginPrepared(p,t.Serial,t.Location,Core.TickCount);Require(a!=null,"Second actual Cleave did not begin");
            CleaveActionProposal.Advance(a,a.EffectAt);Require(a.Fault==null&&!t.Alive&&seen==2,"Actual Cleave reaction death faulted or replayed");
            Require(!WarlockCombat.Implemented(CombatAbility.HexPurge),"Shared infrastructure enabled unapproved Purge");
        }
        finally{SkillDamageEvents.BeforeDirectDamage-=CleaveReaction;CleaveActionProposal.HitRoll=hit;SourceBlocking.BlockRoll=block;ClassCombat.Stop(p);t.Delete();p.Delete();SkillHealth.Clear(t);}
        Log.Information("Ordered damage bridge:nested fractional damage/healing,owned-loss accounting,exclusions,new invulnerability,once terminal cleanup and actual Cleave survival/death pass;Purge remains gated.");
    }
}
