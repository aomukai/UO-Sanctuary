// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class PaladinRemainingAuraInputChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(PaladinRemainingAuraInputChecks));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(var ability in new[]{CombatAbility.ResistFire,CombatAbility.ResistCold,CombatAbility.ResistLightning,CombatAbility.Salvation,
            CombatAbility.Vigor,CombatAbility.Thorns,CombatAbility.HolyFire,CombatAbility.HolyFreeze,CombatAbility.HolyShock,
            CombatAbility.Sanctuary,CombatAbility.Conviction,CombatAbility.Redemption})
        foreach(string mode in new[]{"right","quick"})
        {
            string label="remaining-aura-"+ability.ToString().ToLowerInvariant()+"-"+mode;
            ConsoleInputHandler.RegisterCommand(label+"-check","Actual zero-mana remaining Paladin aura input and recipient",_=>Core.LoopContext.Post(()=>
            {
                try
                {
                    FixtureNetwork.Connected(label+"-result",network=>
                    {
                        if(ability is CombatAbility.ResistFire or CombatAbility.ResistCold or CombatAbility.ResistLightning
                            or CombatAbility.Salvation or CombatAbility.Vigor)
                        {MightAuraChecks.Input(network,label,ability);}
                        else{Input(network,label,ability);}
                    });
                    Log.Information("{Label}-check: passed",label);
                }
                catch(Exception e){Log.Error(e,"{Label}-check: failed",label);}
            }));
        }
    }
    private static void Input(NetState network,string label,CombatAbility ability)
    {
        // Reuse the independent combat corridor's native town/occupancy/floor
        // checks instead of synthetic edge-map tiles.
        var p=BlessedHammerDamageChecks.Owner();
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.HolyBolt)&&SkillBindings.Instance.AssignQuick(p,7,ability)
            &&!SkillBindings.Instance.Assign(p,true,ability),"Remaining aura right/quick-only bindings");
        var ally=ClassSkillChecks.Player(DiabloClass.Sorceress);ally.Name="SacrificeAllocationFixture";
        ally.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);
        var enemy=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true};
        enemy.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);enemy.AIObject.AITimer.Stop();
        enemy.SetHits(20000);enemy.Hits=enemy.HitsMax;
        foreach(var resistance in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Poison,ResistanceType.Energy})
        {enemy.SetResistance(resistance,0);}
        TimedEffects.PreventHealing(enemy,TimedEffects.Now);
        bool enumerated=false;foreach(var actor in p.Map.GetMobilesInRange<Mobile>(p.Location,3)){enumerated|=actor==enemy;}
        Require(ClassCombat.IsHostile(p,enemy)&&enemy.CanBeDamaged()
            &&!Server.Spells.SpellHelper.IsTown(p.Location,p)&&!Server.Spells.SpellHelper.IsTown(enemy.Location,enemy)
            &&enumerated,"Remaining aura actual enumerated non-town hostile recipient");
        Corpse corpse=null;Gold loot=null;
        var oldRoll=PaladinRedemption.RollPercent;
        if(ability==CombatAbility.Redemption)
        {
            var previous=ClassLootHooks.CreatureDied;
            ClassLootHooks.CreatureDied=(actor,container)=>
            {
                if(actor!=enemy){previous?.Invoke(actor,container);return;}
                CorpseSkillRules.Capture(actor,container);corpse=container as Corpse;
                loot=new Gold(17);corpse?.DropItem(loot);
            };
            try{p.DoHarmful(enemy,true);enemy.Damage(enemy.Hits+1,p);CombatDamage.Finish(enemy,p);}
            finally{ClassLootHooks.CreatureDied=previous;}
            Require(corpse!=null&&CorpseSkillRules.Usable(p,corpse,10),"Redemption real usable corpse");
            PaladinRedemption.RollPercent=()=>0;
            SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-30*256-17);
        }
        NecromancerEffects.SetMana256(p,0);var account=FixtureNetwork.Authenticate(network,p,"remaining-aura-");
        var values=SkillMechanics.Values(p,ability);
        int life=SkillHealth.Balance256(p),allyLife=SkillHealth.Balance256(ally),enemyLife=SkillHealth.Balance256(enemy);
        bool right=label.EndsWith("right",StringComparison.Ordinal);
        if(right){Require(SkillBindings.Instance.Assign(p,false,ability),"Remaining aura selection");}
        var selected=PaladinAuras.Snapshot().Emitters.SingleOrDefault(e=>e.Owner==p.Serial.Value);
        var action=ClassCombat.For(p);action.NextAction=Core.TickCount+10000;
        long next=action.NextAction,deadline=Core.TickCount+5000;bool acknowledged=false;
        void Clean()
        {
            PaladinRedemption.RollPercent=oldRoll;PaladinAuras.Clear(p);PaladinAuras.Clear(ally);PaladinAuras.Clear(enemy);
            corpse?.Delete();enemy.Corpse?.Delete();enemy.Delete();ally.Delete();p.Delete();account.Delete();
        }
        void Observe()
        {
            try
            {
                bool received=acknowledged||(right?action.Sequence==1:SkillBindings.Instance.Get(p).Right==ability);
                if(!received){Require(Core.TickCount<deadline,"Remaining aura input timeout");Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(PaladinAuras.Selected(p)==ability&&action.PendingAbility==CombatAbility.None&&action.NextAction==next,"Remaining aura reserved an action cycle");
                var own=PaladinAuras.Effect(p,ability);
                if(!acknowledged)
                {
                    if(ability!=CombatAbility.Redemption)
                    {Require(ClassCombat.IsHostile(p,enemy),$"Live input lost hostility: canSee={p.CanSee(enemy)}, harm={p.CanBeHarmful(enemy,false)}, player={p.Location}/{p.Map}, enemy={enemy.Location}/{enemy.Map}, controlled={enemy.Controlled}, summoned={enemy.Summoned}, innocent={enemy.InitialInnocent}, enemy={enemy.IsEnemy(p)}");}
                    Require(own==null&&NecromancerEffects.ManaBalance256(p)==0&&SkillHealth.Balance256(p)==life
                        &&SkillHealth.Balance256(ally)==allyLife&&(corpse==null||!corpse.SanctuaryConsumed),"Delayed aura acted or paid on selection");
                    if(right){Require(PaladinAuras.Snapshot().Emitters.Single(e=>e.Owner==p.Serial.Value)==selected,"Right input restarted the delayed pulse");}
                    acknowledged=true;Log.Information("{Label}: acknowledged",label);
                }
                if(own==null){Require(Core.TickCount<deadline,"Remaining aura first natural pulse timeout");Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(own.Rank==1&&own.Source==p.Serial.Value&&SkillHealth.Balance256(ally)==allyLife,"Remaining aura supplier/party life");
                if(ability==CombatAbility.Thorns)
                {
                    var allied=PaladinAuras.Effect(ally,ability);
                    Require(allied is{Rank:1}&&allied.Source==p.Serial.Value&&allied.Percent==(int)values.Eval("aurastatcalc1")
                        &&allied.DamagePercent==(int)values.Eval("aurastatcalc2")&&PaladinAuras.Effect(enemy,ability)==null,"Delayed Thorns allied flat/percentage return packet");
                }
                else{Require(PaladinAuras.Effect(ally,ability)==null,"Offensive aura spread to ally");}
                if(PaladinElements.Handles(ability))
                {
                    var range=PaladinElements.WeaponRange(p,ability,values);
                    Require(own.WeaponMin==range.Min&&own.WeaponMax==range.Max&&SkillHealth.Balance256(enemy)<enemyLife
                        &&PaladinElements.Weapon(ally,PaladinElements.Element(ability),.5)==0,
                        $"Elemental input {ability}: weapon {own.WeaponMin}-{own.WeaponMax} expected {range.Min}-{range.Max}; life {enemyLife}->{SkillHealth.Balance256(enemy)}; ally weapon {PaladinElements.Weapon(ally,PaladinElements.Element(ability),.5)}; range {SkillGeometry.Tiles(values.Eval("aurarangecalc"))}; distance {p.GetDistanceToSqrt(enemy)}; hostile {ClassCombat.IsHostile(p,enemy)}; towns {Server.Spells.SpellHelper.IsTown(p.Location,p)}/{Server.Spells.SpellHelper.IsTown(enemy.Location,enemy)}; damageable {enemy.CanBeDamaged()}; fire {enemy.FireResistance}; MDR {GemCombat.Reduction(enemy)}; min damage {values.Damage256(false)}");
                    if(ability==CombatAbility.HolyFreeze){Require(PaladinHolyFreeze.Effect(enemy) is{Rank:1}&&PaladinHolyFreeze.Effect(ally)==null,"Holy Freeze slow recipients");}
                }
                else if(ability==CombatAbility.Sanctuary)
                {Require(SkillHealth.Balance256(enemy)<enemyLife&&PaladinSanctuary.Active(p),"Zero-mana Sanctuary undead pulse");}
                else if(ability==CombatAbility.Conviction)
                {
                    var hostile=PaladinConviction.Effect(enemy);
                    Require(hostile is{Rank:1}&&hostile.Source==p.Serial.Value&&hostile.DefensePercent==-(int)values.Eval("aurastatcalc1")
                        &&hostile.Fire==(int)values.Eval("aurastatcalc2")&&hostile.Cold==hostile.Fire&&hostile.Lightning==hostile.Fire
                        &&PaladinConviction.Effect(ally)==null&&SkillHealth.Balance256(enemy)==enemyLife,"Conviction hostile defense/resistance packet");
                }
                if(ability==CombatAbility.Redemption)
                {
                    Require(corpse is{Deleted:false,SanctuaryConsumed:true,Animated:true}&&loot is{Deleted:false,Amount:17}&&loot.Parent==corpse
                        &&!CorpseSkillRules.Usable(p,corpse,10)&&SkillHealth.Balance256(p)==life+(int)values.Eval("calc2")*256
                        &&NecromancerEffects.ManaBalance256(p)==(int)values.Eval("calc3")*256,"Redemption natural one-use corpse recovery or retained loot");
                }
                else{Require(NecromancerEffects.ManaBalance256(p)==0&&SkillHealth.Balance256(p)==life,"Remaining aura charged mana or changed owner life");}
                Clean();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Clean();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>
        {Log.Information("{Label}: ready ability={Ability} class={Class} target=0 x={X} y={Y} z={Z}",label,(int)ability,(int)DiabloClass.Paladin,p.X,p.Y,p.Z);Observe();});
    }
}
