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

public static class MirroredCastChecks
{
    private const CombatAbility Ability=CombatAbility.MirroredBlades;
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(MirroredCastChecks));
    private sealed record Saved(int Generation,uint Owner,uint Weapon,int Life,int Mana);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"mirrored-cast-fixtures.json");
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    [CallPriority(int.MaxValue)]public static void Initialize()
    {
        var hp=ClassGearHooks.CanRegenerateHits;var mp=ClassGearHooks.CanRegenerateMana;
        ClassGearHooks.CanRegenerateHits=p=>!(p.Name?.StartsWith("MirroredCast",StringComparison.Ordinal)==true)&&hp?.Invoke(p)!=false;
        ClassGearHooks.CanRegenerateMana=p=>!(p.Name?.StartsWith("MirroredCast",StringComparison.Ordinal)==true)&&mp?.Invoke(p)!=false;
    }
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {
            bool reload=again;string name=reload?"mirrored-cast-reload-check":"mirrored-cast-check";
            ConsoleInputHandler.RegisterCommand(name,"Approved learned Mirrored SQ25/bindings/original saves",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>
            {try{if(reload){Reload(n);}else{Scene(n);}Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});}));
        }
        foreach(string choice in new[]{"left","right","quick","crossbow"})
        {
            string mode=choice,name=choice=="crossbow"?"mirrored-crossbow-quick":"mirrored-cast-"+choice;
            ConsoleInputHandler.RegisterCommand(name+"-check","Real learned Mirrored weapon inputs and automatic contacts",_=>Core.LoopContext.Post(()=>
            {try{FixtureNetwork.Connected(name+"-result",n=>Input(n,mode,name));Log.Information("{Name}: passed",name+"-check");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-check");}}));
        }
    }
    private static void Ready(PlayerMobile p){ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;p.Frozen=p.Paralyzed=false;}
    private static PlayerMobile Fresh(int kind)
    {
        var p=MirroredWorldChecks.Fresh(kind);p.Name="MirroredCastFixture";
        if(kind==0)
        {
            p.AddToBackpack(CombatTiming.Equipped(p));var sword=new Broadsword();ClassGear.Profiles.Convert(sword,99,GearRarity.Normal,new Random(173));
            p.AddToBackpack(sword);Require(p.EquipItem(sword),"Nonleeching original learned sword");p.Female=true;p.Body=0x191;
        }
        FixtureSpace.Corridor(p,6200+kind*40,24,3400);ClassGear.Refresh(p);Ready(p);return p;
    }
    private static GoatmanNpcContactChecks.Recipient Target(PlayerMobile p,int distance)
    {
        var t=new GoatmanNpcContactChecks.Recipient{Name="MirroredCastTarget",Frozen=true,CantWalk=true,NoKillAwards=true};t.SetHits(50000);t.Hits=t.HitsMax;
        t.MoveToWorld(new(p.X+distance,p.Y,p.Z),p.Map);t.AIObject?.AITimer.Stop();return t;
    }
    private static void Bind(PlayerMobile p)
    {Require(MirroredBladesAction.Approved&&MirroredBladesPackets.Approved&&ClassCombat.Available(p,Ability)&&!ClassAbilities.SelfTarget(Ability)
        &&SkillBindings.Instance.Assign(p,true,Ability)&&SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Approved learned targeting/bindings");}
    private static void Cast(PlayerMobile p)
    {
        var target=Target(p,1);var hit=MirroredBladesContacts.HitRoll;MirroredBladesContacts.HitRoll=()=>0;
        try
        {
            Ready(p);NecromancerEffects.SetMana256(p,100*256+17);int mana=NecromancerEffects.ManaBalance256(p),hp=SkillHealth.Balance256(target);
            Require(ClassCombat.Perform(p,Ability,target,target.Location,Core.TickCount),"Learned ordinary start");var a=ClassCombat.For(p).Mirrored;
            Require(a!=null&&a.Timing==MirroredBladesAction.Timing(p,MirroredBladesPackets.Capture(p).Skill),"Learned source SQ25");
            ClassCombat.ResolveAction(p,a.EffectAt-1);Require(!a.Released&&NecromancerEffects.ManaBalance256(p)==mana,"Learned early debit");
            Require(ClassCombat.ResolveAction(p,a.EffectAt)&&a.Released&&a.Attempts==3&&a.Successful==3&&SkillHealth.Balance256(target)<hp,"Learned ordered contacts");
            int after=NecromancerEffects.ManaBalance256(p);ClassCombat.ResolveAction(p,a.EffectAt);
            Require(after==mana-a.ManaPaid256&&NecromancerEffects.ManaBalance256(p)==after,"Learned payment/replay");Ready(p);
        }
        finally{MirroredBladesContacts.HitRoll=hit;target.Delete();}
    }
    private static void Scene(NetState n)
    {
        var p=Fresh(0);var account=FixtureNetwork.Authenticate(n,p,"mirrored-learned-");bool kept=false;
        try
        {
            Bind(p);Cast(p);SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-513);NecromancerEffects.SetMana256(p,p.ManaMax*256-17);p.Name="MirroredCastSaved";
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,CombatTiming.Equipped(p).Serial.Value,SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p))));kept=true;
            Log.Information("Mirrored learned:approved ordinary dispatch/SQ25/all bindings,unpaid windup,three independent contacts,one fractional debit/no replay pass");
        }
        finally{ClassCombat.Stop(p);n.Mobile=null;p.NetState=null;account[0]=null;n.Account=null;account.Delete();if(!kept){p.Delete();}}
    }
    private static void Reload(NetState n)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var account=FixtureNetwork.Authenticate(n,p,"mirrored-learned-reload-");
        try
        {
            Require(p.Female&&CombatTiming.Equipped(p)?.Serial.Value==saved.Weapon&&SkillHealth.Balance256(p)==saved.Life&&NecromancerEffects.ManaBalance256(p)==saved.Mana
                &&ClassCombat.For(p).Mirrored==null&&ClassCombat.For(p).PendingAbility==CombatAbility.None,"Saved original learned owner/weapon/resources or replay");
            var b=SkillBindings.Instance.Get(p);Require(b.Left==Ability&&b.Right==Ability&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Warlock,7)==Ability,"Saved learned bindings");
            FixtureSpace.Corridor(p,6200,24,3400);Cast(p);Ready(p);SkillHealth.SetBalance256(p,saved.Life);NecromancerEffects.SetMana256(p,saved.Mana);
            File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
            Log.Information("Mirrored learned:saved Witch/original sword/fractional resources/all bindings retain;post-load learned action works without pending replay");
        }
        finally{ClassCombat.Stop(p);n.Mobile=null;p.NetState=null;account[0]=null;n.Account=null;account.Delete();}
    }
    private static void Input(NetState n,string mode,string label)
    {
        int kind=mode switch{"left"=>0,"right"=>1,"crossbow"=>2,_=>3};var p=Fresh(kind);p.Name="MirroredCastWire";Bind(p);
        var target=Target(p,kind==0?1:4);var account=FixtureNetwork.Authenticate(n,p,"mirrored-wire-");var oldHit=MirroredBladesContacts.HitRoll;MirroredBladesContacts.HitRoll=()=>0;
        Ready(p);NecromancerEffects.SetMana256(p,100*256+17);int mana=NecromancerEffects.ManaBalance256(p),hp=SkillHealth.Balance256(target),contacts=0;
        var weapon=CombatTiming.Equipped(p);int arrows=BowAmmunition.Stack(p,weapon)?.Amount??0,quantity=NamedAmmunition.Remaining(weapon);
        bool began=false,done=false;long deadline=Core.TickCount+8000;MirroredBladesAction.Pending action=null;
        void Landed(WeaponLandedContact e){if(e.Source==p&&e.Target==target){contacts++;}}
        WeaponLandedEvents.BeforeDamage+=Landed;
        void Cleanup(){WeaponLandedEvents.BeforeDamage-=Landed;MirroredBladesContacts.HitRoll=oldHit;WeaponMissiles.CancelMirrored(p);ClassCombat.Stop(p);target.Delete();p.Delete();account.Delete();}
        void Inspect()
        {
            if(done){return;}
            try
            {
                if(Core.TickCount>=deadline){throw new InvalidOperationException("Mirrored wire action/contact timeout");}
                var s=ClassCombat.For(p);
                if(!began&&s.PendingAbility==Ability&&s.Mirrored is{} pending)
                {Require(s.PendingTarget==target.Serial&&NecromancerEffects.ManaBalance256(p)==mana,"Wire target/unpaid windup");action=pending;began=true;Log.Information("{Label}: windup",label);}
                if(action is{Released:true}&&contacts==3&&SkillHealth.Balance256(target)<hp)
                {
                    Require(began&&action.Attempts==3&&action.Fault==null&&action.ManaPaid256==8*256&&NecromancerEffects.ManaBalance256(p)==mana-action.ManaPaid256,"Wire one payment/three automatic contacts");
                    if(kind is 1 or 2){Require(BowAmmunition.Stack(p,weapon)?.Amount==arrows-1,"Wire quiver debit");}
                    if(kind==3){Require(NamedAmmunition.Remaining(weapon)==quantity-1,"Wire throw debit");}
                    done=true;Cleanup();Log.Information("{Label}-result: passed",label);return;
                }
                Timer.StartTimer(TimeSpan.FromMilliseconds(10),()=>Core.LoopContext.Post(Inspect));
            }
            catch(Exception e){done=true;Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(()=>
        {Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)Ability,(int)DiabloClass.Warlock,target.Serial.Value,target.X,target.Y,target.Z);Inspect();}));
    }
}
