// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ShapeRestrictionChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ShapeRestrictionChecks));
    private static bool _creating;private static int _scene;
    public static void Configure(){Rates();Register("shape-restriction-check",Check);Register("shape-restriction-reload-check",Reload);}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Rates()
    {
        bool Held(Mobile p)=>_creating||p.Name?.StartsWith("ShapeRestrictionFixture",StringComparison.Ordinal)==true;
        var old=Mobile.ManaRegenRateHandler;
        Mobile.ManaRegenRateHandler=p=>Held(p)?TimeSpan.FromHours(1):old?.Invoke(p)??Mobile.DefaultManaRate;
    }
    private static void Register(string name,Action check)=>ConsoleInputHandler.RegisterCommand(name,"Current shape restriction policy and real casting boundaries",_=>Core.LoopContext.Post(()=>
    {try{check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Train(PlayerMobile p,CombatAbility ability)
    {
        var definition=SkillMechanics.For(ability);TalentCatalog.TryGet(DiabloClass.Druid,definition.TalentId,out var talent);
        foreach(var prior in talent.Prerequisites)
        {if(ClassCreation.Profiles.GetRank(p.Serial,prior)==0){Train(p,SkillMechanics.All.Single(x=>x.TalentId==prior).Ability);}}
        if(ClassCreation.Profiles.GetRank(p.Serial,definition.TalentId)==0)
        {Require(ClassCreation.Profiles.TryTrain(p.Serial,definition.TalentId,out _),"Train "+definition.TalentId);}
    }
    private static PlayerMobile Fresh()
    {
        PlayerMobile p;_creating=true;try{p=new PlayerMobile{Body=0x190,Player=true,Name="ShapeRestrictionFixture"};}finally{_creating=false;}
        p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(p,99);
        foreach(var ability in new[]{CombatAbility.Werewolf,CombatAbility.Lycanthropy,CombatAbility.Werebear,CombatAbility.Firestorm,CombatAbility.CycloneArmor,CombatAbility.Raven}){Train(p,ability);}
        FixtureSpace.Corridor(p,6000,20,1100+32*_scene++);Ready(p);return p;
    }
    private static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);p.Frozen=p.Paralyzed=false;var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,p.ManaMax*256);}
    private static void Policy()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"shape-restrictions-source-probe.json")));
        var rows=SkillMechanics.All.ToDictionary(x=>x.SourceId);int compared=0;
        foreach(var row in data.RootElement.EnumerateArray())
        {
            int context=row[1].GetInt32();bool flags=row[2].GetInt32()!=0;
            bool active=flags&&context is 1 or 2 or 3;
            bool State(string name)=>flags&&((name=="wolf"&&context is 1 or 3)||(name=="bear"&&context is 2 or 3));
            Require(ShapeRestrictions.Allowed(rows[row[0].GetInt32()],active,State)==(row[3].GetInt32()!=0),"Complete source shape rule mismatch");compared++;
        }
        Require(compared==2400,"Incomplete source restriction matrix");
        var fire=SkillMechanics.For(CombatAbility.Firestorm);
        var fields=new Dictionary<string,JsonElement>(fire.SourceFields){["restrict"]=JsonSerializer.SerializeToElement(7)};
        bool rejected=false;try{ShapeRestrictions.Allowed(fire with{SourceFields=fields},true,_=>false);}catch(NotSupportedException){rejected=true;}
        Require(rejected,"Unsupported future restriction silently allowed");
        var fury=SkillMechanics.For(CombatAbility.Fury);
        fields=new(fury.SourceFields);fields.Remove("State1");fields["State2"]=JsonSerializer.SerializeToElement("wolf");
        Require(!ShapeRestrictions.Allowed(fury with{SourceFields=fields},true,name=>name=="wolf"),"Negative first-state sentinel skipped");
        Require(!ShapeRestrictions.Allowed((PlayerMobile)null,CombatAbility.BasicAttack),"Null owner accepted");
        Log.Information("Shape restrictions:2400native complete-source policy comparisons across240talents,5state contexts and flag presence; source mask eligibility remains separate from usability");
    }
    private static void Cast(PlayerMobile p,CombatAbility ability,Point3D point)
    {
        Ready(p);int before=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,ability).Mana256;
        Require(ClassCombat.Perform(p,ability,null,point,Core.TickCount),"Allowed transformed cast start "+ability);
        long effect=ClassCombat.For(p).EffectAt;ClassCombat.ReleaseCast(p);
        Require(ClassCombat.ResolveAction(p,effect)&&!ClassCombat.ResolveAction(p,effect),"Allowed transformed cast once "+ability);
        Require(NecromancerEffects.ManaBalance256(p)==before-cost,"Allowed transformed cast mana "+ability);
    }
    private static void Consumers()
    {
        var p=Fresh();
        try
        {
            foreach(var form in new[]{CombatAbility.Werewolf,CombatAbility.Werebear})
            {
                Ready(p);var point=new Point3D(p.X+2,p.Y,p.Z);int mana=NecromancerEffects.ManaBalance256(p);
                Require(ClassCombat.Available(p,CombatAbility.Firestorm)&&ClassCombat.Perform(p,CombatAbility.Firestorm,null,point,Core.TickCount),"Human fire start");
                long effect=ClassCombat.For(p).EffectAt;
                Require(DruidPlayerForms.ApplyPrepared(p,form,TimedEffects.Now),"Prepared form in unpaid windup");
                Require(!ClassCombat.ResolveAction(p,effect)&&ClassCombat.For(p).PendingAbility==CombatAbility.None
                    &&NecromancerEffects.ManaBalance256(p)==mana,"Form change during windup released or paid forbidden spell");
                Ready(p);mana=NecromancerEffects.ManaBalance256(p);long next=ClassCombat.For(p).NextAction;
                Require(!ClassCombat.Available(p,CombatAbility.Firestorm)&&!DruidCombat.Available(p,CombatAbility.Firestorm)
                    &&!ClassCombat.Perform(p,CombatAbility.Firestorm,null,point,Core.TickCount)
                    &&!DruidFirestorm.Begin(p,Serial.Zero,point,Core.TickCount)&&!DruidFirestorm.Release(p)
                    &&NecromancerEffects.ManaBalance256(p)==mana&&ClassCombat.For(p).NextAction==next,"Forbidden availability/start/direct release changed resources/action");
                Require(ShapeRestrictions.Allowed(p,CombatAbility.BasicAttack)&&ClassCombat.Available(p,CombatAbility.BasicAttack),"Common unarmed Attack excluded");
                Require(ShapeRestrictions.Allowed(p,CombatAbility.Hurricane)&&!ClassCombat.Available(p,CombatAbility.Hurricane),"Shape allowance bypassed untrained rank");
                Require(ShapeRestrictions.Allowed(p,CombatAbility.Fury)==(form==CombatAbility.Werewolf)
                    &&ShapeRestrictions.Allowed(p,CombatAbility.ShockWave)==(form==CombatAbility.Werebear)
                    &&ShapeRestrictions.Allowed(p,CombatAbility.FireClaws)&&!ClassCombat.Available(p,CombatAbility.FireClaws),"Form-specific eligibility invented a learned handler");
                Cast(p,CombatAbility.CycloneArmor,p.Location);Require(DruidCycloneArmor.State(p)!=null,"Allowed buff absent");
                Cast(p,CombatAbility.Raven,point);
                Ready(p);p.Frozen=true;
                Require(!ClassCombat.Perform(p,CombatAbility.CycloneArmor,null,p.Location,Core.TickCount),"Shape allowance bypassed freeze");p.Frozen=false;
                NecromancerEffects.SetMana256(p,0);
                Require(!ClassCombat.Perform(p,CombatAbility.CycloneArmor,null,p.Location,Core.TickCount),"Shape allowance bypassed mana");
                DruidPlayerForms.Clear(p);Ready(p);
                Require(ClassCombat.Available(p,CombatAbility.Firestorm),"Human eligibility not restored");
            }
        }
        finally{ClassCombat.Stop(p);DruidPlayerForms.Clear(p);DruidCycloneArmor.Clear(p);PersistentMinions.Dismiss(p);p.Delete();}
    }
    private static PlayerMobile SavedOwner()
    {
        using var manifest=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"druid-form-world-fixtures.json")));
        return World.FindMobile((Serial)manifest.RootElement.GetProperty("Owner").GetUInt32()) as PlayerMobile;
    }
    private static void Check()
    {
        Policy();Consumers();var p=SavedOwner();Require(p!=null,"Saved form fixture missing");
        Train(p,CombatAbility.Firestorm);Train(p,CombatAbility.CycloneArmor);Train(p,CombatAbility.Raven);Reload();
        Log.Information("Shape casting consumers:both prepared forms reject forbidden fire at availability/start/unpaid release,allow actual Cyclone/Raven once with mana,and preserve rank/mana/freeze guards; no form talent enabled");
    }
    private static void Reload()
    {
        var p=SavedOwner();Require(p!=null&&DruidPlayerForms.State(p)?.Form.Ability==CombatAbility.Werebear,"Saved bear shape context missing");
        Require(!ClassCombat.Available(p,CombatAbility.Firestorm)&&!DruidCombat.Available(p,CombatAbility.Firestorm)
            &&ClassCombat.Available(p,CombatAbility.CycloneArmor)&&ClassCombat.Available(p,CombatAbility.Raven)
            &&ShapeRestrictions.Allowed(p,CombatAbility.ShockWave)&&!ShapeRestrictions.Allowed(p,CombatAbility.Fury),"Restored availability ignored saved form");
    }
}
