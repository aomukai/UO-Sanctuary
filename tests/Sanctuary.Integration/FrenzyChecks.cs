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

public static class FrenzyChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(FrenzyChecks));
    private sealed record Saved(uint Owner,uint First,uint Second,uint Target,int Life,int Mana,int TargetLife,int FirstHp,int SecondHp,string FirstGear,string SecondGear,FrenzySpeedState Speed,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"frenzy-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner(int rank=3,bool throwing=false)=>DoubleSwingChecks.Owner(rank,throwing,CombatAbility.Frenzy);
    private static ReviveEventChecks.Recipient Target(PlayerMobile p,int dx=1,int dy=0)=>DoubleSwingChecks.Target(p,dx,dy);
    private static void Clear(PlayerMobile p,params Mobile[] targets){BarbarianFrenzyEffects.Clear(p);DoubleSwingChecks.Clear(p,targets);}
    private static bool Begin(PlayerMobile p,Mobile t){DoubleSwingChecks.Ready(p);return ClassCombat.Perform(p,CombatAbility.Frenzy,t,t.Location,Core.TickCount);}
    private static bool Event(PlayerMobile p)=>ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt);
    private static void Train(PlayerMobile p,string id,int rank)
    {
        Require(TalentCatalog.TryGet(DiabloClass.Barbarian,id,out var row),"Barbarian talent");foreach(string pre in row.Prerequisites){if(ClassCreation.Profiles.GetRank(p.Serial,pre)==0){Train(p,pre,1);}}
        while(ClassCreation.Profiles.GetRank(p.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}
    }
    private static void Rules()
    {
        foreach(var row in new[]{new[]{0,0,0},new[]{1,47,7},new[]{2,68,13},new[]{3,84,18},new[]{20,171,42},new[]{60,200,50},new[]{255,200,50}})
        {Require(BarbarianFrenzyEffects.Values(row[0])==new FrenzySpeed(row[1],row[2]),"Pinned charge speed curve differs");}
        foreach(int rank in new[]{1,3,20,40,255})foreach(int hard in new[]{0,1,20})
        {
            var v=new SkillValues(SkillMechanics.For(CombatAbility.Frenzy),key=>key=="Frenzy"?rank:255,_=>hard,99);
            Require(v.Mana256==384&&v.Formula("toht")==100+7*(rank-1)&&v.Eval("calc1")==90+5*(rank-1)+16*hard&&v.Eval("calc4")==hard&&v.Eval("auralencalc")==150+10*hard,"Pinned Frenzy damage/AR/cost/hard synergies differ");
        }
        Require(CombatTiming.WeaponAttack(CombatAbility.Frenzy)&&CombatTiming.SequenceRateAdjustment(CombatAbility.Frenzy)==-30,"Frenzy did not use shared sequence channel without Double Swing bonus");
    }
    private static void Contacts()
    {
        foreach(bool throwing in new[]{false,true})
        {
            var p=Owner(3,throwing);var t=Target(p);var pair=WeaponHands.Pair(p).Value;int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(t);
            try
            {
                int walk=ClassCombatHooks.WalkingDelay(p,400),run=ClassCombatHooks.RunningDelay(p,400);var before=CombatTiming.For(p,CombatAbility.BasicAttack);
                Require(Begin(p,t),"Learned Frenzy refused pair");var series=ClassCombat.For(p).Frenzy;long end=ClassCombat.For(p).NextAction;
                Require(series is{Basic:false,Released:0}&&series.Events.Length==2&&!ClassCombat.ResolveAction(p,series.Events[0]-1)&&NecromancerEffects.ManaBalance256(p)==mana,"Frenzy paid or hit early");
                Require(Event(p)&&t.Notifications==1&&SkillHealth.Balance256(t)<life&&NecromancerEffects.ManaBalance256(p)==mana-384&&BarbarianFrenzyEffects.State(p)?.Charges==1,"First paid contact did not grant one charge");
                Require(Event(p)&&t.Notifications==2&&NecromancerEffects.ManaBalance256(p)==mana-768&&BarbarianFrenzyEffects.State(p)?.Charges==2,"Second hand did not pay/grant independently");
                Require(ClassCombat.For(p).Frenzy==null&&!ClassCombat.ResolveAction(p,end)&&t.Notifications==2&&WeaponHands.Pair(p)==pair,"Completed Frenzy replay or hand leak");
                Require(ClassCombatHooks.WalkingDelay(p,400)<walk&&ClassCombatHooks.RunningDelay(p,400)<run&&CombatTiming.For(p,CombatAbility.BasicAttack).Frames<before.Frames,"Frenzy speeds did not reach actual walking/running/attack consumers");
                Require(Begin(p,t)&&Event(p)&&Event(p)&&BarbarianFrenzyEffects.State(p)?.Charges==3,"Frenzy charge cap differs");
                var speed=BarbarianFrenzyEffects.State(p);ClassCombat.Stop(p);p.X++;p.Backpack.DropItem(pair.Secondary);
                Require(BarbarianFrenzyEffects.State(p)==speed,"Stopping/moving/swapping reset earned speed");
            }
            finally{Clear(p,t);}
        }
        var owner=Owner();var a=Target(owner);var b=Target(owner,0,1);
        try{Require(Begin(owner,b)&&Event(owner)&&Event(owner)&&a.Notifications==1&&b.Notifications==1,"Shared GUID wrap lost second Frenzy recipient");}
        finally{Clear(owner,a,b);}
    }
    private static void Funding()
    {
        foreach(int mana in new[]{383,384,767})
        {
            var p=Owner();var t=Target(p);NecromancerEffects.SetMana256(p,mana);
            try
            {
                Require(Begin(p,t),"Underfunded Frenzy did not start basic fallback");var series=ClassCombat.For(p).Frenzy;
                Require(series.Basic==(mana<384)&&series.Events.Length==(mana<384?1:2)&&Event(p),"Low-mana fallback event contract");if(mana>=384){Require(Event(p),"Mid-series ordinary contact refused");}
                Require(t.Notifications==(mana<384?1:2)&&NecromancerEffects.ManaBalance256(p)==(mana<384?mana:mana-384)&&(BarbarianFrenzyEffects.State(p)?.Charges??0)==(mana<384?0:1),"Fallback spent absent mana or granted a skill charge");
            }
            finally{Clear(p,t);}
        }
    }
    private static void MissBlockAndAbsorb()
    {
        foreach(bool blocked in new[]{false,true})
        {
            var p=Owner();var t=new ZealContactChecks.Recipient{Frozen=true,CantWalk=true,NoKillAwards=true};t.SetHits(20000);t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();TimedEffects.PreventHealing(t,TimedEffects.Now);
            try
            {
                string source=CreatureProfiles.Definition(t).Reference;SourceBlocking.Restore(SourceBlocking.Snapshot().Where(s=>s.Actor!=t.Serial.Value).Append(new(t.Serial.Value,source,1)).ToArray());
                SourceBlocking.BlockRoll=n=>blocked?0:n-1;BarbarianFrenzy.HitRoll=()=>blocked?0:1;
                int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(t);
                Require(Begin(p,t)&&Event(p)&&Event(p)&&NecromancerEffects.ManaBalance256(p)==mana-768&&SkillHealth.Balance256(t)==life&&BarbarianFrenzyEffects.State(p)==null,"Miss/block granted speed or escaped per-release payment");
            }
            finally{BarbarianFrenzy.HitRoll=()=>0;SourceBlocking.BlockRoll=n=>n-1;Clear(p,t);}
        }
        var owner=Owner();var target=Target(owner);int health=SkillHealth.Balance256(target);
        try
        {
            NecromancerEffects.Profiles.Store(target,new(BoneArmor:10000));int pool=NecromancerEffects.Armor256(target);
            Require(Begin(owner,target)&&Event(owner)&&Event(owner)&&SkillHealth.Balance256(target)==health&&NecromancerEffects.Armor256(target)<pool&&BarbarianFrenzyEffects.State(owner)?.Charges==2,"Absorbed successful contacts did not grant charges independently of life loss");
        }
        finally{Clear(owner,target);}
    }
    private static void GearAndConversion()
    {
        var p=Owner(1);var t=Target(p);
        try
        {
            var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey=="Tarnhelm").Id,99,new Random(173),true);p.AddToBackpack(helm);Require(p.EquipItem(helm),"Original +skill gear");ClassGear.Refresh(p);
            Require(SkillMechanics.Rank(p,CombatAbility.Frenzy)==2&&Begin(p,t)&&Event(p)&&Event(p)&&BarbarianFrenzyEffects.State(p)?.Charges==2,"Effective rank did not supply charge cap");
            p.AddToBackpack(helm);ClassGear.Refresh(p);Require(Begin(p,t)&&Event(p)&&Event(p)&&BarbarianFrenzyEffects.State(p)?.Charges==1,"Next gain did not cap at current rank");
        }
        finally{Clear(p,t);}
        foreach(bool berserk in new[]{false,true})
        {
            p=Owner(1);t=Target(p);t.SetResistance(ResistanceType.Physical,100);int life=SkillHealth.Balance256(t);
            try
            {
                if(berserk){Train(p,"barbarian.berserk",20);}
                Require(Begin(p,t)&&Event(p)&&Event(p)&&(SkillHealth.Balance256(t)<life)==berserk,"Hard Berserk conversion did not reach ordinary physical/magic mitigation");
            }
            finally{Clear(p,t);}
        }
    }
    private static void Cancellation()
    {
        foreach(string mode in new[]{"stop","move","move-back","facet","offhand","broken","paralyze","respec","disconnect","logout","death"})
        {
            var p=Owner();var t=Target(p);var pair=WeaponHands.Pair(p).Value;
            try
            {
                Require(Begin(p,t)&&Event(p),"Cancel setup");int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(t);long next=ClassCombat.For(p).EffectAt;
                if(mode=="stop")ClassCombat.Stop(p);if(mode=="move")p.X++;if(mode=="move-back"){p.X++;p.X--;}
                if(mode=="facet")p.MoveToWorld(p.Location,Map.Felucca);if(mode=="offhand")p.Backpack.DropItem(pair.Secondary);if(mode=="broken")pair.Secondary.HitPoints=0;
                if(mode=="paralyze")p.Paralyzed=true;if(mode=="respec"){ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);}
                if(mode=="disconnect")EventSink.InvokeDisconnected(p);if(mode=="logout")EventSink.InvokeLogout(p);if(mode=="death")p.Kill();
                Require(!ClassCombat.ResolveAction(p,next+1000)&&ClassCombat.For(p).Frenzy==null&&t.Notifications==1&&SkillHealth.Balance256(t)==life,"Cancelled Frenzy released another hand:"+mode);
                if(mode!="death"){Require(NecromancerEffects.ManaBalance256(p)==mana,"Cancellation refunded/charged:"+mode);}
                Require((BarbarianFrenzyEffects.State(p)?.Charges??0)==(mode is "respec" or "death"?0:1),"Cancellation reset or revived speed:"+mode);
            }
            finally{Clear(p,t);}
        }
    }
    private static void RespecResources()
    {
        foreach(bool cap in new[]{false,true})
        {
            var p=Owner();var t=Target(p);
            try
            {
                for(int i=0;i<20;i++){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Vitality)&&ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Energy),"Respec resource attributes");}
                Require(Begin(p,t)&&Event(p),"Respec charge setup");
                int life=cap?SkillHealth.Maximum256(p)-17:SkillHealth.Maximum256(p)/4+37;
                int mana=cap?NecromancerEffects.ManaMaximum256(p)-19:NecromancerEffects.ManaMaximum256(p)/4+59;
                int stam=cap?BattleOrders.StaminaMaximum256(p)-23:BattleOrders.StaminaMaximum256(p)/4+83;
                SkillHealth.SetBalance256(p,life);NecromancerEffects.SetMana256(p,mana);BattleOrders.SetStamina256(p,stam);
                ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);
                Require(SkillHealth.Balance256(p)==Math.Min(life,SkillHealth.Maximum256(p))&&NecromancerEffects.ManaBalance256(p)==Math.Min(mana,NecromancerEffects.ManaMaximum256(p))
                    &&BattleOrders.Stamina256(p)==Math.Min(stam,BattleOrders.StaminaMaximum256(p))&&BarbarianFrenzyEffects.State(p)==null,"Respec rounded/refilled fractional resources or retained Frenzy");
            }
            finally{Clear(p,t);}
        }
    }
    private static void Check(bool reload)
    {
        Rules();var hit=BarbarianFrenzy.HitRoll;var element=BarbarianFrenzy.ElementRoll;var physical=BarbarianFrenzy.PhysicalRoll;var poison=BarbarianFrenzy.PoisonRoll;var block=SourceBlocking.BlockRoll;var shields=SourceBlocking.Snapshot();
        try
        {
            BarbarianFrenzy.HitRoll=()=>0;BarbarianFrenzy.ElementRoll=()=>0;BarbarianFrenzy.PhysicalRoll=_=>0;BarbarianFrenzy.PoisonRoll=_=>0;SourceBlocking.BlockRoll=n=>n-1;
            if(!reload)
            {
                Contacts();Funding();MissBlockAndAbsorb();GearAndConversion();Cancellation();RespecResources();var p=Owner();var t=Target(p);var pair=WeaponHands.Pair(p).Value;
                Require(Begin(p,t)&&Event(p),"Saved Frenzy first contact");ClassCombat.Stop(p);
                // Explicitly held save fixture; actual six-second expiry is
                // tested separately by natural left input on every boot.
                var speed=BarbarianFrenzyEffects.State(p)with{UntilUtc=TimedEffects.Now+FixtureSpace.Retention};BarbarianFrenzyEffects.Restore(BarbarianFrenzyEffects.Snapshot().Where(s=>s.Owner!=p.Serial.Value).Append(speed).ToArray());
                SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-1237);TimedEffects.PreventHealing(p,TimedEffects.Now);
                var saved=new Saved(p.Serial.Value,pair.Primary.Serial.Value,pair.Secondary.Serial.Value,t.Serial.Value,SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(t),pair.Primary.HitPoints,pair.Secondary.HitPoints,JsonSerializer.Serialize(ClassGear.State(pair.Primary)),JsonSerializer.Serialize(ClassGear.State(pair.Secondary)),speed,0);
                p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
            }
            else
            {
                var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var first=(BaseWeapon)World.FindItem((Serial)s.First);var second=(BaseWeapon)World.FindItem((Serial)s.Second);var t=World.FindMobile((Serial)s.Target);
                Require(first.Parent==p&&second.Parent==p&&WeaponHands.Pair(p)==new WeaponPair(first,second)&&BarbarianFrenzyEffects.State(p)==s.Speed,"Saved original pair/charge/expiry changed");
                Require(SkillHealth.Balance256(p)==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillHealth.Balance256(t)==s.TargetLife&&first.HitPoints==s.FirstHp&&second.HitPoints==s.SecondHp,"Saved wounds/resources/durability changed");
                Require(JsonSerializer.Serialize(ClassGear.State(first))==s.FirstGear&&JsonSerializer.Serialize(ClassGear.State(second))==s.SecondGear&&ClassCombat.For(p).Frenzy==null,"Saved original rolls changed or cancelled sequence replayed");
                File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
            }
        }
        finally{BarbarianFrenzy.HitRoll=hit;BarbarianFrenzy.ElementRoll=element;BarbarianFrenzy.PhysicalRoll=physical;BarbarianFrenzy.PoisonRoll=poison;SourceBlocking.BlockRoll=block;SourceBlocking.Restore(shields);}
        Log.Information("Approved Frenzy:paid independent normal/throwing hands,charge caps/gear,miss/block/absorbed contact,ordinary fallback,hard Berserk magic conversion,11cancellations,real speed consumers and original saved resources/charge expiry;current startup adaptation explicit");
    }
    private static void Input(NetState net,string mode)
    {
        var p=Owner();var t=Target(p);FixtureNetwork.Authenticate(net,p,"frenzy-");DoubleSwingChecks.Ready(p);
        Require(SkillBindings.Instance.Assign(p,mode=="left",CombatAbility.Frenzy)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Frenzy),"Frenzy input binding");
        int mana=NecromancerEffects.ManaBalance256(p);bool began=false,ended=false;long deadline=Core.TickCount+10000;var old=BarbarianFrenzy.HitRoll;BarbarianFrenzy.HitRoll=()=>0;
        string name="frenzy-"+mode;DateTime until=default;
        void Finish(){BarbarianFrenzy.HitRoll=old;ClassCombat.Stop(p);Clear(p,t);Log.Information("{Name}-result: passed",name);}
        void Poll()
        {
            try
            {
                var session=ClassCombat.For(p);if(!began&&session.Frenzy!=null){began=true;Log.Information("{Name}: windup",name);}
                if(!ended&&began&&session.Frenzy==null)
                {
                    Require(t.Notifications==2&&NecromancerEffects.ManaBalance256(p)==mana-768&&BarbarianFrenzyEffects.State(p)is{Charges:2},"Natural input contact/payment/charges differ");
                    until=BarbarianFrenzyEffects.State(p).UntilUtc;Require((until-TimedEffects.Now).TotalSeconds is >5 and <=6,"Natural six-second duration differs");
                    ClassCombat.Stop(p);ended=true;if(mode!="left"){Finish();return;}
                }
                if(ended&&TimedEffects.Now>until.AddMilliseconds(100))
                {Require(BarbarianFrenzyEffects.State(p)==null&&BarbarianFrenzyEffects.Velocity(p)==0&&BarbarianFrenzyEffects.AttackRate(p)==0&&t.Notifications==2,"Natural expiry failed or replayed a contact");Finish();return;}
                Require(Core.TickCount<deadline,"Frenzy input timeout");Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Poll));
            }
            catch(Exception e){BarbarianFrenzy.HitRoll=old;Clear(p,t);Log.Error(e,"{Name}-result: failed",name);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(()=>
        {Log.Information("{Name}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",name,(byte)CombatAbility.Frenzy,(byte)DiabloClass.Barbarian,t.Serial.Value,t.X,t.Y,t.Z);Poll();}));
    }
    public static void Configure()
    {
        foreach(string name in new[]{"frenzy-check","frenzy-reload-check"})ConsoleInputHandler.RegisterCommand(name,"Approved Frenzy mechanical checks",_=>Core.LoopContext.Post(()=>
        {try{Check(name.EndsWith("-reload-check",StringComparison.Ordinal));Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
        foreach(string mode in new[]{"left","right","quick"})
        {string captured=mode;ConsoleInputHandler.RegisterCommand("frenzy-"+mode+"-check","Frenzy real input",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("frenzy-"+captured,net=>
        {try{Input(net,captured);Log.Information("frenzy-{Mode}-check: passed",captured);}catch(Exception e){Log.Error(e,"frenzy-{Mode}-check: failed",captured);}})));}
    }
}
