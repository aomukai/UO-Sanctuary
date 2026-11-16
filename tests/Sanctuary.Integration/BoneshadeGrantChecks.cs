// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class BoneshadeGrantChecks
{
    private const CombatAbility Ability=CombatAbility.BoneWall;
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(BoneshadeGrantChecks));
    private sealed record Saved(int Generation,uint Owner,uint Wand,string Gear,int Life,int Mana,int Rank);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"boneshade-grant-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    [CallPriority(int.MaxValue)]public static void Initialize()
    {
        var hp=ClassGearHooks.CanRegenerateHits;var mp=ClassGearHooks.CanRegenerateMana;
        ClassGearHooks.CanRegenerateHits=m=>m.Name!="BoneshadeGrantFixture"&&hp?.Invoke(m)!=false;
        ClassGearHooks.CanRegenerateMana=m=>m.Name!="BoneshadeGrantFixture"&&mp?.Invoke(m)!=false;
    }
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {
            bool reload=again;string label=reload?"boneshade-grant-reload-check":"boneshade-grant-check";
            ConsoleInputHandler.RegisterCommand(label,"Complete Boneshade item grants,barrier casting and original saves",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Label}: passed",label);FixtureNetwork.Connected(label+"-result",n=>
            {try{if(reload){Reload(n);}else{Scene(n);}Log.Information("{Label}-result: passed",label);}catch(Exception e){Log.Error(e,"{Label}-result: failed",label);}});}));
        }
        foreach(string choice in new[]{"right","quick"})
        {
            string mode=choice,label="boneshade-grant-"+choice;
            ConsoleInputHandler.RegisterCommand(label+"-check","Actual item-granted untrained Bone Wall input",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Label}-check: passed",label);FixtureNetwork.Connected(label+"-result",n=>Input(n,mode));}));
        }
    }
    private static PlayerMobile Owner(DiabloClass type=DiabloClass.Necromancer)
    {
        var p=new PlayerMobile{Body=0x191,Female=true,Player=true,Name="BoneshadeGrantFixture"};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,type);ClassCreation.Profiles.AdvanceLevel(p,99);
        foreach(var stat in new[]{CharacterStat.Strength,CharacterStat.Dexterity,CharacterStat.Energy})
        {while((stat==CharacterStat.Strength?p.RawStr:stat==CharacterStat.Dexterity?p.RawDex:p.RawInt)<100){Require(ClassCreation.Profiles.TrySpendStat(p,stat),"Ordinary stat allocation");}}
        FixtureSpace.Corridor(p,6900,34,2450);ClassGear.Refresh(p);p.Hits=p.HitsMax;p.Mana=p.ManaMax;return p;
    }
    private static Item Wand(PlayerMobile p)
    {
        Require(NamedGear.Available("unique/351"),"Whole Boneshade remains gated");
        var wand=NamedGear.Generate("7bw",GearRarity.Unique,99,new Random(173),true);Require(wand!=null&&ClassGear.State(wand).NamedItemId=="unique/351","Weighted source acquisition did not return original Boneshade");
        var props=NamedGear.IntrinsicProperties(wand).ToArray();Require(props.Length==7&&props.Count(x=>x.Property.Code=="skill")==5&&props.Any(x=>x.Property.Code=="nec"&&x.Roll.Value==2)&&props.Any(x=>x.Property.Code=="cast2"&&x.Roll.Value==25),"Complete wand lost a source property");
        p.AddToBackpack(wand);Require(p.EquipItem(wand),"Equip original complete Boneshade");ClassGear.Refresh(p);return wand;
    }
    private static int ItemRank(Item wand)
    {
        var roll=NamedGear.IntrinsicProperties(wand).Single(x=>x.Property.Code=="skill"&&x.Property.Parameter?.ToString()=="Bone Wall").Roll.Value;
        Require(roll is 2 or 3,"Pinned Bone Wall roll range differs");return roll+2;
    }
    private static BoneBarrierBody[] Bodies(PlayerMobile p)=>SkillCombat.NearbySnapshot<BoneBarrierBody>(p.Map,p.Location,24).Where(b=>b.SummonMaster==p).ToArray();
    private static void Clear(PlayerMobile p){ClassCombat.Stop(p);foreach(var b in Bodies(p)){b.Delete();}}
    private static void Ready(PlayerMobile p){Clear(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,p.ManaMax*256);}
    private static void Cast(PlayerMobile p,int rank)
    {
        Ready(p);int mana=NecromancerEffects.ManaBalance256(p);Require(ClassCombat.Perform(p,Ability,null,new(p.X+6,p.Y,p.Z),Core.TickCount),"Item-granted barrier startup");var s=ClassCombat.For(p);
        Require(s.Timing==NecromancerCastClock.Calculate(CombatTiming.Fcr(p))&&!ClassCombat.ResolveAction(p,s.EffectAt-1)&&NecromancerEffects.ManaBalance256(p)==mana,"Item FCR/unpaid windup differs");
        Require(ClassCombat.ResolveAction(p,s.EffectAt)&&Bodies(p).Length>0,"Item-granted Bone Wall did not release");var expected=BoneBarrierBirth.Capture(SkillMechanics.Values(p,Ability),SummonDifficulties.For(p.Map));
        Require(expected.Rank==rank&&Bodies(p).All(b=>b.Prepared==expected&&SkillHealth.Maximum256(b)==expected.Numbers.Life256)&&NecromancerEffects.ManaBalance256(p)==mana-SkillMechanics.Values(p,Ability).Mana256,"Real barrier did not use item effective rank/hard synergies/one payment");Clear(p);
    }
    private static void Scene(NetState network)
    {
        var p=Owner();var account=FixtureNetwork.Authenticate(network,p,"boneshade-grant-");Item wand=null;bool kept=false;
        try
        {
            Require(!ClassCombat.Available(p,Ability)&&ClassCreation.Profiles.GetRank(p.Serial,"necromancer.bonewall")==0,"Fresh owner learned Bone Wall");wand=Wand(p);int bonus=ItemRank(wand);
            Require(SkillMechanics.Rank(p,Ability)==bonus&&ClassCombat.Available(p,Ability)&&ClassCreation.Profiles.GetRank(p.Serial,"necromancer.bonewall")==0,"Specific item grant missing or spent hard points");Cast(p,bonus);
            Require(!SkillBindings.Instance.Assign(p,true,Ability)&&SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Granted barrier bindings");
            p.AddToBackpack(wand);ClassGear.Refresh(p);Require(SkillMechanics.Rank(p,Ability)==0&&!ClassCombat.Available(p,Ability),"Unequipped wand retained grant");Require(p.EquipItem(wand),"Reequip original");ClassGear.Refresh(p);
            var state=ClassGear.State(wand);ClassGear.Profiles.Store(wand,state with{Identified=false});ClassGear.Refresh(p);Require(SkillMechanics.Rank(p,Ability)==0,"Unidentified wand retained grant");ClassGear.Profiles.Store(wand,state);ClassGear.Refresh(p);
            var weapon=(BaseWeapon)wand;int durability=weapon.HitPoints;weapon.HitPoints=0;ClassGear.Refresh(p);Require(SkillMechanics.Rank(p,Ability)==0,"Broken wand retained grant");weapon.HitPoints=durability;ClassGear.Refresh(p);Require(SkillMechanics.Rank(p,Ability)==bonus,"Repair lost original grant");
            var other=Owner(DiabloClass.Sorceress);p.AddToBackpack(wand);other.AddToBackpack(wand);Require(other.EquipItem(wand),"Cross-class original equip");ClassGear.Refresh(other);
            Require(SkillMechanics.Rank(other,Ability)==0&&!ClassCombat.Available(other,Ability),"Necromancer staffmod granted another class");other.AddToBackpack(wand);p.AddToBackpack(wand);Require(p.EquipItem(wand),"Return same traded original");ClassGear.Refresh(p);other.Delete();
            ClassSkillChecks.Train(p,"necromancer.bonewall");Require(SkillMechanics.Rank(p,Ability)==bonus+1&&ClassCreation.Profiles.GetRank(p.Serial,"necromancer.bonewall")==1,"Item rank changed hard training");Cast(p,bonus+1);
            SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-513);NecromancerEffects.SetMana256(p,37*256+17);Clear(p);
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,wand.Serial.Value,JsonSerializer.Serialize(ClassGear.State(wand)),SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),bonus+1)));kept=true;
        }
        finally{Clear(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();if(!kept){wand?.Delete();p.Delete();}}
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(s.Generation<2,"Too many grant reloads");var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var wand=World.FindItem((Serial)s.Wand);
        Require(wand?.Parent==p&&JsonSerializer.Serialize(ClassGear.State(wand))==s.Gear&&SkillHealth.Balance256(p)==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana,"Saved original rolls/equipment/fractional resources changed");
        var account=FixtureNetwork.Authenticate(network,p,"boneshade-grant-reload-");
        try
        {
            ClassGear.Refresh(p);Require(SkillMechanics.Rank(p,Ability)==s.Rank&&ClassCreation.Profiles.GetRank(p.Serial,"necromancer.bonewall")==1&&SkillBindings.Instance.Get(p).Right==Ability&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Necromancer,7)==Ability,"Saved grant/hard rank/bindings changed");Cast(p,s.Rank);
            SkillHealth.SetBalance256(p,s.Life);NecromancerEffects.SetMana256(p,s.Mana);File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
        }
        finally{Clear(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
    }
    private static void Input(NetState network,string mode)
    {
        string label="boneshade-grant-"+mode;var p=Owner();var wand=Wand(p);int rank=ItemRank(wand);var account=FixtureNetwork.Authenticate(network,p,"boneshade-grant-wire-");Ready(p);
        Require(SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Wire item grant binding");int mana=NecromancerEffects.ManaBalance256(p);bool began=false,done=false;long deadline=Core.TickCount+8000;
        void Cleanup(){Clear(p);wand.Delete();p.Delete();account.Delete();}
        void Inspect()
        {
            if(done){return;}
            try
            {
                Require(Core.TickCount<deadline,"Granted skill input timeout");var s=ClassCombat.For(p);
                if(!began&&s.PendingAbility==Ability){Require(NecromancerEffects.ManaBalance256(p)==mana&&Bodies(p).Length==0,"Early grant payment/allocation");began=true;Log.Information("{Label}: windup",label);}
                if(Bodies(p).Length>0)
                {
                    var expected=BoneBarrierBirth.Capture(SkillMechanics.Values(p,Ability),SummonDifficulties.For(p.Map));
                    Require(began&&expected.Rank==rank&&Bodies(p).All(b=>b.Prepared==expected)&&NecromancerEffects.ManaBalance256(p)==mana-SkillMechanics.Values(p,Ability).Mana256&&ClassCreation.Profiles.GetRank(p.Serial,"necromancer.bonewall")==0,"Actual untrained item-granted wall snapshot/payment/hard rank");done=true;Cleanup();Log.Information("{Label}-result: passed",label);return;
                }
                Timer.StartTimer(TimeSpan.FromMilliseconds(10),()=>Core.LoopContext.Post(Inspect));
            }
            catch(Exception e){done=true;Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(()=>
        {Log.Information("{Label}: ready ability={Ability} class={Class} x={X} y={Y} z={Z}",label,(byte)Ability,(byte)DiabloClass.Necromancer,p.X+6,p.Y,p.Z);Inspect();}));
    }
}
