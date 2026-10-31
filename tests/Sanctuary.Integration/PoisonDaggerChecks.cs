// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class PoisonDaggerChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(PoisonDaggerChecks));
    private sealed record Saved(uint Owner,uint Target,uint Weapon,uint Helm,int Balance,TimedEffectState Poison,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"poison-dagger-fixtures.json");
    private static bool Creating;
    public static void Configure()
    {
        Rates();Register("poison-dagger-check",Check);Register("poison-dagger-reload-check",Reload);
        foreach(bool quick in new[]{false,true})
        {bool captured=quick;Register(captured?"poison-dagger-quick-check":"poison-dagger-right-check",()=>FixtureNetwork.Connected(captured?"poison-dagger-quick-result":"poison-dagger-right-result",n=>Input(n,captured)));}
    }
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Rates()
    {
        var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;
        Mobile.HitsRegenRateHandler=x=>Creating||x.Name?.StartsWith("PoisonDaggerFixture",StringComparison.Ordinal)==true?TimeSpan.FromHours(1):hp?.Invoke(x)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=x=>Creating||x.Name?.StartsWith("PoisonDaggerFixture",StringComparison.Ordinal)==true?TimeSpan.FromHours(1):mp?.Invoke(x)??Mobile.DefaultManaRate;
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Poison Dagger combined source packet and learned delivery",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static SkillValues Values(int rank,int hard)=>new(SkillMechanics.For(CombatAbility.PoisonDagger),k=>k=="Poison Dagger"?rank:255,k=>k=="Poison Explosion"?Math.Min(20,hard):k=="Poison Nova"?Math.Max(0,hard-20):0,99);
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"poison-dagger-source-cases.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            int V(int i)=>row[i].GetInt32();int bound=-1;
            var skill=PoisonDaggerPackets.Skill(Values(V(0),V(1)),V(2),n=>{bound=n;return V(8)!=0?n-1:0;});
            Require(skill==new WeaponPoisonRoll(V(13),V(12))&&bound==V(14),"Complete skill poison roll differs at "+count);
            var raw=new MissileSourceStats(PoisonMin:V(3),PoisonMax:V(4),PoisonFrames:V(5),PoisonCount:V(6));bound=-1;
            var packet=PoisonDaggerPackets.Combine(raw,V(2),skill,V(7),n=>{bound=n;return V(9)!=0?n-1:0;});
            Require(packet==new WeaponPoisonRoll(V(16),V(17))&&bound==V(15),"Combined skill/item poison differs at "+count);count++;
        }
        Require(count==6912,"Missing Poison Dagger reference cases");
        Log.Information("Poison Dagger:6912 complete elemental-roll/shared-poison-lane comparisons;soft synergy ranks do not enter hard bonuses");
    }
    private static PlayerMobile Owner()
    {
        PlayerMobile p;Creating=true;try{p=new PlayerMobile{Name="PoisonDaggerFixture",Player=true,Body=0x190};}finally{Creating=false;}
        p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(p,99);
        while(p.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Strength allocation");}
        while(p.RawDex<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Dexterity allocation");}
        Require(ClassCreation.Profiles.TryTrain(p.Serial,"necromancer.poisondagger",out _),"Learn Poison Dagger");
        FixtureSpace.Corridor(p,6200,24,2300);p.Hits=p.HitsMax;p.Mana=p.ManaMax;
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.PoisonDagger)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.PoisonDagger),"Poison Dagger bindings");return p;
    }
    private static Item Named(PlayerMobile p,string source)
    {var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==source).Id,99,new Random(173),true);p.AddToBackpack(item);Require(p.EquipItem(item),"Equip "+source);ClassGear.Refresh(p);return item;}
    private static BaseWeapon Dagger(PlayerMobile p)
    {var item=new Dagger();ClassGear.Profiles.Convert(item,99,GearRarity.Normal,new Random(173));p.AddToBackpack(item);Require(p.EquipItem(item),"Equip normal dagger");return item;}
    private static GraveyardSkeleton Target(PlayerMobile p,int resistance=0)
    {
        GraveyardSkeleton target;Creating=true;try{target=new GraveyardSkeleton{Name="PoisonDaggerFixtureTarget",NoKillAwards=true,Frozen=true,CantWalk=true};}finally{Creating=false;}
        target.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);target.AIObject.AITimer.Stop();target.SetHits(50000);target.Hits=target.HitsMax;target.SetResistance(ResistanceType.Poison,resistance);return target;
    }
    private static TimedEffectState Poison(Mobile target)=>TimedEffects.Snapshot().Single(x=>x.Owner==target.Serial.Value);
    private static void Freeze(Mobile target){var s=Poison(target);TimedEffects.Load(TimedEffects.Snapshot().Where(x=>x.Owner!=target.Serial.Value).Append(s with{UpdatedUtc=TimedEffects.Now.AddHours(1)}).ToArray());}
    private static void Ready(PlayerMobile p){ClassCombat.Stop(p);ClassCombat.For(p).NextAction=0;}
    private static bool Cast(PlayerMobile p,Mobile target)
    {Ready(p);if(!ClassCombat.Perform(p,CombatAbility.PoisonDagger,target,target.Location,Core.TickCount)){return false;}return ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt);}
    private static GearDamage Packet(PlayerMobile p,Mobile target)=>PoisonDaggerPackets.Damage(p,target,SkillMechanics.Values(p,CombatAbility.PoisonDagger),()=>0,out _);
    private static void Drop(PlayerMobile p,Mobile target){target.Delete();p.Delete();}
    private static void Check()
    {
        Source();var hit=PoisonDaggerPackets.HitRoll;var roll=PoisonDaggerPackets.PoisonRoll;
        PoisonDaggerPackets.HitRoll=()=>0;PoisonDaggerPackets.PoisonRoll=_=>0;
        try
        {
            var p=Owner();var dagger=Dagger(p);var t=Target(p);
            Require(Packet(p,t).SourcePoison==new WeaponPoisonRoll(36,50),"Ungeared skill packet");
            var helm=Named(p,"Wormskull");Require(Packet(p,t).SourcePoison==new WeaponPoisonRoll(158,260),"Real Wormskull soft rank plus one item poison");
            p.AddToBackpack(dagger);ClassGear.Profiles.Store(dagger,ClassGear.State(dagger) with{TinkerSockets=1});
            GemActivationChecks.Install(p,GemActivationChecks.Gem(p,"Emerald"),dagger);Require(p.EquipItem(dagger),"Equip socketed dagger");ClassGear.Refresh(p);var blade=dagger;
            Require(SkillMechanics.Rank(p,CombatAbility.PoisonDagger)==2&&Packet(p,t).SourcePoison==new WeaponPoisonRoll(304,217),"Real two-item poison aggregation and hard-only synergies");
            int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.PoisonDagger).Mana256;
            Require(Cast(p,t)&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Learned attack payment");
            Require(Poison(t).PoisonClock?.Rate256==304&&Poison(t).PoisonMs==8680&&Poison(t).PoisonSource==p.Serial.Value,"Learned attack applied competing poison states");
            long at=ClassCombat.For(p).EffectAt;Require(!ClassCombat.ResolveAction(p,at)&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Attack replayed");
            int before=SkillHealth.Balance256(t);var start=Poison(t).UpdatedUtc;TimedEffects.Settle(t,start.AddMilliseconds(40));Require(before-SkillHealth.Balance256(t)==304&&Poison(t).PoisonMs==8640,"Fractional combined poison tick");
            var captured=Packet(p,t);p.AddToBackpack(helm);ClassGear.Refresh(p);Require(captured.SourcePoison==new WeaponPoisonRoll(304,217)&&Packet(p,t).SourcePoison!=captured.SourcePoison,"Captured packet changed with live gear");Require(p.EquipItem(helm),"Restore helmet");ClassGear.Refresh(p);
            var immune=Target(p,100);Require(Cast(p,immune)&&!TimedEffects.Snapshot().Any(x=>x.Owner==immune.Serial.Value&&x.PoisonMs>0),"Poison immunity ignored");immune.Delete();
            var miss=Target(p);PoisonDaggerPackets.HitRoll=()=>1;int balance=SkillHealth.Balance256(miss);mana=NecromancerEffects.ManaBalance256(p);Require(Cast(p,miss)&&SkillHealth.Balance256(miss)==balance&&!TimedEffects.Snapshot().Any(x=>x.Owner==miss.Serial.Value&&x.PoisonMs>0)&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Miss applied poison or skipped payment");miss.Delete();PoisonDaggerPackets.HitRoll=()=>0;
            var cancel=Target(p);Ready(p);mana=NecromancerEffects.ManaBalance256(p);Require(ClassCombat.Perform(p,CombatAbility.PoisonDagger,cancel,cancel.Location,Core.TickCount),"Cancellation windup");p.AddToBackpack(blade);Require(!ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt)&&NecromancerEffects.ManaBalance256(p)==mana,"Changed weapon released/paid");cancel.Delete();Require(p.EquipItem(blade),"Restore weapon");
            TimedEffects.ClearGear(t);Require(Cast(p,t),"Saved poison attack");Freeze(t);
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,t.Serial.Value,blade.Serial.Value,helm.Serial.Value,SkillHealth.Balance256(t),Poison(t),0)));
        }
        finally{PoisonDaggerPackets.HitRoll=hit;PoisonDaggerPackets.PoisonRoll=roll;}
    }
    private static void Reload()
    {
        Source();var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var t=World.FindMobile((Serial)s.Target);
        Require(World.FindItem((Serial)s.Weapon)?.Parent==p&&World.FindItem((Serial)s.Helm)?.Parent==p&&SkillMechanics.Rank(p,CombatAbility.PoisonDagger)==2,"Saved real equipment and ranks");
        Require(SkillHealth.Balance256(t)==s.Balance&&JsonSerializer.Serialize(Poison(t))==JsonSerializer.Serialize(s.Poison),"Saved fractional health or combined poison");
        Require(SkillBindings.Instance.Get(p).Right==CombatAbility.PoisonDagger&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Necromancer,7)==CombatAbility.PoisonDagger,"Saved bindings");
        var now=TimedEffects.Now;TimedEffects.Load(TimedEffects.Snapshot().Where(x=>x.Owner!=t.Serial.Value).Append(s.Poison with{UpdatedUtc=now}).ToArray());TimedEffects.Settle(t,now.AddMilliseconds(40));
        Require(s.Balance-SkillHealth.Balance256(t)==304&&Poison(t).PoisonMs==s.Poison.PoisonMs-40&&Poison(t).PoisonSource==s.Owner,"Resumed fractional poison frame");Freeze(t);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Balance=SkillHealth.Balance256(t),Poison=Poison(t),Generation=s.Generation+1}));
    }
    private static void Input(NetState network,bool quick)
    {
        var p=Owner();Dagger(p);Named(p,"Wormskull");var target=Target(p);Ready(p);
        string label=quick?"poison-dagger-quick":"poison-dagger-right";int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.PoisonDagger).Mana256;
        var hit=PoisonDaggerPackets.HitRoll;var roll=PoisonDaggerPackets.PoisonRoll;PoisonDaggerPackets.HitRoll=()=>0;PoisonDaggerPackets.PoisonRoll=_=>0;
        network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var field=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);field.SetValue(network,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        bool began=false;long deadline=Core.TickCount+6000;
        void Restore(){PoisonDaggerPackets.HitRoll=hit;PoisonDaggerPackets.PoisonRoll=roll;Drop(p,target);}
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Poison Dagger input timeout");var session=ClassCombat.For(p);
                if(session.PendingAbility==CombatAbility.PoisonDagger&&!began){Require(NecromancerEffects.ManaBalance256(p)==mana,"Input prepaid unexpectedly");began=true;Log.Information("{Label}: windup",label);}
                var state=TimedEffects.Snapshot().SingleOrDefault(x=>x.Owner==target.Serial.Value&&x.PoisonMs>0);
                if(state==null){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Inspect);return;}
                Require(began&&state.PoisonClock?.Rate256==158&&state.PoisonSource==p.Serial.Value&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Real learned input combined packet/payment");Restore();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Restore();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.PoisonDagger,(int)DiabloClass.Necromancer,target.Serial.Value,target.X,target.Y,target.Z);Inspect();});
    }
}
