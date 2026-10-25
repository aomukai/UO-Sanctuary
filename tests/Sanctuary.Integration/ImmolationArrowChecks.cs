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
using Server.Spells;

namespace Sanctuary.Integration;

public static class ImmolationArrowChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ImmolationArrowChecks));
    private sealed class Maximum:Random{public override int Next(int max)=>Math.Max(0,max-1);public override int Next(int min,int max)=>Math.Max(min,max-1);}
    private sealed class Minimum:Random{public int Calls;public override int Next(int max){Calls++;return 0;}}
    private sealed record Saved(uint Owner,uint Target,uint Bow,uint Ring,uint Ammo,Point3D Origin,WeaponFlight Flight,FirePatch[] Fields,FireCooldown Cooldown,int Health,int Mana,int Quantity);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"immolation-arrow.json");
    public static void Configure(){Register("immolation-arrow-check",Check);Register("immolation-arrow-reload-check",Reload);Register("immolation-arrow-input-check",Input);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Immolation direct/area/disc fields, local delay, input and persistence",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Train(PlayerMobile p,CombatAbility ability,int rank=1)
    {
        var row=SkillMechanics.For(ability);Require(TalentCatalog.TryGet(row.CharacterClass,row.TalentId,out var talent),"Talent");foreach(string id in talent.Prerequisites){Train(p,SkillMechanics.All.Single(x=>x.TalentId==id).Ability);}
        while(ClassCreation.Profiles.GetRank(p.Serial,row.TalentId)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,row.TalentId,out var why),"Train "+why);}
    }
    private static PlayerMobile Fresh()
    {
        var p=MagicArrowChecks.Fresh("ImmolationArrowOwner");bool found=false;
        for(int x=2500;x<2900 && !found;x+=4)for(int y=1004;y<1450 && !found;y+=4)
        {
            int z=p.Map.GetAverageZ(x,y);bool clear=true;
            for(int dx=0;dx<28 && clear;dx++)for(int dy=-3;dy<=3 && clear;dy++)
            {var at=new Point3D(x+dx,y+dy,z);clear=p.Map.GetAverageZ(at.X,at.Y)==z && p.Map.CanFit(at,16,false,false,true) && !SpellHelper.IsTown(at,p)
                && p.Map.LineOfSight(new Point3D(x+5,y,z+12),new Point3D(at.X,at.Y,z+12));}
            if(!clear){continue;}foreach(var other in p.Map.GetMobilesInRange<Mobile>(new Point3D(x,y,z),32)){if(other!=p){clear=false;break;}}
            if(!clear){continue;}p.MoveToWorld(new(x,y,z),p.Map);found=true;
        }
        Require(found,"No open two-dimensional Immolation fixture");return p;
    }
    private static void Mana(PlayerMobile p)=>MagicArrowChecks.Mana(p);
    private static void Ready(PlayerMobile p)
    {MagicArrowChecks.Ready(p);var all=FireGround.Snapshot();FireGround.Restore(all with{Cooldowns=all.Cooldowns.Where(x=>x.Owner!=p.Serial.Value || x.Ability!=CombatAbility.ImmolationArrow).ToArray()});}
    private static WeaponFlight[] Own(PlayerMobile p)=>WeaponMissiles.Snapshot().Flights.Where(x=>x.Owner==p.Serial.Value).ToArray();
    private static FirePatch[] Fields(PlayerMobile p)=>FireGround.Snapshot().Patches.Where(x=>x.Owner==p.Serial.Value && x.Ability==CombatAbility.ImmolationArrow).OrderBy(x=>x.Id).ToArray();
    private static void Clean(PlayerMobile p){WeaponMissiles.Cancel(p);FireGround.Cancel(p);}
    private static BowLaunchTarget Enemy(PlayerMobile p,int x=5,int y=0)
    {var t=MagicArrowChecks.Target(p);t.MoveToWorld(new(p.X+x,p.Y+y,p.Z),p.Map);return t;}
    private static void Controlled(Action action)
    {
        var random=AmazonBow.LaunchRandom;var hit=WeaponMissiles.HitRoll;var damage=WeaponMissiles.DamageRoll;var block=SourceBlocking.BlockRoll;var soft=ImmolationArrows.SoftHitRoll;var effects=WeaponMissiles.ItemEffects;
        try{AmazonBow.LaunchRandom=new Minimum();WeaponMissiles.HitRoll=()=>0;WeaponMissiles.DamageRoll=_=>0;SourceBlocking.BlockRoll=_=>99;ImmolationArrows.SoftHitRoll=_=>127;WeaponMissiles.ItemEffects=new Minimum();action();}
        finally{AmazonBow.LaunchRandom=random;WeaponMissiles.HitRoll=hit;WeaponMissiles.DamageRoll=damage;SourceBlocking.BlockRoll=block;ImmolationArrows.SoftHitRoll=soft;WeaponMissiles.ItemEffects=effects;}
    }
    private static WeaponFlight Cast(PlayerMobile p,Mobile target=null,bool held=false,bool left=false)
    {
        Ready(p);int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.ImmolationArrow).Mana256;var ammo=BowAmmunition.Stack(p,CombatTiming.Equipped(p));int quantity=ammo.Amount;var ids=Own(p).Select(x=>x.Id).ToHashSet();var point=target?.Location??MagicArrowChecks.Aim(p);
        bool start=held?(left?ClassCombat.ContinueAttack(p,CombatAbility.ImmolationArrow,target,point,Core.TickCount):ClassCombat.ContinueCast(p,CombatAbility.ImmolationArrow,target,point,Core.TickCount)):ClassCombat.Perform(p,CombatAbility.ImmolationArrow,target,point,Core.TickCount);
        Require(start && NecromancerEffects.ManaBalance256(p)==mana-cost && ammo.Amount==quantity && FireGround.Ready(p,CombatAbility.ImmolationArrow),"Paid start before quantity/localdelay");var s=ClassCombat.For(p);ClassCombat.ReleaseCast(p);
        Require(!ClassCombat.ResolveAction(p,s.EffectAt-1) && ClassCombat.ResolveAction(p,s.EffectAt) && !ClassCombat.ResolveAction(p,s.EffectAt),"Once-only timed emission");var f=Own(p).Single(x=>!ids.Contains(x.Id));
        Require(f.Ability==CombatAbility.ImmolationArrow && f.Missile=="immolationarrow" && f.Frames==40 && f.NextDelay==0 && f.SkillRank==SkillMechanics.Rank(p,CombatAbility.ImmolationArrow)
            && f.Damage.Conversion==null && f.Damage.PhysicalToMagicPercent==0 && !f.Damage.FreezeColdLength && NecromancerEffects.ManaBalance256(p)==mana-cost && (ammo.Deleted?0:ammo.Amount)==quantity-1,"Captured parent/once quantity/resources");
        var delay=FireGround.Snapshot().Cooldowns.Single(x=>x.Owner==p.Serial.Value && x.Ability==CombatAbility.ImmolationArrow);
        Require(!FireGround.Ready(p,CombatAbility.ImmolationArrow) && (delay.UntilUtc-TimedEffects.Now).TotalMilliseconds is >550 and <=600 && FireGround.Ready(p,CombatAbility.FireArrow),"Release local15-frame delay/per-skill independence");return f;
    }
    private static FirePatch[] Contact(PlayerMobile p)
    {for(int i=0;i<100 && Fields(p).Length==0 && Own(p).Length>0;i++){WeaponMissiles.Tick(Own(p).Min(x=>x.NextUtc));}Require(Fields(p).Length>0,"No immediate disc on parent contact");return Fields(p);}
    private static void Differential()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"immolation-damage-source-probe.json")));var root=doc.RootElement;var names=root.GetProperty("fields").EnumerateArray().Select(x=>x.GetString()).ToArray();int count=0;
        foreach(var r in root.GetProperty("cases").EnumerateArray())
        {
            int kind=r[0].GetInt32(),rank=r[1].GetInt32(),percent=r[2].GetInt32(),boost=r[5].GetInt32();int Expected(string key)=>r[7][Array.IndexOf(names,key)].GetInt32();
            if(kind==0)
            {
                int lo=r[3].GetInt32(),fire=r[4].GetInt32();var d=BowLaunch.FromSource(lo*256,(lo+5)*256,0,0,128,79,0,0,0,r[6].GetInt32()!=0,new(FireMin:3072+3072*boost/100+fire*256,FireMax:5888+5888*boost/100+(fire+31)*256));
                Require(d.Min256==Expected("nMinDamage") && d.Max256==Expected("nMaxDamage") && d.Percent==Expected("nDamagePercent") && d.Elements.FireMin==Expected("nFireMinDamage") && d.Elements.FireMax==Expected("nFireMaxDamage"),"Parent complete creator differential");
            }
            else
            {
                string key=kind==1?"immolationfire":"meteorfire";int low=FireGround.MissileDamage256(key,rank,percent,false),high=FireGround.MissileDamage256(key,rank,percent,true);
                if(kind==2){low=AmazonMelee.EnhanceEndpoint(low,boost);high=AmazonMelee.EnhanceEndpoint(high,boost);}
                Require(low==Expected("nFireMinDamage") && high==Expected("nFireMaxDamage") && Expected("nMinDamage")==0 && Expected("nMaxDamage")==0 && r[8].GetInt32()==0,"Field pre-shift synergy/ApplyMastery/no weapon inheritance differential");
            }count++;
        }Require(count==1620,"Creator rows");using var field=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"immolation-field-source-probe.json")));count=0;
        foreach(var r in field.RootElement.GetProperty("footprint").EnumerateArray())
        {bool expected=r[3].GetInt32()!=3 && r[4].GetInt32()==0 && ImmolationArrows.Touches(r[0].GetInt32(),r[1].GetInt32(),r[2].GetInt32());Require(expected==(r[7].GetInt32()!=0),"Complete point-unit footprint differential");count++;}Require(count==1176,"Footprint rows");
        Log.Information("Immolation Arrow:1620 creator/endpoint/setter and1176 point-footprint source/native rows passed; hit/disc/router/field lifetime source matrices separate");
    }
    private static void Resources()
    {
        var p=Fresh();try
        {
            MagicArrowChecks.Bow(p,typeof(Bow));Require(!ClassCombat.Available(p,CombatAbility.ImmolationArrow),"Unlearned Immo");Train(p,CombatAbility.ImmolationArrow);Require(CombatTiming.For(p,CombatAbility.ImmolationArrow)==CombatTiming.For(p,CombatAbility.MagicArrow)
                && SkillBindings.Instance.Assign(p,true,CombatAbility.ImmolationArrow) && SkillBindings.Instance.Assign(p,false,CombatAbility.ImmolationArrow) && SkillBindings.Instance.AssignQuick(p,11,CombatAbility.ImmolationArrow),"A1/bindings");
            var v=SkillMechanics.Values(p,CombatAbility.ImmolationArrow);Require(v.Mana256==1536 && v.Damage256(false)==3379 && v.Damage256(true)==6476 && ImmolationArrows.FieldDamage256(v,false)==48 && ImmolationArrows.FieldDamage256(v,true)==100,$"Rank1 source endpoints/synergies: mana={v.Mana256} skill={v.Damage256(false)}..{v.Damage256(true)} field={ImmolationArrows.FieldDamage256(v,false)}..{ImmolationArrows.FieldDamage256(v,true)} hardFire={ClassCreation.Profiles.GetRank(p.Serial,"amazon.firearrow")} hardExplode={ClassCreation.Profiles.GetRank(p.Serial,"amazon.explodingarrow")}");
            Ready(p);int mana=NecromancerEffects.ManaBalance256(p);Require(!ClassCombat.Perform(p,CombatAbility.ImmolationArrow,null,MagicArrowChecks.Aim(p),Core.TickCount) && NecromancerEffects.ManaBalance256(p)==mana,"No ammunition paid");var bolts=new Bolt(4);p.AddToBackpack(bolts);Ready(p);Require(!ClassCombat.Perform(p,CombatAbility.ImmolationArrow,null,MagicArrowChecks.Aim(p),Core.TickCount),"Wrong ammunition");var arrows=new Arrow(30);p.AddToBackpack(arrows);
            Cast(p);Clean(p);Cast(p,held:true);Clean(p);Cast(p,held:true,left:true);Clean(p);MagicArrowChecks.Bow(p,typeof(Crossbow));Cast(p);Clean(p);Require(arrows.Amount==27 && bolts.Amount==3,"Once-only bow/crossbow quantity");MagicArrowChecks.Bow(p,typeof(Bow));
            Ready(p);NecromancerEffects.SetMana256(p,1535);Require(!ClassCombat.Perform(p,CombatAbility.ImmolationArrow,null,MagicArrowChecks.Aim(p),Core.TickCount) && NecromancerEffects.ManaBalance256(p)==1535,"Insufficient start mana");Mana(p);Ready(p);mana=NecromancerEffects.ManaBalance256(p);int quantity=arrows.Amount;
            Require(ClassCombat.Perform(p,CombatAbility.ImmolationArrow,null,MagicArrowChecks.Aim(p),Core.TickCount),"Paid cancel start");long at=ClassCombat.For(p).EffectAt;ClassCombat.Stop(p);Require(!ClassCombat.ResolveAction(p,at) && NecromancerEffects.ManaBalance256(p)==mana-1536 && arrows.Amount==quantity && FireGround.Ready(p,CombatAbility.ImmolationArrow),"Cancelled paid action quantity/refund/delay");
            Ready(p);Require(ClassCombat.Perform(p,CombatAbility.ImmolationArrow,null,MagicArrowChecks.Aim(p),Core.TickCount),"Paid movement start");at=ClassCombat.For(p).EffectAt;var origin=p.Location;p.Direction=Direction.East;Require(p.Move(Direction.East),"Outward step");p.Direction=Direction.West;Require(p.Move(Direction.West) && p.Location==origin,"Return step");Require(!ClassCombat.ResolveAction(p,at) && arrows.Amount==quantity && FireGround.Ready(p,CombatAbility.ImmolationArrow),"Moved windup resumed or set delay");
            Ready(p);Require(ClassCombat.Perform(p,CombatAbility.ImmolationArrow,null,MagicArrowChecks.Aim(p),Core.TickCount),"Paid zero-mana release");at=ClassCombat.For(p).EffectAt;NecromancerEffects.SetMana256(p,0);Require(ClassCombat.ResolveAction(p,at) && Own(p).Length==1 && arrows.Amount==quantity-1,"Second release debit");Clean(p);
            Train(p,CombatAbility.ImmolationArrow,20);v=SkillMechanics.Values(p,CombatAbility.ImmolationArrow);Require(v.Mana256==2752 && v.Damage256(false)==117145 && v.Damage256(true)==120243 && ImmolationArrows.FieldDamage256(v,false)==436 && ImmolationArrows.FieldDamage256(v,true)==504,"Rank20 source tiers and independent hard synergies");
        }finally{Clean(p);p.Delete();}
    }
    private static void AreaAndField()
    {
        var p=Fresh();Train(p,CombatAbility.ImmolationArrow);MagicArrowChecks.Bow(p,typeof(Bow));p.AddToBackpack(new Arrow(30));try
        {
            foreach(int mode in new[]{0,1,2})
            {
                Mana(p);var first=Enemy(p);var second=Enemy(p,5,1);var outside=Enemy(p,5,3);var hit=WeaponMissiles.HitRoll;var block=SourceBlocking.BlockRoll;try
                {
                    if(mode==1){WeaponMissiles.HitRoll=()=>.99999;}if(mode==2){SourceBlocking.Restore(SourceBlocking.Snapshot().Where(x=>x.Actor!=first.Serial.Value).Append(new SourceShield(first.Serial.Value,CreatureProfiles.Definition(first).Reference,1)).ToArray());SourceBlocking.BlockRoll=_=>0;}
                    int hp=SkillHealth.Balance256(first),two=SkillHealth.Balance256(second),far=SkillHealth.Balance256(outside);var shot=Cast(p,first);int direct=NamedElements.To256(shot.Damage.Roll(first,_=>0).Total);var fields=Contact(p);
                    Require(fields.Length==29 && fields.All(x=>x is{Min256:48,Max256:100,DamageRate:41,Size:1,SkillRank:1} && x.UntilUtc-x.NextUtc==TimeSpan.FromMilliseconds(3960)) && !BowExplosions.Snapshot().Any(x=>x.Owner==p.Serial.Value),"Source29-cell disc/field packet/range versus delayed child");
                    Require(SkillHealth.Balance256(first)==hp-3379-(mode==0?direct:0) && SkillHealth.Balance256(second)==two-3379 && SkillHealth.Balance256(outside)==far,"Immediate shared explosion versus separate direct hit/miss/shield/radius");
                }finally{WeaponMissiles.HitRoll=hit;SourceBlocking.BlockRoll=block;Clean(p);first.Delete();second.Delete();outside.Delete();}
            }
            Mana(p);var a=Enemy(p);var b=Enemy(p);try
            {
                Cast(p,a);var fields=Contact(p);int before=SkillHealth.Balance256(a)+SkillHealth.Balance256(b),calls=0;var damage=WeaponMissiles.DamageRoll;var soft=ImmolationArrows.SoftHitRoll;WeaponMissiles.DamageRoll=_=>0;ImmolationArrows.SoftHitRoll=_=>{calls++;return 127;};
                try{FireGround.Tick(fields[0].NextUtc.AddTicks(-1));Require(SkillHealth.Balance256(a)+SkillHealth.Balance256(b)==before,"Field before source update");FireGround.Tick(fields[0].NextUtc);Require(before-SkillHealth.Balance256(a)-SkillHealth.Balance256(b)==5*48 && calls==5,"Source five-cell cross, one collider per cell, one SOFTHIT draw");}
                finally{WeaponMissiles.DamageRoll=damage;ImmolationArrows.SoftHitRoll=soft;}
            }finally{Clean(p);a.Delete();b.Delete();}
            Mana(p);a=Enemy(p);try
            {
                Cast(p,a);var field=Contact(p).Single(x=>x.X==a.X && x.Y==a.Y);FireGround.Restore(FireGround.Snapshot() with{Patches=FireGround.Snapshot().Patches.Where(x=>x.Owner!=p.Serial.Value).Append(field).ToArray()});int hp=SkillHealth.Balance256(a);FireGround.Tick(field.UntilUtc);
                Require(SkillHealth.Balance256(a)==hp-99*48 && Fields(p).Length==0 && CreatureHitRecovery.Remaining(a)==0,"99 field ticks/expiry/no GetHit recovery");
            }finally{Clean(p);a.Delete();}
            Mana(p);a=Enemy(p);var wall=new Static(0x80);try
            {
                Cast(p,a);var field=Contact(p).Single(x=>x.X==a.X && x.Y==a.Y);FireGround.Restore(FireGround.Snapshot() with{Patches=FireGround.Snapshot().Patches.Where(x=>x.Owner!=p.Serial.Value).Append(field).ToArray()});wall.MoveToWorld(a.Location,p.Map);int hp=SkillHealth.Balance256(a);FireGround.Tick(field.NextUtc);Require(Fields(p).Length==0 && SkillHealth.Balance256(a)==hp,"Field did not remove on new hard terrain");
            }finally{wall.Delete();Clean(p);a.Delete();}
            Mana(p);a=Enemy(p);a.SetResistance(ResistanceType.Fire,100);try{int hp=SkillHealth.Balance256(a);var shot=Cast(p,a);int physical=NamedElements.To256(shot.Damage.Roll(a,_=>0).Physical);var fields=Contact(p);Require(SkillHealth.Balance256(a)==hp-physical,"Immune fire direct/area split");hp=SkillHealth.Balance256(a);FireGround.Tick(fields[0].NextUtc);Require(SkillHealth.Balance256(a)==hp,"Immune field");}finally{Clean(p);a.Delete();}
        }finally{Clean(p);p.Delete();}
    }
    private static void GearAndTerrain()
    {
        var p=Fresh();Train(p,CombatAbility.ImmolationArrow);var bow=MagicArrowChecks.Bow(p,typeof(Bow));p.AddToBackpack(new Arrow(30));try
        {
            var target=Enemy(p);var hell=NamedGear.Create("unique/65",99,new Maximum(),true);p.AddToBackpack(hell);try
            {
                var parent=Cast(p,target);p.AddToBackpack(bow);Require(p.EquipItem(hell),"Real Hellclap after launch");var fields=Contact(p);Require(parent.Damage.Elements.FireMin==3379 && fields.All(x=>x.SkillRank==1 && x.Min256==48 && x.Max256==100),"Live item fire/soft rank changed the captured-rank source0 field");Clean(p);Mana(p);
                parent=Cast(p,target);Require(parent.SkillRank==2 && parent.Damage.Elements.FireMin==10598 && parent.Damage.Elements.FireMax==22656,"Hellclap captured skill/item fire");p.AddToBackpack(hell);Require(p.EquipItem(bow),"Plain bow restore");fields=Contact(p);Require(fields.All(x=>x.SkillRank==2 && x.Min256==64 && x.Max256==116),"Removed item rewrote retained field rank");Clean(p);Mana(p);
                parent=Cast(p,target);Train(p,CombatAbility.FireArrow,2);fields=Contact(p);Require(fields.All(x=>x.Min256==52 && x.Max256==104),"Live hard Fire synergy/pre-shift field rounding");Clean(p);Mana(p);
                var ice=NamedGear.Create("unique/87",99,new Maximum(),true);var ring=NamedGear.Create("unique/275",99,new Maximum(),true);p.AddToBackpack(ice);p.AddToBackpack(ring);Require(p.EquipItem(ice) && p.EquipItem(ring),"Real Iceblink/Raven");parent=Cast(p,target);Require(parent.Damage.Elements.ColdFrames==100 && ClassGear.Bonus(p,"freeze")==1,"Captured cold/item-freeze fixture");Contact(p);Require(NamedControl.FrozenRemaining(target)>0 && TimedEffects.Chill(target,TimedEffects.Now)==0,"Cleared missile cold length suppressed attacker Iceblink event or added ordinary chill");Clean(p);p.AddToBackpack(ice);p.AddToBackpack(ring);Mana(p);
            }finally{TimedEffects.Clear(target);target.Delete();}
            var wall=new Static(0x80);target=Enemy(p,4);try{var shot=Cast(p);wall.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);int hp=SkillHealth.Balance256(target);Contact(p);Require(Own(p).Length==0 && SkillHealth.Balance256(target)==hp-shot.Damage.Elements.FireMin,"Wall AlwaysExplode immediate area/source no LOS");}finally{wall.Delete();Clean(p);target.Delete();}
            Mana(p);var last=Cast(p);Contact(p);Require(Own(p).Length==0 && Fields(p).Any(x=>Math.Abs(x.X-(Math.Floor((p.X+last.Vx*40)*2)/2))<.01),"Lifetime disc position");Clean(p);
            Train(p,CombatAbility.Pierce);Mana(p);var first=Enemy(p,5);var second=Enemy(p,8);try
            {var shot=Cast(p);Require(shot.Pierces==4,"Learned Pierce budget");for(int i=0;i<100 && Own(p).Length>0;i++){WeaponMissiles.Tick(Own(p)[0].NextUtc);}Require(Fields(p).Length==87,"Pierce contacts/lifetime did not emit independent29-cell discs");}
            finally{Clean(p);first.Delete();second.Delete();}
        }finally{Clean(p);p.Delete();}
    }
    private static void Hold(PlayerMobile p,WeaponFlight flight,FirePatch[] fields,FireCooldown cooldown)
    {
        var weapons=WeaponMissiles.Snapshot();WeaponMissiles.Restore(weapons with{Flights=weapons.Flights.Where(x=>x.Owner!=p.Serial.Value).Append(flight).ToArray()});var fire=FireGround.Snapshot();FireGround.Restore(fire with{Patches=fire.Patches.Where(x=>x.Owner!=p.Serial.Value).Concat(fields).ToArray(),Cooldowns=fire.Cooldowns.Where(x=>x.Owner!=p.Serial.Value).Append(cooldown).ToArray()});
    }
    private static FirePatch[] Held(FirePatch[] fields,DateTime at)=>fields.Select(x=>x with{NextUtc=at,UntilUtc=x.UntilUtc+(at-x.NextUtc)}).ToArray();
    private static void Persist()
    {
        var p=Fresh();Train(p,CombatAbility.ImmolationArrow);var bow=NamedGear.Create("unique/65",99,new Maximum(),true);var ring=NamedGear.Create("unique/275",99,new Maximum(),true);p.AddToBackpack(bow);p.AddToBackpack(ring);Require(p.EquipItem(bow) && p.EquipItem(ring),"Saved real Hellclap/Raven");var ammo=new Arrow(20);p.AddToBackpack(ammo);var target=Enemy(p);Cast(p,target);var fields=Contact(p);FireGround.Tick(fields[0].NextUtc.AddMilliseconds(80));fields=Fields(p);Require(fields.Length==29 && fields.All(x=>x.SkillRank==2 && x.Min256==64 && x.Max256==116),"Saved actual partially active field");
        var until=TimedEffects.Now.Add(FixtureSpace.Retention);var savedFields=Held(fields,until);var fire=FireGround.Snapshot();FireGround.Restore(fire with{Patches=fire.Patches.Where(x=>x.Owner!=p.Serial.Value).Concat(savedFields).ToArray()});Mana(p);var flight=Cast(p,target) with{NextUtc=until};Ready(p);Require(ClassCombat.Perform(p,CombatAbility.ImmolationArrow,null,MagicArrowChecks.Aim(p),Core.TickCount),"Saved paid unemitted windup");
        var cooldown=new FireCooldown(p.Serial.Value,CombatAbility.ImmolationArrow,until);Hold(p,flight,savedFields,cooldown);Require(SkillBindings.Instance.Assign(p,true,CombatAbility.ImmolationArrow) && SkillBindings.Instance.Assign(p,false,CombatAbility.ImmolationArrow) && SkillBindings.Instance.AssignQuick(p,11,CombatAbility.ImmolationArrow),"Saved bindings");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,target.Serial.Value,bow.Serial.Value,ring.Serial.Value,ammo.Serial.Value,p.Location,flight,savedFields,cooldown,SkillHealth.Balance256(target),NecromancerEffects.ManaBalance256(p),ammo.Amount)));
    }
    private static void Reload()=>Controlled(()=>
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var target=(BowLaunchTarget)World.FindMobile((Serial)saved.Target);var bow=World.FindItem((Serial)saved.Bow);var ring=World.FindItem((Serial)saved.Ring);var ammo=World.FindItem((Serial)saved.Ammo);
        Require(Own(p).Single()==saved.Flight && Fields(p).SequenceEqual(saved.Fields) && FireGround.Snapshot().Cooldowns.Contains(saved.Cooldown) && NecromancerEffects.ManaBalance256(p)==saved.Mana && ammo.Amount==saved.Quantity && SkillHealth.Balance256(target)==saved.Health && ClassCombat.For(p).PendingAbility==CombatAbility.None
            && SkillBindings.Instance.Get(p)==(CombatAbility.ImmolationArrow,CombatAbility.ImmolationArrow) && SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Amazon,11)==CombatAbility.ImmolationArrow,"Saved parent/field/partial timers/cooldown/resources/bindings or replayed windup");
        MagicArrowChecks.Ready(p);Require(!ClassCombat.Perform(p,CombatAbility.ImmolationArrow,null,MagicArrowChecks.Aim(p),Core.TickCount) && NecromancerEffects.ManaBalance256(p)==saved.Mana,"Saved cooldown allowed paid cast");p.MoveToWorld(saved.Origin,Map.Maps[saved.Flight.Map]);p.AddToBackpack(bow);p.AddToBackpack(ring);
        int expected=NamedElements.To256(saved.Flight.Damage.Roll(target,_=>0).Total)+saved.Flight.Damage.Elements.FireMin;Hold(p,saved.Flight with{NextUtc=TimedEffects.Now},saved.Fields,saved.Cooldown);
        for(int i=0;i<100 && Own(p).Length>0 && Fields(p).Length==saved.Fields.Length;i++){WeaponMissiles.Tick(Own(p).Min(x=>x.NextUtc));}
        var newly=Fields(p).Where(x=>saved.Fields.All(s=>s.Id!=x.Id)).ToArray();Require(newly.Length==29 && newly.All(x=>x is{SkillRank:2,Min256:64,Max256:116}) && SkillHealth.Balance256(target)==saved.Health-expected,"Resumed immutable parent/fire area versus live unmastered field");
        var resume=Held(saved.Fields,TimedEffects.Now);var state=FireGround.Snapshot();FireGround.Restore(state with{Patches=state.Patches.Where(x=>x.Owner!=p.Serial.Value).Concat(resume).ToArray()});int before=SkillHealth.Balance256(target);FireGround.Tick(resume[0].NextUtc);Require(SkillHealth.Balance256(target)==before-5*64 && NecromancerEffects.ManaBalance256(p)==saved.Mana && ammo.Amount==saved.Quantity,"Resumed five-cell field packet/cadence/repayment");
        var until=TimedEffects.Now.Add(FixtureSpace.Retention);var held=saved.Flight with{NextUtc=until};var fields=Held(saved.Fields,until);var cooldown=saved.Cooldown with{UntilUtc=until};Hold(p,held,fields,cooldown);Require(p.EquipItem(bow) && p.EquipItem(ring),"Reload real gear");Ready(p);Require(ClassCombat.Perform(p,CombatAbility.ImmolationArrow,null,MagicArrowChecks.Aim(p),Core.TickCount),"Next paid unemitted windup");Hold(p,held,fields,cooldown);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Flight=held,Fields=fields,Cooldown=cooldown,Health=SkillHealth.Balance256(target),Mana=NecromancerEffects.ManaBalance256(p)}));
    });
    private static void Natural()
    {
        var p=Fresh();Train(p,CombatAbility.ImmolationArrow);MagicArrowChecks.Bow(p,typeof(Bow));p.AddToBackpack(new Arrow(3));var target=Enemy(p);Cast(p,target);Contact(p);
        Timer.StartTimer(TimeSpan.FromMilliseconds(5100),()=>{try{Require(Fields(p).Length==0 && FireGround.Ready(p,CombatAbility.ImmolationArrow),"Natural field/cooldown expiry");Log.Information("immolation-arrow-state-result: passed");}catch(Exception e){Log.Error(e,"immolation-arrow-state-result: failed");}finally{Clean(p);target.Delete();p.Delete();}});
    }
    private static void Check()=>Controlled(()=>{Differential();Resources();AreaAndField();GearAndTerrain();Persist();Natural();});
    private static void Input()
    {
        var p=Fresh();Train(p,CombatAbility.ImmolationArrow);MagicArrowChecks.Bow(p,typeof(Bow));Ready(p);var ammo=new Arrow(3);p.AddToBackpack(ammo);var target=Enemy(p);Require(SkillBindings.Instance.Assign(p,true,CombatAbility.ImmolationArrow) && SkillBindings.Instance.Assign(p,false,CombatAbility.ImmolationArrow) && SkillBindings.Instance.AssignQuick(p,11,CombatAbility.ImmolationArrow),"Connected bindings");
        var network=NetState.Instances.Single(x=>x.Mobile==null && IPAddress.IsLoopback(x.Address));network.Seeded=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;var protocol=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);Require(protocol!=null,"Protocol bootstrap changed");protocol.SetValue(network,Enum.Parse(protocol.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        int mana=NecromancerEffects.ManaBalance256(p),hp=SkillHealth.Balance256(target),immediate=0;bool started=false,emitted=false;long deadline=Core.TickCount+5000;var hit=WeaponMissiles.HitRoll;var damage=WeaponMissiles.DamageRoll;var block=SourceBlocking.BlockRoll;WeaponMissiles.HitRoll=()=>0;WeaponMissiles.DamageRoll=_=>0;SourceBlocking.BlockRoll=_=>99;
        void Finish(){WeaponMissiles.HitRoll=hit;WeaponMissiles.DamageRoll=damage;SourceBlocking.BlockRoll=block;Clean(p);target.Delete();p.Delete();}
        void Wait()
        {
            try
            {
                Require(Core.TickCount<deadline,"Connected Immolation did not complete");if(!started && ClassCombat.For(p).PendingAbility==CombatAbility.ImmolationArrow){started=true;Require(NecromancerEffects.ManaBalance256(p)==mana-1536 && ammo.Amount==3 && Own(p).Length==0 && FireGround.Ready(p,CombatAbility.ImmolationArrow),"Natural paid start before emission/delay");}
                var flights=Own(p);if(!emitted && flights.Length>0){emitted=true;Require(started && flights.Length==1 && flights[0].Frame<3 && flights[0].Missile=="immolationarrow" && Fields(p).Length==0 && ammo.Amount==2,"Natural one-arrow emission");immediate=NamedElements.To256(flights[0].Damage.Roll(target,_=>0).Total)+flights[0].Damage.Elements.FireMin;}
                if(!emitted || flights.Length>0 || SkillHealth.Balance256(target)>hp-immediate-5*48){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Wait);return;}
                Require(Fields(p).Length==29 && NecromancerEffects.ManaBalance256(p)==mana-1536 && ammo.Amount==2,"Natural immediate explosion/field/cadence/once resources");Finish();Log.Information("immolation-arrow-input-result: passed");
            }catch(Exception e){Finish();Log.Error(e,"immolation-arrow-input-result: failed");}
        }
        var point=MagicArrowChecks.Aim(p);Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("immolation-arrow-input: ready ability={Ability} x={X} y={Y} z={Z}",(int)CombatAbility.ImmolationArrow,point.X,point.Y,point.Z);Wait();});
    }
}
