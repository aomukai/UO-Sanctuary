// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class ConsumeAuraChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ConsumeAuraChecks));
    private sealed record Saved(int Generation,uint Owner,uint Ally,uint Enemy,ConsumeEffect State,int Life,int AllyLife);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"consume-aura-fixtures.json");
    private static PaladinAuraLease[] Loaded=[];
    private static bool Creating;
    private static void Rates(){var old=Mobile.HitsRegenRateHandler;Mobile.HitsRegenRateHandler=m=>Creating||m.Name=="ConsumeAuraFixture"?TimeSpan.FromHours(1):old?.Invoke(m)??Mobile.DefaultHitsRate;}
    [CallPriority(int.MaxValue)]public static void Initialize(){Rates();Loaded=PaladinAuras.Snapshot().Leases;}
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        ConsoleInputHandler.RegisterCommand("consume-aura-save-check","Refresh real short recipient lease immediately before saved-world proof",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("consume-aura-save-check",BeforeSave)));
        Rates();foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"consume-aura-reload-check":"consume-aura-check";
            ConsoleInputHandler.RegisterCommand(name,"Consume fixed-rank Paladin aura providers and native lifecycle",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>
            {try{if(reload){Reload(n);Log.Information("{Name}: passed",name+"-result");}else{Scene(n);}}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});}));}
    }
    private static PlayerMobile Player(DiabloClass type)
    {
        PlayerMobile p;Creating=true;try{p=new PlayerMobile{Body=0x191,Female=true,Player=true,Name="ConsumeAuraFixture"};ClassCreation.Profiles.Assign(p,type);ClassCreation.Profiles.AdvanceLevel(p,99);}finally{Creating=false;}
        p.AddItem(new Backpack());p.Hits=p.HitsMax;p.Mana=p.ManaMax;return p;
    }
    private static ConsumeEffect Put(PlayerMobile p,string monster,MonsterDifficulty difficulty=MonsterDifficulty.Hell)
    {
        var dummy=new Daemon();BattleCommand.Track(dummy);uint serial=dummy.Serial.Value;long order=BattleCommand.PetOrder(dummy);dummy.Delete();
        var value=ConsumePrepared.Capture(new(SkillMechanics.For(CombatAbility.Consume),_=>20,_=>0,99),0,monster,difficulty);var at=TimedEffects.Now;
        var state=new ConsumeEffect(p.Serial.Value,serial,order,at,at.AddMilliseconds(value.DurationFrames*40L),value);ConsumeEffects.ApplyPrepared(p,state);return state;
    }
    private static DateTime Due(PlayerMobile p,ConsumeEffect state)=>SkillMechanics.For(ConsumeAuras.Ability(state.Suppliers.Bonus.AuraSkill)).Number("immediate")!=0?state.AppliedUtc
        :state.AppliedUtc.AddMilliseconds(Math.Max(5,(int)ConsumeAuras.Values(p,state).Eval("perdelay"))*40L);
    private static void Pulse(PlayerMobile p,ConsumeEffect s,DateTime at)=>ConsumeAuras.Tick(p,s,at);
    private static void Basic(PlayerMobile p,PlayerMobile ally,GoatmanNpcContactChecks.Recipient enemy)
    {
        NecromancerEffects.SetMana256(p,0);var selected=PaladinAuras.Selected(p);var s=Put(p,"megademon6");var due=Due(p,s);
        Require(ConsumeAuras.Values(p,s).Rank==15&&PaladinAuras.Effect(p,CombatAbility.Salvation)is{Rank:15},"Provided fixed immediate Salvation differs");
        var first=PaladinAuras.Effect(p,CombatAbility.Salvation);Pulse(p,s,due.AddMilliseconds(1999));Require(PaladinAuras.Effect(p,CombatAbility.Salvation)==first,"Aura refreshed before source period");Pulse(p,s,due.AddSeconds(2));
        var lease=PaladinAuras.Effect(p,CombatAbility.Salvation);int percent=(int)ConsumeAuras.Values(p,s).Eval("aurastatcalc1");
        Require(lease is{Rank:15}&&lease.Percent==percent&&lease.ProviderOwner==p.Serial.Value&&lease.ProviderOrder==s.Order
            &&PaladinAuras.Effect(ally,CombatAbility.Salvation)?.Percent==percent&&PaladinResistance.Bonus(ally,ResistanceType.Fire)==percent
            &&NecromancerEffects.ManaBalance256(p)==0&&PaladinAuras.Selected(p)==selected,"Salvation owner/allied values,mana or independent emitter failed");
        ConsumeEffects.Clear(p);PaladinAuras.Clear(p);PaladinAuras.Clear(ally);var rate=GearEffects.RegenerationRate(p,TimeSpan.FromSeconds(1),"mana-per-five");
        s=Put(p,"Baal Subject 5");SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-513);SkillHealth.SetBalance256(ally,SkillHealth.Maximum256(ally)-257);
        int life=SkillHealth.Balance256(p),other=SkillHealth.Balance256(ally);Require(ConsumeAuras.Values(p,s).Eval("aurastatcalc2")==0,"Unlearned Prayer contributed healing");
        Pulse(p,s,Due(p,s).AddMilliseconds(-1));Require(PaladinAuras.Effect(p,CombatAbility.Meditation)==null,"Meditation emitted before source period");Pulse(p,s,Due(p,s));
        Require(PaladinAuras.ManaRecoveryPercent(p)==650&&PaladinAuras.ManaRecoveryPercent(ally)==650
            &&GearEffects.RegenerationRate(p,TimeSpan.FromSeconds(1),"mana-per-five").Ticks==1333333
            &&rate==TimeSpan.FromSeconds(1)&&SkillHealth.Balance256(p)==life&&SkillHealth.Balance256(ally)==other
            &&NecromancerEffects.ManaBalance256(p)==0&&!PaladinAuras.ManaSuppressed(p),"Meditation fixed15/650percent recovery/no Prayer heal/no mana cost failed");
        ConsumeEffects.Clear(p);PaladinAuras.Clear(p);PaladinAuras.Clear(ally);
        foreach(var sample in new[]{("The Smith",21),("The Feature Creep",25)})
        {
            s=Put(p,sample.Item1);var v=ConsumeAuras.Values(p,s);Require(v.Rank==sample.Item2,"Captured HolyFire rank raised by owner general skills");int before=SkillHealth.Balance256(enemy);
            Pulse(p,s,Due(p,s));var aura=PaladinAuras.Effect(p,CombatAbility.HolyFire);var range=PaladinElements.WeaponRange(p,CombatAbility.HolyFire,v);
            double radius=SkillGeometry.Tiles(v.Eval("aurarangecalc"));int expected=(int)Math.Floor(NamedElements.Enhance(p,v.Damage256(false),"fire")*PaladinElements.Proximity(p.GetDistanceToSqrt(enemy),radius));
            Require(aura!=null&&aura.Rank==sample.Item2&&aura.WeaponMin==range.Min&&aura.WeaponMax==range.Max&&before-SkillHealth.Balance256(enemy)==expected,"Actual provided HolyFire pulse/weapon values differ from existing Paladin mechanics");
            before=SkillHealth.Balance256(enemy);Pulse(p,s,Due(p,s));Require(SkillHealth.Balance256(enemy)==before,"Same-frame aura pulse replayed");
            var tick=Due(p,s).AddSeconds(20);Pulse(p,s,tick);Require(before-SkillHealth.Balance256(enemy)==expected,"Lag catch-up emitted more than one pulse");
            var hook=ClassCombatHooks.SkillResistedDamage;ClassSkillDamage? packet=null;
            ClassCombatHooks.SkillResistedDamage=(to,from,amount,physical,fire,cold,poison,lightning,spell,weapon,ranged)=>
            {var result=hook(to,from,amount,physical,fire,cold,poison,lightning,spell,weapon,ranged);if(from==p&&to==enemy){packet=result;}return result;};
            try{before=SkillHealth.Balance256(enemy);AOS.Damage(enemy,p,10,false,100,0,0,0,0,weaponDamage:true);}finally{ClassCombatHooks.SkillResistedDamage=hook;}
            Require(packet is{} value&&before-SkillHealth.Balance256(enemy)==value.Total256&&value.Total256-value.Physical256>=range.Min*256&&value.Total256-value.Physical256<=range.Max*256,"Actual native weapon packet lost provided HolyFire");
            enemy.SetResistance(ResistanceType.Fire,200);before=SkillHealth.Balance256(enemy);Pulse(p,s,tick.AddSeconds(4));Require(SkillHealth.Balance256(enemy)==before,"HolyFire bypassed unbroken fire immunity");enemy.SetResistance(ResistanceType.Fire,0);
            ConsumeAuras.Stop(p,s.Order);Require(PaladinAuras.Effect(p,CombatAbility.HolyFire)==null,"Provider stop left own HolyFire weapon buff");
            ConsumeEffects.Clear(p);PaladinAuras.Clear(p);
        }
        Log.Information("Consume aura:all four recipes share fixed15Salvation/fixed15Meditation650/no Prayer healing/HolyFire21and25 existing damage/range/weapon math;zero mana,independent selection,period gate/no replay/no lag bursts,actual native weapon fire and immunity pass");
    }
    private static void Scene(NetState network)
    {
        var p=Player(DiabloClass.Warlock);ClassSkillChecks.Train(p,"warlock.consume",19);FixtureSpace.Corridor(p,7100,34,3300);var account=FixtureNetwork.Authenticate(network,p,"consume-aura-");
        var ally=Player(DiabloClass.Warlock);ally.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);var paladin=Player(DiabloClass.Paladin);paladin.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);ClassSkillChecks.Train(paladin,"paladin.salvation",15);
        var enemy=new GoatmanNpcContactChecks.Recipient{Frozen=true,CantWalk=true,NoKillAwards=true};enemy.SetHits(50000);enemy.Hits=enemy.HitsMax;enemy.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);enemy.AIObject?.AITimer.Stop();enemy.SetResistance(ResistanceType.Fire,0);enemy.SetResistance(ResistanceType.Physical,0);
        var roll=PaladinElements.Roll;PaladinElements.Roll=_=>0;bool pending=false;
        void Finish(bool keep)
        {PaladinElements.Roll=roll;PaladinAuras.Clear(paladin);paladin.Delete();network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();if(!keep){ConsumeEffects.Clear(p);PaladinAuras.Clear(p);PaladinAuras.Clear(ally);p.Delete();ally.Delete();enemy.Delete();}}
        try
        {
            PoisonDaggerChecks.Named(p,"Tarnhelm");Basic(p,ally,enemy);var state=Put(p,"megademon6");Pulse(p,state,Due(p,state));var original=PaladinAuras.Snapshot();
            var strong=new PaladinAuraLease(p.Serial.Value,paladin.Serial.Value,CombatAbility.Salvation,20,108,TimedEffects.Now.AddSeconds(5));
            PaladinAuras.Restore(original with{Leases=original.Leases.Where(l=>l.Target!=p.Serial.Value||l.Ability!=CombatAbility.Salvation).Append(strong).ToArray()});
            PaladinAuras.PulseProvided(p,CombatAbility.Salvation,ConsumeAuras.Values(p,state),TimedEffects.Now,state.UntilUtc,state.Order);
            ConsumeEffects.Clear(p);Require(PaladinAuras.Effect(p,CombatAbility.Salvation)==strong,"Weaker provider/cleanup erased stronger ordinary aura");PaladinAuras.Clear(p);PaladinAuras.Clear(ally);
            state=Put(p,"megademon6");Pulse(p,state,Due(p,state));Require(PaladinCombat.Begin(paladin,CombatAbility.Salvation),"Normal selected Paladin setup failed");
            var snapshot=PaladinAuras.Snapshot();PaladinAuras.Restore(snapshot with{Emitters=snapshot.Emitters.Select(e=>e.Owner==paladin.Serial.Value?e with{NextUtc=TimedEffects.Now}:e).ToArray()});
            pending=true;Timer.StartTimer(TimeSpan.FromMilliseconds(120),()=>Core.LoopContext.Post(()=>
            {
                bool kept=false;
                try
                {
                    var fresh=PaladinAuras.Effect(p,CombatAbility.Salvation);Require(fresh is{Rank:15,ProviderOrder:0},"Normal equal-rank selected pulse did not take provider ownership");
                    ConsumeEffects.Clear(p);Require(PaladinAuras.Effect(p,CombatAbility.Salvation)==fresh,"Provider cleanup removed refreshed ordinary equal aura");PaladinAuras.Clear(p);PaladinAuras.Clear(ally);PaladinAuras.Clear(paladin);
                    var saved=Put(p,"Baal Subject 5",SummonDifficulties.For(p.Map));Pulse(p,saved,Due(p,saved));var lease=PaladinAuras.Effect(ally,CombatAbility.Meditation);Require(lease?.ProviderOrder==saved.Order&&lease.ProviderOwner==p.Serial.Value,"Saved allied provider provenance missing");
                    var text=JsonSerializer.Serialize(lease);var node=JsonNode.Parse(text).AsObject();node.Remove("ProviderOwner");node.Remove("ProviderOrder");var old=node.Deserialize<PaladinAuraLease>();Require(old.ProviderOwner==0&&old.ProviderOrder==0,"Old aura JSON defaults broke");
                    var intact=PaladinAuras.Snapshot();bool bad=false;try{PaladinAuras.Restore(intact with{Leases=intact.Leases.Append(lease with{Target=paladin.Serial.Value,ProviderOrder=-1}).ToArray()});}catch(InvalidDataException){bad=true;}Require(bad&&PaladinAuras.Snapshot().Leases.SequenceEqual(intact.Leases),"Invalid provider metadata mutated aura state");
                    SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-513);SkillHealth.SetBalance256(ally,SkillHealth.Maximum256(ally)-257);
                    File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,ally.Serial.Value,enemy.Serial.Value,saved,SkillHealth.Balance256(p),SkillHealth.Balance256(ally))));kept=true;
                    Log.Information("Consume aura:stronger/equal ordinary selected aura ownership survives provider cleanup;optional old JSON/atomic invalid provenance controls pass;actual allied provided lease saved under existing aura profile");Log.Information("consume-aura-check-result: passed");
                }
                catch(Exception e){Log.Error(e,"consume-aura-check-result: failed");}
                finally{Finish(kept);}
            }));
        }
        finally{if(!pending){Finish(false);}}
    }
    private static void BeforeSave(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var ally=(PlayerMobile)World.FindMobile((Serial)s.Ally);
        var account=FixtureNetwork.Authenticate(network,p,"consume-aura-save-");
        try
        {
            ally.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);ConsumeAuras.Stop(p,s.State.Order);ConsumeAuras.Tick(p,s.State,TimedEffects.Now);
            var lease=PaladinAuras.Effect(ally,CombatAbility.Meditation);
            Require(ConsumeEffects.State(p)==s.State&&lease?.ProviderOrder==s.State.Order&&lease.ProviderOwner==s.Owner
                &&lease.UntilUtc>TimedEffects.Now&&SkillHealth.Balance256(p)==s.Life&&SkillHealth.Balance256(ally)==s.AllyLife,"Pre-save pulse changed saved buff/deadline/wounds or failed real recipient lease");
            ally.Internalize();Log.Information("consume-aura-save-check: passed");
        }
        catch(Exception e){Log.Error(e,"consume-aura-save-check: failed");}
        finally{network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(s.Generation<2,"Too many Consume aura reloads");var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var ally=(PlayerMobile)World.FindMobile((Serial)s.Ally);
        string providers=string.Join(';',Loaded.Where(l=>l.ProviderOrder>0).Select(l=>l.Target+"/"+l.ProviderOwner+"/"+l.ProviderOrder+"/"+l.Ability));
        Require(ConsumeEffects.State(p)==s.State&&SkillHealth.Balance256(p)==s.Life&&SkillHealth.Balance256(ally)==s.AllyLife
            &&Loaded.Any(l=>l.Target==s.Ally&&l.Ability==CombatAbility.Meditation&&l.ProviderOwner==s.Owner&&l.ProviderOrder==s.State.Order),
            $"Saved aura:state{ConsumeEffects.State(p)==s.State},ownerHP{SkillHealth.Balance256(p)}/{s.Life},allyHP{SkillHealth.Balance256(ally)}/{s.AllyLife},loaded{Loaded.Length},providers[{providers}],expected{s.Ally}/{s.Owner}/{s.State.Order}");
        var account=FixtureNetwork.Authenticate(network,p,"consume-aura-reload-");
        try
        {
            ally.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);
            NecromancerEffects.SetMana256(p,0);ConsumeAuras.Tick(p,s.State,TimedEffects.Now);Require(PaladinAuras.ManaRecoveryPercent(p)==550&&PaladinAuras.ManaRecoveryPercent(ally)==550&&NecromancerEffects.ManaBalance256(p)==0,
                $"Resume aura:self{PaladinAuras.ManaRecoveryPercent(p)},ally{PaladinAuras.ManaRecoveryPercent(ally)},mana{NecromancerEffects.ManaBalance256(p)},rank{s.State.Suppliers.Bonus.AuraRank},current{ConsumeEffects.State(p)==s.State},selfLease{JsonSerializer.Serialize(PaladinAuras.Effect(p,CombatAbility.Meditation))},allyLease{JsonSerializer.Serialize(PaladinAuras.Effect(ally,CombatAbility.Meditation))}");
            var own=PaladinAuras.Effect(p,CombatAbility.Meditation);ConsumeAuras.Stop(p,s.State.Order);Require(PaladinAuras.Effect(p,CombatAbility.Meditation)==null,"Offline/source-stop did not remove own provider");
            ConsumeAuras.Tick(p,s.State,TimedEffects.Now);Require(PaladinAuras.Effect(p,CombatAbility.Meditation)!=null&&ConsumeEffects.State(p).UntilUtc==s.State.UntilUtc,"Resume reset buff deadline");
            ConsumeAuras.Tick(p,s.State,s.State.UntilUtc);Require(PaladinAuras.Effect(p,CombatAbility.Meditation)==null,"Expired Consume source kept own aura");
            ConsumeAuras.Tick(p,s.State,TimedEffects.Now);File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1,Life=SkillHealth.Balance256(p),AllyLife=SkillHealth.Balance256(ally)}));
            Log.Information("Consume aura:actual saved provider tags parse through unchanged aura profile;fixed11NormalMeditation550resumes once at zero mana with original deadline,source stop/expiry clears own effect without resetting state");
        }
        finally{network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
    }
}
