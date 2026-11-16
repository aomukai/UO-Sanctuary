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

public static class WarpspearGrantChecks
{
    private const CombatAbility Tk=CombatAbility.Telekinesis,Es=CombatAbility.EnergyShield;
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(WarpspearGrantChecks));
    private sealed record Saved(int Generation,uint Owner,uint Staff,string Gear,int Life,int Mana,EnergyShieldState Shield);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"warpspear-grants-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    [CallPriority(int.MaxValue)]public static void Initialize()
    {
        var hp=ClassGearHooks.CanRegenerateHits;var mp=ClassGearHooks.CanRegenerateMana;
        ClassGearHooks.CanRegenerateHits=m=>m.Name!="WarpspearGrantFixture"&&hp?.Invoke(m)!=false;
        ClassGearHooks.CanRegenerateMana=m=>m.Name!="WarpspearGrantFixture"&&mp?.Invoke(m)!=false;
    }
    public static void Configure()
    {
        ConsoleInputHandler.RegisterCommand("warpspear-readiness-check","Compare complete named readiness with the preserved baseline",_=>Core.LoopContext.Post(()=>
        {try{Readiness();Log.Information("warpspear-readiness-check: passed");}catch(Exception e){Log.Error(e,"warpspear-readiness-check: failed");}}));
        foreach(bool again in new[]{false,true})
        {
            bool reload=again;string label=reload?"warpspear-grants-reload-check":"warpspear-grants-check";
            ConsoleInputHandler.RegisterCommand(label,"Complete Warpspear item grants,native consumers and original saves",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Label}: passed",label);FixtureNetwork.Connected(label+"-result",n=>
            {try{if(reload){Reload(n);}else{Scene(n);}Log.Information("{Label}-result: passed",label);}catch(Exception e){Log.Error(e,"{Label}-result: failed",label);}});}));
        }
        foreach(string choice in new[]{"tk-right","tk-quick","es-right","es-quick"})
        {
            string mode=choice,label="warpspear-"+choice;
            ConsoleInputHandler.RegisterCommand(label+"-check","Actual untrained Warpspear-granted spell packets",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Label}-check: passed",label);FixtureNetwork.Connected(label+"-result",n=>Input(n,mode));}));
        }
    }
    private static void Readiness()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"warpspear-before-readiness.json")));
        var rows=doc.RootElement.GetProperty("Definitions").EnumerateArray().ToArray();Require(rows.Length==573,"Incomplete named baseline");
        var changed=rows.Where(r=>r.GetProperty("Available").GetBoolean()!=NamedGear.Available(r.GetProperty("Id").GetString())).ToArray();
        Require(changed.Length==1&&changed[0].GetProperty("Id").GetString()=="unique/186"&&!changed[0].GetProperty("Available").GetBoolean()&&NamedGear.Available("unique/186"),"Direct grant update changed additional item readiness: "+string.Join(',',changed.Select(r=>r.GetProperty("Id").GetString())));
        Require(NamedGear.Available("unique/254"),"Retained complete Hellslayer admission regressed");
    }
    private static PlayerMobile Owner(DiabloClass type=DiabloClass.Sorceress)
    {
        var p=new PlayerMobile{Body=0x191,Female=true,Player=true,Name="WarpspearGrantFixture"};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,type);ClassCreation.Profiles.AdvanceLevel(p,99);
        foreach(var stat in new[]{CharacterStat.Strength,CharacterStat.Dexterity,CharacterStat.Energy})
        {while((stat==CharacterStat.Strength?p.RawStr:stat==CharacterStat.Dexterity?p.RawDex:p.RawInt)<100){Require(ClassCreation.Profiles.TrySpendStat(p,stat),"Ordinary stat allocation");}}
        FixtureSpace.Corridor(p,6900,34,2350);ClassGear.Refresh(p);p.Hits=p.HitsMax;p.Mana=p.ManaMax;return p;
    }
    private static Item Staff(PlayerMobile p)
    {
        Require(NamedGear.Available("unique/186"),"Whole Warpspear unavailable");var item=NamedGear.Generate("8bs",GearRarity.Unique,99,new Random(173),true);
        Require(item!=null&&ClassGear.State(item).NamedItemId=="unique/186","Normal weighted generation did not return original Warpspear");
        var props=NamedGear.IntrinsicProperties(item).ToArray();Require(props.Length==6&&props.Count(x=>x.Property.Code=="skill"&&x.Roll.Value==3)==3&&props.Any(x=>x.Property.Code=="sor"&&x.Roll.Value==3)&&props.Any(x=>x.Property.Code=="ignore-ac"&&x.Roll.Value==1)&&props.Any(x=>x.Property.Code=="ac-miss"&&x.Roll.Value==250),"Complete staff lost a source property");
        p.AddToBackpack(item);Require(p.EquipItem(item),"Equip original complete Warpspear");ClassGear.Refresh(p);return item;
    }
    private static GraveyardSkeleton Target(PlayerMobile p)
    {var t=FireBoltChecks.Target(p,4);t.Frozen=t.CantWalk=false;t.AIObject?.AITimer.Stop();return t;}
    private static void ClearShield(PlayerMobile p)
    {var s=SorceressEffects.Profiles.Find(p);if(s!=null){SorceressEffects.Profiles.Store(p,s with{Shield=null});}}
    private static void Ready(PlayerMobile p){ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,p.ManaMax*256);}
    private static void CastShield(PlayerMobile p)
    {
        Ready(p);ClearShield(p);int mana=NecromancerEffects.ManaBalance256(p);
        Require(ClassCombat.Perform(p,Es,null,p.Location,Core.TickCount),"Untrained item Shield startup");var s=ClassCombat.For(p);
        Require(!ClassCombat.ResolveAction(p,s.EffectAt-1)&&SorceressEffects.Shield(p)==null&&NecromancerEffects.ManaBalance256(p)==mana,"Shield paid/applied early");
        Require(ClassCombat.ResolveAction(p,s.EffectAt)&&SorceressEffects.Shield(p)is{Rank:6,AbsorbPercent:45}&&NecromancerEffects.ManaBalance256(p)==mana-1280,"Item Shield rank6/45percent/five mana mismatch");ClassCombat.Stop(p);
    }
    private static void ShieldHit(PlayerMobile p,int divisor)
    {
        var enemy=EncounterShieldChecks.Enemy(p);
        try
        {
            NecromancerEffects.SetMana256(p,100*256+17);int hp=SkillHealth.Balance256(p);AOS.Damage(p,enemy,10,true,100,0,0,0,0,spellDamage:true);
            int absorbed=1152,cost=absorbed*divisor/16;
            Require(SkillHealth.Balance256(p)==hp-1408&&NecromancerEffects.ManaBalance256(p)==100*256+17-cost&&SorceressEffects.Shield(p)is{Rank:6},"Native item Shield HP/mana/hard-only TK synergy differs");
        }
        finally{enemy.Delete();}
    }
    private static void CastTk(PlayerMobile p)
    {
        var t=Target(p);
        try
        {
            Ready(p);int hp=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p);var values=SkillMechanics.Values(p,Tk);var packet=TelekinesisCombat.Prepare(p,values);
            Require(packet.Rank==6&&ClassCombat.Perform(p,Tk,t,t.Location,Core.TickCount),"Untrained item TK startup");var s=ClassCombat.For(p);
            Require(!ClassCombat.ResolveAction(p,s.EffectAt-1)&&SkillHealth.Balance256(t)==hp&&NecromancerEffects.ManaBalance256(p)==mana,"TK paid/damaged early");
            Require(ClassCombat.ResolveAction(p,s.EffectAt)&&hp-SkillHealth.Balance256(t)>=packet.Minimum256&&hp-SkillHealth.Balance256(t)<=packet.Maximum256&&NecromancerEffects.ManaBalance256(p)==mana-1792&&NativeMonsterReturn.Session(t).LastHitClass==109&&CombatStuns.Remaining(t)==0,"Item TK real damage/class/cost/control differs");
        }
        finally{SourceKnockbacks.Clear(t);ClassCombat.Stop(p);t.Delete();}
    }
    private static void Suppressed(PlayerMobile p)
    {Require(SkillMechanics.Rank(p,Tk)==0&&SkillMechanics.Rank(p,Es)==0&&!ClassCombat.Available(p,Tk)&&!ClassCombat.Available(p,Es),"Inactive staff retained untrained grants");}
    private static void Scene(NetState network)
    {
        var p=Owner();var account=FixtureNetwork.Authenticate(network,p,"warpspear-grants-");Item staff=null;bool kept=false;
        try
        {
            Suppressed(p);staff=Staff(p);
            Require(SkillMechanics.Rank(p,Tk)==6&&SkillMechanics.Rank(p,Es)==6&&SkillMechanics.Rank(p,CombatAbility.Teleport)==6&&ClassCreation.Profiles.GetRank(p.Serial,"sorceress.telekinesis")==0&&ClassCreation.Profiles.GetRank(p.Serial,"sorceress.energyshield")==0,"Complete staff rank6 grants spent hard points or lost general/specific bonus");
            Require(ClassGear.Bonus(p,"ac-miss")==250&&ClassGear.Bonus(p,"ignore-ac")==1,"Whole staff lost nonspell effects");
            Require(SkillBindings.Instance.Assign(p,false,Tk)&&SkillBindings.Instance.AssignQuick(p,6,Tk)&&SkillBindings.Instance.AssignQuick(p,7,Es)&&!SkillBindings.Instance.Assign(p,true,Es),"Actual staff binding capabilities");CastTk(p);CastShield(p);ShieldHit(p,32);
            var shield=SorceressEffects.Shield(p);p.AddToBackpack(staff);ClassGear.Refresh(p);Suppressed(p);Require(SorceressEffects.Shield(p)==shield,"Gear removal erased captured Shield");ShieldHit(p,32);
            Require(p.EquipItem(staff),"Reequip staff");ClassGear.Refresh(p);var original=ClassGear.State(staff);ClassGear.Profiles.Store(staff,original with{Identified=false});ClassGear.Refresh(p);Suppressed(p);ClassGear.Profiles.Store(staff,original);ClassGear.Refresh(p);
            var weapon=(BaseWeapon)staff;int durability=weapon.HitPoints;weapon.HitPoints=0;ClassGear.Refresh(p);Suppressed(p);weapon.HitPoints=durability;ClassGear.Refresh(p);Require(SkillMechanics.Rank(p,Tk)==6&&SkillMechanics.Rank(p,Es)==6,"Restoration lost grant");
            var other=Owner(DiabloClass.Necromancer);p.AddToBackpack(staff);other.AddToBackpack(staff);Require(other.EquipItem(staff),"Other class original equip");ClassGear.Refresh(other);Suppressed(other);other.AddToBackpack(staff);p.AddToBackpack(staff);Require(p.EquipItem(staff),"Return exact traded original");ClassGear.Refresh(p);other.Delete();
            ClassSkillChecks.Train(p,"sorceress.telekinesis");Require(SkillMechanics.Rank(p,Tk)==7&&ClassCreation.Profiles.GetRank(p.Serial,"sorceress.telekinesis")==1&&SorceressEffects.Shield(p)==shield,"Normal hard point rewrote captured Shield");ShieldHit(p,31);
            SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-513);NecromancerEffects.SetMana256(p,37*256+17);ClassCombat.Stop(p);
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,staff.Serial.Value,JsonSerializer.Serialize(ClassGear.State(staff)),SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),shield)));kept=true;
        }
        finally{ClassCombat.Stop(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();if(!kept){staff?.Delete();p.Delete();}}
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(s.Generation<2,"Too many staff reloads");var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var staff=World.FindItem((Serial)s.Staff);
        Require(staff?.Parent==p&&JsonSerializer.Serialize(ClassGear.State(staff))==s.Gear&&SkillHealth.Balance256(p)==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana&&SorceressEffects.Shield(p)==s.Shield,"Saved original/rolls/resources/captured Shield changed");
        var account=FixtureNetwork.Authenticate(network,p,"warpspear-grants-reload-");
        try
        {
            ClassGear.Refresh(p);Require(SkillMechanics.Rank(p,Tk)==7&&SkillMechanics.Rank(p,Es)==6&&ClassCreation.Profiles.GetRank(p.Serial,"sorceress.telekinesis")==1&&ClassCreation.Profiles.GetRank(p.Serial,"sorceress.energyshield")==0&&SkillBindings.Instance.Get(p).Right==Tk&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Sorceress,6)==Tk&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Sorceress,7)==Es,"Saved hard/effective grants or bindings changed");
            ShieldHit(p,31);SkillHealth.SetBalance256(p,s.Life);NecromancerEffects.SetMana256(p,s.Mana);File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
        }
        finally{ClassCombat.Stop(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
    }
    private static void Input(NetState network,string mode)
    {
        string label="warpspear-"+mode;var ability=mode.StartsWith("tk",StringComparison.Ordinal)?Tk:Es;var p=Owner();var staff=Staff(p);var account=FixtureNetwork.Authenticate(network,p,"warpspear-wire-");Ready(p);GraveyardSkeleton target=ability==Tk?Target(p):null;
        Require(SkillBindings.Instance.Assign(p,false,ability)&&SkillBindings.Instance.AssignQuick(p,7,ability),"Wire staff grant binding");int mana=NecromancerEffects.ManaBalance256(p),life=target==null?0:SkillHealth.Balance256(target);bool began=false,done=false;long deadline=Core.TickCount+8000;
        void Cleanup(){ClassCombat.Stop(p);if(target!=null){SourceKnockbacks.Clear(target);target.Delete();}staff.Delete();p.Delete();account.Delete();}
        void Inspect()
        {
            if(done){return;}
            try
            {
                Require(Core.TickCount<deadline,"Staff-granted input timeout");var s=ClassCombat.For(p);
                if(!began&&s.PendingAbility==ability){Require(NecromancerEffects.ManaBalance256(p)==mana&&(target==null?SorceressEffects.Shield(p)==null:SkillHealth.Balance256(target)==life),"Wire grant paid/applied early");began=true;Log.Information("{Label}: windup",label);}
                if(target==null?SorceressEffects.Shield(p)!=null:SkillHealth.Balance256(target)<life)
                {
                    Require(began&&SkillMechanics.Rank(p,ability)==6&&ClassCreation.Profiles.GetRank(p.Serial,(ability==Tk?"sorceress.telekinesis":"sorceress.energyshield"))==0&&NecromancerEffects.ManaBalance256(p)==mana-(ability==Tk?1792:1280)&&(target==null?SorceressEffects.Shield(p)is{Rank:6,AbsorbPercent:45}:NativeMonsterReturn.Session(target).LastHitClass==109),"Actual untrained item spell rank/payment/result");done=true;Cleanup();Log.Information("{Label}-result: passed",label);return;
                }
                Timer.StartTimer(TimeSpan.FromMilliseconds(10),()=>Core.LoopContext.Post(Inspect));
            }
            catch(Exception e){done=true;Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(()=>
        {if(target==null){Log.Information("{Label}: ready ability={Ability} class={Class} x={X} y={Y} z={Z}",label,(byte)ability,(byte)DiabloClass.Sorceress,p.X,p.Y,p.Z);}else{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(byte)ability,(byte)DiabloClass.Sorceress,target.Serial.Value,target.X,target.Y,target.Z);}Inspect();}));
    }
}
