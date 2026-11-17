// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class LightningStrikeContactChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(LightningStrikeContactChecks));
    private const CombatAbility Ability=CombatAbility.LightningStrike;
    private sealed class NoWeaponEffects:Random{public override int Next(int n)=>throw new InvalidOperationException("Lightning Strike source0 chain requested weapon effects");}
    private sealed record Saved(uint Owner,uint Primary,uint Secondary,uint Weapon,uint[] Items,Point3D Point,string Gear,string Profile,ChainFlight Flight,int Life,int PrimaryLife,int SecondaryLife,int Mana,int Durability,int Quantity,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"lightning-strike-contact-fixtures.json");
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static ChainFlight[] Flights(PlayerMobile p)=>ChainLightning.Snapshot().Where(x=>x.Owner==p.Serial.Value).ToArray();
    private static ChainFlight Flight(PlayerMobile p)=>Flights(p).SingleOrDefault();
    private static PlayerProcSave Own(PlayerMobile p,Item w)=>MagicArrowContactChecks.Own(p,w);
    private static TimedEffectState State(Mobile t)=>TimedEffects.Snapshot().SingleOrDefault(x=>x.Owner==t.Serial.Value);
    private static BaseCreature Target(PlayerMobile p,int dx)=>ChargedStrikeContactChecks.Target(p,dx);
    private static void Ready(PlayerMobile p)=>ChargedStrikeContactChecks.Ready(p);
    private static void Clean(PlayerMobile p,params BaseCreature[] targets){ChainLightning.Cancel(p);ChargedStrikeContactChecks.Clean(p,targets);}
    private static void Keep(PlayerMobile p,ChainFlight f)=>ChainLightning.Restore(ChainLightning.Snapshot().Where(x=>x.Owner!=p.Serial.Value).Append(f).ToArray());
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"lightning-strike-contact-reload-check":"lightning-strike-contact-check";ConsoleInputHandler.RegisterCommand(name,"Lightning Strike source0 chain contacts and original saved return/lease",_=>Core.LoopContext.Post(()=>
            {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
        foreach(string mode in new[]{"right","quick"})
        {string label="lightning-strike-contact-"+mode;ConsoleInputHandler.RegisterCommand(label+"-check","Authenticated melee miss and finite independent Lightning Strike chain",_=>Core.LoopContext.Post(()=>
            {try{FixtureNetwork.Connected(label+"-result",n=>Input(n,label));Log.Information("{Label}-check: passed",label);}catch(Exception e){Log.Error(e,"{Label}-check: failed",label);}}));}
    }
    private static PlayerMobile Owner(int row,out BaseWeapon w,out Item[] gear)
    {var p=ChargedStrikeContactChecks.Owner(row,out w,out gear);ClassSkillChecks.Train(p,SkillMechanics.For(Ability).TalentId);Require(SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Lightning Strike bindings");return p;}
    private static ChainFlight Birth(PlayerMobile p,BaseCreature primary,BaseWeapon w,bool solo=false)
    {Ready(p);int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,Ability).Mana256,dur=w.HitPoints;Require(ClassCombat.Perform(p,Ability,primary,primary.Location,Core.TickCount)&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Lightning Strike start payment");ClassCombat.ReleaseCast(p);Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt)&&NativeMonsterReturn.Session(primary).Packets==1&&w.HitPoints==dur,"Missed melee must free its own return packet");var f=Flight(p);Require(solo?f==null:f is{PlayerPacket:not null,Remaining:2,StrikeContext.OpeningPrimary:var id}&&id==primary.Serial.Value,"Approved opening/budget/solo");return f;}
    private static DateTime Contact(PlayerMobile p,BaseCreature target)
    {int before=NativeMonsterReturn.Session(target).Packets;for(int i=0;i<32;i++){var f=Flight(p);Require(f!=null,"Chain ended before expected contact");var at=f.NextUtc;ChainLightning.Tick(at,true,f.Cast);if(NativeMonsterReturn.Session(target).Packets>before){return at;}}throw new InvalidOperationException("Chain contact timeout");}
    private static int Raw(ChainFlight f)=>SorceressSpellMissiles.Roll(f.Min256,f.Max256,f.PlayerPacket.Seed,out _);
    private static void PacketClock(PlayerMobile p,BaseWeapon w)
    {
        var t=Target(p,6);try{t.SetResistance(ResistanceType.Poison,0);var at=TimedEffects.Now;Require(TimedEffects.PoisonFrames(p,t,64,200,at),"Prior poison clock");var old=State(t);int hp=SkillHealth.Balance256(t);var profile=Json(Own(p,w));var world=NpcWorldAllocation.Capture();Require(PlayerWeaponMissileContact.TryElementalPacket(p,t,new(0,0,0,0,1),at,out string reason),reason);Require(hp-SkillHealth.Balance256(t)==256&&State(t).PoisonClock==old.PoisonClock&&State(t).PoisonMs==old.PoisonMs&&Json(Own(p,w))==profile&&NpcWorldAllocation.Capture()==world,"Immediate zero-length poison replaced an existing clock or allocated weapon RNG");}finally{t.Delete();}
    }
    private static void Cases(PlayerMobile p,BaseWeapon w,Item[] gear)
    {
        using var candidate=PlayerItemProcWorld.Candidate(p);PacketClock(p,w);
        foreach(string mode in new[]{"hit","immune","resisted","lethal","removed","potion","potion-live","two-hops","solo"})
        {
            var primary=Target(p,1);var secondary=mode=="solo"?null:Target(p,5);var old=PlayerItemProcWorld.Observed;var effects=WeaponMissiles.ItemEffects;
            try
            {
                if(mode=="potion"){Require(CombatPotions.Apply(p,3),"Potion before creation");}var f=Birth(p,primary,w,mode=="solo");if(mode=="solo"){Require(Flight(p)==null,"Solo emitted a chain");continue;}if(mode=="immune"){secondary.SetResistance(ResistanceType.Energy,100);}if(mode=="resisted"){secondary.SetResistance(ResistanceType.Energy,50);}if(mode=="lethal"){SkillHealth.SetBalance256(secondary,1);}bool potion=mode.StartsWith("potion");if(potion){foreach(var r in Enum.GetValues<ResistanceType>()){secondary.SetResistance(r,0);}if(mode=="potion-live"){Require(f.PlayerPacket.Modifiers.Potion==0&&CombatPotions.Apply(p,3),"Live potion setup");}}if(mode=="removed"){foreach(var item in new[]{(Item)w}.Concat(gear)){p.AddToBackpack(item);}ClassGear.Refresh(p);}int before=SkillHealth.Balance256(secondary),primaryLife=SkillHealth.Balance256(primary),mana=NecromancerEffects.ManaBalance256(p),dur=w.HitPoints,quantity=NamedAmmunition.Remaining(w),visits=0,raw=Raw(f);var profile=Json(Own(p,w));var world=NpcWorldAllocation.Capture();PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits++;}};WeaponMissiles.ItemEffects=new NoWeaponEffects();SourceBlocking.BlockRoll=_=>throw new InvalidOperationException("Chain requested physical shield");WeaponMissiles.HitRoll=()=>throw new InvalidOperationException("Chain requested accuracy");
                var at=Contact(p,secondary);int expected=mode=="immune"?0:mode=="resisted"?raw/2:potion?raw+4*(raw/10):raw;var next=Flight(p);Require(before-SkillHealth.Balance256(secondary)==Math.Min(before,expected)&&SkillHealth.Balance256(primary)==primaryLife&&NativeMonsterReturn.Session(secondary).Packets==1&&next is{Remaining:1,StrikeContext.OpeningPrimary:0}&&next.Delays.Single().Target==secondary.Serial.Value&&next.Delays.Single().UntilUtc==at.AddMilliseconds(160),"First chain contact/typed damage/opening/next lease: "+mode);
                if(mode=="two-hops"){int second=Raw(next);Contact(p,primary);Require(Flight(p)==null&&primaryLife-SkillHealth.Balance256(primary)==second&&NativeMonsterReturn.Session(primary).Packets==2,"Finite return hop and per-child packet");}
                Require(visits==0&&State(secondary)?.PoisonClock==null&&State(secondary)?.Cold==null&&(State(secondary)?.HealBlockedUntilUtc??default)==default&&TimedEffects.Slow(secondary,TimedEffects.Now)==0&&TimedEffects.DefenseLoss(secondary)==0&&Json(Own(p,w))==profile&&NpcWorldAllocation.Capture()==world&&NecromancerEffects.ManaBalance256(p)==mana&&w.HitPoints==dur&&NamedAmmunition.Remaining(w)==quantity,"Chain weapon event/status/RNG/resource leak: "+mode);
                if(mode=="hit"){var all=ChainLightning.Snapshot();bool refused=false;try{ChainLightning.Restore(all.Select(x=>x.Cast==next.Cast?x with{PlayerPacket=null}:x).ToArray());}catch(InvalidDataException){refused=true;}Require(refused&&Json(ChainLightning.Snapshot())==Json(all),"Missing required Lightning Strike player packet was adopted or partially restored");}
            }
            catch(Exception e){throw new InvalidOperationException("Lightning Strike chain "+mode,e);}
            finally{PlayerItemProcWorld.Observed=old;WeaponMissiles.ItemEffects=effects;SourceBlocking.BlockRoll=n=>n-1;WeaponMissiles.HitRoll=()=>0;Clean(p,primary,secondary);TimedEffects.Clear(p);foreach(var item in new[]{(Item)w}.Concat(gear)){if(item.Parent!=p){Require(p.EquipItem(item),"Restore chain provider");}}ClassGear.Refresh(p);}
        }
    }
    private static void Check(bool reload)
    {
        using var rolls=new ChargedStrikeContactChecks.Rolls();var seed=ChainLightning.SeedLow;ChainLightning.SeedLow=()=>73;
        try
        {
            PlayerMobile p;BaseWeapon w;Item[] gear;int generation=0;
            if(!reload){p=Owner(3600,out w,out gear);using(PlayerItemProcWorld.Candidate(p)){ChargedStrikeContactChecks.Birth(p,w,true);ChargedStrikeBolts.Cancel(p);}Cases(p,w,gear);}
            else
            {
                var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=s.Generation+1;p=(PlayerMobile)World.FindMobile((Serial)s.Owner);w=(BaseWeapon)World.FindItem((Serial)s.Weapon);gear=s.Items.Select(id=>World.FindItem((Serial)id)).ToArray();var a=(BaseCreature)World.FindMobile((Serial)s.Primary);var b=(BaseCreature)World.FindMobile((Serial)s.Secondary);
                Require(generation<=2&&w.Parent==p&&gear.All(x=>x.Parent==p)&&Json(new[]{(Item)w}.Concat(gear).Select(ClassGear.State))==s.Gear&&Json(Own(p,w))==s.Profile&&Json(Flight(p))==Json(s.Flight)&&SkillHealth.Balance256(p)==s.Life&&SkillHealth.Balance256(a)==s.PrimaryLife&&SkillHealth.Balance256(b)==s.SecondaryLife&&NecromancerEffects.ManaBalance256(p)==s.Mana&&w.HitPoints==s.Durability&&NamedAmmunition.Remaining(w)==s.Quantity,"Saved original returning chain/lease/gear/proc/wound/resources");p.MoveToWorld(s.Point,Map.Trammel);a.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);b.MoveToWorld(new(p.X+5,p.Y,p.Z),p.Map);foreach(var t in new[]{a,b}){t.Frozen=t.CantWalk=true;t.AIObject.AITimer.Stop();foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,100);}t.SetResistance(ResistanceType.Energy,0);}var old=PlayerItemProcWorld.Observed;var effects=WeaponMissiles.ItemEffects;int visits=0;var world=NpcWorldAllocation.Capture();
                try{using var candidate=PlayerItemProcWorld.Candidate(p);PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits++;}};WeaponMissiles.ItemEffects=new NoWeaponEffects();SourceBlocking.BlockRoll=_=>throw new InvalidOperationException("Restored chain requested shield");var at=TimedEffects.Now;var f=s.Flight with{NextUtc=at,UntilUtc=at.AddSeconds(4),Delays=s.Flight.Delays.Select(d=>d with{UntilUtc=at+(d.UntilUtc-s.Flight.NextUtc)}).ToArray()};Keep(p,f);Contact(p,a);Require(Flight(p)==null&&s.PrimaryLife-SkillHealth.Balance256(a)==Raw(s.Flight)&&SkillHealth.Balance256(b)==s.SecondaryLife&&NativeMonsterReturn.Session(a).Packets==1&&visits==0&&Json(Own(p,w))==s.Profile&&NpcWorldAllocation.Capture()==world&&NecromancerEffects.ManaBalance256(p)==s.Mana&&w.HitPoints==s.Durability&&NamedAmmunition.Remaining(w)==s.Quantity,"Original saved return replayed damage/procs/RNG/resources or ignored lease");}
                finally{PlayerItemProcWorld.Observed=old;WeaponMissiles.ItemEffects=effects;SourceBlocking.BlockRoll=n=>n-1;Clean(p,a,b);}
            }
            var primary=Target(p,1);var secondary=Target(p,5);ChainFlight held;using(PlayerItemProcWorld.Candidate(p)){Birth(p,primary,w);Contact(p,secondary);held=Flight(p);ChainLightning.Tick(held.NextUtc,true,held.Cast);held=Flight(p);}Require(held is{Frame:1,Remaining:1,StrikeContext.OpeningPrimary:0}&&held.Delays.Length==1,"Partial original returning chain/lease setup");var until=TimedEffects.Now.Add(FixtureSpace.Retention);held=held with{NextUtc=until,UntilUtc=until.AddMinutes(1),Delays=held.Delays.Select(d=>d with{UntilUtc=until+(d.UntilUtc-held.NextUtc)}).ToArray()};Keep(p,held);SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)/2+17);NecromancerEffects.SetMana256(p,17);var point=p.Location;p.Internalize();File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,primary.Serial.Value,secondary.Serial.Value,w.Serial.Value,gear.Select(x=>x.Serial.Value).ToArray(),point,Json(new[]{(Item)w}.Concat(gear).Select(ClassGear.State)),Json(Own(p,w)),held,SkillHealth.Balance256(p),SkillHealth.Balance256(primary),SkillHealth.Balance256(secondary),NecromancerEffects.ManaBalance256(p),w.HitPoints,NamedAmmunition.Remaining(w),generation)));
        }
        finally{ChainLightning.SeedLow=seed;}
    }
    private static void Input(NetState n,string label)
    {
        var rolls=new ChargedStrikeContactChecks.Rolls();var seed=ChainLightning.SeedLow;ChainLightning.SeedLow=()=>73;var p=Owner(3500,out var w,out _);var a=Target(p,1);var b=Target(p,5);Ready(p);var account=FixtureNetwork.Authenticate(n,p,"lightning-strike-contact-");var old=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits.Add(v);}};int aLife=SkillHealth.Balance256(a),bLife=SkillHealth.Balance256(b),mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,Ability).Mana256,dur=w.HitPoints;long deadline=Core.TickCount+6000;bool began=false,sawChain=false;
        void Close(){Clean(p,a,b);PlayerItemProcWorld.Observed=old;ChainLightning.SeedLow=seed;rolls.Dispose();p.Delete();account.Delete();}
        void Inspect()
        {
            try{Require(Core.TickCount<deadline,"Lightning Strike input timeout");if(!began&&ClassCombat.For(p).PendingAbility==Ability){began=true;Require(visits.Count==0&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Wire Lightning Strike start");Log.Information("{Label}: windup",label);}if(Flight(p)!=null){sawChain=true;}if(NativeMonsterReturn.Session(b).Packets<1||Flight(p)!=null){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Inspect);return;}Require(began&&sawChain&&visits.Count==1&&visits.Single().Phase=="domeleeattack"&&visits.Single().Outcomes.Single()is{CastAttempts:0,CastResults:0}&&NativeMonsterReturn.Session(a).Packets==2&&NativeMonsterReturn.Session(b).Packets==1&&aLife>SkillHealth.Balance256(a)&&bLife>SkillHealth.Balance256(b)&&State(b)?.PoisonClock==null&&TimedEffects.Slow(b,TimedEffects.Now)==0&&NecromancerEffects.ManaBalance256(p)==mana-cost&&w.HitPoints==dur,"Wire missed melee/finite chain/proc/resource split");Close();Log.Information("{Label}-result: passed",label);}catch(Exception e){Close();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)Ability,(int)DiabloClass.Amazon,a.Serial.Value,a.X,a.Y,a.Z);Inspect();});
    }
}
