// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Net;
using Server.Network;
using System.Text.Json;
using System.Threading.Tasks;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;
public static class FirestormChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(FirestormChecks));
    private sealed record Saved(uint Owner,uint Target,int Life256,FirestormSave State);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"firestorm-fixtures.json");
    private static int _scene;
    public static void Configure(){Register("firestorm-check",Check);Register("firestorm-reload-check",Reload);Register("firestorm-live-check",Live);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Prepared Druid Firestorm travelling emitters and burning fields",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static PlayerMobile Fresh()
    {
        var p=new PlayerMobile{Body=0x190,Player=true,Name="FirestormFixture"};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(p,99);FixtureSpace.Corridor(p,6860,25,2840+32*_scene++);return p;
    }
    private static void Train(PlayerMobile p,CombatAbility ability)
    {
        var row=SkillMechanics.For(ability);Require(TalentCatalog.TryGet(DiabloClass.Druid,row.TalentId,out var talent),"Druid talent");
        foreach(string parent in talent.Prerequisites){Train(p,SkillMechanics.All.Single(x=>x.TalentId==parent).Ability);}
        if(ClassCreation.Profiles.GetRank(p.Serial,row.TalentId)==0){bool success=ClassCreation.Profiles.TryTrain(p.Serial,row.TalentId,out var reason);Require(success,"Train: "+reason);}
    }
    private static GraveyardSkeleton Target(PlayerMobile p,int dx=1)
    {
        var target=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true};target.SetHits(2000);target.SetResistance(ResistanceType.Fire,0);target.SetResistance(ResistanceType.Cold,0);target.SetResistance(ResistanceType.Energy,0);target.SetResistance(ResistanceType.Poison,0);
        target.MoveToWorld(new(p.X+dx,p.Y,p.Z),p.Map);target.AIObject.AITimer.Stop();return target;
    }
    private static string Text(FirestormSave saved)=>JsonSerializer.Serialize(saved);
    private static DateTime Next()=>DruidFirestorm.Snapshot().Emitters.Select(x=>x.NextUtc).Concat(DruidFirestorm.Snapshot().Patches.Select(x=>x.NextUtc)).Min();
    private static long[] Launch(PlayerMobile p,DateTime at,int rank=1)=>DruidFirestorm.Create(p,new(p.X+10,p.Y,p.Z),SkillMechanics.Values(p,CombatAbility.Firestorm,rank),at);
    private static void Formula()
    {
        var d=SkillMechanics.For(CombatAbility.Firestorm);Require(d.SourceId==225 && d.Number("localdelay")==15 && !DruidCombat.Implemented(CombatAbility.Firestorm),"Prepared-only Firestorm identity");int count=0;
        for(int rank=1;rank<=255;rank++)foreach(int hard in new[]{0,1,20})foreach(int mastery in new[]{0,20})
        {
            var v=new SkillValues(d,_=>rank,key=>key is "Molten Boulder" or "Eruption"?hard:0,99);var ends=DruidFirestorm.Endpoints(v,mastery,SlowMissilePayload.Normal);
            int Expected(bool max){long basis=SkillValues.Tier(d.Number(max?"EMax":"EMin"),rank,i=>d.Number((max?"EMaxLev":"EMinLev")+i))<<2;basis+=basis*(46*hard)/100;return (int)(basis+basis*mastery/100);}
            Require(ends.Min==Expected(false) && ends.Max==Expected(true) && v.Mana256==1024,"Per-frame skill synergy/mastery endpoint");count++;
        }
        var mods=new FirestormModifiers(10,20,30,40,50);Require(DruidFirestorm.Packet(mods,2560)==new GearDamage(0,11.2,1.3,1.4,1.5),"Approved mastered spell-potion components");
        Log.Information("Firestorm:1530current endpoint/mana cases and approved potion packet pass");
    }
    private static void MotionAndCapture()
    {
        var p=Fresh();Train(p,CombatAbility.Firestorm);var at=TimedEffects.Now.AddDays(1);int mana=NecromancerEffects.ManaBalance256(p);var ids=Launch(p,at);var initial=DruidFirestorm.Snapshot();
        Require(ids.Length==3 && initial.Emitters.Length==3 && initial.Emitters.Count(x=>!x.Charged)==1 && initial.Emitters.Where(x=>x.Charged).Select(x=>x.Ordinal).Order().SequenceEqual(new[]{0,1}) && initial.Emitters.All(x=>x.VelocityFp8==1536),"One straight/two seeded emitters");
        DruidFirestorm.Tick(at.AddMilliseconds(40),true);Require(DruidFirestorm.Snapshot().Patches.Length==0 && DruidFirestorm.Snapshot().Emitters.All(x=>x.Frame==1),"Subcell move emitted fire");
        DruidFirestorm.Tick(at.AddMilliseconds(80),true);Require(DruidFirestorm.Snapshot().Patches.Length==0,"Maker emitted before previous movement callback");DruidFirestorm.Tick(at.AddMilliseconds(120),true);var early=DruidFirestorm.Snapshot().Patches;Require(early.Length>0 && early.All(x=>x.Min256==12 && x.Max256==24 && x.Rank==1),"First fresh source fields");
        var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.Name=="Tarnhelm").Id,99,new Random(173),true);Require(p.EquipItem(helm),"Actual Tarnhelm");ClassGear.Refresh(p);Train(p,CombatAbility.MoltenBoulder);
        Require(SkillMechanics.Rank(p,CombatAbility.Firestorm)==2,"Actual soft-rank control");
        DruidFirestorm.Tick(at.AddMilliseconds(240),true);var captured=DruidFirestorm.Snapshot();
        Require(captured.Patches.Any(x=>x.Min256==14 && x.Max256==29 && x.Rank==1) && early.All(x=>captured.Patches.Any(y=>x.Id==y.Id && x.Min256==y.Min256 && x.Max256==y.Max256)),"New live hard synergy/captured rank or retroactive field damage");
        DruidFirestorm.Tick(at.AddMilliseconds(40*40),true);var released=DruidFirestorm.Snapshot();Require(released.Emitters.Length==0 && released.Patches.Length>0,"Independent released fire lifetime");
        DruidFirestorm.Tick(at.AddMilliseconds(55*40),true);Require(DruidFirestorm.Snapshot().Patches.Length==0 && NecromancerEffects.ManaBalance256(p)==mana,"Natural field expiry/prepared launch payment");
        // Explicit terminal-frame control keeps do23 child-before-removal order.
        Launch(p,at);var straight=DruidFirestorm.Snapshot().Emitters.Single(x=>!x.Charged);straight=straight with{Frame=39,X=p.X+.5,CellX=p.X*2+1,CellChanged=true,NextUtc=at.AddSeconds(5)};
        DruidFirestorm.Restore(new([straight],[],straight.Id));DruidFirestorm.Tick(straight.NextUtc,true);Require(DruidFirestorm.Snapshot() is{Emitters.Length:0,Patches.Length:1},"Terminal callback discarded previously changed-cell child");
        DruidFirestorm.Cancel(p);p.Delete();
    }
    private static void FieldsAndDefense()
    {
        var p=Fresh();var a=Target(p);var b=Target(p);var at=TimedEffects.Now.AddDays(1);Launch(p,at);DruidFirestorm.Tick(at.AddMilliseconds(120),true);var source=DruidFirestorm.Snapshot().Patches.First();
        var field=source with{X=a.X*2,Y=a.Y*2,Min256=256,Max256=512};DruidFirestorm.Restore(new([], [field],field.Id));
        int before=SkillHealth.Balance256(a)+SkillHealth.Balance256(b);ulong seed=field.Seed;int expected=0;
        for(int i=0;i<14;i++){expected+=WeaponMissileDamage.RollValue(256,512,0,bound=>(int)SourceRandom.Limited(ref seed,bound));}
        DruidFirestorm.Tick(field.UntilUtc.AddMilliseconds(-40),true);var after=DruidFirestorm.Snapshot().Patches.Single();
        Require(before-SkillHealth.Balance256(a)-SkillHealth.Balance256(b)==expected && after.Seed==seed && (SkillHealth.Full(a) || SkillHealth.Full(b)),"One collider/14exclusive seeded fire rolls or extra field callback");
        // State at next==expiry is valid between the final hit and removal.
        var saved=DruidFirestorm.Snapshot();DruidFirestorm.Restore(JsonSerializer.Deserialize<FirestormSave>(Text(saved)));DruidFirestorm.Tick(field.UntilUtc,true);Require(DruidFirestorm.Snapshot().Patches.Length==0,"Damage at zero remaining lifetime");
        var constant=field with{Min256=256,Max256=256,NextUtc=at.AddMinutes(1),UntilUtc=at.AddMinutes(1).AddMilliseconds(14*40)};
        var other=constant with{Id=constant.Id+1};DruidFirestorm.Restore(new([], [constant,other],other.Id));before=SkillHealth.Balance256(a)+SkillHealth.Balance256(b);DruidFirestorm.Tick(constant.NextUtc,true);
        Require(before-SkillHealth.Balance256(a)-SkillHealth.Balance256(b)==512,"Independent overlapping fields were throttled");
        a.SetResistance(ResistanceType.Fire,100);b.SetResistance(ResistanceType.Fire,100);Require(a.FireResistance==100 && b.FireResistance==100,"Real fire immunity controls");
        constant=constant with{NextUtc=at.AddMinutes(2),UntilUtc=at.AddMinutes(2).AddMilliseconds(14*40)};DruidFirestorm.Restore(new([], [constant],constant.Id));before=SkillHealth.Balance256(a)+SkillHealth.Balance256(b);DruidFirestorm.Tick(constant.NextUtc,true);
        Require(before-SkillHealth.Balance256(a)-SkillHealth.Balance256(b)==0,"Pure fire bypassed immunity");
        Require(CombatPotions.Apply(p,3),"Actual spell potion");var mods=DruidFirestorm.Modifiers(p);Require(mods.Potion==10,"Actual spell potion capture");
        var potion=constant with{Min256=2560,Max256=2560,Modifiers=mods,NextUtc=at.AddMinutes(3),UntilUtc=at.AddMinutes(3).AddMilliseconds(14*40)};DruidFirestorm.Restore(new([], [potion],potion.Id));before=SkillHealth.Balance256(a)+SkillHealth.Balance256(b);DruidFirestorm.Tick(potion.NextUtc,true);
        Require(before-SkillHealth.Balance256(a)-SkillHealth.Balance256(b)==3*256,"Approved nonfire potion components lost against fire immunity");
        DruidFirestorm.Cancel(p);a.Delete();b.Delete();p.Delete();
    }
    private static void WallsAndReductions()
    {
        var p=Fresh();var target=Target(p,6);var wall=new Static(0x80);wall.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);
        try
        {
            var at=TimedEffects.Now.AddDays(1);Launch(p,at);var straight=DruidFirestorm.Snapshot().Emitters.Single(x=>!x.Charged);DruidFirestorm.Restore(new([straight],[],straight.Id));
            int life=SkillHealth.Balance256(target);DruidFirestorm.Tick(at.AddSeconds(3),true);Require(DruidFirestorm.Snapshot() is{Emitters.Length:0,Patches.Length:0} && SkillHealth.Balance256(target)==life,"Firestorm crossed wall");
        }
        finally{wall.Delete();target.Delete();}
        var ring=NamedGear.Create("unique/274",99,new Random(173),true);Require(p.EquipItem(ring),"Real Dwarf Star MDR");ClassGear.Refresh(p);Require(GemCombat.Reduction(p)>0,"Actual MDR supplier");
        var raw=new GearDamage(0,40,40,40,0);var legacy=SkillCombat.MitigatePrepared(p,p,raw);var explicitZero=SkillCombat.MitigatePrepared(p,p,raw,damageRate:0);var field=SkillCombat.MitigatePrepared(p,p,raw,damageRate:41);
        Require(legacy==explicitZero && field.Cold>legacy.Cold && field.Lightning>legacy.Lightning && field.Fire>legacy.Fire,"Per-frame MDR rate/default backward compatibility");p.Delete();
    }
    private static void Live()
    {
        var held=DruidFirestorm.Snapshot();var p=Fresh();var target=Target(p);var network=NetState.Instances.Single(x=>x.Mobile==null && IPAddress.IsLoopback(x.Address));network.Seeded=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var protocol=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);Require(protocol!=null,"Protocol bootstrap");protocol.SetValue(network,Enum.Parse(protocol.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        int life=SkillHealth.Balance256(target);Launch(p,TimedEffects.Now);bool moved=false,burned=false,damaged=false;long deadline=Core.TickCount+6000;
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Natural emitter/field timeout");var state=DruidFirestorm.Snapshot();var emitters=state.Emitters.Where(x=>x.Owner==p.Serial.Value).ToArray();var patches=state.Patches.Where(x=>x.Owner==p.Serial.Value).ToArray();
                moved|=emitters.Any(x=>x.Frame>0 && (x.X!=p.X || x.Y!=p.Y));burned|=patches.Length>0;damaged|=SkillHealth.Balance256(target)<life;
                if(emitters.Length>0 || patches.Length>0){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Inspect);return;}
                Require(moved && burned && damaged,"Natural timer did not move/burn/damage");DruidFirestorm.Cancel(p);target.Delete();p.Delete();var current=DruidFirestorm.Snapshot();Require(Text(current with{NextId=held.NextId})==Text(held),"Natural fixture changed held owner state");
                var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var heldOwner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var heldTarget=World.FindMobile((Serial)saved.Target);Require(SkillHealth.Balance256(heldTarget)==saved.Life256,"Natural fixture changed held target life");Save(heldOwner,heldTarget);Log.Information("firestorm-live-result: passed");
            }
            catch(Exception e){DruidFirestorm.Cancel(p);target.Delete();p.Delete();Log.Error(e,"firestorm-live-result: failed");}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),Inspect);
    }
    private static void OfflineAndLifecycle()
    {
        var p=Fresh();var a=Target(p);var at=TimedEffects.Now.AddDays(1);Launch(p,at);DruidFirestorm.Tick(at.AddMilliseconds(120),true);int life=SkillHealth.Balance256(a);DruidFirestorm.Tick(at.AddSeconds(3),false);
        Require(DruidFirestorm.Snapshot() is{Emitters.Length:0,Patches.Length:0} && SkillHealth.Balance256(a)==life,"Offline damage/lifetime replay");
        Launch(p,at);p.MoveToWorld(new(400,400,0),Map.Felucca);DruidFirestorm.Tick(at.AddSeconds(3),true);Require(DruidFirestorm.Snapshot() is{Emitters.Length:0,Patches.Length:0},"Cross-facet player projectiles retained");a.Delete();p.Delete();
    }
    private static void WorkerAndMalformed()
    {
        var before=DruidFirestorm.Snapshot();string text=Text(before);var data=Task.Run(()=>{var writer=new BufferWriter(new byte[2*1024*1024],true);DruidFirestorm.Profiles.Serialize(writer);return writer.Buffer[..(int)writer.Position];}).GetAwaiter().GetResult();var reader=new BufferReader(data);
        Require(reader.ReadInt()==0 && reader.ReadString()==text && Text(DruidFirestorm.Snapshot())==text,"Worker mutated Firestorm state");
        var e=before.Emitters.First();foreach(var bad in new[]{e with{Frame=40},e with{X=double.NaN},e with{Rank=0},e with{CellX=e.CellX+1}})
        {bool rejected=false;try{DruidFirestorm.Restore(before with{Emitters=before.Emitters.Where(x=>x.Id!=e.Id).Append(bad).ToArray()});}catch(InvalidDataException){rejected=true;}Require(rejected && Text(DruidFirestorm.Snapshot())==text,"Invalid restore changed live state");}
    }
    private static void Save(PlayerMobile p,Mobile target)
    {File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,target.Serial.Value,SkillHealth.Balance256(target),DruidFirestorm.Snapshot())));}
    private static void Check()
    {
        Require(DruidFirestorm.Snapshot() is{Emitters.Length:0,Patches.Length:0},"Unexpected existing Firestorm fixtures");Formula();MotionAndCapture();FieldsAndDefense();WallsAndReductions();OfflineAndLifecycle();
        var p=Fresh();Train(p,CombatAbility.Firestorm);var target=Target(p);var at=TimedEffects.Now.AddDays(2);Launch(p,at);DruidFirestorm.Tick(at.AddMilliseconds(6*40),true);Require(DruidFirestorm.Snapshot() is{Emitters.Length:3,Patches.Length:>0},"Saved active emitter/patch setup");WorkerAndMalformed();Save(p,target);
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=World.FindMobile((Serial)saved.Owner) as PlayerMobile;var target=World.FindMobile((Serial)saved.Target);
        Require(p!=null && target!=null && Text(DruidFirestorm.Snapshot())==Text(saved.State) && SkillHealth.Balance256(target)==saved.Life256,"Exact world emitter/patch/seed/life reload");WorkerAndMalformed();
        var before=DruidFirestorm.Snapshot();DruidFirestorm.Tick(Next(),true);var after=DruidFirestorm.Snapshot();Require(after.Emitters.Length==3 && after.Emitters.All(e=>e.Frame==before.Emitters.Single(x=>x.Id==e.Id).Frame+1),"Saved path did not resume");
        Save(p,target);
    }
}
