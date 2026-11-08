// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Actual native effects of captured do155. The caller supplies the qualified
// source room/target environment and ordered live roster;this does not invent
// D2 level IDs,pet iteration order or a controller path from native geometry.
internal static class DeathMarkWorldRelease
{
    internal static int RunPrepared(PlayerMobile owner,DeathMarkCastContext context,DeathMarkCastServices environment,
        IReadOnlyList<SkeletonGuardian> orderedPets,DateTime at)
    {
        ArgumentNullException.ThrowIfNull(context);ArgumentNullException.ThrowIfNull(environment);ArgumentNullException.ThrowIfNull(orderedPets);
        if(at.Kind!=DateTimeKind.Utc||owner is not{Alive:true,Deleted:false}||owner.Map==null||owner.Map==Map.Internal
            ||!ClassCreation.Profiles.TryGet(owner.Serial,out var profile)||profile.Class!=DiabloClass.Warlock
            ||context.Caster!=owner.Serial.Value||context.Skill!=375||context.Rank is <1 or >255||context.Position is not{Kind:0,HasPath:true}
            ||context.Position.Coordinates()!=(owner.X*2,owner.Y*2)||context.PetType!=20||context.PetTypeCount<=20
            ||context.State!=212||context.StateCount<=212||context.Selector is <1 or >3)
        {throw new NotSupportedException("Unqualified original Death Mark release context");}
        // Validate the complete supplied roster before any source/world effect.
        var seen=new HashSet<uint>();var pets=new SkeletonGuardian[orderedPets.Count];
        for(int i=0;i<pets.Length;i++)
        {
            var body=orderedPets[i];
            if(body is not{Alive:true,Deleted:false}||!seen.Add(body.Serial.Value)||!WarlockDemonPlacement.Prepared(body)
                ||WarlockDemonPlacement.Owner(body)!=owner.Serial.Value||body.ControlMaster!=owner||body.Map!=owner.Map
                ||DeathMarkWorld.State(body)is not{} saved||saved.Selector!=context.Selector||saved.Map!=owner.Map.MapID)
            {throw new NotSupportedException("Death Mark roster needs distinct original owned demons with registered controller paths");}
            pets[i]=body;
        }
        var row=SkillMechanics.For(CombatAbility.DeathMark);var values=SkillMechanics.Values(owner,CombatAbility.DeathMark,context.Rank);
        if(row.SourceId!=375||row.Number("srvdofunc")!=155||row.Text("pettype")!="demon"||row.Text("auratargetstate")!="death_mark"
            ||row.Text("auraevent1") is not("" or "0" or "-1")||row.Text("auraevent2") is not("" or "0" or "-1")
            ||row.Text("auraevent3") is not("" or "0" or "-1")||row.Text("auraevent4") is not("" or "0" or "-1"))
        {throw new NotSupportedException("Unqualified Death Mark row/events");}
        DeathMarkDebuff pending=null;Mobile recipient=null;int write=0;
        return DeathMarkCast.Run(context,environment with
        {
            Formula=field=>checked((int)values.Eval(field switch{0x194=>"calc2",0x190=>"calc1",0x84=>"aurarangecalc",0x80=>"auralencalc",
                _=>throw new NotSupportedException("Unexpected Death Mark formula field")})),
            VisitPets=(type,directive)=>
            {
                if(type!=20){throw new NotSupportedException("Prepared world release only admits the qualified demon roster");}
                foreach(var body in pets){DeathMarkWorld.ApplyPrepared(body,directive,at);}
            },
            AllocateState=request=>
            {
                if(request.Caster!=owner.Serial.Value||request.Target.Kind!=1||request.Target.IsCaster||request.Skill!=375||request.Rank!=context.Rank
                    ||request.State!=212||request.Stat!=-1||request.Value!=0||request.Duration!=DeathMarkDebuffs.DurationFrames(context.Rank))
                {throw new NotSupportedException("Unqualified Death Mark recipient allocation");}
                recipient=World.FindMobile((Serial)request.Target.Id);
                pending=DeathMarkDebuffs.Prepare(owner,recipient,request.Rank,at);return pending!=null;
            },
            BindState=()=>DeathMarkDebuffs.BindPrepared(owner,recipient,pending),
            ApplyAura=()=>{}, // Owned row375 has no aura events;numeric binding is the approved adaptation above.
            WriteStat=(stat,value)=>
            {
                int expectedStat=write switch{0=>350,1=>351,2=>202,_=>throw new NotSupportedException("Duplicate Death Mark metadata write")};
                int expectedValue=write switch{0=>375,1=>context.Rank,_=>context.Context12c};
                if(stat!=expectedStat||value!=expectedValue){throw new NotSupportedException("Death Mark metadata ordering changed");}
                if(stat==202){DeathMarkDebuffs.WriteCastId(recipient,value);}write++;
            }
        });
    }
}
