// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

internal sealed record PlayerItemFireBallBirth(DiabloClass Class,Point3D Origin,ulong Seed);

internal static partial class SorceressSpellMissiles
{
    private static SkillValues PlayerItemFireBallValues(PlayerMobile owner,int rank)
    {
        ClassCreation.Profiles.TryGet(owner.Serial,out var profile);
        if(profile?.Class==DiabloClass.Sorceress){return SkillMechanics.Values(owner,CombatAbility.Fireball,rank);}
        var row=SkillMechanics.For(CombatAbility.Fireball);
        return new(row,key=>key.Equals(row.SourceKey,StringComparison.OrdinalIgnoreCase)?rank:0,_=>0,ClassStats.Level(owner));
    }
    // Prepared item-effect producer. The outer event/property host still owns
    // chance,actual item admission and the protected temporary target frame.
    // Explicit item rank never gains +skills;owner hard synergies/mastery do apply.
    internal static NpcItemSpellEmission EmitPlayerItemFireBall(PlayerMobile owner,Point3D? target,Point3D path,int rank,int outgoing,DateTime at,
        Func<NpcItemMissileRequest,NpcItemSpellAllocation> allocate)
    {
        ArgumentNullException.ThrowIfNull(allocate);_=NpcItemSpellDamage.Require(47);
        if(owner is not{Alive:true,Deleted:false}||owner.Map==null||owner.Map==Map.Internal
            ||ClassCreation.Profiles.TryGet(owner.Serial,out var profile)!=true||!Enum.IsDefined(profile.Class)
            ||rank is <1 or >63||outgoing is <0 or >1||at.Kind!=DateTimeKind.Utc||SpellHelper.IsTown(owner.Location,owner))
        {throw new NotSupportedException("Player item Fire Ball needs its explicit living world owner,rank and caller flag");}
        var before=_pool;int attempts=0;var flights=new List<SorceressSpellFlight>();
        var values=PlayerItemFireBallValues(owner,rank);
        var frame=new NpcItemSpellOrigins((owner.X*2,owner.Y*2),target is{} t?(t.X*2,t.Y*2):null,(path.X*2,path.Y*2),
            checked((int)ClassStats.AttackRating(owner)),false,request=>
        {
            int attempt=attempts++;
            if(request.Skill!=47||request.Rank!=rank||request.Missile!=ItemSkillExecution.MissileId("fireball")
                ||(request.Flags&~0x1000)!=33||request.Velocity!=0||request.ChargedOrdinal!=-1
                ||(request.X&1)!=0||(request.Y&1)!=0||(request.TargetX&1)!=0||(request.TargetY&1)!=0)
            {throw new NotSupportedException("Unqualified player item Fire Ball factory request");}
            var origin=new Point3D(request.X/2,request.Y/2,owner.Z);var aim=new Point3D(request.TargetX/2,request.TargetY/2,owner.Z);
            if(origin.X<=0||origin.Y<=0||origin.X>=owner.Map.Width||origin.Y>=owner.Map.Height
                ||aim.X<=0||aim.Y<=0||aim.X>=owner.Map.Width||aim.Y>=owner.Map.Height
                ||Math.Abs(request.TargetX-request.X)>=100||Math.Abs(request.TargetY-request.Y)>=100||SpellHelper.IsTown(origin,owner)||SpellHelper.IsTown(aim,owner))
            {return null;}
            var allocation=allocate(request);if(!allocation.Allocated){return null;}
            var flight=Prepare(owner,aim,values,at,item:new(profile.Class,origin,allocation.Seed)) with{Id=checked(before.NextId+attempt+1)};
            Validate(flight);flights.Add(flight);return flight;
        },(_,_)=>throw new NotSupportedException("Player Fire Ball has no chain budget"),unitType:0);
        int result=frame.Run(47,rank,outgoing);
        if(!ReferenceEquals(before,_pool)){throw new InvalidOperationException("Player item allocator changed the flight pool reentrantly");}
        var pool=before.Flights;foreach(var flight in flights){pool=pool.Add(flight.Id,Copy(flight));}
        if(attempts!=0){_pool=new(pool,checked(before.NextId+attempts));}
        return new(result,attempts,flights.ConvertAll(f=>f.Id).ToArray());
    }
}
