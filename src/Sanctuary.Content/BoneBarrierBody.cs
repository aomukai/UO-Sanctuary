// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ModernUO.Serialization;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Historical BoneBarrierSegment saves keep their existing resource path.
// Newly learned barriers opt into a saved recipe and exact FP256 resources.
[SerializationGenerator(0)]
public partial class BoneBarrierBody:BoneBarrierSegment,ISourceLifeMaximum
{
    private static readonly HashSet<BoneBarrierBody> Tracked=new();
    [SerializableField(0,setter:"private")] private string _saved="{}";
    private BarrierBirthState _prepared;
    internal BarrierBirthState Prepared=>_prepared??(_saved=="{}"?null:_prepared=JsonSerializer.Deserialize<BarrierBirthState>(_saved));
    int ISourceLifeMaximum.MaximumLife256=>Prepared?.Numbers.Life256??19*256;
    [Constructible]public BoneBarrierBody(){Name="bone barrier [prepared placeholder]";}
    internal void Prepare(BarrierBirthState state)
    {BoneBarrierBirth.Validate(state);_saved=JsonSerializer.Serialize(state);_prepared=state;Tracked.Add(this);}
    protected override void RecoverLife(long now)
    {if(Prepared is{} state){NpcEquipmentRegeneration.Recover(this,now,state.Numbers.Regeneration256);}}
    protected override void ResetRecovery(){base.ResetRecovery();NpcEquipmentRegeneration.Reset(this);}
    internal void InspectRecovery(long now)=>RecoverLife(now);
    internal void ResetInspectedRecovery()=>ResetRecovery();
    internal void Rescale(MonsterDifficulty difficulty)
    {
        var old=Prepared;if(old==null||old.Difficulty==difficulty){return;}
        int life=SkillHealth.Balance256(this);var next=old with{Difficulty=difficulty,Numbers=BoneBarrierBirth.Prepare(old.Ability,old.Rank,old.Armor,old.Other,difficulty)};
        Prepare(next);ApplyBirth(next.Numbers);SkillHealth.SetBalance256(this,SummonDifficulties.Life256(life,old.Numbers.Life256,next.Numbers.Life256));ResetRecovery();
    }
    internal static void RescaleFor(PlayerMobile owner,MonsterDifficulty difficulty)
    {
        foreach(var body in Tracked.ToArray())
        {if(body is{Deleted:false,Alive:true}&&body.SummonMaster==owner){body.Rescale(difficulty);}}
    }
    public override void OnAfterDelete(){Tracked.Remove(this);NpcEquipmentRegeneration.Reset(this);SkillHealth.Clear(this);base.OnAfterDelete();}
    [AfterDeserialization]private void AfterBarrierLoad()
    {if(Prepared is{} state){BoneBarrierBirth.Validate(state);Tracked.Add(this);}ResetRecovery();}
}
