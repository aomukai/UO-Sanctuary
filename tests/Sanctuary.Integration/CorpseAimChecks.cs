// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class CorpseAimChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(CorpseAimChecks));
    private sealed record Saved(uint Owner,uint Corpse,CorpseSkillRecord Row,Point3D Origin,int Map,int Mana,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"corpse-aim-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("corpse-aim-check",Check);Register("corpse-aim-reload-check",Reload);
        foreach(string mode in new[]{"right","quick"})
        {string label="corpse-ground-"+mode;Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,label,mode=="quick")));}
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Corpse Explosion near-click selection and original corpse conservation",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static PlayerMobile Owner(int row=3300)
    {
        var owner=PoisonDaggerChecks.Owner();owner.Name="PoisonDaggerFixtureCorpseAim";ClassSkillChecks.Train(owner,"necromancer.corpseexplosion");
        FixtureSpace.Corridor(owner,7000,24,row);Require(SkillBindings.Instance.Assign(owner,false,CombatAbility.CorpseExplosion)&&SkillBindings.Instance.AssignQuick(owner,7,CombatAbility.CorpseExplosion),"Corpse aim bindings");Ready(owner);return owner;
    }
    private static void Ready(PlayerMobile owner)
    {PoisonDaggerChecks.Ready(owner);ClassCombat.For(owner).RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(owner,owner.ManaMax*256);}
    private static Corpse Body(PlayerMobile owner,int dx=5,int dy=0)
    {var corpse=DeathSentryExplosionChecks.Body(owner,new(owner.X+dx,owner.Y+dy,owner.Z));corpse.Owner?.Delete();return corpse;}
    private static ulong Advanced(PlayerMobile owner,Corpse corpse)
    {
        var life=CorpseSkillRules.Profiles.Find(corpse).BaseLife;ulong seed=life.Seed.Value;var v=SkillMechanics.Values(owner,CombatAbility.CorpseExplosion);
        _=CorpseExplosionPackets.Create(life.Base256,ClassStats.Level(owner),life.Level,(int)v.Eval("aurarangecalc"),(int)v.Eval("calc1"),(int)v.Eval("calc2"),(int)v.Eval("calc3"),bound=>(int)SourceRandom.Limited(ref seed,bound));return seed;
    }
    private static void Cast(PlayerMobile owner,Corpse corpse,Point3D cursor)
    {
        Ready(owner);int mana=NecromancerEffects.ManaBalance256(owner),cost=SkillMechanics.Values(owner,CombatAbility.CorpseExplosion).Mana256;
        var row=CorpseSkillRules.Profiles.Find(corpse);ulong expected=Advanced(owner,corpse);
        Require(ClassCombat.BeginCorpse(owner,CombatAbility.CorpseExplosion,Serial.Zero,cursor,Core.TickCount),"Ground corpse begin failed");
        var session=ClassCombat.For(owner);long at=session.EffectAt;
        Require(session.PendingTarget==corpse.Serial&&NecromancerEffects.ManaBalance256(owner)==mana&&CorpseSkillRules.Profiles.Find(corpse)==row&&!corpse.SanctuaryConsumed,"Acquisition changed corpse,seed or mana during windup");
        Require(!ClassCombat.BeginCorpse(owner,CombatAbility.CorpseExplosion,Serial.Zero,owner.Location,Core.TickCount)&&session.PendingTarget==corpse.Serial&&session.EffectAt==at,"Repeated ground input replaced pending corpse or timing");
        Require(ClassCombat.ResolveAction(owner,at)&&corpse.SanctuaryConsumed&&CorpseSkillRules.NoSelect(corpse)&&corpse.GetAmount(typeof(Gold))==1
            &&CorpseSkillRules.Profiles.Find(corpse).BaseLife.Seed==expected&&NecromancerEffects.ManaBalance256(owner)==mana-cost,"Acquired release lost once-only corpse/loot/seed/payment");
        Require(!ClassCombat.ResolveAction(owner,at)&&NecromancerEffects.ManaBalance256(owner)==mana-cost,"Corpse action replayed");
    }
    private static void Save(PlayerMobile owner,int generation)
    {
        Ready(owner);var corpse=Body(owner);var saved=new Saved(owner.Serial.Value,corpse.Serial.Value,CorpseSkillRules.Profiles.Find(corpse),owner.Location,owner.Map.MapID,NecromancerEffects.ManaBalance256(owner),generation);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));owner.Internalize();
    }
    private static void Check()
    {
        var owner=Owner();var a=Body(owner);var b=Body(owner,6,1);var row=CorpseSkillRules.Profiles.Find(a);var other=CorpseSkillRules.Profiles.Find(b);var tie=new Point3D(owner.X+6,owner.Y,owner.Z);
        try
        {
            Require(CorpseExplosionCasts.Acquire(owner,new(b.X,b.Y+1,b.Z))==b,"Corpse selection used caster proximity rather than cursor");
            Require(CorpseExplosionCasts.Acquire(owner,tie)==(a.Serial.Value<b.Serial.Value?a:b),"Corpse exact tie lost explicit serial policy");
            Require(CorpseSkillRules.Profiles.Find(a)==row&&CorpseSkillRules.Profiles.Find(b)==other,"Selection advanced a corpse seed or reserved it");
            foreach(var bad in new[]{row with{Owned=true},row with{Selectable=false},row with{Shattered=true},row with{NoSelect=true},row with{BaseLife=null},row with{BaseLife=row.BaseLife with{Seed=null}}})
            {CorpseSkillRules.Profiles.Store(bad);Require(CorpseExplosionCasts.Acquire(owner,tie)==b,"Ineligible closest corpse blocked the next usable corpse");}
            CorpseSkillRules.Profiles.Store(row);b.MoveToWorld(b.Location,Map.Felucca);
            Require(CorpseExplosionCasts.Acquire(owner,new(a.X+2,a.Y,a.Z))==a,"Corpse near-click circle lost its boundary");
            Require(CorpseExplosionCasts.Acquire(owner,new(a.X+3,a.Y,a.Z))==null&&CorpseExplosionCasts.Acquire(owner,new(a.X+2,a.Y+2,a.Z))==null,"Corpse selection reached outside its circle");
            var original=a.Location;a.MoveToWorld(new(owner.X+9,owner.Y,owner.Z),owner.Map);Require(CorpseExplosionCasts.Acquire(owner,a.Location)==null,"Acquisition expanded existing corpse range");
            a.MoveToWorld(new(original.X,original.Y,original.Z+20),owner.Map);Require(CorpseExplosionCasts.Acquire(owner,original)==null,"Corpse acquisition ignored height");a.MoveToWorld(original,owner.Map);
            var target=DeathSentryExplosionChecks.Target(a,1);target.Frozen=target.CantWalk=true;TimedEffects.PreventHealing(target,TimedEffects.Now);int hp=SkillHealth.Balance256(target);
            try{Cast(owner,a,tie);Require(SkillHealth.Balance256(target)<hp,"Ground-selected corpse did not deliver its existing explosion damage");}finally{target.Delete();}
            Ready(owner);var pending=Body(owner,4);var survivor=Body(owner,6);int mana=NecromancerEffects.ManaBalance256(owner);
            try
            {
                Require(ClassCombat.BeginCorpse(owner,CombatAbility.CorpseExplosion,Serial.Zero,pending.Location,Core.TickCount),"Loss control begin failed");
                var session=ClassCombat.For(owner);CorpseSkillRules.Reserve(pending);_=ClassCombat.ResolveAction(owner,session.EffectAt);
                Require(NecromancerEffects.ManaBalance256(owner)==mana&&!pending.SanctuaryConsumed&&!survivor.SanctuaryConsumed,"Consumed primary silently reacquired or charged");
            }
            finally{pending.Delete();survivor.Delete();}
            Ready(owner);Require(!ClassCombat.BeginCorpse(owner,CombatAbility.CorpseExplosion,Serial.Zero,owner.Location,Core.TickCount)&&NecromancerEffects.ManaBalance256(owner)==mana,"Empty ground cast or spent mana");
            a.Delete();b.Delete();Save(owner,0);
        }
        catch{a.Delete();b.Delete();owner.Delete();throw;}
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var corpse=(Corpse)World.FindItem((Serial)saved.Corpse);
        owner.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);
        Require(corpse.Owner==null&&CorpseSkillRules.Profiles.Find(corpse)==saved.Row&&corpse.GetAmount(typeof(Gold))==1&&NecromancerEffects.ManaBalance256(owner)==saved.Mana,"Saved orphan corpse,loot,seed or mana changed");
        Cast(owner,corpse,new(corpse.X+1,corpse.Y,corpse.Z));corpse.Delete();Save(owner,saved.Generation+1);
    }
    private static void Input(NetState net,string label,bool quick)
    {
        var owner=Owner(quick?3450:3400);var corpse=Body(owner);var unrelated=Body(owner,1);var target=DeathSentryExplosionChecks.Target(corpse,1);
        target.Frozen=target.CantWalk=true;TimedEffects.PreventHealing(target,TimedEffects.Now);var cursor=new Point3D(corpse.X+1,corpse.Y,corpse.Z);
        int mana=NecromancerEffects.ManaBalance256(owner),cost=SkillMechanics.Values(owner,CombatAbility.CorpseExplosion).Mana256,hp=SkillHealth.Balance256(target);var before=CorpseSkillRules.Profiles.Find(corpse);var other=CorpseSkillRules.Profiles.Find(unrelated);ulong advanced=Advanced(owner,corpse);
        var account=FixtureNetwork.Authenticate(net,owner,"corpse-ground-");bool began=false,finished=false;long deadline=Core.TickCount+10000;
        void End(){ClassCombat.Stop(owner);target.Delete();corpse.Delete();unrelated.Delete();owner.Delete();account.Delete();}
        void Inspect()
        {
            if(finished){return;}
            try
            {
                Require(net.Running&&Core.TickCount<deadline,"Ground corpse input timeout");var session=ClassCombat.For(owner);
                if(session.PendingAbility==CombatAbility.CorpseExplosion&&!began)
                {Require(session.PendingTarget==corpse.Serial&&NecromancerEffects.ManaBalance256(owner)==mana&&CorpseSkillRules.Profiles.Find(corpse)==before&&!corpse.SanctuaryConsumed,"Actual ground input lost primary or paid/consumed early");began=true;Log.Information("{Label}: windup",label);}
                if(corpse.SanctuaryConsumed)
                {
                    Require(began&&NecromancerEffects.ManaBalance256(owner)==mana-cost&&CorpseSkillRules.NoSelect(corpse)&&CorpseSkillRules.Profiles.Find(corpse).BaseLife.Seed==advanced
                        &&corpse.GetAmount(typeof(Gold))==1&&SkillHealth.Balance256(target)<hp&&!unrelated.SanctuaryConsumed&&CorpseSkillRules.Profiles.Find(unrelated)==other,"Actual ground input lost damage,loot,seed,payment or untouched corpse");
                    finished=true;End();Log.Information("{Label}-result: passed",label);return;
                }
                Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Inspect));
            }
            catch(Exception e){finished=true;End();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target=0 x={X} y={Y} z={Z}",label,(int)CombatAbility.CorpseExplosion,(int)DiabloClass.Necromancer,cursor.X,cursor.Y,cursor.Z);Inspect();});
    }
}
