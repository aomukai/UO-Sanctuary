// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record IronGolemBodyState(IronGolemBodyRecipe Recipe,MonsterDifficulty Difficulty,IronGolemBodyNumbers Numbers,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IronGolemWorldState Controller=null);

// Vulnerable saved body with an optional prepared world controller. Native
// skeleton attacks/AI stay suppressed; the learned cast remains gated.
[SerializationGenerator(0)]
public partial class IronGolemBody:SkeletonGuardian
{
    [SerializableField(0,setter:"private")] private string _saved="{}";
    private IronGolemBodyState _prepared;
    internal bool ControllerLoaded;
    internal IronGolemBodyState Prepared=>_prepared??(_saved=="{}"?null:_prepared=JsonSerializer.Deserialize<IronGolemBodyState>(_saved));
    internal SkillValues BirthValues=>new(SkillMechanics.For(CombatAbility.IronGolem),name=>Prepared.Recipe.Effective.GetValueOrDefault(name),
        name=>Prepared.Recipe.Hard.GetValueOrDefault(name),Prepared.Recipe.OwnerLevel);
    internal override string SourceMonsterId=>"irongolem";
    public override long SourceLife256=>Prepared?.Numbers.LifeBase256??306*256;
    internal override int SourceRegeneration256=>Prepared?.Numbers.Regeneration256??57;
    public override MonsterProfile CombatProfile=>Prepared is{} state
        ?new(state.Numbers.Level,(state.Numbers.LifeBase256+255)/256,state.Numbers.AttackBase,state.Numbers.ArmorBase,0,0,1000,400,0,0,0,0,0)
        :new(1,306,0,140,0,0,1000,400,0,0,0,0,0);
    public override (int Min,int Max) SkillWeaponDamage=>(0,0);
    internal override (int Min,int Max,int Percent) SourceWeaponBase=>(NecromancerSummons.Source("irongolem","A1MinD",SummonDifficulty),
        NecromancerSummons.Source("irongolem","A1MaxD",SummonDifficulty),
        checked((int)BirthValues.Eval("passivecalc4")));
    public override bool UsesNativeSummonTimer=>false;
    public override bool IsDispellable=>false;
    public override bool CanRegenStam=>false;
    public override Poison PoisonImmune=>null;
    public override bool BleedImmune=>false;
    public override bool Permitted(PlayerMobile owner)=>ClassCreation.Profiles.Histories(owner.Serial)
        .Any(x=>x.Profile.Class==DiabloClass.Necromancer&&x.Ranks.GetValueOrDefault("necromancer.irongolem")>0);
    public IronGolemBody()
    {Name="Iron Golem [prepared placeholder]";Body=14;Hue=0x835;BaseSoundID=-1;CantWalk=true;ControlSlots=0;AIObject?.AITimer.Stop();SetHits(306);SetDamage(0,0);}
    public override TimeSpan BeginMelee(Mobile target)=>TimeSpan.FromSeconds(1);
    public override void OnThink(){AIObject?.AITimer.Stop();Combatant=null;FocusMob=null;Warmode=false;}
    private void Store(IronGolemBodyState state)
    {
        IronGolemBodyRecipes.Validate(state?.Recipe);
        if(state.Numbers!=IronGolemBodyRecipes.Build(state.Recipe,state.Difficulty))
        {throw new InvalidDataException("Iron Golem derived birth numbers do not match its recipe");}
        if(state.Controller!=null){IronGolemWorld.Validate(state.Controller);}
        _saved=JsonSerializer.Serialize(state);_prepared=state;
    }
    internal void RestoreController(IronGolemWorldState controller)
    {IronGolemWorld.Validate(controller);var state=Prepared with{Controller=controller};string text=JsonSerializer.Serialize(state);_prepared=state;_saved=text;}
    private void SetBaseResistances()
    {
        var n=Prepared.Numbers;
        SetResistance(ResistanceType.Physical,0);SetResistance(ResistanceType.Fire,n.Fire);SetResistance(ResistanceType.Cold,n.Cold);
        SetResistance(ResistanceType.Energy,n.Lightning);SetResistance(ResistanceType.Poison,n.Poison);
    }
    internal static IronGolemBody CreatePrepared(PlayerMobile owner,Item original,Point3D point,Action<IronGolemBody> configure=null)
    {
        if(owner?.Map==null||owner.Map==Map.Internal||!owner.Map.CanSpawnMobile(point)){throw new ArgumentException("Iron Golem needs a legal world point");}
        var recipe=IronGolemBodyRecipes.Capture(owner,original);
        // Preflight every difficulty before transferring the original.
        foreach(var difficulty in new[]{MonsterDifficulty.Normal,MonsterDifficulty.Nightmare,MonsterDifficulty.Hell}){IronGolemBodyRecipes.Build(recipe,difficulty);}
        var difficultyNow=SummonDifficulties.For(owner.Map);var numbers=IronGolemBodyRecipes.Build(recipe,difficultyNow);
        var map=original.Map;var location=original.Location;bool visible=original.Visible,movable=original.Movable;
        var body=new IronGolemBody();
        try
        {
            body.Initialize(owner,recipe.Rank,0);if(!body.SetControlMaster(owner)){throw new InvalidOperationException("Iron Golem control master refused");}
            body.MoveToWorld(point,owner.Map);
            if(!IronGolemItems.TryAttach(owner,body,original,out var issue)){throw new NotSupportedException(issue);}
            var before=BattleOrders.Capture(body);body.Store(new(recipe,difficultyNow,numbers));
            body.SetHits(body.CombatProfile.Life);body.SetBaseResistances();BattleOrders.Change(body,before);
            if(SkillHealth.Maximum256(body)!=numbers.BirthMaximum256){throw new NotSupportedException("Iron Golem live maximum differs from its source graph");}
            SkillHealth.SetBalance256(body,numbers.CurrentLifeWrite256);
            // Later controller integration belongs to the same transaction.
            // A rejected controller must return the original, not consume it.
            configure?.Invoke(body);
            BattleCommand.Track(body);PersistentMinions.Profiles.Track(body);body.AIObject?.AITimer.Stop();return body;
        }
        catch
        {
            if(IronGolemItems.State(body)!=null){IronGolemItems.ReturnPreparedOriginal(body,map,location,visible,movable);}
            body.Delete();throw;
        }
    }
    internal override void RefreshDifficulty()
    {
        if(Prepared is{} state){Store(state with{Difficulty=SummonDifficulty,Numbers=IronGolemBodyRecipes.Build(state.Recipe,SummonDifficulty)});}
        base.RefreshDifficulty();if(Prepared!=null){SetBaseResistances();}
    }
    [AfterDeserialization]private void AfterIronLoad()
    {AIObject?.AITimer.Stop();var state=JsonSerializer.Deserialize<IronGolemBodyState>(_saved);Store(state);if(state.Controller!=null){ControllerLoaded=true;IronGolems.Track(this);}}
    public override void SuspendActions()
    {base.SuspendActions();if(Prepared?.Controller is{} controller){RestoreController(IronGolemWorld.Park(controller));}}
    internal override void InterruptForRecovery()
    {base.InterruptForRecovery();if(Prepared?.Controller is{} controller){RestoreController(IronGolemWorld.Park(controller));}}
    public override void OnAfterDelete(){IronGolems.Forget(this);base.OnAfterDelete();}
    public override bool OnBeforeDeath()
    {if(!base.OnBeforeDeath()){return false;}IronGolemItems.DestroyBoundItem(this);return true;}
    public override void OnDelete(){IronGolemItems.DestroyBoundItem(this);base.OnDelete();}
}
