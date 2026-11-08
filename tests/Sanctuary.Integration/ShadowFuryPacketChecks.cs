// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ShadowFuryPacketChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowFuryPacketChecks));
    private sealed record Held(uint Actor,string Body,string Resources,string[] Items,NpcEquipmentSeed Random,int Life,ShadowFuryPacket Packet);
    private sealed record Saved(uint Owner,Held[] Bodies,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shadow-fury-packets-fixtures.json");
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v);
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("shadow-fury-packets-check",false);Register("shadow-fury-packets-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Own NPC Fury weapon/stat snapshot and original saves",_=>Core.LoopContext.Post(()=>{try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Differential()
    {
        Require(ItemSkillExecution.All.Where(r=>r.UseManaOnDo).Select(r=>r.Id).SequenceEqual(new[]{25,266,402})&&ItemSkillExecution.All.Where(r=>r.UseManaOnDo).All(r=>r.LegacyRecord(false,1).UseManaOnDo),"Current usemanaondo flags were dropped or read with wrong capitalization");
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"shadow-fury-packet-cases.json")));var root=json.RootElement;var fields=root.GetProperty("fields").EnumerateArray().Select(x=>x.GetString()).ToArray();var s=root.GetProperty("stats");int S(string key)=>s.GetProperty("STAT_"+key).GetInt32();
        var raw=new MissileSourceStats(S("FIREMINDAM")*256,S("FIREMAXDAM")*256,S("LIGHTMINDAM")*256,S("LIGHTMAXDAM")*256,S("MAGICMINDAM")*256,S("MAGICMAXDAM")*256,S("COLDMINDAM")*256,S("COLDMAXDAM")*256,S("COLDLENGTH"),S("POISONMINDAM"),S("POISONMAXDAM"),S("POISONLENGTH"),S("POISON_COUNT"),S("LIFEDRAINMINDAM"),S("MANADRAINMINDAM"));int count=0;
        var skill=SkillMechanics.For(CombatAbility.BladeFury);string sentinel=SkillMechanics.For(CombatAbility.BladeSentinel).SourceKey;
        foreach(var row in root.GetProperty("cases").EnumerateArray())
        {
            int rank=row[0].GetInt32(),hard=row[1].GetInt32(),percent=row[2].GetInt32();bool critical=row[3].GetInt32()!=0;var expected=row[4].EnumerateArray().Select(x=>x.GetInt32()).ToArray();int E(string key)=>expected[Array.IndexOf(fields,key)];
            var v=new SkillValues(skill,key=>key==skill.SourceKey?rank:0,key=>key==sentinel?hard:0,99);
            var d=BladeFuryLaunch.FromSource(v,9*256,14*256,false,percent+17+S("STRENGTH")+S("DEXTERITY")/2+Math.Max(S("ITEM_MINDAMAGE_PERCENT"),S("ITEM_MAXDAMAGE_PERCENT")),S("ITEM_DEMONDAMAGE_PERCENT"),S("ITEM_UNDEADDAMAGE_PERCENT"),critical,raw);var e=d.Elements;
            Require(d.Min256==E("nMinDamage")&&d.Max256==E("nMaxDamage")&&d.Percent==E("nDamagePercent")&&d.DemonPercent==E("nDemonDamagePercent")&&d.UndeadPercent==E("nUndeadDamagePercent")&&d.Critical==((E("nFlags")&2)!=0)
                &&e.FireMin==E("nFireMinDamage")&&e.FireMax==E("nFireMaxDamage")&&e.LightningMin==E("nLightMinDamage")&&e.LightningMax==E("nLightMaxDamage")&&e.MagicMin==E("nMagicMinDamage")&&e.MagicMax==E("nMagicMaxDamage")
                &&e.ColdMin==E("nColdMinDamage")&&e.ColdMax==E("nColdMaxDamage")&&e.ColdFrames==E("nColdLength")&&e.PoisonMin==E("nPoisonMinDamage")&&e.PoisonMax==E("nPoisonMaxDamage")&&e.PoisonFrames==E("nPoisonLength")&&e.PoisonCount==E("nPoisonCount")
                &&e.LifeDrain==E("nLifeDrainMinDamage")&&e.ManaDrain==E("nManaDrainMinDamage")&&d.SkillAttackPercent==rank*10,"Complete monster creator versus reused Fury packet differs");count++;
        }
        Require(count==16,"Incomplete own Fury source contexts");
    }
    private static ShadowFuryPacket Packet(AssassinShadowBody b)=>ShadowFuryPackets.Capture(b,b.OwnedSkillRank(266),false);
    private static void Live(AssassinShadowBody b,PlayerMobile p)
    {
        b.ChangeSkills(book=>book.Use(266));var graph=b.EquipmentNumbers;var binding=ShadowLoadout.Binding(b);var primary=graph.Item(binding.Primary).Unit.Stats;var secondary=graph.Item(binding.Secondary).Unit.Stats;var actor=graph.Actor.Stats;
        int Base(EquipmentStatNode n,int stat)=>EquipmentStatValues.Raw(n.BaseValues,EquipmentStatKey.Pack(stat));void Set(EquipmentStatNode n,int stat,int value)=>graph.Values.SetBase(n,EquipmentStatKey.Pack(stat),value);
        var rng=NpcEquipmentRandom.Require(b);var world=NpcWorldAllocation.Capture();string effects=Json(new{W=WeaponMissiles.Snapshot(),F=FireGround.Snapshot(),C=MartialCold.Snapshot(),L=MartialLightning.Snapshot(),Charges=AssassinCharges.Snapshot()});int mana=NecromancerEffects.ManaBalance256(p);var original=Packet(b);
        int fire=Base(primary,48),off=Base(secondary,48),armor=Base(actor,120),poison=Base(actor,101),strength=p.RawStr;
        try
        {
            Set(primary,48,fire+7);Set(secondary,48,off+1000);var a=Packet(b);
            Require(a.Damage.Min256==original.Damage.Min256&&a.Damage.Max256==original.Damage.Max256&&a.Damage.Elements.FireMin==original.Damage.Elements.FireMin+7*256*96/128
                &&graph.DamageItems.Contains(binding.Primary)&&!graph.DamageItems.Contains(binding.Secondary),"Own Fury used secondary typed hand or changed physical during fire addition");
            Set(primary,48,fire);Set(secondary,48,off);Set(actor,120,-5);Require(Packet(b).TargetArmorDelta==-10,"Fury lost source double target-armor delta");Set(actor,120,armor);
            Set(actor,101,10);Require(Packet(b).Damage.Elements.PoisonFrames==10&&Packet(b).Damage.Elements.PoisonCount==0,"Fury lost poison override/no-count capture");Set(actor,101,poison);
            p.RawStr=strength+100;Require(Packet(b)==original,"Own Fury borrowed owner attributes");p.RawStr=strength;
            b.ChangeSkills(book=>book.SetBonus(257,20));Require(Packet(b)==original,"Fury incorrectly used soft points for hard Sentinel synergy");b.ChangeSkills(book=>book.SetBonus(257,0));
            b.ChangeSkills(book=>book.Assign(257,4));Require(Packet(b).Damage.Min256>original.Damage.Min256,"Fury ignored own hard Sentinel synergy");b.ChangeSkills(book=>book.Assign(257,3));
            var critical=ShadowFuryPackets.Capture(b,b.OwnedSkillRank(266),true);Require(critical.Damage==original.Damage with{Critical=true}&&critical.Flags==(original.Flags|2),"Fury critical service altered endpoints or other creation flags");
            bool refused=false;try{_=ShadowFuryPackets.Capture(b,b.OwnedSkillRank(266)+1,false);}catch(NotSupportedException){refused=true;}Require(refused,"Fury accepted a borrowed rank");
            Require(NpcEquipmentRandom.Require(b)==rng&&NpcWorldAllocation.Capture()==world&&NecromancerEffects.ManaBalance256(p)==mana&&Json(new{W=WeaponMissiles.Snapshot(),F=FireGround.Snapshot(),C=MartialCold.Snapshot(),L=MartialLightning.Snapshot(),Charges=AssassinCharges.Snapshot()})==effects,"Fury snapshot rolled RNG,spent resources,emitted or consumed charges");
            Require(JsonSerializer.Deserialize<ShadowFuryPacket>(Json(original))==original,"Fury packet JSON changed capture");
        }
        finally{Set(primary,48,fire);Set(secondary,48,off);Set(actor,120,armor);Set(actor,101,poison);p.RawStr=strength;b.ChangeSkills(book=>{book.SetBonus(257,0);book.Assign(257,3);book.Use(null);});}
    }
    private static void Resources(AssassinShadowBody b,PlayerMobile p)
    {
        b.ChangeSkills(book=>book.Use(266));int own=NecromancerEffects.ManaBalance256(b),owner=NecromancerEffects.ManaBalance256(p);NecromancerEffects.SetMana256(b,0);NecromancerEffects.SetMana256(p,0);
        try
        {
            int allocations=0,shots=0,rewinds=0;var services=new BladeFuryChannelServices(()=>{allocations++;return true;},()=>true,()=>{shots++;return true;},()=>throw new InvalidOperationException("NPC Fury tried to debit mana"),()=>rewinds++);
            var start=ShadowFuryResources.Start(b,new(false,0,0),100,services);Require(start.Success&&start.State.Active&&start.State.Expires==121&&allocations==1&&shots==0,"Free NPC Fury startup/lease differs");
            var first=ShadowFuryResources.Do(b,start.State,109,services);Require(first.Success&&shots==1&&first.State.NextShot==113&&rewinds==1,"NPC Fury first emit/deadline differs");
            var equal=ShadowFuryResources.Start(b,first.State,113,services);Require(!equal.Success&&shots==1&&equal.State.Expires==120,"NPC Fury strict deadline/lease renewal differs");
            var next=ShadowFuryResources.Do(b,equal.State,114,services);Require(next.Success&&shots==2&&next.State.NextShot==118,"NPC Fury five-frame renewal differs");
            var released=BladeFuryChannel.Release(next.State);var renewed=ShadowFuryResources.Start(b,released,115,services);Require(renewed.Success&&!renewed.State.Active&&renewed.State.HasLease&&renewed.State.Expires==122&&allocations==1&&shots==2,"NPC Fury recreated a released state bit or lease");
            var fresh=ShadowFuryResources.Start(b,BladeFuryChannel.Expire(renewed.State),130,services);Require(fresh.Success&&allocations==2&&fresh.State.Expires==151&&NecromancerEffects.ManaBalance256(b)==0&&NecromancerEffects.ManaBalance256(p)==0,"NPC Fury expiry/reentry/debit differs");
        }
        finally{NecromancerEffects.SetMana256(b,own);NecromancerEffects.SetMana256(p,owner);b.ChangeSkills(book=>book.Use(null));}
    }
    private static Held Capture(AssassinShadowBody b)
    {
        b.ChangeSkills(book=>book.Use(266));var packet=Packet(b);b.ChangeSkills(book=>book.Use(null));return new(b.Serial.Value,b.SavedText,Json(GeneratedNpcLiveResources.State(b)),ShadowLoadout.Originals(b).Select(x=>x.SavedText).ToArray(),NpcEquipmentRandom.Require(b),SkillHealth.Balance256(b),packet);
    }
    private static void Check(bool reload)
    {
        Differential();PlayerMobile p;AssassinShadowBody[] bodies;int generation=0;
        if(reload)
        {
            var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=s.Generation+1;p=(PlayerMobile)World.FindMobile((Serial)s.Owner);bodies=s.Bodies.Select(h=>(AssassinShadowBody)World.FindMobile((Serial)h.Actor)).ToArray();
            for(int i=0;i<bodies.Length;i++){Require(Json(Capture(bodies[i]))==Json(s.Bodies[i]),"Reload changed original Fury actor/items/resources/wounds/RNG or own packet");}p.MoveToWorld(new(6380,3570,0),Map.Trammel);
        }
        else
        {
            p=BladeBodyChecks.Fresh(6380,30,3570);ClassSkillChecks.Train(p,"assassin.shadowwarrior",20);ClassSkillChecks.Train(p,"assassin.shadowmaster",20);var world=NpcWorldAllocation.Capture();bodies=new AssassinShadowBody[2];
            try{int i=0;foreach(var ability in new[]{CombatAbility.ShadowWarrior,CombatAbility.ShadowMaster}){NpcWorldAllocation.Restore(new(0,NpcWorldAllocation.Profile,1,SourceRandom.InitLow(1)));bodies[i]=AssassinShadowBody.CreateInitialized(p,ability,TimedEffects.Now);GeneratedNpcLiveResources.ActivateItemEvents(bodies[i]);ShadowEquipmentDefense.Activate(bodies[i]);i++;}}finally{NpcWorldAllocation.Restore(world);}
        }
        foreach(var b in bodies)
        {
            b.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);b.ChangeSkills(book=>{book.Assign(266,12);book.Assign(257,3);book.Assign(277,3);});Live(b,p);Resources(b,p);SkillHealth.SetBalance256(b,b.MaximumLife256*2/3-1);PersistentMinions.Park(b);
        }
        GeneratedNpcLiveResources.Snapshot();var held=bodies.Select(Capture).ToArray();PersistentMinions.Suspend(p);p.Internalize();NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,held,generation)));

        Log.Information("Own Fury packet:16complete monster creator contexts,original primary weapon/body stats/hard synergies,typed source96/poison override/double armor delta,supplied critical without actor RNG or emissions,and exact original actor/items/resources/wounds/seed/packet through two reloads;channel,critical selection and impact remain separate.");
    }
}
