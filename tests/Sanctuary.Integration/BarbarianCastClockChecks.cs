// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class BarbarianCastClockChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BarbarianCastClockChecks));
    private static bool _creating;
    private static void Freeze()
    {var mana=Mobile.ManaRegenRateHandler;Mobile.ManaRegenRateHandler=p=>_creating||p.Name=="BarbarianCastClockFixture"?TimeSpan.FromHours(1):mana?.Invoke(p)??Mobile.DefaultManaRate;}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Freeze();
    public static void Configure()
    {
        Freeze();Register("barbarian-cast-clock-check",Check);
        foreach(bool quick in new[]{false,true})
        {bool captured=quick;string label=quick?"barbarian-cast-clock-quick":"barbarian-cast-clock-right";Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,captured)));}
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Barbarian SC/FCR, dual gear and original payment phases",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {
        PlayerMobile p;_creating=true;try{p=ClassSkillChecks.Player(DiabloClass.Barbarian);}finally{_creating=false;}p.Name="BarbarianCastClockFixture";
        if(p.Backpack==null){p.AddItem(new Backpack());}
        while(p.RawStr<50){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Real gear Strength");}
        FixtureSpace.Corridor(p,6200,26,3600);Ready(p);return p;
    }
    private static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,50*256+17);}
    private static void DualGear(PlayerMobile p)
    {
        Item Named(string key)
        {var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==key).Id,99,new Random(173),true);p.AddToBackpack(item);Require(p.EquipItem(item),"Actual equip: "+key);ClassGear.Refresh(p);return item;}
        var first=Named("Rixots Keen");var second=Named("Gull");Named("Magefist");
        Require(WeaponHands.Pair(p)==new WeaponPair((BaseWeapon)first,(BaseWeapon)second)&&CombatTiming.Equipped(p)==first,"Actual dual primary identity");
    }
    private static bool CorpseSkill(CombatAbility ability)=>ability is CombatAbility.FindItem or CombatAbility.FindPotion;
    private static bool Start(PlayerMobile p,CombatAbility ability,BaseCreature enemy,Corpse corpse,long now)
        =>CorpseSkill(ability)?ClassCombat.BeginCorpse(p,ability,corpse.Serial,corpse.Location,now)
            :ClassCombat.Perform(p,ability,ability==CombatAbility.Taunt?enemy:null,ability==CombatAbility.Taunt?enemy.Location:p.Location,now);
    private static bool Effect(PlayerMobile p,CombatAbility ability,BaseCreature enemy=null,Corpse corpse=null)=>ability switch
    {
        CombatAbility.Shout=>BarbarianShout.State(p)!=null,
        CombatAbility.BattleOrders=>BattleOrders.State(p)!=null,
        CombatAbility.BattleCommand=>BattleCommand.State(p)!=null,
        CombatAbility.Taunt=>NecromancerEffects.Curse(enemy)?.Ability==CombatAbility.Taunt,
        CombatAbility.FindItem or CombatAbility.FindPotion=>CorpseSkillRules.NoSelect(corpse),
        _=>BarbarianShout.Snapshot().Rays.Any(r=>r.Owner==p.Serial.Value&&r.Ability==ability)
    };
    private static void ClearEffects(PlayerMobile p,BaseCreature enemy=null)
    {
        BattleCommand.Clear(p);BattleOrders.Clear(p);BarbarianShout.Clear(p);
        var wave=BarbarianShout.Snapshot();BarbarianShout.Restore(wave with{Rays=wave.Rays.Where(r=>r.Owner!=p.Serial.Value).ToArray()});
        if(enemy!=null){NecromancerEffects.Profiles.Clear(enemy);}
    }
    private static void Clean(PlayerMobile p,BaseCreature enemy=null)
    {ClassCombat.Stop(p);ClearEffects(p,enemy);enemy?.Delete();p.Delete();}
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"barbarian-cast-clock-source-probe.json")));
        int comparisons=0,routes=0,ownerRoutes=0,casts=0,cancellations=0;
        foreach(var r in doc.RootElement.EnumerateArray())
        {var timing=BarbarianCastClock.Calculate(r[1].GetInt32());Require(timing.EffectFrame==r[3][0][1].GetInt32()&&timing.Frames==r[3][1][1].GetInt32(),"Complete source Barbarian SC differential");comparisons++;}
        var skills=SkillMechanics.All.Where(r=>BarbarianCastClock.Handles(r.Ability)).ToArray();
        Require(comparisons==3108&&skills.Length==9&&!BarbarianCastClock.Handles(CombatAbility.GrimWard)&&!BarbarianCastClock.Handles(CombatAbility.Bash)&&!BarbarianCastClock.Handles(CombatAbility.IronSkin),"SC routes or exclusions changed");
        foreach(var skill in skills)for(int i=0;i<259;i++)
        {
            int fcr=i<256?i:i==256?500:i==257?1000:10000;
            Require(CombatTiming.Calculate(DiabloClass.Barbarian,skill.Ability,new(AttackFamily.Bow,-20),90,fcr,85,70)==BarbarianCastClock.Calculate(fcr),"SC leaked weapon/IAS/chill/attack rate");routes++;
        }
        var itemRoll=BarbarianFindItem.Roll;var potionRoll=BarbarianFindPotion.Roll;
        try
        {
            // Deterministic failed searches still reserve the original corpse.
            // This isolates the timing/payment transaction from treasure rolls.
            BarbarianFindItem.Roll=_=>99;BarbarianFindPotion.Roll=()=>99;
            foreach(bool boosted in new[]{false,true})
            {
                var p=Owner();var enemy=PoisonDaggerChecks.Target(p);
                try
                {
                    if(boosted){DualGear(p);}var weaponClock=CombatTiming.For(p,CombatAbility.BasicAttack);
                    int fcr=boosted?20:0;var expected=BarbarianCastClock.Calculate(fcr);
                    Require(CombatTiming.Fcr(p)==fcr,"Real gear FCR");
                    foreach(var ability in new[]{CombatAbility.GrimWard,CombatAbility.DoubleSwing,CombatAbility.Frenzy,CombatAbility.LeapAttack})
                    {Require(!ClassCombat.Available(p,ability),"Clock enabled an unsupported learned skill");}
                    foreach(var skill in skills)
                    {
                        var ability=skill.Ability;ClearEffects(p,enemy);Ready(p);
                        Corpse corpse=ability==CombatAbility.FindItem?FindItemChecks.Body(p):ability==CombatAbility.FindPotion?FindPotionChecks.Body(p):null;
                        try
                        {
                            var loot=corpse?.Items.ToArray();int mana=NecromancerEffects.ManaBalance256(p),maximum=NecromancerEffects.ManaMaximum256(p),cost=SkillMechanics.Values(p,ability).Mana256;
                            Require(CombatTiming.For(p,ability)==expected&&CombatTiming.For(p,CombatAbility.BasicAttack)==weaponClock,"Actual clock/weapon exclusion");ownerRoutes++;
                            long at=Core.TickCount;Require(Start(p,ability,enemy,corpse,at),"Actual SC start: "+ability);var s=ClassCombat.For(p);
                            Require(s.Timing==expected&&s.EffectAt==at+expected.EffectMs&&s.NextAction==at+expected.DurationMs
                                &&!Effect(p,ability,enemy,corpse)&&NecromancerEffects.ManaBalance256(p)==mana-(CorpseSkill(ability)?cost:0),"SC windup/payment: "+ability);
                            Require(!ClassCombat.ResolveAction(p,s.EffectAt-1),"Early SC event");
                            bool released=ClassCombat.ResolveAction(p,s.EffectAt);
                            Require(released==(ability!=CombatAbility.FindItem)&&!ClassCombat.ResolveAction(p,s.EffectAt)&&Effect(p,ability,enemy,corpse),"Effect/failed-search/duplicate SC event: "+ability);
                            int expectedMana=ability==CombatAbility.BattleOrders?BattleOrders.Rescale256(mana,maximum,NecromancerEffects.ManaMaximum256(p))-cost:mana-cost;
                            Require(NecromancerEffects.ManaBalance256(p)==expectedMana,"Original payment/rescale order: "+ability);
                            if(loot!=null){Require(loot.All(item=>!item.Deleted&&item.Parent==corpse),"Search changed original loot");}
                            if(!CorpseSkill(ability)){Require(!Start(p,ability,enemy,null,s.NextAction-1),"SC tail reentry");}casts++;
                        }
                        finally{corpse?.Delete();}
                    }
                    foreach(var ability in new[]{CombatAbility.Shout,CombatAbility.FindItem,CombatAbility.FindPotion})
                    {
                        ClearEffects(p,enemy);Ready(p);Corpse corpse=ability==CombatAbility.FindItem?FindItemChecks.Body(p):ability==CombatAbility.FindPotion?FindPotionChecks.Body(p):null;
                        try
                        {
                            int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,ability).Mana256;
                            Require(Start(p,ability,enemy,corpse,Core.TickCount),"Cancellation start");long effect=ClassCombat.For(p).EffectAt;ClassCombat.Stop(p);
                            Require(!ClassCombat.ResolveAction(p,effect)&&!Effect(p,ability,enemy,corpse)&&NecromancerEffects.ManaBalance256(p)==mana-(CorpseSkill(ability)?cost:0),"Cancellation refunded/replayed payment");cancellations++;
                        }
                        finally{corpse?.Delete();}
                    }
                }
                finally{Clean(p,enemy);}
            }
        }
        finally{BarbarianFindItem.Roll=itemRoll;BarbarianFindPotion.Roll=potionRoll;}
        Require(routes==2331&&ownerRoutes==18&&casts==18&&cancellations==6,"Missing Barbarian clock coverage");
        Log.Information("Barbarian SC clock:3108source comparisons,2331static clocks,18actual owner/gear/effect casts,6cancellations;dual hands,corpse start payment,Orders rescale and four unavailable skills retain");
    }
    private static void Input(NetState network,bool quick)
    {
        var p=Owner();if(quick){DualGear(p);}var ability=quick?CombatAbility.BattleCommand:CombatAbility.Shout;
        Require(SkillBindings.Instance.Assign(p,false,ability)&&SkillBindings.Instance.AssignQuick(p,7,ability),"Actual Barbarian bindings");
        var account=FixtureNetwork.Authenticate(network,p,"barbarian-clock-");Ready(p);int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,ability).Mana256;
        bool began=false,finished=false;string label=quick?"barbarian-cast-clock-quick":"barbarian-cast-clock-right";long deadline=Core.TickCount+10000;
        void Cleanup(){Clean(p);account.Delete();}
        void Inspect()
        {
            if(finished){return;}
            try
            {
                Require(Core.TickCount<deadline,"Barbarian SC input timeout");var s=ClassCombat.For(p);
                if(s.PendingAbility==ability&&!began)
                {Require(s.Timing==(quick?new ActionTiming(12,8):new ActionTiming(14,9))&&!Effect(p,ability)&&NecromancerEffects.ManaBalance256(p)==mana,"Wire SC/windup payment");began=true;Log.Information("{Label}: windup",label);}
                if(!Effect(p,ability)){Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Inspect));return;}
                Require(began&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Wire exact payment/effect");finished=true;Cleanup();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){finished=true;Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(()=>
        {Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)ability,(int)DiabloClass.Barbarian,0,p.X,p.Y,p.Z);Inspect();}));
    }
}
