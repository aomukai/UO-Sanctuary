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

public static class EngorgeWorldChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(EngorgeWorldChecks));
    private sealed record Pet(uint Serial,string Birth,int Life,NpcEquipmentSeed Random,EngorgeEffect Effect);
    private sealed record Saved(int Generation,uint Owner,Pet[] Pets);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"engorge-world-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static string Birth(SkeletonGuardian b)=>JsonSerializer.Serialize(b switch
    {WarlockGoatmanBody g=>(object)g.Prepared.Snapshot,WarlockTaintedBody t=>t.Prepared.Snapshot,WarlockDefilerBody d=>d.Prepared.Snapshot,_=>throw new NotSupportedException()});
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"engorge-world-reload-check":"engorge-world-check";
            ConsoleInputHandler.RegisterCommand(name,"Prepared complete Engorge corpse,recipient,combat and original save checks",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>
            {try{if(reload){Reload(n);}else{Scene(n);}Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});}));}
    }
    private static Corpse Corpse(PlayerMobile p,int offset=12)=>DeathSentryExplosionChecks.Body(p,new(p.X+offset,p.Y,p.Z));
    private static Pet SavedPet(SkeletonGuardian b)=>new(b.Serial.Value,Birth(b),SkillHealth.Balance256(b),NpcEquipmentRandom.State(b),EngorgeEffects.Saved(b));
    private static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,p.ManaMax*256);}
    private static void Feed(PlayerMobile p,SkeletonGuardian[] bodies)
    {
        var corpse=Corpse(p);var loot=corpse.Items.Select(i=>i.Serial).ToArray();var capture=EngorgePrepared.Capture(p);
        var before=bodies.Select(SkillHealth.Balance256).ToArray();int mana=NecromancerEffects.ManaBalance256(p);var at=TimedEffects.Now;
        var released=EngorgeCasting.ReleasePrepared(p,corpse,at);
        Require(released==new EngorgeRelease(true,bodies.Length,capture.Mana256)&&NecromancerEffects.ManaBalance256(p)==mana-capture.Mana256,"Corpse once-only payment/recipient count");
        Require(corpse.SanctuaryConsumed&&corpse.Animated&&!corpse.Deleted&&loot.All(serial=>corpse.Items.Any(i=>i.Serial==serial)),"Engorge lost protected corpse loot");
        for(int i=0;i<bodies.Length;i++)
        {var b=bodies[i];Require(SkillHealth.Balance256(b)==before[i]+EngorgePrepared.Healing256(capture,SkillHealth.Maximum256(b),before[i])
            &&EngorgeEffects.Saved(b)==new EngorgeEffect(p.Serial.Value,at,at.AddMilliseconds(40L*capture.DurationFrames),capture),"Healing or saved payload differs from released capture");}
        int paid=NecromancerEffects.ManaBalance256(p);Require(EngorgeCasting.ReleasePrepared(p,corpse,at)==new EngorgeRelease(false,0,0)&&NecromancerEffects.ManaBalance256(p)==paid,"Consumed corpse replayed");
    }
    private static void Scene(NetState network)
    {
        var p=ClassSkillChecks.Player(DiabloClass.Warlock);p.Name="EngorgeWorldFixture";p.Female=true;p.AddItem(new Backpack());
        ClassSkillChecks.Train(p,"warlock.engorge",19);ClassSkillChecks.Train(p,"warlock.bloodoath",7);ClassSkillChecks.Train(p,"warlock.demonicmastery",10);
        foreach(var key in new[]{"warlock.summongoatman","warlock.summontainted","warlock.summondefiler"}){ClassSkillChecks.Train(p,key,6);}
        FixtureSpace.Corridor(p,6900,34,3300);var account=FixtureNetwork.Authenticate(network,p,"engorge-world-");SkeletonGuardian[] bodies=[];bool kept=false;
        try
        {
            Require(!EngorgeEffects.Approved&&!WarlockCombat.Implemented(CombatAbility.Engorge),"Unapproved Engorge activated");
            var first=Corpse(p);int mana=NecromancerEffects.ManaBalance256(p);
            Require(EngorgeCasting.ReleasePrepared(p,first,TimedEffects.Now)==new EngorgeRelease(false,0,0)&&!first.SanctuaryConsumed&&NecromancerEffects.ManaBalance256(p)==mana,"No-demon cast spent corpse/mana");
            bodies=new[]{CombatAbility.SummonGoatman,CombatAbility.SummonTainted,CombatAbility.SummonDefiler}
                .Select((ability,i)=>WarlockGoatmanWorld.Create(p,new(p.X+2+i*2,p.Y,p.Z),TimedEffects.Now,ability)).ToArray();
            foreach(var b in bodies){WarlockGoatmanWorld.Admit(b);Require(EngorgeEffects.Saved(b)==null,"Fresh body inherited an Engorge state");}
            var births=bodies.Select(Birth).ToArray();var armor=bodies.Select(CreatureProfiles.Defense).ToArray();var speed=bodies.Select(SourceAttackRate.WolfRate).ToArray();
            var resistance=bodies.Select(b=>NamedElements.Resistance(p,b,"physical",b.PhysicalResistance)).ToArray();
            NecromancerEffects.SetMana256(p,1);Require(!EngorgeCasting.ReleasePrepared(p,first,TimedEffects.Now).Consumed&&!first.SanctuaryConsumed,"Low mana consumed a corpse");Ready(p);
            Require(EngorgeCasting.Acquire(p,new(first.X,first.Y+1,first.Z))==first&&EngorgeCasting.Acquire(p,new(first.X+5,first.Y,first.Z))==null,"Bounded corpse ground targeting");
            var cancel=EngorgeCasting.BeginPrepared(p,first.Serial,first.Location,Core.TickCount);Require(cancel!=null,"Prepared cast did not enter shared clock");mana=NecromancerEffects.ManaBalance256(p);
            ClassCombat.Stop(p);Require(cancel.Cancelled&&!EngorgeCasting.ResolvePrepared(p,Core.TickCount+10000)&&!first.SanctuaryConsumed&&NecromancerEffects.ManaBalance256(p)==mana,"Cancelled windup committed effects");
            Ready(p);var moved=EngorgeCasting.BeginPrepared(p,first.Serial,first.Location,Core.TickCount);Require(moved!=null,"Movement cancellation setup");
            var origin=p.Location;p.Location=new(p.X,p.Y+1,p.Z);p.Location=origin;
            Require(moved.Cancelled&&ClassCombat.For(p).Engorge==null&&!first.SanctuaryConsumed,"Move-away-and-back resurrected a pending cast");
            Ready(p);var gone=EngorgeCasting.BeginPrepared(p,first.Serial,first.Location,Core.TickCount);Require(gone!=null,"Late corpse-loss setup");
            Require(CorpseSkillRules.Reserve(first),"Competing corpse reservation");NecromancerCombat.Consume(first);mana=NecromancerEffects.ManaBalance256(p);
            Require(!ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt)&&gone.Released&&gone.Result is{Consumed:false}&&NecromancerEffects.ManaBalance256(p)==mana,"Late corpse loss paid or replayed");
            foreach(var b in bodies){SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)/5);}
            Feed(p,bodies);
            var at=TimedEffects.Now;
            for(int i=0;i<bodies.Length;i++)
            {
                var b=bodies[i];Require(CreatureProfiles.Defense(b)==armor[i]+175&&SourceAttackRate.WolfRate(b)==speed[i]+33
                    &&NamedElements.Resistance(p,b,"physical",b.PhysicalResistance)==resistance[i]+5&&Birth(b)==births[i],"Shared flat armor/IAS/resistance or birth mutation");
                var effect=EngorgeEffects.Saved(b);Require(EngorgeEffects.At(b,effect.UntilUtc)==null&&EngorgeEffects.At(b,effect.UntilUtc.AddTicks(-1))==effect,"Exclusive state expiry boundary");
                Require(EngorgeEffects.Recovery256(b,effect.AppliedUtc.AddMilliseconds(-40),3)==70
                    &&EngorgeEffects.Recovery256(b,effect.UntilUtc.AddMilliseconds(-40),3)==35,"Recovery interval crossed applied/expiry boundaries");
                int before=SkillHealth.Balance256(b),basis=BattleOrders.Regeneration256(b);DemonNativeState.Recover(b,at);
                Require(SkillHealth.Balance256(b)==Math.Min(SkillHealth.Maximum256(b),before+basis+35),"Existing recovery did not add raw35once per active source frame");
            }
            var helm=PoisonDaggerChecks.Named(p,"Tarnhelm");ClassGear.Refresh(p);Feed(p,bodies);
            for(int i=0;i<bodies.Length;i++){Require(SourceAttackRate.WolfRate(bodies[i])==speed[i]+34&&CreatureProfiles.Defense(bodies[i])==armor[i]+175&&EngorgeEffects.Saved(bodies[i]).Suppliers.Rank==20,"Refresh stacked instead of replacing captured bonuses");}
            p.AddToBackpack(helm);ClassGear.Refresh(p);ClassSkillChecks.Train(p,"warlock.bloodoath",8);
            Require(EngorgePrepared.Capture(p).FlatArmor==200&&bodies.All(b=>EngorgeEffects.Saved(b).Suppliers.FlatArmor==175),"Live training rewrote stored buff suppliers");
            p.EquipItem(helm);ClassGear.Refresh(p);
            Leech(p,(WarlockGoatmanBody)bodies[0]);
            var original=EngorgeEffects.Saved(bodies[0]);string intact=((WarlockGoatmanBody)bodies[0]).SavedText;bool refused=false;
            try{EngorgeEffects.Store(bodies[0],original with{Owner=1});}catch(InvalidDataException){refused=true;}
            Require(refused&&((WarlockGoatmanBody)bodies[0]).SavedText==intact,"Malformed state restore was not atomic");
            var expired=original with{AppliedUtc=at.AddMinutes(-5),UntilUtc=at.AddMinutes(-5).AddMilliseconds(40L*original.Suppliers.DurationFrames)};
            EngorgeEffects.Store(bodies[0],expired);Require(SourceAttackRate.WolfRate(bodies[0])==speed[0]&&CreatureProfiles.Defense(bodies[0])==armor[0]
                &&NamedElements.Resistance(p,bodies[0],"physical",bodies[0].PhysicalResistance)==resistance[0],"Expired state retained live bonuses");EngorgeEffects.Store(bodies[0],original);
            var fresh=Corpse(p);Ready(p);var pending=EngorgeCasting.BeginPrepared(p,Serial.Zero,new(fresh.X,fresh.Y+1,fresh.Z),Core.TickCount);Require(pending!=null,"Ground cast startup failed");
            mana=NecromancerEffects.ManaBalance256(p);var s=ClassCombat.For(p);Require(!ClassCombat.ResolveAction(p,s.EffectAt-1)&&!fresh.SanctuaryConsumed&&NecromancerEffects.ManaBalance256(p)==mana,"Early cast paid/released");
            Require(ClassCombat.ResolveAction(p,s.EffectAt)&&pending.Released&&pending.Result is{Consumed:true,Recipients:3,ManaPaid256:1984},"Shared timed release failed");
            Require(!ClassCombat.ResolveAction(p,s.EffectAt+1)&&NecromancerEffects.ManaBalance256(p)==mana-1984,"Timed action replayed");
            foreach(var b in bodies){Require(Birth(b)==births[Array.IndexOf(bodies,b)],"Buff action recaptured summon birth");SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)-513);}
            PersistentMinions.Suspend(p);File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,bodies.Select(SavedPet).ToArray())));kept=true;
            Log.Information("Prepared Engorge:corpse loot/reservation,shared timed payment/cancel/late loss,three original recipients,refresh/expiry,raw source recovery,real Witch gear and atomic state restore pass");
        }
        finally{ClassCombat.Stop(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();if(!kept){foreach(var b in bodies){b.Delete();}p.Delete();}}
    }
    private static void Leech(PlayerMobile p,WarlockGoatmanBody body)
    {
        var target=new GoatmanNpcContactChecks.Recipient{Frozen=true,CantWalk=true,NoKillAwards=true};target.SetHits(50000);target.Hits=target.HitsMax;
        target.MoveToWorld(new(body.X+1,body.Y,body.Z),body.Map);target.AIObject?.AITimer.Stop();target.SetResistance(ResistanceType.Physical,0);
        try
        {
            var at=TimedEffects.Now;int missing=Math.Min(20000,SkillHealth.Maximum256(body)/2);SkillHealth.SetBalance256(body,SkillHealth.Maximum256(body)-missing);
            int own=SkillHealth.Balance256(p),before=SkillHealth.Balance256(body);var prepared=GoatmanNpcContact.Prepare(body,p,target,417,at,_=>0,()=>99);var packet=prepared.Impact.Packet;
            Require((packet.ResultFlags&1)!=0&&packet.Physical256>0&&GemCombat.Drain(target)>0,"Leech successful physical/drain control");
            int divisor=MonsterVariants.Difficulty(target)switch{MonsterDifficulty.Nightmare=>2,MonsterDifficulty.Hell=>3,_=>1};
            int expected=Math.Min(missing,WeaponLeech.Calculate(packet.Physical256,SkillHealth.Balance256(target),0,EngorgeEffects.Saved(body).Suppliers.MinimumDrainStat,0,GemCombat.Drain(target),divisor,divisor).Life256);
            var result=GoatmanNpcContact.Release(body,p,target,417,prepared,at);
            Require(result.Commit!=null&&expected>0&&SkillHealth.Balance256(body)==before+expected&&SkillHealth.Balance256(p)==own,"Actual ordered melee did not leech to the demon only");
            before=SkillHealth.Balance256(body);Require(EngorgeEffects.Leech(body,target,packet with{Physical256=0},at)==0
                &&EngorgeEffects.Leech(body,target,packet with{ResultFlags=packet.ResultFlags|32},at)==0
                &&EngorgeEffects.Leech(body,target,packet with{ResultFlags=0},at)==0
                &&EngorgeEffects.Leech(body,target,packet,at,SkillDamageKind.Periodic)==0
                &&EngorgeEffects.Leech(body,target,packet,EngorgeEffects.Saved(body).UntilUtc)==0&&SkillHealth.Balance256(body)==before,"Elemental/flagged/expired leech control");
            bool rawRefused=false;try{EngorgeEffects.Leech(body,target,packet with{LifeDrain256=1},at);}catch(NotSupportedException){rawRefused=true;}
            Require(rawRefused&&SkillHealth.Balance256(body)==before,"Positive raw NPC drain was silently combined with percentage leech");
            Log.Information("Prepared Engorge:actual Goatman ordered melee uses percentage WeaponLeech calculation,drain/difficulty and clipped physical damage;owner not healed;excluded contacts give zero");
        }
        finally{target.Delete();}
    }
    private static void Reload(NetState network)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Generation<2,"Too many Engorge reloads");var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        var bodies=saved.Pets.Select(x=>(SkeletonGuardian)World.FindMobile((Serial)x.Serial)).ToArray();
        for(int i=0;i<bodies.Length;i++)
        {var b=bodies[i];var expected=saved.Pets[i];Require(b.Map==Map.Internal&&Birth(b)==expected.Birth&&SkillHealth.Balance256(b)==expected.Life
            &&NpcEquipmentRandom.State(b)==expected.Random&&EngorgeEffects.Saved(b)==expected.Effect&&DemonNativeState.Controller(b)is{Fault:null},"Original saved body/buff/wound/RNG/host changed or recovered offline");}
        var account=FixtureNetwork.Authenticate(network,p,"engorge-world-reload-");
        try
        {
            PersistentMinions.Resume(p);WarlockGoatmanWorld.Tick(TimedEffects.Now);
            foreach(var b in bodies){EngorgeEffects.Validate(EngorgeEffects.Saved(b),p.Serial.Value);Require(Birth(b)==saved.Pets[Array.IndexOf(bodies,b)].Birth,"Login recaptured original summon birth");}
            NecromancerEffects.SetMana256(p,p.ManaMax*256);Feed(p,bodies);PersistentMinions.Suspend(p);
            File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1,Pets=bodies.Select(SavedPet).ToArray()}));
            Log.Information("Prepared Engorge:original optional body buffs,expiry,wounds,birth,RNG/hosts survive reload without offline recovery;actual roster can feed again after login");
        }
        finally{network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
    }
}
