// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
using Server.Regions;
using Server.Spells;
namespace Sanctuary.Integration;

public static class CorpseTownChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(CorpseTownChecks));
    private sealed record Saved(uint Owner,uint Corpse,CorpseSkillRecord Row,Point3D Origin,int Map,int Mana,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"corpse-town-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static readonly CombatAbility[] Abilities=[CombatAbility.CorpseExplosion,CombatAbility.PoisonExplosion];
    public static void Configure()
    {
        Register("corpse-town-check",Check);Register("corpse-town-reload-check",Reload);
        foreach(var ability in Abilities)foreach(bool quick in new[]{false,true})
        {var a=ability;bool q=quick;string label="corpse-town-"+(a==CombatAbility.CorpseExplosion?"ce":"poison")+(q?"-quick":"-right");Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,label,a,q)));}
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Shared explosion target-town guards and conserved original corpses",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static PlayerMobile Owner(int row=2500)
    {
        var owner=PoisonDaggerChecks.Owner();owner.Name="PoisonDaggerFixtureCorpseTown";ClassSkillChecks.Train(owner,"necromancer.corpseexplosion");ClassSkillChecks.Train(owner,"necromancer.poisonexplosion");
        FixtureSpace.Corridor(owner,7000,24,row);Ready(owner);return owner;
    }
    private static void Ready(PlayerMobile owner)
    {PoisonDaggerChecks.Ready(owner);ClassCombat.For(owner).RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(owner,owner.ManaMax*256);}
    private static Corpse Body(PlayerMobile owner,int dx=5,int dy=0)
    {var corpse=DeathSentryExplosionChecks.Body(owner,new(owner.X+dx,owner.Y+dy,owner.Z));corpse.Owner?.Delete();corpse.BeginDecay(FixtureSpace.Retention);return corpse;}
    private static GuardedRegion Town(PlayerMobile owner,Corpse corpse)
    {
        var town=new GuardedRegion("CorpseTownFixture-"+corpse.Serial.Value,owner.Map,1000,new Rectangle2D(corpse.X,corpse.Y,1,1));town.Register();
        Require(!SpellHelper.IsTown(owner.Location,owner)&&SpellHelper.IsTown(corpse.Location,owner),"Fixture did not separate caster and corpse town locations");return town;
    }
    private static void Conserved(PlayerMobile owner,Corpse corpse,CorpseSkillRecord row,int mana)
    {
        Require(!corpse.SanctuaryConsumed&&!corpse.Animated&&corpse.GetAmount(typeof(Gold))==1&&CorpseSkillRules.Profiles.Find(corpse)==row
            &&NecromancerEffects.ManaBalance256(owner)==mana&&!NecromancerSpellMissiles.Snapshot().Flights.Any(x=>x.Owner==owner.Serial.Value),"Forbidden corpse cast changed loot,seed,flags,mana or emitted clouds");
    }
    private static void Rejected(PlayerMobile owner,Corpse corpse)
    {
        foreach(var ability in Abilities)
        {
            Ready(owner);var row=CorpseSkillRules.Profiles.Find(corpse);int mana=NecromancerEffects.ManaBalance256(owner);long effect=ClassCombat.For(owner).EffectAt;
            Require(!CorpseCastTargets.Usable(owner,ability,corpse)&&!NecromancerCombat.ValidTarget(owner,ability,corpse.Serial,owner.Location)
                &&!ClassCombat.BeginCorpse(owner,ability,corpse.Serial,owner.Location,Core.TickCount)
                &&!NecromancerCombat.Release(owner,ability,corpse.Serial,corpse.Location),"Explicit corpse target bypassed recipient town: "+ability);
            Require(!ClassCombat.BeginCorpse(owner,ability,Serial.Zero,corpse.Location,Core.TickCount)&&ClassCombat.For(owner).PendingAbility==CombatAbility.None&&ClassCombat.For(owner).EffectAt==effect,"Ground corpse town cast started an animation");
            var values=SkillMechanics.Values(owner,ability);bool direct=ability==CombatAbility.CorpseExplosion?CorpseExplosionCasts.Release(owner,corpse,values):NecromancerSpellMissiles.ReleaseClouds(owner,corpse,values);
            Require(!direct,"Internal release bypassed recipient town");Conserved(owner,corpse,row,mana);
        }
    }
    private static void Check()
    {
        var owner=Owner();var corpse=Body(owner);var field=Body(owner,6,1);var row=CorpseSkillRules.Profiles.Find(corpse);var town=Town(owner,corpse);
        try
        {
            field.MoveToWorld(field.Location,Map.Felucca);Rejected(owner,corpse);field.MoveToWorld(new(owner.X+6,owner.Y+1,owner.Z),owner.Map);
            foreach(var ability in Abilities)
            {
                Require(CorpseCastTargets.Acquire(owner,ability,corpse.Location)==field,"Town corpse blocked the next nearby field corpse");
                field.MoveToWorld(field.Location,Map.Felucca);Require(CorpseCastTargets.Acquire(owner,ability,corpse.Location)==null,"Town-only corpse acquired");field.MoveToWorld(new(owner.X+6,owner.Y+1,owner.Z),owner.Map);
                town.GuardsDisabled=true;Require(CorpseCastTargets.Usable(owner,ability,corpse),"Disabled native town remained forbidden");town.GuardsDisabled=false;
            }
        }
        finally{town.Unregister();}
        foreach(var ability in Abilities)
        {
            Ready(owner);int mana=NecromancerEffects.ManaBalance256(owner);
            Require(ClassCombat.BeginCorpse(owner,ability,corpse.Serial,corpse.Location,Core.TickCount),"Field corpse begin failed");town=Town(owner,corpse);
            try{Require(!ClassCombat.ResolveAction(owner,ClassCombat.For(owner).EffectAt),"Town region added during windup still released");Conserved(owner,corpse,row,mana);}
            finally{town.Unregister();}
            // Keep a second eligible corpse nearby: refusing the locked
            // primary must not silently detonate that replacement.
            Require(!field.SanctuaryConsumed,"Release reacquired the field corpse");
            Ready(owner);Require(ClassCombat.BeginCorpse(owner,ability,corpse.Serial,corpse.Location,Core.TickCount)&&ClassCombat.ResolveAction(owner,ClassCombat.For(owner).EffectAt),"Field positive release failed");
            Require(corpse.SanctuaryConsumed&&NecromancerEffects.ManaBalance256(owner)==mana-SkillMechanics.Values(owner,ability).Mana256,"Field release lost normal payment/consumption");
            NecromancerSpellMissiles.Cancel(owner);corpse.Delete();corpse=Body(owner);row=CorpseSkillRules.Profiles.Find(corpse);
        }
        field.Delete();Save(owner,corpse,0);
    }
    private static void Save(PlayerMobile owner,Corpse corpse,int generation)
    {
        Ready(owner);File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,corpse.Serial.Value,CorpseSkillRules.Profiles.Find(corpse),owner.Location,owner.Map.MapID,NecromancerEffects.ManaBalance256(owner),generation)));owner.Internalize();
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var corpse=(Corpse)World.FindItem((Serial)saved.Corpse);owner.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);
        Require(corpse.Owner==null&&CorpseSkillRules.Profiles.Find(corpse)==saved.Row&&NecromancerEffects.ManaBalance256(owner)==saved.Mana,"Saved corpse town metadata/seed/mana changed");
        var town=Town(owner,corpse);try{Rejected(owner,corpse);}finally{town.Unregister();}
        foreach(var ability in Abilities)
        {Ready(owner);Require(ClassCombat.BeginCorpse(owner,ability,corpse.Serial,corpse.Location,Core.TickCount),"Removing native town did not restore field casting");ClassCombat.Stop(owner);}
        Save(owner,corpse,saved.Generation+1);
    }
    private static uint Sequence(PlayerMobile owner,bool quick)
    {
        if(!quick){return ClassCombat.For(owner).Sequence;}
        var sessions=(IDictionary)typeof(SkillBindingNetwork).GetField("_sessions",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);var session=sessions[owner.Serial];
        return session==null?0:(uint)session.GetType().GetField("Sequence",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(session);
    }
    private static void Input(NetState net,string label,CombatAbility ability,bool quick)
    {
        var owner=Owner(ability==CombatAbility.CorpseExplosion?quick?2450:2400:quick?2350:2300);var corpse=Body(owner);var row=CorpseSkillRules.Profiles.Find(corpse);var town=Town(owner,corpse);
        Require(SkillBindings.Instance.Assign(owner,false,ability)&&SkillBindings.Instance.AssignQuick(owner,7,ability),"Corpse town wire bindings");Ready(owner);int mana=NecromancerEffects.ManaBalance256(owner);long effect=ClassCombat.For(owner).EffectAt;
        var account=FixtureNetwork.Authenticate(net,owner,"corpse-town-");bool acknowledged=false,finished=false;long deadline=Core.TickCount+10000;
        void End(){ClassCombat.Stop(owner);town.Unregister();corpse.Delete();owner.Delete();account.Delete();}
        void Inspect()
        {
            if(finished){return;}
            try
            {
                Require(net.Running&&Core.TickCount<deadline,"Corpse town input timeout");var session=ClassCombat.For(owner);
                if(Sequence(owner,quick)>0&&!acknowledged){acknowledged=true;Log.Information("{Label}: acknowledged",label);}
                if(acknowledged&&(quick||Sequence(owner,false)>=2))
                {
                    Require(session.PendingAbility==CombatAbility.None&&session.EffectAt==effect,"Town corpse wire animated");Conserved(owner,corpse,row,mana);finished=true;End();Log.Information("{Label}-result: passed",label);return;
                }
                Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Inspect));
            }
            catch(Exception e){finished=true;End();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)ability,(int)DiabloClass.Necromancer,quick?0:corpse.Serial.Value,corpse.X,corpse.Y,corpse.Z);Inspect();});
    }
}
