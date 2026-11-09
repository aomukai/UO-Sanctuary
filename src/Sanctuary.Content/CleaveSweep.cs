// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal sealed record CleaveSweepInput(bool HasActor,int Kind,bool HasPath,int X,int Y,int Skill,int Rank,int Flags,int Filter,int SourceDamage);
internal sealed record CleaveSweepRequest(int X,int Y,int CenterX,int CenterY,int Skill,int Rank,int Start,int End,int Extra,int SourceDamage,int Radius,int Filter);
internal sealed record CleaveSweepResult(int Result,int Flags);
internal sealed record CleaveSweepServices(Func<bool> Row,Func<(bool Found,int X,int Y)> Target,Action<int> WriteFlags,Func<bool> Delayed,
    Func<SourceGridPoint,SourceGridPoint,int> Heading,Func<int,int> Formula,Func<int> Reach,Func<SourceGridPoint> Center,Action<CleaveSweepRequest> Area);

// Complete current do164 outer control flow. The target/direction/weapon reach
// and ordered area/recipient services are explicit; this never guesses the
// protected450780 contact callback or start68 input/payment behavior.
internal static class CleaveSweep
{
    // Actual mapped player position/weapon/rank binding for prepared requests.
    // Target acquisition and the protected math contracts remain supplied;
    // this does not install start68,hit selection,payment or a learned action.
    internal static CleaveSweepRequest PrimaryPrepared(Server.Mobiles.PlayerMobile owner,Server.Point3D aim,SourceDirectionServices direction,CleaveValues captured=null)
    {
        if(owner is not{Alive:true,Deleted:false}||owner.Map==null||owner.Map==Server.Map.Internal||!PlayerMeleeGeometry.WarlockMelee(owner))
        {throw new NotSupportedException("Cleave world planner requires a live mapped Warlock melee context");}
        var values=SkillMechanics.Values(owner,CombatAbility.Cleave);_=CleavePrepared.Capture(values);
        int FormulaValue(int field)=>captured==null?Formula(values,field):field switch
        {0x1a0=>captured.RawSlices,0x1a4=>30,0x1a8=>0,_=>throw new NotSupportedException("Unknown captured Cleave formula")};
        var row=SkillMechanics.For(CombatAbility.Cleave);var center=PetOwnerRoutes.Cell(owner);CleaveSweepRequest request=null;
        Run(new(true,0,true,center.X,center.Y,row.SourceId,captured?.Rank??values.Rank,0,checked((int)row.Number("aurafilter")),checked((int)row.Number("SrcDam"))),
            new(()=>true,()=>(true,aim.X*2,aim.Y*2),_=>{},()=>false,
                (a,b)=>SourceDirection64.Calculate(a,b,direction).Heading,FormulaValue,()=>PlayerMeleeGeometry.Reach(owner),()=>new(center.X,center.Y),r=>request=r));
        return request;
    }
    internal static int Formula(SkillValues values,int field)
    {
        _=CleavePrepared.Capture(values);
        return checked((int)values.Eval(field switch{0x1a0=>"calc5",0x1a4=>"calc6",0x1a8=>"calc7",_=>throw new NotSupportedException("Unknown Cleave sweep formula field")}));
    }
    private static SourceGridPoint Fixed(int x,int y)=>new(unchecked((x<<16)+0x8000),unchecked((y<<16)+0x8000));
    internal static CleaveSweepResult Run(CleaveSweepInput input,CleaveSweepServices services)
    {
        ArgumentNullException.ThrowIfNull(input);ArgumentNullException.ThrowIfNull(services);
        int flags=input.Flags;
        if(!services.Row()){return new(0,flags);}
        var target=services.Target();if(!target.Found){return new(0,flags);}
        if(input.HasActor){flags|=0x40;services.WriteFlags(flags);}
        if(services.Delayed()){return new(1,flags);}
        if(!input.HasActor||input.Kind is 2 or 4 or 5&&!input.HasPath)
        {throw new NotSupportedException("Invalid current Cleave actor/path context");}
        int x=input.HasPath?input.Kind is 2 or 4 or 5?input.X:input.X&65535:0;
        int y=input.HasPath?input.Kind is 2 or 4 or 5?input.Y:input.Y&65535:0;
        int heading=services.Heading(Fixed(x,y),Fixed(target.X,target.Y));
        if(heading is <0 or >63){throw new NotSupportedException("Unqualified Cleave direction supplier");}
        int width=Math.Max(services.Formula(0x1a0),0),total=Math.Max(services.Formula(0x1a4),0);
        if(width==0||total==0){return new(1,flags);}
        int start=heading,end=heading;
        if(width<total)
        {
            // Keep the current divss/mulss/mulss/subss/addss/trunc sequence.
            float half=(float)width/total;half*=64f;half*=.5f;
            start=(int)((float)heading-half)%64;end=(int)((float)heading+half)%64;
            if(start<0){start+=64;}if(end<0){end+=64;}
        }
        int extra=services.Formula(0x1a8);
        if(input.SourceDamage is <0 or >255){throw new NotSupportedException("Cleave source damage byte");}
        int sourceDamage=input.SourceDamage==0?128:input.SourceDamage;
        int radius=unchecked(services.Reach()+4+extra);var center=services.Center();
        services.Area(new(x,y,center.X,center.Y,input.Skill,input.Rank,start,end,extra,sourceDamage,radius,input.Filter));
        return new(1,flags);
    }
}
