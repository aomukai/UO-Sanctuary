// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ValkyrieGroundCombatChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ValkyrieGroundCombatChecks));
    private sealed record Held(uint Body,string Text,string[] Items,string Resources,NpcEquipmentSeed Random,int Life);
    private sealed record Saved(uint Owner,Held[] Bodies,uint Target,int TargetLife,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"valkyrie-ground-combat-fixtures.json");
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("valkyrie-ground-combat-check",false);Register("valkyrie-ground-combat-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Original Valkyrie automatic A1 contact and saved windup",_=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static Held Capture(AmazonValkyrieBody b)=>new(b.Serial.Value,b.SavedText,ValkyrieEquipment.Originals(b).Select(i=>i.SavedText).ToArray(),Json(GeneratedNpcLiveResources.State(b)),NpcEquipmentRandom.Require(b),SkillHealth.Balance256(b));
    private static IronGolemWorldState State(AmazonValkyrieBody b)=>b.Ground.State.Controller;
    private static void Through(AmazonValkyrieBody b,int frame,bool actions=true)
    {
        var s=State(b);Require(frame>s.Frame,"Reverse Valkyrie contact clock");IronGolemWorld.Advance(b,s.NextUtc.AddMilliseconds(40L*(frame-s.Frame-1)),actions);
        Require(b.Alive&&!b.Deleted&&State(b).Fault==null,"Original Valkyrie controller fault");
    }
    private static ulong AiSeed(out ulong after)
    {
        for(int n=1;n<10000;n++){ulong seed=SourceRandom.InitLow(n),copy=seed;SourceRandom.Roll(ref copy);if(SourceRandom.Roll(ref copy)%100<80){after=copy;return seed;}}
        throw new InvalidOperationException("No automatic A1 seed");
    }
    private static ulong HitSeed(AmazonValkyrieBody b,BaseCreature t,bool hit)
    {
        double chance=ClassStats.HitChance(ClassStats.AttackRatingAgainst(b,t,0),ClassStats.Defense(t),ClassStats.Level(b),ClassStats.Level(t));
        for(int n=1;n<10000;n++){ulong seed=SourceRandom.InitLow(n),copy=seed;if((SourceRandom.Roll(ref copy)%100/100.0<chance)==hit){return seed;}}
        throw new InvalidOperationException("No hit/miss seed");
    }
    private static ValkyrieContactChecks.Recipient Target(AmazonValkyrieBody b)
    {
        var t=new ValkyrieContactChecks.Recipient{Frozen=false};t.SetHits(65000);t.MoveToWorld(b.Location,b.Map);t.AIObject.AITimer.Stop();
        foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}TimedEffects.PreventHealing(t,TimedEffects.Now);return t;
    }
    private static void Start(AmazonValkyrieBody b,PlayerMobile p,BaseCreature target)
    {
        b.Frozen=false;b.Ground.Store(IronGolemWorld.Park(State(b)));target.MoveToWorld(b.Location,b.Map);
        ulong seed=AiSeed(out ulong expected);NpcEquipmentRandom.Set(b,seed);int life=SkillHealth.Balance256(target);
        var clock=ValkyrieGroundCombat.Attack(b);int frame=State(b).Frame+1;Through(b,frame);var s=State(b);
        Require(s.Mode==4&&s.Target==target.Serial.Value&&s.ActionAt==frame+clock.Action&&s.EndAt==frame+clock.End&&s.AttackSpeed==clock.Speed,"Automatic AI67 did not bind the transformed A1 event plan");
        Require(NpcEquipmentRandom.Require(b).Seed==expected&&SkillHealth.Balance256(target)==life,"Windup allocated hit/damage RNG or committed HP");
        if(s.ActionAt>s.Frame+1){Through(b,s.ActionAt-1);}
        Require(NpcEquipmentRandom.Require(b).Seed==expected&&SkillHealth.Balance256(target)==life,"Pre-action frame allocated contact or damaged target");
    }
    private static void Contacts(AmazonValkyrieBody b,PlayerMobile p)
    {
        foreach(bool hit in new[]{true,false})
        {
            var t=Target(b);
            try
            {
                Start(b,p,t);var s=State(b);ulong seed=HitSeed(b,t,hit);NpcEquipmentRandom.Set(b,seed);int life=SkillHealth.Balance256(t),notifications=t.Notifications;
                Through(b,s.ActionAt);var after=State(b);
                Require(after.ActionAt==0&&after.Attempts==s.Attempts+1&&after.Hits==s.Hits+(hit?1:0),"Action event did not count exactly one original hit/miss");
                Require(hit?SkillHealth.Balance256(t)<life&&t.Notifications==notifications+1:SkillHealth.Balance256(t)==life&&t.Notifications==notifications,"Automatic original contact lost HP/notification ordering");
                Through(b,after.EndAt);Require(State(b).Mode==1,"A1 did not end in source neutral mode");
            }
            finally{SorceressSpellMissiles.Cancel(b);ChainLightning.Cancel(b);t.Delete();}
        }
        var target=Target(b);
        try
        {
            Start(b,p,target);var s=State(b);target.MoveToWorld(new(b.X+20,b.Y,b.Z),b.Map);var seed=NpcEquipmentRandom.Require(b);int life=SkillHealth.Balance256(target);
            Through(b,s.ActionAt);Require(SkillHealth.Balance256(target)==life&&NpcEquipmentRandom.Require(b)==seed&&State(b).Attempts==s.Attempts+1&&State(b).Hits==s.Hits,"Departed target allocated damage/RNG or was hit");
            Start(b,p,target);s=State(b);seed=NpcEquipmentRandom.Require(b);b.Frozen=true;Through(b,s.ActionAt);
            Require(State(b).Mode==1&&State(b).Attempts==s.Attempts&&NpcEquipmentRandom.Require(b)==seed&&SkillHealth.Balance256(target)==life,"Interrupted windup released contact");b.Frozen=false;
        }
        finally{target.Delete();}
        b.Ground.Store(IronGolemWorld.Park(State(b)));PersistentMinions.Park(b);
    }
    private static void Check(bool reload)
    {
        Require(NpcWorldAllocation.Capture()==NpcWorldAllocationChecks.CheckedContinuation,"Unexpected shared world draws before ground contact checks");
        PlayerMobile p;int generation=0;var bodies=new List<AmazonValkyrieBody>();
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=saved.Generation+1;Require(generation<=2,"Ground combat reload count");p=World.FindMobile((Serial)saved.Owner)as PlayerMobile;
            foreach(var h in saved.Bodies)
            {
                var original=JsonSerializer.Deserialize<ValkyrieBodyState>(h.Text);
                var restored=original with{GroundMotion=original.GroundMotion with{World=original.GroundMotion.World with{Controller=IronGolemWorld.Park(original.GroundMotion.World.Controller)}}};
                var b=World.FindMobile((Serial)h.Body)as AmazonValkyrieBody;
                Require(b is{Alive:true,Deleted:false}&&b.ControlMaster==p&&Json(Capture(b))==Json(h with{Text=Json(restored)})&&b.Map==Map.Internal,"Load did not preserve original state through normal summon parking");bodies.Add(b);
            }
            var target=World.FindMobile((Serial)saved.Target)as BaseCreature;var first=bodies[0];var rng=NpcEquipmentRandom.Require(first);int life=SkillHealth.Balance256(target);
            var pendingState=JsonSerializer.Deserialize<ValkyrieBodyState>(saved.Bodies[0].Text).GroundMotion.World.Controller;
            Require(pendingState is{Mode:4,ActionAt:>0}&&State(first)is{Mode:1,ActionAt:0}&&life==saved.TargetLife,"Saved pre-action windup was not cancelled by ordinary summon restore");
            Through(first,State(first).Frame+1,false);Require(State(first)is{Mode:1,ActionAt:0}&&NpcEquipmentRandom.Require(first)==rng&&SkillHealth.Balance256(target)==life,"Load replayed or allocated an overdue attack");
            // Offline players restore internally; return this test owner to
            // the still-live target's saved world before starting a new attack.
            p.MoveToWorld(target.Location,target.Map);target.Delete();PersistentMinions.Suspend(p);
        }
        else
        {
            p=new PlayerMobile{Name="ValkyrieGroundContactFixture",Body=0x190,Player=true};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(p,99);ClassSkillChecks.Train(p,"amazon.valkyrie",20);FixtureSpace.Corridor(p,6220,32,3260);
            using var raw=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"valkyrie-damage-cases.json")));var cases=raw.RootElement.EnumerateArray().Where(c=>c.GetProperty("Critical").GetInt32()==1).ToArray();Require(cases.Length==57,"Missing unfiltered original equipment corpus");
            foreach(var c in cases)
            {
                var b=ValkyrieDamageChecks.Body(p,c);ValkyrieEquipmentResources.Activate(b,0,0,0,0,300);GeneratedNpcLiveResources.ActivateItemEvents(b);ValkyrieEquipmentDefense.Activate(b);
                string before=b.SavedText;var rng=NpcEquipmentRandom.Require(b);bool refused=false;
                try{ValkyrieGroundMotion.ActivateCombat(b,100,75,125,TimedEffects.Now.AddHours(1));}catch(NotSupportedException){refused=true;}
                Require(refused&&b.SavedText==before&&NpcEquipmentRandom.Require(b)==rng,"Combat enabled without original event admission");
                ValkyrieEquipmentContact.ActivateOriginal(b);ValkyrieGroundMotion.ActivateCombat(b,100,75,125,TimedEffects.Now.AddHours(1));TimedEffects.PreventHealing(b,TimedEffects.Now);
                Contacts(b,p);bodies.Add(b);
            }
        }
        // Retain one genuine world windup across each save. No contact packet
        // exists yet; restart cancels the obsolete event instead of replaying it.
        var active=bodies[0];NpcItemChainChecks.Place(active,p);active.Ground.Store(IronGolemWorld.Park(State(active)) with{Map=active.Map.MapID,Motion=DruidRavenMotion.Center(active.X*2,active.Y*2)});
        var pending=Target(active);Start(active,p,pending);Require(State(active).ActionAt==State(active).Frame+1,"Save fixture must stop immediately before the action marker");
        Require(!ClassCombat.Available(p,CombatAbility.Valkyrie),"Prepared ground combat enabled learned summon");
        NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,bodies.Select(Capture).ToArray(),pending.Serial.Value,SkillHealth.Balance256(pending),generation)));
        Log.Information("Valkyrie ground combat:57unfiltered original loadouts;automatic AI67 transformed A1 hit/miss,pre-action RNG/HP silence,departed targets/interruption,end mode;original items/resources/wounds/RNG and actual pre-action windup through two reloads;supplied timing bases and learned cast remain separate");
    }
}
