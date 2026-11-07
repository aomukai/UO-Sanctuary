// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class NpcItemWrapperChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(NpcItemWrapperChecks));
    private sealed record Held(int Skill,uint Target,Point3D Point,int Life,long[] Ids);
    private sealed record Saved(uint Owner,uint Actor,Point3D Point,string Body,string[] Items,NpcEquipmentSeed Random,int Life,string Resources,
        Held[] Held,SorceressSpellFlight[] Spells,ChainFlight[] Chains,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-item-wrapper-fixtures.json");
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static SorceressSpellFlight[] Spells(AmazonValkyrieBody b)=>SorceressSpellMissiles.Snapshot().Flights.Where(f=>f.Npc?.Actor==b.Serial.Value).ToArray();
    private static ChainFlight[] Chains(AmazonValkyrieBody b)=>ChainLightning.Snapshot().Where(f=>f.OriginalNpc?.Source.Actor==b.Serial.Value).ToArray();
    private static string[] Items(AmazonValkyrieBody b)=>ValkyrieEquipment.Originals(b).Select(i=>i.SavedText).ToArray();
    private static void Cancel(AmazonValkyrieBody b){SorceressSpellMissiles.Cancel(b);ChainLightning.Cancel(b);}
    public static void Configure(){Register("npc-item-wrapper-check",false);Register("npc-item-wrapper-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Protected NPC item wrappers,target/path/flag restoration and actual saved spells",commandLine=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Matrix(AmazonValkyrieBody b,PlayerMobile p)
    {
        var ordinary=NpcItemChainChecks.Target(b,2);var victim=NpcItemChainChecks.Target(b,12);var target=NpcItemTarget.Capture(victim);var original=NpcItemTarget.Capture(ordinary);int cases=0;
        try
        {
            b.Combatant=ordinary;Require(b.Combatant==ordinary,"Cannot establish ordinary NPC combat target");
            foreach(int skill in new[]{36,38,44,48,53})foreach(int flag in new[]{0,1})foreach(bool hasTarget in new[]{false,true})
            foreach(bool initial in new[]{false,true})foreach(bool position in new[]{false,true})foreach(int mode in new[]{0,1,2,3})
            {
                int allocations=0,notices=0;ItemEffectServices<NpcItemTarget> frame=null;var oldPoint=(X:b.X+4,Y:b.Y+1);var castPoint=(X:victim.X,Y:victim.Y);
                var restoredPoint=position&&hasTarget?castPoint:oldPoint;bool threw=false;
                var rng=NpcEquipmentRandom.State(b);int sourceLife=SkillHealth.Balance256(b),mana=NecromancerEffects.ManaBalance256(p);string source=b.SavedText,items=Json(Items(b)),resources=Json(GeneratedNpcLiveResources.State(b));
                frame=NpcItemSpellCasts.OriginalFrame(b,TimedEffects.Now.AddMinutes(10),r=>
                {
                    allocations++;Require(NpcItemSpellCasts.KeepsCombatTarget(b),"Item wrapper lost protected AI scope");
                    bool expected=initial||!(flag==1&&skill is 44 or 48);Require(frame.ServerDo==expected,"Inner source do flag was not visible during allocation");
                    Require(position?frame.CurrentTarget==null:ReferenceEquals(frame.CurrentTarget,target),"Temporary item target was not supplied to execution");
                    b.Combatant=victim;Require(b.Combatant==ordinary,"Nested item effect changed ordinary combat target");
                    if(mode==2){throw new NotSupportedException("Fixture allocation refusal");}
                    if(mode==3&&allocations==1)
                    {
                        Require(!NpcItemSpellCasts.CastOriginal(b,original,53,3,0,TimedEffects.Now.AddMinutes(10),_=>new(false,0)),"Failed nested Chain allocation claimed success");
                        Require(NpcItemSpellCasts.KeepsCombatTarget(b),"Nested item scope released outer protection");
                        Require(frame.ServerDo==expected&&(position?frame.CurrentTarget==null:ReferenceEquals(frame.CurrentTarget,target)),"Nested item frame lost the outer temporary target/flag");
                    }
                    return new(mode!=1,SourceRandom.InitLow(8100+allocations));
                },notice=>
                {
                    notices++;Require((hasTarget?ReferenceEquals(frame.CurrentTarget,original):frame.CurrentTarget==null)&&frame.FirstPathPoint==restoredPoint&&frame.ServerDo==initial,"Notification preceded temporary frame restoration");
                    Require(position?notice.X==castPoint.X&&notice.Y==castPoint.Y:notice.UnitType==1&&notice.UnitId==target.Id,"Notification lost original target/position identity");
                });
                frame.SetPosition(oldPoint.X,oldPoint.Y);if(hasTarget){frame.SetTarget(original);}frame.ServerDo=initial;
                int result=0;
                using(var scope=NpcItemSpellCasts.PreserveCombatTarget(b))
                {
                    try{result=position?ItemEffectCasts.Position(frame,skill,3,castPoint.X,castPoint.Y,flag):ItemEffectCasts.Target(frame,skill,3,target,flag);}
                    catch(NotSupportedException e){Require(mode==2&&e.Message=="Fixture allocation refusal","Unexpected wrapper refusal");threw=true;}
                    Require(NpcItemSpellCasts.KeepsCombatTarget(b),"Item wrapper ended outer protection");
                }
                bool noTarget=position&&flag==1&&skill is 44 or 48;
                Require(threw==(mode==2&&!noTarget)&&result==(threw||noTarget||mode==1&&skill==53?0:1)&&notices==result,"Item wrapper result/failure/notification policy differs");
                Require((hasTarget?ReferenceEquals(frame.CurrentTarget,original):frame.CurrentTarget==null)&&frame.FirstPathPoint==restoredPoint&&frame.ServerDo==initial&&!NpcItemSpellCasts.KeepsCombatTarget(b)&&b.Combatant==ordinary,"Wrapper exception/success did not restore temporary/AI state");
                Require(NpcEquipmentRandom.State(b)==rng&&SkillHealth.Balance256(b)==sourceLife&&NecromancerEffects.ManaBalance256(p)==mana&&b.SavedText==source&&Json(Items(b))==items&&Json(GeneratedNpcLiveResources.State(b))==resources,"Item wrapper changed source resources/items/owner mana/RNG");
                if(threw||noTarget||mode==1){Require(Spells(b).Length==0&&Chains(b).Length==0,"Failed wrapper published projectiles");}
                Cancel(b);cases++;
            }
            Require(cases==320,"Wrapper matrix incomplete");
            int calls=0;bool refused=false;
            try{NpcItemSkillExecution.RunOriginal(b,target,b.Location,36,3,0,1,1,TimedEffects.Now,_=>{calls++;return new(true,1);},false,null);}
            catch(NotSupportedException){refused=true;}
            Require(refused&&calls==0,"Unprotected original item execution allocated");
        }
        finally{Cancel(b);b.Combatant=null;ordinary.Delete();victim.Delete();}
    }
    private static Held Hold(AmazonValkyrieBody b,int skill,DateTime at)
    {
        var target=NpcItemChainChecks.Target(b,12);var logical=NpcItemTarget.Capture(target);int ordinal=0;
        Require(NpcItemSpellCasts.CastOriginal(b,logical,skill,3,1,at,r=>
        {int i=ordinal++;return new(skill==36||skill==53||i%3!=0,SourceRandom.InitLow(8200+i));}),"Protected wrapper failed actual hold cast");
        target.Internalize();long[] ids;
        if(skill==53)
        {var root=Chains(b).Single();NpcItemChainChecks.Step(b,root.Cast,[],_=>throw new InvalidOperationException("Held root allocated child"));ids=[root.Cast];var moved=Chains(b).Single();return new(skill,target.Serial.Value,new((int)Math.Round(moved.X+moved.Vx),(int)Math.Round(moved.Y+moved.Vy),b.Z),SkillHealth.Balance256(target),ids);}
        var shots=Spells(b).Where(f=>f.Npc.Packet.Skill==skill).ToArray();foreach(var f in shots){SorceressSpellMissiles.Tick(f.NextUtc,true,f.Id);}ids=shots.Select(f=>f.Id).ToArray();
        var first=Spells(b).First(f=>ids.Contains(f.Id));Point3D point;
        if(skill==38){var c=first.Charged;point=new(c.OriginX+c.Path[3].X,c.OriginY+c.Path[3].Y,b.Z);}
        else{point=new(b.X+(skill==36?16:14),b.Y,b.Z);}
        return new(skill,target.Serial.Value,point,SkillHealth.Balance256(target),ids);
    }
    private static void Resume(AmazonValkyrieBody b,Saved s)
    {
        foreach(var h in s.Held)
        {
            var target=(NpcItemChainChecks.Recipient)World.FindMobile((Serial)h.Target);Require(SkillHealth.Balance256(target)==h.Life,"Wrapper target wounds changed on reload");var rng=NpcEquipmentRandom.State(b);ulong critical=rng.Seed;int expected=h.Life,hits=0;
            if(h.Skill==53)
            {
                var contacts=new List<NpcItemChainChecks.Contact>();var old=Chains(b).Single(f=>f.Cast==h.Ids[0]);NpcItemChainChecks.Step(b,old.Cast,contacts,_=>throw new InvalidOperationException("Loaded item replayed child allocation"));var aged=Chains(b).Single(f=>f.Cast==old.Cast);
                Require(aged.X==old.X&&aged.Y==old.Y&&aged.Frame==old.Frame+1&&contacts.Count==0,"Wrapper-created Chain replayed loaded interval");
                target.MoveToWorld(h.Point,b.Map);NpcItemChainChecks.Through(b,old.Cast,contacts,_=>throw new InvalidOperationException("Lone wrapper target found another enemy"));
                Require(contacts.Count==1,"Wrapper-created lone Chain did not hit");NpcItemChainChecks.Verify(b,rng,contacts,[target],[h.Life]);
            }
            else
            {
                var before=Spells(b).Where(f=>h.Ids.Contains(f.Id)).ToArray();foreach(var f in before){SorceressSpellMissiles.Tick(f.NextUtc,true,f.Id);}
                var aged=Spells(b).Where(f=>h.Ids.Contains(f.Id)).ToArray();Require(aged.Length==before.Length&&aged.Zip(before).All(p=>p.First.X==p.Second.X&&p.First.Y==p.Second.Y&&p.First.Frame==p.Second.Frame+1)&&NpcEquipmentRandom.State(b)==rng,"Wrapper-created spell replayed loaded interval");
                target.MoveToWorld(h.Point,b.Map);
                for(int i=0;i<80&&Spells(b).Any(f=>h.Ids.Contains(f.Id));i++)foreach(var f in Spells(b).Where(f=>h.Ids.Contains(f.Id)))
                {
                    int life=SkillHealth.Balance256(target);SorceressSpellMissiles.Tick(f.NextUtc,true,f.Id);
                    if(SkillHealth.Balance256(target)<life){int raw=SorceressSpellMissiles.Roll(f,out _);if(SourceRandom.Roll(ref critical)%100<5){raw*=2;}expected-=raw;hits++;Require(SkillHealth.Balance256(target)==expected,"Wrapper-created spell HP differs");}
                }
                Require(hits>0&&NpcEquipmentRandom.State(b)==rng with{Seed=critical},"Wrapper-created spell RNG/natural hit missing");
            }
            target.Delete();
        }
    }
    private static void Check(bool reload)
    {
        PlayerMobile p;AmazonValkyrieBody b;Point3D point;int generation=0;
        if(reload)
        {
            var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=s.Generation+1;Require(generation<=2,"Extra item wrapper reload");p=World.FindMobile((Serial)s.Owner)as PlayerMobile;b=World.FindMobile((Serial)s.Actor)as AmazonValkyrieBody;point=s.Point;
            Require(p is{Alive:true,Deleted:false}&&b is{Alive:true,Deleted:false}&&b.SavedText==s.Body&&Json(Items(b))==Json(s.Items)&&NpcEquipmentRandom.State(b)==s.Random&&SkillHealth.Balance256(b)==s.Life&&Json(GeneratedNpcLiveResources.State(b))==s.Resources&&Json(Spells(b))==Json(s.Spells)&&Json(Chains(b))==Json(s.Chains),"Wrapper-created spell/Chain/source state changed on reload");
            p.MoveToWorld(point,Map.Trammel);NpcItemChainChecks.Place(b,p);Resume(b,s);
        }
        else
        {
            p=new PlayerMobile{Name="NpcItemWrapperFixture",Body=0x190,Player=true};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(p,99);ClassSkillChecks.Train(p,"amazon.valkyrie",20);FixtureSpace.Corridor(p,6800,32,3290);point=p.Location;
            using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"valkyrie-damage-cases.json")));var row=source.RootElement.EnumerateArray().First(x=>x.GetProperty("Critical").GetInt32()==1&&x.GetProperty("Rank").GetInt32()==20);
            b=ValkyrieDamageChecks.Body(p,row);ValkyrieEquipmentResources.Activate(b,0,0,0,0,300);NpcItemChainChecks.Place(b,p);
        }
        Matrix(b,p);NpcItemChainChecks.Place(b,p);var at=TimedEffects.Now.AddMinutes(10);var held=new[]{36,38,44,48,53}.Select(skill=>Hold(b,skill,at)).ToArray();var spells=Spells(b);var chains=Chains(b);
        Require(spells.Length==88&&chains.Length==1&&held.Sum(h=>h.Ids.Length)==89,"Wrapper saved partial population differs");var rng=NpcEquipmentRandom.State(b);int life=SkillHealth.Balance256(b);string resources=Json(GeneratedNpcLiveResources.State(b));PersistentMinions.Suspend(p);p.Internalize();
        File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,b.Serial.Value,point,b.SavedText,Items(b),rng,life,resources,held,spells,chains,generation)));
        Log.Information("NPC original item wrappers:320target/position/flag/failure/nested restoration cases,ordinary combatant and live serverdo state,notification-after-restore,no owner payment/RNG;five actual spell bodies and89saved partial projectiles resume natural exactHP/RNG through two reloads;full original graph/proc/game stream remain separate");
    }
}
