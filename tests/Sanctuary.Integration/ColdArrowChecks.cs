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

public static class ColdArrowChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ColdArrowChecks));
    private sealed class Maximum:Random{public override int Next(int max)=>Math.Max(0,max-1);public override int Next(int min,int max)=>Math.Max(min,max-1);}
    private sealed class Minimum:Random{public override int Next(int max)=>0;}
    private sealed record Saved(uint Owner,uint Target,uint Bow,uint Ammo,Point3D Origin,WeaponFlight Flight,int Health,int Mana,int Quantity,DateTime ChillUntil,SourceColdState Cold);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"cold-arrow.json");
    public static void Configure(){Register("cold-arrow-check",Check);Register("cold-arrow-reload-check",Reload);Register("cold-arrow-input-check",Input);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Cold Arrow source conversion, actual skill/gear/payment and saved projectile",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static PlayerMobile Fresh()=>MagicArrowChecks.Fresh("ColdArrowOwner");
    private static void Ready(PlayerMobile p)=>MagicArrowChecks.Ready(p);
    private static void Mana(PlayerMobile p)=>MagicArrowChecks.Mana(p);
    private static Point3D Aim(PlayerMobile p)=>MagicArrowChecks.Aim(p);
    private static void Train(PlayerMobile p,CombatAbility ability,int rank=1)
    {
        var row=SkillMechanics.For(ability);Require(TalentCatalog.TryGet(row.CharacterClass,row.TalentId,out var talent),"Talent");
        foreach(string id in talent.Prerequisites){Train(p,SkillMechanics.All.Single(x=>x.TalentId==id).Ability);}
        while(ClassCreation.Profiles.GetRank(p.Serial,row.TalentId)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,row.TalentId,out var why),"Train "+why);}
    }
    private static void Differential()
    {
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"cold-state-source-probe.json")));int count=0;
        var owner=Fresh();var target=Target(owner);try
        {
        foreach(var row in source.RootElement.GetProperty("state").EnumerateArray())
        {
            int type=row[0].GetInt32(),effect=type==1?row[2].GetInt32():-50,frames=row[3].GetInt32();bool old=row[4].GetInt32()!=0;
            TimedEffects.Clear(target);long tick=Core.TickCount;var now=TimedEffects.TimeAt(tick);
            if(old){var initial=TimedEffects.Actor(target,now);initial.ChillPercent=17;initial.ChillUntil=now.AddMilliseconds(200*40);initial.Cold=new(owner.Serial.Value);}
            bool valid=ColdStates.Commit(owner,target,frames,type==1,effect,new[]{1,2,4}[row[1].GetInt32()],tick);
            bool active=TimedEffects.TryActor(target,out var state);Require((active?1:0)==row[7].GetInt32(),"Compiled/native cold state creation");
            if(active)
            {
                Require(state.ChillUntil==now.AddMilliseconds((row[8].GetInt32()-1000)*40) && state.ChillPercent==-row[9].GetInt32()
                    && state.ChillPercent==-row[10].GetInt32() && state.ChillPercent==-row[11].GetInt32() && state.Cold.Source==owner.Serial.Value,"Compiled/native expiry/rate/owner retention");
            }
            // Raw source bit only. Its current corpse-destruction projection is
            // individually deferred, rather than inventing a20percent policy.
            Require(row[12].GetInt32()==(!valid || ColdStates.ShatterRoll(type==1,row[6].GetInt32()!=0,row[5].GetInt32())?1:0)
                && row[13].GetInt32()==(valid?1:0),"Compiled raw shatter bit/component RNG boundary");count++;
        }
        }finally{TimedEffects.Clear(target);target.Delete();owner.Delete();}
        Require(count==7200,"Incomplete source cold state fixture");
        using var creator=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"cold-arrow-creator-source-probe.json")));
        var fields=creator.RootElement.GetProperty("fields").EnumerateArray().Select(x=>x.GetString()).ToArray();count=0;
        foreach(var row in creator.RootElement.GetProperty("cases").EnumerateArray())
        {
            int lo=row[0].GetInt32(),added=row[1].GetInt32();int Expected(string key)=>row[6][Array.IndexOf(fields,key)].GetInt32();
            var d=BowLaunch.FromSource(lo*256,(lo+5)*256,0,0,128,row[4].GetInt32()+57,0,0,0,row[5].GetInt32()!=0,new(ColdMin:added+7*256,ColdMax:added+512+13*256,ColdFrames:row[2].GetInt32()+row[3].GetInt32()));
            Require(d.Min256==Expected("nMinDamage") && d.Max256==Expected("nMaxDamage") && d.Percent==Expected("nDamagePercent")
                && d.Elements.ColdMin==Expected("nColdMinDamage") && d.Elements.ColdMax==Expected("nColdMaxDamage") && d.Elements.ColdFrames==Expected("nColdLength"),"Complete cold creator/length differential");count++;
        }
        Require(count==360,"Incomplete cold creator fixture");Log.Information("Cold Arrow:7200 source cold-state rows and360 complete creator rows passed; raw shatter bit is component-only");
    }
    private static WeaponFlight Cast(PlayerMobile p,Mobile target=null,bool held=false,bool left=false)
    {
        Ready(p);int before=NecromancerEffects.ManaBalance256(p);var ammo=BowAmmunition.Stack(p,CombatTiming.Equipped(p));int quantity=ammo.Amount;var old=WeaponMissiles.Snapshot().Flights.Select(x=>x.Id).ToHashSet();
        bool start=held?(left?ClassCombat.ContinueAttack(p,CombatAbility.ColdArrow,target,target?.Location??Aim(p),Core.TickCount):ClassCombat.ContinueCast(p,CombatAbility.ColdArrow,target,target?.Location??Aim(p),Core.TickCount))
            :ClassCombat.Perform(p,CombatAbility.ColdArrow,target,target?.Location??Aim(p),Core.TickCount);
        Require(start && NecromancerEffects.ManaBalance256(p)==before-SkillMechanics.Values(p,CombatAbility.ColdArrow).Mana256 && ammo.Amount==quantity,"Cold Arrow start payment");var s=ClassCombat.For(p);ClassCombat.ReleaseCast(p);
        Require(!ClassCombat.ResolveAction(p,s.EffectAt-1) && ClassCombat.ResolveAction(p,s.EffectAt) && !ClassCombat.ResolveAction(p,s.EffectAt),"Single Cold Arrow event");
        var f=WeaponMissiles.Snapshot().Flights.Single(x=>!old.Contains(x.Id));Require(f.Ability==CombatAbility.ColdArrow && f.Missile=="coldarrow" && f.Frames==20 && f.NextDelay==0 && f.Damage.PhysicalToMagicPercent==0
            && f.Damage.Conversion==new WeaponElementConversion("cold",Math.Min(100,3+2*(SkillMechanics.Rank(p,CombatAbility.ColdArrow)-1))) && NecromancerEffects.ManaBalance256(p)==before-SkillMechanics.Values(p,CombatAbility.ColdArrow).Mana256 && (ammo.Deleted?0:ammo.Amount)==quantity-1,"Captured Cold conversion/once quantity/no repeated mana");return f;
    }
    private static void Controlled(Action action)
    {
        var launch=AmazonBow.LaunchRandom;var hit=WeaponMissiles.HitRoll;var damage=WeaponMissiles.DamageRoll;var block=SourceBlocking.BlockRoll;
        try{AmazonBow.LaunchRandom=new Minimum();WeaponMissiles.HitRoll=()=>0;WeaponMissiles.DamageRoll=_=>0;SourceBlocking.BlockRoll=_=>99;action();}
        finally{AmazonBow.LaunchRandom=launch;WeaponMissiles.HitRoll=hit;WeaponMissiles.DamageRoll=damage;SourceBlocking.BlockRoll=block;}
    }
    private static void Finish(long id)
    {for(int i=0;i<250 && WeaponMissiles.State(id) is{} f;i++){WeaponMissiles.Tick(f.NextUtc);}Require(WeaponMissiles.State(id)==null,"Cold projectile did not finish");}
    private static BowLaunchTarget Target(PlayerMobile p,int physical=0,int cold=0)
    {var t=MagicArrowChecks.Target(p);t.SetResistance(ResistanceType.Physical,physical);t.SetResistance(ResistanceType.Cold,cold);return t;}
    private static WeaponFlight[] Own(PlayerMobile p)=>WeaponMissiles.Snapshot().Flights.Where(x=>x.Owner==p.Serial.Value).ToArray();
    private static void Cases()
    {
        var p=Fresh();try
        {
            var bow=MagicArrowChecks.Bow(p,typeof(Bow));Require(!ClassCombat.Available(p,CombatAbility.ColdArrow),"Unlearned Cold Arrow available");Train(p,CombatAbility.ColdArrow);
            Require(ClassCombat.Available(p,CombatAbility.ColdArrow) && CombatTiming.WeaponAttack(CombatAbility.ColdArrow)
                && CombatTiming.For(p,CombatAbility.ColdArrow)==CombatTiming.For(p,CombatAbility.MagicArrow)
                && SkillBindings.Instance.Assign(p,true,CombatAbility.ColdArrow) && SkillBindings.Instance.Assign(p,false,CombatAbility.ColdArrow)
                && SkillBindings.Instance.AssignQuick(p,6,CombatAbility.ColdArrow),"Learned timing/bindings");
            var values=SkillMechanics.Values(p,CombatAbility.ColdArrow);Require(values.Mana256==896 && values.Damage256(false)==768 && values.Damage256(true)==1024 && values.DurationFrames==100,"Pinned rank1 cold");
            Ready(p);int mana=NecromancerEffects.ManaBalance256(p);Require(!ClassCombat.Perform(p,CombatAbility.ColdArrow,null,Aim(p),Core.TickCount) && NecromancerEffects.ManaBalance256(p)==mana,"No-ammo start accepted/paid");
            var bolts=new Bolt(4);p.AddToBackpack(bolts);Ready(p);Require(!ClassCombat.Perform(p,CombatAbility.ColdArrow,null,Aim(p),Core.TickCount),"Wrong ammo accepted");
            var arrows=new Arrow(15);p.AddToBackpack(arrows);var f=Cast(p);Require(f.Damage.Elements is{ColdMin:768,ColdMax:1024,ColdFrames:100},"Actual cold capture");WeaponMissiles.Cancel(p);
            Cast(p,held:true);WeaponMissiles.Cancel(p);Cast(p,held:true,left:true);WeaponMissiles.Cancel(p);Require(arrows.Amount==12 && bolts.Amount==4,"Once-only Arrow selection");
            bow=MagicArrowChecks.Bow(p,typeof(Crossbow));Cast(p);WeaponMissiles.Cancel(p);Require(arrows.Amount==12 && bolts.Amount==3,"Crossbow Bolt selection");bow=MagicArrowChecks.Bow(p,typeof(Bow));
            NecromancerEffects.SetMana256(p,895);Ready(p);Require(!ClassCombat.Perform(p,CombatAbility.ColdArrow,null,Aim(p),Core.TickCount) && NecromancerEffects.ManaBalance256(p)==895,"Insufficient fractional mana accepted/paid");
            Mana(p);Ready(p);mana=NecromancerEffects.ManaBalance256(p);int quantity=arrows.Amount;
            Require(ClassCombat.Perform(p,CombatAbility.ColdArrow,null,Aim(p),Core.TickCount),"Paid cancel start");long at=ClassCombat.For(p).EffectAt;ClassCombat.Stop(p);
            Require(!ClassCombat.ResolveAction(p,at) && NecromancerEffects.ManaBalance256(p)==mana-896 && arrows.Amount==quantity,"Cancel refunded or consumed ammo");
            Ready(p);Require(ClassCombat.Perform(p,CombatAbility.ColdArrow,null,Aim(p),Core.TickCount),"Paid move start");at=ClassCombat.For(p).EffectAt;var origin=p.Location;
            p.Direction=Direction.East;Require(p.Move(Direction.East),"Outward step");p.Direction=Direction.West;Require(p.Move(Direction.West) && p.Location==origin,"Return step");
            Require(!ClassCombat.ResolveAction(p,at) && NecromancerEffects.ManaBalance256(p)==mana-1792 && arrows.Amount==quantity,"Movement revived/refunded shot");
            Ready(p);Require(ClassCombat.Perform(p,CombatAbility.ColdArrow,null,Aim(p),Core.TickCount),"Mana-loss start");at=ClassCombat.For(p).EffectAt;NecromancerEffects.SetMana256(p,0);
            Require(ClassCombat.ResolveAction(p,at) && Own(p).Length==1 && arrows.Amount==quantity-1 && NecromancerEffects.ManaBalance256(p)==0,"Paid shot required second mana debit");WeaponMissiles.Cancel(p);
            Train(p,CombatAbility.ColdArrow,20);values=SkillMechanics.Values(p,CombatAbility.ColdArrow);
            Require(values.Mana256==1504 && values.Damage256(false)==53*256 && values.Damage256(true)==56*256 && values.DurationFrames==670,"Rank20 cold tiers/mana/length");
            Train(p,CombatAbility.IceArrow);values=SkillMechanics.Values(p,CombatAbility.ColdArrow);Require(values.Damage256(false)==15196 && values.Damage256(true)==16056,"Hard Ice synergy fractions");
            var item=NamedGear.Create("unique/343",99,new Maximum(),true);p.AddToBackpack(item);Require(p.EquipItem(item) && ClassGear.Active(p,item),"Actual Nightwing");values=SkillMechanics.Values(p,CombatAbility.ColdArrow);var d=BowLaunch.Capture(p,CombatAbility.ColdArrow,false);
            Require(values.Rank==22 && SkillMechanics.Rank(p,CombatAbility.IceArrow)==3 && d.Elements.ColdMin==20112 && d.Elements.ColdMax==21431 && d.Elements.ColdFrames==730 && d.ColdMastery==0,"Real soft ranks/hard-only synergy/once cold mastery");var ring=NamedGear.Create("unique/275",99,new Maximum(),true);p.AddToBackpack(ring);Require(p.EquipItem(ring),"Actual Raven Frost");d=BowLaunch.Capture(p,CombatAbility.ColdArrow,false);
            Require(d.Elements.ColdMin==20112+4416 && d.Elements.ColdMax==21431+13248 && d.Elements.ColdFrames==830,"Independent real item cold mastery and additive raw length");p.AddToBackpack(ring);p.AddToBackpack(item);
            foreach(bool immune in new[]{false,true})
            {
                Mana(p);var target=Target(p,immune?0:100,immune?100:0);try
                {
                    int before=SkillHealth.Balance256(target);f=Cast(p,target);var roll=f.Damage.Roll(target,_=>0);int expected=NamedElements.To256(immune?roll.Physical:roll.Cold);Finish(f.Id);
                    Require(expected>0 && SkillHealth.Balance256(target)==before-expected && (TimedEffects.Chill(target,TimedEffects.Now)!=0)==!immune,"Cold conversion/immunity or chill eligibility");
                }finally{target.Delete();}
            }
        }finally{WeaponMissiles.Cancel(p);p.Delete();}
    }
    private static void ColdState()
    {
        var p=Fresh();var target=Target(p);try
        {
            var now=TimedEffects.Now;Require(ColdStates.Apply(p,target,100,Core.TickCount),"Actual source cold state");Require(TimedEffects.TryActor(target,out var a) && a.ChillPercent==50 && a.Cold.Source==p.Serial.Value,"Cold owner/rate");
            Require(Math.Abs((a.ChillUntil-now).TotalMilliseconds-4000)<50,"Normal frame duration");var until=a.ChillUntil;
            Require(ColdStates.Apply(p,target,1,Core.TickCount) && a.ChillUntil==until,"Shorter cold shortened active state");
            TimedEffects.Settle(target,until);Require(TimedEffects.Chill(target,until)==0 && !TimedEffects.TryActor(target,out _),"Source cold expiration");
            target.SetResistance(ResistanceType.Cold,50);now=TimedEffects.Now;Require(ColdStates.Apply(p,target,100,Core.TickCount),"Resisted cold state");TimedEffects.TryActor(target,out a);
            Require(Math.Abs((a.ChillUntil-now).TotalMilliseconds-2000)<50,"Cold resistance frame scaling");TimedEffects.Clear(target);
            Require(ColdStates.LengthFrames(1,50,false,false)==0 && ColdStates.LengthFrames(3,0,false,true)==1 && ColdStates.LengthFrames(100,0,true,false)==0,"Pre-state frame truncation/freeze item gates");
            foreach(var difficulty in new[]{MonsterDifficulty.Nightmare,MonsterDifficulty.Hell})
            {
                var t=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true};MonsterVariants.Assign(t,difficulty);t.MoveToWorld(MagicArrowChecks.Aim(p,7),p.Map);t.AIObject.AITimer.Stop();t.SetResistance(ResistanceType.Cold,0);
                try
                {
                    now=TimedEffects.Now;Require(ColdStates.Apply(p,t,101,Core.TickCount),"Real variant cold state");TimedEffects.TryActor(t,out var variant);
                    Require(variant.ChillPercent==(difficulty==MonsterDifficulty.Nightmare?40:25) && Math.Abs((variant.ChillUntil-now).TotalMilliseconds-(difficulty==MonsterDifficulty.Nightmare?2000:1000))<50,"Variant-specific cold effect/divisor/truncation");
                }finally{TimedEffects.Clear(t);t.Delete();}
            }
            // Signed rate component for the pinned Hell Frozen Horror +33 row.
            TimedEffects.Chill(target,-33,1000,TimedEffects.Now);TimedEffects.TryActor(target,out a);a.Cold=new(p.Serial.Value);
            Require(GearEffects.MovementSlow(target)==-33 && GearEffects.AttackSlow(target)==-33 && ColdStates.OtherRate(target)==33,"Signed cold speed was turned into a slow or discarded");
            var old=JsonSerializer.SerializeToNode(TimedEffects.Snapshot().Single(x=>x.Owner==target.Serial.Value));old.AsObject().Remove("Cold");Require(old.Deserialize<TimedEffectState>().Cold==null,"Old timed-effect optional cold compatibility");
            TimedEffects.Clear(target);target.SetResistance(ResistanceType.Cold,0);var other=Fresh();try
            {
                Require(ColdStates.Apply(other,target,100,Core.TickCount) && ColdStates.Apply(p,target,201,Core.TickCount),"Cross-owner active refresh");TimedEffects.TryActor(target,out a);
                Require(a.Cold.Source==other.Serial.Value,"Active cold owner replaced");
                a.ChillUntil=TimedEffects.Now.AddMilliseconds(-40);a.Updated=TimedEffects.Now.AddMilliseconds(-80);
                Require(ColdStates.Apply(p,target,100,Core.TickCount),"Expired state fresh application");TimedEffects.TryActor(target,out a);
                Require(a.Cold.Source==p.Serial.Value,"Expired cold retained previous owner before periodic settle");
                TimedEffects.Clear(target);TimedEffects.Chill(target,17,8000,TimedEffects.Now);TimedEffects.TryActor(target,out a);var retained=a.ChillUntil;
                Require(ColdStates.Apply(p,target,100,Core.TickCount) && a.Cold.Source==0 && a.ChillPercent==17 && a.ChillUntil==retained,"Untagged legacy state invented owner or replaced strength/expiry");
            }finally{other.Delete();}
        }finally{TimedEffects.Clear(target);target.Delete();p.Delete();}
    }
    private static void Hold(PlayerMobile p,WeaponFlight f)
    {var all=WeaponMissiles.Snapshot();WeaponMissiles.Restore(all with{Flights=all.Flights.Where(x=>x.Owner!=p.Serial.Value).Append(f).ToArray()});}
    private static void Persist()
    {
        var p=Fresh();Train(p,CombatAbility.ColdArrow,20);Train(p,CombatAbility.IceArrow);var bow=MagicArrowChecks.Bow(p,typeof(Bow));
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.ColdArrow) && SkillBindings.Instance.Assign(p,false,CombatAbility.ColdArrow) && SkillBindings.Instance.AssignQuick(p,6,CombatAbility.ColdArrow),"Saved bindings");
        var ammo=new Arrow(10);p.AddToBackpack(ammo);var target=Target(p);var first=Cast(p,target);Finish(first.Id);Require(TimedEffects.TryActor(target,out var state) && state.Cold!=null,"Actual arrow did not apply saved cold state");state.ChillUntil=TimedEffects.Now.AddMinutes(10);
        var flight=Cast(p,target) with{NextUtc=TimedEffects.Now.AddMinutes(10)};Hold(p,flight);Ready(p);Require(ClassCombat.Perform(p,CombatAbility.ColdArrow,null,Aim(p),Core.TickCount),"Paid/unemitted saved windup");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,target.Serial.Value,bow.Serial.Value,ammo.Serial.Value,p.Location,flight,SkillHealth.Balance256(target),NecromancerEffects.ManaBalance256(p),ammo.Amount,state.ChillUntil,state.Cold)));
    }
    private static void NaturalExpiry()
    {
        var p=Fresh();var target=Target(p);Require(ColdStates.Apply(p,target,1,Core.TickCount),"Natural minimum-frame state");
        Timer.StartTimer(TimeSpan.FromMilliseconds(220),()=>
        {
            try{Require(TimedEffects.Chill(target,TimedEffects.Now)==0 && !TimedEffects.TryActor(target,out _),"Natural source state expiry");Log.Information("cold-arrow-state-result: passed");}
            catch(Exception e){Log.Error(e,"cold-arrow-state-result: failed");}
            finally{target.Delete();p.Delete();}
        });
    }
    private static void Check()=>Controlled(()=>{Differential();Cases();ColdState();Persist();NaturalExpiry();});
    private static void Reload()=>Controlled(()=>
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var target=(BowLaunchTarget)World.FindMobile((Serial)saved.Target);var ammo=World.FindItem((Serial)saved.Ammo);
        Require(Own(p).Single()==saved.Flight && SkillHealth.Balance256(target)==saved.Health && NecromancerEffects.ManaBalance256(p)==saved.Mana && ammo.Amount==saved.Quantity
            && ClassCombat.For(p).PendingAbility==CombatAbility.None && SkillBindings.Instance.Get(p)==(CombatAbility.ColdArrow,CombatAbility.ColdArrow)
            && SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Amazon,6)==CombatAbility.ColdArrow && TimedEffects.TryActor(target,out var state) && state.Cold==saved.Cold && state.ChillUntil==saved.ChillUntil,"Saved cold payload/state/resources/bindings or replayed windup");
        p.MoveToWorld(saved.Origin,Map.Maps[saved.Flight.Map]);var bow=World.FindItem((Serial)saved.Bow);p.AddToBackpack(bow);
        int expected=NamedElements.To256(saved.Flight.Damage.Roll(target,_=>0).Total);Hold(p,saved.Flight with{NextUtc=TimedEffects.Now});Finish(saved.Flight.Id);
        Require(SkillHealth.Balance256(target)==saved.Health-expected && NecromancerEffects.ManaBalance256(p)==saved.Mana && ammo.Amount==saved.Quantity,"Resumed cold payload/resource repayment");
        TimedEffects.TryActor(target,out state);Require(state.Cold==saved.Cold && state.ChillUntil==saved.ChillUntil,"Resumed shorter chill replaced saved state");state.ChillUntil=TimedEffects.Now.AddMinutes(10);
        var held=saved.Flight with{NextUtc=TimedEffects.Now.AddMinutes(10)};Hold(p,held);Require(p.EquipItem(bow),"Reload bow");Ready(p);Require(ClassCombat.Perform(p,CombatAbility.ColdArrow,null,Aim(p),Core.TickCount),"Next paid/unemitted windup");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Flight=held,Health=SkillHealth.Balance256(target),Mana=NecromancerEffects.ManaBalance256(p),ChillUntil=state.ChillUntil}));
    });
    private static void Input()
    {
        var p=Fresh();Train(p,CombatAbility.ColdArrow);MagicArrowChecks.Bow(p,typeof(Bow));Ready(p);var ammo=new Arrow(3);p.AddToBackpack(ammo);var target=Target(p);
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.ColdArrow) && SkillBindings.Instance.Assign(p,false,CombatAbility.ColdArrow) && SkillBindings.Instance.AssignQuick(p,6,CombatAbility.ColdArrow),"Connected bindings");
        var network=NetState.Instances.Single(x=>x.Mobile==null && IPAddress.IsLoopback(x.Address));network.Seeded=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var protocol=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);Require(protocol!=null,"Protocol bootstrap changed");protocol.SetValue(network,Enum.Parse(protocol.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        int before=NecromancerEffects.ManaBalance256(p),health=SkillHealth.Balance256(target);bool started=false,emitted=false;long deadline=Core.TickCount+5000;
        var hit=WeaponMissiles.HitRoll;var damage=WeaponMissiles.DamageRoll;var block=SourceBlocking.BlockRoll;WeaponMissiles.HitRoll=()=>0;WeaponMissiles.DamageRoll=_=>0;SourceBlocking.BlockRoll=_=>99;
        void Clean(){WeaponMissiles.HitRoll=hit;WeaponMissiles.DamageRoll=damage;SourceBlocking.BlockRoll=block;WeaponMissiles.Cancel(p);target.Delete();p.Delete();}
        void Wait()
        {
            try
            {
                Require(Core.TickCount<deadline,"Connected Cold Arrow did not complete");
                if(!started && ClassCombat.For(p).PendingAbility==CombatAbility.ColdArrow)
                {started=true;Require(NecromancerEffects.ManaBalance256(p)==before-SkillMechanics.Values(p,CombatAbility.ColdArrow).Mana256 && ammo.Amount==3 && Own(p).Length==0,"Natural start4 phase");}
                var fan=Own(p);if(!emitted && fan.Length>0)
                {emitted=true;Require(started && fan.Length==1 && fan.All(x=>x.Frame<3 && x.Missile=="coldarrow") && SkillHealth.Balance256(target)==health && ammo.Amount==2,"Natural single-arrow/ammunition phase");}
                if(!emitted || fan.Length>0){Timer.StartTimer(TimeSpan.FromMilliseconds(30),Wait);return;}
                Require(SkillHealth.Balance256(target)<health && NecromancerEffects.ManaBalance256(p)==before-SkillMechanics.Values(p,CombatAbility.ColdArrow).Mana256 && ammo.Amount==2,"Natural travelling cold arrow/resource accounting");Require(TimedEffects.Chill(target,TimedEffects.Now)==50 && ColdStates.OtherRate(target)==-50,"Natural cold impact state");Clean();Log.Information("cold-arrow-input-result: passed");
            }catch(Exception e){Clean();Log.Error(e,"cold-arrow-input-result: failed");}
        }
        var point=Aim(p);Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("cold-arrow-input: ready ability={Ability} x={X} y={Y} z={Z}",(int)CombatAbility.ColdArrow,point.X,point.Y,point.Z);Wait();});
    }
}
