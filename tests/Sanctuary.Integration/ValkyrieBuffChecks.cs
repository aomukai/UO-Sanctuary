// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ValkyrieBuffChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ValkyrieBuffChecks));
    private sealed record Held(uint Body,string Text,string[] Items,NpcEquipmentSeed Random,int Life,string Resources,string Effects);
    private sealed record Pending(Held Body,uint Target,int Life,IronGolemContact.Saved Contact);
    private sealed record Saved(uint Owner,Point3D Point,Held[] Matrix,Pending Live,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"valkyrie-buffs-fixtures.json");
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static int[] Fields(NpcFillPacket p)=>[p.Physical,p.Fire,p.Lightning,p.Cold,p.Magic,p.Poison,p.ColdFrames,p.PoisonFrames,p.StunFrames,p.LifeDrain,p.ManaDrain,p.StaminaDrain,p.AdditionalPercent,(int)p.HitFlags,p.ResultFlags,p.ConversionType,p.ConversionPercent,p.FreezeFrames,p.BurnFrames,p.HitClass,p.Overlay];
    private static Held Capture(AmazonValkyrieBody b)=>new(b.Serial.Value,b.SavedText,ValkyrieEquipment.Originals(b).Select(i=>i.SavedText).ToArray(),NpcEquipmentRandom.Require(b),SkillHealth.Balance256(b),Json(GeneratedNpcLiveResources.State(b)),Json(new{Enchant=SorceressEffects.Enchantment(b),Auras=PaladinAuras.Snapshot().Leases.Where(l=>l.Target==b.Serial.Value).OrderBy(l=>l.Ability).ToArray()}));
    public static void Configure(){Register("valkyrie-buffs-check",false);Register("valkyrie-buffs-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Original Valkyrie external Enchant/aura stats and frozen contact",_=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Clear(AmazonValkyrieBody b){SorceressEffects.Profiles.Clear(b);PaladinAuras.Clear(b);}
    private static void Buff(AmazonValkyrieBody b,int[] v)
    {
        Clear(b);var until=v[8]!=0?TimedEffects.Now.Add(FixtureSpace.Retention):TimedEffects.Now.AddSeconds(-1);
        if(v[0]!=0||v[1]!=0){SorceressEffects.Profiles.Store(b,new(Enchantment:new(until,1,v[0],v[1],20,b.ControlMaster.Serial.Value)));}
        foreach(var p in new[]{(CombatAbility.HolyFire,2),(CombatAbility.HolyFreeze,4),(CombatAbility.HolyShock,6)})
        {if(v[p.Item2]!=0||v[p.Item2+1]!=0){PaladinAuras.ElementalWeapon(new(b.Serial.Value,b.ControlMaster.Serial.Value,p.Item1,1,0,until,WeaponMin:v[p.Item2],WeaponMax:v[p.Item2+1]));}}
    }
    private static void Matrix(AmazonValkyrieBody b,JsonElement c,JsonElement buffs,int generation)
    {
        var original=Capture(b);string graph=Json(b.EquipmentNumbers.Stats);int mana=NecromancerEffects.ManaBalance256(b.ControlMaster),count=0;
        foreach(var r in c.GetProperty("Packets").EnumerateArray().Where(r=>r.GetProperty("Generation").GetInt32()==generation))
        {
            Buff(b,buffs[r.GetProperty("Buff").GetInt32()].EnumerateArray().Select(v=>v.GetInt32()).ToArray());
            var before=NpcEquipmentRandom.Require(b);Require(before.Seed==ulong.Parse(r.GetProperty("Before").GetString()),"External buff source RNG start changed");int armor=20;
            var target=new GeneratedNpcFillTarget(true,true,false,false,0,()=>armor,v=>armor=v,(_,_)=>false);
            var packet=ValkyrieEquipmentDamage.Allocate(b,target,new(ResultFlags:1),r.GetProperty("Share").GetInt32());
            Require(Fields(packet).SequenceEqual(r.GetProperty("Output").EnumerateArray().Select(v=>v.GetInt32()))&&armor==r.GetProperty("Armor").GetInt32(),"External buff source full packet/armor mismatch:case="+c.GetProperty("Case")+",buff="+r.GetProperty("Buff")+",share="+r.GetProperty("Share"));
            Require(NpcEquipmentRandom.Require(b)==before with{Seed=ulong.Parse(r.GetProperty("After").GetString())},"External buff added or reordered an actor RNG draw");
            Require(b.SavedText==original.Text&&SkillHealth.Balance256(b)==original.Life&&Json(b.EquipmentNumbers.Stats)==graph&&NecromancerEffects.ManaBalance256(b.ControlMaster)==mana,"External allocation mutated original graph/birth/wound or owner mana");count++;
        }
        Clear(b);var after=Capture(b);Require(count==32&&Json(after with{Random=original.Random})==Json(original),"External buff matrix changed original saved state");
    }
    private static ValkyrieContactChecks.Recipient Target(AmazonValkyrieBody b)
    {
        var t=new ValkyrieContactChecks.Recipient();t.SetHits(65000);t.MoveToWorld(new(b.X+1,b.Y,b.Z),b.Map);t.AIObject.AITimer.Stop();foreach(var type in Enum.GetValues<ResistanceType>()){t.SetResistance(type,0);}TimedEffects.PreventHealing(t,TimedEffects.Now);return t;
    }
    private static IronGolemContact.Prepared Contact(AmazonValkyrieBody b,PlayerMobile p,ValkyrieContactChecks.Recipient t)
    {
        double chance=ClassStats.HitChance(ClassStats.AttackRatingAgainst(b,t,0),ClassStats.Defense(t),ClassStats.Level(b),ClassStats.Level(t));
        ulong seed=0;for(int n=1;n<10000;n++){ulong next=SourceRandom.InitLow(n),copy=next;if(SourceRandom.Roll(ref copy)%100/100.0<chance){seed=next;break;}}
        Require(seed!=0,"No source hit seed");NpcEquipmentRandom.Set(b,seed);return ValkyrieEquipmentContact.Prepare(b,p,t,_=>true,TimedEffects.Now);
    }
    private static void Release(AmazonValkyrieBody b,PlayerMobile p,ValkyrieContactChecks.Recipient t,IronGolemContact.Saved pending,int life)
    {
        Clear(b);foreach(var type in Enum.GetValues<ResistanceType>()){t.SetResistance(type,100);}
        var prepared=IronGolemContact.Restore(pending);var result=IronGolemContact.Release(b,p,t,prepared,_=>true,TimedEffects.Now);
        Require(result.Commit!=null&&SkillHealth.Balance256(t)==life-pending.Impact.Packet.Total256&&prepared.Consumed,"Expired/removed Enchant,aura or target resistance rewrote a frozen original contact");
        SorceressSpellMissiles.Cancel(b);ChainLightning.Cancel(b);t.Delete();
    }
    private static Pending Live(AmazonValkyrieBody b,PlayerMobile p,int[] buffs)
    {
        Buff(b,buffs);SorceressEffects.Profiles.Clear(b);var caster=new Rat();caster.MoveToWorld(new(b.X+2,b.Y,b.Z),b.Map);caster.AIObject.AITimer.Stop();
        try
        {
            var rng=NpcEquipmentRandom.Require(b);var world=NpcWorldAllocation.Capture();int mana=NecromancerEffects.ManaBalance256(p);string graph=Json(b.EquipmentNumbers.Stats);
            var values=MagePetBuffs.Values(CombatAbility.Enchant,20,99);SorceressEffects.CastNpcEnchant(caster,b,values);var enchant=SorceressEffects.Enchantment(b);
            Require(enchant is{Rank:20}&&enchant.Caster==caster.Serial.Value&&enchant.Min256==values.Damage256(false)&&enchant.Max256==values.Damage256(true)&&NpcEquipmentRandom.Require(b)==rng&&NpcWorldAllocation.Capture()==world&&NecromancerEffects.ManaBalance256(p)==mana&&Json(b.EquipmentNumbers.Stats)==graph,"Existing NPC Enchant changed caster ownership,amounts,graph or resource/RNG context");
        }
        finally{caster.Delete();}
        var t=Target(b);int life=SkillHealth.Balance256(t);var contact=Contact(b,p,t);var packet=contact.Impact.Packet;
        Require((packet.ResultFlags&1)!=0&&packet.Fire256>0&&packet.Lightning256>0&&packet.Cold256>0&&SkillHealth.Balance256(t)==life,"Live buffed contact was refused,hit before release or lost combined elements");
        var saved=IronGolemContact.Capture(contact);contact.Cancel();Release(b,p,t,saved,life);
        // Save a second genuine packet with live leases on the parked original
        // actor. Reload then expires/removes leases before releasing it once.
        Buff(b,buffs);t=Target(b);life=SkillHealth.Balance256(t);contact=Contact(b,p,t);saved=IronGolemContact.Capture(contact);contact.Cancel();PersistentMinions.Park(b);
        return new(Capture(b),t.Serial.Value,life,saved);
    }
    private static void Check(bool reload)
    {
        Require(NpcWorldAllocation.Capture()==NpcWorldAllocationChecks.CheckedContinuation,"Unexpected world allocation before Valkyrie buffs");
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"valkyrie-buffs-source-cases.json")));
        var cases=doc.RootElement.GetProperty("Bodies").EnumerateArray().ToArray();var buffs=doc.RootElement.GetProperty("Buffs");Require(cases.Length==57&&buffs.GetArrayLength()==8,"External buff source corpus");
        PlayerMobile p;Point3D point;AmazonValkyrieBody[] matrix;AmazonValkyrieBody live;int generation=0;
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=saved.Generation+1;Require(generation<=2,"External buff reload count");p=World.FindMobile((Serial)saved.Owner)as PlayerMobile;point=saved.Point;
            matrix=saved.Matrix.Select(h=>World.FindMobile((Serial)h.Body)as AmazonValkyrieBody).ToArray();live=World.FindMobile((Serial)saved.Live.Body.Body)as AmazonValkyrieBody;
            for(int i=0;i<matrix.Length;i++){Require(Json(Capture(matrix[i]))==Json(saved.Matrix[i]),"Original external matrix actor/items/wound/RNG changed on reload");}
            Require(Json(Capture(live))==Json(saved.Live.Body),"Original live actor/items/resources/wound/RNG or Enchant/aura leases changed on reload");
            var target=World.FindMobile((Serial)saved.Live.Target)as ValkyrieContactChecks.Recipient;Require(target is{Alive:true}&&SkillHealth.Balance256(target)==saved.Live.Life,"Saved original contact target changed");
            p.MoveToWorld(point,Map.Trammel);PersistentMinions.Resume(p);Release(live,p,target,saved.Live.Contact,saved.Live.Life);
        }
        else
        {
            p=new PlayerMobile{Name="ValkyrieBuffFixture",Body=0x190,Player=true};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(p,99);ClassSkillChecks.Train(p,"amazon.valkyrie",20);FixtureSpace.Corridor(p,6220,32,3340);point=p.Location;
            matrix=cases.Select(c=>ValkyrieDamageChecks.Body(p,c)).ToArray();live=AmazonValkyrieBody.CreateInitializedController(p,new(p.X+2,p.Y,p.Z),TimedEffects.Now.AddHours(8));SkillHealth.SetBalance256(live,live.MaximumLife256-1025);
        }
        for(int i=0;i<matrix.Length;i++){Matrix(matrix[i],cases[i],buffs,generation);}
        var pending=Live(live,p,buffs[5].EnumerateArray().Select(v=>v.GetInt32()).ToArray());PersistentMinions.Suspend(p);p.Internalize();
        NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,point,matrix.Select(Capture).ToArray(),pending,generation)));
        Log.Information("Valkyrie buffs:57unfiltered original loadouts/330items,1824complete-source full packets per boot with8external contexts and4shares;whole-point Enchant,combined own mastery,exact actor RNG and immutable graph;actual NPC Enchant/buffed cached HP contact,live leases and original wounds/items/resources/RNG through two reloads;monster-type/sunder/learned cast remain separate");
    }
}
