// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

public static partial class FistsFireRelease
{
    internal static FistsFirePacket CaptureShadow(AssassinShadowBody body,MartialCharge charge)
    {
        if(charge==null){return null;}
        if(charge.Owner!=body.Serial.Value||charge.Ability!=CombatAbility.FistsOfFire||!ShadowSkillCasting.Attached(body)
            ||NecromancerSummons.Source(body.SourceMonsterId,"Crit")!=0)
        {throw new NotSupportedException("Unqualified own Shadow Fists damage source");}
        int rank=Rank(charge,body.OwnedSkillRank(259));
        return Capture(ShadowSkillCasting.OwnValues(body,rank,259),charge.Count,OriginalNpcEquipment.Graph(body).Read(329),n=>NpcEquipmentRandom.Limited(body,n));
    }
    internal static NpcFillPacket InlineShadow(NpcFillPacket fill,FistsFirePacket packet)
    {
        if(packet==null){return fill;}
        // Full prgdam4 runs after weapon fill/critical, then skill roll and
        // source percentage conversion. Converted fire gets no second mastery.
        int converted=Math.Min(SourcePercentage.Calculate(fill.Physical,packet.ConversionPercent,100),fill.Physical);
        return fill with{Physical=checked(fill.Physical-converted),Fire=checked(fill.Fire+packet.Inline256+converted),ResultFlags=fill.ResultFlags|16384};
    }
    internal static void AfterShadow(AssassinShadowBody body,Point3D center,Map map,FistsFirePacket packet,DateTime at)
    {
        if(packet is not{Count:>=2}||!ShadowSkillCasting.Attached(body)||body.Map!=map||map==null||map==Map.Internal
            ||body.ControlMaster is not PlayerMobile owner||SpellHelper.IsTown(center,owner)){return;}
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Shadow Fists release needs UTC");}
        // Callback38 first drains the allocated parent. This method is called
        // only after that attempt; a killed parent still supplies its center.
        var values=ShadowSkillCasting.OwnValues(body,packet.Rank,259);var graph=OriginalNpcEquipment.Graph(body);
        var areaPacket=Capture(values,packet.Count,graph.Read(329),n=>NpcEquipmentRandom.Limited(body,n));
        int pierce=graph.Read(333);Effects.SendLocationEffect(center,map,0x36BD,3); // Identified Fists burst placeholder.
        foreach(var target in SkillCombat.NearbySnapshot<BaseCreature>(map,center,(areaPacket.Radius+1)/2))
        {
            if(target is not{Alive:true,Deleted:false}||!InRadius(center,target,areaPacket.Radius)||!ClassCombat.IsHostile(owner,target)
                ||ClassCombat.Friendly(body,target)||SpellHelper.IsTown(target.Location,owner)){continue;}
            _=NpcElementSpellHit.Deliver(body,owner,target,"fire",areaPacket.Inline256,0,at,"Shadow Fists burst",hitFlags:1,
                pierce:e=>e=="fire"?pierce:0,periodic:true);
        }
        if(packet.Count<3||!ShadowSkillCasting.Attached(body)){return;}
        // Callback39 takes one caster draw, then the existing local scatter;
        // every admitted child receives its separate world/private unit seed.
        int low=unchecked((int)NpcEquipmentRandom.Raw(body));
        values=ShadowSkillCasting.OwnValues(body,packet.Rank,259);graph=OriginalNpcEquipment.Graph(body);
        var fields=Capture(values,packet.Count,graph.Read(329),_=>0);pierce=graph.Read(333);
        foreach(var offset in Scatter(fields.ScatterRadius,low))
        {
            double x=center.X+offset.X/2.0,y=center.Y+offset.Y/2.0;var point=new Point3D((int)Math.Round(x),(int)Math.Round(y),center.Z);
            if(x<0||y<0||x>=map.Width||y>=map.Height||SpellHelper.IsTown(point,owner)){continue;}
            var source=new PhoenixNpcSource(body.Serial.Value,OriginalNpcEquipment.Binding(body),NpcWorldAllocation.UnitSeed(),pierce);
            FireGround.AddShadowFists(body,packet.Rank,x,y,center.Z,fields.FieldMin256,fields.FieldMax256,source,at);
        }
    }
}
