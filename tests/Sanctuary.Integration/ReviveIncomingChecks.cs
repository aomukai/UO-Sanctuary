// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Collections;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ReviveIncomingChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ReviveIncomingChecks));
    private sealed record Saved(uint Owner,uint Incoming,uint Legacy,int Life,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"revive-incoming-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("revive-incoming-check",Check);Register("revive-incoming-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Original Kind1 incoming fractional/AOS/contact/return writer",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public class Original:Skeleton
    {
        internal int ScalarReads;
        [Constructible] public Original(){}
        public Original(Serial serial):base(serial){}
        // Deliberately large: a replay of BaseCreature.Damage's random
        // controlled-pet modifier would corrupt exact HP and read this getter.
        public override double BonusPetDamageScalar{get{ScalarReads++;return 8;}}
        public override void Serialize(IGenericWriter writer){base.Serialize(writer);writer.Write(0);writer.Write(ScalarReads);}
        public override void Deserialize(IGenericReader reader){base.Deserialize(reader);reader.ReadInt();ScalarReads=reader.ReadInt();}
    }
    private static Item Named(string name)=>NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey==name).Id,99,new Random(173),true);
    private static Original Create(PlayerMobile owner,bool incoming)
    {
        var b=new Original{Name="PoisonDaggerFixtureReviveIncoming",NoKillAwards=true};foreach(var item in b.Items.Where(i=>i is not Backpack&&i.Layer!=Layer.Bank).ToArray()){item.Delete();}
        var gear=new[]{"The Gnasher","Swordback Hold","Goblin Toe"}.Select(Named).ToArray();foreach(var item in gear){b.AddItem(item);}
        foreach(var r in Enum.GetValues<ResistanceType>()){b.SetResistance(r,r==ResistanceType.Fire?25:r==ResistanceType.Cold?50:r==ResistanceType.Energy?80:r==ResistanceType.Poison?75:0);}
        b.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);b.AIObject?.AITimer.Stop();Corpse corpse=null;var died=ClassLootHooks.CreatureDied;
        ClassLootHooks.CreatureDied=(who,item)=>{died?.Invoke(who,item);if(who==b){corpse=item as Corpse;corpse?.DropItem(new Gold(1));}};
        try{owner.DoHarmful(b,true);b.Damage(b.Hits+1,owner);CombatDamage.Finish(b,owner);}finally{ClassLootHooks.CreatureDied=died;}
        Require(corpse is{Deleted:false}&&ReviveOriginals.Find(corpse)==b,"Incoming original was not retained");corpse.BeginDecay(FixtureSpace.Retention);
        var original=ReviveOriginals.State(corpse);var before=ReviveBeforeDeath.Decode(original.BeforeDeathJson);
        var setup=ReviveLife.Setup(owner,corpse,before.Mana,before.Stamina)with{RegenerationRate=ReviveRegeneration.Rate("skeleton1")};setup=incoming?ReviveEquipmentIncoming.Prepare(setup,original):ReviveEquipmentEvents.Prepare(setup,original);
        Require(ReviveNativeActivation.CommitPrepared(owner,corpse,corpse.Location,before.AiStatus.Value,setup,out var actual,out var issue)&&actual==b,"Incoming activation failed: "+issue);
        foreach(var item in gear){Require(item.Parent==corpse,"Incoming gear was not independent loot");owner.AddToBackpack(item);item.Delete();}return b;
    }
    private static void Place(PlayerMobile owner,BaseCreature actor)
    {actor.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);actor.Blessed=false;actor.AIObject?.AITimer.Stop();}
    private static int Expected(int before,int loss){int after=(int)Math.Max(0,(long)before-loss);return after<256?0:after;}
    private static int Credit(BaseCreature actor,Mobile source)
    {int total=0;foreach(var entry in actor.DamageEntries){if(entry.Damager==source){total+=entry.DamageGiven;}}return total;}
    private static void Direct(Original actor,BaseCreature enemy)
    {
        int maximum=SkillHealth.Maximum256(actor);Require(maximum>2048,"Incoming source life control too small");
        foreach(var source in new Mobile[]{enemy,null})foreach(int loss in new[]{1,127,255,256,257,511,1025})
        {
            SkillHealth.SetBalance256(actor,maximum-17);int before=SkillHealth.Balance256(actor),displayed=actor.Hits,after=Expected(before,loss);
            int prior=Credit(actor,enemy);
            var stream=NpcEquipmentRandom.State(actor);int result=SkillHealth.Damage(source,actor,loss);
            int whole=displayed-(after+255)/256,credit=Credit(actor,enemy);
            Require(SkillHealth.Balance256(actor)==after&&result==whole&&actor.ScalarReads==0&&NpcEquipmentRandom.State(actor)==stream,"Incoming writer lost fractional HP,applied native pet multiplier or advanced attack RNG");
            Require(credit-prior==(source==null?0:whole),"Incoming whole-display credit was omitted/duplicated or invented for an anonymous source");
        }
    }
    private static void Aos(PlayerMobile owner,Original actor,BaseCreature enemy)
    {
        for(int channel=0;channel<5;channel++)foreach(bool anonymous in new[]{false,true})
        {
            SkillHealth.SetBalance256(actor,SkillHealth.Maximum256(actor)-17);int before=SkillHealth.Balance256(actor);int[] split=new int[5];split[channel]=100;
            int resistance=channel switch{0=>0,1=>25,2=>50,3=>75,_=>80};int expected=NamedElements.Reduce256(2*256,resistance,channel==3?0:256,0,0).Damage256;
            AOS.Damage(actor,anonymous?null:enemy,2,false,split[0],split[1],split[2],split[3],split[4],spellDamage:true);
            Require(SkillHealth.Balance256(actor)==Expected(before,expected)&&actor.ScalarReads==0,"Native AOS fractional route bypassed Kind1 writer or remitigated damage");
        }
        PreparedMonsterNativeDamage.RequireIncoming(owner,actor);PlayerWeaponContact.Require(owner,actor,weaponSetup:false);
    }
    private static void Contact(PlayerMobile owner,Original actor)
    {
        foreach(int flags in new[]{0,1})
        {
            var enemy=new GraveyardZombie{NoKillAwards=true,CantWalk=true};enemy.SetHits(20000);enemy.SetResistance(ResistanceType.Physical,0);enemy.MoveToWorld(actor.Location,owner.Map);enemy.AIObject?.AITimer.Stop();
            try
            {
                // Reuse the same explicit defensive lease fixture as the
                // established ordered-contact retaliation checks.
                PaladinAuras.ElementalWeapon(new(enemy.Serial.Value,owner.Serial.Value,CombatAbility.Thorns,1,10,TimedEffects.Now.AddHours(1)));
                SkillHealth.SetBalance256(actor,SkillHealth.Maximum256(actor)-17);int before=SkillHealth.Balance256(actor);
                NpcEquipmentRandom.Set(actor,SourceRandom.InitLow(173));var prepared=IronGolemContact.Prepare(actor,enemy,IronGolemContact.Require(actor,owner,enemy),()=>flags);
                var result=IronGolemContact.Release(actor,owner,enemy,prepared,_=>true,TimedEffects.Now);
                Require(prepared.Consumed&&result.Exit==MonsterContactExit.Finished&&SkillHealth.Balance256(actor)==before-9*256&&actor.ScalarReads==0,"Qualified hit/miss flat return replayed pet damage or returned twice");
                Require((flags==0)==(result.Commit==null),"Qualified hit/miss contact lost its health boundary");
            }
            finally{PaladinAuras.Clear(enemy);enemy.Corpse?.Delete();enemy.Delete();}
        }
    }
    private static void Exercise(PlayerMobile owner,Original actor,Original legacy)
    {
        owner.MoveToWorld(new(6550,3550,0),Map.Trammel);Place(owner,actor);
        Require(actor.Controlled&&!actor.Summoned&&ReviveEquipmentIncoming.Handles(actor)&&!ReviveEquipmentIncoming.Handles(legacy),"Incoming opt-in did not distinguish existing originals");
        var enemy=new GraveyardZombie{NoKillAwards=true,CantWalk=true};enemy.MoveToWorld(actor.Location,owner.Map);enemy.AIObject?.AITimer.Stop();
        try{Direct(actor,enemy);Aos(owner,actor,enemy);Contact(owner,actor);}
        finally{enemy.Delete();}
        bool refused=false;try{PreparedMonsterNativeDamage.RequireIncoming(owner,legacy);}catch(NotSupportedException){refused=true;}Require(refused,"Legacy original silently acquired incoming admission");
        var custom=new SkeletonGuardian();refused=false;try{ReviveEquipmentIncoming.RequireBody(custom);}catch(NotSupportedException){refused=true;}finally{custom.Delete();}Require(refused,"Custom damage override silently acquired incoming adapter");
        var rows=ReviveNativeActivation.Snapshot();var state=rows.Single(s=>s.Actor==actor.Serial.Value);refused=false;
        try{ReviveNativeActivation.Restore(rows.Select(r=>r==state?r with{Setup=r.Setup with{EquipmentIncomingProfile="unknown"}}:r).ToArray());}catch(InvalidDataException){refused=true;}
        Require(refused&&ReviveNativeActivation.Snapshot().SequenceEqual(rows),"Unknown incoming profile replaced the roster");
        SkillHealth.SetBalance256(actor,SkillHealth.Maximum256(actor)-73);owner.Internalize();
    }
    private static void Lethal(PlayerMobile owner)
    {
        foreach(bool anonymous in new[]{false,true})
        {
            var actor=Create(owner,true);Place(owner,actor);SkillHealth.SetBalance256(actor,257);
            var source=new GraveyardZombie{NoKillAwards=true};source.MoveToWorld(actor.Location,owner.Map);source.AIObject?.AITimer.Stop();
            try{int whole=SkillHealth.Damage(anonymous?null:source,actor,2);Require(!actor.Alive&&actor.Hits==0&&whole==2&&actor.ScalarReads==0,"Sub-one-HP incoming death threshold or native multiplier changed");}
            finally{actor.Corpse?.Delete();actor.Delete();source.Delete();ReviveNativeActivation.Tick();}
        }
    }
    private static void Check()
    {
        var owner=PoisonDaggerChecks.Owner();owner.Name="PoisonDaggerFixtureReviveIncomingOwner";MagePetBuffChecks.Train(owner,"necromancer.revive",20);MagePetBuffChecks.Train(owner,"necromancer.skeletonmastery",20);FixtureSpace.Corridor(owner,6550,24,3550);
        var actor=Create(owner,true);var legacy=Create(owner,false);Lethal(owner);Exercise(owner,actor,legacy);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,actor.Serial.Value,legacy.Serial.Value,SkillHealth.Balance256(actor))));
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=World.FindMobile((Serial)saved.Owner)as PlayerMobile;var actor=World.FindMobile((Serial)saved.Incoming)as Original;var legacy=World.FindMobile((Serial)saved.Legacy)as Original;
        Require(actor is{Alive:true,Deleted:false}&&legacy is{Alive:true,Deleted:false}&&actor.Map==Map.Internal&&SkillHealth.Balance256(actor)==saved.Life&&actor.ScalarReads==0,"Saved incoming original,partial life or zero modifier-read count changed");
        Exercise(owner,actor,legacy);File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Life=SkillHealth.Balance256(actor),Generation=saved.Generation+1}));
    }
}
