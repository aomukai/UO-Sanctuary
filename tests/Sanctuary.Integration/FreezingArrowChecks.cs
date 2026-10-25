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

public static class FreezingArrowChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(FreezingArrowChecks));
    private sealed class Maximum:Random{public override int Next(int max)=>Math.Max(0,max-1);public override int Next(int min,int max)=>Math.Max(min,max-1);}
    private sealed class Minimum:Random{public override int Next(int max)=>0;}
    private sealed record Saved(uint Owner,uint First,uint Second,uint Bow,uint Ammo,Point3D Origin,WeaponFlight Flight,BowExplosion Blast,uint Helm,uint Ring,NamedControlState Control,int FirstHealth,int SecondHealth,int Mana,int Quantity);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"freezing-arrow.json");
    public static void Configure(){Register("freezing-arrow-check",Check);Register("freezing-arrow-reload-check",Reload);Register("freezing-arrow-input-check",Input);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Freezing Arrow parent/child fire split, real input, gear and saved state",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static PlayerMobile Fresh()=>MagicArrowChecks.Fresh("FreezingArrowOwner");
    private static void Ready(PlayerMobile p)=>MagicArrowChecks.Ready(p);
    private static void Mana(PlayerMobile p)=>MagicArrowChecks.Mana(p);
    private static Point3D Aim(PlayerMobile p)=>MagicArrowChecks.Aim(p);
    private static void Train(PlayerMobile p,CombatAbility ability,int rank=1)
    {
        var row=SkillMechanics.For(ability);Require(TalentCatalog.TryGet(row.CharacterClass,row.TalentId,out var talent),"Talent");
        foreach(string id in talent.Prerequisites){Train(p,SkillMechanics.All.Single(x=>x.TalentId==id).Ability);}
        while(ClassCreation.Profiles.GetRank(p.Serial,row.TalentId)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,row.TalentId,out var why),"Train "+why);}
    }
    private static void Differential()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"freezing-arrow-damage-source-probe.json")));var root=doc.RootElement;
        var fields=root.GetProperty("creatorFields").EnumerateArray().Select(x=>x.GetString()).ToArray();int count=0;
        foreach(var row in root.GetProperty("creator").EnumerateArray())
        {
            int lo=row[0].GetInt32(),add=row[1].GetInt32();var actual=BowLaunch.FromSource(lo*256,(lo+5)*256,0,0,128,row[4].GetInt32()+57,0,0,0,row[5].GetInt32()!=0,
                new(ColdMin:add+7*256,ColdMax:add+512+13*256,ColdFrames:row[2].GetInt32()+row[3].GetInt32()));
            int Expected(string key)=>row[6][Array.IndexOf(fields,key)].GetInt32();Require(actual.Min256==Expected("nMinDamage") && actual.Max256==Expected("nMaxDamage") && actual.Percent==Expected("nDamagePercent")
                && actual.Elements.ColdMin==Expected("nColdMinDamage") && actual.Elements.ColdMax==Expected("nColdMaxDamage") && actual.Elements.ColdFrames==Expected("nColdLength"),"Complete freeze child creator differential");count++;
        }Require(count==360,"Creator rows");count=0;
        foreach(var row in root.GetProperty("getter").EnumerateArray())
        {
            int cold=WeaponMissileDamage.RollValue(row[0].GetInt32(),row[0].GetInt32()+512,row[2].GetInt32(),_=>0);
            Require(row[6].GetInt32()==cold && row[7].GetInt32()==0 && row[8].GetInt32()==0 && row[9].GetInt32()==0 && row[10].GetInt32()==cold && row[11].GetInt32()==0
                && row[12].GetInt32()==row[1].GetInt32() && row[13].GetInt32()==0,"Freeze-only getter/length/critical/species isolation");count++;
        }Require(count==216,"Getter rows");Log.Information("Freezing Arrow:360 complete creator/216 freeze-only getter source/native rows passed;256 parent/4320 child-area source cases separate");
    }
    private static WeaponFlight Cast(PlayerMobile p,Mobile target=null,bool held=false,bool left=false)
    {
        Ready(p);int before=NecromancerEffects.ManaBalance256(p);var ammo=BowAmmunition.Stack(p,CombatTiming.Equipped(p));int quantity=ammo.Amount;var old=WeaponMissiles.Snapshot().Flights.Select(x=>x.Id).ToHashSet();
        bool start=held?(left?ClassCombat.ContinueAttack(p,CombatAbility.FreezingArrow,target,target?.Location??Aim(p),Core.TickCount):ClassCombat.ContinueCast(p,CombatAbility.FreezingArrow,target,target?.Location??Aim(p),Core.TickCount))
            :ClassCombat.Perform(p,CombatAbility.FreezingArrow,target,target?.Location??Aim(p),Core.TickCount);
        Require(start && NecromancerEffects.ManaBalance256(p)==before-SkillMechanics.Values(p,CombatAbility.FreezingArrow).Mana256 && ammo.Amount==quantity,"Freezing Arrow start payment");var s=ClassCombat.For(p);ClassCombat.ReleaseCast(p);
        Require(!ClassCombat.ResolveAction(p,s.EffectAt-1) && ClassCombat.ResolveAction(p,s.EffectAt) && !ClassCombat.ResolveAction(p,s.EffectAt),"Single Freezing Arrow event");
        var f=WeaponMissiles.Snapshot().Flights.Single(x=>!old.Contains(x.Id));Require(f.Ability==CombatAbility.FreezingArrow && f.Missile=="freezingarrow" && f.Frames==40 && f.NextDelay==0 && f.Damage.PhysicalToMagicPercent==0
            && f.Damage.Conversion==null && f.SkillRank==SkillMechanics.Rank(p,CombatAbility.FreezingArrow) && NecromancerEffects.ManaBalance256(p)==before-SkillMechanics.Values(p,CombatAbility.FreezingArrow).Mana256 && (ammo.Deleted?0:ammo.Amount)==quantity-1,"Captured weapon parent/once quantity/no repeated mana");return f;
    }
    private static void Controlled(Action action)
    {
        var launch=AmazonBow.LaunchRandom;var hit=WeaponMissiles.HitRoll;var damage=WeaponMissiles.DamageRoll;var block=SourceBlocking.BlockRoll;
        try{AmazonBow.LaunchRandom=new Minimum();WeaponMissiles.HitRoll=()=>0;WeaponMissiles.DamageRoll=_=>0;SourceBlocking.BlockRoll=_=>99;action();}
        finally{AmazonBow.LaunchRandom=launch;WeaponMissiles.HitRoll=hit;WeaponMissiles.DamageRoll=damage;SourceBlocking.BlockRoll=block;}
    }
    private static void Finish(long id)
    {for(int i=0;i<250 && WeaponMissiles.State(id) is{} f;i++){WeaponMissiles.Tick(f.NextUtc);}Require(WeaponMissiles.State(id)==null,"Parent projectile did not finish");}
    private static BowLaunchTarget Target(PlayerMobile p,int physical=0,int fire=0)
    {var t=MagicArrowChecks.Target(p);t.SetResistance(ResistanceType.Physical,physical);t.SetResistance(ResistanceType.Cold,fire);return t;}
    private static WeaponFlight[] Own(PlayerMobile p)=>WeaponMissiles.Snapshot().Flights.Where(x=>x.Owner==p.Serial.Value).ToArray();
    private static BowExplosion[] Blasts(PlayerMobile p)=>BowExplosions.Snapshot().Where(x=>x.Owner==p.Serial.Value).ToArray();
    private static BowExplosion Contact(PlayerMobile p)
    {
        for(int i=0;i<100 && Blasts(p).Length==0 && Own(p).Length>0;i++){WeaponMissiles.Tick(Own(p).Min(x=>x.NextUtc));}
        Require(Blasts(p).Length==1,"Parent did not create one delayed child");return Blasts(p)[0];
    }
    private static BowLaunchTarget Enemy(PlayerMobile p,int dx=5,int dy=0)
    {var t=Target(p);t.MoveToWorld(new(p.X+dx,p.Y+dy,p.Z),p.Map);return t;}
    private static void Cases()
    {
        var p=Fresh();try
        {
            var bow=MagicArrowChecks.Bow(p,typeof(Bow));Require(!ClassCombat.Available(p,CombatAbility.FreezingArrow),"Unlearned exploding arrow available");Train(p,CombatAbility.FreezingArrow);
            Require(CombatTiming.WeaponAttack(CombatAbility.FreezingArrow) && CombatTiming.For(p,CombatAbility.FreezingArrow)==CombatTiming.For(p,CombatAbility.MagicArrow)
                && SkillBindings.Instance.Assign(p,true,CombatAbility.FreezingArrow) && SkillBindings.Instance.Assign(p,false,CombatAbility.FreezingArrow) && SkillBindings.Instance.AssignQuick(p,10,CombatAbility.FreezingArrow),"Bow timing/left/right/quick bindings");
            Ready(p);int mana=NecromancerEffects.ManaBalance256(p);Require(!ClassCombat.Perform(p,CombatAbility.FreezingArrow,null,Aim(p),Core.TickCount) && NecromancerEffects.ManaBalance256(p)==mana,"No ammo accepted/paid");
            var bolts=new Bolt(4);p.AddToBackpack(bolts);Ready(p);Require(!ClassCombat.Perform(p,CombatAbility.FreezingArrow,null,Aim(p),Core.TickCount),"Wrong ammo accepted");
            var arrows=new Arrow(30);p.AddToBackpack(arrows);var f=Cast(p);Require(f.Damage.Elements.ColdMin==0 && f.Damage.Elements.ColdMax==0,"Parent incorrectly added skill cold");WeaponMissiles.Cancel(p);
            Cast(p,held:true);WeaponMissiles.Cancel(p);Cast(p,held:true,left:true);WeaponMissiles.Cancel(p);Require(arrows.Amount==27 && bolts.Amount==4,"Once-only arrow consumption");
            bow=MagicArrowChecks.Bow(p,typeof(Crossbow));Cast(p);WeaponMissiles.Cancel(p);Require(arrows.Amount==27 && bolts.Amount==3,"Crossbow bolt selection");bow=MagicArrowChecks.Bow(p,typeof(Bow));
            Ready(p);NecromancerEffects.SetMana256(p,2303);Require(!ClassCombat.Perform(p,CombatAbility.FreezingArrow,null,Aim(p),Core.TickCount) && NecromancerEffects.ManaBalance256(p)==2303,"Insufficient start mana accepted/debited");
            Mana(p);Ready(p);mana=NecromancerEffects.ManaBalance256(p);int amount=arrows.Amount;Require(ClassCombat.Perform(p,CombatAbility.FreezingArrow,null,Aim(p),Core.TickCount),"Paid cancel start");long at=ClassCombat.For(p).EffectAt;ClassCombat.Stop(p);
            Require(!ClassCombat.ResolveAction(p,at) && NecromancerEffects.ManaBalance256(p)==mana-2304 && arrows.Amount==amount,"Cancelled paid arrow refunded or used ammo");
            Ready(p);Require(ClassCombat.Perform(p,CombatAbility.FreezingArrow,null,Aim(p),Core.TickCount),"Paid zero-mana release start");at=ClassCombat.For(p).EffectAt;NecromancerEffects.SetMana256(p,0);
            Require(ClassCombat.ResolveAction(p,at) && Own(p).Length==1 && arrows.Amount==amount-1,"Paid arrow charged twice");WeaponMissiles.Cancel(p);
            Train(p,CombatAbility.FreezingArrow,20);var v=SkillMechanics.Values(p,CombatAbility.FreezingArrow);
            Require(v.DurationFrames==52 && v.Mana256==3520 && v.Damage256(false)==88883 && v.Damage256(true)==91750,"Rank20 source tiers/hard Fire synergy/fractional mana");
        }finally{WeaponMissiles.Cancel(p);p.Delete();}
    }
    private static void Area()
    {
        var p=Fresh();var random=WeaponMissiles.DamageRoll;try
        {
            Train(p,CombatAbility.FreezingArrow);MagicArrowChecks.Bow(p,typeof(Bow));p.AddToBackpack(new Arrow(12));
            foreach(int mode in new[]{0,1,2})
            {
                Mana(p);var first=Enemy(p);var second=Enemy(p,5,1);var outside=Enemy(p,5,3);var hit=WeaponMissiles.HitRoll;var block=SourceBlocking.BlockRoll;
                try
                {
                    if(mode==1){WeaponMissiles.HitRoll=()=>.99999;}
                    if(mode==2)
                    {
                        SourceBlocking.Restore(SourceBlocking.Snapshot().Where(x=>x.Actor!=first.Serial.Value).Append(new SourceShield(first.Serial.Value,CreatureProfiles.Definition(first).Reference,1)).ToArray());
                        Require(SourceBlocking.TryRate(first,out int chance,out _) && chance==10,"Actual shield fixture");SourceBlocking.BlockRoll=_=>0;
                    }
                    int hp=SkillHealth.Balance256(first),two=SkillHealth.Balance256(second),far=SkillHealth.Balance256(outside),calls=0;WeaponMissiles.DamageRoll=n=>{calls++;return 0;};Cast(p,first);var blast=Contact(p);
                    int after=SkillHealth.Balance256(first);Require((after<hp)==(mode==0) && SkillHealth.Balance256(second)==two && SkillHealth.Balance256(outside)==far,"Parent damage versus delayed area/miss/block");
                    Require(blast.FreezeColdLength && blast.ColdFrames==52 && blast.Min256==11468 && blast.Max256==14336 && blast.NextUtc>TimedEffects.Now.AddMilliseconds(-500),"Hard Fire source fire-only child snapshot");int oldCalls=calls;
                    BowExplosions.Tick(blast.NextUtc.AddTicks(-1));Require(SkillHealth.Balance256(second)==two,"Child hit before its source update");BowExplosions.Tick(blast.NextUtc);
                    Require(calls==oldCalls+1 && SkillHealth.Balance256(first)==after-blast.Min256 && SkillHealth.Balance256(second)==two-blast.Min256 && SkillHealth.Balance256(outside)==far,"One cold area roll/primary overlap/radius/no shield or AR on child");
                    Require(NamedControl.FrozenRemaining(first)>0 && NamedControl.FrozenRemaining(second)>0 && NamedControl.FrozenRemaining(outside)==0 && TimedEffects.Chill(second,TimedEffects.Now)==0,"Area freeze/radius without ordinary chill");
                }finally{WeaponMissiles.HitRoll=hit;SourceBlocking.BlockRoll=block;WeaponMissiles.Cancel(p);first.Delete();second.Delete();outside.Delete();}
            }
        }finally{WeaponMissiles.DamageRoll=random;WeaponMissiles.Cancel(p);p.Delete();}
    }
    private static void LiveGear()
    {
        var p=Fresh();try
        {
            Train(p,CombatAbility.FreezingArrow);MagicArrowChecks.Bow(p,typeof(Bow));p.AddToBackpack(new Arrow(12));var target=Enemy(p);
            try
            {
                var parent=Cast(p,target);var helm=NamedGear.Create("unique/343",99,new Maximum(),true);p.AddToBackpack(helm);Require(p.EquipItem(helm),"Actual Nightwing after launch");
                var blast=Contact(p);Require(parent.SkillRank==1 && parent.Damage.Elements.ColdMin==0 && blast.Rank==1 && blast.Min256==13188 && blast.Max256==16486 && blast.ColdFrames==52,"Child retained rank/live cold mastery/hard-only synergies");WeaponMissiles.Cancel(p);Mana(p);
                parent=Cast(p,target);Require(parent.SkillRank==3 && parent.Damage.Elements.ColdMin==0,"Actual parent weapon-only cold");p.AddToBackpack(helm);
                blast=Contact(p);Require(blast.Rank==3 && blast.Min256==17203 && blast.Max256==20070 && blast.ColdFrames==52,"Removed Nightwing rewrote captured rank or left stale mastery");WeaponMissiles.Cancel(p);Mana(p);
                parent=Cast(p,target);Train(p,CombatAbility.ColdArrow,2);blast=Contact(p);Require(blast.Min256==12697 && blast.Max256==15872,"Live hard Cold synergy");WeaponMissiles.Cancel(p);Mana(p);
                parent=Cast(p,target);var ring=NamedGear.Create("unique/275",99,new Maximum(),true);p.AddToBackpack(ring);Require(p.EquipItem(ring),"Actual Raven after launch");blast=Contact(p);
                Require(parent.Damage.Elements.ColdMin==0 && blast.Min256==16537 && blast.Max256==27392 && blast.ColdFrames==152,"Live real item cold/length not collected by child");WeaponMissiles.Cancel(p);Mana(p);
                Require(p.EquipItem(helm),"Nightwing re-equip");var v=SkillMechanics.Values(p,CombatAbility.FreezingArrow);var cold=BowLaunch.ExplosionCold(p,v);
                Require(v.Rank==3 && v.DurationFrames==52 && cold.ColdFrames==152 && cold.ColdMin==26318 && cold.ColdMax==38801,$"Independent item/skill mastery and hard-only duration: {JsonSerializer.Serialize(cold)}");
            }finally{TimedEffects.Clear(target);target.Delete();}
        }finally{WeaponMissiles.Cancel(p);p.Delete();}
    }
    private static void TerrainAndPierce()
    {
        var p=Fresh();try
        {
            Train(p,CombatAbility.FreezingArrow);MagicArrowChecks.Bow(p,typeof(Bow));p.AddToBackpack(new Arrow(8));Cast(p);var wall=new Static(0x80);var behind=Enemy(p,4);
            try
            {
                wall.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);Require(!p.Map.LineOfSight(new Point3D(p.X,p.Y,p.Z+12),new Point3D(behind.X,behind.Y,behind.Z+12)),"Inserted wall fixture");int hp=SkillHealth.Balance256(behind);var blast=Contact(p);
                Require(Own(p).Length==0 && SkillHealth.Balance256(behind)==hp,$"Wall parent crossed geometry or dealt damage: flights={Own(p).Length} hp={SkillHealth.Balance256(behind)}/{hp} center={blast.X},{blast.Y} owner={p.X},{p.Y}");BowExplosions.Tick(blast.NextUtc);Require(SkillHealth.Balance256(behind)==hp-blast.Min256,"Source area incorrectly required LOS");
            }finally{wall.Delete();behind.Delete();WeaponMissiles.Cancel(p);}
            Mana(p);var lifetime=Cast(p);var end=Contact(p);Require(Own(p).Length==0 && Math.Abs(end.X-(p.X+lifetime.Vx*lifetime.Frames))<.01,$"Lifetime explosion position: end={end.X},{end.Y} expected={p.X+lifetime.Vx*lifetime.Frames},{p.Y+lifetime.Vy*lifetime.Frames}");WeaponMissiles.Cancel(p);
            Train(p,CombatAbility.Pierce);Mana(p);var first=Enemy(p,5);var second=Enemy(p,8);try
            {
                var shot=Cast(p);Require(shot.Pierces==4,"Pierce preroll cap");var ids=new System.Collections.Generic.HashSet<long>();
                for(int i=0;i<100 && Own(p).Length>0;i++){WeaponMissiles.Tick(Own(p)[0].NextUtc);foreach(var b in Blasts(p)){ids.Add(b.Id);}}
                Require(ids.Count==3,"Piercing contacts/lifetime did not create separate children");
            }finally{WeaponMissiles.Cancel(p);first.Delete();second.Delete();}
        }finally{WeaponMissiles.Cancel(p);p.Delete();}
    }
    private static void ElementsAndEvents()
    {
        var p=Fresh();var effects=WeaponMissiles.ItemEffects;try
        {
            Train(p,CombatAbility.FreezingArrow);MagicArrowChecks.Bow(p,typeof(Bow));p.AddToBackpack(new Arrow(8));
            foreach(string id in new[]{"unique/207","unique/88"})
            {var item=NamedGear.Create(id,99,new Random(71),true);p.AddToBackpack(item);Require(p.EquipItem(item) && ClassGear.Active(p,item),"Real parent event gear");}
            WeaponMissiles.ItemEffects=new Minimum();var first=Enemy(p);var second=Enemy(p,5,1);try
            {
                int hp=SkillHealth.Balance256(second);Cast(p,first);var blast=Contact(p);BowExplosions.Tick(blast.NextUtc);
                Require(TimedEffects.Slow(first,TimedEffects.Now)==20 && !TimedEffects.CanRegenerateHits(first) && TimedEffects.Snapshot().Single(x=>x.Owner==first.Serial.Value).BleedMs>0,"Parent weapon events lost");
                Require(SkillHealth.Balance256(second)==hp-blast.Min256 && TimedEffects.Slow(second,TimedEffects.Now)==0 && TimedEffects.CanRegenerateHits(second)
                    && !TimedEffects.Snapshot().Any(x=>x.Owner==second.Serial.Value && x.BleedMs>0),"Fire-only child inherited physical/attacker item events");
            }finally{WeaponMissiles.Cancel(p);TimedEffects.Clear(first);TimedEffects.Clear(second);first.Delete();second.Delete();}
        }finally{WeaponMissiles.ItemEffects=effects;WeaponMissiles.Cancel(p);p.Delete();}
    }
    private static void Hold(PlayerMobile p,WeaponFlight flight,BowExplosion blast)
    {
        var all=WeaponMissiles.Snapshot();WeaponMissiles.Restore(all with{Flights=all.Flights.Where(x=>x.Owner!=p.Serial.Value).Append(flight).ToArray(),
            Explosions=all.Explosions.Where(x=>x.Owner!=p.Serial.Value).Append(blast).ToArray()});
    }
    private static void FreezeCases()
    {
        var p=Fresh();try
        {
            Train(p,CombatAbility.FreezingArrow);MagicArrowChecks.Bow(p,typeof(Bow));p.AddToBackpack(new Arrow(20));
            foreach(int resistance in new[]{0,50,100})
            {
                Mana(p);var first=Enemy(p);var second=Enemy(p,5,1);second.SetResistance(ResistanceType.Cold,resistance);try
                {
                    int hp=SkillHealth.Balance256(second);Cast(p,first);var blast=Contact(p);BowExplosions.Tick(blast.NextUtc);int expected=blast.Min256*(100-resistance)/100;
                    Require(SkillHealth.Balance256(second)==hp-expected && (NamedControl.FrozenRemaining(second)>0)==(resistance<100) && TimedEffects.Chill(second,TimedEffects.Now)==0,"Cold damage/immunity/length/ordinary chill separation");
                    if(resistance<100){Require(TimedEffects.TryActor(second,out var state) && state.Control.Freeze.Source==p.Serial.Value,"Area freeze owner");}
                }finally{WeaponMissiles.Cancel(p);TimedEffects.Clear(first);TimedEffects.Clear(second);first.Delete();second.Delete();}
            }
            Mana(p);var target=Enemy(p);var elite=new EncounterCreature{NoKillAwards=true,Frozen=true,CantWalk=true};var profile=EncounterProfiles.Profile("crypt-sentinel-90");elite.Assign(profile,profile.MinimumLevel,MonsterDifficulty.Normal,EncounterGrade.Champion,ChampionKind.Champion,[],EliteAura.None,null,new Random(173));elite.MoveToWorld(new(p.X+5,p.Y+1,p.Z),p.Map);elite.AIObject.AITimer.Stop();elite.SetResistance(ResistanceType.Cold,0);
            try{Cast(p,target);var blast=Contact(p);BowExplosions.Tick(blast.NextUtc);Require(NamedControl.FrozenRemaining(elite)==0 && TimedEffects.Chill(elite,TimedEffects.Now)>0,"Elite freeze-to-chill fallback");}finally{WeaponMissiles.Cancel(p);TimedEffects.Clear(target);TimedEffects.Clear(elite);target.Delete();elite.Delete();}
            Mana(p);target=Enemy(p);var victim=Enemy(p,5,1);try
            {
                victim.SetHits(1);Cast(p,target);var blast=Contact(p);BowExplosions.Tick(blast.NextUtc);var corpse=victim.Corpse as Corpse;
                Require(corpse!=null && CorpseSkillRules.Shattered(corpse) && !CorpseSkillRules.Usable(p,corpse,20),"Lethal child freeze corpse remains usable");var gold=new Gold(777);corpse.DropItem(gold);Require(gold.IsChildOf(corpse),"Frozen child discarded ordinary loot");
            }finally{WeaponMissiles.Cancel(p);TimedEffects.Clear(target);TimedEffects.Clear(victim);target.Delete();victim.Delete();}
        }finally{WeaponMissiles.Cancel(p);p.Delete();}
    }
    private static void NaturalExpiry()
    {
        var p=Fresh();Train(p,CombatAbility.FreezingArrow);MagicArrowChecks.Bow(p,typeof(Bow));p.AddToBackpack(new Arrow(3));var target=Enemy(p);Cast(p,target);var blast=Contact(p);BowExplosions.Tick(blast.NextUtc);
        Require(NamedControl.FrozenRemaining(target)>0,"Natural freeze seed");Timer.StartTimer(TimeSpan.FromMilliseconds(3300),()=>
        {try{Require(NamedControl.FrozenRemaining(target)==0 && !FreezeStates.Waiting(target),"Natural area freeze/thaw expiry");Log.Information("freezing-arrow-state-result: passed");}catch(Exception e){Log.Error(e,"freezing-arrow-state-result: failed");}finally{TimedEffects.Clear(target);WeaponMissiles.Cancel(p);target.Delete();p.Delete();}});
    }
    private static void Persist()
    {
        var p=Fresh();Train(p,CombatAbility.FreezingArrow);var bow=MagicArrowChecks.Bow(p,typeof(Bow));var helm=NamedGear.Create("unique/343",99,new Maximum(),true);var ring=NamedGear.Create("unique/275",99,new Maximum(),true);p.AddToBackpack(helm);p.AddToBackpack(ring);Require(p.EquipItem(helm) && p.EquipItem(ring),"Saved real cold gear");
        // Six paid actions across the complete inspection/reload sequence.
        Require(p.ManaMax>=81,"Legal persistence resource budget");NecromancerEffects.SetMana256(p,80*256+17);
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.FreezingArrow) && SkillBindings.Instance.Assign(p,false,CombatAbility.FreezingArrow) && SkillBindings.Instance.AssignQuick(p,10,CombatAbility.FreezingArrow),"Saved bindings");
        var ammo=new Arrow(20);p.AddToBackpack(ammo);var first=Enemy(p);Cast(p,first);var actual=Contact(p);BowExplosions.Tick(actual.NextUtc);Require(TimedEffects.TryActor(first,out var state) && state.Control?.Freeze!=null,"Actual saved area freeze");
        var until=TimedEffects.Now.Add(FixtureSpace.Retention);state.Control=state.Control with{FreezeUntil=until,Freeze=state.Control.Freeze with{AiUntilUtc=until.AddMilliseconds(40)}};
        Cast(p,first);var blast=Contact(p) with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};Require(blast is{FreezeColdLength:true,ColdFrames:152,Min256:24199,Max256:36328},"Saved real cold child payload");
        first.MoveToWorld(new(p.X+5,p.Y+2,p.Z),p.Map);var second=Enemy(p);var flight=Cast(p,second) with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};Hold(p,flight,blast);Ready(p);Require(ClassCombat.Perform(p,CombatAbility.FreezingArrow,null,Aim(p),Core.TickCount),"Saved paid/unemitted windup");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,first.Serial.Value,second.Serial.Value,bow.Serial.Value,ammo.Serial.Value,p.Location,flight,blast,helm.Serial.Value,ring.Serial.Value,state.Control,
            SkillHealth.Balance256(first),SkillHealth.Balance256(second),NecromancerEffects.ManaBalance256(p),ammo.Amount)));
        var legacy=JsonSerializer.SerializeToNode(blast with{FreezeColdLength=false,ColdFrames=0});legacy.AsObject().Remove("FreezeColdLength");legacy.AsObject().Remove("ColdFrames");Require(legacy.Deserialize<BowExplosion>() is{FreezeColdLength:false,ColdFrames:0},"Prior fire child optional field defaults");
    }
    private static void Check()=>Controlled(()=>{Differential();Cases();Area();LiveGear();TerrainAndPierce();ElementsAndEvents();FreezeCases();Persist();NaturalExpiry();});
    private static void Reload()=>Controlled(()=>
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var first=(BowLaunchTarget)World.FindMobile((Serial)saved.First);var second=(BowLaunchTarget)World.FindMobile((Serial)saved.Second);var ammo=World.FindItem((Serial)saved.Ammo);var bow=World.FindItem((Serial)saved.Bow);var helm=World.FindItem((Serial)saved.Helm);var ring=World.FindItem((Serial)saved.Ring);
        Require(Own(p).Single()==saved.Flight && Blasts(p).Single()==saved.Blast && SkillHealth.Balance256(first)==saved.FirstHealth && SkillHealth.Balance256(second)==saved.SecondHealth && TimedEffects.TryActor(first,out var state) && state.Control==saved.Control
            && NecromancerEffects.ManaBalance256(p)==saved.Mana && ammo.Amount==saved.Quantity && ClassCombat.For(p).PendingAbility==CombatAbility.None && SkillBindings.Instance.Get(p)==(CombatAbility.FreezingArrow,CombatAbility.FreezingArrow) && SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Amazon,10)==CombatAbility.FreezingArrow,"Saved parent/child/freeze/resources/bindings or replayed windup");
        p.MoveToWorld(saved.Origin,Map.Maps[saved.Flight.Map]);p.AddToBackpack(bow);p.AddToBackpack(helm);p.AddToBackpack(ring);int parent=NamedElements.To256(saved.Flight.Damage.Roll(second,_=>0).Total);Hold(p,saved.Flight with{NextUtc=TimedEffects.Now},saved.Blast);
        for(int i=0;i<100 && Blasts(p).Length<2 && Own(p).Length>0;i++){WeaponMissiles.Tick(Own(p).Min(x=>x.NextUtc));}
        var next=Blasts(p).Single(x=>x.Id!=saved.Blast.Id);Require(next is{Rank:3,Min256:17203,Max256:20070,ColdFrames:52,FreezeColdLength:true},"Resumed parent rank or removed item/mastery/length");
        BowExplosions.Restore(BowExplosions.Snapshot().Select(x=>x.Owner==p.Serial.Value?x with{NextUtc=TimedEffects.Now}:x).ToArray());BowExplosions.Tick(TimedEffects.Now);
        Require(SkillHealth.Balance256(first)==saved.FirstHealth-saved.Blast.Min256-next.Min256 && SkillHealth.Balance256(second)==saved.SecondHealth-parent-saved.Blast.Min256-next.Min256 && NecromancerEffects.ManaBalance256(p)==saved.Mana && ammo.Amount==saved.Quantity,"Resumed typed cold/freeze children/parent/resource repayment");
        TimedEffects.TryActor(first,out state);Require(state.Control.FreezeUntil==saved.Control.FreezeUntil && state.Control.Freeze.Source==saved.Control.Freeze.Source,"Resumed shorter freeze replaced expiry/owner");var until=TimedEffects.Now.Add(FixtureSpace.Retention);state.Control=state.Control with{FreezeUntil=until,Freeze=state.Control.Freeze with{AiUntilUtc=until.AddMilliseconds(40)}};
        var held=saved.Flight with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};var blast=saved.Blast with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};Hold(p,held,blast);Require(p.EquipItem(bow) && p.EquipItem(helm) && p.EquipItem(ring),"Reload real cold gear");Ready(p);Require(ClassCombat.Perform(p,CombatAbility.FreezingArrow,null,Aim(p),Core.TickCount),"Next paid/unemitted windup");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Flight=held,Blast=blast,Control=state.Control,FirstHealth=SkillHealth.Balance256(first),SecondHealth=SkillHealth.Balance256(second),Mana=NecromancerEffects.ManaBalance256(p)}));
    });
    private static void Input()
    {
        var p=Fresh();Train(p,CombatAbility.FreezingArrow);MagicArrowChecks.Bow(p,typeof(Bow));Ready(p);var ammo=new Arrow(3);p.AddToBackpack(ammo);var target=Enemy(p);var second=Enemy(p,5,1);
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.FreezingArrow) && SkillBindings.Instance.Assign(p,false,CombatAbility.FreezingArrow) && SkillBindings.Instance.AssignQuick(p,10,CombatAbility.FreezingArrow),"Connected bindings");
        var network=NetState.Instances.Single(x=>x.Mobile==null && IPAddress.IsLoopback(x.Address));network.Seeded=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var protocol=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);Require(protocol!=null,"Protocol bootstrap changed");protocol.SetValue(network,Enum.Parse(protocol.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        int before=NecromancerEffects.ManaBalance256(p),health=SkillHealth.Balance256(target),two=SkillHealth.Balance256(second);bool started=false,emitted=false;long deadline=Core.TickCount+5000;
        var hit=WeaponMissiles.HitRoll;var damage=WeaponMissiles.DamageRoll;var block=SourceBlocking.BlockRoll;WeaponMissiles.HitRoll=()=>0;WeaponMissiles.DamageRoll=_=>0;SourceBlocking.BlockRoll=_=>99;
        void Clean(){WeaponMissiles.HitRoll=hit;WeaponMissiles.DamageRoll=damage;SourceBlocking.BlockRoll=block;WeaponMissiles.Cancel(p);target.Delete();second.Delete();p.Delete();}
        void Wait()
        {
            try
            {
                Require(Core.TickCount<deadline,"Connected Freezing Arrow did not complete");
                if(!started && ClassCombat.For(p).PendingAbility==CombatAbility.FreezingArrow)
                {started=true;Require(NecromancerEffects.ManaBalance256(p)==before-2304 && ammo.Amount==3 && Own(p).Length==0,"Natural start4 phase");}
                var flights=Own(p);if(!emitted && flights.Length>0)
                {emitted=true;Require(started && flights.Length==1 && flights[0].Frame<3 && flights[0].Missile=="freezingarrow" && SkillHealth.Balance256(target)==health && SkillHealth.Balance256(second)==two && ammo.Amount==2,"Natural single-arrow/ammunition phase");}
                if(!emitted || flights.Length>0 || Blasts(p).Length>0){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Wait);return;}
                Require(SkillHealth.Balance256(target)<health && SkillHealth.Balance256(second)==two-11468 && NecromancerEffects.ManaBalance256(p)==before-2304 && ammo.Amount==2,"Natural parent/child cold and once-only payment");Require(NamedControl.FrozenRemaining(target)>0 && NamedControl.FrozenRemaining(second)>0 && TimedEffects.Chill(second,TimedEffects.Now)==0,"Natural area freeze without ordinary chill");Clean();Log.Information("freezing-arrow-input-result: passed");
            }catch(Exception e){Clean();Log.Error(e,"freezing-arrow-input-result: failed");}
        }
        var point=Aim(p);Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("freezing-arrow-input: ready ability={Ability} x={X} y={Y} z={Z}",(int)CombatAbility.FreezingArrow,point.X,point.Y,point.Z);Wait();});
    }
}
