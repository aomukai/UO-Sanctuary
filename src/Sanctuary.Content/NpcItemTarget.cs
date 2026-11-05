// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;

namespace Sanctuary;

// A synchronous logical monster reference, not a new mobile or persisted AI
// target. Native death can internalize/delete the mobile while its existing
// source corpse identity still represents the same registered monster.
internal sealed class NpcItemTarget
{
    internal BaseCreature Original { get; }
    internal int Id { get; }
    private readonly Map WorldMap;
    private NpcItemTarget(BaseCreature target)
    { Original=target;Id=checked((int)target.Serial.Value);WorldMap=target.Map; }
    internal static NpcItemTarget Capture(BaseCreature target)
    {
        if(target is not{Alive:true,Deleted:false}||target.Map==null||target.Map==Map.Internal
            ||World.FindMobile(target.Serial)!=target)
        {throw new NotSupportedException("NPC item target capture requires its registered living monster");}
        return new(target);
    }
    internal Point3D Point(Map map)
    {
        if(map==null||map==Map.Internal||map!=WorldMap)
        {throw new NotSupportedException("NPC item target left its synchronous world context");}
        var current=World.FindMobile((Serial)(uint)Id);
        if(current!=null&&current!=Original)
        {throw new NotSupportedException("NPC item target registration changed identity");}
        if(Original is{Alive:true,Deleted:false})
        {
            if(current!=Original||Original.Map!=map)
            {throw new NotSupportedException("NPC item target has no matching world registration");}
            return Original.Location;
        }
        var corpse=SourceCorpseIdentity.Find(Id)?.Body;
        if(corpse is not{Deleted:false}||corpse.Map!=map)
        {throw new NotSupportedException("Removed NPC item target needs its actual path fallback;no source corpse identity remains");}
        return corpse.Location;
    }
}
