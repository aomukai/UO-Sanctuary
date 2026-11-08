// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ShadowKickPacketChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowKickPacketChecks));
    private sealed record Held(uint Actor,string Body,string Resources,string[] Items,NpcEquipmentSeed Random,int Life,uint Target,int TargetLife,IronGolemContact.Saved Pending);
    private sealed record Saved(uint Owner,Held[] Bodies,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shadow-kick-packets-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("shadow-kick-packets-check",false);Register("shadow-kick-packets-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Own NPC kick typed packet, original hand fill and progressive postfill",_=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static BaseCreature Target(AssassinShadowBody b)
    {
        var t=new GraveyardZombie{Frozen=true,CantWalk=true,NoKillAwards=true};t.SetHits(65000);
        foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}t.MoveToWorld(new(b.X+1,b.Y,b.Z),b.Map);t.AIObject.AITimer.Stop();TimedEffects.PreventHealing(t,TimedEffects.Now);return t;
    }
    private static void Gain(AssassinShadowBody b,int id,int count,DateTime at)
    {var v=ShadowSkillCasting.OwnValues(b,b.OwnedSkillRank(id),id);for(int i=0;i<count;i++){AssassinCharges.Gain(b,v,at);}}
    private static ShadowKickPacket WithSeed(AssassinShadowBody b,BaseCreature t,ulong seed,int flags=5,int skill=255)
    {NpcEquipmentRandom.Set(b,seed);return ShadowKickPackets.Prepare(b,t,flags,skill:skill);}
    private static void Packets(AssassinShadowBody b,PlayerMobile owner,AssassinShadowBody other,int skill=255)
    {
        int enhanced=skill==255?82:485,hitClass=skill==255?1:0;
        var t=Target(b);var graph=b.EquipmentNumbers;var binding=ShadowLoadout.Binding(b);var at=TimedEffects.Now.AddMinutes(15);
        try
        {
            AssassinCharges.Clear(b);var rules=IronGolemContact.Require(b,owner,t);var seed=SourceRandom.InitLow(1301);int hp=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(owner);
            var alien=ShadowLoadout.Binding(other);var foreign=OriginalNpcEquipment.Graph(other).Weapons(alien.Primary,alien.Secondary,true,true,4,0,0);
            var ownSeed=NpcEquipmentRandom.Require(b);var otherSeed=NpcEquipmentRandom.Require(other);bool rejected=false;
            try{OriginalNpcMelee.Allocate(b,OriginalNpcMelee.TargetFacts(t),new(HitFlags:3,ResultFlags:5),128,foreign,true);}catch(NotSupportedException){rejected=true;}
            Require(rejected&&NpcEquipmentRandom.Require(b)==ownSeed&&NpcEquipmentRandom.Require(other)==otherSeed&&SkillHealth.Balance256(t)==hp,"Kick borrowed foreign weapon graph or mutated before refusing it");
            var packet=WithSeed(b,t,seed,skill:skill);var after=NpcEquipmentRandom.Require(b);
            // Independent source-formula inputs plus the already-qualified
            // original filler establish this new caller's composition.
            var input=ShadowKickDamage.Capture(b,enhanced);var range=DragonKickDamage.Range256(input);ulong s=seed;
            int physical=range.Min+(int)SourceRandom.Limited(ref s,range.Max-range.Min);NpcEquipmentRandom.Set(b,s);
            var frame=graph.Weapons(binding.Primary,binding.Secondary,true,true,4,0,0);
            var expected=OriginalNpcMelee.Allocate(b,OriginalNpcMelee.TargetFacts(t),new(Physical:physical,AdditionalPercent:enhanced,HitFlags:3,ResultFlags:5),128,frame,true) with{HitClass=hitClass};
            Require(packet.Input==input&&packet.Filled==expected&&NpcEquipmentRandom.Require(b)==after,"Kick physical/typed full-source composition or draw order differs");
            Require(packet.Filled.HitFlags==35&&packet.Filled.HitClass==hitClass&&SkillHealth.Balance256(t)==hp,"Kick recomputed weapon physical, lost skill hit class or delivered HP during allocation");
            var primary=graph.Item(binding.Primary).Unit;var secondary=graph.Item(binding.Secondary).Unit;
            var nodes=new[]{primary.Stats,secondary.Stats,graph.Actor.Stats};int[] stats=[17,48,49,141,337];
            var original=nodes.Select(n=>stats.Select(id=>EquipmentStatValues.Raw(n.BaseValues,EquipmentStatKey.Pack(id))).ToArray()).ToArray();
            try
            {
                // Both ordinary physical critical properties must be ignored
                // by kick flags3, including their RNG calls.
                foreach(int id in new[]{141,337}){graph.Values.SetBase(graph.Actor.Stats,EquipmentStatKey.Pack(id),100);}
                Require(WithSeed(b,t,seed,skill:skill)==packet&&NpcEquipmentRandom.Require(b)==after,"Kick applied Deadly/weapon critical or drew their RNG");
                Require(graph.Read(329)==0&&NecromancerSummons.Source(b.SourceMonsterId,"Crit")==0,"Typed hand witness needs its declared no-fire-mastery/no-intrinsic-critical source");
                graph.Values.SetBase(primary.Stats,EquipmentStatKey.Pack(17),original[0][0]+217);
                graph.Values.SetBase(primary.Stats,EquipmentStatKey.Pack(48),original[0][1]+7);graph.Values.SetBase(primary.Stats,EquipmentStatKey.Pack(49),original[0][2]+7);
                graph.Values.SetBase(secondary.Stats,EquipmentStatKey.Pack(48),original[1][1]+1000);graph.Values.SetBase(secondary.Stats,EquipmentStatKey.Pack(49),original[1][2]+1000);
                var hand=WithSeed(b,t,seed,skill:skill);
                Require(hand.Filled.Physical==packet.Filled.Physical&&hand.Filled.Fire==packet.Filled.Fire+7*256
                    &&graph.DamageItems.Contains(binding.Primary)&&!graph.DamageItems.Contains(binding.Secondary),"Kick used weapon ED, both typed hands or failed primary restoration");
            }
            finally{for(int n=0;n<nodes.Length;n++){for(int i=0;i<stats.Length;i++){graph.Values.SetBase(nodes[n],EquipmentStatKey.Pack(stats[i]),original[n][i]);}}}
            foreach(int id in new[]{254,265,280,259,274,269}){Gain(b,id,3,at);}
            string charges=Json(AssassinCharges.Finisher(b)),effects=Json(new{Cold=MartialCold.Snapshot(),Fire=FireGround.Snapshot(),Thunder=MartialLightning.Snapshot(),Phoenix=MartialPhoenix.Snapshot()});
            var world=NpcWorldAllocation.Capture();var charged=WithSeed(b,t,seed,skill:skill);var cobra=ItemSkillExecution.Skill(265);int drain=2*(cobra.Number("Param1")+2*cobra.Number("Param2"));
            var cold=ShadowSkillCasting.OwnValues(b,3,274);
            Require(charged.Input.Value.SkillDamagePercent>enhanced&&charged.Filled.Fire>0&&charged.Filled.Cold>0&&charged.Filled.Lightning>0
                &&charged.Filled.ColdFrames==cold.DurationFrames&&charged.Filled.FreezeFrames==cold.DurationFrames
                &&charged.Filled.LifeDrain>=drain&&charged.Filled.ManaDrain>=drain&&(charged.Filled.ResultFlags&16384)!=0,"Kick omitted Tiger/Cobra/fire/ice/lightning postfill");
            Require(Json(AssassinCharges.Finisher(b))==charges&&Json(new{Cold=MartialCold.Snapshot(),Fire=FireGround.Snapshot(),Thunder=MartialLightning.Snapshot(),Phoenix=MartialPhoenix.Snapshot()})==effects
                &&NpcWorldAllocation.Capture()==world&&SkillHealth.Balance256(t)==hp,"Cached kick allocation emitted effects, consumed charges or delivered HP");
            var no=WithSeed(b,t,seed,0,skill);Require(no.Input==null&&no.Filled.HitClass==hitClass&&no.Impact.Packet.Total256==0&&NpcEquipmentRandom.Require(b).Seed==seed,"Miss allocated kick damage or consumed RNG");
            AssassinCharges.Clear(b);var pending=new IronGolemContact.Prepared(rules,packet.Impact);
            var result=IronGolemContact.Release(b,owner,t,pending,_=>true,at);
            Require(result.Commit!=null&&SkillHealth.Balance256(t)==hp-packet.Impact.Packet.Total256,"Prepared kick lost ordered native HP delivery");
            int life=SkillHealth.Balance256(t);bool refused=false;
            try{IronGolemContact.Release(b,owner,t,pending,_=>true,at);}catch(InvalidOperationException){refused=true;}
            Require(refused&&pending.Consumed&&SkillHealth.Balance256(t)==life,"Kick contact replayed");
            Require(NecromancerEffects.ManaBalance256(owner)==mana,"NPC packet borrowed player mana");
        }
        finally{AssassinCharges.Clear(b);t.Delete();}
    }
    private static void FlightPackets(AssassinShadowBody b,PlayerMobile owner,AssassinShadowBody other)
    {
        b.ChangeSkills(book=>{book.Assign(275,12);book.Use(275);});
        try{Packets(b,owner,other,275);}
        finally{b.ChangeSkills(book=>book.Use(null));}
    }
    private static Held Capture(AssassinShadowBody b,BaseCreature t,IronGolemContact.Prepared contact)=>new(b.Serial.Value,b.SavedText,Json(GeneratedNpcLiveResources.State(b)),
        ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray(),NpcEquipmentRandom.Require(b),SkillHealth.Balance256(b),t.Serial.Value,SkillHealth.Balance256(t),IronGolemContact.Capture(contact));
    private static void Check(bool reload)
    {
        PlayerMobile owner;AssassinShadowBody[] bodies;int generation=0;
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=saved.Generation+1;owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
            bodies=saved.Bodies.Select(h=>(AssassinShadowBody)World.FindMobile((Serial)h.Actor)).ToArray();
            for(int i=0;i<bodies.Length;i++)
            {
                var b=bodies[i];var h=saved.Bodies[i];Require(b.SavedText==h.Body&&Json(GeneratedNpcLiveResources.State(b))==h.Resources
                    &&ShadowLoadout.Originals(b).Select(x=>x.SavedText).SequenceEqual(h.Items)&&NpcEquipmentRandom.Require(b)==h.Random&&SkillHealth.Balance256(b)==h.Life
                    &&ShadowActions.State(b)==null,"Reload changed original kick actor/items/resources/RNG/wounds or replayed action");
            }
            owner.MoveToWorld(new(6330,3470,0),Map.Trammel);
            for(int i=0;i<bodies.Length;i++)
            {
                var b=bodies[i];b.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);var h=saved.Bodies[i];var t=(BaseCreature)World.FindMobile((Serial)h.Target);
                Require(SkillHealth.Balance256(t)==h.TargetLife,"Pending kick target wounds changed");var contact=IronGolemContact.Restore(h.Pending);
                Require(IronGolemContact.Release(b,owner,t,contact,_=>true,TimedEffects.Now.AddMinutes(20)).Commit!=null
                    &&SkillHealth.Balance256(t)==h.TargetLife-h.Pending.Impact.Packet.Total256,"Saved original kick packet failed native delivery");t.Delete();
            }
        }
        else
        {
            owner=BladeBodyChecks.Fresh(6330,12,3470);ClassSkillChecks.Train(owner,"assassin.shadowwarrior",20);ClassSkillChecks.Train(owner,"assassin.shadowmaster",20);
            var world=NpcWorldAllocation.Capture();bodies=new AssassinShadowBody[2];
            try{int i=0;foreach(var a in new[]{CombatAbility.ShadowWarrior,CombatAbility.ShadowMaster}){NpcWorldAllocation.Restore(new(0,NpcWorldAllocation.Profile,1,SourceRandom.InitLow(1)));bodies[i]=AssassinShadowBody.CreateInitialized(owner,a,TimedEffects.Now);GeneratedNpcLiveResources.ActivateItemEvents(bodies[i]);ShadowEquipmentDefense.Activate(bodies[i]);i++;}}
            finally{NpcWorldAllocation.Restore(world);}
        }
        var held=new Held[bodies.Length];int index=0;
        foreach(var b in bodies)
        {
            b.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);b.ChangeSkills(book=>book.Assign(255,12));foreach(int id in new[]{254,265,280,259,274,269}){b.ChangeSkills(book=>book.Assign(id,3));}Packets(b,owner,bodies.Single(x=>x!=b));FlightPackets(b,owner,bodies.Single(x=>x!=b));
            var t=Target(b);var rules=IronGolemContact.Require(b,owner,t);var packet=ShadowKickPackets.Prepare(b,t,5);var contact=new IronGolemContact.Prepared(rules,packet.Impact);
            SkillHealth.SetBalance256(b,b.MaximumLife256*2/3-1);PersistentMinions.Park(b);held[index++]=Capture(b,t,contact);
        }
        GeneratedNpcLiveResources.Snapshot();
        held=held.Select(h=>h with{Resources=Json(GeneratedNpcLiveResources.State((AssassinShadowBody)World.FindMobile((Serial)h.Actor)))}).ToArray();
        PersistentMinions.Suspend(owner);owner.Internalize();NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());File.WriteAllText(Manifest,Json(new Saved(owner.Serial.Value,held,generation)));

        Log.Information("Own NPC Talon/Flight packets:original body/boot physical plus full source128typed fill,primary-only typed hand,flags3critical bypass,six-family shared postfill without emission/consumption,miss/no RNG,ordered HP/no replay and saved original prepared packet through two reloads;startup/repeated action remains separate.");
    }
}
