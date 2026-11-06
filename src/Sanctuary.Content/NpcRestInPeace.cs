// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary;

public sealed class RestInPeaceProfiles:GenericPersistence
{
    private uint[] _snapshot;
    public RestInPeaceProfiles():base("SanctuaryRestInPeace",241){}
    public override void Serialize(){_snapshot=NpcRestInPeace.Snapshot();base.Serialize();}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(_snapshot));}
    public override void Deserialize(IGenericReader reader)
    {
        if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Rest in Peace state version");}
        NpcRestInPeace.Restore(JsonSerializer.Deserialize<uint[]>(reader.ReadString()));
    }
}

public static class NpcRestInPeace
{
    // Source callback29 marks the original unit before death mode creates its
    // native loot container. Preserve that state if death completion is delayed.
    private static HashSet<uint> _marked=[];
    public static RestInPeaceProfiles Profiles{get;private set;}
    public static void Configure()=>Profiles=new();
    internal static bool Marked(BaseCreature target)=>target!=null&&_marked.Contains(target.Serial.Value);
    internal static bool Apply(BaseCreature source,BaseCreature target)
    {
        if(source==null||target==null){return false;}
        if(target.Deleted){throw new NotSupportedException("Rest in Peace needs the original registered NPC unit");}
        _marked.Add(target.Serial.Value);return true;
    }
    internal static void CaptureDeath(BaseCreature target,Corpse corpse)
    {
        if(!Marked(target)){return;}
        if(corpse is not{Deleted:false}||corpse.Owner!=target)
        {throw new NotSupportedException("Rest in Peace corpse transfer lost the original unit");}
        corpse.SanctuaryRestInPeace=true;_marked.Remove(target.Serial.Value);
    }
    internal static uint[] Snapshot()
    {
        _marked.RemoveWhere(id=>World.FindMobile((Serial)id) is not BaseCreature{Deleted:false});
        return _marked.Order().ToArray();
    }
    internal static void Restore(uint[] saved)
    {
        if(saved==null||saved.Any(id=>id==0||!((Serial)id).IsMobile)||saved.Distinct().Count()!=saved.Length)
        {throw new InvalidDataException("Invalid Rest in Peace unit state");}
        _marked=new(saved);
    }
}
