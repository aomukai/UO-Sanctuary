// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal sealed record ShadowFuryPacket(uint Actor,string Binding,WeaponMissileDamage Damage,int TargetArmorDelta,int Flags);

// Own original equipment -> existing missile creator arithmetic. Critical
// selection is an explicit caller input,not an invented NPC RNG policy.
// Startup/state/timing,missile allocation and impact remain separate stages.
internal static class ShadowFuryPackets
{
    internal static ItemExecutionMetadata Row()
    {
        var r=ItemSkillExecution.Skill(266);
        if(r is not{Start:26,Do:48,UseManaOnDo:true,InTown:false,Lob:false,DecrementQuantity:false,Missile:-1}
            ||r.Text("srvmissilea")!="bladefragment1"||r.Text("monanim")!="SC"||ShadowSkillBook.Metadata(266).Mode!=7
            ||r.Number("SrcDam")!=96||r.Text("ToHitCalc")!="lvl*10"||r.Number("Param4")!=5||r.Number("startmana")!=6)
        {throw new NotSupportedException("Unqualified own Shadow Fury packet row");}
        return r;
    }
    internal static ShadowFuryPacket Capture(AssassinShadowBody body,int rank,bool critical)
    {
        _=Row();_=ShadowEquipmentDefense.Require(body,false);
        if(body is not{Alive:true,Deleted:false}||body.ControlMaster==null||OriginalNpcEquipment.Owner(body)!=body.ControlMaster.Serial.Value||body.OwnedSkills.Used!=266||rank!=body.OwnedSkillRank(266)||rank is <1 or >255||ShadowEquipmentDefense.Composite(body)!=13)
        {throw new NotSupportedException("Own Fury capture needs its intrinsic rank and original active dual claws");}
        var graph=OriginalNpcEquipment.Graph(body);var binding=ShadowLoadout.Binding(body);
        var frame=graph.Weapons(binding.Primary,binding.Secondary,false,false,0,0,0);frame.Enter();
        try
        {
            // Current admitted Shadows have actual original primary claws.
            // The source monster branch uses that item's actor endpoint lane;
            // it does not acquire the controlling player's weapon/passives.
            var p=frame.Physical(0,2,checked(CombatAuras.DamagePercent(body)+NecromancerEffects.DamageStatPercent(body)));
            if(!p.Weapon||frame.Selected!=binding.Primary){throw new NotSupportedException("Fury NPC weapon selection is outside its original primary-claw admission");}
            int percent=checked(p.DamagePercent+Math.Max(p.MinimumPercent,p.MaximumPercent)
                +p.Strength*p.StrengthCoefficient/100+p.Dexterity*p.DexterityCoefficient/100);
            int poisonLength=graph.Read(101);bool overridden=poisonLength>0;
            var raw=new MissileSourceStats(checked(graph.Read(48)*256),checked(graph.Read(49)*256),checked(graph.Read(50)*256),checked(graph.Read(51)*256),
                checked(graph.Read(52)*256),checked(graph.Read(53)*256),checked(graph.Read(54)*256),checked(graph.Read(55)*256),graph.Read(56),
                graph.Read(57),graph.Read(58),overridden?poisonLength:graph.Read(59),overridden?0:graph.Read(326),graph.Read(60),graph.Read(62),graph.Read(316),graph.Read(317),graph.Read(315));
            raw=NpcEquipmentElements.WithBonuses(body,raw,0,0,0,0).Stats;
            var damage=BladeFuryLaunch.FromSource(ShadowSkillCasting.OwnValues(body,rank,266),checked(p.Minimum*256),checked(p.Maximum*256),false,
                percent,graph.Read(121),checked(graph.Read(122)+(frame.SelectedBlunt?50:0)),critical,raw);
            int flags=1|(critical?2:0)|(graph.Read(103)!=0?256:0)|(graph.Read(104)!=0?512:0)|(graph.Read(106)!=0?1024:0);
            // Complete creator reads DAMAGETARGETAC twice in this branch.
            return new(body.Serial.Value,binding.Identity,damage,checked(graph.Read(120)*2),flags);
        }
        finally{frame.Restore();}
    }
}
