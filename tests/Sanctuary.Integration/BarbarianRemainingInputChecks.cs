// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class BarbarianRemainingInputChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BarbarianRemainingInputChecks));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(var ability in new[]{CombatAbility.Howl,CombatAbility.FindPotion,CombatAbility.Taunt,CombatAbility.BattleCry,
            CombatAbility.FindItem,CombatAbility.BattleOrders,CombatAbility.WarCry,CombatAbility.Bash,CombatAbility.Leap,
            CombatAbility.DoubleThrow,CombatAbility.Stun,CombatAbility.Concentrate,CombatAbility.Whirlwind,CombatAbility.Berserk})
        foreach(string mode in BarbarianCastClock.Handles(ability)?new[]{"right","quick"}:new[]{"left","right","quick"})
        {
            string label="remaining-barbarian-"+ability.ToString().ToLowerInvariant()+"-"+mode;
            ConsoleInputHandler.RegisterCommand(label+"-check","Actual Barbarian input and natural recipient/payment",_=>Core.LoopContext.Post(()=>
            {
                try
                {
                    FixtureNetwork.Connected(label+"-result",network=>
                    {if(BarbarianCastClock.Handles(ability)){Spell(network,label,ability);}else{Attack(network,label,ability);}});
                    Log.Information("{Label}-check: passed",label);
                }
                catch(Exception e){Log.Error(e,"{Label}-check: failed",label);}
            }));
        }
    }
    private static void Bind(PlayerMobile p,CombatAbility ability)
    {
        Require(SkillBindings.Instance.Assign(p,false,ability)&&SkillBindings.Instance.AssignQuick(p,7,ability),"Barbarian right/quick binding");
        Require(SkillBindings.Instance.Assign(p,true,ability)==!BarbarianCastClock.Handles(ability),"Barbarian left eligibility");
    }
    private static GraveyardSkeleton Enemy(PlayerMobile p,int dx=1,int dy=0)
    {
        var enemy=WhirlwindChecks.Enemy(p,dx,dy);enemy.Hits=enemy.HitsMax;
        foreach(var resistance in Enum.GetValues<ResistanceType>()){enemy.SetResistance(resistance,0);}
        TimedEffects.PreventHealing(enemy,TimedEffects.Now);return enemy;
    }
    private static void Ready(string label,CombatAbility ability,PlayerMobile p,Serial target,Point3D point,Action observe)
    {
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(()=>
        {Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)ability,(int)DiabloClass.Barbarian,target.Value,point.X,point.Y,point.Z);observe();}));
    }
    private static void Spell(NetState network,string label,CombatAbility ability)
    {
        var p=BarbarianCastClockChecks.Owner();Bind(p,ability);
        bool search=ability is CombatAbility.FindItem or CombatAbility.FindPotion;
        var enemy=search||ability==CombatAbility.BattleOrders?null:Enemy(p);
        var corpse=ability==CombatAbility.FindItem?FindItemChecks.Body(p):ability==CombatAbility.FindPotion?FindPotionChecks.Body(p):null;
        var loot=corpse?.Items.ToArray();var itemRoll=BarbarianFindItem.Roll;var potionRoll=BarbarianFindPotion.Roll;var block=SourceBlocking.BlockRoll;
        // Failed Find Item rolls still prove its paid original-corpse attempt;
        // successful treasure contexts retain the dedicated source fixtures.
        BarbarianFindItem.Roll=_=>99;BarbarianFindPotion.Roll=()=>0;SourceBlocking.BlockRoll=n=>n-1;
        var account=FixtureNetwork.Authenticate(network,p,"barbarian-input-");
        int mana=NecromancerEffects.ManaBalance256(p),maximum=NecromancerEffects.ManaMaximum256(p),cost=SkillMechanics.Values(p,ability).Mana256;
        int life=enemy==null?0:SkillHealth.Balance256(enemy);long deadline=Core.TickCount+10000;bool began=false,finished=false;
        bool Effect()=>ability switch
        {
            CombatAbility.BattleOrders=>BattleOrders.State(p)!=null,
            CombatAbility.Howl=>NecromancerEffects.Curse(enemy)?.Ability==CombatAbility.Terror,
            CombatAbility.Taunt or CombatAbility.BattleCry=>NecromancerEffects.Curse(enemy)?.Ability==ability,
            CombatAbility.WarCry=>SkillHealth.Balance256(enemy)<life&&CombatStuns.Remaining(enemy)>0,
            _=>CorpseSkillRules.NoSelect(corpse)
        };
        void Clean()
        {
            BarbarianFindItem.Roll=itemRoll;BarbarianFindPotion.Roll=potionRoll;SourceBlocking.BlockRoll=block;
            corpse?.Delete();BarbarianCastClockChecks.Clean(p,enemy);account.Delete();
        }
        void Observe()
        {
            if(finished){return;}
            try
            {
                Require(Core.TickCount<deadline,"Barbarian spell input timeout: "+ability);var action=ClassCombat.For(p);
                if(action.PendingAbility==ability&&!began)
                {
                    Require(action.Timing==BarbarianCastClock.Calculate(CombatTiming.Fcr(p))&&!Effect()
                        &&NecromancerEffects.ManaBalance256(p)==mana-(search?cost:0),"Barbarian spell windup/start payment");
                    began=true;Log.Information("{Label}: windup",label);
                }
                if(!Effect()){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                int expected=ability==CombatAbility.BattleOrders?BattleOrders.Rescale256(mana,maximum,NecromancerEffects.ManaMaximum256(p))-cost:mana-cost;
                Require(began&&NecromancerEffects.ManaBalance256(p)==expected,"Barbarian spell release/payment/resource rescale");
                if(search)
                {
                    Require(corpse is{Deleted:false}&&loot.All(x=>!x.Deleted&&x.Parent==corpse),"Search removed original corpse/loot");
                    if(ability==CombatAbility.FindPotion)
                    {Require(CorpseSkillRules.TryMonsterLevel(corpse,out int level)&&corpse.Items.OfType<RestorationPotion>().Single().Tier==PotionLevelBands.For(level).ManaTier,"Actual Find Potion monster-level result");}
                }
                finished=true;Clean();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){finished=true;Clean();Log.Error(e,"{Label}-result: failed",label);}
        }
        Serial target=corpse?.Serial??(ability==CombatAbility.Taunt?enemy.Serial:Serial.Zero);
        Ready(label,ability,p,target,corpse?.Location??(ability==CombatAbility.Taunt?enemy.Location:p.Location),Observe);
    }
    private static void Attack(NetState network,string label,CombatAbility ability)
    {
        var p=ability==CombatAbility.DoubleThrow?DoubleThrowChecks.Fresh():WhirlwindChecks.Fresh(false);
        p.Name="SacrificeAllocationFixture";ClassSkillChecks.Train(p,SkillMechanics.For(ability).TalentId);Bind(p,ability);
        var origin=p.Location;var point=new Point3D(p.X+4,p.Y,p.Z);
        var enemy=Enemy(p,ability==CombatAbility.DoubleThrow?3:1,ability==CombatAbility.Whirlwind?1:0);
        var hit=BarbarianMelee.HitRoll;var spinHit=BarbarianWhirlwind.HitRoll;var throwHit=BarbarianDoubleThrow.HitRoll;var block=SourceBlocking.BlockRoll;
        BarbarianMelee.HitRoll=BarbarianWhirlwind.HitRoll=BarbarianDoubleThrow.HitRoll=()=>0;SourceBlocking.BlockRoll=n=>n-1;
        var account=FixtureNetwork.Authenticate(network,p,"barbarian-melee-input-");WhirlwindChecks.Ready(p);
        int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,ability).Mana256,life=SkillHealth.Balance256(enemy);
        bool began=false,finished=false,airborne=false;uint reservation=0;long deadline=Core.TickCount+10000;
        void Clean()
        {
            BarbarianMelee.HitRoll=hit;BarbarianWhirlwind.HitRoll=spinHit;BarbarianDoubleThrow.HitRoll=throwHit;SourceBlocking.BlockRoll=block;
            ClassCombat.Stop(p);BarbarianConcentrate.Cancel(p);BarbarianBerserk.Clear(p);WeaponMissiles.Cancel(p);
            enemy.Corpse?.Delete();enemy.Delete();p.Delete();account.Delete();
        }
        void Observe()
        {
            if(finished){return;}
            try
            {
                Require(Core.TickCount<deadline,"Barbarian attack input timeout: "+ability);var action=ClassCombat.For(p);
                var leap=BarbarianLeap.State(p);var spin=BarbarianWhirlwind.State(p);
                if(action.PendingAbility==ability&&!began)
                {
                    began=true;reservation=leap?.Reservation??spin?.Reservation??0;
                    Require(BarbarianCombat.Available(p,ability),"Input lost trained Barbarian skill");Log.Information("{Label}: windup",label);
                }
                if(leap?.Phase==LeapPhase.Airborne){airborne=true;}
                if(!began){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                bool complete=ability switch
                {
                    CombatAbility.Leap=>leap==null&&p.Location==point,
                    CombatAbility.Whirlwind=>spin==null&&p.Location==point&&SkillHealth.Balance256(enemy)<life,
                    CombatAbility.DoubleThrow=>action.PendingAbility!=ability&&!WeaponMissiles.Snapshot().Flights.Any(f=>f.Owner==p.Serial.Value)&&SkillHealth.Balance256(enemy)<life,
                    _=>SkillHealth.Balance256(enemy)<life
                };
                if(!complete){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(NecromancerEffects.ManaBalance256(p)==mana-cost*(ability==CombatAbility.DoubleThrow?2:1),"Attack once-only/per-throw mana");
                if(ability==CombatAbility.Leap){Require(airborne&&origin!=p.Location,"Leap never entered real flight");}
                if(ability is CombatAbility.Leap or CombatAbility.Whirlwind){Require(reservation!=0&&World.FindMobile((Serial)reservation)==null,"Movement reservation leaked");}
                if(ability==CombatAbility.Stun){Require(CombatStuns.Remaining(enemy)>0,"Actual Stun contact lost control");}
                if(ability==CombatAbility.Berserk){Require(BarbarianBerserk.State(p)!=null,"Actual Berserk hit lost its timed state");}
                finished=true;Clean();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){finished=true;Clean();Log.Error(e,"{Label}-result: failed",label);}
        }
        bool movement=ability is CombatAbility.Leap or CombatAbility.Whirlwind;
        Ready(label,ability,p,movement?Serial.Zero:enemy.Serial,movement?point:enemy.Location,Observe);
    }
}
