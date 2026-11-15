// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class MirroredPacketChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(MirroredPacketChecks));
    private sealed record Row(uint Owner,uint Weapon,uint Helm,uint[] PoisonItems,uint Arrow,uint Bolt,int Quantity,int Durability,
        int Life,int Mana,MirroredWeaponInput Captured,MirroredWeaponInput Current,MirroredWeaponPacket[] Packets);
    private sealed record Saved(int Generation,Row[] Rows,int Controls);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"mirrored-packets-fixtures.json");
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {
            bool reload=again;string name=reload?"mirrored-packets-reload-check":"mirrored-packets-check";
            ConsoleInputHandler.RegisterCommand(name,"Prepared Mirrored real melee/bow/crossbow/throw packets and original saves",_=>Core.LoopContext.Post(()=>
            {try{if(reload){Reload();}else{Scene();}Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
        }
    }
    [CallPriority(int.MaxValue)]public static void Initialize()
    {
        var hp=ClassGearHooks.CanRegenerateHits;var mp=ClassGearHooks.CanRegenerateMana;
        ClassGearHooks.CanRegenerateHits=p=>p.Name!="MirroredPacketFixture"&&hp?.Invoke(p)!=false;
        ClassGearHooks.CanRegenerateMana=p=>p.Name!="MirroredPacketFixture"&&mp?.Invoke(p)!=false;
    }
    private static BaseWeapon Weapon(PlayerMobile p,int kind)
    {
        p.AddToBackpack(CombatTiming.Equipped(p));BaseWeapon w;
        if(kind is 0 or 3)
        {string key=kind==0?"Stone Crusher":"Gimmershred";w=(BaseWeapon)NamedGear.Create(NamedGear.Catalog.Items.Single(i=>i.SourceKey==key).Id,99,new Random(173),true);}
        else{w=kind==1?new Bow():new Crossbow();ClassGear.Profiles.Convert(w,99,GearRarity.Normal,new Random(173));}
        p.AddToBackpack(w);Require(p.EquipItem(w),"Actual original four-kind weapon equip");return w;
    }
    private static int Controls(MirroredWeaponInput original)
    {
        var elements=new MissileSourceStats(FireMin:773,FireMax:773,ColdMin:1031,ColdMax:1031,LightningMin:1291,LightningMax:1291,
            MagicMin:1543,MagicMax:1543,ColdFrames:101,PoisonMin:193,PoisonMax:193,PoisonFrames:175,PoisonCount:2,LifeDrain:5,ManaDrain:7);
        var basis=original.Damage with{Min256=26003,Max256=26003,Percent=37,DemonPercent=0,UndeadPercent=0,
            Elements=elements,PhysicalSupplement=new(317,317)};
        int controls=0;
        foreach(int rank in new[]{1,5,10,15,20,40})foreach(bool critical in new[]{false,true})
        {
            var skill=MirroredBladesPrepared.Capture(new(SkillMechanics.For(CombatAbility.MirroredBlades),k=>k=="Mirrored Blades"?rank:0,_=>0,99));
            var input=original with{Skill=skill,Damage=basis with{SkillAttackPercent=skill.AttackPercent}};
            int count=Math.Min(2+rank/5,5);
            foreach(int ordinal in Enumerable.Range(0,count))
            {
                var packet=MirroredBladesPackets.Prepare(input,ordinal,critical);ulong seed=SourceRandom.InitLow(71);
                var target=new GoatmanNpcContactChecks.Recipient();
                try
                {
                    var actual=MirroredBladesPackets.Roll(packet,target,ref seed);
                    int percent=ordinal==0?100:skill.DuplicatePercent;
                    int Scale(int n)=>(int)((long)n*percent/100);
                    int physical=(26003+26003*37/100+317)*(critical?2:1);
                    Require(NamedElements.To256(actual.Damage.Physical)==Scale(physical)
                        &&NamedElements.To256(actual.Damage.Fire)==Scale(773)&&NamedElements.To256(actual.Damage.Cold)==Scale(1031)
                        &&NamedElements.To256(actual.Damage.Lightning)==Scale(1291)&&NamedElements.To256(actual.Damage.Magic)==Scale(1543)
                        &&actual.Damage.Poison==0&&actual.PoisonRate256==Scale(193)&&actual.PoisonFrames==87&&actual.ColdFrames==101,"Independent final-channel/critical/poison integer order");
                    Require(packet.Damage.Elements.LifeDrain==5&&packet.Damage.Elements.ManaDrain==7&&packet.Damage.Elements.PoisonCount==2
                        &&packet.Damage.Elements.PoisonFrames==175&&packet.Attack.Ordinal==ordinal,"Duration/count/drain or ordinal scaled");
                    Require(ordinal==0?packet.Attack.RawMeleeDivisor==null:packet.Attack.RawMeleeDivisor==skill.DuplicateMeleeDivisor
                        &&packet.Attack.RawRangedDivisor==skill.DuplicateRangedDivisor&&packet.Attack.RawPlayerDivisor==skill.DuplicatePlayerDivisor,"Crushing suppliers lost");
                    controls++;
                }
                finally{target.Delete();}
            }
        }
        ulong a=SourceRandom.InitLow(811),b=a;
        var zero=original with{Critical=new(0,0,0)};
        Require(!MirroredBladesPackets.Critical(zero,ref a),"Zero critical succeeded");_=SourceRandom.Roll(ref b);Require(a==b,"Unsigned unconditional first draw");
        a=SourceRandom.InitLow(811);Require(MirroredBladesPackets.Critical(zero with{Critical=new(100,100,100)},ref a)&&a==b,"Critical double-rolled");
        return controls;
    }
    private static void Scene()
    {
        var rows=new Row[4];int controls=0;
        for(int i=0;i<rows.Length;i++)
        {
            var p=CleaveActionChecks.Fresh(i%2!=0);p.Name="MirroredPacketFixture";
            while(p.RawStr<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Actual weapon strength");}
            while(p.RawDex<150){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Actual weapon dexterity");}
            ClassSkillChecks.Train(p,"warlock.mirroredblades",7);ClassSkillChecks.Train(p,"warlock.hexbane");var weapon=Weapon(p,i);
            var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(31),true);p.AddToBackpack(helm);Require(p.EquipItem(helm),"Actual +skill helm");
            var poisons=new[]{"Snakecord","Venom Grip"}.Select(k=>NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==k).Id,99,new Random(47),true)).ToArray();
            foreach(var item in poisons){p.AddToBackpack(item);Require(p.EquipItem(item),"Actual poison item");}
            var arrow=new Arrow(3);var bolt=new Bolt(4);p.AddToBackpack(arrow);p.AddToBackpack(bolt);ClassGear.Refresh(p);
            NecromancerEffects.SetMana256(p,p.ManaMax*256);Require(HexBane.CastPrepared(p,true),"Actual Bane weapon source");
            SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-513);NecromancerEffects.SetMana256(p,p.ManaMax*256-17);
            int hp=SkillHealth.Balance256(p),mp=NecromancerEffects.ManaBalance256(p),durability=weapon.HitPoints,quantity=NamedAmmunition.Remaining(weapon);
            var captured=MirroredBladesPackets.Capture(p);string frozen=JsonSerializer.Serialize(captured);
            Require(captured.Kind==(MirroredWeaponKind)i&&captured.Skill is{Rank:8,TotalAttacks:3}
                &&captured.Damage.Elements is{PoisonMin:193,PoisonMax:193,PoisonFrames:175,PoisonCount:2,LifeDrain:5}
                &&captured.Damage.Elements.MagicMin>0,"Actual kind/rank/Hex/raw poison capture");
            Require(captured.Damage.Min256==ClassStats.WeaponBaseDamage(p).Min*256&&captured.Damage.Max256==ClassStats.WeaponBaseDamage(p).Max*256,"Full source128 endpoints");
            if(i==0){Require(ClassGear.Bonus(weapon,"normal-damage")==18&&captured.Damage.Min256==(50*(100+ClassGear.Bonus(weapon,"dmg%"))/100+18)*256,"Actual Stone Crusher Damage+ retained");}
            controls+=Controls(captured);
            var packets=Enumerable.Range(0,3).Select(n=>MirroredBladesPackets.Prepare(captured,n,n!=0)).ToArray();
            Require(SkillHealth.Balance256(p)==hp&&NecromancerEffects.ManaBalance256(p)==mp&&weapon.HitPoints==durability
                &&NamedAmmunition.Remaining(weapon)==quantity&&arrow.Amount==3&&bolt.Amount==4,"Prepared collection/roll changed resources/ammunition/durability");
            p.AddToBackpack(helm);ClassSkillChecks.Train(p,"warlock.mirroredblades",20);HexBane.ClearBuff(p);ClassGear.Refresh(p);
            var current=MirroredBladesPackets.Capture(p);
            Require(current.Skill is{Rank:20,TotalAttacks:5}&&current.Damage.Elements.MagicMin<captured.Damage.Elements.MagicMin
                &&JsonSerializer.Serialize(captured)==frozen,"Capture mutated with gear/rank/Hex changes");
            bool bad=false;try{MirroredBladesPackets.Prepare(captured,3,false);}catch(ArgumentOutOfRangeException){bad=true;}Require(bad,"Out-of-count mirror accepted");
            p.AddToBackpack(weapon);bad=false;try{MirroredBladesPackets.Capture(p);}catch(NotSupportedException){bad=true;}Require(bad&&p.EquipItem(weapon),"Unarmed admission or restore");
            SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-513);NecromancerEffects.SetMana256(p,p.ManaMax*256-17);
            rows[i]=new(p.Serial.Value,weapon.Serial.Value,helm.Serial.Value,poisons.Select(x=>x.Serial.Value).ToArray(),arrow.Serial.Value,bolt.Serial.Value,
                quantity,durability,SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),captured,current,packets);
        }
        Require(!MirroredBladesPackets.Approved&&!WarlockCombat.Implemented(CombatAbility.MirroredBlades),"Packet candidate enabled learned action");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,rows,controls)));
        Log.Information("Mirrored packets:{Controls}independent channel/count/critical controls;actual Stone Crusher/bow/crossbow/Gimmershred retain Damage+,Hex,raw poison,ammo/quantity/durability and immutable captures;whole action remains gated",controls);
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        foreach(var row in saved.Rows)
        {
            var p=(PlayerMobile)World.FindMobile((Serial)row.Owner);var w=(BaseWeapon)World.FindItem((Serial)row.Weapon);ClassGear.Refresh(p);
            Require(w.Parent==p&&w.HitPoints==row.Durability&&NamedAmmunition.Remaining(w)==row.Quantity&&World.FindItem((Serial)row.Helm)?.Parent==p.Backpack
                &&row.PoisonItems.All(n=>World.FindItem((Serial)n)?.Parent==p)&&World.FindItem((Serial)row.Arrow)?.Amount==3&&World.FindItem((Serial)row.Bolt)?.Amount==4,"Original saved gear/ammunition/quantity/durability");
            Require(MirroredBladesPackets.Capture(p)==row.Current&&SkillHealth.Balance256(p)==row.Life&&NecromancerEffects.ManaBalance256(p)==row.Mana,"Saved current capture or fractional resources");
            foreach(var packet in row.Packets)
            {
                Require(packet.Input==row.Captured&&packet==MirroredBladesPackets.Prepare(row.Captured,packet.Attack.Ordinal,packet.Damage.Critical),"Original prepared packet changed");
                ulong a=SourceRandom.InitLow(17),b=a;var left=MirroredBladesPackets.Roll(packet,null,ref a);var right=MirroredBladesPackets.Roll(packet,null,ref b);
                Require(left==right&&a==b,"Saved deterministic packet/seed");
            }
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
        Log.Information("Mirrored packets:four original weapon kinds/items/ammo/quantity/FP256resources/current stats and earlier immutable primary/duplicate packets retain through reload;no action replay");
    }
}
