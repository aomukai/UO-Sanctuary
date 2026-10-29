// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class FindPotionChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(FindPotionChecks));
    private sealed record Saved(uint Owner,uint Used,uint Pending,uint Potion,uint Loot,int Mana256,Point3D Origin,int Map);
    private static int _starts;
    private static string Manifest=>Path.Join(Core.BaseDirectory,"find-potion-fixtures.json");
    public static void Configure()
    {
        Register("find-potion-check",Check);Register("find-potion-reload-check",Reload);Register("find-potion-live-check",Live);Register("find-potion-snapshot-check",Snapshot);
        Rates();
    }
    [CallPriority(int.MaxValue)] public static void Initialize()=>Rates();
    private static void Rates()
    {
        var prior=Mobile.ManaRegenRateHandler;
        Mobile.ManaRegenRateHandler=p=>p.Name=="FindPotionFixture"?TimeSpan.FromHours(1):prior?.Invoke(p)??Mobile.DefaultManaRate;
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Find Potion source/world/casting/persistence",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Equal(int[] values,JsonElement row)=>Require(JsonSerializer.Serialize(values)==JsonSerializer.Serialize(row),"Legacy Find Potion row: "+JsonSerializer.Serialize(values));
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"find-potion-native-source-probe.json")));
        int count=0;
        foreach(var row in doc.RootElement.GetProperty("eligibilityCases").EnumerateArray())
        {
            int[] v=row.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
            Require(FindPotionPolicy.Eligible(v[0],v[1],(uint)v[2],v[3]!=0,v[4]!=0,v[5]!=0)==(v[6]!=0),"Soft/dead/selectable/state eligibility");count++;
        }
        foreach(var row in doc.RootElement.GetProperty("chanceCases").EnumerateArray())
        {
            int rank=row[0].GetInt32(),chance=row[2].GetInt32(),calls=0,drops=0;uint draw=row[1].GetUInt32();bool used=false;
            int Run()=>FindPotionPolicy.Execute(true,!used,()=>used=true,()=>chance,()=>++calls==1?draw:0,true,0,0,30,10,(_,_)=>drops++);
            int result=Run(),first=calls,again=Run();
            // The unsigned random word can exceed Int32; compare that field separately.
            Require(row[1].GetUInt32()==draw,"Raw unsigned RNG");
            Equal([rank,unchecked((int)draw),chance,result,used?1:0,drops,first,again,calls],
                JsonSerializer.SerializeToElement(row.EnumerateArray().Select((x,i)=>i==1?unchecked((int)x.GetUInt32()):x.GetInt32()).ToArray()));count++;
        }
        foreach(var row in doc.RootElement.GetProperty("outputCases").EnumerateArray())
        {
            int difficulty=row[0].GetInt32(),act=row[1].GetInt32(),rank=row[2].GetInt32(),kind=row[3].GetInt32(),calls=0,quality=0,drops=0;uint code=0;
            int result=FindPotionPolicy.Execute(true,true,()=>{},()=>100,()=>++calls==1?0u:(uint)kind,true,act,difficulty,30,10,(c,q)=>{code=c;quality=q;drops++;});
            Require(calls==2&&code==row[4].GetUInt32(),"Potion code/RNG");Equal([difficulty,act,rank,kind,(int)code,quality,drops,result,1],row);count++;
        }
        foreach(var row in doc.RootElement.GetProperty("contextCases").EnumerateArray())
        {
            int room=row[0].GetInt32(),act=row[1].GetInt32(),difficulty=row[2].GetInt32(),calls=0,quality=0;bool used=false;uint code=0;
            int result=FindPotionPolicy.Execute(true,true,()=>used=true,()=>100,()=>{calls++;return 0;},room!=0,act,difficulty,30,10,(c,q)=>{code=c;quality=q;});
            Equal([room,act,difficulty,result,used?1:0,(int)code,quality,calls],row);count++;
        }
        foreach(var row in doc.RootElement.GetProperty("doGuardCases").EnumerateArray())
        {
            int valid=row[0].GetInt32(),present=row[1].GetInt32(),used=row[2].GetInt32(),calls=0,drops=0;
            int result=FindPotionPolicy.Execute(valid!=0,present!=0&&used==0,()=>used=1,()=>100,()=>{calls++;return 0;},true,0,0,30,10,(_,_)=>drops++);
            Equal([valid,present,row[2].GetInt32(),result,used,calls,drops],row);count++;
        }
        Require(count==5004,"Missing source cases");
        foreach(string code in new[]{"hp2","hp3","hp4","hp5","mp2","mp3","mp4","mp5","rvs","rvl"})
        {
            var p=BarbarianFindPotion.CreatePotion(FindPotionPolicy.Code(code));
            Require(p.Origin==ItemOrigin.Drop&&p.Tier==(code=="rvs"?1:code=="rvl"?2:code[2]-'0')&&p.Kind==(code[0]=='h'?RestorationKind.Healing:code[0]=='m'?RestorationKind.Mana:RestorationKind.Rejuvenation),"Existing potion factory/code/origin");p.Delete();
        }
        bool unsupported=false;try{BarbarianFindPotion.CreatePotion(0);}catch(NotSupportedException){unsupported=true;}
        Require(unsupported,"Generic fallback silently invented");
        Log.Information("Find Potion policy:5004complete legacy-source cases;10existing potion codes;generic fallback explicit;modern RNG/curve parity remains open");
    }
    private static void Ready(PlayerMobile p){ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;}
    private static PlayerMobile Fresh(int level=99)
    {
        var p=new PlayerMobile{Body=0x190,Player=true,Name="FindPotionFixture"};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Barbarian);ClassCreation.Profiles.AdvanceLevel(p,level);
        Require(ClassCreation.Profiles.TryTrain(p.Serial,"barbarian.findpotion",out var why),why);
        for(int x=2700;x<3050;x+=50)for(int y=1000;y<1350;y+=50)
        {
            Point3D point;try{FixtureSpace.Place(p,new(x,y,0),Map.Felucca);point=ClassSkillChecks.Corridor(p,x,y);}catch(InvalidOperationException){continue;}
            bool occupied=false;foreach(var mobile in p.Map.GetMobilesInRange<Mobile>(point,20)){if(mobile!=p){occupied=true;break;}}
            if(occupied||!Server.Spells.SpellHelper.CheckTown(point,p)){continue;}
            p.MoveToWorld(point,p.Map);p.Mana=p.ManaMax;return p;
        }
        throw new InvalidOperationException("No Find Potion corridor");
    }
    private static Corpse Body(PlayerMobile owner,int level=1,bool soft=true)
    {
        // Test-only scoped catalogue override:the actual creature level is read
        // by production capture at death. Restore the catalogue immediately.
        var field=typeof(CreatureProfiles).GetField("_variants",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        Require(field!=null,"Creature profile fixture access");
        var profiles=(System.Collections.Generic.Dictionary<(string,MonsterDifficulty),CreatureVariant>)field.GetValue(null);
        var key=((soft?typeof(GraveyardZombie):typeof(GraveyardSkeleton)).FullName,MonsterDifficulty.Normal);var before=profiles[key];
        profiles[key]=before with{Enabled=true,ApplyStats=true,Level=level};
        BaseCreature victim=null;Corpse body=null;var prior=ClassLootHooks.CreatureDied;
        try
        {
            victim=soft?new GraveyardZombie():new GraveyardSkeleton();victim.Frozen=true;victim.CantWalk=true;victim.NoKillAwards=true;
            victim.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);victim.AIObject.AITimer.Stop();Require(CreatureProfiles.Level(victim)==level,"Configured real monster level");
            ClassLootHooks.CreatureDied=(actor,container)=>{if(actor!=victim){prior?.Invoke(actor,container);return;}CorpseSkillRules.Capture(actor,container);body=container as Corpse;body?.DropItem(new Bag{Name="FindPotionOriginalLoot"});};
            owner.DoHarmful(victim,true);victim.Damage(victim.Hits+1,owner);CombatDamage.Finish(victim,owner);
        }
        finally{ClassLootHooks.CreatureDied=prior;profiles[key]=before;}
        Require(body!=null&&CorpseSkillRules.Soft(body)==soft&&CorpseSkillRules.Profiles.Find(body).Treasure==null&&CorpseSkillRules.Profiles.Find(body).MonsterLevel==level&&CorpseSkillRules.TryMonsterLevel(body,out int captured)&&captured==level,"Captured death level/source/soft flag independent of current profile");
        body.BeginDecay(TimeSpan.FromHours(1));return body;
    }
    private static void Start(PlayerMobile p,Corpse corpse)
    {
        Ready(p);int mana=NecromancerEffects.ManaBalance256(p);_starts++;
        Require(SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Barbarian,0)==CombatAbility.FindPotion,"Find Potion quick binding changed");
        Require(SkillBindingNetwork.Use(p,DiabloClass.Barbarian,0,CombatAbility.FindPotion,corpse.Serial,corpse.Location),$"Quick corpse cast #{_starts}:mana={mana},valid={BarbarianFindPotion.ValidTarget(p,corpse.Serial)}");
        Require(ClassCombat.For(p).FindPotionTarget==corpse.Serial&&NecromancerEffects.ManaBalance256(p)==mana-512&&!CorpseSkillRules.NoSelect(corpse),"Start payment/token/reservation timing");
        Require(!ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt-1),"Early effect");
    }
    private static bool Finish(PlayerMobile p)
    {
        var action=ClassCombat.For(p);int mana=NecromancerEffects.ManaBalance256(p);bool result=ClassCombat.ResolveAction(p,action.EffectAt);
        Require(!ClassCombat.ResolveAction(p,action.EffectAt)&&action.FindPotionTarget==Serial.Zero&&NecromancerEffects.ManaBalance256(p)==mana,"Duplicate event/release payment");return result;
    }
    private static void Bands()
    {
        foreach(int level in new[]{1,19,20,39,40,59,60,99,100,int.MaxValue})
        {
            int expected=level<20?2:level<40?3:level<60?4:5;var band=PotionLevelBands.For(level);
            Require(band.HealingTier==expected&&band.ManaTier==expected&&band.RejuvenationTier==(level<40?1:2),"Approved monster-level band");
            foreach(uint draw in new[]{0u,29u,30u,39u,40u,99u,100u,uint.MaxValue})
            {
                uint type=draw%100;string code=type<30?"mp"+expected:type<40?level<40?"rvs":"rvl":"hp"+expected;
                Require(PotionLevelBands.Code(band,draw,30,10)==FindPotionPolicy.Code(code),"Approved type odds/monster-level code");
            }
        }
        var entries=PotionLevelBands.Entries();
        foreach(var invalid in new[]{Array.Empty<PotionLevelBand>(),new[]{new PotionLevelBand(0,2,2,1)},new[]{new PotionLevelBand(2,2,2,1)},new[]{new PotionLevelBand(1,1,2,1)},new[]{new PotionLevelBand(1,2,6,1)},new[]{new PotionLevelBand(1,2,2,3)},new[]{new PotionLevelBand(1,2,2,1),new PotionLevelBand(1,3,3,1)}})
        {bool rejected=false;try{PotionLevelBands.Set(invalid);}catch(InvalidDataException){rejected=true;}Require(rejected&&PotionLevelBands.Entries().SequenceEqual(entries),"Invalid bands replaced valid policy");}
    }
    private static void Check()
    {
        Source();Bands();Require(ClassAbilities.CorpseTarget(CombatAbility.FindPotion)&&ClassCombat.CorpseCast(CombatAbility.FindPotion),"Generated corpse routing lost Find Potion");
        var p=Fresh();Require(!SkillBindings.Instance.Assign(p,true,CombatAbility.FindPotion)&&SkillBindings.Instance.Assign(p,false,CombatAbility.FindPotion)&&SkillBindings.Instance.AssignQuick(p,0,CombatAbility.FindPotion),"Right/quick bindings");
        var facets=SummonDifficulties.Facets();var roll=BarbarianFindPotion.Roll;
        try
        {
            var corpse=Body(p);int mana=NecromancerEffects.ManaBalance256(p);Require(BarbarianFindPotion.ValidTarget(p,corpse.Serial),"Levelled corpse requires an obsolete region tag");
            var hard=Body(p,1,false);Require(!BarbarianFindPotion.ValidTarget(p,hard.Serial),"Hard skeleton corpse accepted");hard.Delete();
            Require(!BarbarianFindPotion.Release(p,corpse.Serial),"Free release");NecromancerEffects.SetMana256(p,511);Ready(p);
            Require(!ClassCombat.BeginCorpse(p,CombatAbility.FindPotion,corpse.Serial,corpse.Location,Core.TickCount),"Fractionally insufficient mana");p.Mana=p.ManaMax;
            int calls=0;BarbarianFindPotion.Roll=()=>{calls++;return 99;};Start(p,corpse);int art=corpse.ItemID;
            Require(Finish(p)&&calls==1&&CorpseSkillRules.NoSelect(corpse)&&corpse.Items.Count==1&&corpse.ItemID==art&&!corpse.SanctuaryConsumed&&!CorpseRecoveryHooks.CanAnimate(corpse),"Failed attempt/loot/presentation/shared exclusion");
            Ready(p);mana=NecromancerEffects.ManaBalance256(p);Require(!ClassCombat.BeginCorpse(p,CombatAbility.FindPotion,corpse.Serial,corpse.Location,Core.TickCount)&&NecromancerEffects.ManaBalance256(p)==mana&&calls==1,"Repeated attempt spent mana/RNG");corpse.Delete();
            foreach(var difficulty in Enum.GetValues<MonsterDifficulty>())foreach(int level in new[]{1,19,20,39,40,59,60,99})foreach(uint type in new[]{0u,29u,30u,39u,40u,99u})
            {
                SummonDifficulties.SetFacets([new(p.Map.MapID,difficulty)]);var body=Body(p,level);calls=0;BarbarianFindPotion.Roll=()=>++calls==1?0:type;
                p.Mana=p.ManaMax;Start(p,body);NecromancerEffects.SetMana256(p,0);Require(Finish(p)&&calls==2&&body.Items.Count==2&&NecromancerEffects.ManaBalance256(p)==0,"Monster-level cast/type/zero-mana release");
                var potion=body.Items.OfType<RestorationPotion>().Single();int expected=type<30||type>=40?level<20?2:level<40?3:level<60?4:5:level<40?1:2;
                Require(potion.Tier==expected&&potion.Kind==(type<30?RestorationKind.Mana:type<40?RestorationKind.Rejuvenation:RestorationKind.Healing),"Tier changed with facet/owner instead of recorded monster level");body.Delete();
            }
            SummonDifficulties.SetFacets(facets);BarbarianFindPotion.Roll=()=>0;p.Mana=p.ManaMax;
            var unknown=Body(p,60);var state=CorpseSkillRules.Profiles.Find(unknown);
            CorpseSkillRules.Profiles.Store(state with{MonsterLevel=null,Treasure=null,BaseLife=null});Ready(p);mana=NecromancerEffects.ManaBalance256(p);
            Require(!ClassCombat.BeginCorpse(p,CombatAbility.FindPotion,unknown.Serial,unknown.Location,Core.TickCount)&&!CorpseSkillRules.NoSelect(unknown)&&NecromancerEffects.ManaBalance256(p)==mana,"Unknown legacy level invented");
            Require(state.BaseLife?.Level==60,"Legacy level fixture");
            var node=System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(state));node.AsObject().Remove("MonsterLevel");var legacy=JsonSerializer.Deserialize<CorpseSkillRecord>(node.ToJsonString());
            Require(legacy.MonsterLevel==null,"Old JSON level default");CorpseSkillRules.Profiles.Store(legacy);
            Require(CorpseSkillRules.TryMonsterLevel(unknown,out int oldLevel)&&oldLevel==60,"Exact old base-life snapshot level unavailable");Start(p,unknown);Require(Finish(p)&&unknown.Items.OfType<RestorationPotion>().Single().Tier==5,"Old snapshot level yield");unknown.Delete();
            var conflict=Body(p,40);state=CorpseSkillRules.Profiles.Find(conflict);CorpseSkillRules.Profiles.Store(state with{MonsterLevel=null,Treasure=new("zombie1","Act 1 H2H A",20,MonsterDifficulty.Normal)});
            Require(!CorpseSkillRules.TryMonsterLevel(conflict,out _),"Conflicting legacy levels silently selected");conflict.Delete();
            var interrupted=Body(p,40);p.Mana=p.ManaMax;Start(p,interrupted);mana=NecromancerEffects.ManaBalance256(p);ClassCombat.Stop(p);
            Require(!BarbarianFindPotion.Release(p,interrupted.Serial)&&!CorpseSkillRules.NoSelect(interrupted)&&NecromancerEffects.ManaBalance256(p)==mana,"Cancelled cast refund/attempt");interrupted.Delete();
            var invalidated=Body(p,40);Start(p,invalidated);state=CorpseSkillRules.Profiles.Find(invalidated);CorpseSkillRules.Profiles.Store(state with{MonsterLevel=null,BaseLife=null,Treasure=null});
            Require(!Finish(p)&&!CorpseSkillRules.NoSelect(invalidated),"Missing release level invented");invalidated.Delete();
            var low=Fresh(2);Require(SkillBindings.Instance.AssignQuick(low,0,CombatAbility.FindPotion),"Low-level quick slot");var highMonster=Body(low,60);low.Mana=low.ManaMax;Start(low,highMonster);Require(Finish(low)&&highMonster.Items.OfType<RestorationPotion>().Single().Tier==5,"Character level replaced corpse level");highMonster.Delete();low.Delete();
            var used=Body(p,60);p.Mana=p.ManaMax;Start(p,used);Require(Finish(p),"Saved success");var result=used.Items.OfType<RestorationPotion>().Single();var loot=used.Items.OfType<Bag>().Single();
            var pending=Body(p,40);p.Mana=p.ManaMax;Start(p,pending);ClassCombat.For(p).EffectAt=ClassCombat.For(p).NextAction=Core.TickCount+FixtureSpace.RetentionMilliseconds;
            NecromancerEffects.SetMana256(p,NecromancerEffects.ManaBalance256(p)-17);
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,used.Serial.Value,pending.Serial.Value,result.Serial.Value,loot.Serial.Value,NecromancerEffects.ManaBalance256(p),p.Location,p.Map.MapID)));
        }
        finally{SummonDifficulties.SetFacets(facets);BarbarianFindPotion.Roll=roll;}
        Log.Information("Find Potion monster tiers:144real boundary/facet/type casts;recorded death levels,legacy fallback/conflict,low-level owner,no area tags,payment,cancel,loot and pending-save fixtures pass");
    }
    private static void Live()
    {
        FixtureNetwork.Connected("find-potion-live-result",network=>
        {
            var p=Fresh();Require(SkillBindings.Instance.AssignQuick(p,0,CombatAbility.FindPotion),"Natural quick slot");var roll=BarbarianFindPotion.Roll;var body=Body(p,40);
            network.Seeded=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
            var protocol=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);Require(protocol!=null,"Natural network bootstrap");protocol.SetValue(network,Enum.Parse(protocol.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
            Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>Log.Information("find-potion-live: ready"));
            int calls=0;BarbarianFindPotion.Roll=()=>{calls++;return 0;};p.Mana=p.ManaMax;Start(p,body);NecromancerEffects.SetMana256(p,0);var action=ClassCombat.For(p);
            Timer.StartTimer(TimeSpan.FromMilliseconds(Math.Max(1,action.EffectAt-Core.TickCount+160)),()=>
            {
                try{Require(action.PendingAbility==CombatAbility.None&&action.FindPotionTarget==Serial.Zero&&calls==2&&CorpseSkillRules.NoSelect(body)&&body.Items.OfType<RestorationPotion>().Single().Tier==4&&body.Items.OfType<Bag>().Count()==1&&NecromancerEffects.ManaBalance256(p)==0,"Natural40ms release/payment/level-based output");Log.Information("find-potion-live-result: passed");}
                catch(Exception e){Log.Error(e,"find-potion-live-result: failed");}
                finally{BarbarianFindPotion.Roll=roll;body.Delete();p.Delete();}
            });
        });
    }
    private static void Snapshot()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        Require(NecromancerEffects.ManaBalance256(p)==saved.Mana256,$"Parked mana changed before save:{NecromancerEffects.ManaBalance256(p)}/{saved.Mana256}");
        Require(CorpseSkillRules.NoSelect((Corpse)World.FindItem((Serial)saved.Used))&&!CorpseSkillRules.NoSelect((Corpse)World.FindItem((Serial)saved.Pending)),"Parked corpse state before save");
        Require(CorpseSkillRules.Profiles.Find((Corpse)World.FindItem((Serial)saved.Used)).MonsterLevel==60&&CorpseSkillRules.Profiles.Find((Corpse)World.FindItem((Serial)saved.Pending)).MonsterLevel==40,"Recorded levels before save");
    }
    private static void Reload()
    {
        Source();Bands();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        var used=(Corpse)World.FindItem((Serial)saved.Used);var pending=(Corpse)World.FindItem((Serial)saved.Pending);
        Require(CorpseSkillRules.NoSelect(used)&&!CorpseSkillRules.NoSelect(pending)&&World.FindItem((Serial)saved.Potion) is RestorationPotion{Origin:ItemOrigin.Drop,Tier:5} potion&&potion.Parent==used&&World.FindItem((Serial)saved.Loot)?.Parent==used,"Saved attempt/source/potion/original loot");
        Require(CorpseSkillRules.TryMonsterLevel(used,out int level)&&level==60&&CorpseSkillRules.TryMonsterLevel(pending,out int pendingLevel)&&pendingLevel==40,"Monster levels changed across reload");
        Require(ClassCombat.For(p).FindPotionTarget==Serial.Zero&&ClassCombat.For(p).PendingAbility==CombatAbility.None&&NecromancerEffects.ManaBalance256(p)==saved.Mana256&&!BarbarianFindPotion.Release(p,pending.Serial),"Transient paid cast replay/fractional resource loss");
        Log.Information("Find Potion monster-tier reload:recorded levels,corpse flags,potion identity,loot,fractional mana and no transient cast replay pass");
    }
}
