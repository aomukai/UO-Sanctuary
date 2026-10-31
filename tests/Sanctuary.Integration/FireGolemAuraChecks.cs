// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
using Server.Spells;
namespace Sanctuary.Integration;

public static class FireGolemAuraChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(FireGolemAuraChecks));
    private sealed record Saved(uint Owner,uint Actor,ulong Seed,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"fire-golem-aura-fixtures.json");
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("fire-golem-aura-check",Check);Register("fire-golem-aura-reload-check",Reload);
        ConsoleInputHandler.RegisterCommand("fire-golem-aura-live","Connected Fire Holy Fire pulse and self state",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("fire-golem-aura-live-result",Live)));
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Fire own aura suppliers, combined packet and saved lease",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static PlayerMobile Owner()
    {
        var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureFireAura";
        void Train(string id,int rank){TalentCatalog.TryGet(DiabloClass.Necromancer,id,out var row);foreach(string prior in row.Prerequisites){if(ClassCreation.Profiles.GetRank(p.Serial,prior)==0){Train(prior,1);}}while(ClassCreation.Profiles.GetRank(p.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}}
        Train("necromancer.firegolem",4);return p;
    }
    private static FireGolemBody Pet(PlayerMobile p)
    {
        Require(NecromancerSummons.Create(p,CombatAbility.FireGolem,Serial.Zero,new(p.X+1,p.Y,p.Z)),"Actual Fire creation");
        var b=NecromancerGolems.For(p).OfType<FireGolemBody>().Single();b.AIObject.AITimer.Stop();return b;
    }
    private static void Check()
    {
        var p=Owner();var b=Pet(p);
        foreach(var (rank,min,max) in new[]{(1,6,18),(10,51,90),(20,195,264),(30,405,504)})
        {
            var v=new SkillValues(SkillMechanics.Source("Holy Fire"),name=>name=="Holy Fire"?rank:0,_=>0,99);
            Require(PaladinElements.WeaponRange(b,CombatAbility.HolyFire,v)==(min,max),"Holy Fire whole-point weapon formula "+rank);
        }
        Require(b.HolyFireValues.Rank==10&&b.HolyFireValues.Damage256(false)==2176&&b.HolyFireValues.Damage256(true)==3840,"Captured Fire rank4 must grant Holy Fire10 without owner synergies");
        PoisonDaggerChecks.Named(p,"Wormskull");Require(b.HolyFireValues.Rank==10,"Later owner gear changed the pet aura rank");
        var now=TimedEffects.Now;var range=PaladinElements.WeaponRange(b,CombatAbility.HolyFire,b.HolyFireValues);
        // A deliberately long fixture lease tests shared persistence across
        // boots. Live controls below verify the actual 51-frame lifetime.
        PaladinAuras.ElementalWeapon(new(b.Serial.Value,b.Serial.Value,CombatAbility.HolyFire,10,0,now.AddDays(1),WeaponMin:range.Min,WeaponMax:range.Max));
        var raw=NpcEquipmentElements.Capture(b).Stats;Require(raw.FireMin==22528&&raw.FireMax==37632,"Innate37..57 and aura51..90 must accrue into one88..147fire range");
        var rolled=NpcEquipmentDamage.Roll(b,null,_=>0,()=>0);Require(rolled.Damage.Elements.Fire==176,"Intrinsic critical must double innate plus aura once");
        var lease=PaladinAuras.Effect(b,CombatAbility.HolyFire);
        PaladinAuras.ElementalWeapon(lease with{UntilUtc=now.AddMilliseconds(-1)});Require(PaladinAuras.Effect(b,CombatAbility.HolyFire)==null&&NpcEquipmentElements.Capture(b).Stats.FireMin==9472,"Expired aura leaked weapon stats");
        PaladinAuras.ElementalWeapon(lease);
        NpcEquipmentRandom.Set(b,SourceRandom.InitLow(173));NpcEquipmentRandom.Raw(b);var state=NpcEquipmentRandom.State(b);
        PersistentMinions.Suspend(p);p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,b.Serial.Value,state.Seed,0)));
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var b=(FireGolemBody)World.FindMobile((Serial)saved.Actor);
        var lease=PaladinAuras.Effect(b,CombatAbility.HolyFire);
        Require(b.ControlMaster==p&&b.Map==Map.Internal&&lease is{Rank:10,WeaponMin:51,WeaponMax:90}&&lease.Source==b.Serial.Value&&NpcEquipmentElements.Capture(b).Stats.FireMin==22528,"Saved actor-owned aura lease/stat composition");
        Require(NpcEquipmentRandom.State(b).Seed==saved.Seed,"Offline aura replay advanced RNG");b.PulseHolyFire(p,TimedEffects.Now);
        Require(NpcEquipmentRandom.State(b).Seed==saved.Seed,"Parked/offline pulse advanced RNG");
        ulong seed=saved.Seed;uint next=SourceRandom.Roll(ref seed);Require(NpcEquipmentRandom.Raw(b)==next&&NpcEquipmentRandom.State(b).Seed==seed,"Saved Fire aura stream failed to resume once");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Seed=seed,Generation=saved.Generation+1}));
    }
    private static GraveyardSkeleton Enemy(PlayerMobile p,int x,int y)
    {
        var t=PoisonDaggerChecks.Target(p);t.MoveToWorld(new(x,y,p.Z),p.Map);t.SetHits(20000);t.Hits=t.HitsMax;t.SetResistance(ResistanceType.Fire,0);TimedEffects.PreventHealing(t,TimedEffects.Now);return t;
    }
    private static void Live(NetState network)
    {
        var p=Owner();network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;
        var field=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);field.SetValue(network,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));
        var account=new Account("fire-golem-aura-"+Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"));account[0]=p;network.Account=account;network.Mobile=p;p.NetState=network;
        var b=Pet(p);p.MoveToWorld(new(5900,2380,0),p.Map);b.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);b.AIObject.AITimer.Stop();
        var a=Enemy(p,b.X+1,b.Y+1);var c=Enemy(p,b.X+1,b.Y-1);var immune=Enemy(p,b.X+2,b.Y);immune.SetResistance(ResistanceType.Fire,100);
        var far=Enemy(p,b.X+30,b.Y);bool cleaned=false;
        void Clean(){if(cleaned){return;}cleaned=true;b.Delete();foreach(var t in new[]{a,c,immune,far}){t.Corpse?.Delete();t.Delete();}p.Delete();account.Delete();}
        FixtureClock.NextLoop("fire-golem-aura-live-result",()=>
        {
            try
            {
                Log.Information("fire-golem-aura-live: ready");Require(!SpellHelper.IsTown(b.Location,b),"Field fixture is in town");
                p.Mana=b.Mana=0;var now=TimedEffects.Now;NpcEquipmentRandom.Set(b,SourceRandom.InitLow(0));ulong seed=SourceRandom.InitLow(0);
                int pulse=2176+(int)SourceRandom.Limited(ref seed,3840-2176);int expected=(int)Math.Floor(pulse*PaladinElements.Proximity(Math.Sqrt(2),SkillGeometry.Tiles(b.HolyFireValues.Eval("aurarangecalc"))));
                int first=SkillHealth.Balance256(a),second=SkillHealth.Balance256(c),immuneLife=SkillHealth.Balance256(immune),farLife=SkillHealth.Balance256(far);
                b.PulseHolyFire(p,now);var lease=PaladinAuras.Effect(b,CombatAbility.HolyFire);
                Require(first-SkillHealth.Balance256(a)==expected&&second-SkillHealth.Balance256(c)==expected&&SkillHealth.Balance256(immune)==immuneLife&&SkillHealth.Balance256(far)==farLife,"Pulse must share one roll, respect immunity/range, and use actual NPC source");
                Require(NpcEquipmentRandom.State(b).Seed==seed&&lease is{Rank:10,WeaponMin:51,WeaponMax:90}&&lease.Source==b.Serial.Value&&lease.UntilUtc==now.AddMilliseconds(2040)&&p.Mana==0&&b.Mana==0,"NPC weapon lease/source/duration or mana bypass");
                Require(NpcEquipmentDamage.Roll(b,a,_=>0,()=>99).Damage.Elements.Fire==88,"Live aura did not join the ordinary melee packet");
                p.MoveToWorld(new(1496,1628,10),p.Map);b.MoveToWorld(new(1497,1628,10),p.Map);a.MoveToWorld(new(1498,1628,10),p.Map);
                Require(SpellHelper.IsTown(b.Location,b),"Town fixture is not guarded");first=SkillHealth.Balance256(a);seed=NpcEquipmentRandom.State(b).Seed;b.PulseHolyFire(p,now.AddMilliseconds(1));
                Require(SkillHealth.Balance256(a)==first&&NpcEquipmentRandom.State(b).Seed==seed&&PaladinAuras.Effect(b,CombatAbility.HolyFire).UntilUtc==now.AddMilliseconds(2041),"Town must refresh own weapon state without pulse damage or random draw");
                b.Delete();Require(PaladinAuras.Effect(b,CombatAbility.HolyFire)==null&&!PaladinAuras.Snapshot().Leases.Any(x=>x.Target==b.Serial.Value),"Deleted Fire retained aura state");
                Clean();Log.Information("fire-golem-aura-live-result: passed");
            }
            catch(Exception e){Clean();Log.Error(e,"fire-golem-aura-live-result: failed");}
        });
    }
}
