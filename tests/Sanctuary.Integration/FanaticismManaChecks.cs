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

public static class FanaticismManaChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(FanaticismManaChecks));
    private sealed record Saved(uint Owner,uint Ally,uint Helm,uint Sword,Point3D Point,int Life,int AllyLife,PaladinAuraSave State,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"fanaticism-mana-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    [CallPriority(int.MaxValue)]public static void Initialize()
    {
        var hp=ClassGearHooks.CanRegenerateHits;var mp=ClassGearHooks.CanRegenerateMana;
        ClassGearHooks.CanRegenerateHits=m=>m.Name?.StartsWith("FanaticismMana",StringComparison.Ordinal)!=true&&hp?.Invoke(m)!=false;
        ClassGearHooks.CanRegenerateMana=m=>m.Name?.StartsWith("FanaticismMana",StringComparison.Ordinal)!=true&&mp?.Invoke(m)!=false;
    }
    public static void Configure()
    {
        ConsoleInputHandler.RegisterCommand("fanaticism-mana-check","Prepared Fanaticism mana-neutral self packet",_=>Core.LoopContext.Post(()=>
        {try{Check();Log.Information("fanaticism-mana-check: passed");}catch(Exception e){Log.Error(e,"fanaticism-mana-check: failed");}}));
        ConsoleInputHandler.RegisterCommand("fanaticism-mana-reload-check","Original prepared Fanaticism state and natural post-load pulse",_=>Core.LoopContext.Post(()=>
        {Log.Information("fanaticism-mana-reload-check: passed");FixtureNetwork.Connected("fanaticism-mana-reload-check-result",Reload);}));
    }
    private static PlayerMobile Owner(int rank)
    {
        var p=new PlayerMobile{Name="FanaticismManaFixture",Player=true,Body=0x190};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Paladin);ClassCreation.Profiles.AdvanceLevel(p,99);
        ClassSkillChecks.Train(p,"paladin.fanaticism",rank);while(p.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Fanatic Strength");}
        FixtureSpace.Corridor(p,6380,24,1000);var sword=new Broadsword();p.AddToBackpack(sword);Require(p.EquipItem(sword),"Original melee sword");ClassGear.Refresh(p);return p;
    }
    private static PlayerMobile Ally(PlayerMobile p)
    {
        var a=new PlayerMobile{Name="FanaticismManaAlly",Player=true,Body=0x191,Female=true};a.AddItem(new Backpack());ClassCreation.Profiles.Assign(a,DiabloClass.Sorceress);ClassCreation.Profiles.AdvanceLevel(a,99);a.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);return a;
    }
    private static void Select(PlayerMobile p,CombatAbility ability=CombatAbility.Fanaticism)=>Require(SkillBindings.Instance.Assign(p,false,ability),"Aura selection");
    private static void Stats(PlayerMobile p,Mobile ally,int rank,int self,int party,int rate,int attack)
    {
        Require(PaladinAuras.Effect(p,CombatAbility.Fanaticism) is{} a&&a.Rank==rank&&a.DamagePercent==self&&a.Percent==rate&&a.AttackPercent==attack
            &&PaladinAuras.Effect(ally,CombatAbility.Fanaticism) is{} b&&b.Rank==rank&&b.DamagePercent==party&&b.Percent==rate&&b.AttackPercent==attack,"Prepared self/party/rate/AR packet mismatch");
    }
    private static PaladinAuraSave Scope(PaladinAuraSave state,uint owner)=>new(state.Emitters.Where(e=>e.Owner==owner).ToArray(),state.Leases.Where(l=>l.Source==owner).ToArray());
    private static PaladinAuraSave Hold(PlayerMobile p)
    {
        var all=PaladinAuras.Snapshot();var until=TimedEffects.Now.Add(FixtureSpace.Retention);
        all=all with{Emitters=all.Emitters.Select(e=>e.Owner==p.Serial.Value?e with{NextUtc=until}:e).ToArray(),Leases=all.Leases.Select(l=>l.Source==p.Serial.Value?l with{UntilUtc=until.AddMilliseconds(40)}:l).ToArray()};
        PaladinAuras.Restore(all);return Scope(all,p.Serial.Value);
    }
    private static void Clean(params Mobile[] actors)
    {foreach(var actor in actors){PaladinAuras.Clear(actor);actor.Corpse?.Delete();actor.Delete();}}
    private static void Check()
    {
        Require(!PaladinAuras.PreparedFanaticismManaNeutral,"Prepared policy leaked into ordinary routes");PaladinAuras.PreparedFanaticismManaNeutral=true;
        try
        {
            foreach(int rank in new[]{1,20})
            {
                var p=Owner(rank);var a=Ally(p);var pet=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true};pet.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);pet.AIObject.AITimer.Stop();Require(pet.SetControlMaster(a),"Controlled ally fixture");
                try
                {
                    int self=rank==1?50:373,party=rank==1?25:186,rate=rank==1?14:35,attack=rank==1?40:135;double? physical=null;int? rating=null;
                    foreach(int mana in new[]{0,1,256,p.ManaMax*256})
                    {
                        NecromancerEffects.SetMana256(p,mana);Select(p);Stats(p,a,rank,self,party,rate,attack);Stats(p,pet,rank,self,party,rate,attack);
                        double damage=GearEffects.Damage(p,.5).Physical;int ar=ClassStats.AttackRating(p);
                        Require(NecromancerEffects.ManaBalance256(p)==mana&&(physical==null||physical==damage)&&(rating==null||rating==ar),"Mana changed personal physical/AR consumers or was charged");physical=damage;rating=ar;
                        Require(Math.Abs(PaladinAuras.NativeWeaponMultiplier(pet,true)-(1+party/100.0))<.000001,"Pet received personal rather than party bonus");
                    }
                    NecromancerEffects.SetMana256(p,0);Select(p,CombatAbility.Might);Require(PaladinAuras.Effect(p,CombatAbility.Might)?.Percent==40&&NecromancerEffects.ManaBalance256(p)==0,"Prepared rule changed another aura");
                }
                finally{Clean(p,a,pet);}
            }
            var owner=Owner(1);var ally=Ally(owner);var helm=PoisonDaggerChecks.Named(owner,"Tarnhelm");NecromancerEffects.SetMana256(owner,0);Select(owner);Stats(owner,ally,2,67,33,18,45);
            SkillBindings.Instance.AssignQuick(owner,7,CombatAbility.Fanaticism);SkillHealth.SetBalance256(owner,SkillHealth.Maximum256(owner)-513);SkillHealth.SetBalance256(ally,SkillHealth.Maximum256(ally)-257);
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,ally.Serial.Value,helm.Serial.Value,CombatTiming.Equipped(owner).Serial.Value,owner.Location,SkillHealth.Balance256(owner),SkillHealth.Balance256(ally),Hold(owner),0)));owner.Internalize();ally.Internalize();
            Log.Information("Prepared Fanaticism:rank1/20 at zero,fractional,one/full mana preserve physical/AR/rate and party/pet values;actual rank2Tarnhelm67/33,other aura unchanged,original zero-mana state saved");
        }
        finally{PaladinAuras.PreparedFanaticismManaNeutral=false;}
    }
    private static void Reload(NetState network)
    {
        Require(!PaladinAuras.PreparedFanaticismManaNeutral,"Prepared policy remained active");var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var a=(PlayerMobile)World.FindMobile((Serial)s.Ally);
        Require(s.Generation<2&&JsonSerializer.Serialize(Scope(PaladinAuras.Snapshot(),s.Owner))==JsonSerializer.Serialize(s.State)
            &&World.FindItem((Serial)s.Helm)?.Parent==p&&CombatTiming.Equipped(p)?.Serial.Value==s.Sword&&NecromancerEffects.ManaBalance256(p)==0
            &&SkillHealth.Balance256(p)==s.Life&&SkillHealth.Balance256(a)==s.AllyLife,"Original prepared aura/gear/resources changed");
        var account=FixtureNetwork.Authenticate(network,p,"fanaticism-mana-reload-");p.MoveToWorld(s.Point,Map.Trammel);a.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);
        PaladinAuras.PreparedFanaticismManaNeutral=true;var all=PaladinAuras.Snapshot();var before=PaladinAuras.Effect(p,CombatAbility.Fanaticism);PaladinAuras.Restore(all with{Emitters=all.Emitters.Select(e=>e.Owner==s.Owner?e with{NextUtc=TimedEffects.Now}:e).ToArray()});long deadline=Core.TickCount+5000;
        void Finish(){PaladinAuras.PreparedFanaticismManaNeutral=false;p.Internalize();a.Internalize();network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
        void Observe()
        {
            try
            {
                if(PaladinAuras.Effect(p,CombatAbility.Fanaticism)==before){Require(Core.TickCount<deadline,"Prepared natural pulse timeout");Timer.StartTimer(TimeSpan.FromMilliseconds(10),()=>Core.LoopContext.Post(Observe));return;}
                Stats(p,a,2,67,33,18,45);Require(NecromancerEffects.ManaBalance256(p)==0&&SkillHealth.Balance256(p)==s.Life&&SkillHealth.Balance256(a)==s.AllyLife
                    &&PaladinAuras.Effect(p,CombatAbility.Fanaticism).UntilUtc<TimedEffects.Now.AddSeconds(3)
                    &&PaladinAuras.Snapshot().Emitters.Single(e=>e.Owner==s.Owner).NextUtc<TimedEffects.Now.AddSeconds(3),"Natural pulse paid/healed/reset or retained inspection clock");
                var held=Hold(p);Finish();File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{State=held,Generation=s.Generation+1}));Log.Information("fanaticism-mana-reload-check-result: passed");
            }
            catch(Exception e){Finish();Log.Error(e,"fanaticism-mana-reload-check-result: failed");}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(10),()=>Core.LoopContext.Post(Observe));
    }
}
