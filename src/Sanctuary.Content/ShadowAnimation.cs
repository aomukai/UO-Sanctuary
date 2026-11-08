// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
namespace Sanctuary;

internal sealed record ShadowAnimationData(string ReferenceVersion,int State,int GraphicsType,int GraphicsClass,
    int ActorMode,int GraphicsMode,ValkyrieAnimationRow[] Rows,ValkyrieAnimationRow[] MeleeRows);
internal sealed record ShadowAnimationPlan(int Speed,SourceAnimationEvent[] Events);
internal sealed record ShadowBuffAnimationData(string ReferenceVersion,ValkyrieAnimationRow[] Rows);
internal sealed record ShadowChargeAnimationData(string ReferenceVersion,int State,int ActorMode,int GraphicsMode,ValkyrieAnimationRow[] Rows);
// Both species use state119's Assassin graphics. Rate and event planning are
// the existing source services; original dual-hand stats supply their inputs.
internal static class ShadowAnimation
{
    private static readonly Lazy<ValkyrieAnimationRow> Row=new(()=>
    {
        var data=BalanceCatalog.ReadObject<ShadowAnimationData>("shadow-animation");
        if(data is not{ReferenceVersion:"3.3.93847",State:119,GraphicsType:2,GraphicsClass:6,ActorMode:9,GraphicsMode:14,Rows.Length:3}
            ||!data.Rows.Select(r=>r.Code).Order().SequenceEqual(new[]{"AIS2HT1","AIS2HT2","AIS2HTH"})
            ||data.Rows.Any(r=>r.Frames!=8||r.Speed!=128||r.Events is not{Length:1}||!r.Events[0].SequenceEqual(new[]{4,2})))
        {throw new InvalidDataException("Unsupported owned Shadow S2 transformation/animation");}
        return data.Rows.Single(r=>r.Code=="AIS2HT2");
    });
    private static readonly Lazy<ValkyrieAnimationRow> MeleeRow=new(()=>
    {
        var data=BalanceCatalog.ReadObject<ShadowAnimationData>("shadow-animation");_=Row.Value;
        if(data.MeleeRows is not{Length:3}||!data.MeleeRows.Select(r=>r.Code).Order().SequenceEqual(new[]{"AIKKHT1","AIKKHT2","AIKKHTH"})
            ||data.MeleeRows.Any(r=>r.Frames!=13||r.Speed!=256||r.Events is not{Length:1}||!r.Events[0].SequenceEqual(new[]{4,1})))
        {throw new InvalidDataException("Unsupported owned Shadow converted A1/kick graphics");}
        return data.MeleeRows.Single(r=>r.Code=="AIKKHT2");
    });
    private static readonly Lazy<ValkyrieAnimationRow> BuffRow=new(()=>
    {
        var data=BalanceCatalog.ReadObject<ShadowBuffAnimationData>("shadow-buff-animation");
        if(data is not{ReferenceVersion:"3.3.93847",Rows.Length:3}
            ||!data.Rows.Select(r=>r.Code).Order().SequenceEqual(new[]{"AISCHT1","AISCHT2","AISCHTH"})
            ||data.Rows.Any(r=>r.Frames!=17||r.Speed!=256||r.Events is not{Length:1}||!r.Events[0].SequenceEqual(new[]{9,1})))
        {throw new InvalidDataException("Unsupported owned Shadow defensive cast animation");}
        return data.Rows.Single(r=>r.Code=="AISCHT2");
    });
    private static readonly Lazy<ValkyrieAnimationRow> ChargeRow=new(()=>
    {
        var data=BalanceCatalog.ReadObject<ShadowChargeAnimationData>("shadow-charge-animation");
        if(data is not{ReferenceVersion:"3.3.93847",State:119,ActorMode:5,GraphicsMode:8,Rows.Length:3}
            ||!data.Rows.Select(r=>r.Code).Order().SequenceEqual(new[]{"AIA2HT1","AIA2HT2","AIA2HTH"})
            ||data.Rows.Any(r=>r.Frames!=12||r.Speed!=(r.Code=="AIA2HTH"?256:227)||r.Events is not{Length:1}||!r.Events[0].SequenceEqual(new[]{6,1})))
        {throw new InvalidDataException("Unsupported owned Shadow charge A2 animation");}
        return data.Rows.Single(r=>r.Code=="AIA2HT2");
    });
    internal static ShadowAnimationPlan Supplied(int ias,int attack,int rightRate,int leftRate,int frame=0)
        =>SuppliedRow(Row.Value,ias,attack,rightRate,leftRate,frame);
    internal static ShadowAnimationPlan SuppliedMelee(int ias,int attack,int rightRate,int leftRate,int frame=0)
        =>SuppliedRow(MeleeRow.Value,ias,attack,rightRate,leftRate,frame);
    internal static ShadowAnimationPlan SuppliedBuff(int ias,int attack,int rightRate,int leftRate,int frame=0)
        =>SuppliedRow(BuffRow.Value,ias,attack,rightRate,leftRate,frame);
    internal static ShadowAnimationPlan SuppliedCharge(int ias,int attack,int rightRate,int leftRate,int frame=0)
        =>SuppliedRow(ChargeRow.Value,ias,attack,rightRate,leftRate,frame);
    private static ShadowAnimationPlan SuppliedRow(ValkyrieAnimationRow row,int ias,int attack,int rightRate,int leftRate,int frame)
    {
        int correction=checked((rightRate+leftRate)/2-rightRate);
        int speed=SourceAttackRate.Speed(row.Speed,ias,checked(attack+correction));
        var flags=new int[144];foreach(var e in row.Events){flags[e[0]]=e[1];}
        return new(speed,SourceAnimationEvents.Initial(new(0,speed,row.Frames*256,flags,[]),frame).Events);
    }
    internal static ShadowAnimationPlan FireBlast(AssassinShadowBody body)
        =>body.OwnedSkills.Used==251?S2(body):throw new NotSupportedException("Shadow has no used Fire Blast");
    internal static ShadowAnimationPlan S2(AssassinShadowBody body)
    {
        _=ShadowEquipmentDefense.Require(body);
        if(body.OwnedSkills.Used is not{} id||!(id==251||ShadowSentries.Handles(id))||body.OwnedSkills.Find(id)?.Mode!=9||ShadowEquipmentDefense.Composite(body)!=13)
        {throw new NotSupportedException("Shadow S2 needs its qualified intrinsic skill and original active dual claws");}
        return Original(body,Row.Value);
    }
    internal static ShadowAnimationPlan Melee(AssassinShadowBody body)
    {
        _=ShadowEquipmentDefense.Require(body);
        if(body.OwnedSkills.Used!=0||ShadowEquipmentDefense.Composite(body)!=13)
        {throw new NotSupportedException("Shadow A1 needs its own Attack and original active dual claws");}
        return Original(body,MeleeRow.Value);
    }
    internal static ShadowAnimationPlan Charge(AssassinShadowBody body)
    {
        _=ShadowEquipmentDefense.Require(body);
        if(body.OwnedSkills.Used is not{} id||!ShadowChargeStrikes.Handles(id)||body.OwnedSkills.Find(id)?.Mode!=5||ShadowEquipmentDefense.Composite(body)!=13)
        {throw new NotSupportedException("Shadow A2 needs its own charge skill and original active dual claws");}
        return Original(body,ChargeRow.Value);
    }
    internal static ShadowAnimationPlan Claws(AssassinShadowBody body)
    {
        _=ShadowEquipmentDefense.Require(body);
        if(body.OwnedSkills.Used is not{} id||!ShadowClawSequence.Handles(id)||body.OwnedSkills.Find(id)?.Mode!=14||ShadowEquipmentDefense.Composite(body)!=13)
        {throw new NotSupportedException("Shadow sequence needs its own elemental skill and original dual claws");}
        var r=OriginalRates(body);return SuppliedClaws(r.Ias,r.Attack,r.Right,r.Left);
    }
    internal static ShadowAnimationPlan SuppliedClaws(int ias,int attack,int rightRate,int leftRate)
        =>ShadowClawSequence.Plan(SourceAttackRate.Speed(256,ias,checked(attack+(rightRate+leftRate)/2-rightRate)));
    internal static ShadowAnimationPlan Buff(AssassinShadowBody body)
        =>body.OwnedSkills.Used is{} id&&ShadowDefensiveBuffs.Handles(id)?Cast(body):throw new NotSupportedException("Shadow has no used defensive buff");
    internal static ShadowAnimationPlan Cast(AssassinShadowBody body)
    {
        _=ShadowEquipmentDefense.Require(body);
        if(body.OwnedSkills.Used is not{} id||!(id is 253 or 273 or 277||ShadowDefensiveBuffs.Handles(id))||body.OwnedSkills.Find(id)?.Mode!=7||ShadowEquipmentDefense.Composite(body)!=13)
        {throw new NotSupportedException("Shadow SC needs its own defensive buff and original active dual claws");}
        return Original(body,BuffRow.Value);
    }
    private static ShadowAnimationPlan Original(AssassinShadowBody body,ValkyrieAnimationRow row)
    {
        var r=OriginalRates(body);return SuppliedRow(row,r.Ias,r.Attack,r.Right,r.Left,0);
    }
    private static (int Ias,int Attack,int Right,int Left) OriginalRates(AssassinShadowBody body)
    {
        var binding=ShadowLoadout.Binding(body);var graph=OriginalNpcEquipment.Graph(body);
        var weapon=graph.Weapons(binding.Primary,binding.Secondary,true,true,0,0,0);
        weapon.Enter(); // Complete shared source selected-hand stat-list merge, not owner/average IAS.
        int HandRate(int slot)
        {
            var item=ShadowLoadout.Originals(body).Single(i=>i.Snapshot.Equipment.Body==slot);
            return EquipmentStatValues.SourceRead(graph.Item(item.Serial.Value).Unit.Stats,EquipmentStatKey.Pack(68),EquipmentStatValues.Definition(68));
        }
        // Shadow's captured initializer already posts base attackrate100.
        // NPCs do not receive the player-only sequence minus30 modifier.
        return (graph.Read(93),checked(graph.Read(68)+SourceAttackRate.WolfRate(body)-100),HandRate(4),HandRate(5));
    }
}
