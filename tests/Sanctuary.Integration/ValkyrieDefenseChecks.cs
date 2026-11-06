// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ValkyrieDefenseChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ValkyrieDefenseChecks));
    private sealed record Held(uint Body,string Text,uint[] Items,string[] Payloads,NpcEquipmentSeed Random,int Life);
    private sealed record Saved(uint Owner,Held[] Bodies,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"valkyrie-defense-fixtures.json");
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("valkyrie-defense-check",false);Register("valkyrie-defense-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Original Valkyrie inherited defensive hit consumers",_=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private sealed class Supplied(JsonElement row):SourceAvoidanceServices
    {
        internal readonly int[] A=row.GetProperty("Args").EnumerateArray().Select(v=>v.GetInt32()).ToArray();
        internal ulong Seed=ulong.Parse(row.GetProperty("Seed").GetString());
        internal override bool Present=>A[0]!=0;
        internal override int Kind=>A[1];internal override int Mode=>A[2];internal override bool Moving=>Mode is 2 or 3 or 15;
        internal override int BlockRate=>Present?A[3]:0;
        internal override int Stat(int stat)=>!Present?0:stat switch{338=>A[4],339=>A[5],340=>A[6],_=>throw new InvalidOperationException("Unexpected avoidance stat")};
        internal override IEnumerable<EquipmentStatValue> WeaponBlockStats=>row.GetProperty("Layers").EnumerateArray().Select(v=>new EquipmentStatValue(EquipmentStatKey.Pack(348,v[0].GetInt32()),v[1].GetInt32()));
        internal override bool HandType(int type)=>A[9]!=0&&type is>0 and<=8&&((A[7]|A[8])&(1<<(type-1)))!=0;
        internal override int WeaponClass=>A[10];internal override uint Raw()=>SourceRandom.Roll(ref Seed);
    }
    private static void SourceCases()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"source-avoidance-cases.json")));int count=0;
        foreach(var c in doc.RootElement.EnumerateArray())
        {
            var s=new Supplied(c);Require(SourceAvoidance.WeaponBlockChance(s)==c.GetProperty("WeaponBlock").GetInt32(),"Layered source weapon-block mismatch "+count);
            int flags=SourceAvoidance.Resolve(s,(s.A[11]&1)!=0,(s.A[11]&2)!=0);
            Require(flags==c.GetProperty("Result").GetInt32()&&s.Seed==ulong.Parse(c.GetProperty("After").GetString()),"Full source block/dodge/RNG ordering mismatch "+count);count++;
        }
        Require(count==16384,"Missing source avoidance controls");
    }
    private static Held Store(AmazonValkyrieBody b)
    {var items=ValkyrieEquipment.Originals(b);return new(b.Serial.Value,b.SavedText,items.Select(i=>i.Serial.Value).ToArray(),items.Select(i=>i.SavedText).ToArray(),NpcEquipmentRandom.State(b),SkillHealth.Balance256(b));}
    private static ulong SeedFor(bool success,int chance)
    {
        for(int i=0;i<100000;i++){ulong seed=SourceRandom.InitLow(i),copy=seed;if((SourceRandom.Roll(ref copy)%100<chance)==success){return seed;}}
        throw new InvalidOperationException("No controlled defender seed");
    }
    private static void Inspect(AmazonValkyrieBody b,JsonElement c,int generation,PlayerMobile supplier)
    {
        var old=Store(b);string stats=Json(b.EquipmentNumbers.Stats);
        Require(SourceBlocking.TryRate(b,out int shield,out bool animation)&&shield==0&&!animation,"Valkyrie ToBlock28 became a phantom shield");
        // Expected stats are independent full-getter/current-formula source
        // outputs from the prior damage oracle,not the live passive view.
        foreach(int command in new[]{0,1})
        {
            if(command==1){BattleCommand.Apply(supplier,b,1,TimedEffects.Now);}
            var r=c.GetProperty("Packets").EnumerateArray().First(r=>r.GetProperty("Generation").GetInt32()==generation&&r.GetProperty("Command").GetInt32()==command);
            var fields=r.GetProperty("Passives").EnumerateArray().ToDictionary(v=>v[2].GetInt32(),v=>v[3].GetInt32());
            foreach(int mode in new[]{1,2,15})foreach(bool avoid in new[]{false,true})foreach(bool block in new[]{false,true})
            {
                var before=NpcEquipmentRandom.State(b);ulong seed=before.Seed;
                int chance=fields[mode is 2 or 15?340:avoid?339:338];int expected=chance>0&&SourceRandom.Roll(ref seed)%100<chance?(mode is 2 or 15?8:avoid?2:4):0;
                int actual=ValkyrieEquipmentDefense.SuppliedMode(b,mode,avoid,block);
                Require(actual==expected&&NpcEquipmentRandom.State(b)==before with{Seed=seed},"Original bound avoidance/passive/source RNG mismatch");
            }
            BattleCommand.Clear(b);
        }
        Require(PoolsUnchanged(b,old)&&Json(b.EquipmentNumbers.Stats)==stats,"Defender rolls changed original wounds/items/stat graph");
    }
    private static bool PoolsUnchanged(AmazonValkyrieBody b,Held old)=>SkillHealth.Balance256(b)==old.Life&&b.SavedText==old.Text&&ValkyrieEquipment.Originals(b).Select(i=>i.SavedText).SequenceEqual(old.Payloads);
    private static void World(AmazonValkyrieBody b,PlayerMobile owner)
    {
        var attacker=new GraveyardSkeleton();attacker.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);attacker.Frozen=true;attacker.CantWalk=true;attacker.AIObject.AITimer.Stop();
        var caster=new PlayerMobile{Name="Unassigned hostile defense fixture",Body=0x190,Player=true};caster.MoveToWorld(new(owner.X+3,owner.Y,owner.Z),owner.Map);
        try
        {
            int dodge=ValkyrieEquipmentDamage.Passives(b).Single(p=>p.Stat==338).Value,avoid=ValkyrieEquipmentDamage.Passives(b).Single(p=>p.Stat==339).Value;
            ulong yes=SeedFor(true,dodge),no=SeedFor(false,dodge);NpcEquipmentRandom.Set(b,yes);ulong expected=yes;SourceRandom.Roll(ref expected);
            Require(ClassStats.CheckHit(attacker,b,0,()=>0,guaranteed:true)==false&&NpcEquipmentRandom.State(b).Seed==expected,"Guaranteed accuracy bypassed NPC Dodge or used the wrong stream");
            NpcEquipmentRandom.Set(b,no);expected=no;SourceRandom.Roll(ref expected);
            Require(ClassStats.CheckHit(attacker,b,0,()=>0,guaranteed:true)==true&&NpcEquipmentRandom.State(b).Seed==expected,"Failed NPC Dodge did not retain the original accuracy result");
            NpcEquipmentRandom.Set(b,yes);expected=yes;SourceRandom.Roll(ref expected);
            int result=NpcMeleeResult.Resolve(attacker,b,_=>true,()=>true,_=>0);
            Require(result==SourceMeleeResult.Dodge&&NpcEquipmentRandom.State(b).Seed==expected,"Ordered melee rolled defense twice or folded Dodge into shield Block");
            NpcEquipmentRandom.Set(b,SeedFor(true,avoid));int life=SkillHealth.Balance256(b);var seed=NpcEquipmentRandom.State(b);expected=seed.Seed;SourceRandom.Roll(ref expected);
            Require(!ClassCombat.Friendly(caster,b)&&SkillCombat.FireImpact(caster,b,2560)==0&&SkillHealth.Balance256(b)==life&&NpcEquipmentRandom.State(b).Seed==expected,"Actual incoming fire bypassed Avoid or committed HP on success");
            NpcEquipmentRandom.Set(b,SeedFor(false,avoid));life=SkillHealth.Balance256(b);
            Require(SkillCombat.FireImpact(caster,b,2560)>0&&SkillHealth.Balance256(b)<life,"Failed Avoid did not pass actual incoming fire to HP");
            b.CantWalk=false;var before=Store(b);bool refused=false;
            try{ClassStats.CheckHit(attacker,b,0,()=>throw new InvalidOperationException("Accuracy drew before unsupported mode refusal"),guaranteed:true);}catch(NotSupportedException){refused=true;}
            Require(refused&&Json(Store(b))==Json(before),"World movement used an invented source mode or advanced defender RNG");b.CantWalk=true;
        }
        finally{b.CantWalk=true;attacker.Delete();caster.Delete();}
    }
    private static void Check(bool reload)
    {
        SourceCases();using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"valkyrie-damage-cases.json")));var cases=doc.RootElement.EnumerateArray().Where(c=>c.GetProperty("Critical").GetInt32()==1).ToArray();Require(cases.Length==57,"Original defense loadout corpus");
        PlayerMobile p;int generation=0;var bodies=new List<AmazonValkyrieBody>();var supplier=ClassSkillChecks.Player(DiabloClass.Barbarian);
        try
        {
            if(reload)
            {
                var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Generation<2,"Defense reload count");generation=saved.Generation+1;p=WorldMobile(saved.Owner);
                foreach(var h in saved.Bodies){var b=Server.World.FindMobile((Serial)h.Body)as AmazonValkyrieBody;Require(b is{Alive:true,Deleted:false}&&b.ControlMaster==p&&Json(Store(b))==Json(h),"Original defense state/gear/wound/stream changed on reload");bodies.Add(b);}
            }
            else
            {
                p=new PlayerMobile{Name="ValkyrieDefenseFixture",Body=0x190,Player=true};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(p,99);ClassSkillChecks.Train(p,"amazon.valkyrie",20);FixtureSpace.Corridor(p,6380,20,3700);
                foreach(var c in cases)
                {
                    var b=ValkyrieDamageChecks.Body(p,c);string old=b.SavedText;Require(!old.Contains("EquipmentDefenseProfile")&&Json(JsonSerializer.Deserialize<ValkyrieBodyState>(old))==old,"Legacy body JSON changed");ValkyrieEquipmentDefense.Activate(b);bodies.Add(b);
                }
                var before=Store(bodies[0]);bool refused=false;try{ValkyrieEquipmentDefense.Activate(bodies[0]);}catch(NotSupportedException){refused=true;}Require(refused&&Json(Store(bodies[0]))==Json(before),"Repeated defense activation reset original state");
            }
            for(int i=0;i<bodies.Count;i++){Inspect(bodies[i],cases[i],generation,supplier);}p.MoveToWorld(new(6380,3700,0),Map.Trammel);bodies[0].MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);World(bodies[0],p);
            foreach(var b in bodies){b.RestorePrepared(b.Prepared with{NextRecoveryUtc=TimedEffects.Now.AddDays(2)});}
            PersistentMinions.Suspend(p);p.Internalize();File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,bodies.Select(Store).ToArray(),generation)));
        }
        finally{supplier.Delete();}
        Log.Information("Valkyrie defense:16384complete source contexts;57original loadouts/330items;1368bound mode/Command decisions per boot;actual guaranteed/ordered melee and fire Avoid/HP,once-only shared RNG,phantom-shield guard,legacy profile/parking/reloads;moving world mode/controller/full incoming contact remain gated");
    }
    private static PlayerMobile WorldMobile(uint id)=>Server.World.FindMobile((Serial)id)as PlayerMobile??throw new InvalidOperationException("Original defense owner lost");
}
