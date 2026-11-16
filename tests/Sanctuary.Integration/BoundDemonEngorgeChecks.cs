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

public static class BoundDemonEngorgeChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(BoundDemonEngorgeChecks));
    private sealed record Saved(int Generation,uint Owner,uint Bound,uint Weapon,BoundDemonState State,int Life,int Mana,int Stamina,uint Goat,string Birth,int GoatLife,EngorgeEffect GoatEffect);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"bound-demon-engorge-fixtures.json");
    private static bool Creating;
    private static void Rates(){var old=Mobile.HitsRegenRateHandler;Mobile.HitsRegenRateHandler=m=>Creating||m.Name is"BoundDemonEngorgeFixture"or"BoundDemonEngorgeTarget"?TimeSpan.FromHours(1):old?.Invoke(m)??Mobile.DefaultHitsRate;}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Rates();foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"bound-demon-engorge-reload-check":"bound-demon-engorge-check";
            ConsoleInputHandler.RegisterCommand(name,"Bound original Engorge corpse/stat/recovery/leech/persistence checks",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>
            {try{if(reload){Reload(n);}else{Scene(n);}Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});}));}
    }
    private static void Ready(PlayerMobile p){ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,p.ManaMax*256);}
    private static void Feed(PlayerMobile p,Daemon body,WarlockGoatmanBody goat)
    {
        Ready(p);var capture=EngorgePrepared.Capture(p);Require(capture is{Rank:20,HardBloodOath:7,HealPercent:49,FlatArmor:175,RegenerationStat:35,AttackSpeed:34},"Literal Engorge capture differs");
        var corpse=DeathSentryExplosionChecks.Body(p,new(p.X+12,p.Y,p.Z));var loot=corpse.Items.Select(i=>i.Serial).ToArray();int a=SkillHealth.Balance256(body),b=SkillHealth.Balance256(goat),mana=NecromancerEffects.ManaBalance256(p);
        var result=EngorgeCasting.ReleasePrepared(p,corpse,TimedEffects.Now);
        Require(result==new EngorgeRelease(true,2,capture.Mana256)&&NecromancerEffects.ManaBalance256(p)==mana-capture.Mana256
            &&SkillHealth.Balance256(body)==a+EngorgePrepared.Healing256(capture,SkillHealth.Maximum256(body),a)
            &&SkillHealth.Balance256(goat)==b+EngorgePrepared.Healing256(capture,SkillHealth.Maximum256(goat),b),"Mixed corpse release/healing/payment failed");
        Require(corpse.SanctuaryConsumed&&!corpse.Deleted&&loot.All(id=>corpse.Items.Any(i=>i.Serial==id))&&EngorgeEffects.Saved(body)==EngorgeEffects.Saved(goat),"Corpse loot or shared captured effect changed");
    }
    private static GoatmanNpcContactChecks.Recipient Target(PlayerMobile p,BaseCreature center)
    {
        var t=new GoatmanNpcContactChecks.Recipient{Name="BoundDemonEngorgeTarget",Frozen=true,CantWalk=true,NoKillAwards=true};t.SetHits(50000);t.Hits=t.HitsMax;
        t.SetResistance(ResistanceType.Physical,0);t.MoveToWorld(new(center.X-1,center.Y,center.Z),p.Map);t.AIObject?.AITimer.Stop();return t;
    }
    private static void Contact(PlayerMobile p,Daemon body,GoatmanNpcContactChecks.Recipient target,bool clipped=false)
    {
        SkillHealth.SetBalance256(body,SkillHealth.Maximum256(body)/3);if(clipped){SkillHealth.SetBalance256(target,256);}
        int before=SkillHealth.Balance256(body),victim=SkillHealth.Balance256(target),owner=SkillHealth.Balance256(p),calls=0,physical=0;
        var hook=ClassCombatHooks.PreparedWeaponDamage;ClassCombatHooks.PreparedWeaponDamage=(from,to,amount,ranged)=>
        {if(from==body&&to==target){calls++;physical=amount;Require(!ranged,"Dagger became ranged");}hook?.Invoke(from,to,amount,ranged);};
        try{((Dagger)body.FindItemOnLayer(Layer.OneHanded)).OnHit(body,target);}finally{ClassCombatHooks.PreparedWeaponDamage=hook;}
        int divisor=MonsterVariants.Difficulty(target)switch{MonsterDifficulty.Nightmare=>2,MonsterDifficulty.Hell=>3,_=>1};
        int gain=WeaponLeech.Calculate(Math.Min(physical,victim),victim,0,EngorgeEffects.Saved(body).Suppliers.MinimumDrainStat,0,GemCombat.Drain(target),divisor,divisor).Life256;
        Require(calls==1&&physical>0&&gain>0&&SkillHealth.Balance256(body)==before+gain&&SkillHealth.Balance256(p)==owner,"Actual native Dagger fractional leech duplicated/lost healing or healed owner");
        if(clipped){Require(gain<256&&(target.Deleted||!target.Alive),"Lethal native contact ignored target-life clip");}
    }
    private static void Recovery(PlayerMobile p,Daemon body)
    {
        var effect=EngorgeEffects.Saved(body);var at=TimedEffects.Now;EngorgeEffects.Store(body,effect);
        int before=SkillHealth.Balance256(body);BoundDemonHosts.Tick();
        Require(SkillHealth.Balance256(body)==before+35&&EngorgeEffects.RecoverNative(body,at)==0&&EngorgeEffects.RecoverNative(body,at.AddMilliseconds(80))==70,"Bound lifecycle frame recovery lost35raw stat or replayed frames");
        int life=SkillHealth.Balance256(body);PersistentMinions.Suspend(p);Require(EngorgeEffects.RecoverNative(body,at.AddHours(1))==0&&SkillHealth.Balance256(body)==life,"Parking recovered/backpaid wounds");
        PersistentMinions.Resume(p);Require(EngorgeEffects.RecoverNative(body,at.AddMilliseconds(80))==35,"Resume backpaid suspended source frames");
        DateTime next=at.AddMilliseconds(120);long frames=(effect.UntilUtc-next).Ticks/SourceFrameTimeline.IntervalTicks+1;
        int expected=Math.Min(SkillHealth.Maximum256(body)-SkillHealth.Balance256(body),EngorgeEffects.Recovery256(body,next,frames));
        Require(EngorgeEffects.RecoverNative(body,effect.UntilUtc)==expected&&EngorgeEffects.RecoverNative(body,effect.UntilUtc.AddSeconds(1))==0,"Expiry granted extra source frames");
        EngorgeEffects.Store(body,effect);
    }
    private static void Scene(NetState network)
    {
        PlayerMobile p;Daemon body;Creating=true;try{p=ClassSkillChecks.Player(DiabloClass.Warlock);body=new Daemon();}finally{Creating=false;}
        p.Name=body.Name="BoundDemonEngorgeFixture";p.Female=true;p.AddItem(new Backpack());
        foreach(var pair in new[]{("warlock.binddemon",1),("warlock.demonicmastery",10),("warlock.engorge",19),("warlock.bloodoath",7),("warlock.summongoatman",6)}){ClassSkillChecks.Train(p,pair.Item1,pair.Item2);}
        FixtureSpace.Corridor(p,7080,34,3300);var account=FixtureNetwork.Authenticate(network,p,"bound-engorge-");WarlockGoatmanBody goat=null;GoatmanNpcContactChecks.Recipient target=null;bool kept=false;
        try
        {
            var helm=PoisonDaggerChecks.Named(p,"Tarnhelm");body.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);body.AIObject?.AITimer.Stop();var dagger=new Dagger();body.AddItem(dagger);
            Require(BoundDemonHosts.BindPrepared(p,body,out var why),"Engorge original bind: "+why);body.AIObject?.AITimer.Stop();var birth=BoundDemonHosts.State(body).Birth;
            goat=WarlockGoatmanWorld.Create(p,new(p.X+4,p.Y,p.Z),TimedEffects.Now);WarlockGoatmanWorld.Admit(goat);
            SkillHealth.SetBalance256(body,SkillHealth.Maximum256(body)/10);SkillHealth.SetBalance256(goat,SkillHealth.Maximum256(goat)/10);
            int defense=CreatureProfiles.Defense(body),resist=NamedElements.Resistance(p,body,"physical",body.PhysicalResistance),rate=100+BoundDemonHosts.AttackSpeed(body);
            Feed(p,body,goat);var effect=EngorgeEffects.Saved(body);
            Require(CreatureProfiles.Defense(body)==defense+175&&NamedElements.Resistance(p,body,"physical",body.PhysicalResistance)==resist+effect.Suppliers.PhysicalResistance
                &&ClassGearHooks.AttackDelay(body,TimeSpan.FromSeconds(1)).Ticks==TimeSpan.TicksPerSecond*100/Math.Clamp(rate+34,15,175),"Actual bound armor/resistance/attack-delay consumers ignored buff");
            p.AddToBackpack(helm);ClassGear.Refresh(p);ClassSkillChecks.Train(p,"warlock.bloodoath",8);
            Require(EngorgeEffects.Saved(body)==effect&&BoundDemonHosts.State(body).Birth==birth,"Gear/training rewrote captured buff or pet birth");
            EngorgeEffects.ApplyPrepared(body,p,EngorgePrepared.Capture(p),TimedEffects.Now);Require(CreatureProfiles.Defense(body)==defense+200,"Refresh stacked armor instead of replacing");EngorgeEffects.Store(body,effect);
            var intact=BoundDemonHosts.State(body);bool refused=false;try{EngorgeEffects.Store(body,effect with{Owner=1});}catch(InvalidDataException){refused=true;}
            Require(refused&&BoundDemonHosts.State(body)==intact,"Invalid bound effect mutated saved host");
            string legacy=JsonSerializer.Serialize(intact with{Engorge=null});Require(!legacy.Contains("\"Engorge\"")&&JsonSerializer.Deserialize<BoundDemonState>(legacy).Engorge==null,"Old optional bound JSON no longer loads");
            target=Target(p,body);Contact(p,body,target);int before=SkillHealth.Balance256(body);
            AOS.Damage(target,body,100,false,100,0,0,0,0,archer:true,weaponDamage:true);
            AOS.Damage(target,body,100,false,100,0,0,0,0,spellDamage:true);
            target.SetResistance(ResistanceType.Physical,100);AOS.Damage(target,body,100,false,100,0,0,0,0,weaponDamage:true);target.SetResistance(ResistanceType.Physical,0);
            Require(SkillHealth.Balance256(body)==before&&EngorgeEffects.NativeLeech(body,target,256,false,effect.UntilUtc)==0
                &&EngorgeEffects.NativeLeech(body,goat,256,false,TimedEffects.Now)==0,"Ranged/nonweapon/immune/expired/friendly native contact leeched");
            target.Delete();target=Target(p,body);Contact(p,body,target,clipped:true);target=null;Recovery(p,body);
            Require(EngorgeEffects.Approved&&WarlockCombat.Implemented(CombatAbility.Engorge),"Approved bound Engorge route unavailable");
            SkillHealth.SetBalance256(body,SkillHealth.Maximum256(body)/2-17);body.Mana=Math.Max(0,body.ManaMax-3);body.Stam=Math.Max(0,body.StamMax-5);PersistentMinions.Suspend(p);
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,body.Serial.Value,dagger.Serial.Value,BoundDemonHosts.State(body),SkillHealth.Balance256(body),body.Mana,body.Stam,goat.Serial.Value,JsonSerializer.Serialize(goat.Prepared.Snapshot),SkillHealth.Balance256(goat),EngorgeEffects.Saved(goat))));kept=true;
            Log.Information("Bound Engorge:real mixed corpse heal/payment/protected loot,captured refresh/gear/birth,actual armor/resistance/native attack delay,optional old JSON/atomic invalid-effect refusal pass;actual Dagger leeches fractional clipped physical once with no owner/ranged/spell/immune/expired healing;35raw frame recovery/parking/no catch-up/expiry pass");
        }
        finally{target?.Delete();ClassCombat.Stop(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();if(!kept){goat?.Delete();body.Delete();p.Delete();BoundDemonHosts.Tick();}}
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(s.Generation<2,"Too many bound Engorge reloads");var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var body=World.FindMobile((Serial)s.Bound)as Daemon;var goat=World.FindMobile((Serial)s.Goat)as WarlockGoatmanBody;
        Require(body is{Alive:true,Deleted:false}&&body.Map==Map.Internal&&BoundDemonHosts.State(body)==s.State&&SkillHealth.Balance256(body)==s.Life&&body.Mana==s.Mana&&body.Stam==s.Stamina
            &&World.FindItem((Serial)s.Weapon)?.Parent==body&&goat!=null&&JsonSerializer.Serialize(goat.Prepared.Snapshot)==s.Birth&&SkillHealth.Balance256(goat)==s.GoatLife&&EngorgeEffects.Saved(goat)==s.GoatEffect,"Original mixed Engorge buffs/resources/items/birth changed or recovered offline");
        var account=FixtureNetwork.Authenticate(network,p,"bound-engorge-reload-");GoatmanNpcContactChecks.Recipient target=null;
        try
        {
            PersistentMinions.Resume(p);Require(EngorgeEffects.At(body,TimedEffects.Now)==s.State.Engorge,"Original bound buff lost deadline on reload");target=Target(p,body);Contact(p,body,target);
            PersistentMinions.Suspend(p);File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1,State=BoundDemonHosts.State(body),Life=SkillHealth.Balance256(body)}));
            Log.Information("Bound Engorge:same native Daemon/items/birth/wounds/resources and original mixed buff deadlines survive reload without recovery;actual native Dagger still leeches once");
        }
        finally{target?.Delete();ClassCombat.Stop(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
    }
}
