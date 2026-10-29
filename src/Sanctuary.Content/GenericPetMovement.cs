// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
namespace Sanctuary;

internal sealed record GenericPetMovementEvent(int Identity,byte Type,int Frame);
// Neutral source fields: no invented labels for movement controls or path flags.
internal sealed class GenericPetMovementUnit
{
    internal int Kind=1,TableSelector=1,Word20,Word24,Aux88Word1c;
    internal bool HasMonsterData=true,HasMovementData=true,HasAiControl=true,HasPath=true,HasAux88;
    internal byte Byte28,PathByteBf;
    internal uint Aux88FlagsB34;
    internal ushort AiWord38;
    internal readonly List<GenericPetMovementEvent> Events=[];
}
internal sealed record GenericPetMovementServices(Func<int,int> TableWord2d0,
    Func<int,GenericPetModeRequest> InitializeMode,Func<GenericPetModeRequest,int> ExecuteMode,
    Action<int> RemoveEvent,Action<int,int,int,int,int> ScheduleEvent,Action<byte> Wander,Action<int> Idle);
internal static class GenericPetMovement
{
    internal static void Prepare(GenericPetMovementUnit pet,int speed,int tuning,int options)
    {
        if(pet is not{Kind:1,HasMonsterData:true,HasMovementData:true})
        {throw new NotSupportedException("Current movement preparation requires monster movement data");}
        pet.Word20=0;pet.Word24=0;pet.Byte28=0;
        int mode=options==1?7:options;
        if(mode!=0){pet.Word20=mode;}
        if(speed!=0){pet.Word24=speed;}
        byte value=unchecked((byte)tuning);if(value!=0){pet.Byte28=Math.Min(value,(byte)77);}
    }
    internal static void EnsureThink(GenericPetMovementUnit pet,int frame,GenericPetMovementServices s)
    {
        int earliest=0;
        if(pet!=null)
        {
            foreach(var e in pet.Events)
            {if(e.Type==2&&e.Frame>0&&(earliest==0||e.Frame<earliest)){earliest=e.Frame;}}
        }
        int deadline=unchecked(frame+50);
        if(earliest<=0||earliest>deadline){s.ScheduleEvent(2,deadline,0,0,0);}
    }
    internal static int Execute(GenericPetMovementUnit pet,GenericPetActionRandom random,int mode,int target,int x,int y,int pathByte,int flags,GenericPetMovementServices s)
    {
        if(pet==null){throw new NotSupportedException("Current movement execution requires a unit");}
        if(mode==15&&pet.Kind is 0 or 1 or 3)
        {
            if(pet.TableSelector is <1 or >3){throw new NotSupportedException("Current movement table selector requires1..3");}
            if(s.TableWord2d0(pet.TableSelector)>60&&pet.HasAux88&&pet.Aux88Word1c<0&&(pet.Aux88FlagsB34&0x10000000)!=0)
            {
                if(pet is not{Kind:1,HasMonsterData:true,HasMovementData:true})
                {throw new NotSupportedException("Current movement fallback requires monster movement data");}
                pet.Word20=0;pet.Word24=0;pet.Byte28=0;mode=2;
            }
        }
        if(pet.Kind==5){throw new NotSupportedException("Current movement execution rejects unit type5");}
        if(pet.HasPath)
        {
            byte value=unchecked((byte)(pathByte-1));pet.PathByteBf=value<=18?value:(byte)0;
        }
        var request=s.InitializeMode(mode)??throw new NotSupportedException("Missing native movement mode request");
        request=target!=0?request with{Target=target}:request with{X=x,Y=y};
        int result=s.ExecuteMode(request);
        if(result!=0){return result;}
        byte options=unchecked((byte)flags);
        if((options&4)!=0)
        {
            // The source saves next before removal. Services may mutate the
            // live queue; snapshot identities retain duplicate event entries.
            var events=pet.Events.ToArray();
            foreach(var e in events){if(e.Type==2){s.RemoveEvent(e.Identity);}}
        }
        if((options&1)!=0&&pet.Kind==1&&pet.HasMonsterData&&pet.HasAiControl){pet.AiWord38|=0x40;}
        if((options&2)!=0)
        {
            uint draw=SourceRandom.Roll(ref random.Seed);
            if(draw%100<70){s.Wander(4);}else{s.Idle(10);}
            return 1;
        }
        return result;
    }
    internal static bool Approach(GenericPetMovementUnit pet,GenericPetActionRandom random,int frame,int target,int hint,GenericPetMovementServices s)
    {
        foreach(var(tuning,options) in new (int,int)[]{(0,0),(100,0),(100,15),(100,1)})
        {
            Prepare(pet,0,tuning,options);
            // Fifth source argument is unwritten by the caller and unread by
            // the complete callee. It has no invented value or gameplay role.
            int mode=hint==1?15:2;
            if(Execute(pet,random,mode,target,0,0,1,4,s)==0){continue;}
            EnsureThink(pet,frame,s);return true;
        }
        return false;
    }
    internal static GenericPetActionServices Bind(GenericPetActionServices actions,GenericPetMovementUnit pet,GenericPetActionRandom random,Func<int> frame,GenericPetMovementServices services)
        =>actions with{ResetMove=()=>Prepare(pet,0,0,0),MoveFinished=()=>EnsureThink(pet,frame(),services),Approach=(target,hint)=>Approach(pet,random,frame(),target,hint?1:0,services)};
}
