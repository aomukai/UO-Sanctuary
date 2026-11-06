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

public static class LeapAttackChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(LeapAttackChecks));
    private static bool Creating;
    public sealed class Recipient:GraveyardZombie
    {
        internal int DamageCalls,Applied256;
        [Constructible]public Recipient(){}
        public Recipient(Serial serial):base(serial){}
        public override void OnDamage(int amount,Mobile from,bool willKill)
        {
            if(from is PlayerMobile p&&BarbarianLeap.State(p) is{Phase:LeapPhase.Recovery,Attack:{Released:false}})
            {DamageCalls++;Applied256+=amount*256;}
            base.OnDamage(amount,from,willKill);
        }
        public override void Serialize(IGenericWriter writer){base.Serialize(writer);writer.Write(0);writer.Write(DamageCalls);writer.Write(Applied256);}
        public override void Deserialize(IGenericReader reader){base.Deserialize(reader);if(reader.ReadInt()!=0)throw new InvalidOperationException("Unknown Leap recipient version");DamageCalls=reader.ReadInt();Applied256=reader.ReadInt();}
    }
    private sealed record Saved(LeapFlight Flight,int Mana,int Life,uint Target,int TargetLife,uint Weapon,int WeaponHp,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"leap-attack-fixtures.json");
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static void Rates()
    {
        var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;
        bool Fixture(Mobile p)=>Creating||p.Name?.StartsWith("LeapAttackFixture",StringComparison.Ordinal)==true;
        Mobile.HitsRegenRateHandler=p=>Fixture(p)?TimeSpan.FromHours(1):hp?.Invoke(p)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=p=>Fixture(p)?TimeSpan.FromHours(1):mp?.Invoke(p)??Mobile.DefaultManaRate;
    }
    public static void Configure()
    {
        Rates();Register("leap-attack-check",Check);Register("leap-attack-reload-check",Reload);
        foreach(string mode in new[]{"left","right","quick"})
        {string label="leap-attack-"+mode;Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,label)));}
    }
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Leap Attack flight,area,weapon,lifecycle and saves",_=>Core.LoopContext.Post(()=>
    {
        bool input=name is not("leap-attack-check"or"leap-attack-reload-check");
        try{if(input){Log.Information("{Name}: passed",name);}action();if(!input){Log.Information("{Name}: passed",name);}}
        catch(Exception e){Log.Error(e,"{Name}: failed",name);}
    }));
    private static PlayerMobile Owner(bool unarmed=false)
    {
        PlayerMobile p;Creating=true;try{p=DoubleSwingChecks.Owner(1,ability:CombatAbility.LeapAttack);}finally{Creating=false;}p.Name="LeapAttackFixture";
        if(unarmed){foreach(var w in p.Items.OfType<BaseWeapon>().ToArray()){w.Delete();}}DoubleSwingChecks.Ready(p);return p;
    }
    private static Recipient Target(PlayerMobile p,int dx,bool preventHealing=true)
    {
        Recipient t;Creating=true;try{t=new Recipient{Name="LeapAttackFixtureTarget",Frozen=true,CantWalk=true,NoKillAwards=true};}finally{Creating=false;}
        t.MoveToWorld(new(p.X+dx,p.Y,p.Z),p.Map);t.AIObject?.AITimer.Stop();t.SetHits(30000);t.Hits=t.HitsMax;t.SetResistance(ResistanceType.Physical,0);if(preventHealing){TimedEffects.PreventHealing(t,TimedEffects.Now);}return t;
    }
    private static bool Begin(PlayerMobile p,Mobile target=null,int distance=4)
    {DoubleSwingChecks.Ready(p);return ClassCombat.Perform(p,CombatAbility.LeapAttack,target,target?.Location??new(p.X+distance,p.Y,p.Z),Core.TickCount);}
    private static void Step(PlayerMobile p)
    {var f=BarbarianLeap.State(p);Require(f!=null,"Missing Leap Attack flight");BarbarianLeap.Tick(f.NextUtc);}
    private static void Until(PlayerMobile p,Func<LeapFlight,bool> ready)
    {for(int i=0;i<600&&BarbarianLeap.State(p) is{} f&&!ready(f);i++){Step(p);}Require(BarbarianLeap.State(p) is{} found&&ready(found),"Leap Attack phase did not arrive");}
    private static void Finish(PlayerMobile p)
    {for(int i=0;i<600&&BarbarianLeap.State(p)!=null;i++){Step(p);}Require(BarbarianLeap.State(p)==null,"Leap Attack never finished");}
    private static void Clean(PlayerMobile p,params Mobile[] targets)
    {if(BarbarianLeap.State(p)!=null){Finish(p);}foreach(var t in targets){SourceKnockbacks.Clear(t);CombatStuns.Clear(t);t.Corpse?.Delete();t.Delete();}p.Corpse?.Delete();p.Delete();}
    private static void Rules()
    {
        var row=SkillMechanics.For(CombatAbility.LeapAttack);
        foreach(int rank in new[]{1,8,9,16,17,22,23,28,29,40})foreach(int hard in new[]{0,1,20})
        {
            var v=new SkillValues(row,k=>k=="Leap Attack"?rank:255,k=>k=="Leap"?hard:0,99);
            Require(v.Mana256==2560&&v.Param(5)==12&&v.Eval("calc2")==175&&v.Eval("calc1")==200+30*(rank-1)+10*hard
                &&v.Formula("toht")==100+20*(rank-1),"Pinned Leap Attack costs/flight/weapon suppliers");
        }
        Require(CombatTiming.WeaponAttack(CombatAbility.LeapAttack)&&CombatTiming.SequenceRateAdjustment(CombatAbility.LeapAttack)==-30,"Leap Attack sequence rate channel");
    }
    private static void Mechanics()
    {
        var hit=BarbarianLeapAttack.HitRoll;var area=BarbarianLeapAttack.AreaRoll;var physical=BarbarianLeapAttack.PhysicalRoll;var block=SourceBlocking.BlockRoll;
        BarbarianLeapAttack.HitRoll=()=>0;BarbarianLeapAttack.AreaRoll=_=>0;BarbarianLeapAttack.PhysicalRoll=_=>0;SourceBlocking.BlockRoll=_=>99;
        try
        {
            foreach(bool unarmed in new[]{false,true})
            {
                var p=Owner(unarmed);var t=Target(p,6);var far=Target(p,11);var weapon=CombatTiming.Equipped(p);
                int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(t),farLife=SkillHealth.Balance256(far),durability=weapon?.HitPoints??0;
                try
                {
                    Require(Begin(p),"Learned ground Leap Attack refused");var start=BarbarianLeap.State(p);
                    Require(start is{MinimumAirFrames:12,Velocity256:4032,Phase:LeapPhase.Windup,Attack:not null}
                        &&start.Destination.X==start.Origin.X+5&&NecromancerEffects.ManaBalance256(p)==mana-2560,"Start/ground offset/payment mismatch");
                    ClassCombat.Stop(p);Require(BarbarianLeap.State(p)!=null,"Stop removed uninterruptible flight");
                    Until(p,f=>f.Phase==LeapPhase.Airborne);Require(SkillHealth.Balance256(t)==life,"Damage before landing");
                    Require(!p.Move(Direction.East)&&!HitRecovery.Apply(p,p.HitsMax,Core.TickCount),"Airborne input/get-hit lock missing");
                    Until(p,f=>f.Phase==LeapPhase.Recovery);var landed=BarbarianLeap.State(p);int afterArea=SkillHealth.Balance256(t);
                    Require(landed.AirFrames>=12&&landed.Attack is{Released:false,Frame:0}&&afterArea<life
                        &&SkillHealth.Balance256(far)==farLife&&(weapon?.HitPoints??0)==durability&&SourceKnockbacks.Remaining(t)==0,"Area order/radius/weapon isolation/minimum flight mismatch");
                    int untilContact=landed.Attack.ContactFrame;
                    for(int i=1;i<untilContact;i++){Step(p);Require(SkillHealth.Balance256(t)==afterArea,"Weapon contacted early");}
                    Step(p);Require(BarbarianLeap.State(p).Attack.Released&&SkillHealth.Balance256(t)<afterArea,"Post-landing weapon contact missing");
                    int afterWeapon=SkillHealth.Balance256(t);Finish(p);
                    Require(SkillHealth.Balance256(t)==afterWeapon&&NecromancerEffects.ManaBalance256(p)==mana-2560&&World.FindMobile((Serial)start.Reservation)==null,"Repeated damage/payment or leaked reservation");
                }
                finally{Clean(p,t,far);}
            }
            var adjacent=Owner();var near=Target(adjacent,1);
            Require(Begin(adjacent,near)&&BarbarianLeap.State(adjacent).Destination.X==adjacent.X+2,"Adjacent enemy used the obsolete ordinary-attack fallback");Finish(adjacent);Clean(adjacent,near);
            var miss=Owner();var missed=Target(miss,6);int paid=NecromancerEffects.ManaBalance256(miss);BarbarianLeapAttack.HitRoll=()=>1;
            Require(Begin(miss),"Miss scene begin");Until(miss,f=>f.Phase==LeapPhase.Recovery);int areaLife=SkillHealth.Balance256(missed);Finish(miss);
            Require(SkillHealth.Balance256(missed)==areaLife&&NecromancerEffects.ManaBalance256(miss)==paid-2560,"Weapon miss repeated area or escaped mana");Clean(miss,missed);BarbarianLeapAttack.HitRoll=()=>0;
            var shieldOwner=Owner();var shielded=new ZealContactChecks.Recipient{Frozen=true,CantWalk=true,NoKillAwards=true};shielded.SetHits(20000);shielded.MoveToWorld(new(shieldOwner.X+6,shieldOwner.Y,shieldOwner.Z),shieldOwner.Map);shielded.AIObject.AITimer.Stop();
            string source=CreatureProfiles.Definition(shielded).Reference;SourceBlocking.Restore(SourceBlocking.Snapshot().Where(s=>s.Actor!=shielded.Serial.Value).Append(new(shielded.Serial.Value,source,1)).ToArray());SourceBlocking.BlockRoll=_=>0;
            int shieldLife=SkillHealth.Balance256(shielded);Require(Begin(shieldOwner),"Blocked scene begin");Finish(shieldOwner);
            Require(SkillHealth.Balance256(shielded)==shieldLife,"Area/weapon bypassed existing source shield block");Clean(shieldOwner,shielded);SourceBlocking.BlockRoll=_=>99;
            var poor=Owner();NecromancerEffects.SetMana256(poor,2559);Require(!Begin(poor)&&BarbarianLeap.State(poor)==null,"Underfunded flight started");poor.Delete();
            var dying=Owner();var victim=Target(dying,6);Require(Begin(dying),"Deferred death begin");Until(dying,f=>f.Phase==LeapPhase.Airborne);
            int health=SkillHealth.Balance256(victim);dying.Damage(dying.Hits+1);Require(dying.Alive&&BarbarianLeap.State(dying).DelayedDeath,"Airborne death was not deferred");Finish(dying);
            Require(!dying.Alive&&SkillHealth.Balance256(victim)==health,"Deferred-dead flight emitted combat");Clean(dying,victim);
            var moved=Owner();Require(Begin(moved),"Foreign-map begin");var state=BarbarianLeap.State(moved);moved.MoveToWorld(new(100,100,0),Map.Felucca);BarbarianLeap.Tick(state.NextUtc);
            Require(BarbarianLeap.State(moved)==null&&moved.Map==Map.Felucca&&moved.Location==new Point3D(100,100,0),"Foreign-map cleanup pulled owner back");moved.Delete();
        }
        finally{BarbarianLeapAttack.HitRoll=hit;BarbarianLeapAttack.AreaRoll=area;BarbarianLeapAttack.PhysicalRoll=physical;SourceBlocking.BlockRoll=block;}
    }
    private static Saved Save(LeapPhase phase)
    {
        var p=Owner();var t=Target(p,6);SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-7*256-13);NecromancerEffects.SetMana256(p,50*256+17);
        Require(Begin(p),"Saved real Leap Attack begin");
        if(phase!=LeapPhase.Windup){Until(p,f=>f.Phase==phase);}
        var f=BarbarianLeap.State(p) with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};
        BarbarianLeap.Restore(BarbarianLeap.Snapshot().Select(s=>s.Owner==p.Serial.Value?f:s).ToArray());
        var weapon=CombatTiming.Equipped(p);return new(f,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(p),t.Serial.Value,SkillHealth.Balance256(t),weapon.Serial.Value,weapon.HitPoints);
    }
    private static void Check()
    {
        Rules();Mechanics();File.WriteAllText(Manifest,JsonSerializer.Serialize(Enum.GetValues<LeapPhase>().Select(Save).ToArray()));
        Log.Information("Leap Attack:ground/adjacent source placement,paid flight,min12air,area-before-weapon,isolation/radius,source post-landing contact,no repeats,low mana,deferred-dead suppression and external-map cleanup pass;three original paid flight phases retained for reload");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest));
        foreach(var s in saved)
        {
            var p=(PlayerMobile)World.FindMobile((Serial)s.Flight.Owner);var t=(BaseCreature)World.FindMobile((Serial)s.Target);var w=(BaseWeapon)World.FindItem((Serial)s.Weapon);
            Require(BarbarianLeap.State(p)==null&&World.FindMobile((Serial)s.Flight.Reservation)==null&&p.LogoutMap?.MapID==s.Flight.Map&&p.LogoutLocation==s.Flight.Destination,"Offline flight did not settle/cancel its reservation");
            Require(NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillHealth.Balance256(p)==s.Life&&SkillHealth.Balance256(t)==s.TargetLife&&w.Parent==p&&w.HitPoints==s.WeaponHp,"Offline load replayed/refilled/replaced original resources,target or weapon");
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved.Select(s=>s with{Generation=s.Generation+1}).ToArray()));
    }
    private static void Input(NetState network,string label)
    {
        var p=Owner();var t=Target(p,4,preventHealing:false);var account=FixtureNetwork.Authenticate(network,p,"LeapAttack");int mana=NecromancerEffects.ManaBalance256(p),before=SkillHealth.Balance256(t);bool began=false,landed=false;int landingLife=0,areaCalls=0;var oldArea=BarbarianLeapAttack.AreaRoll;BarbarianLeapAttack.AreaRoll=n=>{areaCalls++;return oldArea(n);};long start=0,deadline=Core.TickCount+10000;
        try
        {
            Require(SkillBindings.Instance.Assign(p,true,CombatAbility.LeapAttack)&&SkillBindings.Instance.Assign(p,false,CombatAbility.LeapAttack)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.LeapAttack),"Actual Leap Attack bindings");
            void Poll()
            {
                try
                {
                    Require(Core.TickCount<deadline,"Actual Leap Attack input timeout");var f=BarbarianLeap.State(p);
                    if(f!=null&&!began){began=true;start=Core.TickCount;Require(NecromancerEffects.ManaBalance256(p)==mana-2560,"Input start mana");Log.Information("{Label}: windup",label);}
                    if(f is{Phase:LeapPhase.Recovery}&&!landed){landed=true;landingLife=SkillHealth.Balance256(t);Require(t.Applied256>0&&t.DamageCalls==1&&areaCalls==1,$"Natural landing area absent: mode={label},owner={p.Location},dest={f.Destination},target={t.Location},life={landingLife}/{before},alive={t.Alive},damageable={t.CanBeDamaged()},hostile={ClassCombat.IsHostile(p,t)},LOS={p.InLOS(t)},phys={t.PhysicalResistance},source={CreatureProfiles.Definition(t).Reference},attack={JsonSerializer.Serialize(f.Attack)},candidates={SkillCombat.Targets(p,p.Location,SkillGeometry.Tiles(7)).Any(x=>x==t)},supported={BarbarianBattleCry.Supported(t)},rank={SkillMechanics.Rank(p,CombatAbility.LeapAttack)},min={SkillMechanics.Values(p,CombatAbility.LeapAttack).PhysicalDamage256(false)},calls={areaCalls},bone={NecromancerEffects.Armor256(t)},region={t.Region?.Name}");}
                    if(f==null&&began)
                    {
                        Require(landed&&t.Applied256>0&&t.DamageCalls==1&&areaCalls==1&&NecromancerEffects.ManaBalance256(p)==mana-2560&&Core.TickCount-start>=12*40,"Natural input flight/payment/minimum duration");
                        Log.Information("Leap landing native damage notification:{Applied}FP;current HP delta:{Current}FP;source regeneration remains enabled",t.Applied256,before-SkillHealth.Balance256(t));
                        BarbarianLeapAttack.AreaRoll=oldArea;Clean(p,t);account.Delete();Log.Information("{Result}: passed",label+"-result");return;
                    }
                    Timer.StartTimer(TimeSpan.FromMilliseconds(10),()=>Core.LoopContext.Post(Poll));
                }
                catch(Exception e){BarbarianLeapAttack.AreaRoll=oldArea;Clean(p,t);account.Delete();Log.Error(e,"{Result}: failed",label+"-result");}
            }
            Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(byte)CombatAbility.LeapAttack,(byte)DiabloClass.Barbarian,t.Serial.Value,t.X,t.Y,t.Z);
            FixtureClock.NextLoop(label+"-result",Poll);
        }
        catch(Exception e){BarbarianLeapAttack.AreaRoll=oldArea;Clean(p,t);account.Delete();Log.Error(e,"{Result}: failed",label+"-result");}
    }
}
