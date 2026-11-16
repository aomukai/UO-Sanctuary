// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

internal sealed record PlayerProcWorldVisit(uint Owner,string Phase,PreparedMonsterDamagePacket Packet,PlayerItemProcOutcome[] Outcomes);

// Approved Sanctuary world binding for the existing ordered player melee consumer.
// No scalar native Damage inference,and no attack event per missile impact.
internal static class PlayerItemProcWorld
{
    internal static bool Approved=>true;
    private static readonly HashSet<uint> CandidateOwners=[];
    internal static Action<PlayerProcWorldVisit> Observed;
    private sealed class Proof(PlayerMobile owner):IDisposable
    {
        private bool Done;
        public void Dispose(){if(!Done){Done=true;CandidateOwners.Remove(owner.Serial.Value);}}
    }
    // Explicit isolated qualification scope;never a saved gameplay flag.
    internal static IDisposable Candidate(PlayerMobile owner)
    {
        if(owner is not{Deleted:false}||!CandidateOwners.Add(owner.Serial.Value))
        {throw new InvalidOperationException("Candidate player proc scope needs its distinct actual owner");}
        return new Proof(owner);
    }
    private static bool Actions(PlayerMobile owner)=>CandidateOwners.Contains(owner.Serial.Value)||owner.NetState is{Running:true};
    internal static Ticket Capture(PlayerMobile owner,BaseCreature target,DateTime at)
    {
        if(!(Approved||CandidateOwners.Contains(owner.Serial.Value))||!PlayerItemProcs.ActiveProvider(owner)) {return null;}
        if(World.Loading||owner is not{Alive:true,Deleted:false}||owner.Map==null||owner.Map==Map.Internal||!Actions(owner)
            ||SpellHelper.IsTown(owner.Location,owner)||at.Kind!=DateTimeKind.Utc||at>DateTime.MaxValue.AddMilliseconds(-2000)
            ||target is not{Alive:true,Deleted:false}||target.Map!=owner.Map||!ClassCombat.IsHostile(owner,target)) {return null;}
        var retained=NpcItemTarget.Capture(target);
        if(!PlayerItemProcs.Initialized(owner)){PlayerItemProcs.InitializeActor(owner,NpcWorldAllocation.UnitSeed());}
        return new(owner,retained,target.Location,at);
    }
    internal sealed class Ticket(PlayerMobile owner,NpcItemTarget target,Point3D path,DateTime at)
    {
        private bool Damage,Attack;
        internal void Dispatch(string phase,PreparedMonsterDamagePacket packet)
        {
            if(phase=="domeleedamage")
            {if(Damage){throw new InvalidOperationException("Player melee damage proc phase replayed");}Damage=true;}
            else if(phase=="domeleeattack")
            {if(Attack){throw new InvalidOperationException("Player melee attack proc phase replayed");}Attack=true;}
            else{throw new NotSupportedException("Player melee ticket needs its actual ordered phase");}
            // This route permits item casts without changing the ordinary
            // action. This is the approved adaptation,not a guessed D2 state bit.
            var outcomes=PlayerItemProcs.Dispatch(owner,phase,target,path,packet,false,at,
                request=>Allocate(owner,request,at),actions:Actions(owner));
            Observed?.Invoke(new(owner.Serial.Value,phase,packet,outcomes));
        }
    }
    private static NpcItemSpellAllocation Allocate(PlayerMobile owner,NpcItemMissileRequest request,DateTime at)
    {
        if(owner is not{Alive:true,Deleted:false}||!Actions(owner)||owner.Map==null||owner.Map==Map.Internal||World.Loading
            ||at.Kind!=DateTimeKind.Utc||request==null||request.Skill!=47||request.Rank is<1 or>63
            ||request.Missile!=ItemSkillExecution.MissileId("fireball")||(request.Flags&~0x1000)!=33
            ||request.Velocity!=0||request.ChargedOrdinal!=-1||request.AttackRating<0
            ||request.X<=0||request.Y<=0||request.X>=owner.Map.Width*2||request.Y>=owner.Map.Height*2
            ||(request.X&1)!=0||(request.Y&1)!=0)
        {throw new NotSupportedException("Player world proc allocation lost its qualified source/request");}
        return new(true,NpcWorldAllocation.UnitSeed());
    }
}
