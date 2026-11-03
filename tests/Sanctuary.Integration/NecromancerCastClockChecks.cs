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

public static class NecromancerCastClockChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(NecromancerCastClockChecks));
    private static void Freeze()
    {var mana=Mobile.ManaRegenRateHandler;Mobile.ManaRegenRateHandler=p=>p.Name=="NecromancerCastClockFixture"?TimeSpan.FromHours(1):mana?.Invoke(p)??Mobile.DefaultManaRate;}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Freeze();
    public static void Configure()
    {
        Freeze();Register("necromancer-cast-clock-check",Check);
        foreach(bool quick in new[]{false,true})
        {bool captured=quick;string name=quick?"necromancer-cast-clock-quick":"necromancer-cast-clock-right";Register(name+"-check",()=>FixtureNetwork.Connected(name+"-result",n=>Input(n,captured)));}
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Necromancer SC/FCR clock and actual casts",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {
        var p=PoisonDaggerChecks.Owner();p.Name="NecromancerCastClockFixture";
        foreach(string id in new[]{"necromancer.bonearmor","necromancer.amplifydamage"})
        {if(ClassCreation.Profiles.GetRank(p.Serial,id)==0){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train Necromancer SC");}}
        FixtureSpace.Corridor(p,6200,26,3600);Ready(p);return p;
    }
    private static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,50*256+17);}
    private static Item Magefist(PlayerMobile p)
    {var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Magefist").Id,99,new Random(173),true);p.AddToBackpack(item);Require(p.EquipItem(item),"Equip actual Magefist");ClassGear.Refresh(p);return item;}
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"necromancer-cast-clock-source-probe.json")));
        int comparisons=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var timing=NecromancerCastClock.Calculate(row[1].GetInt32());
            Require(timing.EffectFrame==row[3][0][1].GetInt32()&&timing.Frames==row[3][1][1].GetInt32(),"Necromancer complete source scheduling differential");comparisons++;
        }
        var skills=SkillMechanics.All.Where(r=>NecromancerCastClock.Handles(r.Ability)).ToArray();int routes=0;
        Require(comparisons==2072&&skills.Length==26&&!NecromancerCastClock.Handles(CombatAbility.PoisonDagger),"Incomplete SC source/routes or Poison Dagger captured");
        foreach(var row in skills)for(int i=0;i<259;i++)
        {
            int fcr=i<256?i:i==256?500:i==257?1000:10000;
            Require(CombatTiming.Calculate(DiabloClass.Necromancer,row.Ability,new(AttackFamily.Bow,-20),90,fcr,85,70)
                ==NecromancerCastClock.Calculate(fcr),"SC route leaked IAS/weapon modifier/chill/attackrate or generic clock");routes++;
        }
        var p=Owner();var enemy=PoisonDaggerChecks.Target(p);int casts=0,forRoutes=0;
        try
        {
            var weaponTiming=CombatTiming.For(p,CombatAbility.PoisonDagger);
            foreach(bool boosted in new[]{false,true})
            {
                Item gloves=boosted?Magefist(p):null;
                try
                {
                    int fcr=boosted?20:0;var expected=NecromancerCastClock.Calculate(fcr);
                    Require(CombatTiming.Fcr(p)==fcr&&CombatTiming.For(p,CombatAbility.PoisonDagger)==weaponTiming,"Real gear FCR or Poison Dagger weapon clock changed");
                    foreach(var row in skills){Require(CombatTiming.For(p,row.Ability)==expected,"Actual owner route missed shared SC clock");forRoutes++;}
                    foreach(var ability in new[]{CombatAbility.BoneArmor,CombatAbility.AmplifyDamage})
                    {
                        NecromancerEffects.Profiles.Clear(p);NecromancerEffects.Profiles.Clear(enemy);Ready(p);
                        int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,ability).Mana256;long at=Core.TickCount;
                        Require(ClassCombat.Perform(p,ability,ability==CombatAbility.AmplifyDamage?enemy:null,enemy.Location,at),"Actual learned Necromancer SC start");
                        var s=ClassCombat.For(p);
                        Require(s.Timing==expected&&s.EffectAt==at+expected.EffectMs&&s.NextAction==at+expected.DurationMs
                            &&NecromancerEffects.ManaBalance256(p)==mana,"Actual SC clock or unpaid windup");
                        Require(!ClassCombat.ResolveAction(p,s.EffectAt-1)&&ClassCombat.ResolveAction(p,s.EffectAt)&&!ClassCombat.ResolveAction(p,s.EffectAt),"Early/duplicate SC release");
                        Require(NecromancerEffects.ManaBalance256(p)==mana-cost
                            &&(ability==CombatAbility.BoneArmor?NecromancerEffects.Armor256(p)>0:NecromancerEffects.Curse(enemy)?.Ability==CombatAbility.AmplifyDamage),"Actual SC effect/payment missing");
                        Require(!ClassCombat.Perform(p,ability,null,p.Location,s.NextAction-1),"SC end event no longer bounds reentry");casts++;
                    }
                }
                finally{gloves?.Delete();ClassGear.Refresh(p);}
            }
            Require(routes==6734&&forRoutes==52&&casts==4,"Missing Necromancer clock coverage");
        }
        finally{NecromancerEffects.Profiles.Clear(p);NecromancerEffects.Profiles.Clear(enemy);enemy.Delete();p.Delete();}
        Log.Information("Necromancer SC clock:2072source comparisons,6734static route clocks,52actual owner routes,4learned gear/effect casts;Poison Dagger excluded and shared Amazon arithmetic retained");
    }
    private static void Input(NetState network,bool quick)
    {
        var p=Owner();var enemy=PoisonDaggerChecks.Target(p);var ability=quick?CombatAbility.AmplifyDamage:CombatAbility.BoneArmor;
        if(quick){Magefist(p);}
        Require(SkillBindings.Instance.Assign(p,false,ability)&&SkillBindings.Instance.AssignQuick(p,7,ability),"Real Necromancer bindings");
        var account=FixtureNetwork.Authenticate(network,p,"necro-clock-");Ready(p);
        int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,ability).Mana256;string label=quick?"necromancer-cast-clock-quick":"necromancer-cast-clock-right";
        bool began=false,finished=false;long limit=Core.TickCount+10000;
        void Cleanup(){NecromancerEffects.Profiles.Clear(p);NecromancerEffects.Profiles.Clear(enemy);enemy.Delete();p.Delete();account.Delete();}
        void Inspect()
        {
            if(finished){return;}
            try
            {
                Require(Core.TickCount<limit,"Necromancer SC real input timeout");var s=ClassCombat.For(p);
                if(s.PendingAbility==ability&&!began)
                {
                    Require(s.Timing==(quick?new ActionTiming(14,7):new ActionTiming(16,8))&&NecromancerEffects.ManaBalance256(p)==mana,"Real input source clock/unpaid windup");
                    began=true;Log.Information("{Label}: windup",label);
                }
                bool effect=quick?NecromancerEffects.Curse(enemy)?.Ability==CombatAbility.AmplifyDamage:NecromancerEffects.Armor256(p)>0;
                if(!effect){Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Inspect));return;}
                Require(began&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Real input missing windup or exact payment");
                finished=true;ClassCombat.Stop(p);Cleanup();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){finished=true;Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(()=>
        {
            Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)ability,(int)DiabloClass.Necromancer,quick?enemy.Serial.Value:0,enemy.X,enemy.Y,enemy.Z);
            Inspect();
        }));
    }
}
