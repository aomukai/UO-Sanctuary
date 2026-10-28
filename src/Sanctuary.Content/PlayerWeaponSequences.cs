// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace Sanctuary;
internal sealed record PlayerWeaponSequence(string Code,int Sequence,int Points,int Frames,int RawAuxiliary,int[][] Rows);
// Current player file data only. Callers supply the weapon class, advance,
// frame bonus and dispatch. Pose rows do not choose an attack/cast rate.
internal static class PlayerWeaponSequences
{
    private static readonly Lazy<IReadOnlyDictionary<string,PlayerWeaponSequence>> Cleave=new(()=>Load("cleave-sequence",24));
    private static readonly Lazy<IReadOnlyDictionary<string,PlayerWeaponSequence>> Mirrored=new(()=>Load("mirrored-blades-sequence",25));
    private static IReadOnlyDictionary<string,PlayerWeaponSequence> Load(string file,int sequence)
    {
        var rows=BalanceCatalog.Read<PlayerWeaponSequence>(file);
        string[] codes=["HTH","1HT","2HT","1HS","2HS","BOW","XBW","STF","1JS","1JT","1SS","1ST","HT1","HT2"];
        if(rows.Length!=14||!rows.Select(x=>x.Code).SequenceEqual(codes)){throw new InvalidDataException("Missing player weapon sequence profiles");}
        foreach(var r in rows)
        {
            if(r.Sequence!=sequence||r.Points!=r.Frames||r.Points is <16 or >22||r.RawAuxiliary!=256||r.Rows?.Length!=r.Points
                ||r.Rows.Any(x=>x?.Length!=5||x[0]!=0||x[1]!=10||x[3]!=0||x[4] is <0 or >1)||r.Rows.Count(x=>x[4]==1)!=1)
            {throw new InvalidDataException("Unsupported player weapon sequence row");}
        }
        return rows.ToDictionary(x=>x.Code,StringComparer.Ordinal);
    }
    internal static PlayerWeaponSequence Profile(int sequence,string sourceWeaponCode)
    {
        var table=sequence switch{24=>Cleave.Value,25=>Mirrored.Value,_=>throw new NotSupportedException("Unsupported player weapon sequence")};
        if(sourceWeaponCode==null||!table.TryGetValue(sourceWeaponCode,out var r)){throw new NotSupportedException("Unknown source weapon class");}
        return r with{Rows=r.Rows.Select(x=>x.ToArray()).ToArray()};
    }
    internal static SourceAnimationState Schedule(int sequence,string sourceWeaponCode,int advance,int frameBonus,int atFrame)
    {
        var s=Profile(sequence,sourceWeaponCode);
        return SourceAnimationEvents.Initial(new(0,advance,s.Frames*256,s.Rows.Select(x=>x[4]).ToArray(),[]),atFrame,frameBonus);
    }
}
