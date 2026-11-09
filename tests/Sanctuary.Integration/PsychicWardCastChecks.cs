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

public sealed class WardPlayer:PlayerMobile
{
    internal BaseCreature Watch;
    internal bool StunAtDamage;
    public WardPlayer(){}
    public WardPlayer(Serial serial):base(serial){}
    public override void OnDamage(int amount,Mobile from,bool willKill)
    {if(Watch!=null){StunAtDamage|=CombatStuns.Remaining(Watch)>0;}base.OnDamage(amount,from,willKill);}
    public override void OnAfterDelete(){Watch=null;base.OnAfterDelete();}
    public override void Serialize(IGenericWriter w){base.Serialize(w);w.Write(0);}
    public override void Deserialize(IGenericReader r){base.Deserialize(r);if(r.ReadInt()!=0){throw new InvalidDataException("Ward fixture version");}}
}
public static class PsychicWardCastChecks
{
    private const CombatAbility Ability=CombatAbility.PsychicWard;
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(PsychicWardCastChecks));
    private sealed record Saved(uint Owner,bool Female,int Life,int Mana,PsychicWardState State);
    private sealed record Manifest(int Generation,Saved[] Owners);
    private static string FileName=>Path.Join(Core.BaseDirectory,"ward-cast-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Learned Ward and automatic native incoming events",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure()
    {
        Register("ward-cast-check",()=>Check(false));Register("ward-cast-reload-check",()=>Check(true));
        foreach(string choice in new[]{"right","quick"}){string mode=choice;Register("ward-cast-"+mode+"-check",()=>FixtureNetwork.Connected("ward-cast-"+mode+"-result",n=>Input(n,mode)));}
    }
    public static void Initialize()
    {
        var hits=ClassGearHooks.CanRegenerateHits;var mana=ClassGearHooks.CanRegenerateMana;
        ClassGearHooks.CanRegenerateHits=p=>p.Name!="WardCastSaved"&&hits?.Invoke(p)!=false;
        ClassGearHooks.CanRegenerateMana=p=>p.Name!="WardCastSaved"&&mana?.Invoke(p)!=false;
    }
    private static void Ready(PlayerMobile p){ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;p.Frozen=p.Paralyzed=false;}
    private static WardPlayer Fresh(bool female=false,int x=6880)
    {
        var p=new WardPlayer{Player=true,Female=female,Body=female?0x191:0x190,Name="WardCastFixture"};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Warlock);ClassCreation.Profiles.AdvanceLevel(p,99);ClassSkillChecks.Train(p,"warlock.psychicward");
        FixtureSpace.Corridor(p,x,24,3500);NecromancerEffects.SetMana256(p,p.ManaMax*256);Ready(p);return p;
    }
    private static void Pool(WardPlayer p,int amount)
    {PsychicWard.Restore(PsychicWard.Snapshot().Select(r=>r.Owner==p.Serial.Value?r with{Remaining256=amount}:r).ToArray());}
    private static void Cast(WardPlayer p)
    {
        Ready(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);
        Require(ClassCombat.Perform(p,Ability,null,new(-100,-100,0),Core.TickCount),"Ward self start");var s=ClassCombat.For(p);
        Require(s.PendingTarget==Serial.Zero&&s.PendingPoint==p.Location&&s.Timing==WarlockCastClock.Calculate(CombatTiming.Fcr(p)),"Ward SC/self context");
        Require(ClassCombat.ResolveAction(p,s.EffectAt)&&!ClassCombat.ResolveAction(p,s.EffectAt),"Ward one-shot release");Ready(p);
    }
    private static void Casting(WardPlayer p)
    {
        Require(ClassAbilities.SelfTarget(Ability)&&ClassCombat.Available(p,Ability)&&!SkillBindings.Instance.Assign(p,true,Ability)
            &&SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability)&&SkillMechanics.For(Ability).Number("InTown")==1,"Ward learned/right/quick/self/town metadata");
        PsychicWard.Clear(p);Ready(p);int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,Ability).Mana256;
        Require(ClassCombat.Perform(p,Ability,null,p.Location,Core.TickCount),"Ward windup start");var action=ClassCombat.For(p);long effect=action.EffectAt;
        Require(!ClassCombat.ResolveAction(p,effect-1)&&PsychicWard.State(p)==null&&NecromancerEffects.ManaBalance256(p)==mana,"Ward early event/debit");
        ClassCombat.Stop(p);Require(!ClassCombat.ResolveAction(p,effect)&&PsychicWard.State(p)==null&&NecromancerEffects.ManaBalance256(p)==mana,"Ward Stop paid/released");
        Ready(p);Require(ClassCombat.Perform(p,Ability,null,p.Location,Core.TickCount),"Ward move start");effect=action.EffectAt;var at=p.Location;p.MoveToWorld(new(p.X,p.Y+1,p.Z),p.Map);
        Require(!ClassCombat.ResolveAction(p,effect)&&PsychicWard.State(p)==null,"Ward moved windup released");p.MoveToWorld(at,p.Map);
        Cast(p);Pool(p,641);var old=PsychicWard.State(p);Ready(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);
        Require(ClassCombat.Perform(p,Ability,null,p.Location,Core.TickCount),"Ward late-mana start");effect=action.EffectAt;NecromancerEffects.SetMana256(p,cost-1);
        Require(!ClassCombat.ResolveAction(p,effect)&&PsychicWard.State(p)==old&&NecromancerEffects.ManaBalance256(p)==cost-1,"Ward late mana altered old pool");
        var allocate=PsychicWard.Allocate;
        try{Ready(p);NecromancerEffects.SetMana256(p,mana);Require(ClassCombat.Perform(p,Ability,null,p.Location,Core.TickCount),"Ward allocation start");PsychicWard.Allocate=()=>false;
            Require(!ClassCombat.ResolveAction(p,action.EffectAt)&&PsychicWard.State(p)==null&&NecromancerEffects.ManaBalance256(p)==mana,"Ward allocation failure ordering/payment");}
        finally{PsychicWard.Allocate=allocate;}
        Cast(p);Require(NecromancerEffects.ManaBalance256(p)==p.ManaMax*256-cost,"Ward table payment");
        Log.Information("Ward learned cast:self WKSC/right/quick availability,unpaid/early/Stop/movement/late-mana controls,old-pool retention,allocation failure and one table debit pass.");
    }
    private static void Incoming(WardPlayer p)
    {
        var enemy=new ShockWaveTarget{Frozen=true,CantWalk=true,NoKillAwards=true};enemy.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);enemy.AIObject.AITimer.Stop();p.Watch=enemy;
        var contextual=ClassCombatHooks.PreparedWeaponDamageContext;var ordinary=ClassCombatHooks.PreparedWeaponDamage;int contexts=0,legacy=0;bool beforeHp=false;int hp=0;
        ClassCombatHooks.PreparedWeaponDamageContext=(a,b,physical,ranged,ctx)=>{if(b==p){contexts++;Require(SkillHealth.Balance256(p)==hp,"Ward native callback after HP");}contextual(a,b,physical,ranged,ctx);if(b==p){beforeHp=CombatStuns.Remaining(enemy)>0&&SkillHealth.Balance256(p)==hp;}};
        ClassCombatHooks.PreparedWeaponDamage=(a,b,physical,ranged)=>{if(b==p){legacy++;}ordinary(a,b,physical,ranged);};int cases=0;
        try
        {
            // Pool and expected loss are FP256. Damage is the actual native AOS input.
            foreach(var c in new[]{
                (Damage:10,Phys:100,Ignore:false,Weapon:true,Ranged:false,Pool:3000,Loss:0,React:true,Contexts:1,Legacy:0),
                (Damage:3,Phys:100,Ignore:false,Weapon:true,Ranged:false,Pool:768,Loss:0,React:true,Contexts:1,Legacy:0),
                (Damage:3,Phys:100,Ignore:false,Weapon:true,Ranged:false,Pool:640,Loss:128,React:true,Contexts:1,Legacy:0),
                (Damage:3,Phys:100,Ignore:false,Weapon:true,Ranged:false,Pool:256,Loss:512,React:true,Contexts:1,Legacy:0),
                (Damage:3,Phys:100,Ignore:true,Weapon:true,Ranged:false,Pool:640,Loss:128,React:true,Contexts:1,Legacy:0),
                (Damage:3,Phys:100,Ignore:true,Weapon:true,Ranged:false,Pool:768,Loss:0,React:true,Contexts:1,Legacy:0),
                (Damage:3,Phys:100,Ignore:false,Weapon:true,Ranged:true,Pool:640,Loss:128,React:false,Contexts:0,Legacy:1),
                (Damage:3,Phys:0,Ignore:false,Weapon:true,Ranged:false,Pool:641,Loss:768,React:false,Contexts:0,Legacy:1),
                (Damage:3,Phys:100,Ignore:false,Weapon:false,Ranged:false,Pool:640,Loss:128,React:false,Contexts:0,Legacy:0),
                (Damage:3,Phys:0,Ignore:true,Weapon:false,Ranged:false,Pool:641,Loss:768,React:false,Contexts:0,Legacy:0),
                (Damage:3,Phys:100,Ignore:true,Weapon:true,Ranged:true,Pool:768,Loss:0,React:false,Contexts:0,Legacy:0)})
            {
                Cast(p);Pool(p,c.Pool);SkillHealth.SetBalance256(p,p.HitsMax*256);hp=SkillHealth.Balance256(p);p.StunAtDamage=false;CombatStuns.Clear(enemy);contexts=legacy=0;beforeHp=false;
                if(cases==0){NecromancerEffects.SetMana256(p,0);}int manaBefore=NecromancerEffects.ManaBalance256(p);
                AOS.Damage(p,enemy,c.Damage,c.Ignore,c.Phys,100-c.Phys,0,0,0,archer:c.Ranged,weaponDamage:c.Weapon,spellDamage:!c.Weapon);
                Require(NecromancerEffects.ManaBalance256(p)==manaBefore&&SkillHealth.Balance256(p)==hp-c.Loss&&contexts==c.Contexts&&legacy==c.Legacy&&(CombatStuns.Remaining(enemy)>0)==c.React
                    &&(!c.React||beforeHp)&&(!c.React||c.Loss<256||p.StunAtDamage),$"Native Ward packet/order differs at{cases}:loss={hp-SkillHealth.Balance256(p)} context={contexts} legacy={legacy}");
                Require((PsychicWard.State(p)?.Remaining256??0)==Math.Max(0,c.Pool-c.Damage*c.Phys*256/100),"Native Ward pool/channel mismatch");cases++;
            }
            foreach(bool weapon in new[]{false,true})
            {
                Cast(p);Pool(p,640);CombatStuns.Clear(enemy);SkillHealth.SetBalance256(p,p.HitsMax*256);hp=SkillHealth.Balance256(p);
                SkillCombat.ResolveHitParts(enemy,p,new(3,0,0,0,0),false,weapon:weapon,enhanceBase:false,addWeaponElements:false);
                Require(SkillHealth.Balance256(p)==hp-128&&PsychicWard.State(p)==null&&(CombatStuns.Remaining(enemy)>0)==weapon,"Direct class delivery lost Ward context");
            }
            Cast(p);CombatStuns.Clear(enemy);enemy.Frozen=false;var hit=ClassCombatHooks.ClassHitCheck;var swing=ClassCombatHooks.CreatureSwing;
            try
            {
                ClassCombatHooks.CreatureSwing=(a,b)=>a==enemy?null:swing?.Invoke(a,b);ClassCombatHooks.ClassHitCheck=(a,b)=>a==enemy?false:hit?.Invoke(a,b);
                var original=PsychicWard.State(p);hp=SkillHealth.Balance256(p);((BaseWeapon)enemy.Weapon).OnSwing(enemy,p);
                Require(PsychicWard.State(p)==original&&SkillHealth.Balance256(p)==hp&&CombatStuns.Remaining(enemy)==0,"Nonlanded native swing triggered Ward");
                ClassCombatHooks.ClassHitCheck=(a,b)=>a==enemy?true:hit?.Invoke(a,b);contexts=0;beforeHp=false;((BaseWeapon)enemy.Weapon).OnSwing(enemy,p);
                Require(contexts==1&&beforeHp&&CombatStuns.Remaining(enemy)>0&&PsychicWard.State(p).Remaining256<original.Remaining256,"Landed native swing bypassed Ward bridge");
            }
            finally{ClassCombatHooks.ClassHitCheck=hit;ClassCombatHooks.CreatureSwing=swing;}
            Require(cases==11,"Missing native Ward packets");
            Log.Information("Ward automatic incoming:11native AOS packets plus direct weapon/spell delivery and native landed/nonlanded swing gates pass. Full absorption,depletion,fractional overflow,armor-ignore,ranged/elemental exclusions and single pre-HP callback retain shared stun order.");
        }
        finally{ClassCombatHooks.PreparedWeaponDamageContext=contextual;ClassCombatHooks.PreparedWeaponDamage=ordinary;p.Watch=null;CombatStuns.Clear(enemy);enemy.Delete();}
    }
    private static void Check(bool reload)
    {
        PsychicWardReactionProposal.RequireApproval();Manifest manifest;
        if(!reload)
        {
            var rows=new Saved[2];int i=0;
            foreach(bool female in new[]{false,true})
            {
                var p=Fresh(female,female?6920:6880);Casting(p);Incoming(p);Cast(p);Pool(p,641+i);p.Name="WardCastSaved";
                SkillHealth.SetBalance256(p,p.HitsMax*256-333-i);NecromancerEffects.SetMana256(p,p.ManaMax*256-517-i);
                rows[i++]=new(p.Serial.Value,female,SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),PsychicWard.State(p));
            }
            manifest=new(0,rows);
        }
        else
        {
            var old=JsonSerializer.Deserialize<Manifest>(File.ReadAllText(FileName));Require(old.Generation is 0 or 1,"Ward reload generation");
            foreach(var row in old.Owners)
            {
                var p=(WardPlayer)World.FindMobile((Serial)row.Owner);Require(p.Female==row.Female&&PsychicWard.State(p)==row.State&&SkillHealth.Balance256(p)==row.Life&&NecromancerEffects.ManaBalance256(p)==row.Mana,"Saved Ward original pool/resources changed");
                Require(ClassCombat.For(p).PendingAbility==CombatAbility.None,"Ward windup replayed after reload");
                FixtureSpace.Corridor(p,row.Female?6920:6880,24,3500);Incoming(p);
                PsychicWard.Restore(PsychicWard.Snapshot().Select(r=>r.Owner==row.Owner?row.State:r).ToArray());SkillHealth.SetBalance256(p,row.Life);NecromancerEffects.SetMana256(p,row.Mana);Ready(p);
            }
            manifest=old with{Generation=old.Generation+1};
        }
        File.WriteAllText(FileName,JsonSerializer.Serialize(manifest));
        Log.Information("Ward original:Warlock/Witch captured rank/stun,maximum/fractional pool and isolated wounds/mana persist;original actors resume automatic incoming reactions after both reloads;transient cast does not replay.");
    }
    private static void Input(NetState network,string mode)
    {
        var p=Fresh(mode=="quick",7000);if(mode=="quick")
        {while(p.RawStr<60){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Ward FCR strength");}var gloves=NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey=="Magefist").Id,99,new Random(173),true);p.AddToBackpack(gloves);Require(p.EquipItem(gloves),"Ward FCR gear");ClassGear.Refresh(p);}
        Require(SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Ward socket bindings");var account=FixtureNetwork.Authenticate(network,p,"WardWire");Ready(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);
        int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,Ability).Mana256;bool began=false,done=false;long deadline=Core.TickCount+6000;string label="ward-cast-"+mode;
        void Cleanup(){ClassCombat.Stop(p);p.Delete();account.Delete();PsychicWard.Prune();}
        void Inspect()
        {
            if(done){return;}try
            {
                if(Core.TickCount-deadline>=0){throw new InvalidOperationException("Ward wire timeout");}var s=ClassCombat.For(p);
                if(!began&&s.PendingAbility==Ability){Require(s.Timing==WarlockCastClock.Calculate(CombatTiming.Fcr(p))&&s.PendingTarget==Serial.Zero&&s.PendingPoint==p.Location&&NecromancerEffects.ManaBalance256(p)==mana,"Ward wire windup/self/payment");began=true;Log.Information("{Label}: windup",label);}
                if(PsychicWard.State(p) is not{} state){Timer.StartTimer(TimeSpan.FromMilliseconds(10),()=>Core.LoopContext.Post(Inspect));return;}
                Require(began&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Ward wire one-shot payment");
                var enemy=new ShockWaveTarget{Frozen=true,CantWalk=true,NoKillAwards=true};enemy.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);enemy.AIObject.AITimer.Stop();int hp=SkillHealth.Balance256(p);
                try{AOS.Damage(p,enemy,1,false,100,0,0,0,0,weaponDamage:true);Require(SkillHealth.Balance256(p)==hp&&PsychicWard.State(p).Remaining256==state.Remaining256-256&&CombatStuns.Remaining(enemy)>0,"Wire-created Ward failed actual automatic incoming reaction");}
                finally{CombatStuns.Clear(enemy);enemy.Delete();}
                done=true;Cleanup();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){done=true;Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)Ability,(int)DiabloClass.Warlock,0,p.X,p.Y,p.Z);Inspect();}));
    }
}
