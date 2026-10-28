// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal sealed record BasicMissileInput(bool HasMissile,bool HasRow,int Velocity,int Remaining,int Mode,int ActivateFrame,bool HasFinder);
internal sealed record BasicMissileServices(Func<int> Move,Action<int> WriteRemaining,Func<bool> HasRoom,Func<bool> HasPath,
    Func<int> PathFlags,Func<SourceMissileCell[]> Steps,Func<SourceMissileCell,int> Mask,
    Func<SourceMissileCell,uint> Find,Func<uint,bool,int> Hit);
internal sealed record BasicMissileResult(int Result,int Remaining,bool WroteFrame);

// Complete ordinary do1 collision/update control flow. World movement, room
// masks, ordered candidate selection and hit callbacks are explicit services.
// New Miasma hit41 is not replaced by a generic damage/area handler here.
internal static class BasicMissileUpdate
{
    internal static BasicMissileResult Run(BasicMissileInput s,BasicMissileServices services)
    {
        if(s==null||services==null){throw new ArgumentNullException();}
        if(!s.HasMissile||!s.HasRow||s.Velocity!=0&&services.Move()==2)
        {return new(services.Hit(0,true),s.Remaining,false);}
        int remaining=s.Remaining-1;
        services.WriteRemaining(remaining);
        if(remaining<=0){return new(services.Hit(0,true),remaining,true);}
        if(s.Mode==0){return new(1,remaining,true);}
        if(!services.HasRoom()||!services.HasPath()){return new(2,remaining,true);}
        int flags=services.PathFlags();
        if((flags&5)!=0){return new(2,remaining,true);}
        if(s.Mode==1||remaining>s.ActivateFrame||flags==0){return new(1,remaining,true);}
        if(!s.HasFinder){return new(2,remaining,true);}
        foreach(var cell in services.Steps())
        {
            int mask=services.Mask(cell);if(mask==0){continue;}
            uint target=services.Find(cell);
            if(target!=0){return new(services.Hit(target,false),remaining,true);}
            if((mask&4)!=0){services.Hit(0,true);return new(2,remaining,true);}
        }
        return new(1,remaining,true);
    }
}
