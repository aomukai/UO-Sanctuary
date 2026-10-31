// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;
public static class SkeletonBirthChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(SkeletonBirthChecks));
    private sealed record Saved(uint Owner,uint Pet,Point3D Point,int Map,int Life256,int Maximum256,int Rate,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"skeleton-birth-fixtures.json");
    public static void Configure()
    {
        Register("skeleton-birth-check",Check);Register("skeleton-birth-reload-check",Reload);
        foreach(bool quick in new[]{false,true}){bool q=quick;Register(q?"skeleton-birth-quick-check":"skeleton-birth-right-check",()=>FixtureNetwork.Connected(q?"skeleton-birth-quick-result":"skeleton-birth-right-result",n=>Input(n,q)));}
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Regular skeleton exact birth resources",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static void Train(PlayerMobile p,string id,int count=1)
    {TalentCatalog.TryGet(DiabloClass.Necromancer,id,out var row);foreach(string pre in row.Prerequisites){Train(p,pre);}while(ClassCreation.Profiles.GetRank(p.Serial,id)<count){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}}
    private static PlayerMobile Owner()
    {
        var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureSkeletonBirth";Train(p,"necromancer.raiseskeleton",4);
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.RaiseSkeleton)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.RaiseSkeleton),"Skeleton right/quick bindings");return p;
    }
    private static Corpse Corpse(PlayerMobile p)
    {var mob=new GraveyardZombie{NoKillAwards=true};mob.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);mob.Damage(10000,p);if(mob.Alive){mob.Kill();}var corpse=mob.Corpse as Corpse;Require(SkeletonPets.Usable(p,corpse),"Skeleton actual corpse");corpse.DropItem(new Gold(19));return corpse;}
    private static void Stop(SkeletonGuardian pet){pet.AIObject.AITimer.Stop();pet.SuspendActions();}
    private static RaisedSkeleton Cast(PlayerMobile p,Corpse corpse)
    {
        PoisonDaggerChecks.Ready(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);int mana=NecromancerEffects.ManaBalance256(p);
        Require(ClassCombat.CastSummon(p,corpse.Serial,Core.TickCount),"Skeleton learned windup");var session=ClassCombat.For(p);
        Require(!ClassCombat.ResolveAction(p,session.EffectAt-1)&&NecromancerEffects.ManaBalance256(p)==mana&&!corpse.SanctuaryConsumed,"Skeleton early payment/body use");
        Require(ClassCombat.ResolveAction(p,session.EffectAt)&&!ClassCombat.ResolveAction(p,session.EffectAt)&&NecromancerEffects.ManaBalance256(p)==mana-9*256&&corpse.SanctuaryConsumed,"Skeleton once-only9mana/corpse");
        var pet=NecromancerSkeletons.For(p,CombatAbility.RaiseSkeleton).OfType<RaisedSkeleton>().Last();Stop(pet);return pet;
    }
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"skeleton-birth-source-cases.json")));int count=0,unsupported=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            int V(int i)=>row[i].GetInt32();int initial=new[]{21,30,42}[V(2)]*256,after=initial+V(3)*8*256,percent=Math.Max(0,V(1)-3)*50;
            bool overflow=after<=1048576&&(long)after*percent>int.MaxValue;
            try
            {
                var b=NecromancerSkeletonBirth.Prepare(V(0),V(1),V(3),(MonsterDifficulty)V(2));
                Require(!overflow&&b.Level==V(4)&&b.Life256==V(5)&&V(5)==V(6)&&b.Armor==V(7)&&b.Attack==V(8)&&b.Regeneration256==V(9)
                    &&b.FlatDamage==V(10)&&b.DamagePercent==V(11)&&b.RequestedEquipmentLevel==V(12)&&V(17)==0,$"Skeleton complete birth context {count}: actual={JsonSerializer.Serialize(b)},expected={row.GetRawText()},overflow={overflow}");
            }
            catch(NotSupportedException){Require(overflow,"Unexpected source domain rejection "+count);unsupported++;}
            count++;
        }
        Require(count==11475&&unsupported>0,"Missing complete skeleton domains/overflow refusal");
        Log.Information("Skeleton birth: {Count} complete source contexts; {Unsupported} extreme signed-product domains explicitly refused",count,unsupported);
    }
    private static void Recovery(RaisedSkeleton pet)
    {
        int maximum=SkillHealth.Maximum256(pet);Require(maximum==8064&&pet.HitsMax==32&&SkillHealth.Balance256(pet)==8064&&pet.SourceRegeneration256==7,"Regular31.5HP/rate7 birth");
        var enemy=new GraveyardSkeleton{NoKillAwards=true};try
        {Require(PoisonStates.Drain(enemy,pet,1)==1&&SkillHealth.Balance256(pet)==8063,"Fractional incoming life lane");}finally{enemy.Delete();}
        long now=Core.TickCount+10000;pet.ResetLifeRecovery();pet.RecoverLife(now);pet.RecoverLife(now+39);Require(SkillHealth.Balance256(pet)==8063,"Skeleton healed before40ms");
        pet.RecoverLife(now+40);Require(SkillHealth.Balance256(pet)==8064&&pet.Hits==32&&SkillHealth.Remainder256(pet)==128,"Exact full life/recovery cap");
        SkillHealth.SetBalance256(pet,maximum-100);pet.ResetLifeRecovery();pet.RecoverLife(now+80);pet.RecoverLife(now+120);Require(SkillHealth.Balance256(pet)==maximum-93,"Skeleton MAXHP callback rate");
        TimedEffects.PreventHealing(pet,TimedEffects.Now);int before=SkillHealth.Balance256(pet);pet.RecoverLife(now+1120);Require(SkillHealth.Balance256(pet)==before,"Skeleton PMH recovery");
        TimedEffects.Clear(pet);pet.ResetLifeRecovery();
    }
    private static void Buff(RaisedSkeleton pet)
    {
        var barb=new PlayerMobile{Player=true,Body=0x190};ClassCreation.Profiles.Assign(barb,DiabloClass.Barbarian);
        try
        {
            int before=SkillHealth.Balance256(pet),old=SkillHealth.Maximum256(pet);BattleOrders.Apply(barb,pet,1,TimedEffects.Now);int max=SkillHealth.Maximum256(pet);
            Require(max==old+SourcePercentage.Calculate(old,BattleOrders.Percent(pet),100)&&SkillHealth.Balance256(pet)==(int)(before/(double)old*max)
                &&BattleOrders.Regeneration256(pet)==4*(max>>8)>>4,"Skeleton BO life/ratio/regeneration callback");
            before=SkillHealth.Balance256(pet);BattleOrders.Clear(pet);Require(SkillHealth.Maximum256(pet)==old&&SkillHealth.Balance256(pet)==(int)(before/(double)max*old)&&BattleOrders.Regeneration256(pet)==7,"Skeleton BO removal");
        }finally{BattleOrders.Clear(pet);barb.Delete();}
    }
    private static void Facets(PlayerMobile p,RaisedSkeleton pet)
    {
        var facets=SummonDifficulties.Facets();var point=p.Location;var map=p.Map;int mana=NecromancerEffects.ManaBalance256(p);
        try
        {
            SummonDifficulties.SetFacets([new(Map.Trammel.MapID,MonsterDifficulty.Normal),new(Map.Felucca.MapID,MonsterDifficulty.Nightmare),new(Map.Ilshenar.MapID,MonsterDifficulty.Hell)]);
            foreach(var next in new[]{Map.Felucca,Map.Ilshenar,Map.Trammel})
            {
                int old=SkillHealth.Maximum256(pet),life=SkillHealth.Balance256(pet);p.MoveToWorld(point,next);int max=SkillHealth.Maximum256(pet);
                Require(SkillHealth.Balance256(pet)==(int)((long)life*max/old)&&pet.ControlMaster==p,"Skeleton facet ratio/identity");
                var b=NecromancerSkeletonBirth.Prepare(99,4,0,SummonDifficulties.For(next));Require(pet.SourceLife256==b.Life256&&BattleOrders.Regeneration256(pet)==b.Regeneration256,"Skeleton difficulty resources");
            }
            Require(NecromancerEffects.ManaBalance256(p)==mana,"Facet recast/paid mana");
        }finally{p.MoveToWorld(point,map);SummonDifficulties.SetFacets(facets);}
    }
    private static void Check()
    {
        Source();var p=Owner();var corpse=Corpse(p);var pet=Cast(p,corpse);corpse.Delete();
        Require(pet.CombatProfile.Level==78&&pet.CombatProfile.Defense==533&&pet.CombatProfile.AttackRating==902,"Source pet level/monlvl/auras");
        Recovery(pet);SkillHealth.SetBalance256(pet,4013);Buff(pet);Facets(p,pet);
        var captured=pet.SourceLife256;Train(p,"necromancer.skeletonmastery");PoisonDaggerChecks.Named(p,"Wormskull");
        Require(pet.Rank==4&&pet.Mastery==0&&pet.SourceLife256==captured,"Existing skeleton reread live owner ranks");
        var legacy=new SkeletonGuardian();try{legacy.Initialize(p,4,0);Require(legacy.GetType()==typeof(SkeletonGuardian)&&legacy.HitsMax==31,"Legacy actor resource path silently migrated");}finally{legacy.Delete();}
        SkillHealth.SetBalance256(pet,4013);var saved=new Saved(p.Serial.Value,pet.Serial.Value,p.Location,p.Map.MapID,4013,8064,BattleOrders.Regeneration256(pet),0);
        PersistentMinions.Suspend(p);p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
        Log.Information("Skeleton birth: learned corpse/payment,8064FP life/rate7,pet level78/armor533/AR902,exact damage/healing,PMH,BO,facets,captured ranks and legacy isolation pass");
    }
    private static void Reload()
    {
        Source();var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var pet=(RaisedSkeleton)World.FindMobile((Serial)s.Pet);
        Require(pet.ControlMaster==p&&pet.Map==Map.Internal&&pet.Rank==4&&pet.Mastery==0&&pet.CombatProfile.Level==78&&SkillHealth.Balance256(pet)==s.Life256
            &&SkillHealth.Maximum256(pet)==s.Maximum256&&BattleOrders.Regeneration256(pet)==s.Rate,"Saved exact skeleton resources/captured ranks");
        p.MoveToWorld(s.Point,Map.Maps[s.Map]);p.ClaimAutoStabledPets();pet.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);Stop(pet);long now=Core.TickCount+10000;
        pet.RecoverLife(now);Require(SkillHealth.Balance256(pet)==s.Life256,"Skeleton offline recovery replay");pet.RecoverLife(now+40);Require(SkillHealth.Balance256(pet)==s.Life256+s.Rate,"Saved skeleton source recovery");
        PersistentMinions.Suspend(p);p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Life256=s.Life256+s.Rate,Generation=s.Generation+1}));
    }
    private static void Input(NetState network,bool quick)
    {
        var p=Owner();var corpse=Corpse(p);PoisonDaggerChecks.Ready(p);int mana=NecromancerEffects.ManaBalance256(p);string label=quick?"skeleton-birth-quick":"skeleton-birth-right";
        network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var field=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);field.SetValue(network,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        bool began=false;long deadline=Core.TickCount+6000;
        void Clean(){foreach(var pet in NecromancerSkeletons.For(p,CombatAbility.RaiseSkeleton)){pet.Delete();}corpse.Delete();p.Delete();}
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Skeleton input timeout");var session=ClassCombat.For(p);
                if(session.PendingAbility==CombatAbility.RaiseSkeleton&&!began){Require(NecromancerEffects.ManaBalance256(p)==mana&&!corpse.SanctuaryConsumed,"Skeleton input paid early");began=true;Log.Information("{Label}: windup",label);}
                var pet=NecromancerSkeletons.For(p,CombatAbility.RaiseSkeleton).OfType<RaisedSkeleton>().SingleOrDefault();
                if(pet==null){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Inspect);return;}
                Require(began&&pet.SourceLife256==8064&&SkillHealth.Balance256(pet)==8064&&NecromancerEffects.ManaBalance256(p)==mana-9*256&&corpse.SanctuaryConsumed,"Actual skeleton input/resource/payment");Clean();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Clean();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.RaiseSkeleton,(int)DiabloClass.Necromancer,corpse.Serial.Value,corpse.X,corpse.Y,corpse.Z);Inspect();});
    }
}
