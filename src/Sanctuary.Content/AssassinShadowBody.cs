// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModernUO.Serialization;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record ShadowBodyState(uint Owner,ShadowOwnerSnapshot Capture,ShadowInitialization Initialization,
    MonsterDifficulty Difficulty,DateTime NextRecoveryUtc,ShadowLoadoutState Equipment=null,ShadowWeaponBinding Weapons=null)
{
    public string Profile{get;init;}="parked-shadow-resources-v1";
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]public string Skills{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]public string DefenseProfile{get;init;}
}

// Real serialized species/ownership/original inventory and FP256 resource
// recipient. The missing NPC skills/events/controller keep it parked. The
// talent cast is still unavailable; no inherited skeleton combat is allowed.
[SerializationGenerator(0)]
public partial class AssassinShadowBody:SkeletonGuardian,ISourceLifeMaximum,IPersistentMinion
{
    [SerializableField(0,setter:"private")]private string _saved="{}";
    private ShadowBodyState _state;
    private ShadowCreationStats _birth;
    private ShadowBodyState State=>_state??(_saved=="{}"?null:_state=JsonSerializer.Deserialize<ShadowBodyState>(_saved));
    internal ShadowBodyState Prepared=>State==null?null:State with{Equipment=ShadowLoadout.Copy(State.Equipment)};
    internal string SavedText=>_saved;
    private ShadowCreationStats Numbers=>_birth??=State.Capture.Plan(State.Initialization.SpawnLife256);
    internal GeneratedNpcStatGraph EquipmentNumbers=>!World.Loading&&State?.Equipment!=null&&Alive&&!Deleted
        ?GeneratedNpcLiveResources.ShadowFrame(this)??ShadowEquipmentResources.Numeric(this):null;
    internal ShadowInitialSkill[] InitialSkillRequests=>ShadowInitializer.Roll(State.Capture.Ability,State.Initialization.UnitSeed,State.Difficulty).Skills;
    internal ShadowSkillBook OwnedSkills=>ShadowSkillBook.Restore(State?.Skills);
    internal void ChangeSkills(Action<ShadowSkillBook> change)
    {
        ArgumentNullException.ThrowIfNull(change);var book=OwnedSkills;change(book);
        if(GeneratedNpcLiveResources.Handles(this)){GeneratedNpcLiveResources.ChangeShadowSkills(this,book.Save());}
        else{RestorePrepared(State with{Skills=book.Save()});}
    }
    internal int OwnedSkillRank(int id)
    {var graph=EquipmentNumbers??throw new NotSupportedException("Shadow skill ranks need its original item graph");return OwnedSkills.Rank(id,graph.Read,BattleCommand.Bonus(this));}
    public override DiabloClass SummonClass=>DiabloClass.Assassin;
    bool IPersistentMinion.CanActivate=>false;
    public override bool Permitted(PlayerMobile owner)=>State!=null&&ClassCreation.Profiles.Histories(owner.Serial)
        .Any(h=>h.Profile.Class==DiabloClass.Assassin&&h.Ranks.GetValueOrDefault(SkillMechanics.For(State.Capture.Ability).TalentId)>0);
    public override bool UsesNativeSummonTimer=>false;
    public override bool CanRegenStam=>false;
    public override bool BleedImmune=>false;
    public override Poison PoisonImmune=>null;
    public override bool IsDispellable=>false;
    internal override string SourceMonsterId=>State?.Capture.Ability==CombatAbility.ShadowMaster?"shadowmaster":"shadowwarrior";
    internal override int SourceRegeneration256=>State?.Initialization.RegenerationBase??0;
    internal override int SourceDefensePercent=>EquipmentNumbers?.Read(16)??0;
    public override long SourceLife256=>State==null?376*256:Numbers.MaximumLifeWrite256;
    public int MaximumLife256=>GeneratedNpcLiveResources.Maximum256(this,0)
        ??checked((int)(SourceLife256+ClassResources.Percentage(SourceLife256,BattleOrders.LifePercent(this))));
    public override MonsterProfile CombatProfile
    {
        get
        {
            var graph=EquipmentNumbers;
            return new(State?.Capture.Level??1,checked(((int)SourceLife256+255)/256),
                graph==null?0:checked(graph.Read(19)+5*graph.Read(2)),graph==null?196:checked(graph.Read(31)+graph.Read(2)/4),0,0,1000,400,0,0,0,0,0);
        }
    }
    public override(int Min,int Max)SkillWeaponDamage=>(0,0);
    public AssassinShadowBody()
    {Name="Shadow [appearance placeholder]";Body=0x190;Hue=0x455;CantWalk=true;ControlSlots=0;AIObject?.AITimer.Stop();SetStr(0);SetDex(0);SetInt(0);SetHits(376);SetMana(0);SetStam(0);SetDamage(0,0);}
    public override TimeSpan BeginMelee(Mobile target)=>throw new NotSupportedException("Shadow NPC skills/events/controller have not been qualified");
    public override void OnThink(){AIObject?.AITimer.Stop();Combatant=null;FocusMob=null;Warmode=false;}
    public override void SuspendActions(){ShadowActions.Cancel(this);base.SuspendActions();}
    internal override void InterruptForRecovery(){ShadowActions.Cancel(this);base.InterruptForRecovery();}
    internal static void Validate(ShadowBodyState s)
    {
        try
        {
            if(s?.Capture==null||s.Initialization==null||s.Profile!="parked-shadow-resources-v1"||!((Serial)s.Owner).IsMobile
                ||!Enum.IsDefined(s.Difficulty)||s.NextRecoveryUtc.Kind!=DateTimeKind.Utc||s.NextRecoveryUtc>DateTime.MaxValue.AddMilliseconds(-40))
            {throw new InvalidDataException("Invalid Shadow actor birth");}
            s.Capture.Validate();ShadowInitializer.Validate(s.Initialization);
            if(s.DefenseProfile!=null&&s.DefenseProfile!=ShadowEquipmentDefense.Profile)
            {throw new InvalidDataException("Unknown original Shadow defense profile");}
            if(s.Skills!=null){_=s.Capture.SkillRanks();_=ShadowSkillBook.Restore(s.Skills);}
            if(s.Capture.Ability!=s.Initialization.Ability){throw new InvalidDataException("Shadow actor species changed");}
            if(s.Equipment is{} e)
            {
                ShadowLoadout.Validate(e);
                if(e.Owner!=s.Owner||e.Birth!=s.Capture||e.Difficulty!=s.Initialization.Difficulty
                    ||e.PetBefore!=s.Initialization.AfterInitialization||e.GameBefore!=s.Initialization.WorldAfter)
                {throw new InvalidDataException("Shadow actor lost initializer/equipment continuation");}
            }
            if(s.Weapons is{} w&&(s.Equipment==null||!((Serial)w.Primary).IsItem||w.Secondary!=0&&!((Serial)w.Secondary).IsItem
                ||w.Primary==w.Secondary||w.Identity is not{Length:64}||w.Identity.Any(c=>c is not(>= '0' and <= '9' or >= 'a' and <= 'f'))))
            {throw new InvalidDataException("Invalid original Shadow weapon frame");}
        }
        catch(Exception e)when(e is ArgumentException or NotSupportedException or OverflowException)
        {throw new InvalidDataException("Unsupported original Shadow actor snapshot",e);}
    }
    internal void RestorePrepared(ShadowBodyState s)
    {
        Validate(s);if(s.Equipment!=null&&s.Equipment.Actor!=Serial.Value){throw new InvalidDataException("Shadow loadout belongs to another body");}
        var next=s with{Equipment=ShadowLoadout.Copy(s.Equipment)};string json=JsonSerializer.Serialize(next);_state=next;_birth=null;_saved=json;
    }
    internal CreatureResistances SourceResistances(MonsterDifficulty? difficulty=null)
    {
        if(State?.Equipment!=null&&!World.Loading&&Alive&&!Deleted)
        {
            var graph=GeneratedNpcLiveResources.ShadowFrame(this,difficulty)??ShadowEquipmentResources.Numeric(this);
            return new(graph.Read(36),graph.Read(37),graph.Read(39),graph.Read(41),graph.Read(43),graph.Read(45));
        }
        int R(string name)=>State==null?0:Numbers.Passives.FirstOrDefault(s=>s.Name==name)?.Value??0;
        return new(40,0,R("fireresist"),R("lightresist"),R("coldresist"),R("poisonresist"));
    }
    internal void ApplyEquipmentResistances()
    {
        var r=SourceResistances(State.Difficulty);SetResistance(ResistanceType.Physical,r.Physical??0);SetResistance(ResistanceType.Fire,r.Fire??0);
        SetResistance(ResistanceType.Energy,r.Lightning??0);SetResistance(ResistanceType.Cold,r.Cold??0);SetResistance(ResistanceType.Poison,r.Poison??0);
    }
    internal static AssassinShadowBody CreateInitialized(PlayerMobile owner,CombatAbility ability,DateTime at)
    {
        if(at.Kind!=DateTimeKind.Utc||at>DateTime.MaxValue.AddMilliseconds(-40)||owner?.Map==null||owner.Map==Map.Internal)
        {throw new ArgumentException("Shadow needs its world owner and UTC clock");}
        var capture=ShadowOwnerSnapshot.Capture(owner,ability);var difficulty=SummonDifficulties.For(owner.Map);
        _=ShadowInitializer.Roll(ability,SourceRandom.InitLow(1),difficulty);var body=new AssassinShadowBody();
        try
        {
            var init=ShadowInitializer.Allocate(ability,difficulty);var state=new ShadowBodyState(owner.Serial.Value,capture,init,difficulty,at.AddMilliseconds(40))
            {Skills=ShadowSkillBook.Initial(capture,init).Save()};
            body.RestorePrepared(state);body.Name=(ability==CombatAbility.ShadowWarrior?"Shadow Warrior":"Shadow Master")+" [appearance placeholder]";
            body.Initialize(owner,capture.Rank,0);body.SetStr(body.Numbers.Auras.Single(s=>s.Name=="strength").Value);body.SetDex(body.Numbers.Auras.Single(s=>s.Name=="dexterity").Value);body.SetInt(0);body.SetMana(0);body.SetStam(0);
            if(!body.SetControlMaster(owner)){throw new InvalidOperationException("Shadow owner binding refused");}
            PersistentMinions.Profiles.Track(body);body.Internalize();body.AIObject?.AITimer.Stop();SkillHealth.SetBalance256(body,body.MaximumLife256);
            var pet=new SourceSeedStream(init.AfterInitialization);ShadowLoadout.Materialize(body,pet,NpcWorldAllocation.RequireStream());
            ShadowLoadout.BindStream(body,pet.Value);GeneratedNpcLiveResources.ActivateShadow(body,new(capture,init,difficulty));
            BattleCommand.Track(body);PersistentMinions.Park(body);body.ApplyEquipmentResistances();return body;
        }
        catch{body.Delete();throw;}
    }
    internal override void RefreshDifficulty()
    {
        if(State==null){return;}
        RestorePrepared(State with{Difficulty=SummonDifficulty});GeneratedNpcLiveResources.RefreshShadowBasis(this);base.RefreshDifficulty();ApplyEquipmentResistances();
    }
    internal void AdvanceRecovery(DateTime at,bool actions)
    {
        if(State is{} old&&PermanentSummonRecovery.Advance(this,old.Owner,old.NextRecoveryUtc,at,actions)is{} next)
        {RestorePrepared(State with{NextRecoveryUtc=next});}
    }
    public override void OnAfterDelete(){ShadowActions.Cancel(this);GeneratedNpcLiveResources.Forget(this);NpcEquipmentRandom.Forget(this);base.OnAfterDelete();}
    [AfterDeserialization]private void AfterShadowLoad()
    {
        AIObject?.AITimer.Stop();_state=JsonSerializer.Deserialize<ShadowBodyState>(_saved);Validate(_state);_birth=null;
        if(_state.Equipment==null||_state.Weapons==null||_state.Equipment.Actor!=Serial.Value){throw new InvalidDataException("Saved Shadow lost its completed original loadout");}
    }
}
