// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record GoatmanAnimation(string Code,int Frames,int Speed,int ActionFrame);

// Prepared Sanctuary adaptation for unrecovered NPC startup/action boundaries.
// No timer, learned route, world AI registration or mana writer is installed.
internal static class GoatmanNpcActionProposal
{
    internal static bool Approved=>true;
    private static readonly Lazy<GoatmanAnimation[]> Animations=new(()=>
    {
        var rows=BalanceCatalog.Read<GoatmanAnimation>("goatman-animation");
        if(rows.Length!=3||rows[0]!=new GoatmanAnimation("GMA12HS",20,256,12)
            ||rows[1]!=new GoatmanAnimation("GMA22HS",16,256,8)||rows[2]!=new GoatmanAnimation("GMWL2HS",8,256,-1))
        {throw new System.IO.InvalidDataException("Unsupported owned Goatman animation metadata");}
        return rows;
    });
    internal static GoatmanAnimation WalkAnimation=>Animations.Value[2];
    internal static SourceAnimationState Schedule(int skill,int rank,int totalAttackRate,int frame)
    {
        if(skill is not(417 or 419)){throw new NotSupportedException("Only NPC Frenzy/Cleave use this proposal");}
        var values=GoatmanNpcSkills.Values(skill,rank);var row=Animations.Value[skill==417?1:0];
        int rate=checked(totalAttackRate+(skill==419?(int)values.Eval("calc3"):0));
        int[] events=new int[144];events[row.ActionFrame]=1;
        return SourceAnimationEvents.Initial(new(0,SourceAttackRate.Speed(row.Speed,0,rate),row.Frames*256,events,[]),frame);
    }

    // This actor-owned, explicit-clock state reuses the complete shared stat
    // writer. The host must retain it between actions; expiry does not refresh
    // on reads, parking or failed attacks. Persistence binding is still open.
    internal sealed class Frenzy(System.Action<int,int,FrenzyReleaseList> changed=null)
    {
        internal FrenzyReleaseList State{get;private set;}
        internal static Frenzy Restore(int rank,int charges,int expiry,System.Action<int,int,FrenzyReleaseList> changed=null)
        {
            if(rank is <1 or >255||charges<1||charges>rank||expiry<=0){throw new ArgumentException("Invalid saved NPC Frenzy projection");}
            var result=new Frenzy(changed);
            var list=new FrenzyReleaseList(charges,expiry){State=103,Posted=true,RemoveCallback=true};result.State=list;
            new Stats(result,0).WriteAura(list,new(103,0,0,0,0,128),417,charges);
            return result;
        }
        internal int Bonus(int frame)
        {
            if(frame<0){throw new ArgumentOutOfRangeException(nameof(frame));}
            if(State!=null&&frame>=State.Expiry){State=null;}
            return State?.AttackRate??0;
        }
        internal void Contact(int rank,int frame,bool successful)
        {
            _=Bonus(frame);if(!successful){return;}
            BarbarianFrenzyRelease.ApplyStats(417,rank,new Stats(this,frame));
            changed?.Invoke(rank,frame,State);
        }
        private sealed class Stats(Frenzy owner,int frame):FrenzyStatServices
        {
            internal override FrenzyReleaseRow Row(int skill)=>new(103,0,0,0,0,128);
            internal override FrenzyUsedSkill UsedSkill()=>new(1);
            internal override int StateCount=>104;
            internal override int GameFrame=>frame;
            internal override int UnitKind=>1;
            internal override int UnitId=>0; // Lists are actor-local, not world GUID registrations.
            internal override int Formula(int slot,int skill,int rank)=>slot==-1
                ?checked((int)GoatmanNpcSkills.Values(417,rank).Eval("auralencalc"))
                :throw new NotSupportedException("Unexpected NPC Frenzy formula request");
            internal override FrenzyReleaseList List(int state)=>owner.State;
            internal override FrenzyReleaseList AllocateList(int flags,int expiry,int kind,int id)=>new(0,expiry);
            internal override void SetRemoveCallback(FrenzyReleaseList list)=>list.RemoveCallback=true;
            internal override void PostList(FrenzyReleaseList list,int flag){list.Posted=true;owner.State=list;}
            internal override void ToggleState(int state,int flag){}
            internal override void ScheduleRemoval(int at,int zero,int zero2){}
            internal override void ToggleGraphic(int state,int flag){}
            internal override void WriteAura(FrenzyReleaseList list,FrenzyReleaseRow row,int skill,int level)
            {
                var values=GoatmanNpcSkills.Values(417,level);
                list.Velocity=checked((int)values.Eval("aurastatcalc1"));
                list.AttackRate=checked((int)values.Eval("aurastatcalc2"));
                list.OtherAnimationRate=checked((int)values.Eval("aurastatcalc3"));
            }
        }
    }

    internal abstract class Services
    {
        // Mandatory host leaves: active owner/body, hostility, source range,
        // safety/LOS and the existing ordered NPC contact/damage pipeline.
        internal abstract bool Active(BaseCreature actor);
        internal abstract bool Eligible(BaseCreature actor,BaseCreature target);
        internal abstract bool Contact(BaseCreature actor,BaseCreature target,int skill,int rank);
        internal abstract BaseCreature[] Nearby(BaseCreature actor,int sourceRadius);
        internal abstract int Reach(BaseCreature actor);
    }
    internal sealed class Action
    {
        internal readonly BaseCreature Actor,Target;
        internal readonly Map Map;
        internal readonly Point3D Origin,Aim;
        internal readonly int Skill,Rank,ReleaseFrame,EndFrame;
        internal readonly Frenzy Speed;
        internal int LastFrame,Contacts;
        internal bool Released,Finished,Cancelled,Delivering;
        internal Action(BaseCreature actor,BaseCreature target,int skill,int rank,int totalAttackRate,int frame,Frenzy speed)
        {
            if(actor is not{Alive:true,Deleted:false}||target is not{Alive:true,Deleted:false}||actor.Map is null
                ||actor.Map==Map.Internal||actor.Map!=target.Map||speed==null)
            {throw new ArgumentException("Prepared Goatman action requires living mapped actors and its own speed state");}
            Actor=actor;Target=target;Skill=skill;Rank=rank;Speed=speed;Map=actor.Map;Origin=actor.Location;Aim=target.Location;LastFrame=frame;
            var plan=Schedule(skill,rank,checked(totalAttackRate+speed.Bonus(frame)),frame);
            ReleaseFrame=plan.Events.Single(e=>e.Type==SourceAnimationEvents.ModeChange).Frame;
            EndFrame=plan.Events.Single(e=>e.Type==SourceAnimationEvents.EndAnimation).Frame;
        }
    }
    internal static void Cancel(Action action){action.Cancelled=true;action.Finished=true;}
    internal static CleaveSweepRequest Sweep(Action a,int reach)
    {
        if(a.Skill!=419||reach<0){throw new ArgumentException("NPC Cleave sweep context");}
        var row=GoatmanNpcSkills.Source(419);var v=GoatmanNpcSkills.Values(419,a.Rank);CleaveSweepRequest request=null;
        CleaveSweep.Run(new(true,1,true,a.Origin.X*2,a.Origin.Y*2,419,a.Rank,0,checked((int)row.Number("aurafilter")),128),
            new(()=>true,()=>(true,a.Aim.X*2,a.Aim.Y*2),_=>{},()=>false,
                (x,y)=>SourceDirection64.Calculate(x,y,CleaveActionProposal.MathRule).Heading,
                field=>checked((int)v.Eval(field switch{0x1a0=>"calc5",0x1a4=>"calc6",0x1a8=>"calc7",_=>throw new NotSupportedException()})),
                ()=>reach,()=>new(a.Origin.X*2,a.Origin.Y*2),r=>request=r));
        return request;
    }
    internal static bool Advance(Action a,int frame,Services s)
    {
        ArgumentNullException.ThrowIfNull(a);ArgumentNullException.ThrowIfNull(s);
        if(frame<a.LastFrame){throw new ArgumentException("NPC action clock went backwards");}
        a.LastFrame=frame;
        if(a.Finished||a.Delivering){return false;}
        if(a.Actor is not{Alive:true,Deleted:false,Frozen:false,Paralyzed:false}||a.Actor.Map!=a.Map||a.Actor.Location!=a.Origin
            ||a.Target is not{Alive:true,Deleted:false}||a.Target.Map!=a.Map||!s.Active(a.Actor))
        {Cancel(a);return false;}
        if(!a.Released&&frame>=a.ReleaseFrame)
        {
            a.Released=true;a.Delivering=true; // Claim before any nested combat callback.
            try
            {
                var sweep=a.Skill==419?Sweep(a,s.Reach(a.Actor)):null;
                var targets=sweep==null?[a.Target]:(s.Nearby(a.Actor,sweep.Radius)??throw new NotSupportedException("Missing NPC Cleave recipients"))
                    .Where(t=>t!=null&&t!=a.Actor&&t.Map==a.Map&&CleaveActionProposal.InArc(sweep,t.X*2,t.Y*2))
                    .DistinctBy(t=>t.Serial).OrderBy(t=>(long)(t.X-a.Origin.X)*(t.X-a.Origin.X)+(long)(t.Y-a.Origin.Y)*(t.Y-a.Origin.Y))
                    .ThenBy(t=>t.Serial.Value).ToArray();
                foreach(var target in targets)
                {
                    if(a.Cancelled||a.Actor is not{Alive:true,Deleted:false,Frozen:false,Paralyzed:false}||a.Actor.Map!=a.Map||a.Actor.Location!=a.Origin||!s.Active(a.Actor)){Cancel(a);break;}
                    if(target is not{Alive:true,Deleted:false}||target.Map!=a.Map||!s.Eligible(a.Actor,target)){continue;}
                    a.Contacts++;
                    bool success=s.Contact(a.Actor,target,a.Skill,a.Rank);
                    if(a.Skill==417){a.Speed.Contact(a.Rank,frame,success);}
                }
            }
            catch{Cancel(a);throw;}
            finally{a.Delivering=false;}
        }
        if(frame>=a.EndFrame){a.Finished=true;}
        return a.Released;
    }
}
