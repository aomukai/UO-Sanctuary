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
namespace Sanctuary.Integration;

public static class NecromancerTownChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(NecromancerTownChecks));
    private sealed record Saved(uint Owner,uint Golem,uint Material,int Life,int Mana,Point3D Origin,int Map,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"necro-town-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("necro-town-check",Check);Register("necro-town-reload-check",Reload);
        foreach(var entry in new[]{("necro-town-right",false,false),("necro-town-quick",true,false),("necro-town-armor-quick",true,true)})
        {var (label,quick,armor)=entry;Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,label,quick,armor)));}
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Pinned Necromancer town flags and parked allowed casts",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static PlayerMobile Owner(int row=3000)
    {var owner=ClassSkillChecks.Player();owner.Name="PoisonDaggerFixtureNecroTown";owner.AddItem(new Backpack());FixtureSpace.Corridor(owner,7000,24,row);Ready(owner);return owner;}
    private static void Ready(PlayerMobile owner)
    {PoisonDaggerChecks.Ready(owner);ClassCombat.For(owner).RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(owner,owner.ManaMax*256);}
    private static GuardedRegion Town(PlayerMobile owner)
    {var town=new GuardedRegion("NecroTownFixture-"+owner.Serial.Value,owner.Map,1000,new Rectangle2D(owner.X-1,owner.Y-1,3,3));town.Register();return town;}
    private static Corpse Body(PlayerMobile owner)
    {var corpse=DeathSentryExplosionChecks.Body(owner,new(owner.X+6,owner.Y,owner.Z));corpse.Owner?.Delete();return corpse;}
    private static void Flags(PlayerMobile owner,bool town)
    {
        var allowed=new[]{CombatAbility.BoneArmor,CombatAbility.ClayGolem,CombatAbility.BloodGolem,CombatAbility.IronGolem,CombatAbility.FireGolem};int rows=0;
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"Data/Sanctuary/Balance/class-skills.json")));
        foreach(var value in doc.RootElement.EnumerateArray())
        {
            if(value.GetProperty("characterClass").GetString()!="Necromancer"){continue;}
            var ability=(CombatAbility)value.GetProperty("ability").GetInt32();var fields=value.GetProperty("sourceFields");int permission=fields.TryGetProperty("InTown",out var flag)?flag.GetInt32():0;
            Require((permission==1)==allowed.Contains(ability)&&NecromancerCastRegions.Allowed(owner,ability)==(!town||permission==1),"Pinned town permission mismatch: "+ability);rows++;
        }
        Require(rows==30,"Missing Necromancer town table rows");
    }
    private static void Check()
    {
        var owner=Owner();var corpse=Body(owner);var target=PoisonDaggerChecks.Target(owner);target.MoveToWorld(new(owner.X+4,owner.Y,owner.Z),owner.Map);Flags(owner,false);
        var town=Town(owner);var before=CorpseSkillRules.Profiles.Find(corpse);int blocked=0;
        try
        {
            Flags(owner,true);
            foreach(var skill in SkillMechanics.All.Where(s=>s.CharacterClass==DiabloClass.Necromancer&&!s.Passive&&s.Number("InTown")==0))
            {
                Ready(owner);int mana=NecromancerEffects.ManaBalance256(owner);var session=ClassCombat.For(owner);long effect=session.EffectAt;
                Serial selected=NecromancerCombat.CorpseTarget(skill.Ability)?corpse.Serial:target.Serial;
                bool started=skill.Ability==CombatAbility.RaiseSkeleton?ClassCombat.CastSummon(owner,corpse.Serial,Core.TickCount):NecromancerCombat.Begin(owner,skill.Ability,selected,World.FindEntity(selected).Location,Core.TickCount);
                Require(!started&&!NecromancerCombat.ValidTarget(owner,skill.Ability,selected,target.Location)&&session.PendingAbility==CombatAbility.None&&session.EffectAt==effect
                    &&NecromancerEffects.ManaBalance256(owner)==mana&&CorpseSkillRules.Profiles.Find(corpse)==before&&!corpse.SanctuaryConsumed,"Town cast animated,paid or consumed: "+skill.Ability);
                if(skill.Ability==CombatAbility.RaiseSkeleton){Require(!SkeletonPets.CanRaise(owner,corpse)&&!SkeletonPets.Raise(owner,corpse),"Raise Skeleton bypassed town at release");}
                else{Require(!NecromancerCombat.Release(owner,skill.Ability,selected,target.Location),"Direct release bypassed town: "+skill.Ability);}blocked++;
            }
            Require(blocked==22&&CorpseExplosionCasts.Acquire(owner,corpse.Location)==null,"Active-skill domain or CE picker ignored town");
        }
        finally{town.Unregister();}
        // Same location/facet: changing the region between start and release
        // isolates the release guard from the ordinary movement cancellation.
        foreach(var ability in new[]{CombatAbility.CorpseExplosion,CombatAbility.RaiseSkeleton})
        {
            Ready(owner);int mana=NecromancerEffects.ManaBalance256(owner);bool started=ability==CombatAbility.RaiseSkeleton?ClassCombat.CastSummon(owner,corpse.Serial,Core.TickCount):ClassCombat.BeginCorpse(owner,ability,corpse.Serial,corpse.Location,Core.TickCount);
            Require(started,"Field start refused before region change: "+ability);town=Town(owner);
            try{Require(!ClassCombat.ResolveAction(owner,ClassCombat.For(owner).EffectAt)&&NecromancerEffects.ManaBalance256(owner)==mana&&CorpseSkillRules.Profiles.Find(corpse)==before&&!corpse.SanctuaryConsumed,"Entering town after windup still paid/consumed: "+ability);}
            finally{town.Unregister();}
        }
        Ready(owner);Require(ClassCombat.CastSummon(owner,corpse.Serial,Core.TickCount)&&ClassCombat.ResolveAction(owner,ClassCombat.For(owner).EffectAt)&&corpse.SanctuaryConsumed,"Field Raise Skeleton no longer works");
        foreach(var follower in(owner.AllFollowers??[]).OfType<BaseCreature>()){follower.AIObject.AITimer.Stop();}
        corpse.Delete();target.Delete();town=Town(owner);Item material=null;IronGolemBody retained=null;
        try
        {
            foreach(var ability in new[]{CombatAbility.BoneArmor,CombatAbility.ClayGolem,CombatAbility.BloodGolem,CombatAbility.FireGolem,CombatAbility.IronGolem})
            {
                Ready(owner);Serial selected=Serial.Zero;var point=new Point3D(owner.X+3,owner.Y+2,owner.Z);
                if(ability==CombatAbility.IronGolem){material=ReviveEquippedChecks.Named("The Diggler");material.MoveToWorld(point,owner.Map);selected=material.Serial;}
                int mana=NecromancerEffects.ManaBalance256(owner),cost=SkillMechanics.Values(owner,ability).Mana256;
                Require(NecromancerCombat.Begin(owner,ability,selected,point,Core.TickCount)&&NecromancerEffects.ManaBalance256(owner)==mana,"Allowed town windup refused or paid early: "+ability);
                Require(ClassCombat.ResolveAction(owner,ClassCombat.For(owner).EffectAt)&&NecromancerEffects.ManaBalance256(owner)==mana-cost,"Allowed town release did not pay once: "+ability);
                if(ability==CombatAbility.BoneArmor){Require(NecromancerEffects.Armor(owner)>0,"Town Bone Armor pool missing");}
                else
                {
                    var pet=NecromancerGolems.For(owner).Single();Require(MinionSafeAreas.Parked(pet)&&pet.Map==Map.Internal&&pet.ControlMaster==owner,"Town golem was active instead of parked: "+ability);
                    if(pet is IronGolemBody iron){retained=iron;Require(material.Parent==iron,"Town Iron material was not retained");}
                }
            }
            SkillHealth.SetBalance256(retained,SkillHealth.Maximum256(retained)*2/3+17);Ready(owner);
            var saved=new Saved(owner.Serial.Value,retained.Serial.Value,material.Serial.Value,SkillHealth.Balance256(retained),NecromancerEffects.ManaBalance256(owner),owner.Location,owner.Map.MapID);
            PersistentMinions.Suspend(owner);owner.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
        }
        finally{town.Unregister();}
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var golem=(IronGolemBody)World.FindMobile((Serial)saved.Golem);
        Require(golem.ControlMaster==owner&&MinionSafeAreas.Parked(golem)&&SkillHealth.Balance256(golem)==saved.Life&&World.FindItem((Serial)saved.Material)?.Parent==golem
            &&NecromancerEffects.ManaBalance256(owner)==saved.Mana&&NecromancerEffects.Armor(owner)>0,"Parked town golem,material,wounds,mana or armor changed");
        owner.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);var town=Town(owner);
        try{Flags(owner,true);}finally{town.Unregister();owner.Internalize();}
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
    }
    private static uint Sequence(PlayerMobile owner,bool quick)
    {
        if(!quick){return ClassCombat.For(owner).Sequence;}
        var sessions=(IDictionary)typeof(SkillBindingNetwork).GetField("_sessions",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        var session=sessions[owner.Serial];return session==null?0:(uint)session.GetType().GetField("Sequence",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(session);
    }
    private static void Input(NetState net,string label,bool quick,bool armor)
    {
        var owner=Owner(armor?2850:quick?2900:2950);var corpse=Body(owner);var before=CorpseSkillRules.Profiles.Find(corpse);var town=Town(owner);var ability=armor?CombatAbility.BoneArmor:CombatAbility.CorpseExplosion;
        Require(SkillBindings.Instance.Assign(owner,false,ability)&&SkillBindings.Instance.AssignQuick(owner,7,ability),"Town wire bindings");Ready(owner);
        int mana=NecromancerEffects.ManaBalance256(owner),cost=SkillMechanics.Values(owner,ability).Mana256;var point=armor?owner.Location:corpse.Location;
        var account=FixtureNetwork.Authenticate(net,owner,"necro-town-");long deadline=Core.TickCount+10000;bool began=false,acknowledged=false,finished=false;
        void End(){ClassCombat.Stop(owner);town.Unregister();corpse.Delete();owner.Delete();account.Delete();}
        void Inspect()
        {
            if(finished){return;}
            try
            {
                Require(net.Running&&Core.TickCount<deadline,"Town wire timeout");var session=ClassCombat.For(owner);
                if(Sequence(owner,quick)>0&&!acknowledged)
                {
                    if(!armor){Require(session.PendingAbility==CombatAbility.None&&NecromancerEffects.ManaBalance256(owner)==mana,"Denied town input started a cast");}
                    acknowledged=true;if(!armor){Log.Information("{Label}: acknowledged",label);}
                }
                // Let the real right-input release reach the parser before
                // deleting its owner/connection. Quick use has no release.
                if(!armor&&acknowledged&&(quick||Sequence(owner,false)>=2))
                {
                    Require(session.PendingAbility==CombatAbility.None&&NecromancerEffects.ManaBalance256(owner)==mana&&CorpseSkillRules.Profiles.Find(corpse)==before&&!corpse.SanctuaryConsumed,"Denied town wire animated,paid or consumed");
                    finished=true;End();Log.Information("{Label}-result: passed",label);return;
                }
                if(armor&&session.PendingAbility==ability&&!began){Require(NecromancerEffects.ManaBalance256(owner)==mana,"Town Armor paid during windup");began=true;Log.Information("{Label}: windup",label);}
                if(armor&&NecromancerEffects.Armor(owner)>0)
                {Require(acknowledged&&began&&NecromancerEffects.ManaBalance256(owner)==mana-cost,"Town Armor input/payment failed");finished=true;End();Log.Information("{Label}-result: passed",label);return;}
                Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Inspect));
            }
            catch(Exception e){finished=true;End();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target=0 x={X} y={Y} z={Z}",label,(int)ability,(int)DiabloClass.Necromancer,point.X,point.Y,point.Z);Inspect();});
    }
}
