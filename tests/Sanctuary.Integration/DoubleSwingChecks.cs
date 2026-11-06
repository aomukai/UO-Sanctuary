// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class DoubleSwingChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(DoubleSwingChecks));
    private sealed record Saved(uint Owner,uint First,uint Second,uint Target,int Life,int Mana,int TargetLife,int FirstHp,int SecondHp,string FirstGear,string SecondGear,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"double-swing-fixtures.json");
    private static int Index;
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Train(PlayerMobile p,string id,int rank)
    {
        TalentCatalog.TryGet(DiabloClass.Barbarian,id,out var row);foreach(string pre in row.Prerequisites){Train(p,pre,1);}
        while(ClassCreation.Profiles.GetRank(p.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}
    }
    internal static PlayerMobile Owner(int rank=1,bool throwing=false,CombatAbility ability=CombatAbility.DoubleSwing)
    {
        var p=new PlayerMobile{Name="PoisonDaggerFixtureDoubleSwing"+(Index++),Player=true,Body=0x190};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Barbarian);ClassCreation.Profiles.AdvanceLevel(p,99);
        while(p.RawStr<120)Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Strength");
        while(p.RawDex<100)Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Dexterity");
        Train(p,SkillMechanics.For(ability).TalentId,rank);FixtureSpace.Corridor(p,6800,24,1600+Index*24);
        BaseWeapon first=throwing?new NamedThrowingKnife():new Broadsword(),second=throwing?new NamedThrowingKnife():new Mace();
        foreach(var weapon in new[]{first,second}){ClassGear.Profiles.Convert(weapon,99,GearRarity.Normal,new Random(173));p.AddToBackpack(weapon);Require(p.EquipItem(weapon),"Native Double Swing pair equip");}
        Require(BarbarianDoubleSwing.Pair(p)==new WeaponPair(first,second),"Pair/primary identity lost");NecromancerEffects.SetMana256(p,NecromancerEffects.ManaMaximum256(p));return p;
    }
    internal static ReviveEventChecks.Recipient Target(PlayerMobile p,int dx=1,int dy=0)
    {
        var t=new ReviveEventChecks.Recipient{Name="PoisonDaggerFixtureDoubleSwingTarget",NoKillAwards=true,Frozen=true,CantWalk=true};t.SetHits(20000);t.Hits=t.HitsMax;
        foreach(var r in Enum.GetValues<ResistanceType>())t.SetResistance(r,0);
        t.MoveToWorld(new(p.X+dx,p.Y+dy,p.Z),p.Map);t.AIObject?.AITimer.Stop();TimedEffects.PreventHealing(t,TimedEffects.Now);return t;
    }
    internal static void Ready(PlayerMobile p){ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;}
    private static bool Begin(PlayerMobile p,Mobile t){Ready(p);return ClassCombat.Perform(p,CombatAbility.DoubleSwing,t,t.Location,Core.TickCount);}
    private static bool Event(PlayerMobile p)=>ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt);
    internal static void Clear(PlayerMobile p,params Mobile[] targets){ClassCombat.Stop(p);foreach(var t in targets){t.Corpse?.Delete();t.Delete();}p.Corpse?.Delete();p.Delete();}
    private static void Rules()
    {
        Require(CombatTiming.WeaponAttack(CombatAbility.DoubleSwing)&&CombatTiming.SequenceRateAdjustment(CombatAbility.DoubleSwing)==20,"Approved attack-rate channel missing");
        foreach(int rank in new[]{1,2,8,9,20,40,255})foreach(int hard in new[]{0,1,20})
        {
            var v=new SkillValues(SkillMechanics.For(CombatAbility.DoubleSwing),k=>k=="Double Swing"?rank:255,k=>k=="Bash"?hard:0,99);
            Require(v.Mana256==Math.Max(0,9-rank)*32&&v.Formula("toht")==15+5*(rank-1)&&v.Eval("calc1")==hard*10,"Pinned cost/AR/hard-only Bash formula differs");
        }
        Require(CombatTiming.Calculate(DiabloClass.Barbarian,CombatAbility.DoubleSwing,new(AttackFamily.Swing,0))==new ActionTiming(15,4),"Approved default SQ11 timing differs");
    }
    private static void Releases()
    {
        foreach(bool throwing in new[]{false,true})
        {
            var p=Owner(2,throwing);var t=Target(p);var pair=BarbarianDoubleSwing.Pair(p).Value;int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.DoubleSwing).Mana256;
            int firstHp=pair.Primary.HitPoints,secondHp=pair.Secondary.HitPoints;
            try
            {
                Require(Begin(p,t),"Learned Double Swing begin failed");var series=ClassCombat.For(p).DoubleSwing;long end=ClassCombat.For(p).NextAction;
                Require(series.Events.Length==2&&!series.Basic&&series.Events[0]<series.Events[1]&&!ClassCombat.ResolveAction(p,series.Events[0]-1)&&NecromancerEffects.ManaBalance256(p)==mana&&t.Notifications==0,"Premature payment/delivery");
                Require(Event(p)&&t.Notifications==1&&NecromancerEffects.ManaBalance256(p)==mana-cost&&pair.Secondary.HitPoints==secondHp,"First hand did not release/pay independently");
                int afterFirst=SkillHealth.Balance256(t),primaryHp=pair.Primary.HitPoints;
                Require(Event(p)&&t.Notifications==2&&SkillHealth.Balance256(t)<afterFirst&&NecromancerEffects.ManaBalance256(p)==mana-2*cost&&pair.Primary.HitPoints==primaryHp,"Second hand did not release/pay independently");
                int life=SkillHealth.Balance256(t);Require(ClassCombat.For(p).DoubleSwing==null&&!ClassCombat.ResolveAction(p,end)&&SkillHealth.Balance256(t)==life&&NecromancerEffects.ManaBalance256(p)==mana-2*cost,"Completed series replayed");
                Require(WeaponHands.Pair(p)==pair&&CombatTiming.Equipped(p)==pair.Primary&&pair.Primary.HitPoints<=firstHp&&pair.Secondary.HitPoints<=secondHp,"Selected-hand scope/weapon identity leaked");
            }
            finally{Clear(p,t);}
        }
        var owner=Owner();var a=Target(owner);var b=Target(owner,0,1);var c=Target(owner,-1);
        try
        {
            Require(Begin(owner,c)&&Event(owner)&&c.Notifications==1&&Event(owner)&&a.Notifications==1&&b.Notifications==0,"GUID wrap did not retarget the second hand independently");
        }
        finally{Clear(owner,a,b,c);}
    }
    private static void WiderSearch()
    {
        var p=Owner();var old=BarbarianDoubleSwing.Pair(p).Value.Secondary;p.Backpack.DropItem(old);
        var second=(BaseWeapon)NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="The Patriarch").Id,99,new Random(173),true);
        p.AddToBackpack(second);Require(p.EquipItem(second)&&BarbarianDoubleSwing.Pair(p).Value.Secondary==second,"Wider-search secondary equip");
        var first=Target(p);ReviveEventChecks.Recipient outside;bool beyondContact;
        using(WeaponHands.Use(p,second))
        {
            outside=Target(p,(int)Math.Floor(SkillGeometry.Tiles(PlayerMeleeGeometry.Reach(p)+4)));
            beyondContact=!BarbarianDoubleSwing.InRange(p,outside);
        }
        var inside=Target(p,0,1);int mana=NecromancerEffects.ManaBalance256(p);
        try
        {
            Require(beyondContact&&Begin(p,first)&&Event(p)&&first.Notifications==1&&Event(p)
                &&outside.Notifications==0&&inside.Notifications==0&&NecromancerEffects.ManaBalance256(p)==mana-512,
                "Second search discarded a valid wider-radius GUID or bypassed contact range/payment");
        }
        finally{Clear(p,first,outside,inside);}
    }
    private static void Misses()
    {
        var p=Owner();var t=Target(p);int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(t);var prior=BarbarianDoubleSwing.HitRoll;
        try
        {
            BarbarianDoubleSwing.HitRoll=()=>1;
            Require(Begin(p,t)&&Event(p)&&Event(p)&&SkillHealth.Balance256(t)==life&&t.Notifications==0&&NecromancerEffects.ManaBalance256(p)==mana-512,"Missed released strikes did not pay exactly once");
        }
        finally{BarbarianDoubleSwing.HitRoll=prior;Clear(p,t);}
    }
    private static void Funding()
    {
        foreach(int scene in new[]{0,1,2})
        {
            var p=Owner(scene==2?9:1);var t=Target(p);int mana=scene==0?255:scene==1?256:0;NecromancerEffects.SetMana256(p,mana);
            try
            {
                Require(Begin(p,t),"Approved low/zero mana route refused");var series=ClassCombat.For(p).DoubleSwing;
                Require(series.Basic==(scene==0)&&series.Events.Length==(scene==0?1:2),"Underfunded start did not become one ordinary attack");
                Require(Event(p),"Funding first event failed");if(scene!=0)Require(Event(p),"Funding second event failed");
                Require(t.Notifications==(scene==0?1:2)&&NecromancerEffects.ManaBalance256(p)==(scene==0?255:0),"Funding spent absent mana,charged free rank9 or repeated fallback");
            }
            finally{Clear(p,t);}
        }
    }
    private static void Cancellation()
    {
        foreach(string mode in new[]{"stop","move","move-back","facet","offhand","broken","paralyze","respec","disconnect","logout","death"})
        {
            var p=Owner();var t=Target(p);var pair=BarbarianDoubleSwing.Pair(p).Value;
            try
            {
                Require(Begin(p,t)&&Event(p),"Cancellation could not release first hand");int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(t);long next=ClassCombat.For(p).EffectAt;
                if(mode=="stop")ClassCombat.Stop(p);
                if(mode=="move")p.X++;
                if(mode=="move-back"){p.X++;p.X--;}
                if(mode=="disconnect")EventSink.InvokeDisconnected(p);
                if(mode=="logout")EventSink.InvokeLogout(p);
                if(mode=="facet")p.MoveToWorld(p.Location,Map.Felucca);
                if(mode=="offhand")p.Backpack.DropItem(pair.Secondary);
                if(mode=="broken")pair.Secondary.HitPoints=0;
                if(mode=="paralyze")p.Paralyzed=true;
                if(mode=="respec"){ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);}
                if(mode=="death")p.Kill();
                Require(!ClassCombat.ResolveAction(p,next+1000)&&ClassCombat.For(p).DoubleSwing==null&&t.Notifications==1&&SkillHealth.Balance256(t)==life,"Cancelled sequence released its second hand: "+mode);
                if(mode!="death")Require(NecromancerEffects.ManaBalance256(p)==mana,"Cancellation refunded or charged another strike: "+mode);
            }
            finally{Clear(p,t);}
        }
    }
    private static void Check(bool reload)
    {
        Rules();
        if(!reload)
        {
            var hit=BarbarianDoubleSwing.HitRoll;var element=BarbarianDoubleSwing.ElementRoll;var physical=BarbarianDoubleSwing.PhysicalRoll;var poison=BarbarianDoubleSwing.PoisonRoll;var block=SourceBlocking.BlockRoll;
            try
            {
                BarbarianDoubleSwing.HitRoll=()=>0;BarbarianDoubleSwing.ElementRoll=()=>0;BarbarianDoubleSwing.PhysicalRoll=_=>0;BarbarianDoubleSwing.PoisonRoll=_=>0;SourceBlocking.BlockRoll=_=>99;
                Releases();WiderSearch();Misses();Funding();Cancellation();var p=Owner(2);var t=Target(p);var pair=BarbarianDoubleSwing.Pair(p).Value;
                Require(Begin(p,t)&&Event(p),"Saved half-sequence setup failed");int mana=NecromancerEffects.ManaBalance256(p);ClassCombat.Stop(p);Require(NecromancerEffects.ManaBalance256(p)==mana,"Stop refunded the released hand");
                SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-1237);TimedEffects.PreventHealing(p,TimedEffects.Now);
                var saved=new Saved(p.Serial.Value,pair.Primary.Serial.Value,pair.Secondary.Serial.Value,t.Serial.Value,SkillHealth.Balance256(p),mana,SkillHealth.Balance256(t),pair.Primary.HitPoints,pair.Secondary.HitPoints,JsonSerializer.Serialize(ClassGear.State(pair.Primary)),JsonSerializer.Serialize(ClassGear.State(pair.Secondary)),0);
                p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
            }
            finally{BarbarianDoubleSwing.HitRoll=hit;BarbarianDoubleSwing.ElementRoll=element;BarbarianDoubleSwing.PhysicalRoll=physical;BarbarianDoubleSwing.PoisonRoll=poison;SourceBlocking.BlockRoll=block;}
        }
        else
        {
            var row=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)row.Owner);var first=(BaseWeapon)World.FindItem((Serial)row.First);var second=(BaseWeapon)World.FindItem((Serial)row.Second);var t=(BaseCreature)World.FindMobile((Serial)row.Target);
            Require(first.Parent==p&&second.Parent==p&&WeaponHands.Pair(p)==new WeaponPair(first,second),"Saved pair identity/layers changed");
            Require(SkillHealth.Balance256(p)==row.Life,$"Owner life changed: {SkillHealth.Balance256(p)} expected {row.Life}");
            Require(NecromancerEffects.ManaBalance256(p)==row.Mana,$"Owner mana changed: {NecromancerEffects.ManaBalance256(p)} expected {row.Mana}");
            Require(SkillHealth.Balance256(t)==row.TargetLife,$"Target life changed: {SkillHealth.Balance256(t)} expected {row.TargetLife}");
            Require(first.HitPoints==row.FirstHp&&second.HitPoints==row.SecondHp,$"Weapon durability changed: {first.HitPoints}/{second.HitPoints} expected {row.FirstHp}/{row.SecondHp}");
            Require(JsonSerializer.Serialize(ClassGear.State(first))==row.FirstGear&&JsonSerializer.Serialize(ClassGear.State(second))==row.SecondGear,"Saved original weapon rolls changed");
            Require(ClassCombat.For(p).DoubleSwing==null,"Cancelled sequence replayed on reload");
            File.WriteAllText(Manifest,JsonSerializer.Serialize(row with{Generation=row.Generation+1}));
        }
        Log.Information("Approved Double Swing:normal/throwing melee pairs,independent paid hands,GUID wrap,rank2fraction/rank9free,one-hit low-mana fallback,mid-series funding fallback,11cancellation paths and saved original pair/resources;modern startup adaptation explicit");
    }
    private static void Input(NetState net,string mode)
    {
        var p=Owner();var target=Target(p);FixtureNetwork.Authenticate(net,p,"double-swing-");Ready(p);
        Require(SkillBindings.Instance.Assign(p,mode=="left",CombatAbility.DoubleSwing)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.DoubleSwing),"Double Swing input binding");
        int mana=NecromancerEffects.ManaBalance256(p);bool began=false;long deadline=Core.TickCount+5000;var old=BarbarianDoubleSwing.HitRoll;BarbarianDoubleSwing.HitRoll=()=>0;
        string name="double-swing-"+mode;
        void Poll()
        {
            try
            {
                var session=ClassCombat.For(p);
                if(!began&&session.DoubleSwing!=null){began=true;Log.Information("{Name}: windup",name);}
                if(began&&session.DoubleSwing==null)
                {
                    Require(target.Notifications==2&&NecromancerEffects.ManaBalance256(p)==mana-512,"Natural input did not release exactly two independently paid hands");
                    BarbarianDoubleSwing.HitRoll=old;ClassCombat.Stop(p);Clear(p,target);Log.Information("{Name}-result: passed",name);return;
                }
                Require(Core.TickCount<deadline,"Double Swing input timeout");Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Poll));
            }
            catch(Exception e){BarbarianDoubleSwing.HitRoll=old;Clear(p,target);Log.Error(e,"{Name}-result: failed",name);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(()=>
        {
            Log.Information("{Name}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",name,(byte)CombatAbility.DoubleSwing,(byte)DiabloClass.Barbarian,target.Serial.Value,target.X,target.Y,target.Z);Poll();
        }));
    }
    public static void Configure()
    {
        foreach(string name in new[]{"double-swing-check","double-swing-reload-check"})ConsoleInputHandler.RegisterCommand(name,"Approved Double Swing mechanical checks",_=>Core.LoopContext.Post(()=>
        {try{Check(name.EndsWith("-reload-check",StringComparison.Ordinal));Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
        foreach(string mode in new[]{"left","right","quick"})
        {string captured=mode;ConsoleInputHandler.RegisterCommand("double-swing-"+mode+"-check","Double Swing real input",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("double-swing-"+captured,net=>
        {try{Input(net,captured);Log.Information("double-swing-{Mode}-check: passed",captured);}catch(Exception e){Log.Error(e,"double-swing-{Mode}-check: failed",captured);}})));}
    }
}
