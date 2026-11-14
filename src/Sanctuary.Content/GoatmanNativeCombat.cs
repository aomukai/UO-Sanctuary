// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// One original demon's timed combat executor. The world host supplies its
// qualified AI choice and advancing source clock;no timer or simpler AI is
// installed here. In-flight contacts are transient and never replayed by load.
internal sealed class GoatmanNativeCombat(WarlockGoatmanBody body,PlayerMobile owner)
{
    internal sealed class Action
    {
        internal readonly int Skill,Rank,Mode,ReleaseFrame,EndFrame;
        internal readonly BaseCreature Target;
        internal readonly Map Map;
        internal readonly Point3D Origin;
        internal readonly (uint X,uint Y) SourceOrigin;
        internal readonly DateTime StartedUtc;
        internal readonly int StartFrame;
        internal readonly IronGolemContact.Prepared Packet;
        internal readonly GoatmanNpcActionProposal.Action Sweep;
        internal int LastFrame,Contacts;
        internal bool Released,Finished,Cancelled,Delivering;
        internal MonsterContactResult Result;
        internal Action(WarlockGoatmanBody body,BaseCreature target,int skill,int rank,int frame,DateTime at,
            SourceAnimationState plan,IronGolemContact.Prepared packet,GoatmanNpcActionProposal.Action sweep)
        {
            Skill=skill;Rank=rank;Mode=skill==417?5:4;Target=target;Map=body.Map;Origin=body.Location;
            SourceOrigin=GoatmanNpcActionProposal.Position(body);StartedUtc=at;StartFrame=LastFrame=frame;Packet=packet;Sweep=sweep;
            ReleaseFrame=sweep?.ReleaseFrame??checked(frame+plan.Events.Single(e=>e.Type==SourceAnimationEvents.ModeChange).Frame);
            EndFrame=sweep?.EndFrame??checked(frame+plan.Events.Single(e=>e.Type==SourceAnimationEvents.EndAnimation).Frame);
        }
    }
    internal Action Current{get;private set;}
    private DateTime _at;
    private GoatmanNpcContact.World World=>new(body,owner,()=>_at,()=>Current is not{} a
        ||DeathMarkWorld.State(body)?.Mode==a.Mode&&GenericPetNativeControlStates.Read(body)==0);
    internal GenericPetActionServices Bind(GenericPetActionServices outer,Func<int> frame,Func<DateTime> clock)
    {
        ArgumentNullException.ThrowIfNull(outer);ArgumentNullException.ThrowIfNull(frame);ArgumentNullException.ThrowIfNull(clock);
        BaseCreature Target(int id)=>Server.World.FindMobile((Serial)(uint)id)as BaseCreature
            ??throw new NotSupportedException("Timed Goatman choice has no live NPC target");
        bool Skill(byte mode,int skill,int target,int x,int y,bool selected)
        {
            if(mode!=(skill==417?5:4)||x!=0||y!=0||!selected)
            {throw new NotSupportedException("Timed Goatman choice is not the qualified melee descriptor");}
            Open(Target(target),skill,frame(),clock());return true;
        }
        return outer with{ExecuteSkill=Skill,InitializeMode=mode=>new(0,0,0,mode),ExecuteMode=request=>
        {
            if(request.Opaque is not int mode||mode!=4||request.X!=0||request.Y!=0)
            {throw new NotSupportedException("Timed Goatman base choice requires primary attack mode4");}
            Open(Target(request.Target),0,frame(),clock());return true;
        }};
    }
    internal Action Open(BaseCreature target,int skill,int frame,DateTime at)
    {
        GoatmanNpcActionProposal.ValidateFrame(frame);
        if(at.Kind!=DateTimeKind.Utc||Current!=null||DeathMarkWorld.State(body)is not{Mode:1,Selector:1}
            ||!World.Active(body)||!World.Eligible(body,target)||body.Frozen||body.Paralyzed||GenericPetNativeControlStates.Read(body)!=0)
        {throw new NotSupportedException("Timed Goatman attack needs an idle active registered actor and eligible target");}
        int rank=skill==0?0:GoatmanNpcContact.Rank(body,skill);
        if(skill!=0&&rank<1){throw new NotSupportedException("Timed Goatman NPC skill is not captured");}
        SourceAnimationState plan=null;IronGolemContact.Prepared packet=null;GoatmanNpcActionProposal.Action sweep=null;
        _at=at;
        if(skill is 417 or 419){sweep=GoatmanNpcBodyActions.OpenPrepared(body,target,skill,frame,at);}
        else
        {
            // Current gather selects primary mode4 for basic/Stun/Berserk.
            // Their existing startup allocators retain hit/damage and recipient
            // mitigation until do2;they are not rerolled at release.
            plan=GoatmanNpcActionProposal.PrimarySchedule(GoatmanNpcBodyActions.AttackRate(body,at));
            packet=skill==0?GoatmanNpcContact.PrepareBasic(body,owner,target,at):GoatmanNpcContact.Prepare(body,owner,target,skill,at);
        }
        var action=new Action(body,target,skill,rank,frame,at,plan,packet,sweep);
        DeathMarkWorld.ChangeMode(body,action.Mode);Current=action;return action;
    }
    private void Finish(Action a,bool cancel)
    {
        a.Cancelled|=cancel;a.Finished=true;a.Packet?.Cancel();if(a.Sweep!=null&&cancel){GoatmanNpcActionProposal.Cancel(a.Sweep);}
        if(ReferenceEquals(Current,a)){Current=null;DeathMarkWorld.FinishAction(body,a.Mode);}
    }
    internal void Cancel(){if(Current is{} a){Finish(a,true);}}
    internal void Advance(int frame,DateTime at)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Timed NPC delivery requires UTC");}
        var a=Current;if(a==null){return;}
        if(frame<a.LastFrame||at<a.StartedUtc||at!=a.StartedUtc.AddTicks(checked((long)(frame-a.StartFrame)*SourceFrameTimeline.IntervalTicks)))
        {throw new ArgumentException("Timed NPC delivery must preserve its supplied source-frame phase");}
        if(a.Delivering){return;}
        a.LastFrame=frame;_at=at;
        if(body is not{Alive:true,Deleted:false,Frozen:false,Paralyzed:false}||body.Map!=a.Map||body.Location!=a.Origin
            ||GoatmanNpcActionProposal.Position(body)!=a.SourceOrigin||DeathMarkWorld.State(body)?.Mode!=a.Mode
            ||GenericPetNativeControlStates.Read(body)!=0||!World.Active(body)
            ||a.Target is not{Alive:true,Deleted:false}||a.Target.Map!=a.Map)
        {Finish(a,true);return;}
        a.Delivering=true;
        try
        {
            if(a.Sweep!=null)
            {
                GoatmanNpcActionProposal.Advance(a.Sweep,frame,World);
                a.Released=a.Sweep.Released;a.Contacts=a.Sweep.Contacts;
                if(a.Sweep.Finished){Finish(a,a.Sweep.Cancelled);}
            }
            else
            {
                if(!a.Released&&frame>=a.ReleaseFrame)
                {
                    a.Released=true;a.Contacts++;
                    a.Result=a.Skill==0?GoatmanNpcContact.ReleaseBasic(body,owner,a.Target,a.Packet,at)
                        :GoatmanNpcContact.Release(body,owner,a.Target,a.Skill,a.Packet,at);
                }
                if(ReferenceEquals(Current,a)&&frame>=a.EndFrame){Finish(a,false);}
            }
        }
        catch{Finish(a,true);throw;}
        finally{a.Delivering=false;}
    }
}
