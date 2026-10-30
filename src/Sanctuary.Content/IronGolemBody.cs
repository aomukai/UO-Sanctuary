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

internal sealed record IronGolemBodyState(IronGolemBodyRecipe Recipe,MonsterDifficulty Difficulty,IronGolemBodyNumbers Numbers);

// Vulnerable saved stat body only. No native skeleton attacks/AI or invented
// golem scheduling are enabled. The learned cast remains independently gated.
[SerializationGenerator(0)]
public partial class IronGolemBody:SkeletonGuardian
{
    [SerializableField(0,setter:"private")] private string _saved="{}";
    private IronGolemBodyState _prepared;
    internal IronGolemBodyState Prepared=>_prepared??(_saved=="{}"?null:_prepared=JsonSerializer.Deserialize<IronGolemBodyState>(_saved));
    internal override string SourceMonsterId=>"irongolem";
    public override long SourceLife256=>Prepared?.Numbers.LifeBase256??306*256;
    internal override int SourceRegeneration256=>Prepared?.Numbers.Regeneration256??57;
    public override MonsterProfile CombatProfile=>Prepared is{} state
        ?new(state.Numbers.Level,(state.Numbers.LifeBase256+255)/256,state.Numbers.AttackBase,state.Numbers.ArmorBase,0,0,1000,400,0,0,0,0,0)
        :new(1,306,0,140,0,0,1000,400,0,0,0,0,0);
    public override (int Min,int Max) SkillWeaponDamage=>(0,0);
    internal override (int Min,int Max,int Percent) SourceWeaponBase=>throw new NotSupportedException("Iron Golem attack delivery is not integrated");
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
        _saved=JsonSerializer.Serialize(state);_prepared=state;
    }
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
    {AIObject?.AITimer.Stop();var state=JsonSerializer.Deserialize<IronGolemBodyState>(_saved);Store(state);}
    public override bool OnBeforeDeath()
    {if(!base.OnBeforeDeath()){return false;}IronGolemItems.DestroyBoundItem(this);return true;}
    public override void OnDelete(){IronGolemItems.DestroyBoundItem(this);base.OnDelete();}
}
