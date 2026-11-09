// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public sealed class CleaveActionTarget:GraveyardSkeleton
{
    internal static Action<CleaveActionTarget> Damaged;
    public CleaveActionTarget(){}
    public CleaveActionTarget(Serial serial):base(serial){}
    public override bool CanRegenHits=>false;
    public override void OnThink(){}
    public override void OnSectorActivate()=>AIObject?.AITimer.Stop();
    public override void OnDamage(int amount,Mobile from,bool willKill){Damaged?.Invoke(this);base.OnDamage(amount,from,willKill);}
    public override void Serialize(IGenericWriter w){base.Serialize(w);w.Write(0);}
    public override void Deserialize(IGenericReader r){base.Deserialize(r);if(r.ReadInt()!=0){throw new InvalidDataException("Unknown Cleave fixture");}}
}
public static class CleaveActionChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(CleaveActionChecks));
    private sealed record Saved(int Generation,uint Owner,uint Weapon,uint Target,int X,int Y,int Mana256,int Life256);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"cleave-action-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Prepared complete Cleave action rule",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure()
    {
        Register("cleave-action-check",()=>Check(false));Register("cleave-action-reload-check",()=>Check(true));
        ConsoleInputHandler.RegisterCommand("cleave-action-live","Connected natural prepared Cleave clock",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("cleave-action-live-result",Live)));
    }
    public static void Initialize()
    {
        var hp=ClassGearHooks.CanRegenerateHits;var mp=ClassGearHooks.CanRegenerateMana;
        ClassGearHooks.CanRegenerateHits=p=>p.Name!="CleaveActionSaved"&&hp?.Invoke(p)!=false;
        ClassGearHooks.CanRegenerateMana=p=>p.Name!="CleaveActionSaved"&&mp?.Invoke(p)!=false;
    }
    internal static void Ready(PlayerMobile p){ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;p.Frozen=p.Paralyzed=false;NecromancerEffects.SetMana256(p,p.ManaMax*256);}
    internal static PlayerMobile Fresh(bool female=false)
    {
        var p=new PlayerMobile{Player=true,Female=female,Body=female?0x191:0x190,Name="CleaveActionSaved"};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Warlock);ClassCreation.Profiles.AdvanceLevel(p,99);ClassSkillChecks.Train(p,"warlock.cleave");
        while(p.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Cleave Strength");}FixtureSpace.Corridor(p,6560,20,3500);
        var w=new Broadsword();p.AddToBackpack(w);Require(p.EquipItem(w),"Cleave sword");ClassGear.Refresh(p);Ready(p);return p;
    }
    internal static CleaveActionTarget Enemy(PlayerMobile p,int dx,int dy=0)
    {
        var t=new CleaveActionTarget{Frozen=true,CantWalk=true,NoKillAwards=true};t.MoveToWorld(new(p.X+dx,p.Y+dy,p.Z),p.Map);t.AIObject.AITimer.Stop();t.SetHits(50000);SkillHealth.SetBalance256(t,t.HitsMax*256);t.SetResistance(ResistanceType.Physical,0);return t;
    }
    private static CleaveActionProposal.Action Start(PlayerMobile p,Serial selected=default,Point3D? aim=null)
    {var action=CleaveActionProposal.BeginPrepared(p,selected,aim??new(p.X+4,p.Y,p.Z),Core.TickCount);Require(action!=null,"Prepared Cleave did not begin");return action;}
    private static void Release(CleaveActionProposal.Action a){Require(CleaveActionProposal.Advance(a,a.EffectAt)&&a.Released&&a.Fault==null,"Prepared Cleave release "+a.Fault);}
    private static void End(CleaveActionProposal.Action a){CleaveActionProposal.Advance(a,a.Until);Require(a.Finished&&ClassCombat.For(a.Owner).Cleave==null,"Cleave end did not clear transient state");}
    private static void Exercise(PlayerMobile p)
    {
        var targets=new[]{Enemy(p,1),Enemy(p,1,1),Enemy(p,-1),Enemy(p,5),Enemy(p,0,1),Enemy(p,-1,-1)};var front=targets[0];var side=targets[4];
        var hit=CleaveActionProposal.HitRoll;var element=CleaveActionProposal.ElementRoll;var physical=CleaveActionProposal.PhysicalRoll;var poison=CleaveActionProposal.PoisonRoll;var effects=CleaveActionProposal.Effects;var block=SourceBlocking.BlockRoll;
        CleaveActionProposal.HitRoll=()=>0;CleaveActionProposal.ElementRoll=()=>0;CleaveActionProposal.PhysicalRoll=_=>0;CleaveActionProposal.PoisonRoll=_=>0;CleaveActionProposal.Effects=new Random(12);SourceBlocking.BlockRoll=n=>n-1;
        try
        {
            Ready(p);int mp=NecromancerEffects.ManaBalance256(p);var hp=targets.Select(SkillHealth.Balance256).ToArray();var a=Start(p);
            // The same original actor retains rank19 after the first boot.
            // Golden bearings:forward/45deg,back180deg,outside reach,90deg,135deg.
            bool[] expected=[true,true,false,false,a.Values.RawSlices>=15,a.Values.RawSlices>=23];int expectedContacts=expected.Count(x=>x);
            Require(a.Timing==CleaveActionProposal.Timing(p,a.Values)&&a.Timing.EffectFrame<a.Timing.Frames&&mp==NecromancerEffects.ManaBalance256(p),"Cleave entry clock/payment");
            CleaveActionProposal.Advance(a,a.EffectAt-1);Require(!a.Released&&targets.Select(SkillHealth.Balance256).SequenceEqual(hp),"Cleave damaged before event");
            int nested=0;CleaveActionTarget.Damaged=_=>{nested++;Require(!CleaveActionProposal.Advance(a,a.Until),"Nested Cleave advanced delivering action");};Release(a);CleaveActionTarget.Damaged=null;
            Require(a.Contacts==expectedContacts&&a.Successful==expectedContacts&&nested==expectedContacts&&mp-NecromancerEffects.ManaBalance256(p)==3*256&&a.ManaPaid256==3*256,"Cleave fan/ordinary contacts/reentrant payment");
            Require(targets.Select((t,i)=>expected[i]?SkillHealth.Balance256(t)<hp[i]:SkillHealth.Balance256(t)==hp[i]).All(x=>x),"Cleave front/diagonal/rear/range/side membership");
            var after=targets.Select(SkillHealth.Balance256).ToArray();CleaveActionProposal.Advance(a,a.EffectAt);Require(targets.Select(SkillHealth.Balance256).SequenceEqual(after),"Cleave event replayed");End(a);

            Ready(p);a=Start(p);mp=NecromancerEffects.ManaBalance256(p);var origin=p.Location;p.MoveToWorld(new(p.X,p.Y+1,p.Z),p.Map);p.MoveToWorld(origin,p.Map);CleaveActionProposal.Advance(a,a.EffectAt);
            Require(a.Cancelled&&!a.Released&&mp==NecromancerEffects.ManaBalance256(p),"Move-away/return failed to cancel unpaid Cleave");
            Ready(p);a=Start(p);ClassCombat.Stop(p);CleaveActionProposal.Advance(a,a.EffectAt);Require(a.Cancelled&&!a.Released,"Explicit Cleave cancellation replayed");
            Ready(p);a=Start(p);var weapon=CombatTiming.Equipped(p);p.AddToBackpack(weapon);Require(p.EquipItem(weapon),"Cleave re-equip");ClassGear.Refresh(p);CleaveActionProposal.Advance(a,a.EffectAt);
            Require(a.Cancelled&&!a.Released,"Weapon-away/re-equip failed to cancel Cleave");
            Ready(p);a=Start(p);CleaveActionProposal.Action replacement=null;mp=NecromancerEffects.ManaBalance256(p);
            CleaveActionTarget.Damaged=_=>
            {
                if(replacement!=null){return;}ClassCombat.Stop(p);var session=ClassCombat.For(p);session.NextAction=session.RecoveryUntil=Core.TickCount;
                replacement=CleaveActionProposal.BeginPrepared(p,Serial.Zero,new(p.X+4,p.Y,p.Z),Core.TickCount);Require(replacement!=null,"Replacement prepared action did not start");
            };
            Release(a);CleaveActionTarget.Damaged=null;
            Require(a.Cancelled&&a.Contacts==1&&replacement!=null&&!replacement.Cancelled&&ReferenceEquals(ClassCombat.For(p).Cleave,replacement)
                &&mp-NecromancerEffects.ManaBalance256(p)==3*256,"Interrupted sweep cleared/replayed replacement action");ClassCombat.Stop(p);

            Ready(p);a=Start(p);front.IsParagon=true;mp=NecromancerEffects.ManaBalance256(p);hp=targets.Select(SkillHealth.Balance256).ToArray();CleaveActionProposal.Advance(a,a.EffectAt);
            Require(a.Cancelled&&a.Fault!=null&&mp==NecromancerEffects.ManaBalance256(p)&&targets.Select(SkillHealth.Balance256).SequenceEqual(hp),"Unsupported receiver spent mana or partially hit fan");front.IsParagon=false;

            Ready(p);NecromancerEffects.SetMana256(p,3*256-1);mp=NecromancerEffects.ManaBalance256(p);a=Start(p,front.Serial);Release(a);
            Require(a.BasicAtEntry&&a.UsedBasic&&a.Contacts==1&&a.ManaPaid256==0&&mp==NecromancerEffects.ManaBalance256(p)&&a.Timing==CombatTiming.For(p,CombatAbility.BasicAttack),"Entry low mana ordinary attack fallback");End(a);
            Ready(p);a=Start(p);NecromancerEffects.SetMana256(p,0);Release(a);Require(a.UsedBasic&&a.Contacts==1&&a.ManaPaid256==0,"Release low mana ordinary attack fallback");End(a);
            Ready(p);a=Start(p);CleaveActionProposal.HitRoll=()=>1;hp=targets.Select(SkillHealth.Balance256).ToArray();Release(a);
            Require(a.Contacts==expectedContacts&&a.Successful==0&&a.ManaPaid256==3*256&&targets.Select(SkillHealth.Balance256).SequenceEqual(hp),"Missed Cleave refunded or damaged");End(a);CleaveActionProposal.HitRoll=()=>0;

            // Hold the chosen aim even when the original target dies. Nearby
            // enemies are acquired at release rather than retaining a dead unit.
            Ready(p);a=Start(p,front.Serial);front.Delete();hp=targets.Select(t=>t.Deleted?0:SkillHealth.Balance256(t)).ToArray();Release(a);
            Require(a.Contacts==expectedContacts-1&&a.Successful==expectedContacts-1&&SkillHealth.Balance256(targets[1])<hp[1],"Dead primary cancelled sweep or retained stale target");End(a);

            // +skill19->20 narrows the table's arc. Removing it during windup
            // cannot rewrite the captured rank,arc,damage or clock.
            ClassSkillChecks.Train(p,"warlock.cleave",19);var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(27),true);p.AddToBackpack(helm);Require(p.EquipItem(helm),"Cleave snapshot helm");ClassGear.Refresh(p);
            Ready(p);a=Start(p);var timing=a.Timing;Require(a.Values.Rank==20&&a.Values.RawSlices==20,"Cleave snapshot rank20");p.AddToBackpack(helm);ClassGear.Refresh(p);Require(SkillMechanics.Rank(p,CombatAbility.Cleave)==19,"Cleave live rank19");
            int rear=SkillHealth.Balance256(targets[5]),lateral=SkillHealth.Balance256(side);Release(a);
            Require(a.Values.Rank==20&&a.Timing==timing&&SkillHealth.Balance256(targets[5])==rear&&SkillHealth.Balance256(side)<lateral,"Cleave recaptured arc/rank or ignored captured240degree fan");End(a);helm.Delete();
            Ready(p);a=Start(p);rear=SkillHealth.Balance256(targets[5]);Release(a);Require(a.Values.Rank==19&&a.Values.RawSlices==29&&SkillHealth.Balance256(targets[5])<rear,"Pinned348degree rank19 fan lost");End(a);

            foreach(var target in targets){target.Delete();}Ready(p);a=Start(p);mp=NecromancerEffects.ManaBalance256(p);Release(a);
            Require(a.Contacts==0&&a.ManaPaid256==3*256&&mp-NecromancerEffects.ManaBalance256(p)==3*256,"Empty released sweep unexpectedly free");End(a);
            Require(CleaveActionProposal.Approved&&WarlockCombat.Implemented(CombatAbility.Cleave)&&SkillBindings.Instance.Assign(p,false,CombatAbility.Cleave),"Approved Cleave learned input unavailable");
            Log.Information("Cleave action candidate:SQ24/EIAS captured clock,132/240/348degree fans,independent ordinary contacts,once payment/no replay,move/equipment/cancel/preflight,mana basic fallback,miss/dead-primary/empty sweep andrank snapshot pass.");
        }
        finally
        {
            ClassCombat.Stop(p);CleaveActionTarget.Damaged=null;foreach(var t in targets){t.Delete();SkillHealth.Clear(t);}
            CleaveActionProposal.HitRoll=hit;CleaveActionProposal.ElementRoll=element;CleaveActionProposal.PhysicalRoll=physical;CleaveActionProposal.PoisonRoll=poison;CleaveActionProposal.Effects=effects;SourceBlocking.BlockRoll=block;
        }
    }
    private static void Check(bool reload)
    {
        PlayerMobile p;CleaveActionTarget target;int generation=0;
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=saved.Generation+1;p=World.FindMobile((Serial)saved.Owner)as PlayerMobile;target=World.FindMobile((Serial)saved.Target)as CleaveActionTarget;
            Require(p!=null&&target!=null&&CombatTiming.Equipped(p)?.Serial.Value==saved.Weapon&&SkillHealth.Balance256(p)==saved.Life256&&NecromancerEffects.ManaBalance256(p)==saved.Mana256,"Original Cleave actor/weapon/resources changed");
            Require(ClassCombat.For(p).Cleave==null&&ClassCombat.For(p).PendingAbility==CombatAbility.None,"Transient Cleave resumed after reload");FixtureSpace.Place(p,new(saved.X,saved.Y,0),Map.Trammel);target.MoveToWorld(new(p.X+8,p.Y,p.Z),p.Map);target.AIObject.AITimer.Stop();
        }
        else{p=Fresh();target=Enemy(p,8);}
        Exercise(p);Ready(p);target.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);SkillHealth.SetBalance256(p,p.HitsMax*256-129);NecromancerEffects.SetMana256(p,p.ManaMax*256-17);
        // Future-dated prepared control cannot release between this command
        // and save;the transient action must never restart after load.
        var pending=CleaveActionProposal.BeginPrepared(p,target.Serial,target.Location,Core.TickCount+60000);Require(pending!=null&&!pending.Released,"Cleave pre-save transient control");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(generation,p.Serial.Value,CombatTiming.Equipped(p).Serial.Value,target.Serial.Value,p.X,p.Y,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(p))));
    }
    private static void Live(Server.Network.NetState network)
    {
        var p=Fresh(true);var target=Enemy(p,1);FixtureNetwork.Authenticate(network,p,"cleave-live-");Ready(p);int mana=NecromancerEffects.ManaBalance256(p),hp=SkillHealth.Balance256(target);
        var hit=CleaveActionProposal.HitRoll;var block=SourceBlocking.BlockRoll;CleaveActionProposal.HitRoll=()=>0;SourceBlocking.BlockRoll=n=>n-1;var a=Start(p,target.Serial);
        Log.Information("cleave-action-live: ready");
        Timer.StartTimer(TimeSpan.FromMilliseconds(a.Timing.DurationMs+250),()=>Core.LoopContext.Post(()=>
        {
            try{Require(a.Finished&&!a.Cancelled&&a.Contacts==1&&a.Successful==1&&hp>SkillHealth.Balance256(target)&&mana-NecromancerEffects.ManaBalance256(p)==3*256,"Natural connected prepared Cleave failed");Log.Information("cleave-action-live-result: passed");}
            catch(Exception e){Log.Error(e,"cleave-action-live-result: failed");}
            finally{CleaveActionProposal.HitRoll=hit;SourceBlocking.BlockRoll=block;ClassCombat.Stop(p);p.Delete();target.Delete();}
        }));
    }
}
