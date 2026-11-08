// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

internal sealed record ShadowFlightSequenceData(string ReferenceVersion,string Code,int Mode,int Points,int Speed,int TeleportFrame,int KickFrame,int EndFrame,int[][] Rows);
internal static class ShadowDragonFlight
{
    private static readonly Lazy<ShadowFlightSequenceData> Sequence=new(()=>
    {
        var d=BalanceCatalog.ReadObject<ShadowFlightSequenceData>("shadow-flight-sequence");
        if(d is not{ReferenceVersion:"3.3.93847",Code:"seq_swdragonflight",Mode:14,Points:23,Speed:256,TeleportFrame:10,KickFrame:15,EndFrame:24,Rows.Length:23}
            ||d.Rows.Where((r,i)=>r is not{Length:4}||r[0]!=(i<10?7:4)||r[1]!=(i<10?i:i-10)||r[2]!=0||r[3]!=(i is 9 or 14?1:0)).Any())
        {throw new NotSupportedException("Unqualified own Shadow Flight sequence");}
        return d;
    });
    internal static ItemExecutionMetadata Row()
    {
        var r=ShadowKickPackets.Row(275);
        if(ShadowSkillBook.Metadata(275).Mode!=14||r.InTown||r.UseManaOnDo||r.Lob||r.DecrementQuantity||r.Missile!=-1
            ||r.Number("UseAttackRate")!=0||r.Text("seqtrans")!="A1"||r.Number("seqnum")!=21||r.Number("Param7")!=38
            ||r.Number("TargetableOnly")!=0||r.Number("LineOfSight")!=0||r.Number("periodic")!=0||r.Number("passive")!=0||r.Number("aura")!=0
            ||r.HasDelay("delay")||r.HasDelay("localdelay")||r.HasDelay("globaldelay")||r.HasDelay("perdelay"))
        {throw new NotSupportedException("Unqualified own Shadow Flight row");}
        return r;
    }
    internal static ShadowAnimationPlan Plan(AssassinShadowBody body)
    {
        _=Row();_=ShadowEquipmentDefense.Require(body);
        if(body.OwnedSkills.Used!=275||body.OwnedSkills.Find(275)?.Mode!=14||ShadowEquipmentDefense.Composite(body)!=13)
        {throw new NotSupportedException("Own Flight needs its original mode14/dual-claw context");}
        var d=Sequence.Value;var events=new List<SourceAnimationEvent>();var state=new SentrySequenceState(0,d.Points*256,0);int frame=1;ReadOnlySpan<int> markers=stackalloc int[]{9,14};
        for(;state.Remaining>0;frame++)
        {
            if(state.Action==1){events.Add(new(1,frame,1,state.Point));}
            state=AssassinSentrySequence.AdvanceIn(state,d.Points*256,markers,d.Speed);
        }
        events.Add(new(2,frame,0));
        if(events.Count!=3||events[0].Frame!=d.TeleportFrame||events[1].Frame!=d.KickFrame||frame!=d.EndFrame)
        {throw new NotSupportedException("Own Flight clock differs from qualified NPC sequence");}
        return new(d.Speed,events.ToArray());
    }
    private static bool Target(AssassinShadowBody body,BaseCreature target)=>ShadowSkillCasting.Attached(body)&&body is{Alive:true,Deleted:false}
        &&body.ControlMaster is PlayerMobile owner&&target is{Alive:true,Deleted:false}&&target.Map==body.Map&&target.X!=0&&target.Y!=0
        &&SkillHealth.Balance256(body)>0&&SkillHealth.Balance256(target)>0&&ClassCombat.IsHostile(owner,target)&&!ClassCombat.Friendly(body,target)
        &&!SpellHelper.IsTown(body.Location,body)&&!SpellHelper.IsTown(target.Location,target);
    internal static bool Start(AssassinShadowBody body,BaseCreature target)
    {
        var r=Row();if(!Target(body,target)){return false;}
        long dx=2L*(body.X-target.X),dy=2L*(body.Y-target.Y),range=r.Number("Param7");
        if(dx*dx+dy*dy>range*range){return false;}
        OriginalNpcMelee.RequireAllocation(body,target);_=IronGolemContact.Require(body,(PlayerMobile)body.ControlMaster,target);
        _=ShadowKickDamage.Capture(body,checked(r.Number("Param1")+(body.OwnedSkillRank(275)-1)*r.Number("Param2")));return true;
    }
    internal static bool Destination(AssassinShadowBody body,BaseCreature target,out Point3D destination)
    {
        destination=default;if(!Target(body,target)){return false;}var map=body.Map;var origin=body.Location;var points=new Dictionary<(int,int),Point3D?>();
        Point3D? Resolve(LeapCell cell)
        {
            if(cell.X<0||cell.Y<0||cell.X>=map.Width*2||cell.Y>=map.Height*2){return null;}var key=(cell.X/2,cell.Y/2);
            if(!points.TryGetValue(key,out var value))
            {value=LeapTerrain.Ground(body,map,key.Item1,key.Item2,target.Z,out var point)&&point!=origin?point:null;points[key]=value;}
            return value;
        }
        var found=SourceFreeCoordinates.Find(new(target.X*2,target.Y*2),c=>Resolve(c)!=null);
        if(found is not{} cell||Resolve(cell)is not{} landing||!TreasureHunts.CanLand(body,map,landing)||!Teleportation.TravelAllowed(body,map,landing)){return false;}
        destination=landing;return true;
    }
    internal static bool Continues(AssassinShadowBody body,BaseCreature target)=>Target(body,target)&&ShadowDragonClaw.Continues(body,target);
    internal static IronGolemWorldState Synchronize(AssassinShadowBody body,IronGolemWorldState state,ShadowSkillCasting.Cast prior=null)
    {
        // Advance owns a local current-frame state. Merge only the authorized
        // own transfer's pose; never replace its frame/recovery/think schedule.
        var action=ShadowActions.State(body);var cast=prior??action?.Cast;
        if(cast is not{Skill:275,FlightLanded:true,FlightDestination:not null}||body.Map==null||body.Map==Map.Internal){return state;}
        var moved=state with{Map=body.Map.MapID,Motion=DruidRavenMotion.Center(body.X*2,body.Y*2)};
        return action?.Cast==cast&&body.Map==cast.Map&&body.Location==cast.FlightDestination.Value?moved:IronGolemWorld.Park(moved);
    }
    internal static int Release(ShadowSkillCasting.Cast cast,DateTime at)
    {
        var body=cast.Body;var target=cast.Target;
        if(cast.SequencePoint==9*256&&!cast.FlightLanded&&!cast.FlightKicked)
        {
            var origin=body.Location;var map=body.Map;
            if(!Destination(body,target,out var landing)||!Target(body,target)||body.Map!=map||body.Location!=origin||!Teleportation.Space(map,landing,body))
            {ShadowActions.Cancel(body);return 0;}
            if(!ShadowActions.ClaimFlightLanding(cast,landing)){return 0;}
            body.MoveToWorld(landing,map);body.AIObject?.AITimer.Stop();
            var action=ShadowActions.State(body);
            if(action?.Cast!=cast||body.Map!=map||body.Location!=landing||!Target(body,target))
            {ShadowActions.Cancel(body);return 0;}
            cast.FlightLanded=true;cast.FlightDestination=landing;cast.ServerDo=false;
            if(ShadowGroundMotion.Active(body)){body.WorldGround.Store(Synchronize(body,body.WorldGround.State.Controller));}
            Effects.SendLocationEffect(origin,map,0x3728,10,10);Effects.SendLocationEffect(landing,map,0x3728,10,10);body.PlaySound(0x1FE); // Shared teleport placeholder.
            Teleportation.ArrivalFields(body);
            if(ShadowActions.State(body)?.Cast!=cast||body.Map!=map||body.Location!=landing||body is not{Alive:true,Deleted:false,Frozen:false,Paralyzed:false}||!Target(body,target))
            {ShadowActions.Cancel(body);return 0;}
            return 1;
        }
        if(cast.SequencePoint!=14*256||!cast.FlightLanded||cast.FlightKicked||!Continues(body,target))
        {ShadowActions.Cancel(body);return 0;}
        cast.FlightKicked=true;var owner=(PlayerMobile)body.ControlMaster;var packet=ShadowKickContacts.Allocate(cast,275,false,at);
        if(NpcEquipmentImpact.Fills(packet.Impact.Packet.ResultFlags)&&Continues(body,target))
        {
            cast.ServerDo=true;var states=ShadowFinisherDamage.States(body,target,at);var effects=ShadowFinisherDamage.Effects(body,states);
            ShadowFinishers.Emit(body,target,packet,states,effects.Fire,effects.Ice,effects.Thunder,at,Row().Number("prgchargesconsumed"));
        }
        _=IronGolemContact.Release(body,owner,target,packet,b=>ShadowKickContacts.Range(body,target,b),at);body.PlaySound(0x238);return 1; // Shared kick placeholder.
    }
}
