// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class BlazeChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BlazeChecks));
    private sealed record Saved(uint Owner,uint Target,uint Orb,int Mana,int Life,int TargetLife,int Map,Point3D Point,BlazeState Aura,FirePatch[] Fields,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"blaze-fixtures.json");
    public static void Configure()
    {
        var prior=ClassGearHooks.CanRegenerateMana;ClassGearHooks.CanRegenerateMana=p=>p.Name!="BlazeFixture"&&prior?.Invoke(p)!=false;
        Register("blaze-check",Check);Register("blaze-reload-check",()=>FixtureClock.NextLoop("blaze-reload-result",()=>FixtureNetwork.Connected("blaze-reload-result",Reload)));
        foreach(bool quick in new[]{false,true}){string label=quick?"blaze-quick":"blaze-right";Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,label)));}
    }
    [CallPriority(int.MaxValue)]
    public static void Initialize(){var prior=ClassGearHooks.CanRegenerateHits;ClassGearHooks.CanRegenerateHits=p=>p.Name!="BlazeFixture"&&prior?.Invoke(p)!=false;}
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Blaze prepared cast,source field damage and saved natural resumption",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {
        var p=FireBoltChecks.Owner(CombatAbility.FireBolt,5870);p.Name="BlazeFixture";Require(!Server.Spells.SpellHelper.IsTown(p.Location,p),"Blaze fixture must be outside town");
        foreach(var a in new[]{CombatAbility.Inferno,CombatAbility.Blaze,CombatAbility.Warmth}){EnchantCastChecks.Train(p,a,1);}
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.Blaze)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Blaze)&&!SkillBindings.Instance.Assign(p,true,CombatAbility.Blaze),"Blaze right/quick only");return p;
    }
    private static void Clean(PlayerMobile p,params Mobile[] others){FireGround.Cancel(p);FireBoltChecks.Clean(p,others);}
    private static FirePatch[] Fields(PlayerMobile p)=>FireGround.Snapshot().Patches.Where(x=>x.Owner==p.Serial.Value).OrderBy(x=>x.Id).ToArray();
    private static void Replace(FirePatch[] fields)
    {
        var all=FireGround.Snapshot();FireGround.Restore(all with{Patches=all.Patches.Select(p=>fields.FirstOrDefault(x=>x.Id==p.Id)??p).ToArray()});
    }
    private static void Check()
    {
        Goldens();Source();Admission();FieldsAndPotion();Save();
        Log.Information("Blaze:765rank/hard goldens,23040complete-source rolls/120native HP-return comparisons,prepared right-only fractional mana,retained rank/live emission modifiers,one-recipient typed fields,offline expiry and original saved fields pass");
    }
    private static void Goldens()
    {
        var row=SkillMechanics.For(CombatAbility.Blaze);
        for(int rank=1;rank<=255;rank++)foreach(int hard in new[]{0,1,20})
        {
            int Tier(int basis,int[] steps){int left=rank-1;int[] lengths=[7,8,6,6,255];for(int i=0;i<5;i++){int n=Math.Min(left,lengths[i]);basis+=n*steps[i];left-=n;}return basis;}
            var v=new SkillValues(row,k=>k==row.SourceKey?rank:255,k=>k=="Warmth"?hard:0,99);
            Require(v.Mana256==(21+rank)*128&&v.Eval("passivecalc1")==2*rank&&v.Eval("auralencalc")==50+450*Math.Min(100,110*rank/(rank+6))/100
                &&v.Damage256(false)==(long)Tier(4,[3,5,7,9,11])*16*(100+6*hard)/100&&v.Damage256(true)==(long)Tier(8,[3,6,8,10,12])*16*(100+6*hard)/100
                &&NecromancerSummons.Formula("blaze","Range",v)==90+25*rank,"Independent Blaze formula/lifetime curve");
        }
    }
    private static void Source()
    {
        var p=Owner();int rolls=0,native=0;try
        {
            using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"blaze-damage-source-cases.json")));
            foreach(var r in doc.RootElement.EnumerateArray())
            {
                int I(int i)=>r[0][i].GetInt32();int O(int i)=>r[1][i].GetInt32();ulong seed=SourceRandom.InitLow(I(1));var packet=Blazes.Roll(I(0)*256,(I(0)+1)*256,ref seed);
                Require(packet.Fire256==O(0)&&packet.ResultFlags==(1|O(1))&&packet.HitClass==32&&seed==r[1][8].GetUInt64(),"Complete-source fire/field03 roll/flags/seed");rolls++;
                if(I(3)!=0||I(4)!=0||I(1) is not(0 or 1 or 2 or 123)){continue;}
                var t=FireBoltChecks.Target(p,1);try
                {
                    t.SetResistance(ResistanceType.Fire,I(2));SkillHealth.SetBalance256(t,I(5));NativeMonsterReturn.Session(t).LastLifePercent=128;
                    var result=Blazes.Deliver(p,t,packet,DruidFirestorm.Modifiers(p),TimedEffects.Now);var state=NativeMonsterReturn.Session(t);
                    Require(result.AfterHealth256==O(3)&&result.Packet.Total256==O(2)&&result.Packet.ResultFlags==O(4)&&state.LastHitClass==32&&state.LastMode==0
                        &&result.DeathModeAttempted==(O(10)!=0)&&state.RecoveryNotifications==O(11)&&state.Refreshes==O(12),"Complete-source field HP/return comparison");native++;
                }
                finally{TimedEffects.Clear(t);t.Corpse?.Delete();t.Delete();}
            }
            Require(rolls==23040&&native==120,"Incomplete Blaze source/native scope");
        }
        finally{Clean(p);}
    }
    private static void Admission()
    {
        var p=Owner();try
        {
            int mana=NecromancerEffects.ManaBalance256(p);Require(ClassCombat.ContinueCast(p,CombatAbility.Blaze,null,p.Location,Core.TickCount),"Blaze windup");long at=ClassCombat.For(p).EffectAt;
            Require(NecromancerEffects.ManaBalance256(p)==mana&&FireGround.Blaze(p)==null,"Blaze paid in windup");ClassCombat.Stop(p);
            Require(!ClassCombat.ResolveAction(p,at)&&NecromancerEffects.ManaBalance256(p)==mana,"Cancelled Blaze paid");
            NecromancerEffects.SetMana256(p,2815);Require(!SorceressCombat.Release(p,CombatAbility.Blaze,Serial.Zero,p.Location)&&FireGround.Blaze(p)==null&&NecromancerEffects.ManaBalance256(p)==2815,"Insufficient Blaze paid/applied");
            NecromancerEffects.SetMana256(p,mana);Require(ClassCombat.ContinueCast(p,CombatAbility.Blaze,null,p.Location,Math.Max(Core.TickCount,ClassCombat.For(p).NextAction)),"Second Blaze windup");at=ClassCombat.For(p).EffectAt;EnchantCastChecks.Train(p,CombatAbility.Blaze,2);
            Require(ClassCombat.ResolveAction(p,at)&&mana-NecromancerEffects.ManaBalance256(p)==2944&&FireGround.Blaze(p) is{Rank:2,Speed:4}&&Fields(p).Length==0,"Release fractional mana/current rank or idle emission");
            ClassCombat.Stop(p);p.Internalize();mana=NecromancerEffects.ManaBalance256(p);Require(!SorceressCombat.Release(p,CombatAbility.Blaze,Serial.Zero,p.Location)&&NecromancerEffects.ManaBalance256(p)==mana,"Internal Blaze paid");
            FixtureSpace.Corridor(p,5870,32,3000);p.Kill();mana=NecromancerEffects.ManaBalance256(p);Require(!SorceressCombat.Release(p,CombatAbility.Blaze,Serial.Zero,p.Location)&&NecromancerEffects.ManaBalance256(p)==mana,"Dead Blaze paid");
        }
        finally{ClassCombat.Stop(p);Clean(p);}
    }
    private static void FieldsAndPotion()
    {
        var p=Owner();var a=FireBoltChecks.Target(p,0);var b=FireBoltChecks.Target(p,0);try
        {
            var orb=PoisonDaggerChecks.Named(p,"Eschuta's temper");Require(SorceressCombat.Release(p,CombatAbility.Blaze,Serial.Zero,p.Location),"Gear Blaze cast");var aura=FireGround.Blaze(p);
            p.Direction=Direction.East;Require(p.Move(Direction.East)&&Fields(p).Length==2,"Committed movement/subtile bridge");var first=Fields(p)[0];
            p.Backpack.DropItem(orb);ClassGear.Refresh(p);EnchantCastChecks.Train(p,CombatAbility.Warmth,20);int mana=NecromancerEffects.ManaBalance256(p);
            Require(!SorceressCombat.Release(p,CombatAbility.Blaze,Serial.Zero,p.Location)&&NecromancerEffects.ManaBalance256(p)==mana&&FireGround.Blaze(p)==aura with{X=p.X,Y=p.Y,Z=p.Z},"Weaker Blaze recast paid/changed retained rank");
            Require(p.Move(Direction.East)&&Fields(p).Length==4,"Second committed movement");var newer=Fields(p).Last();var values=SkillMechanics.Values(p,CombatAbility.Blaze,aura.Rank);
            var ends=DruidFirestorm.Endpoints(values,NamedElements.MasteryPercent(p,"fire"),AmazonSlowMissiles.Capture(p,true));
            Require(newer.SkillRank==aura.Rank&&newer.Min256==ends.Min&&newer.Max256==ends.Max&&Fields(p).First()==first,"Retained rank/live emission versus captured field");
            var all=FireGround.Snapshot();FireGround.Restore(all with{Patches=all.Patches.Where(x=>x.Owner!=p.Serial.Value||x.Id==first.Id).ToArray()});
            int la=SkillHealth.Balance256(a),lb=SkillHealth.Balance256(b);FireGround.AdvanceBlaze(p,first.NextUtc,true);
            Require((SkillHealth.Balance256(a)<la?1:0)+(SkillHealth.Balance256(b)<lb?1:0)==1,"Stationary field hit multiple eligible recipients");
            var after=Fields(p).Single();la=SkillHealth.Balance256(a);lb=SkillHealth.Balance256(b);var position=p.Location;var map=p.Map;p.Internalize();FireGround.AdvanceBlaze(p,after.NextUtc.AddMilliseconds(200),false);var offline=Fields(p).Single();
            Require(offline.Seed==after.Seed&&offline.UntilUtc==after.UntilUtc&&offline.NextUtc>after.NextUtc&&SkillHealth.Balance256(a)==la&&SkillHealth.Balance256(b)==lb,"Offline field replayed RNG/damage or extended life");p.MoveToWorld(position,map);
            SorceressEffects.Profiles.Store(p,SorceressEffects.Profiles.Find(p) with{Blaze=aura with{UntilUtc=TimedEffects.Now.AddMilliseconds(-1)}});
            Require(FireGround.Blaze(p)==null&&Fields(p).Length==1,"Aura expiry removed emitted field");
            int total=SkillHealth.Balance256(a)+SkillHealth.Balance256(b);FireGround.AdvanceBlaze(p,offline.NextUtc,true);Require(SkillHealth.Balance256(a)+SkillHealth.Balance256(b)<total,"Emitted field depended on active aura");
            var end=Fields(p).Single();int life=SkillHealth.Balance256(a)+SkillHealth.Balance256(b);Replace([end with{NextUtc=end.UntilUtc}]);FireGround.AdvanceBlaze(p,end.UntilUtc,true);
            Require(Fields(p).Length==0&&SkillHealth.Balance256(a)+SkillHealth.Balance256(b)==life,"Final do5 frame damaged instead of expiring");
            var legacy=first with{BlazeModifiers=null,SkillRank=0,Seed=0,NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention),UntilUtc=TimedEffects.Now.Add(FixtureSpace.Retention).AddSeconds(5)};
            Require(!JsonSerializer.Serialize(legacy).Contains("BlazeModifiers"),"Optional V0 property was written for legacy field");
            all=FireGround.Snapshot();FireGround.Restore(JsonSerializer.Deserialize<FireGroundSave>(JsonSerializer.Serialize(all with{Patches=all.Patches.Append(legacy).ToArray()})));
            Require(Fields(p).Single()==legacy,"Legacy rankless field was reconstructed or lost");FireGround.Cancel(p);
            var captured=DruidFirestorm.Modifiers(p);Require(CombatPotions.Apply(p,3),"Impact potion");a.SetResistance(ResistanceType.Fire,100);int bonus=CombatPotions.Bonus(p,"magical-penetration");
            var packet=new PreparedMonsterDamagePacket(0,2560,0,0,1,32,true,Fire256:2560);var hit=Blazes.Deliver(p,a,packet,captured,TimedEffects.Now);
            Require(hit.Packet.Total256==3*2560*bonus/100&&hit.Packet.Cold256>0&&hit.Packet.Lightning256>0&&hit.Packet.Poison256>0,"Live typed potion components against fire immunity");
            TimedEffects.Clear(p);Require(Blazes.Deliver(p,a,packet,captured,TimedEffects.Now).Packet.Total256==0,"Expired potion remained baked into field fire");
        }
        finally{Clean(p,a,b);}
    }
    private static FirePatch[] Hold(FirePatch[] fields)
    {
        var at=TimedEffects.Now.Add(FixtureSpace.Retention);return fields.Select(f=>f with{NextUtc=at,UntilUtc=at+(f.UntilUtc-f.NextUtc)}).ToArray();
    }
    private static void Save()
    {
        var p=Owner();EnchantCastChecks.Train(p,CombatAbility.Blaze,20);EnchantCastChecks.Train(p,CombatAbility.Warmth,20);var orb=PoisonDaggerChecks.Named(p,"Eschuta's temper");
        Require(SorceressCombat.Release(p,CombatAbility.Blaze,Serial.Zero,p.Location),"Saved real Blaze cast");var target=FireBoltChecks.Target(p,0);p.Direction=Direction.East;Require(p.Move(Direction.East),"Saved native Blaze movement");
        var fields=Fields(p);Require(fields.Length==2&&fields.All(f=>f.BlazeModifiers!=null&&f.SkillRank>20),"Saved original fields");var held=Hold(fields);Replace(held);SkillHealth.Damage(p,p,5*256+17);
        var saved=new Saved(p.Serial.Value,target.Serial.Value,orb.Serial.Value,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(p),SkillHealth.Balance256(target),p.Map.MapID,p.Location,SorceressEffects.Profiles.Find(p).Blaze,held,0);
        p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var target=(BaseCreature)World.FindMobile((Serial)s.Target);
        Require(s.Generation<2&&World.FindItem((Serial)s.Orb)?.Parent==p&&Fields(p).SequenceEqual(s.Fields)&&SorceressEffects.Profiles.Find(p)?.Blaze==s.Aura&&NecromancerEffects.ManaBalance256(p)==s.Mana
            &&SkillHealth.Balance256(p)==s.Life&&SkillHealth.Balance256(target)==s.TargetLife,"Original Blaze actors/gear/fields/resources changed");
        p.MoveToWorld(s.Point,Map.Maps[s.Map]);target.AIObject.AITimer.Stop();var account=FixtureNetwork.Authenticate(network,p,"blaze-reload-");var now=TimedEffects.Now.AddMilliseconds(40);
        Replace(s.Fields.Select(f=>f with{NextUtc=now,UntilUtc=now+(f.UntilUtc-f.NextUtc)}).ToArray());long deadline=Core.TickCount+5000;
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline,"Saved natural Blaze resumption timeout");if(SkillHealth.Balance256(target)>=s.TargetLife){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                var fields=Fields(p);Require(fields.Length==s.Fields.Length&&fields.All(f=>s.Fields.Any(old=>old.Id==f.Id&&old.X==f.X&&old.Y==f.Y&&old.Min256==f.Min256&&old.Max256==f.Max256&&old.BlazeModifiers==f.BlazeModifiers))
                    &&fields.Any(f=>f.Seed!=s.Fields.Single(x=>x.Id==f.Id).Seed)&&NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillHealth.Balance256(p)==s.Life,"Saved field reset packet/position/resources or failed seed advancement");
                var held=Hold(fields);Replace(held);p.Internalize();network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();
                File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Fields=held,TargetLife=SkillHealth.Balance256(target),Generation=s.Generation+1}));Log.Information("blaze-reload-result: passed");
            }
            catch(Exception e){Log.Error(e,"blaze-reload-result: failed");}
        }
        Observe();
    }
    private static void Input(NetState network,string label)
    {
        var p=Owner();var target=FireBoltChecks.Target(p,0);var account=FixtureNetwork.Authenticate(network,p,"blaze-input-");int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(target);bool began=false,moved=false;long deadline=Core.TickCount+5000;
        void Finish(){ClassCombat.Stop(p);Clean(p,target);account.Delete();}
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline,$"Blaze input/natural field timeout: seq={ClassCombat.For(p).Sequence} right={ClassCombat.For(p).Right} pending={ClassCombat.For(p).PendingAbility} alive={p.Alive} frozen={p.Frozen} paralyzed={p.Paralyzed} allowed={AlliedOverlap.Allowed(p)} next={ClassCombat.For(p).NextAction} now={Core.TickCount} available={ClassCombat.Available(p,CombatAbility.Blaze)} valid={SorceressCombat.ValidTarget(p,CombatAbility.Blaze,Serial.Zero,p.Location)}");
                if(ClassCombat.For(p).PendingAbility==CombatAbility.Blaze&&!began){Require(NecromancerEffects.ManaBalance256(p)==mana&&FireGround.Blaze(p)==null,"Input paid during windup");began=true;Log.Information("{Label}: windup",label);}
                if(FireGround.Blaze(p)==null){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(began&&FireGround.Blaze(p).Rank==1&&NecromancerEffects.ManaBalance256(p)==mana-2816,"Actual Blaze input rank/once cost");
                if(!moved){Require(Fields(p).Length==0,"Idle Blaze emitted");p.Direction=Direction.East;Require(p.Move(Direction.East)&&Fields(p).Length==2,"Native movement failed");moved=true;}
                if(SkillHealth.Balance256(target)>=life){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(NativeMonsterReturn.Session(target).LastMode==0&&NativeMonsterReturn.Session(target).LastHitClass==32,"Field entered generic get-hit mode");Finish();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Finish();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target=0 x={X} y={Y} z={Z}",label,(int)CombatAbility.Blaze,(int)DiabloClass.Sorceress,p.X,p.Y,p.Z);Observe();});
    }
}
