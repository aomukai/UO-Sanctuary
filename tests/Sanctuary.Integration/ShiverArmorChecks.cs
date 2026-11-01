// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class ShiverArmorChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ShiverArmorChecks));
    private sealed record Saved(uint Owner,uint Target,uint Helm,int Mana,int Life,int TargetLife,int Map,Point3D Point,ColdArmorState Armor,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shiver-armor-fixtures.json");
    public static void Configure()
    {
        var prior=ClassGearHooks.CanRegenerateMana;ClassGearHooks.CanRegenerateMana=p=>p.Name!="ShiverArmorFixture"&&prior?.Invoke(p)!=false;
        Register("shiver-armor-check",Check);Register("shiver-armor-reload-check",Reload);
    }
    [CallPriority(int.MaxValue)]
    public static void Initialize()
    {var prior=ClassGearHooks.CanRegenerateHits;ClassGearHooks.CanRegenerateHits=p=>p.Name!="ShiverArmorFixture"&&prior?.Invoke(p)!=false;}
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Shiver ordered SOFTHIT packet and late NPC hit/miss",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {var p=ColdArmorChecks.Owner(CombatAbility.ShiverArmor);p.Name="ShiverArmorFixture";return p;}
    private static void Train(PlayerMobile p,CombatAbility a,int rank)
    {string id=SkillMechanics.For(a).TalentId;while(ClassCreation.Profiles.GetRank(p.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Shiver train "+id);}}
    private static ColdArmorState Armor(PlayerMobile p)
    {Require(SorceressCombat.Release(p,CombatAbility.ShiverArmor,Serial.Zero,p.Location),"Paid Shiver cast");return SorceressEffects.Armor(p);}
    private static void Check()
    {
        Goldens();Source();Capture();NativeAttempts();KillEvents();Save();
        Log.Information("Shiver Armor:765rank/hard goldens,7680complete-source roll/160native HP-return-cold comparisons,late NPC hit/miss and suppressed kill recovery,retained rank/live hard/gear and original saved reactions pass");
    }
    private static void Goldens()
    {
        var row=SkillMechanics.For(CombatAbility.ShiverArmor);
        for(int rank=1;rank<=255;rank++)foreach(int hard in new[]{0,1,20})
        {
            int Tier(int basis,int step){int n=rank-1;return basis+Math.Min(n,7)*step+Math.Min(Math.Max(0,n-7),8)*(step+2)
                +Math.Min(Math.Max(0,n-15),6)*(step+4)+Math.Min(Math.Max(0,n-21),6)*(step+6)+Math.Max(0,n-27)*(step+8);}
            var v=new SkillValues(row,k=>k==row.SourceKey?rank:255,_=>hard,99);
            Require(v.Damage256(false)==(long)Tier(12,6)*128*(100+18*hard)/100&&v.Damage256(true)==(long)Tier(16,7)*128*(100+18*hard)/100
                &&v.DurationFrames==100+Math.Min(Math.Max(0,rank-8),8)*25+Math.Max(0,rank-16)*50,"Independent Shiver elemental curve");
        }
    }
    private static void Source()
    {
        var p=Owner();int rolls=0,deliveries=0;try
        {
            using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"shiver-armor-source-cases.json")));
            foreach(var r in doc.RootElement.EnumerateArray())
            {
                int I(int i)=>r[0][i].GetInt32();int O(int i)=>r[1][i].GetInt32();
                ulong seed=SourceRandom.InitLow(unchecked((int)r[0][1].GetUInt32()));
                var prepared=new ShiverPacket(I(0),I(0)*256,(I(0)+1)*256,I(5),DruidFirestorm.Modifiers(p));
                var packet=ShiverArmor.Roll(prepared,ref seed);
                Require(packet.Cold256==O(1)&&packet.Total256==O(1)&&packet.ResultFlags==O(3)&&packet.HitClass==O(10)
                    &&packet.HitFlags==0&&packet.HitClassActive&&seed==r[1][8].GetUInt64(),"Complete-source Shiver roll/flags/class/seed");rolls++;
                if(I(4)!=0||I(5)!=101||I(6)!=0||I(7)!=0){continue;}
                var t=FireBoltChecks.Target(p,1);try
                {
                    t.Frozen=t.CantWalk=false;t.AIObject.AITimer.Stop();t.SetResistance(ResistanceType.Cold,I(2));SkillHealth.SetBalance256(t,I(3));
                    var at=TimedEffects.Now;var result=ShiverArmor.Deliver(p,t,packet,prepared.Modifiers,at);var state=NativeMonsterReturn.Session(t);
                    Require(result.AfterHealth256==O(5)&&result.Packet.Total256==O(2)&&result.Packet.ResultFlags==O(4)
                        &&result.Packet.ColdFrames==O(13)&&result.Packet.HitClass==61&&state.LastHitClass==61
                        &&result.DeathModeAttempted==(O(7)!=0)&&state.LastMode==0&&state.RecoveryNotifications==O(16)&&state.Refreshes==O(17),"Complete-source native HP/cold/SOFTHIT return");
                    if(t.Alive&&O(13)>0)
                    {Require(TimedEffects.TryActor(t,out var actor)&&Math.Abs((actor.ChillUntil-at).TotalMilliseconds-O(13)*40)<2,"Post-HP cold length differs from source");}
                    deliveries++;
                }
                finally{TimedEffects.Clear(t);t.Corpse?.Delete();t.Delete();}
            }
            Require(rolls==7680&&deliveries==160,"Incomplete Shiver source/native coverage");
        }
        finally{FireBoltChecks.Clean(p);}
    }
    private static void Capture()
    {
        var p=Owner();var t=FireBoltChecks.Target(p,1);try
        {
            var orb=PoisonDaggerChecks.Named(p,"Eschuta's temper");var helm=PoisonDaggerChecks.Named(p,"Nightwing's Veil");var armor=Armor(p);
            p.Backpack.DropItem(orb);ClassGear.Refresh(p);Train(p,CombatAbility.FrozenArmor,20);var packet=ShiverArmor.Prepare(p,armor);
            var v=SkillMechanics.Values(p,CombatAbility.ShiverArmor,armor.Rank);var ends=DruidFirestorm.Endpoints(v,NamedElements.MasteryPercent(p,"cold"),SlowMissilePayload.Normal);
            Require(packet.Rank==armor.Rank&&armor.Rank>SkillMechanics.Rank(p,CombatAbility.ShiverArmor)&&packet.Minimum256==ends.Min&&packet.Maximum256==ends.Max,"Retained rank/live hard/positive enhancement");
            var legacy=ShiverArmor.Prepare(p,armor with{Rank=0,Min256=1234,Max256=2345,ColdFrames=137});
            var oldends=DruidFirestorm.Endpoints(1234,2345,NamedElements.MasteryPercent(p,"cold"),SlowMissilePayload.Normal);
            Require(legacy.Rank==0&&legacy.Minimum256==oldends.Min&&legacy.Maximum256==oldends.Max&&legacy.ColdFrames==137,"Rankless Shiver reconstructed a rank");
            p.Backpack.DropItem(helm);ClassGear.Refresh(p);Train(p,CombatAbility.ChillingArmor,20);
            Require(ShiverArmor.Prepare(p,armor).Minimum256!=packet.Minimum256,"New reaction reused old hard/gear snapshot");
            int hp=SkillHealth.Balance256(t);SorceressEffects.MeleeAttempt(t,p);
            Require(SkillHealth.Balance256(t)==hp,"NPC early accuracy hook still dealt Shiver damage");
            using(PaladinThorns.Attempt(t,p,false)){Require(SkillHealth.Balance256(t)==hp,"Late attempt reacted at entry");}
            Require(SkillHealth.Balance256(t)<hp,"Scoped NPC miss did not react at exit");
        }
        finally{FireBoltChecks.Clean(p,t);}
    }
    private static void NativeAttempts()
    {
        var p=Owner();var t=FireBoltChecks.Target(p,1);try
        {
            Armor(p);t.SetDamage(10,10);t.Frozen=t.CantWalk=false;t.AIObject.AITimer.Stop();
            int mana=NecromancerEffects.ManaBalance256(p),hp=SkillHealth.Balance256(p),enemy=SkillHealth.Balance256(t);
            ((BaseWeapon)t.Weapon).OnMiss(t,p);
            Require(SkillHealth.Balance256(p)==hp&&SkillHealth.Balance256(t)<enemy&&NecromancerEffects.ManaBalance256(p)==mana,"Native miss reaction or extra mana");
            TimedEffects.Clear(t);enemy=SkillHealth.Balance256(t);((BaseWeapon)t.Weapon).OnHit(t,p);
            Require(SkillHealth.Balance256(p)<hp&&SkillHealth.Balance256(t)<enemy&&NecromancerEffects.ManaBalance256(p)==mana,"Native hit reaction or extra mana");
            var ordinary=new Orc{Frozen=true,CantWalk=true,NoKillAwards=true};ordinary.MoveToWorld(new(p.X+1,p.Y+2,p.Z),p.Map);ordinary.AIObject.AITimer.Stop();ordinary.SetResistance(ResistanceType.Cold,0);
            try
            {
                var value=ShiverArmor.Prepare(p,SorceressEffects.Armor(p));ulong seed=SourceRandom.InitLow(0);
                Require(ShiverArmor.Supported(p,ordinary,out _)&&ShiverArmor.Deliver(p,ordinary,ShiverArmor.Roll(value,ref seed),value.Modifiers,TimedEffects.Now).Packet.HitClass==61,"Native monster needed an invented D2 identity for SOFTHIT");
            }
            finally{TimedEffects.Clear(ordinary);ordinary.Corpse?.Delete();ordinary.Delete();}
            var hire=new HireFighter{Frozen=true,CantWalk=true,NoKillAwards=true};try
            {
                hire.MoveToWorld(new(p.X+1,p.Y+2,p.Z),p.Map);hire.AIObject.AITimer.Stop();
                Require(!ShiverArmor.Supported(p,hire,out string reason)&&reason.Contains("Hireling"),"Unresolved hireling notification path silently accepted");
            }
            finally{hire.Corpse?.Delete();hire.Delete();}
        }
        finally{FireBoltChecks.Clean(p,t);}
    }
    private static void KillEvents()
    {
        var p=Owner();var t=FireBoltChecks.Target(p,1);try
        {
            PoisonDaggerChecks.Named(p,"Silkweave");Armor(p);Require(ClassGear.Bonus(p,"mana-kill")>0,"Real mana-after-kill fixture");
            t.NoKillAwards=false;SkillHealth.SetBalance256(t,256);NecromancerEffects.SetMana256(p,0);
            using(PaladinThorns.Attempt(t,p,false)){}
            Require(!t.Alive&&NecromancerEffects.ManaBalance256(p)==0,"Flag32 Shiver kill triggered suppressed item recovery");
            var control=FireBoltChecks.Target(p,1);try
            {
                control.NoKillAwards=false;SkillHealth.SetBalance256(control,256);SkillHealth.Damage(p,control,256);
                Require(!control.Alive&&NecromancerEffects.ManaBalance256(p)==ClassGear.Bonus(p,"mana-kill")*256,"Ordinary kill recovery positive control");
            }
            finally{TimedEffects.Clear(control);control.Corpse?.Delete();control.Delete();}
        }
        finally{FireBoltChecks.Clean(p,t);}
    }
    private static void Save()
    {
        var p=Owner();Train(p,CombatAbility.ShiverArmor,20);Train(p,CombatAbility.FrozenArmor,20);Train(p,CombatAbility.ChillingArmor,20);
        var helm=PoisonDaggerChecks.Named(p,"Nightwing's Veil");Armor(p);var t=FireBoltChecks.Target(p,1);
        SkillHealth.Damage(p,p,5*256+17);using(PaladinThorns.Attempt(t,p,false)){}
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,t.Serial.Value,helm.Serial.Value,NecromancerEffects.ManaBalance256(p),
            SkillHealth.Balance256(p),SkillHealth.Balance256(t),p.Map.MapID,p.Location,SorceressEffects.Armor(p),0)));p.Internalize();
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var t=(BaseCreature)World.FindMobile((Serial)s.Target);
        Require(s.Generation<2&&World.FindItem((Serial)s.Helm)?.Parent==p&&SorceressEffects.Armor(p)==s.Armor&&NecromancerEffects.ManaBalance256(p)==s.Mana
            &&SkillHealth.Balance256(p)==s.Life&&SkillHealth.Balance256(t)==s.TargetLife,"Saved Shiver actors/armor/gear/wounds/mana changed");
        p.MoveToWorld(s.Point,Map.Maps[s.Map]);t.AIObject.AITimer.Stop();int hp=SkillHealth.Balance256(t);((BaseWeapon)t.Weapon).OnMiss(t,p);
        Require(SkillHealth.Balance256(t)<hp&&SkillHealth.Balance256(p)==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana,"Saved native miss reaction failed or altered caster resources");
        p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{TargetLife=SkillHealth.Balance256(t),Generation=s.Generation+1}));
    }
}
