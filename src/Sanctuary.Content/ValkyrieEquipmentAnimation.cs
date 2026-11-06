// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace Sanctuary;

internal sealed record ValkyrieAnimationRow(string Code,int Frames,int Speed,int[][] Events);
internal sealed record ValkyrieAnimationData(string ReferenceVersion,int State,int GraphicsType,int GraphicsClass,int WalkVelocity,int RunVelocity,ValkyrieAnimationRow[] Rows);
internal sealed record ValkyrieAnimationPlan(int ActorMode,int GraphicsMode,string Code,int Speed,int FrameBonus,int Velocity,SourceAnimationEvent[] Events);

// Read-only original-item timing preparation. The caller must supply source
// rate bases and external stat-list contributions,including a running state's
// velocity modifier. This is not permission to infer an initializer or activate
// a world controller from native movement/facing flags.
internal static class ValkyrieEquipmentAnimation
{
    private static readonly Lazy<Dictionary<string,ValkyrieAnimationRow>> Rows=new(()=>
    {
        var data=BalanceCatalog.ReadObject<ValkyrieAnimationData>("valkyrie-animation");
        if(data is not{ReferenceVersion:"3.3.93847",State:93,GraphicsType:2,GraphicsClass:0,WalkVelocity:6,RunVelocity:9,Rows.Length:4})
        {throw new InvalidDataException("Unsupported current Valkyrie graphics transformation");}
        var rows=data.Rows.ToDictionary(r=>r.Code,StringComparer.Ordinal);
        foreach(var expected in new[]{("AMA12HT",18,256,11),("AMWL2HT",8,256,-1),("AMRN2HT",8,256,-1),("AMNU2HT",8,128,-1)})
        {
            if(!rows.TryGetValue(expected.Item1,out var row)||row.Frames!=expected.Item2||row.Speed!=expected.Item3||row.Events==null
                ||expected.Item4<0&&row.Events.Length!=0||expected.Item4>=0&&(row.Events.Length!=1||row.Events[0]is not{Length:2}||row.Events[0][0]!=expected.Item4||row.Events[0][1]!=1))
            {throw new InvalidDataException("Unsupported current Valkyrie transformed animation row");}
        }
        return rows;
    });
    internal static ValkyrieAnimationPlan Plan(AmazonValkyrieBody body,int mode,int baseAttackRate,int baseVelocityPercent,int frame=0)
    {
        if(mode is not(2 or 4 or 15)||baseAttackRate is <-10000 or >10000||baseVelocityPercent is <-10000 or >10000)
        {throw new NotSupportedException("Valkyrie animation needs an admitted source mode and explicit rate bases");}
        if(frame<0||frame>(int.MaxValue>>8)-32){throw new ArgumentOutOfRangeException(nameof(frame));}
        var binding=ValkyrieEquipmentWeapons.Binding(body);var weapon=ValkyrieEquipmentWeapons.Frame(body);
        var original=ValkyrieEquipment.Originals(body).Single(i=>i.Serial.Value==binding.Primary);
        if(binding.Secondary!=0||weapon.Selected!=binding.Primary||GeneratedNpcEquipment.WieldType(body)!=2
            ||WeaponAnimationClasses.For(original.Snapshot.Metadata.Code,true)!="2HT")
        {throw new NotSupportedException("Valkyrie timing needs its original two-handed spear composite");}
        var graph=body.EquipmentNumbers;string code=mode==4?"AMA12HT":mode==2?"AMWL2HT":"AMRN2HT";var row=Rows.Value[code];
        if(mode==4)
        {
            int speed=SourceAttackRate.Speed(row.Speed,graph.Read(93),checked(baseAttackRate+graph.Read(68)));
            var flags=new int[144];foreach(var flag in row.Events){flags[flag[0]]=flag[1];}
            // The full older frame-bonus getter converts monster A1 to
            // Amazon A1 and returns the spear's two-frame starting bonus.
            var plan=SourceAnimationEvents.Initial(new(0,speed,row.Frames*256,flags,[]),frame,2);
            return new(mode,7,code,speed,2,0,plan.Events);
        }
        // Converted player locomotion uses the source player base animation
        // rates213/101,not the256metadata speed or the monster's Velocity11.
        var movement=SourceActorMotion.Speed(6,mode==2?213:101,checked(baseVelocityPercent+graph.Read(67)),graph.Read(96));
        return new(mode,mode==2?2:3,code,movement.Animation,0,movement.Velocity,[]);
    }
}
