// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary.Integration;

public static class LightningBoltChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(LightningBoltChecks));
    private sealed class Minimum:Random{public override int Next(int maximum)=>0;}
    private sealed class Maximum:Random{public override int Next(int maximum)=>Math.Max(0,maximum-1);}
    private sealed record Saved(uint Owner,uint Target,uint Weapon,Point3D Origin,WeaponFlight Flight,PoisonCloud[] Clouds,int Health,int Mana,int Quantity,TimedEffectState Poison,DateTime Cooldown);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"lightning-bolt.json");
    public static void Configure(){Register("lightning-bolt-check",Check);Register("lightning-bolt-reload-check",Reload);Register("lightning-bolt-input-check",Input);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Lightning Bolt reset, source path, cast input and persistence",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static PlayerMobile Fresh()=>MagicArrowChecks.Fresh("LightningBoltOwner");
    private static void Train(PlayerMobile p,CombatAbility ability,int rank=1)
    {var row=SkillMechanics.For(ability);Require(TalentCatalog.TryGet(row.CharacterClass,row.TalentId,out var t),"Talent");foreach(var id in t.Prerequisites){Train(p,SkillMechanics.All.Single(x=>x.TalentId==id).Ability);}while(ClassCreation.Profiles.GetRank(p.Serial,row.TalentId)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,row.TalentId,out _),"Train");}}
    private static BowLaunchTarget Target(PlayerMobile p)
    {var t=MagicArrowChecks.Target(p);t.Name="BowLaunchTarget";t.SetResistance(ResistanceType.Energy,0);return t;}
    private static BaseWeapon Weapon(PlayerMobile p)
    {
        if(CombatTiming.Equipped(p) is{} old){p.AddToBackpack(old);}var w=ItemOrigins.Create(ItemOrigin.Drop,()=>new SoulGlaive());ClassGear.Profiles.FromMonster(w,30,GearRarity.Normal,"glv",GearTier.Normal,new Random(173));p.AddToBackpack(w);Require(p.EquipItem(w),"Ordinary javelin equip");ClassGear.Refresh(p);return w;
    }
    private static void Ready(PlayerMobile p)=>MagicArrowChecks.Ready(p);
    private static WeaponFlight[] Own(PlayerMobile p)=>WeaponMissiles.Snapshot().Flights.Where(x=>x.Owner==p.Serial.Value).ToArray();
    private static PoisonCloud[] Clouds(PlayerMobile p)=>PoisonJavelins.Snapshot().Where(x=>x.Owner==p.Serial.Value).ToArray();
    private static void Hold(PlayerMobile p,WeaponFlight f,PoisonCloud[] clouds)
    {var all=WeaponMissiles.Snapshot();WeaponMissiles.Restore(all with{Flights=all.Flights.Where(x=>x.Owner!=p.Serial.Value).Append(f).ToArray(),PoisonClouds=all.PoisonClouds.Where(x=>x.Owner!=p.Serial.Value).Concat(clouds).ToArray()});}
    private static void Controlled(Action action)
    {
        var random=AmazonBow.LaunchRandom;var damage=WeaponMissiles.DamageRoll;var hit=WeaponMissiles.HitRoll;var block=SourceBlocking.BlockRoll;var item=WeaponMissiles.ItemEffects;
        try{AmazonBow.LaunchRandom=new Minimum();WeaponMissiles.DamageRoll=_=>0;WeaponMissiles.HitRoll=()=>throw new InvalidOperationException("Lightning Bolt called AR");SourceBlocking.BlockRoll=_=>throw new InvalidOperationException("Lightning-only packet called physical shield block");WeaponMissiles.ItemEffects=new Maximum();action();}
        finally{AmazonBow.LaunchRandom=random;WeaponMissiles.DamageRoll=damage;WeaponMissiles.HitRoll=hit;SourceBlocking.BlockRoll=block;WeaponMissiles.ItemEffects=item;}
    }
    private static WeaponFlight Cast(PlayerMobile p,Mobile target=null)
    {
        Ready(p);int mana=NecromancerEffects.ManaBalance256(p),quantity=NamedAmmunition.Remaining(CombatTiming.Equipped(p));
        Require(ClassCombat.Perform(p,CombatAbility.LightningBolt,target,target?.Location??MagicArrowChecks.Aim(p,18),Core.TickCount),"Throw start");var s=ClassCombat.For(p);
        Require(NecromancerEffects.ManaBalance256(p)==mana-SkillMechanics.Values(p,CombatAbility.LightningBolt).Mana256 && NamedAmmunition.Remaining(CombatTiming.Equipped(p))==quantity,"Start mana/quantity phase");
        Require(!ClassCombat.ResolveAction(p,s.EffectAt-1) && ClassCombat.ResolveAction(p,s.EffectAt) && !ClassCombat.ResolveAction(p,s.EffectAt+1),"Early/repeated emission");
        var f=Own(p).Single();Require(f.Missile=="lightningjavelin" && f.Frames==25 && f.SkillRank==SkillMechanics.Rank(p,CombatAbility.LightningBolt) && NamedAmmunition.Remaining(CombatTiming.Equipped(p))==quantity-1 && FireGround.Ready(p,CombatAbility.LightningBolt),"Saved source parent/release quantity/localdelay");return f;
    }
    private static void Differential()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"lightning-bolt-source-probe.json")));int count=0;
        foreach(var r in data.RootElement.GetProperty("impact").EnumerateArray())
        {
            int V(int i)=>r[i].GetInt32();int draws=0;
            int Random(int span){int index=draws++;return span>0 && V(6)!=0 && index%2!=0?span-1:0;}
            var e=new MissileSourceStats(257,513,769,1025,1281,1537,1793,2049,13,257,513,101,3,7,9);
            var packet=new WeaponMissileDamage(V(0),V(0)>0?V(0)+500:0,V(1),31,47,V(2)!=0,e,LightningMastery:V(4)){LightningJavelinPercent=V(3)};
            var a=packet.Roll(V(5)!=0,V(5)==2,V(5)==3,Random);
            Require(NamedElements.To256(a.Physical)==V(7) && NamedElements.To256(a.Fire)==V(8) && NamedElements.To256(a.Magic)==V(9) && NamedElements.To256(a.Lightning)==V(10) && NamedElements.To256(a.Cold)==V(11) && NamedElements.To256(a.Poison)==V(12)*V(13) && a.PoisonMs==V(13)*40 && a.ColdMs==V(14)*40 && draws==V(17),"Full damage reset/reroll differential row "+count);count++;
        }
        Require(count==2560,"Impact count");count=0;
        int legacyOverflow=0;var fields=data.RootElement.GetProperty("fields").EnumerateArray().Select(x=>x.GetString()).ToArray();
        foreach(var r in data.RootElement.GetProperty("creator").EnumerateArray())
        {
            int V(int i)=>r[i].GetInt32();int Source(string field)=>r[5][Array.IndexOf(fields,field)].GetInt32();
            var values=new SkillValues(SkillMechanics.For(CombatAbility.LightningBolt),_=>V(0),_=>V(1),99);
            var stats=new MissileSourceStats(FireMin:2560,FireMax:7936,LightningMin:V(3)*256+AmazonMelee.EnhanceEndpoint(values.Damage256(false),V(2)),LightningMax:(V(3)+19)*256+AmazonMelee.EnhanceEndpoint(values.Damage256(true),V(2)),LifeDrain:7);
            var a=BowLaunch.FromSource(1280,2560,0,0,96,94,0,11,13,V(4)!=0,stats);
            bool overflow=(long)stats.LightningMax*96>int.MaxValue;if(overflow){legacyOverflow++;Require(Source("nLightMaxDamage")<0 && a.Elements.LightningMax==(int)((long)stats.LightningMax*96/128),"Bounded older signed-overflow edge");}
            Require(a.Min256==Source("nMinDamage") && a.Max256==Source("nMaxDamage") && a.Percent==Source("nDamagePercent") && a.DemonPercent==Source("nDemonDamagePercent") && a.UndeadPercent==Source("nUndeadDamagePercent") && a.Critical==((Source("nFlags")&2)!=0) && a.Elements.LightningMin==Source("nLightMinDamage") && (overflow || a.Elements.LightningMax==Source("nLightMaxDamage")) && a.Elements.FireMin==Source("nFireMinDamage") && a.Elements.FireMax==Source("nFireMaxDamage") && a.Elements.LifeDrain==Source("nLifeDrainMinDamage"),"Full creator/getter differential row "+count+" row="+r+" actual="+JsonSerializer.Serialize(a)+" values="+values.Damage256(false)+","+values.Damage256(true));count++;
        }
        Require(count==288 && legacyOverflow==8,"Creator count/explicit older overflow exclusions");count=0;
        foreach(var r in data.RootElement.GetProperty("steps").EnumerateArray())
        {
            var a=SourceMissileSteps.Crossings((100L<<16)+r[0].GetInt32(),(100L<<16)+r[1].GetInt32(),r[2].GetInt32(),r[3].GetInt32());
            Require(a.Cells.SequenceEqual(r[4].EnumerateArray().Select(x=>new SourceMissileCell(x[0].GetInt32(),x[1].GetInt32()))),"Full ordered path differential row "+count);count++;
        }
        Require(count==392,"Path count");count=0;
        foreach(var r in data.RootElement.GetProperty("timing").EnumerateArray())
        {int mode=r[0].GetInt32();bool expected=mode==11 || mode==13 && r[1].GetInt32()!=0 && r[2].GetInt32()!=0;Require(expected==(r[3].GetInt32()!=0),"TH source selector");count++;}Require(count==12,"Mode count");
        Log.Information("Lightning Bolt:2560 impact/reset,280 exact creator plus8 explicit older-overflow exclusions,392 ordered path and12 mode source comparisons passed");
    }
    private static Item Named(PlayerMobile p,string id)
    {var item=NamedGear.Create(id,99,new Random(41),true);p.AddToBackpack(item);Require(p.EquipItem(item) && ClassGear.Active(p,item),"Actual named gear "+id);return item;}
    private static void Cases()
    {
        var p=Fresh();var w=Weapon(p);try
        {
            Require(!ClassCombat.Available(p,CombatAbility.LightningBolt),"Unlearned bolt");Train(p,CombatAbility.LightningBolt);
            var values=SkillMechanics.Values(p,CombatAbility.LightningBolt);Require(values.Mana256==1536 && values.Damage256(false)==256 && values.Damage256(true)==40*256,"Pinned rank1");
            var timing=CombatTiming.For(p,CombatAbility.LightningBolt);var gloves=Named(p,"unique/105");Require(CombatTiming.For(p,CombatAbility.LightningBolt)==timing,"Throw used FCR");p.AddToBackpack(gloves);gloves.Delete();var armor=Named(p,"unique/82");Require(CombatTiming.For(p,CombatAbility.LightningBolt).DurationMs<timing.DurationMs,"Throw ignored IAS");p.AddToBackpack(armor);armor.Delete();
            Ready(p);Require(ClassCombat.Perform(p,CombatAbility.LightningBolt,null,p.Location,Core.TickCount) && ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt),"Self-point fallback");var f=Own(p).Single();Require(f.Vx>0 && f.Vx==f.Vy,"Source diagonal fallback");AmazonJavelins.Cancel(p);
            Ready(p);NecromancerEffects.SetMana256(p,1535);Require(!ClassCombat.Perform(p,CombatAbility.LightningBolt,null,MagicArrowChecks.Aim(p),Core.TickCount),"Insufficient mana");MagicArrowChecks.Mana(p);
            Ready(p);int mana=NecromancerEffects.ManaBalance256(p),quantity=NamedAmmunition.Remaining(w);Require(ClassCombat.Perform(p,CombatAbility.LightningBolt,null,MagicArrowChecks.Aim(p),Core.TickCount),"Cancel start");long at=ClassCombat.For(p).EffectAt;ClassCombat.Stop(p);Require(!ClassCombat.ResolveAction(p,at) && NecromancerEffects.ManaBalance256(p)==mana-1536 && NamedAmmunition.Remaining(w)==quantity,"Cancel debit phase");
            MagicArrowChecks.Mana(p);Ready(p);Require(ClassCombat.Perform(p,CombatAbility.LightningBolt,null,MagicArrowChecks.Aim(p),Core.TickCount),"Paid start");NecromancerEffects.SetMana256(p,0);Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt),"Release repeated affordability");AmazonJavelins.Cancel(p);
            MagicArrowChecks.Mana(p);Ready(p);Require(ClassCombat.Perform(p,CombatAbility.LightningBolt,null,MagicArrowChecks.Aim(p),Core.TickCount),"Equipment start");at=ClassCombat.For(p).EffectAt;p.AddToBackpack(w);Require(p.EquipItem(w) && !ClassCombat.ResolveAction(p,at),"Equipment revived windup");
            // Real item lightning stays raw; skill mastery applies before the whole96/128 creator scaling.
            var eye=Named(p,"unique/336");var ring=Named(p,"unique/122");values=SkillMechanics.Values(p,CombatAbility.LightningBolt);var capture=BowLaunch.Capture(p,CombatAbility.LightningBolt,false);int mastery=NamedElements.MasteryPercent(p,"ltng");
            Require(mastery>0 && capture.Elements.LightningMin==(NamedElements.To256(GearEffects.Damage(p,0).Lightning)+AmazonMelee.EnhanceEndpoint(values.Damage256(false),mastery))*96/128 && capture.Elements.LightningMax==(NamedElements.To256(GearEffects.Damage(p,1).Lightning)+AmazonMelee.EnhanceEndpoint(values.Damage256(true),mastery))*96/128,"Actual Griffon/SoJ combined pool/mastery");p.AddToBackpack(eye);eye.Delete();p.AddToBackpack(ring);ring.Delete();
            var ordinary=ClassGear.State(w);var originalResource=w.Resource;w.Resource=CraftResource.Bloodwood;ClassGear.Profiles.Store(w,ordinary with{CraftedEquipment=true,MaterialRolls=[new("damage-physical",5)]});var baseDamage=ClassStats.PhysicalDamageRange(p);var material=BowLaunch.Capture(p,CombatAbility.LightningBolt,false).PhysicalSupplement;Require(material!=null && material.Min256==NamedElements.To256(GearEffects.Damage(p,0).Physical-baseDamage.Min)*96/128 && material.Max256==NamedElements.To256(GearEffects.Damage(p,1).Physical-baseDamage.Max)*96/128,"Existing throwing material endpoints and75percent supplement");ClassGear.Profiles.Store(w,ordinary);w.Resource=originalResource;
            CombatPotions.Apply(p,2);capture=BowLaunch.Capture(p,CombatAbility.LightningBolt,false);Require(capture.PhysicalSupplement is{Min256:1920,Max256:1920},"Existing Deep Wounds supplement scaled75percent");TimedEffects.RemoveBuff(p,TimedEffectCategory.Potion);
            // Item poison/cold and both leech lanes are present in the captured packet, then cleared at impact.
            var cold=Named(p,"unique/118");var leech=Named(p,"unique/121");var poison=Named(p,"unique/232");p.Hits=p.HitsMax-100;MagicArrowChecks.Mana(p);var target=Target(p);target.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);
            f=Cast(p,target);Require(f.Damage.Elements.ColdMin>0 && f.Damage.Elements.PoisonMin>0 && f.Damage.Elements.LifeDrain>0 && f.Damage.Elements.ManaDrain>0,"Real reset fixture lanes");int life=SkillHealth.Balance256(target),ownerLife=SkillHealth.Balance256(p);mana=NecromancerEffects.ManaBalance256(p);var expected=f.Damage.Roll(target,_=>0);Require(expected.Physical==0 && expected.Fire==0 && expected.Cold==0 && expected.Poison==0 && expected.Magic==0 && expected.ColdMs==0 && expected.PoisonMs==0,"Pure lightning packet");
            for(int i=0;i<5 && Own(p).Length>0;i++){WeaponMissiles.Tick(Own(p).Single().NextUtc);}Require(Own(p).Length==0 && SkillHealth.Balance256(target)<life && SkillHealth.Balance256(p)==ownerLife && NecromancerEffects.ManaBalance256(p)==mana && TimedEffects.Snapshot().All(x=>x.Owner!=target.Serial.Value || x.PoisonClock==null && x.Cold==null),"Impact reset/status/leech/accuracy");target.Delete();foreach(var item in new[]{cold,leech,poison}){p.AddToBackpack(item);item.Delete();}
            var crushing=Named(p,"unique/90");var crushed=Target(p);crushed.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);MagicArrowChecks.Mana(p);f=Cast(p,crushed);int beforeCrush=SkillHealth.Balance256(crushed),crushLoss=NamedCombat.CrushingLoss256(crushed,true);var itemRandom=WeaponMissiles.ItemEffects;WeaponMissiles.ItemEffects=new Minimum();try{while(Own(p).Length>0){WeaponMissiles.Tick(Own(p).Single().NextUtc);}}finally{WeaponMissiles.ItemEffects=itemRandom;}Require(beforeCrush-SkillHealth.Balance256(crushed)==crushLoss+NamedElements.To256(f.Damage.Roll(crushed,_=>0).Lightning),"Lightning-only missile skipped item Crushing Blow");crushed.Delete();p.AddToBackpack(crushing);crushing.Delete();
            Train(p,CombatAbility.Pierce);MagicArrowChecks.Mana(p);var a=Target(p);a.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);var b=Target(p);b.MoveToWorld(new(p.X+6,p.Y,p.Z),p.Map);f=Cast(p,a);Require(f.Pierces==4,"Actual Pierce passive");int ah=SkillHealth.Balance256(a),bh=SkillHealth.Balance256(b);quantity=NamedAmmunition.Remaining(w);for(int i=0;i<10;i++){WeaponMissiles.Tick(Own(p).Single().NextUtc);}Require(SkillHealth.Balance256(a)<ah && SkillHealth.Balance256(b)<bh && Own(p).Single().Pierces==2 && NamedAmmunition.Remaining(w)==quantity,"Two victims/Pierce/LastCollide/quantity");AmazonJavelins.Cancel(p);a.Delete();b.Delete();
            while(NamedAmmunition.Remaining(w)>1){NamedAmmunition.Consume(w,TimedEffects.Now);}MagicArrowChecks.Mana(p);Ready(p);Require(ClassCombat.Perform(p,CombatAbility.LightningBolt,null,MagicArrowChecks.Aim(p),Core.TickCount),"Last quantity start");NamedAmmunition.Consume(w,TimedEffects.Now);Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt) && Own(p).Length==1 && NamedAmmunition.Remaining(w)==0,"Release quantity regate");AmazonJavelins.Cancel(p);Ready(p);mana=NecromancerEffects.ManaBalance256(p);Require(!ClassCombat.Perform(p,CombatAbility.LightningBolt,null,MagicArrowChecks.Aim(p),Core.TickCount) && NecromancerEffects.ManaBalance256(p)==mana,"Empty throw paid");
        }finally{AmazonJavelins.Cancel(p);p.Delete();}
    }
    private static void Lifecycle()
    {
        foreach(bool death in new[]{false,true})
        {
            var p=Fresh();Train(p,CombatAbility.LightningBolt);Weapon(p);Cast(p);Require(Own(p).Length==1,"Lifecycle fixture");
            if(death){p.Kill();}else{ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);}
            Require(Own(p).Length==0,"Death/respec retained bolt");p.Corpse?.Delete();p.Delete();
        }
    }
    private static void Persist()
    {
        var p=Fresh();Train(p,CombatAbility.LightningBolt);var w=Weapon(p);var target=Target(p);target.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.LightningBolt) && SkillBindings.Instance.Assign(p,false,CombatAbility.LightningBolt) && SkillBindings.Instance.AssignQuick(p,3,CombatAbility.LightningBolt),"Saved bindings");
        var f=Cast(p,target);WeaponMissiles.Tick(f.NextUtc.AddMilliseconds(40));f=Own(p).Single();Require(f.Frame==2 && f.SourceSteps.Cells.Length>1,"Actual saved ordered steps");f=f with{NextUtc=TimedEffects.Now.AddMinutes(10)};Hold(p,f,[]);Ready(p);Require(ClassCombat.Perform(p,CombatAbility.LightningBolt,null,MagicArrowChecks.Aim(p),Core.TickCount),"Paid/unemitted save start");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,target.Serial.Value,w.Serial.Value,p.Location,f,[],SkillHealth.Balance256(target),NecromancerEffects.ManaBalance256(p),NamedAmmunition.Remaining(w),null,default)));
    }
    private static void Check()=>Controlled(()=>{Differential();Cases();Lifecycle();Persist();});
    private static void Reload()=>Controlled(()=>
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var target=(BowLaunchTarget)World.FindMobile((Serial)s.Target);var w=(BaseWeapon)World.FindItem((Serial)s.Weapon);
        Require(Own(p).Length==1 && Own(p).Single()==s.Flight,"Saved flight structural equality");Require(SkillHealth.Balance256(target)==s.Health && NecromancerEffects.ManaBalance256(p)==s.Mana && NamedAmmunition.Remaining(w)==s.Quantity,"Saved resources");Require(ClassCombat.For(p).PendingAbility==CombatAbility.None && SkillBindings.Instance.Get(p)==(CombatAbility.LightningBolt,CombatAbility.LightningBolt) && SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Amazon,3)==CombatAbility.LightningBolt && FireGround.Ready(p,CombatAbility.LightningBolt),"Saved bindings/pending/no localdelay");
        p.MoveToWorld(s.Origin,Map.Maps[s.Flight.Map]);target.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);target.SetResistance(ResistanceType.Energy,0);p.AddToBackpack(w);int health=SkillHealth.Balance256(target);var packet=s.Flight.Damage.Roll(target,_=>0);var now=TimedEffects.Now;Hold(p,s.Flight with{NextUtc=now},[]);WeaponMissiles.Tick(now);Require(Own(p).Length==0 && SkillHealth.Balance256(target)==health-NamedElements.To256(packet.Lightning),"Resume original wider path/lightning packet");Require(NecromancerEffects.ManaBalance256(p)==s.Mana && NamedAmmunition.Remaining(w)==s.Quantity,"Reload repaid resources");Require(p.EquipItem(w),"Restore weapon");var held=s.Flight with{NextUtc=TimedEffects.Now.AddMinutes(10)};Hold(p,held,[]);Ready(p);Require(ClassCombat.Perform(p,CombatAbility.LightningBolt,null,MagicArrowChecks.Aim(p),Core.TickCount),"Next pending windup");File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Flight=held,Health=SkillHealth.Balance256(target),Mana=NecromancerEffects.ManaBalance256(p)}));
    });
    private static void Input()
    {
        var p=Fresh();Train(p,CombatAbility.LightningBolt);Weapon(p);Ready(p);var weapon=CombatTiming.Equipped(p);int quantity=NamedAmmunition.Remaining(weapon);var target=Target(p);target.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.LightningBolt) && SkillBindings.Instance.Assign(p,false,CombatAbility.LightningBolt) && SkillBindings.Instance.AssignQuick(p,4,CombatAbility.LightningBolt),"Connected bindings");
        var network=NetState.Instances.Single(x=>x.Mobile==null && IPAddress.IsLoopback(x.Address));network.Seeded=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var protocol=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);Require(protocol!=null,"Protocol bootstrap changed");protocol.SetValue(network,Enum.Parse(protocol.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        int before=NecromancerEffects.ManaBalance256(p),health=SkillHealth.Balance256(target);bool started=false,emitted=false;long deadline=Core.TickCount+5000;
        var hit=WeaponMissiles.HitRoll;var damage=WeaponMissiles.DamageRoll;var block=SourceBlocking.BlockRoll;WeaponMissiles.HitRoll=()=>0;WeaponMissiles.DamageRoll=_=>0;SourceBlocking.BlockRoll=_=>99;
        void Clean(){WeaponMissiles.HitRoll=hit;WeaponMissiles.DamageRoll=damage;SourceBlocking.BlockRoll=block;WeaponMissiles.Cancel(p);target.Delete();p.Delete();}
        void Wait()
        {
            try
            {
                Require(Core.TickCount<deadline,"Connected fan did not complete");
                if(!started && ClassCombat.For(p).PendingAbility==CombatAbility.LightningBolt)
                {started=true;Require(NecromancerEffects.ManaBalance256(p)==before-1536 && NamedAmmunition.Remaining(weapon)==quantity && Own(p).Length==0,"Natural start4 phase");}
                var fan=Own(p);if(!emitted && fan.Length>0)
                {emitted=true;Require(started && fan.Length==1 && fan.All(x=>x.Frame<3 && x.Missile=="lightningjavelin") && SkillHealth.Balance256(target)==health && NamedAmmunition.Remaining(weapon)==quantity-1,"Natural do8/ammunition phase");}
                if(!emitted || fan.Length>0){Timer.StartTimer(TimeSpan.FromMilliseconds(30),Wait);return;}
                Require(SkillHealth.Balance256(target)<health && NecromancerEffects.ManaBalance256(p)==before-1536 && NamedAmmunition.Remaining(weapon)==quantity-1,"Natural travelling fan/resource accounting");Clean();Log.Information("lightning-bolt-input-result: passed");
            }catch(Exception e){Clean();Log.Error(e,"lightning-bolt-input-result: failed");}
        }
        var point=target.Location;Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("lightning-bolt-input: ready ability={Ability} x={X} y={Y} z={Z}",(int)CombatAbility.LightningBolt,point.X,point.Y,point.Z);Wait();});
    }
}
