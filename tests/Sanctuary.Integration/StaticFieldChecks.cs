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

public static class StaticFieldChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(StaticFieldChecks));
    private sealed record Saved(uint Owner,uint Target,int Map,Point3D Point,int Mana,int OriginalMana,int Life,long PendingAt,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"static-field-fixtures.json");
    // A timer armed before a rate change may still tick once. Reuse the
    // existing fixture eligibility hook so exact resource checks exclude it.
    private static void Rates()
    {
        var old=ClassGearHooks.CanRegenerateMana;
        ClassGearHooks.CanRegenerateMana=p=>p.Name!="StaticFieldFixture"&&old?.Invoke(p)!=false;
    }
    public static void Configure()
    {
        Rates();
        Register("static-field-check",Check);Register("static-field-reload-check",()=>FixtureNetwork.Connected("static-field-reload-result",Reload));
        foreach(bool q in new[]{false,true}){bool quick=q;string label=q?"static-field-quick":"static-field-right";Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,quick)));}
    }
    private static void Register(string command,Action action)=>ConsoleInputHandler.RegisterCommand(command,"Player Static Field source comparisons,input,resources and saved wounds",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",command);}catch(Exception e){Log.Error(e,"{Name}: failed",command);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {
        var p=FireBoltChecks.Owner(CombatAbility.FireBolt,6590);p.Name="StaticFieldFixture";
        Require(ClassCreation.Profiles.TryTrain(p.Serial,"sorceress.staticfield",out _),"Learn Static Field");
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.StaticField)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.StaticField),"Static bindings");return p;
    }
    private static GraveyardSkeleton Target(PlayerMobile p,int dx=1,int dy=0)
    {var t=FireBoltChecks.Target(p,dx);t.MoveToWorld(new(t.X,t.Y+dy,t.Z),t.Map);t.SetHits(1000);t.Hits=t.HitsMax;return t;}
    private static bool Release(PlayerMobile p)=>SorceressCombat.Release(p,CombatAbility.StaticField,Serial.Zero,p.Location);
    private static void Source()
    {
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"static-field-source-cases.json")));int count=0,delivered=0,negative=0,floorDifferences=0;
        foreach(var r in source.RootElement.EnumerateArray())
        {
            int V(int i)=>r[i].GetInt32();count++;Require(V(8)==(V(7)==3?1:0)&&V(9)==V(8),"Old Static wrapper validation");
            if(V(11)<0){continue;}delivered++;
            Require(StaticFields.Raw256(V(3),25,V(6))==V(11),"Complete source Static whole-HP preparation");
            int compensated=V(5)<0?SourcePercentage.Calculate(100,V(11),100-V(5)):V(11);
            Require(compensated==V(12)&&SourcePercentage.Calculate(compensated,Math.Max(0,100-V(5)),100)==V(15)
                &&V(13)==1&&V(14)==1&&r[18].EnumerateArray().Select(x=>x.GetInt32()).SequenceEqual(new[]{1,2,3,4}),"Complete old Static compensation/event order");
            if(V(5)<0){negative++;}
            if(V(2)==1&&V(1)>0&&StaticFields.Limit256(V(3),V(4),V(1),V(15))!=V(15)){floorDifferences++;}
        }
        Require(count==16200&&delivered==2565&&negative==1026&&floorDifferences>0,"Missing old Static contexts/discrepancy controls");
        for(int rank=1;rank<=255;rank++)
        {
            var values=new SkillValues(SkillMechanics.For(CombatAbility.StaticField),k=>k=="Static Field"?rank:255,_=>20,99);
            Require(values.Mana256==2304&&values.Eval("calc1")==25&&values.Eval("calc2")==0&&values.Eval("aurarangecalc")==rank+4&&values.DurationFrames==0,"Independent Static rank/radius/constant mana");
        }
    }
    private static void Check()
    {
        Source();Casts();Controls();Save();
        Log.Information("Player Static:16200old wrapper/callback/percentage cases,2565prepared deliveries/1026negative paths,255independent rank contexts;actual release/radius/mana/town/lifecycle/gear,characterized negative gap and saved canceled windup/wounds pass");
    }
    private static void Casts()
    {
        var p=Owner();var near=Target(p,2);var peer=Target(p,2,1);var outside=Target(p,3);var immune=Target(p,1,1);var friendly=Target(p,1,-1);
        var facets=SummonDifficulties.Facets();try
        {
            SummonDifficulties.SetFacets([new(p.Map.MapID,MonsterDifficulty.Normal)]);immune.SetResistance(ResistanceType.Energy,100);Require(friendly.SetControlMaster(p),"Friendly Static target");
            int mana=NecromancerEffects.ManaBalance256(p);Require(ClassCombat.Perform(p,CombatAbility.StaticField,null,new(p.X+9,p.Y,p.Z),Core.TickCount),"Static windup");var action=ClassCombat.For(p);
            Require(action.PendingPoint==p.Location&&NecromancerEffects.ManaBalance256(p)==mana&&SkillHealth.Balance256(near)==256000&&!ClassCombat.ResolveAction(p,action.EffectAt-1),"Static self origin/early payment/damage");
            Require(ClassCombat.ResolveAction(p,action.EffectAt)&&!ClassCombat.ResolveAction(p,action.EffectAt)&&NecromancerEffects.ManaBalance256(p)==mana-2304
                &&SkillHealth.Balance256(near)==192000&&SkillHealth.Balance256(peer)==192000&&SkillHealth.Balance256(outside)==256000
                &&SkillHealth.Balance256(immune)==256000&&SkillHealth.Balance256(friendly)==256000&&CreatureHitRecovery.Remaining(near)==0,"Static once release/radius/filter/immune/recovery");
            near.SetResistance(ResistanceType.Energy,50);SkillHealth.SetBalance256(near,128017);int hp=SkillHealth.Balance256(near);Require(Release(p)&&hp-SkillHealth.Balance256(near)==16000,"Static fractional HP/resistance");
            near.SetResistance(ResistanceType.Energy,-50);SkillHealth.SetBalance256(near,256000);Require(Release(p)&&SkillHealth.Balance256(near)==160000,"Characterized native negative-resistance path changed;modern parity remains unresolved");
            near.SetResistance(ResistanceType.Energy,0);
            var gear=PoisonDaggerChecks.Named(p,"Eschuta's temper");Require(ClassCreation.Profiles.TryTrain(p.Serial,"sorceress.lightningmastery",out _)&&CombatPotions.Apply(p,3),"Actual Static enhancement/potion controls");
            SkillHealth.SetBalance256(outside,256000);SkillHealth.SetBalance256(near,256000);Require(SkillMechanics.Rank(p,CombatAbility.StaticField)>1&&NamedElements.MasteryPercent(p,"ltng")>0&&Release(p)
                &&SkillHealth.Balance256(near)==192000&&SkillHealth.Balance256(outside)==192000,"Static mastery/potion enhanced damage or gear rank missed radius");
            p.Backpack.DropItem(gear);ClassGear.Refresh(p);TimedEffects.Clear(p);SkillHealth.SetBalance256(outside,256000);Require(Release(p)&&SkillHealth.Balance256(outside)==256000,"Static removal retained extended radius");
            foreach(var difficulty in new[]{MonsterDifficulty.Normal,MonsterDifficulty.Nightmare,MonsterDifficulty.Hell})
            {
                SummonDifficulties.SetFacets([new(p.Map.MapID,difficulty)]);SkillHealth.SetBalance256(near,256000);
                for(int i=0;i<60;i++){NecromancerEffects.SetMana256(p,p.ManaMax*256);Require(Release(p),"Repeated Static release");}
                Require(SkillHealth.Balance256(near)==(difficulty==MonsterDifficulty.Normal?768:difficulty==MonsterDifficulty.Nightmare?84480:128000)&&near.Alive,"Static designated-facet floor/nonlethal integer endpoint");
            }
        }finally{SummonDifficulties.SetFacets(facets);FireBoltChecks.Clean(p,near,peer,outside,immune,friendly);}
        p=Owner();var t=Target(p,2);Item wall=null;try
        {
            wall=new Static(0x80);wall.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);Require(Release(p)&&SkillHealth.Balance256(t)==256000,"Static native LOS crossed a solid wall");
        }finally{wall?.Delete();FireBoltChecks.Clean(p,t);}
    }
    private static void Controls()
    {
        foreach(string mode in new[]{"cancel","mana","internal","dead","town"})
        {
            var p=Owner();var t=Target(p);try
            {
                int hp=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p);
                if(mode=="cancel")
                {
                    Require(ClassCombat.Perform(p,CombatAbility.StaticField,null,p.Location,Core.TickCount),"Static cancellation windup");long due=ClassCombat.For(p).EffectAt;ClassCombat.Stop(p);
                    Require(!ClassCombat.ResolveAction(p,due),"Canceled Static released");
                }
                else
                {
                    if(mode=="mana"){NecromancerEffects.SetMana256(p,2303);mana=2303;}
                    if(mode=="internal"){p.Internalize();}
                    if(mode=="dead"){p.Kill();mana=NecromancerEffects.ManaBalance256(p);}
                    if(mode=="town"){p.MoveToWorld(new(1497,1628,10),Map.Trammel);}
                    Require(!Release(p),"Invalid Static cast accepted: "+mode);
                }
                Require(NecromancerEffects.ManaBalance256(p)==mana&&SkillHealth.Balance256(t)==hp&&!SorceressSpellMissiles.Snapshot().Flights.Any(x=>x.Owner==p.Serial.Value),"Invalid Static paid/damaged/emitted: "+mode);
            }finally{FireBoltChecks.Clean(p,t);}
        }
    }
    private static void Save()
    {
        var p=Owner();var t=Target(p);Require(Release(p),"Saved Static release");int mana=NecromancerEffects.ManaBalance256(p);
        Require(ClassCombat.Perform(p,CombatAbility.StaticField,null,p.Location,Core.TickCount),"Saved unpaid Static windup");long pending=ClassCombat.For(p).EffectAt;
        var saved=new Saved(p.Serial.Value,t.Serial.Value,p.Map.MapID,p.Location,mana,mana,SkillHealth.Balance256(t),pending,0);p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var held=World.FindMobile((Serial)s.Target);
        Log.Information("Static reload observation:pending={Pending} life={Life}/{ExpectedLife} mana={Mana}/{ExpectedMana} right={Right} quick={Quick}",ClassCombat.For(p).PendingAbility,SkillHealth.Balance256(held),s.Life,NecromancerEffects.ManaBalance256(p),s.Mana,SkillBindings.Instance.Get(p).Right,SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Sorceress,7));
        Require(s.Generation<2&&ClassCombat.For(p).PendingAbility==CombatAbility.None&&SkillHealth.Balance256(held)==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana
            &&SkillBindings.Instance.Get(p).Right==CombatAbility.StaticField&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Sorceress,7)==CombatAbility.StaticField,"Saved Static replay/wounds/mana/bindings");
        var account=FixtureNetwork.Authenticate(network,p,"static-field-reload-");p.MoveToWorld(s.Point,Map.Maps[s.Map]);var point=held.Location;held.MoveToWorld(new(p.X+6,p.Y+6,p.Z),p.Map);var target=Target(p);
        void Resume()
        {
            try
            {
                Require(Release(p)&&SkillHealth.Balance256(target)==192000&&NecromancerEffects.ManaBalance256(p)==s.Mana-2304&&SkillHealth.Balance256(held)==s.Life,"Postload fresh Static release or held wound preservation");
                target.Delete();held.MoveToWorld(point,p.Map);p.Internalize();network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();
                File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1,Mana=s.Mana-2304}));Log.Information("static-field-reload-result: passed");
            }catch(Exception e){Log.Error(e,"static-field-reload-result: failed");}
        }
        FixtureClock.NextLoop("static-field-reload-result",Resume);
    }
    private static void Input(NetState network,bool quick)
    {
        var p=Owner();var t=Target(p);int mana=NecromancerEffects.ManaBalance256(p);var account=FixtureNetwork.Authenticate(network,p,"static-field-input-");string label=quick?"static-field-quick":"static-field-right";bool began=false;long deadline=Core.TickCount+6000;
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline,"Static real input timeout");var action=ClassCombat.For(p);
                if(action.PendingAbility==CombatAbility.StaticField&&!began){Require(NecromancerEffects.ManaBalance256(p)==mana&&SkillHealth.Balance256(t)==256000,"Static input paid/damaged before release");began=true;Log.Information("{Label}: windup",label);}
                if(SkillHealth.Balance256(t)==256000){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Observe);return;}
                Require(began&&NecromancerEffects.ManaBalance256(p)==mana-2304&&SkillHealth.Balance256(t)==192000&&!SorceressSpellMissiles.Snapshot().Flights.Any(x=>x.Owner==p.Serial.Value),"Real Static payment/damage/instant field");
                FireBoltChecks.Clean(p,t);account.Delete();Log.Information("{Label}-result: passed",label);
            }catch(Exception e){FireBoltChecks.Clean(p,t);account.Delete();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target=0 x={X} y={Y} z={Z}",label,(int)CombatAbility.StaticField,(int)DiabloClass.Sorceress,p.X+2,p.Y,p.Z);Observe();});
    }
}
