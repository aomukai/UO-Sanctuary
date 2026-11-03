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

public static class AmazonCastClockChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(AmazonCastClockChecks));
    private sealed record Saved(uint Owner,uint Pet,uint Bow,uint Gloves,DecoyStats Stats,int Life,int Mana,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"amazon-cast-clock-fixtures.json");
    [CallPriority(int.MaxValue)]public static void Initialize()
    {
        FreezeMana();
        if(File.Exists(Manifest))
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
            (World.FindMobile((Serial)saved.Pet) as AmazonDecoy)?.Pause(); // Hold the inspection cohort,not live casts.
        }
    }
    private static void FreezeMana()
    {
        var mana=Mobile.ManaRegenRateHandler;
        Mobile.ManaRegenRateHandler=p=>p.Name=="AmazonCastClockFixture"?TimeSpan.FromHours(1):mana?.Invoke(p)??Mobile.DefaultManaRate;
    }
    public static void Configure()
    {
        FreezeMana();
        Register("amazon-cast-clock-check",()=>Check(false));Register("amazon-cast-clock-reload-check",()=>Check(true));
        foreach(bool quick in new[]{false,true})
        {bool captured=quick;string name=quick?"amazon-cast-clock-quick":"amazon-cast-clock-right";Register(name+"-check",()=>FixtureNetwork.Connected(name+"-result",n=>Input(n,captured)));}
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Amazon SC weapon/FCR clock and actual learned casts",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static void Train(PlayerMobile p,CombatAbility ability)
    {
        var row=SkillMechanics.For(ability);if(ClassCreation.Profiles.GetRank(p.Serial,row.TalentId)>0){return;}
        Require(TalentCatalog.TryGet(DiabloClass.Amazon,row.TalentId,out var talent),"Missing Amazon talent");
        foreach(string prior in talent.Prerequisites){Train(p,SkillMechanics.All.Single(r=>r.TalentId==prior).Ability);}
        Require(ClassCreation.Profiles.TryTrain(p.Serial,row.TalentId,out _),"Cannot train Amazon cast fixture");
    }
    private static PlayerMobile Owner()
    {
        var p=new PlayerMobile{Name="AmazonCastClockFixture",Player=true,Body=0x190};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(p,99);
        while(p.RawStr<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Strength");}
        while(p.RawDex<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Dexterity");}
        Train(p,CombatAbility.InnerSight);Train(p,CombatAbility.Decoy);
        FixtureSpace.Corridor(p,6200,26,3600);Ready(p);return p;
    }
    private static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,50*256+17);}
    private static void HoldUnpaid(PlayerMobile p)
    {
        var session=ClassCombat.For(p);
        session.EffectAt+=FixtureSpace.RetentionMilliseconds;session.NextAction+=FixtureSpace.RetentionMilliseconds;
    }
    private static Item Equip(PlayerMobile p,string source)
    {
        var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==source).Id,99,new Random(173),true);
        p.AddToBackpack(item);Require(p.EquipItem(item),"Cannot equip "+source);ClassGear.Refresh(p);return item;
    }
    private static void Differential()
    {
        string[] classes=["1HS","1HT","2HS","2HT","BOW","HTH","STF","XBW"];
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"amazon-cast-clock-source-probe.json")));
        int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var timing=AmazonCastClock.Calculate(classes[row[0].GetInt32()],row[1].GetInt32());var events=row[3];
            Require(timing.EffectFrame==events[0][1].GetInt32()&&timing.Frames==events[1][1].GetInt32(),"Amazon full source SC scheduler differential");count++;
        }
        Require(count==2072&&AmazonCastClock.Calculate("HTH",0)==new ActionTiming(20,11)
            &&AmazonCastClock.Calculate("BOW",20)==new ActionTiming(18,9),"Missing source clock cases/boundaries");
        bool refused=false;try{AmazonCastClock.Calculate("unmapped",0);}catch(NotSupportedException){refused=true;}
        Require(refused&&!AmazonCombat.Implemented(CombatAbility.SlowMissiles),"Unknown weapon accepted or incomplete Slow Missiles enabled");
    }
    private static void Cast(PlayerMobile p,BaseCreature enemy,CombatAbility ability,string key,int fcr)
    {
        Ready(p);AmazonInnerSight.Clear(enemy);AmazonDecoys.Cancel(p);
        int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,ability).Mana256;long now=Core.TickCount;
        var point=new Point3D(p.X+3,p.Y,p.Z);Require(ClassCombat.Perform(p,ability,null,point,now),"Actual learned Amazon start");
        var s=ClassCombat.For(p);var expected=AmazonCastClock.Calculate(key,fcr);
        Require(s.Timing==expected&&s.EffectAt==now+expected.EffectMs&&s.NextAction==now+expected.DurationMs
            &&NecromancerEffects.ManaBalance256(p)==mana,"Actual weapon/FCR timing or unpaid windup");
        ClassCombat.ReleaseCast(p);
        Require(!ClassCombat.ResolveAction(p,s.EffectAt-1)&&ClassCombat.ResolveAction(p,s.EffectAt)
            &&!ClassCombat.ResolveAction(p,s.EffectAt),"Early/duplicate Amazon SC release");
        Require(NecromancerEffects.ManaBalance256(p)==mana-cost,"Amazon release did not pay exactly once");
        Require(ability==CombatAbility.InnerSight?AmazonInnerSight.State(enemy)!=null:AmazonDecoys.For(p).Length==1,"Actual learned effect missing");
        Require(!ClassCombat.Perform(p,ability,null,point,s.NextAction-1),"SC end event no longer limits reentry");
        AmazonDecoys.Cancel(p);
    }
    private static void Check(bool reload)
    {
        Differential();
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=World.FindMobile((Serial)saved.Owner) as PlayerMobile;
            var pet=World.FindMobile((Serial)saved.Pet) as AmazonDecoy;
            Require(p!=null&&pet!=null&&pet.Snapshot==saved.Stats&&SkillHealth.Balance256(pet)==saved.Life
                &&NecromancerEffects.ManaBalance256(p)==saved.Mana&&World.FindItem((Serial)saved.Bow)?.Parent==p
                &&World.FindItem((Serial)saved.Gloves)?.Parent==p,$"Original paid Decoy/gear/wounds/mana changed across world reload: owner={p?.Serial},pet={pet?.Serial},life={ (pet==null?0:SkillHealth.Balance256(pet)) }/{saved.Life},mana={ (p==null?0:NecromancerEffects.ManaBalance256(p)) }/{saved.Mana}");
            Require(AmazonCastClock.TryTiming(p,out var timing)&&timing==new ActionTiming(18,9)
                &&ClassCombat.For(p).PendingAbility==CombatAbility.None,"Loaded Amazon source clock or unpaid action replay");
            p.MoveToWorld(new(pet.X-3,pet.Y,pet.Z),pet.Map); // Explicitly activate the logged-out inspection owner.
            Ready(p);Require(ClassCombat.Perform(p,CombatAbility.InnerSight,null,p.Location,Core.TickCount),"Reload unpaid windup");
            HoldUnpaid(p);
            File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
            Log.Information("Amazon SC clock:generation{Generation},same paid Decoy/gear/wounds/mana;unpaid windup transient",saved.Generation+1);return;
        }
        var owner=Owner();var enemy=PoisonDaggerChecks.Target(owner);int casts=0;
        try
        {
            foreach(var (key,source) in new[]{("HTH",(string)null),("1HS","Rixots Keen"),("1HT","Titan's Revenge"),("2HS","The Grandfather"),
                ("2HT","Lance of Yaggai"),("STF","Serpent Lord"),("BOW","Hellclap"),("XBW","Leadcrow")})
            {
                Item weapon=source==null?null:Equip(owner,source);
                try
                {
                    Require(WeaponAnimationClasses.Equipped(owner)==key,"Actual equipped source class differs: "+key);
                    foreach(bool boosted in new[]{false,true})
                    {
                        Item gloves=boosted?Equip(owner,"Magefist"):null;
                        try
                        {
                            Require(CombatTiming.Fcr(owner)==(boosted?20:0),"Actual Magefist FCR");
                            foreach(var ability in new[]{CombatAbility.InnerSight,CombatAbility.Decoy})
                            {Cast(owner,enemy,ability,key,boosted?20:0);casts++;}
                        }
                        finally{gloves?.Delete();ClassGear.Refresh(owner);}
                    }
                }
                finally{weapon?.Delete();ClassGear.Refresh(owner);}
            }
            Require(casts==32,"Missing actual SC weapon/gear/skill casts");
            var bow=Equip(owner,"Hellclap");var magefist=Equip(owner,"Magefist");
            Cast(owner,enemy,CombatAbility.InnerSight,"BOW",20);Ready(owner);
            Require(ClassCombat.Perform(owner,CombatAbility.Decoy,null,new(owner.X+3,owner.Y,owner.Z),Core.TickCount),"Saved Decoy begin");
            Require(ClassCombat.ResolveAction(owner,ClassCombat.For(owner).EffectAt),"Saved Decoy release");
            var pet=AmazonDecoys.For(owner).Single();pet.Pause();pet.HoldUntil(TimedEffects.Now.Add(FixtureSpace.Retention));
            SkillHealth.SetBalance256(pet,SkillHealth.Maximum256(pet)*3/4);
            Ready(owner);int mana=NecromancerEffects.ManaBalance256(owner);
            Require(ClassCombat.Perform(owner,CombatAbility.InnerSight,null,owner.Location,Core.TickCount),"Saved unpaid windup");
            HoldUnpaid(owner);
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,pet.Serial.Value,bow.Serial.Value,magefist.Serial.Value,pet.Snapshot,SkillHealth.Balance256(pet),mana,0)));
            Log.Information("Amazon SC clock:2072source comparisons,32actual weapon/FCR/learned-skill casts;generation0 paid Decoy cohort retained");
        }
        finally{AmazonInnerSight.Clear(enemy);enemy.Delete();}
    }
    private static void Input(NetState network,bool quick)
    {
        var p=Owner();var enemy=PoisonDaggerChecks.Target(p);var ability=quick?CombatAbility.Decoy:CombatAbility.InnerSight;
        if(quick){Equip(p,"Hellclap");Equip(p,"Magefist");}
        Require(SkillBindings.Instance.Assign(p,false,ability)&&SkillBindings.Instance.AssignQuick(p,7,ability),"Amazon real bindings");
        var account=FixtureNetwork.Authenticate(network,p,"amazon-clock-");Ready(p);
        int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,ability).Mana256;var point=new Point3D(p.X+3,p.Y,p.Z);
        string label=quick?"amazon-cast-clock-quick":"amazon-cast-clock-right";bool began=false,finished=false;long limit=Core.TickCount+10000;
        void Cleanup(){AmazonInnerSight.Clear(enemy);enemy.Delete();AmazonDecoys.Cancel(p);p.Delete();account.Delete();}
        void Inspect()
        {
            if(finished){return;}
            try
            {
                Require(Core.TickCount<limit,"Amazon SC actual input timeout");var session=ClassCombat.For(p);
                if(session.PendingAbility==ability&&!began)
                {
                    Require(session.Timing==(quick?new ActionTiming(18,9):new ActionTiming(20,11))
                        &&NecromancerEffects.ManaBalance256(p)==mana,"Real input clock or unpaid windup");
                    began=true;Log.Information("{Label}: windup",label);
                }
                bool effect=quick?AmazonDecoys.For(p).Length==1:AmazonInnerSight.State(enemy)!=null;
                if(!effect){Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Inspect));return;}
                Require(began&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Real input missing windup or single payment");
                finished=true;ClassCombat.Stop(p);Cleanup();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){finished=true;Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(()=>
        {
            Log.Information("{Label}: ready ability={Ability} class={Class} target=0 x={X} y={Y} z={Z}",label,(int)ability,(int)DiabloClass.Amazon,point.X,point.Y,point.Z);
            Inspect();
        }));
    }
}
