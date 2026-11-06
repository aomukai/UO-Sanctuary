// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ModernUO.Serialization;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record ValkyrieBodyState(uint Owner,ValkyrieBirth Birth,MonsterDifficulty Difficulty,DateTime NextRecoveryUtc,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]ValkyrieEquipmentState Equipment=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]string EquipmentStatProfile=null)
{public string Profile{get;init;}="prepared-valkyrie-body-v1";}

// Saved supplied-birth backend with optional numeric equipment recipients.
// Resource/weapon/skill/event delivery,NecroPet and casting remain separate.
[SerializationGenerator(0)]
public partial class AmazonValkyrieBody:SkeletonGuardian,ISourceLifeMaximum
{
    [SerializableField(0,setter:"private")]private string _saved="{}";
    private ValkyrieBodyState _state;
    private ValkyrieCreationStats _numbers;
    private GeneratedNpcStatGraph _equipmentNumbers;
    private ValkyrieBodyState State=>_state??(_saved=="{}"?null:_state=JsonSerializer.Deserialize<ValkyrieBodyState>(_saved));
    internal ValkyrieBodyState Prepared=>State?.Equipment==null?State:State with{Equipment=ValkyrieEquipment.Copy(State.Equipment)};
    private ValkyrieCreationStats Numbers=>_numbers??=ValkyrieBirthNumbers.Calculate(State.Birth,State.Difficulty);
    internal ValkyrieCreationStats BirthStats=>Numbers with{Passives=(SummonStat[])Numbers.Passives.Clone(),Auras=(SummonStat[])Numbers.Auras.Clone(),Skills=(SummonStat[])Numbers.Skills.Clone()};
    internal string SavedText=>_saved;
    internal GeneratedNpcStatGraph EquipmentNumbers=>!World.Loading&&Alive&&!Deleted&&State?.EquipmentStatProfile==ValkyrieEquipmentStats.Profile
        ?GeneratedNpcLiveResources.ValkyrieFrame(this)??(_equipmentNumbers??=ValkyrieEquipmentStats.Build(this,State.Difficulty)):null;
    private int Aura(string name)=>Numbers.Auras.FirstOrDefault(s=>s.Name==name)?.Value??0;
    private int Passive(string name)=>Numbers.Passives.FirstOrDefault(s=>s.Name==name)?.Value??0;
    public override DiabloClass SummonClass=>DiabloClass.Amazon;
    public override bool Permitted(PlayerMobile owner)=>ClassCreation.Profiles.Histories(owner.Serial)
        .Any(h=>h.Profile.Class==DiabloClass.Amazon&&h.Ranks.GetValueOrDefault("amazon.valkyrie")>0);
    public override bool UsesNativeSummonTimer=>false;
    public override bool CanRegenStam=>false;
    public override bool BleedImmune=>false;
    public override Poison PoisonImmune=>null;
    public override bool IsDispellable=>false;
    internal override string SourceMonsterId=>"valkyrie";
    internal override int SourceRegeneration256=>Prepared==null?0:checked(Prepared.Birth.SpawnLife256*NecromancerSummons.Source("valkyrie","DamageRegen"))>>12;
    internal override int SourceDefensePercent=>Prepared==null?0:EquipmentNumbers?.Read(16)??Passive("item_armor_percent");
    public override long SourceLife256=>Prepared==null?400*256:Numbers.Life256;
    public int MaximumLife256
    {
        get{if(GeneratedNpcLiveResources.Maximum256(this,0)is{} equipped){return equipped;}long basis=SourceLife256+((long)HitsMaxSeed-CombatProfile.Life)*256;return(int)Math.Clamp(basis+ClassResources.Percentage(basis,BattleOrders.LifePercent(this)),256,65000*256);}
    }
    public override MonsterProfile CombatProfile=>Prepared==null?new(1,400,0,0,0,0,1000,400,0,0,0,0,0)
        :new(Numbers.Level,(Numbers.Life256+255)/256,EquipmentNumbers is{} attack?checked(attack.Read(19)+5*attack.Read(2)):Numbers.Attack+Passive("tohit"),
            EquipmentNumbers is{} defense?checked(defense.Read(31)+defense.Read(2)/4):Numbers.Defense,0,0,1000,400,0,0,0,0,0);
    public override(int Min,int Max)SkillWeaponDamage=>(0,0);
    public override int GetResistance(ResistanceType type)
    {
        // Native seeds/mods own the combined value. Validate the original
        // frame before reading that cache,including during typed mitigation.
        if(_saved!=null&&_saved!="{}"){_=EquipmentNumbers?.Read(36);}
        return base.GetResistance(type);
    }
    public AmazonValkyrieBody(){Name="Valkyrie [appearance placeholder]";Body=0x191;Hue=0x481;CantWalk=true;ControlSlots=0;AIObject?.AITimer.Stop();SetStr(0);SetDex(0);SetInt(0);SetHits(400);SetDamage(0,0);}
    public override TimeSpan BeginMelee(Mobile target)=>throw new NotSupportedException("Valkyrie generated equipment/contact and AI delivery are unfinished");
    public override void OnThink(){AIObject?.AITimer.Stop();Combatant=null;FocusMob=null;Warmode=false;}
    internal static void Validate(ValkyrieBodyState state)
    {
        try
        {
            if(state?.Birth==null||!((Serial)state.Owner).IsMobile||state.Profile!="prepared-valkyrie-body-v1"||!Enum.IsDefined(state.Difficulty)
                ||state.NextRecoveryUtc.Kind!=DateTimeKind.Utc||state.NextRecoveryUtc>DateTime.MaxValue.AddMilliseconds(-40))
            {throw new InvalidDataException("Invalid Valkyrie body state");}
            _=ValkyrieBirthNumbers.Calculate(state.Birth,state.Difficulty);
            if(state.Equipment is{} equipment)
            {
                ValkyrieEquipment.Validate(equipment);
                if(equipment.Owner!=state.Owner||equipment.Rank!=state.Birth.Rank){throw new InvalidDataException("Valkyrie equipment birth owner/rank changed");}
            }
            if(state.EquipmentStatProfile!=null&&(state.EquipmentStatProfile!=ValkyrieEquipmentStats.Profile||state.Equipment==null))
            {throw new InvalidDataException("Invalid Valkyrie numeric equipment admission");}
            if(state.Birth is not{ColdSunder:0,FireSunder:0,LightningSunder:0,PoisonSunder:0,PhysicalSunder:0})
            {throw new NotSupportedException("Valkyrie nonzero inherited sunder delivery remains unfinished");}
        }
        catch(Exception e)when(e is ArgumentException or NotSupportedException or OverflowException)
        {throw new InvalidDataException("Unsupported Valkyrie body snapshot",e);}
    }
    internal void RestorePrepared(ValkyrieBodyState state)
    {Validate(state);if(state.Equipment is{} e&&e.Actor!=Serial.Value){throw new InvalidDataException("Valkyrie loadout belongs to another body");}state=state with{Equipment=ValkyrieEquipment.Copy(state.Equipment)};string json=JsonSerializer.Serialize(state);_state=state;_numbers=null;_equipmentNumbers=null;_saved=json;}
    internal CreatureResistances SourceResistances(MonsterDifficulty? difficulty=null)
    {
        var d=difficulty??SummonDifficulty;var n=ValkyrieBirthNumbers.Calculate(Prepared.Birth,d);
        if(State.EquipmentStatProfile==ValkyrieEquipmentStats.Profile&&!World.Loading&&Alive&&!Deleted)
        {
            var graph=d==State.Difficulty?EquipmentNumbers:GeneratedNpcLiveResources.ValkyrieFrame(this,d)??ValkyrieEquipmentStats.Build(this,d);
            return new(graph.Read(36),graph.Read(37),graph.Read(39),graph.Read(41),graph.Read(43),graph.Read(45));
        }
        int R(string field,string aura)=>NecromancerSummons.Source("valkyrie",field,d)+(n.Auras.FirstOrDefault(s=>s.Name==aura)?.Value??0);
        return new(R("ResDm","damageresist"),R("ResMa","magicresist"),R("ResFi","fireresist"),R("ResLi","lightresist"),R("ResCo","coldresist"),R("ResPo","poisonresist"));
    }
    private void ApplyNumbers()
    {
        SetStr(Aura("strength"));SetDex(Aura("dexterity"));SetInt(0);ApplyEquipmentResistances();
    }
    internal void ApplyEquipmentResistances()
    {
        var r=SourceResistances(State.Difficulty);
        SetResistance(ResistanceType.Physical,r.Physical??0);SetResistance(ResistanceType.Fire,r.Fire??0);
        SetResistance(ResistanceType.Energy,r.Lightning??0);SetResistance(ResistanceType.Cold,r.Cold??0);SetResistance(ResistanceType.Poison,r.Poison??0);
    }
    internal static AmazonValkyrieBody CreatePrepared(PlayerMobile owner,Point3D point,int suppliedSpawnLife256,DateTime at)
    {
        if(at.Kind!=DateTimeKind.Utc||at>DateTime.MaxValue.AddMilliseconds(-40)||owner?.Map==null||owner.Map==Map.Internal||!owner.Map.CanSpawnMobile(point))
        {throw new ArgumentException("Valkyrie needs a legal native point and UTC clock");}
        var state=new ValkyrieBodyState(owner.Serial.Value,ValkyrieBirthNumbers.Capture(owner,suppliedSpawnLife256),SummonDifficulties.For(owner.Map),at.AddMilliseconds(40));
        Validate(state);var body=new AmazonValkyrieBody();
        try
        {
            body.RestorePrepared(state);body.Initialize(owner,state.Birth.Rank,0);body.ApplyNumbers();
            if(!body.SetControlMaster(owner)){throw new InvalidOperationException("Valkyrie control master refused");}
            BattleCommand.Track(body);PersistentMinions.Profiles.Track(body);body.MoveToWorld(point,owner.Map);body.AIObject?.AITimer.Stop();SkillHealth.SetBalance256(body,body.MaximumLife256);return body;
        }
        catch{body.Delete();throw;}
    }
    internal override void RefreshDifficulty()
    {if(Prepared is{} state){RestorePrepared(state with{Difficulty=SummonDifficulty});}GeneratedNpcLiveResources.RefreshValkyrieBasis(this);base.RefreshDifficulty();ApplyNumbers();}
    internal void AdvanceRecovery(DateTime at,bool actions)
    {if(Prepared is{} state&&PermanentSummonRecovery.Advance(this,state.Owner,state.NextRecoveryUtc,at,actions)is{} next){RestorePrepared(Prepared with{NextRecoveryUtc=next});}}
    [AfterDeserialization]private void AfterValkyrieLoad(){AIObject?.AITimer.Stop();_state=JsonSerializer.Deserialize<ValkyrieBodyState>(_saved);Validate(_state);if(_state.Equipment is{} e&&e.Actor!=Serial.Value){throw new InvalidDataException("Saved Valkyrie loadout actor changed");}_numbers=null;_equipmentNumbers=null;}
}
