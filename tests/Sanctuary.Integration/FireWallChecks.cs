// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class FireWallChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(FireWallChecks));
    private sealed record Saved(uint Owner,uint Target,uint Orb,int Mana,int Life,int TargetLife,int Map,Point3D Point,FireMaker[] Makers,FirePatch[] Fields,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"fire-wall-fixtures.json");
    public static void Configure()
    {
        var prior=ClassGearHooks.CanRegenerateMana;ClassGearHooks.CanRegenerateMana=p=>p.Name!="FireWallFixture"&&prior?.Invoke(p)!=false;
        Register("fire-wall-check",Check);Register("fire-wall-reload-check",()=>FixtureClock.NextLoop("fire-wall-reload-result",()=>FixtureNetwork.Connected("fire-wall-reload-result",Reload)));
        foreach(string mode in new[]{"right","quick"}){string label="fire-wall-"+mode;Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,label)));}
    }
    [CallPriority(int.MaxValue)]public static void Initialize(){var prior=ClassGearHooks.CanRegenerateHits;ClassGearHooks.CanRegenerateHits=p=>p.Name!="FireWallFixture"&&prior?.Invoke(p)!=false;}
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Fire Wall prepared cast,typed fields and saved maker progression",_=>Core.LoopContext.Post(()=>{try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {
        var p=FireBoltChecks.Owner(CombatAbility.FireBolt,6070);p.Name="FireWallFixture";
        foreach(var a in new[]{CombatAbility.Inferno,CombatAbility.Blaze,CombatAbility.Warmth,CombatAbility.FireWall}){EnchantCastChecks.Train(p,a,1);}
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.FireWall)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.FireWall)&&!SkillBindings.Instance.Assign(p,true,CombatAbility.FireWall),"Fire Wall right/quick only");
        Require(!Server.Spells.SpellHelper.IsTown(p.Location,p),"Fire Wall fixture in town");return p;
    }
    private static FirePatch[] Fields(PlayerMobile p)=>FireGround.Snapshot().Patches.Where(x=>x.Owner==p.Serial.Value).OrderBy(x=>x.Id).ToArray();
    private static FireMaker[] Makers(PlayerMobile p)=>FireGround.Snapshot().Makers.Where(x=>x.Owner==p.Serial.Value).OrderBy(x=>x.Id).ToArray();
    private static void Replace(PlayerMobile p,FireMaker[] makers,FirePatch[] fields)
    {var all=FireGround.Snapshot();FireGround.Restore(all with{Makers=all.Makers.Where(x=>x.Owner!=p.Serial.Value).Concat(makers).ToArray(),Patches=all.Patches.Where(x=>x.Owner!=p.Serial.Value).Concat(fields).ToArray()});}
    private static void Clean(PlayerMobile p,params Mobile[] others){FireGround.Cancel(p);FireBoltChecks.Clean(p,others);}
    private static void Check(){Source();Admission();WorldChecks();Save();Log.Information("Fire Wall:13770source field endpoints,2400accepted source cast plans/5760cast contexts,840shared maker cases,18native rate41/MDR mitigation comparisons,prepared release/live aim/35frame delay,previous-cell/final-frame emission,typed fields/legacy and original save pass");}
    private static void Source()
    {
        using var values=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"fire-wall-damage-source-cases.json")));
        var row=SkillMechanics.For(CombatAbility.FireWall);int compared=0;
        foreach(var r in values.RootElement.EnumerateArray())
        {
            if(r[0][0].GetInt32()==0){continue;}int rank=r[0][1].GetInt32(),w=r[0][2].GetInt32(),i=r[0][3].GetInt32(),mastery=r[0][4].GetInt32();
            var v=new SkillValues(row,k=>k==row.SourceKey?rank:255,k=>k=="Warmth"?w:k=="Inferno"?i:0,99);var e=DruidFirestorm.Endpoints(v,mastery,SlowMissilePayload.Normal);
            Require(e.Min==r[2][2].GetInt32()&&e.Max==r[2][3].GetInt32()&&v.Mana256==(21+rank)*256&&NecromancerSummons.Formula("firewallmaker","Range",v)==7+2*rank,"Complete-source field endpoints/cost/lifetime");compared++;
        }
        Require(compared==13770,"Incomplete field endpoint scope");
        using var creation=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"fire-wall-creation-source-cases.json")));int casts=0,plans=0,makers=0;
        foreach(var r in creation.RootElement.GetProperty("cast").EnumerateArray())
        {
            casts++;if(r[1][0].GetInt32()==0){continue;}plans++;var requests=r[2].EnumerateArray().ToArray();var a=requests[0];int x=a[2].GetInt32(),y=a[3].GetInt32();
            // Recover the caster's fixed40/70 coordinates from the independent source fixture.
            int center=requests.Length==2?-1:requests[2][1].GetInt32();var plan=FireWalls.Requests(40,70,x,y,r[0][2].GetInt32(),center);
            Require(plan.Length==requests.Length,"Source cast request count");for(int j=0;j<plan.Length;j++){var q=plan[j];var e=requests[j];Require(new[]{q.Flags,q.Missile,q.X,q.Y,q.AimX,q.AimY,51,q.Rank}.SequenceEqual(e.EnumerateArray().Take(8).Select(x=>x.GetInt32())),"Source cast request order/geometry");}
        }
        foreach(var r in creation.RootElement.GetProperty("maker").EnumerateArray())
        {
            int g=r[0][0].GetInt32(),flags=r[0][1].GetInt32(),rank=r[0][3].GetInt32(),end=r[0][4].GetInt32();var plan=MoltenBoulderPackets.Trail(g!=2,g!=3,g!=1,g!=5,g==4?-1:g==6?0:69,flags,50,60,rank,end);
            Require(plan.Result==r[1][0].GetInt32()&&plan.Requests.Length==r[2].GetArrayLength(),"Reused complete do6 callback");makers++;
        }
        Require(casts==5760&&plans==2400&&makers==840,"Incomplete creation scope");Mdr();
    }
    private sealed class Maximum:Random{public override int Next(int min,int max)=>max-1;public override int Next(int max)=>max-1;public override double NextDouble()=>.999999;}
    private static void Mdr()
    {
        var p=Owner();int count=0;try
        {
            using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"fire-wall-return-source-cases.json")));
            foreach(var r in doc.RootElement.EnumerateArray())
            {
                int I(int n)=>r[0][n].GetInt32();if(I(3)!=18||I(4)!=41||I(5)!=25600||I(1) is not(0 or 1)||I(2) is not(0 or 50 or 100)){continue;}
                var t=new SkeletonGuardian{Name="Fire Wall MDR storage [fixture]",NoKillAwards=true,Frozen=true,CantWalk=true};t.Initialize(p,2,0);t.ControlSlots=0;t.Summoned=true;Require(t.SetControlMaster(p),"MDR owner");t.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();t.SetHits(50000);
                Item item=null;try
                {
                    foreach(var d in NamedGear.Catalog.Items.Where(d=>NamedGear.Available(d.Id)&&d.Properties.Any(x=>x.Code=="red-mag")))
                    {var candidate=NamedGear.Create(d.Id,99,new Maximum(),true);candidate.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);if(IronGolemItems.Eligible(candidate,out _)&&new ItemStatContext(candidate,99).Bonus("magic-reduction")==18){item=candidate;break;}candidate.Delete();}
                    Require(item!=null,"No real supported MDR original");item.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);Require(IronGolemItems.TryAttach(p,t,item,out _),"MDR binding");
                    // Compare the actual item value; choose the18MDR original deterministically.
                    Require(GemCombat.Reduction(t)==18,"MDR fixture must use18 from a real original item");t.SetResistance(ResistanceType.Fire,0);int offset=t.FireResistance;t.SetResistance(ResistanceType.Fire,I(2)-offset);Require(t.FireResistance==I(2),"Actual MDR recipient fire resistance");SkillHealth.SetBalance256(t,I(5));NativeMonsterReturn.Session(t).LastLifePercent=128;
                    ulong seed=SourceRandom.InitLow(I(1));var packet=SourceFireFields.Roll(I(0)*256,(I(0)+1)*256,ref seed);
                    // This existing holder overrides Damage; its ordered HP/event adapter
                    // is intentionally not claimed. Compare the real shared numeric
                    // mitigation service against source; natural NPC HP is separate.
                    int before=SkillHealth.Balance256(t);
                    var service=new InstantSpellNpcDamage(p,t,new(-1,-1,0,0,false,"Unused mitigation identity"),DruidFirestorm.Modifiers(p),TimedEffects.Now,"MDR numeric",()=>0,damageRate:41);
                    var hit=service.MitigateMissile(packet);
                    Require(hit.Total256==r[1][2].GetInt32()&&SkillHealth.Balance256(t)==before,"Source rate41/real MDR numeric mitigation");count++;
                }
                finally{IronGolemItems.DestroyBoundItem(t);item?.Delete();t.Corpse?.Delete();t.Delete();}
            }
            Require(count==18,"Incomplete MDR native scope");
        }
        finally{Clean(p);}
    }
    private static void Admission()
    {
        var p=Owner();var target=FireBoltChecks.Target(p,4);try
        {
            int mana=NecromancerEffects.ManaBalance256(p);Require(ClassCombat.ContinueCast(p,CombatAbility.FireWall,target,target.Location,Core.TickCount),"Wall windup");long at=ClassCombat.For(p).EffectAt;Require(Fields(p).Length==0&&Makers(p).Length==0&&NecromancerEffects.ManaBalance256(p)==mana,"Wall paid/emitted before release");ClassCombat.Stop(p);Require(!ClassCombat.ResolveAction(p,at),"Cancelled wall resolved");
            NecromancerEffects.SetMana256(p,5631);Require(!SorceressCombat.Release(p,CombatAbility.FireWall,target.Serial,target.Location)&&Fields(p).Length==0&&Makers(p).Length==0&&NecromancerEffects.ManaBalance256(p)==5631,"Insufficient wall paid/emitted");NecromancerEffects.SetMana256(p,mana);
            Require(ClassCombat.ContinueCast(p,CombatAbility.FireWall,target,target.Location,Math.Max(Core.TickCount,ClassCombat.For(p).NextAction)),"Second wall windup");at=ClassCombat.For(p).EffectAt;target.MoveToWorld(new(p.X+6,p.Y,p.Z),p.Map);EnchantCastChecks.Train(p,CombatAbility.FireWall,2);
            Require(ClassCombat.ResolveAction(p,at)&&mana-NecromancerEffects.ManaBalance256(p)==5888&&Fields(p).Single().X==target.X&&Makers(p).All(m=>m.Rank==2),"Live release aim/rank/cost");
            mana=NecromancerEffects.ManaBalance256(p);Require(!SorceressCombat.Release(p,CombatAbility.FireWall,Serial.Zero,target.Location)&&NecromancerEffects.ManaBalance256(p)==mana,"Local delay repeat paid");var cooldown=FireGround.Snapshot().Cooldowns.Single(c=>c.Owner==p.Serial.Value&&c.Ability==CombatAbility.FireWall);Require((cooldown.UntilUtc-TimedEffects.Now).TotalMilliseconds is >1300 and <=1400,"Wall35frame delay");
            p.Internalize();Require(!SorceressCombat.Release(p,CombatAbility.FireWall,Serial.Zero,target.Location)&&NecromancerEffects.ManaBalance256(p)==mana,"Internal wall paid");
        }
        finally{ClassCombat.Stop(p);Clean(p,target);}
    }
    private static void WorldChecks()
    {
        var p=Owner();var a=FireBoltChecks.Target(p,4);var b=FireBoltChecks.Target(p,4);try
        {
            var orb=PoisonDaggerChecks.Named(p,"Eschuta's temper");var values=SkillMechanics.Values(p,CombatAbility.FireWall);var at=TimedEffects.Now;FireGround.CastWall(p,a.Location,values);
            var original=Fields(p).Single();var initial=Makers(p);Require(initial.Length==2&&initial.All(m=>m.Motion is{VelocityFp8:2304,Frame:0,CellChanged:false}&&m.Min256==0&&m.Max256==0)&&initial[1].Id<original.Id,"Maker-first source factory order/zero packet");
            int life=SkillHealth.Balance256(a)+SkillHealth.Balance256(b);FireGround.AdvanceWall(p,initial[0].NextUtc,true);Require(Fields(p).Length==1&&SkillHealth.Balance256(a)+SkillHealth.Balance256(b)<life,"First frame emitted a new child or field failed");
            var moved=Makers(p);p.Backpack.DropItem(orb);ClassGear.Refresh(p);EnchantCastChecks.Train(p,CombatAbility.Warmth,20);FireGround.AdvanceWall(p,moved[0].NextUtc,true);
            var newer=Fields(p).Where(f=>f.Id!=original.Id).ToArray();var current=SkillMechanics.Values(p,CombatAbility.FireWall,values.Rank);var ends=DruidFirestorm.Endpoints(current,NamedElements.MasteryPercent(p,"fire"),AmazonSlowMissiles.Capture(p,false));
            Require(newer.Length==2&&newer.All(f=>f.SkillRank==values.Rank&&f.Min256==ends.Min&&f.Max256==ends.Max)&&Fields(p).Single(f=>f.Id==original.Id).Min256==original.Min256,"Child retained rank/live hard/gear or recaptured old field");
            var m=Makers(p)[0];Require(newer.Any(f=>f.X==moved[0].CellX/2.0&&f.Y==moved[0].CellY/2.0),"Child emitted after movement rather than previous cell");
            // Isolate a final-frame maker: do6 emits before the terminal service.
            var last=m with{Motion=m.Motion with{Frame=m.Motion.Frames-1,CellChanged=true},UntilUtc=m.NextUtc};Replace(p,[last],[]);FireGround.AdvanceWall(p,last.NextUtc,true);
            Require(Makers(p).Length==0&&Fields(p).Length==1&&Fields(p)[0].X==last.CellX/2.0,"Terminal maker lost its preceding child");
            var field=Fields(p).Single();int hp=SkillHealth.Balance256(a)+SkillHealth.Balance256(b);Replace(p,[],[field with{NextUtc=field.UntilUtc}]);FireGround.AdvanceWall(p,field.UntilUtc,true);Require(Fields(p).Length==0&&SkillHealth.Balance256(a)+SkillHealth.Balance256(b)==hp,"Final field frame damaged");
            FireGround.CastWall(p,p.Location,SkillMechanics.Values(p,CombatAbility.FireWall));Require(Makers(p).All(x=>x.Dx>0&&x.Dy>0)&&Makers(p)[0].Dx==Makers(p)[1].Dx,"Older factory self-aim nudge differs");
            var old=Makers(p)[0] with{Motion=null,Min256=111,Max256=222,Dx=0,Dy=1};var legacy=Fields(p)[0] with{FireWallModifiers=null,SkillRank=0,Seed=0};Replace(p,[old],[legacy]);var json=JsonSerializer.Serialize(FireGround.Snapshot());FireGround.Restore(JsonSerializer.Deserialize<FireGroundSave>(json));Require(Makers(p).Single()==old&&Fields(p).Single()==legacy,"Legacy maker/field was reconstructed");
            FireGround.Cancel(p);b.Internalize();a.SetResistance(ResistanceType.Fire,100);FireGround.CastWall(p,a.Location,SkillMechanics.Values(p,CombatAbility.FireWall));
            var immune=Fields(p).Single();Replace(p,[],[immune]);int immuneLife=SkillHealth.Balance256(a);FireGround.AdvanceWall(p,immune.NextUtc,true);Require(SkillHealth.Balance256(a)==immuneLife,"Fire immunity did not reject base field");
            Require(CombatPotions.Apply(p,3),"Live field potion");FireGround.AdvanceWall(p,Fields(p).Single().NextUtc,true);Require(SkillHealth.Balance256(a)<immuneLife,"Potion's separate elements did not cross fire immunity");
            TimedEffects.Clear(p);immuneLife=SkillHealth.Balance256(a);FireGround.AdvanceWall(p,Fields(p).Single().NextUtc,true);Require(SkillHealth.Balance256(a)==immuneLife,"Potion additions remained captured after clearing");
            FireGround.Cancel(p);FireGround.CastWall(p,a.Location,SkillMechanics.Values(p,CombatAbility.FireWall));var beforeFields=Fields(p);var beforeMakers=Makers(p);var map=p.Map;var position=p.Location;immuneLife=SkillHealth.Balance256(a);
            p.Internalize();FireGround.AdvanceWall(p,beforeMakers[0].NextUtc.AddMilliseconds(400),false);
            Require(Fields(p).Length==1&&Fields(p)[0].Seed==beforeFields[0].Seed&&Makers(p).All(m=>m.Motion.Frame>0&&!m.Motion.CellChanged)&&SkillHealth.Balance256(a)==immuneLife,"Offline maker/field replay or RNG");p.MoveToWorld(position,map);
            FireGround.Cancel(p);FireGround.CastWall(p,a.Location,SkillMechanics.Values(p,CombatAbility.FireWall));var disk=FireGround.Snapshot();beforeFields=Fields(p);beforeMakers=Makers(p);FireGround.Restore(disk,true);FireGround.AdvanceWall(p,beforeMakers[0].NextUtc.AddMilliseconds(200),true);
            Require(Fields(p).Length==1&&Fields(p)[0].Seed==beforeFields[0].Seed&&Makers(p).All(m=>m.Motion.Frame>0&&!m.Motion.CellChanged),"Disk load replayed missed actions");

        }
        finally{Clean(p,a,b);}
    }
    private static void Hold(PlayerMobile p)
    {
        var at=TimedEffects.Now.Add(FixtureSpace.Retention);Replace(p,Makers(p).Select(m=>m with{NextUtc=at,UntilUtc=at+(m.UntilUtc-m.NextUtc)}).ToArray(),Fields(p).Select(f=>f with{NextUtc=at,UntilUtc=at+(f.UntilUtc-f.NextUtc)}).ToArray());
    }
    private static Saved Capture(PlayerMobile p,BaseCreature target,Item orb,int generation,int map,Point3D point)=>new(p.Serial.Value,target.Serial.Value,orb.Serial.Value,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(p),SkillHealth.Balance256(target),map,point,Makers(p),Fields(p),generation);
    private static void Save()
    {
        var p=Owner();EnchantCastChecks.Train(p,CombatAbility.FireWall,20);EnchantCastChecks.Train(p,CombatAbility.Warmth,20);var orb=PoisonDaggerChecks.Named(p,"Eschuta's temper");var target=FireBoltChecks.Target(p,4);
        Require(SorceressCombat.Release(p,CombatAbility.FireWall,target.Serial,target.Location),"Saved real wall cast");FireGround.AdvanceWall(p,Makers(p)[0].NextUtc,true);Require(Makers(p).Length==2&&Fields(p).Length==1,"Saved partial makers");Hold(p);SkillHealth.Damage(p,p,5*256+17);
        var s=Capture(p,target,orb,0,p.Map.MapID,p.Location);p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(s));
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var target=(BaseCreature)World.FindMobile((Serial)s.Target);var orb=World.FindItem((Serial)s.Orb);
        Require(s.Generation<2&&orb?.Parent==p&&Makers(p).SequenceEqual(s.Makers)&&Fields(p).SequenceEqual(s.Fields)&&NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillHealth.Balance256(p)==s.Life&&SkillHealth.Balance256(target)==s.TargetLife,"Original wall actors/gear/state/resources changed");
        p.MoveToWorld(s.Point,Map.Maps[s.Map]);target.AIObject.AITimer.Stop();var account=FixtureNetwork.Authenticate(network,p,"fire-wall-reload-");var at=TimedEffects.Now.AddMilliseconds(40);
        Replace(p,s.Makers.Select(m=>m with{NextUtc=at,UntilUtc=at+(m.UntilUtc-m.NextUtc)}).ToArray(),s.Fields.Select(f=>f with{NextUtc=at,UntilUtc=at+(f.UntilUtc-f.NextUtc)}).ToArray());long deadline=Core.TickCount+5000;
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline,"Saved natural wall progression timeout");var makers=Makers(p);var fields=Fields(p);
                if(SkillHealth.Balance256(target)>=s.TargetLife||fields.Length<=s.Fields.Length){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(makers.Length==2&&makers.All(m=>s.Makers.Any(old=>old.Id==m.Id&&m.Motion.Frame>old.Motion.Frame))&&s.Fields.All(old=>fields.Any(f=>f.Id==old.Id&&f.Min256==old.Min256&&f.FireWallModifiers==old.FireWallModifiers))&&s.Fields.Any(old=>fields.Any(f=>f.Id==old.Id&&f.Seed!=old.Seed))&&NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillHealth.Balance256(p)==s.Life,"Saved makers reset or fields/resources recaptured");
                Hold(p);p.Internalize();network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();File.WriteAllText(Manifest,JsonSerializer.Serialize(Capture(p,target,orb,s.Generation+1,s.Map,s.Point)));Log.Information("fire-wall-reload-result: passed");
            }
            catch(Exception e){Log.Error(e,"fire-wall-reload-result: failed");}
        }
        Observe();
    }
    private static void Input(NetState network,string label)
    {
        var p=Owner();var target=FireBoltChecks.Target(p,4);var account=FixtureNetwork.Authenticate(network,p,"fire-wall-input-");int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(target);bool began=false;long deadline=Core.TickCount+5000;
        void Finish(){ClassCombat.Stop(p);Clean(p,target);account.Delete();}
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline,"Wall wire/natural field timeout");if(ClassCombat.For(p).PendingAbility==CombatAbility.FireWall&&!began){Require(NecromancerEffects.ManaBalance256(p)==mana&&Fields(p).Length==0,"Wire wall paid in windup");began=true;Log.Information("{Label}: windup",label);}
                if(SkillHealth.Balance256(target)>=life){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(began&&NecromancerEffects.ManaBalance256(p)==mana-5632&&Fields(p).Any(f=>f.FireWallModifiers!=null)&&NativeMonsterReturn.Session(target).LastMode==0,"Wire wall payment/typed field/recovery");Finish();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Finish();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.FireWall,(int)DiabloClass.Sorceress,target.Serial.Value,target.X,target.Y,target.Z);Observe();});
    }
}
