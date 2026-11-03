// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
namespace Sanctuary;

internal static class GenericPetFollowMovement
{
    internal static (int X,int Y) Destination(GenericPetDecisionUnit owner,Func<bool> moving,Func<(int X,int Y)> pathDestination)
    {
        if((uint)owner.Kind>1){return(0,0);}
        if(!moving()){return owner.Coordinates();}
        if(!owner.HasPath){throw new NotSupportedException("Current moving owner destination requires a path");}
        var(x,y)=pathDestination();return(unchecked((ushort)x),unchecked((ushort)y));
    }
    internal static int Point(GenericPetMovementUnit pet,int x,int y,GenericPetMovementServices s)
    {
        if(pet==null||pet.Kind==5){throw new NotSupportedException("Current fixed-point movement requires a non-tile unit");}
        if(pet.HasPath){pet.PathByteBf=0;}
        var request=s.InitializeMode(2)??throw new NotSupportedException("Missing current point mode request");
        int result=s.ExecuteMode(request with{X=x,Y=y});
        if(result==0)
        {foreach(var e in pet.Events.ToArray()){if(e.Type==2){s.RemoveEvent(e.Identity);}}}
        return result;
    }
    internal static int RandomPoint(GenericPetMovementUnit pet,GenericPetActionRandom random,int x,int y,byte radius,int hint,ushort flags,GenericPetMovementServices s)
    {
        if(pet==null){throw new NotSupportedException("Current random-point movement requires a unit");}
        bool side=(SourceRandom.Roll(ref random.Seed)&1)!=0;int a,b;
        if(side){a=radius;b=radius==0?0:(int)(SourceRandom.Roll(ref random.Seed)%radius);}
        else{a=radius==0?0:(int)(SourceRandom.Roll(ref random.Seed)%radius);b=radius;}
        if((SourceRandom.Roll(ref random.Seed)&1)!=0){a=-a;}
        if((SourceRandom.Roll(ref random.Seed)&1)!=0){b=-b;}
        return GenericPetMovement.Execute(pet,random,hint==1?15:2,0,unchecked(x+a),unchecked(y+b),1,flags,s);
    }
    internal static GenericPetFollowExecutionServices Bind(GenericPetFollowExecutionServices leaves,GenericPetMovementUnit pet,
        GenericPetActionRandom random,Func<int> frame,GenericPetMovementServices movement,Func<int,int,byte,int,ushort,int> towardPoint)
    {
        void Prepare(int speed,byte tuning,int options)=>GenericPetMovement.Prepare(pet,speed,tuning,options);
        void Think()=>GenericPetMovement.EnsureThink(pet,frame(),movement);
        int Finish(int result){if(result==0){return 0;}Think();return 1;}
        return leaves with
        {
            Prepare=Prepare,EnsureThink=Think,Point=(x,y)=>Point(pet,x,y,movement),
            Move=(x,y,radius,hint,flags)=>RandomPoint(pet,random,x,y,radius,hint,flags,movement),
            Bound=(x,y,hint,radius,speed,tuning,options)=>
            {Prepare(speed,tuning,options);return Finish(RandomPoint(pet,random,x,y,radius,hint,4,movement));},
            Alternate=(x,y,hint,speed,tuning,options)=>
            {Prepare(speed,tuning,options);return Finish(hint==1?GenericPetMovement.Execute(pet,random,15,0,x,y,1,4,movement):Point(pet,x,y,movement));},
            // Explicit leaf contract retained for isolated fixtures. The
            // checked425bb0body is supplied by GenericPetMotionServices.Bind.
            Toward=(x,y,radius,tuning)=>{Prepare(0,tuning,0);return Finish(towardPoint(x,y,radius,0,4));}
        };
    }
}
