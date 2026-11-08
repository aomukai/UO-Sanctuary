// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class NpcSpellFreezeChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(NpcSpellFreezeChecks));
    private sealed record Held(uint Actor,string Body,string Resources,string[] Items,NpcEquipmentSeed Random,int Life,uint Target,int TargetLife,string Freeze);
    private sealed record Saved(uint Owner,Held[] Bodies,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-spell-freeze-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static string Control(BaseCreature t)=>Json(NamedControl.State(t));
    private static Held Capture(AssassinShadowBody b,BaseCreature t)=>new(b.Serial.Value,b.SavedText,Json(GeneratedNpcLiveResources.State(b)),ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray(),NpcEquipmentRandom.Require(b),SkillHealth.Balance256(b),t.Serial.Value,SkillHealth.Balance256(t),Control(t));
    public static void Configure(){Register("npc-spell-freeze-check",false);Register("npc-spell-freeze-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Owned NPC spell cold/freeze lanes and original persistent state",_=>Core.LoopContext.Post(()=>{try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static BaseCreature Target(AssassinShadowBody b,int resistance=0)
    {
        var t=new ShadowPsychicChecks.Witness{CasterId=b.Serial.Value};t.SetHits(65000);foreach(var type in Enum.GetValues<ResistanceType>()){t.SetResistance(type,type==ResistanceType.Cold?resistance:0);}
        t.MoveToWorld(new(b.X+1,b.Y,b.Z),b.Map);t.AIObject.AITimer.Stop();TimedEffects.PreventHealing(t,TimedEffects.Now);return t;
    }
    private static BaseCreature Exercise(AssassinShadowBody b,PlayerMobile owner,int generation)
    {
        b.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);var at=TimedEffects.Now.AddMinutes(20+generation);
        var items=ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray();var rng=NpcEquipmentRandom.Require(b);int mana=NecromancerEffects.ManaBalance256(owner);
        // Ordinary cold delivery must keep its existing chill-only behavior.
        var cold=Target(b);
        try
        {
            int hp=SkillHealth.Balance256(cold);var result=NpcElementSpellHit.Deliver(b,owner,cold,"cold",2560,100,at,"Owned NPC cold control");
            Require(result.AfterHealth256<hp&&TimedEffects.TryActor(cold,out var state)&&state.Cold?.Source==b.Serial.Value&&state.Control?.Freeze==null,"Chill-only NPC route changed");
        }
        finally{cold.Delete();}
        foreach(int resistance in new[]{0,50,100})
        {
            var t=Target(b,resistance);
            try
            {
                int hp=SkillHealth.Balance256(t);var result=NpcElementSpellHit.Deliver(b,owner,t,"cold",0,0,at,"Owned NPC zero-damage freeze",freezeFrames:101);
                int frames=ColdStates.LengthFrames(101,NamedElements.Resistance(b,t,"cold",t.ColdResistance,0),false,false);
                Require(result.AfterHealth256==hp&&result.Packet.ColdFrames==0&&result.Packet.FreezeFrames==frames,"Freeze duration/zero-damage lane was not independent");
                var control=NamedControl.State(t);
                Require(frames>0?control?.Freeze?.Source==b.Serial.Value&&control.FreezeUntil==at.AddMilliseconds(Math.Max(1,frames/NecromancerEffects.DifficultyDivisor(t))*40L):control?.Freeze==null,"NPC freeze owner/resistance/difficulty or immunity is wrong");
            }
            finally{t.Delete();}
        }
        var pierced=Target(b,50);
        try
        {
            var result=NpcElementSpellHit.Deliver(b,owner,pierced,"cold",0,0,at,"Owned NPC captured cold pierce",pierce:e=>e=="cold"?30:0,freezeFrames:101);
            int frames=ColdStates.LengthFrames(101,NamedElements.Resistance(b,pierced,"cold",pierced.ColdResistance,30),false,false);
            Require(result.Packet.FreezeFrames==frames&&NamedControl.State(pierced)?.FreezeUntil==at.AddMilliseconds(Math.Max(1,frames/NecromancerEffects.DifficultyDivisor(pierced))*40L),"NPC freeze did not use its explicit pierce exactly once");
        }
        finally{pierced.Delete();}
        var invalid=Target(b);
        try
        {
            int hp=SkillHealth.Balance256(invalid);bool rejected=false;
            try{_=NpcElementSpellHit.Deliver(b,owner,invalid,"fire",2560,0,at,"Invalid non-cold freeze",freezeFrames:10);}catch(NotSupportedException){rejected=true;}
            Require(rejected&&SkillHealth.Balance256(invalid)==hp&&NpcEquipmentRandom.Require(b)==rng&&NamedControl.State(invalid)?.Freeze==null,"Non-cold freeze was admitted or mutated recipient/source RNG");
        }
        finally{invalid.Delete();}
        // Use the existing Phoenix pure packet capture against the original
        // NPC book/graph. This is its receiver prerequisite, not a missile cast.
        b.ChangeSkills(book=>book.Assign(280,3));var values=ShadowSkillCasting.OwnValues(b,b.OwnedSkillRank(280),280);
        var packet=MartialPhoenixPackets.Capture("royalstrikechaosice",values,OriginalNpcEquipment.Graph(b).Read(331));var target=Target(b);
        int before=SkillHealth.Balance256(target);int raw=WeaponMissileDamage.RollValue(packet.Minimum256,packet.Maximum256,0,n=>NpcEquipmentRandom.Limited(b,n));var rolled=NpcEquipmentRandom.Require(b);
        var impact=NpcElementSpellHit.Deliver(b,owner,target,"cold",raw,0,at,"Owned NPC chaos-ice packet",pierce:e=>e=="cold"?OriginalNpcEquipment.Graph(b).Read(335):0,freezeFrames:packet.ColdFrames);
        Require(impact.AfterHealth256<before&&impact.Packet.ColdFrames==0&&NamedControl.State(target)?.Freeze?.Source==b.Serial.Value&&NpcEquipmentRandom.Require(b)==rolled,"Own Phoenix packet lost actual damage/freeze,added ordinary chill or drew extra caster RNG");
        Require(NecromancerEffects.ManaBalance256(owner)==mana&&ShadowLoadout.Originals(b).Select(i=>i.SavedText).SequenceEqual(items),"NPC cold receiver borrowed owner mana or changed original items");
        SkillHealth.SetBalance256(b,b.MaximumLife256*2/3-1);return target;
    }
    private static void Check(bool reload)
    {
        PlayerMobile owner;AssassinShadowBody[] bodies;int generation=0;
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=saved.Generation+1;owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);bodies=saved.Bodies.Select(h=>(AssassinShadowBody)World.FindMobile((Serial)h.Actor)).ToArray();
            for(int i=0;i<bodies.Length;i++){var t=(BaseCreature)World.FindMobile((Serial)saved.Bodies[i].Target);Require(Json(Capture(bodies[i],t))==Json(saved.Bodies[i]),"Reload lost original cold emitter/resources/items/wounds/RNG or freeze identity/deadline");t.Delete();}
            owner.MoveToWorld(new(6260,3720,0),Map.Trammel);
        }
        else
        {
            owner=BladeBodyChecks.Fresh(6260,24,3720);ClassSkillChecks.Train(owner,"assassin.shadowwarrior",20);ClassSkillChecks.Train(owner,"assassin.shadowmaster",20);var game=NpcWorldAllocation.Capture();
            try
            {
                bodies=new AssassinShadowBody[2];int i=0;
                foreach(var ability in new[]{CombatAbility.ShadowWarrior,CombatAbility.ShadowMaster})
                {NpcWorldAllocation.Restore(new(0,NpcWorldAllocation.Profile,1,SourceRandom.InitLow(1)));bodies[i]=AssassinShadowBody.CreateInitialized(owner,ability,TimedEffects.Now);GeneratedNpcLiveResources.ActivateItemEvents(bodies[i]);ShadowEquipmentDefense.Activate(bodies[i]);i++;}
            }
            finally{NpcWorldAllocation.Restore(game);}
        }
        var targets=bodies.Select(b=>Exercise(b,owner,generation)).ToArray();GeneratedNpcLiveResources.Snapshot();foreach(var b in bodies){PersistentMinions.Park(b);}PersistentMinions.Suspend(owner);owner.Internalize();NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());
        File.WriteAllText(Manifest,Json(new Saved(owner.Serial.Value,bodies.Select((b,i)=>Capture(b,targets[i])).ToArray(),generation)));

        Log.Information("Owned NPC spell freeze:shared typed length/mitigation/status,independent zero-damage freeze and chill-only lane,resistance/pierce/immunity,own chaos-ice packet damage/source/RNG and original resources/status deadlines through reloads;Phoenix missile and elemental finisher routes remain unimplemented.");
    }
}
