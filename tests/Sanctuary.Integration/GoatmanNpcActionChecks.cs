// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class GoatmanNpcActionChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(GoatmanNpcActionChecks));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("goatman-npc-action-check","Prepared NPC Frenzy/Cleave action and shared writer",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("goatman-npc-action-check: passed");}catch(Exception e){Log.Error(e,"goatman-npc-action-check: failed");}}));
    // Explicit empty fixture notifications, not production event admission.
    private sealed class Damage:PreparedMonsterDamageServices
    {
        internal int Frees;
        internal override void StoreLastAttacker(){}
        internal override PreparedMonsterDamagePacket DealtDamage(PreparedMonsterDamagePacket p)=>p;
        internal override PreparedMonsterDamagePacket TookDamage(PreparedMonsterDamagePacket p)=>p;
        internal override void BeforeHealth(PreparedMonsterDamagePacket p){}
        internal override void ApplyCold(int frames){Require(frames==0,"Unexpected cold");}
        internal override void SurvivedDamage(){}
        internal override PreparedMonsterDamagePacket Killed(PreparedMonsterDamagePacket p)=>p;
        internal override PreparedMonsterDamagePacket Kill(PreparedMonsterDamagePacket p)=>p;
        internal override void PresentOverlay(int overlay)=>throw new InvalidOperationException("Unexpected critical overlay");
        internal override int BaseHitClass()=>3;
        internal override PreparedMonsterDamagePacket MeleeAttack(PreparedMonsterDamagePacket p)=>p;
        internal override PreparedMonsterDamagePacket AttackedInMelee(PreparedMonsterDamagePacket p)=>p;
        internal override bool AttackerInActiveMode()=>true;
        internal override void BeforeThorns(PreparedMonsterDamagePacket p){}
        internal override void Return(ref PreparedMonsterDamagePacket p,System.Action death){if((p.ResultFlags&2)!=0){death();}}
        internal override void FreePacket()=>Frees++;
    }
    internal sealed class World:GoatmanNpcActionProposal.Services
    {
        internal bool Enabled=true,Hit=true;
        internal BaseCreature[] Targets=[];
        internal readonly List<uint> Order=[];
        internal System.Action Nested;
        internal override bool Active(BaseCreature actor)=>Enabled;
        internal override bool Eligible(BaseCreature actor,BaseCreature target)=>actor.InRange(target,2)&&actor.InLOS(target);
        internal override int Reach(BaseCreature actor)=>2;
        internal override BaseCreature[] Nearby(BaseCreature actor,int radius)=>Targets;
        internal override bool Contact(BaseCreature actor,BaseCreature target,int skill,int rank)
        {
            Order.Add(target.Serial.Value);Nested?.Invoke();
            var p=GoatmanNpcMelee.Prepare(skill,rank,Hit?1:0,SkillHealth.Balance256(target),new(10,10,0),d=>d,_=>0,()=>99);
            var damage=new Damage();var before=SkillHealth.Balance256(target);
            var packet=new PreparedMonsterDamagePacket(p.Physical256,p.Total256,0,0,p.ResultFlags,p.HitClass);
            if((p.ResultFlags&1)!=0)
            {
                var delivered=PreparedMonsterDamage.Release(actor,target,packet,new(false,()=>false,()=>1,()=>false),_=>0,damage,TimedEffects.Now);
                Require(delivered.AfterHealth256==before-p.Total256,"Successful contact did not commit HP");
            }
            else{_=PreparedMonsterDamage.Finish(actor,target,packet,damage,false);}
            Require(damage.Frees==1&&SkillHealth.Balance256(target)==before-p.Total256,"Shared actual FP256 HP/release was not committed once");
            return (p.ResultFlags&1)!=0;
        }
    }
    private static EncounterCreature Body(Point3D at,Map map)
    {
        var b=new EncounterCreature{Name="Goatman action boundary [fixture]",CantWalk=true,NoKillAwards=true};
        b.SetHits(50000);b.Hits=b.HitsMax;b.MoveToWorld(at,map);b.AIObject?.AITimer.Stop();return b;
    }
    private static void Check()
    {
        Require(GoatmanNpcActionProposal.Approved&&!WarlockCombat.Implemented(CombatAbility.SummonGoatman),"Prepared candidate enabled incomplete world summon");
        int packets=0,schedules=0;
        for(int rank=1;rank<=255;rank++)
        {
            foreach(int id in new[]{417,419})
            {
                int draws=0;var p=GoatmanNpcMelee.Prepare(id,rank,1,500000,new(10,10,20),d=>d,_=>{draws++;return 0;},()=>99);
                int ed=id==419?rank+4:0;int expected=2560+SourcePercentage.Calculate(2560,20+ed,100);
                Require(p.Physical256==expected&&p.Magic256==0&&p.StunFrames==0&&!p.Critical,"NPC damage used player Frenzy/Cleave ED or stun");
                Require(draws==1,"Absent skill element consumed RNG beyond the ordinary physical range roll");
                var miss=GoatmanNpcMelee.Prepare(id,rank,0,500000,new(10,10,20),_=>throw new InvalidOperationException(),_=>throw new InvalidOperationException(),()=>throw new InvalidOperationException());
                Require(miss.Total256==0,"Miss allocated damage");packets++;
                foreach(int rate in new[]{25,100,175})
                {
                    var plan=GoatmanNpcActionProposal.Schedule(id,rank,rate,100);
                    var hit=plan.Events.Single(x=>x.Type==SourceAnimationEvents.ModeChange);var end=plan.Events.Single(x=>x.Type==SourceAnimationEvents.EndAnimation);
                    Require(hit.Frame>100&&hit.Frame<end.Frame&&hit.Kind==1,"Bad owned animation schedule");schedules++;
                }
            }
            var speed=new GoatmanNpcActionProposal.Frenzy();
            for(int i=1;i<=rank+1;i++){speed.Contact(rank,100+i,true);}
            int charges=rank;var values=GoatmanNpcSkills.Values(417,charges);
            Require(speed.State.Charges==rank&&speed.State.Posted&&speed.State.RemoveCallback&&speed.State.State==103
                &&speed.State.Velocity==values.Eval("aurastatcalc1")&&speed.State.AttackRate==values.Eval("aurastatcalc2")
                &&speed.State.OtherAnimationRate==values.Eval("aurastatcalc3"),"Shared NPC Frenzy cap/stat writer differs");
            int expiry=speed.State.Expiry;Require(expiry==101+rank+200+(rank-1)*25,"NPC Frenzy duration differs");
            speed.Contact(rank,expiry-1,false);Require(speed.State.Expiry==expiry&&speed.Bonus(expiry-1)>0&&speed.Bonus(expiry)==0,"Miss refreshed Frenzy or lazy expiry moved deadline");
        }
        var owner=new PlayerMobile{Name="Goatman action fixture",Player=true};BaseCreature actor=null,front=null,side=null,back=null;
        try
        {
            FixtureSpace.Corridor(owner,6600,24,3590);
            actor=Body(new(owner.X+2,owner.Y,owner.Z),owner.Map);front=Body(new(actor.X+1,actor.Y,actor.Z),owner.Map);
            side=Body(new(actor.X+2,actor.Y+1,actor.Z),owner.Map);back=Body(new(actor.X-1,actor.Y,actor.Z),owner.Map);
            int initialMana=actor.Mana;
            var w=new World{Targets=[back,side,front,front]};var state=new GoatmanNpcActionProposal.Frenzy();
            var a=new GoatmanNpcActionProposal.Action(actor,front,417,4,100,100,state);
            Require(a.ReleaseFrame==108&&a.EndFrame==116,"NPC A2 base clock differs");
            Require(!GoatmanNpcActionProposal.Advance(a,107,w)&&w.Order.Count==0,"Early NPC release");
            w.Nested=()=>GoatmanNpcActionProposal.Advance(a,108,w);
            Require(GoatmanNpcActionProposal.Advance(a,108,w)&&w.Order.Count==1&&state.State.Charges==1,"Contact/reentrant charge duplicated");
            w.Nested=null;GoatmanNpcActionProposal.Advance(a,116,w);GoatmanNpcActionProposal.Advance(a,117,w);Require(a.Finished&&w.Order.Count==1,"Finished action replayed");
            var c=new GoatmanNpcActionProposal.Action(actor,front,419,5,100,200,state);var sweep=GoatmanNpcActionProposal.Sweep(c,2);
            Require(sweep.Skill==419&&sweep.Radius==6&&sweep.Start!=sweep.End,"NPC Cleave did not reuse its own 11/30 fan");
            w.Order.Clear();GoatmanNpcActionProposal.Advance(c,c.EndFrame,w);
            Require(w.Order.SequenceEqual(new[]{front.Serial.Value,side.Serial.Value})&&c.Contacts==2&&c.Finished,"Cleave arc/order/dedup or late single release differs");
            var cancelled=new GoatmanNpcActionProposal.Action(actor,front,419,5,100,300,state);w.Order.Clear();
            w.Nested=()=>GoatmanNpcActionProposal.Cancel(cancelled);GoatmanNpcActionProposal.Advance(cancelled,cancelled.ReleaseFrame,w);
            Require(cancelled.Cancelled&&w.Order.Count==1,"Cancellation delivered later Cleave contacts");w.Nested=null;
            var stunned=new GoatmanNpcActionProposal.Action(actor,front,419,5,100,350,state);w.Order.Clear();
            w.Nested=()=>actor.Frozen=true;GoatmanNpcActionProposal.Advance(stunned,stunned.ReleaseFrame,w);
            Require(stunned.Cancelled&&w.Order.Count==1,"Interruption during the fan delivered later contacts");w.Nested=null;actor.Frozen=false;
            var moved=new GoatmanNpcActionProposal.Action(actor,front,417,4,100,400,state);actor.Location=new(actor.X,actor.Y+1,actor.Z);w.Order.Clear();
            Require(!GoatmanNpcActionProposal.Advance(moved,moved.ReleaseFrame,w)&&moved.Cancelled&&w.Order.Count==0,"Movement did not cancel");
            actor.Location=moved.Origin;var offline=new GoatmanNpcActionProposal.Action(actor,front,417,4,100,500,state);w.Enabled=false;
            Require(!GoatmanNpcActionProposal.Advance(offline,offline.ReleaseFrame,w)&&offline.Cancelled,"Inactive owner/host did not cancel");w.Enabled=true;
            var failed=new GoatmanNpcActionProposal.Action(actor,front,417,4,100,600,new());w.Hit=false;GoatmanNpcActionProposal.Advance(failed,failed.ReleaseFrame,w);
            Require(failed.Speed.State==null,"Miss granted NPC Frenzy speed");
            bool refused=false;try{GoatmanNpcActionProposal.Advance(failed,599,w);}catch(ArgumentException){refused=true;}Require(refused,"Backward clock accepted");
            Require(actor.Mana==initialMana,"NPC candidate spent or supplied mana");
        }
        finally{actor?.Delete();front?.Delete();side?.Delete();back?.Delete();owner.Delete();}
        Log.Information("Goatman NPC candidate:510 own-row packets/1530 schedules/255 charge caps;actual shared HP,fan ordering,reentrancy,miss,expiry,cancellation;world-host/persistence remain open");
    }
}
