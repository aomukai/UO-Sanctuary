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

public static class PlayerFrozenOrbChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(PlayerFrozenOrbChecks));
    private sealed record Saved(uint Owner,uint First,uint Second,uint Orb,int Mana,int Life,int FirstLife,int SecondLife,int Map,Point3D Point,FrozenMissile[] Missiles,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"player-frozen-orb-fixtures.json");
    public static void Configure()
    {
        var prior=ClassGearHooks.CanRegenerateMana;ClassGearHooks.CanRegenerateMana=p=>p.Name!="PlayerFrozenOrbFixture"&&prior?.Invoke(p)!=false;
        Register("player-frozen-orb-check",Check);Register("player-frozen-orb-reload-check",()=>FixtureClock.NextLoop("player-frozen-orb-reload-result",()=>FixtureNetwork.Connected("player-frozen-orb-reload-result",Reload)));
        foreach(string mode in new[]{"left","right","quick"}){string label="player-frozen-orb-"+mode;Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,label)));}
    }
    [CallPriority(int.MaxValue)]public static void Initialize(){var prior=ClassGearHooks.CanRegenerateHits;ClassGearHooks.CanRegenerateHits=p=>p.Name!="PlayerFrozenOrbFixture"&&prior?.Invoke(p)!=false;}
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Prepared Frozen Orb,source ordering/cold and original saved flight",_=>Core.LoopContext.Post(()=>{try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {
        var p=FireBoltChecks.Owner(CombatAbility.IceBolt,6510);p.Name="PlayerFrozenOrbFixture";
        foreach(var a in new[]{CombatAbility.FrostNova,CombatAbility.IceBlast,CombatAbility.GlacialSpike,CombatAbility.Blizzard,CombatAbility.FrozenOrb}){EnchantCastChecks.Train(p,a,1);}
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.FrozenOrb)&&SkillBindings.Instance.Assign(p,false,CombatAbility.FrozenOrb)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.FrozenOrb),"Orb left/right/quick bindings");return p;
    }
    private static FrozenMissile[] Own(PlayerMobile p)=>FrozenOrbs.Snapshot().Where(m=>m.Owner==p.Serial.Value).OrderBy(m=>m.Id).ToArray();
    private static void Replace(PlayerMobile p,params FrozenMissile[] states)=>FrozenOrbs.Restore(FrozenOrbs.Snapshot().Where(x=>x.Owner!=p.Serial.Value).Concat(states).ToArray());
    private static void Clean(PlayerMobile p,params Mobile[] others){FrozenOrbs.Cancel(p);FireBoltChecks.Clean(p,others);}
    private static void Check(){Source();Admission();WorldChecks();Contacts();Save();Log.Information("Player Frozen Orb:9180native endpoints/9216shared source update comparisons,prepared live release/25frame delay,30trail/16terminal/turn ordering,typed cold/immunity/potion/zero root/offline/load/legacy and original saved progression pass");}
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"frozen-orb-damage-source-cases.json")));int count=0;
        foreach(var r in doc.RootElement.EnumerateArray())
        {
            int I(int n)=>r[0][n].GetInt32();if(I(0)==0){continue;}var row=SkillMechanics.For(CombatAbility.FrozenOrb);var v=new SkillValues(row,k=>k==row.SourceKey?I(1):255,k=>k=="Ice Bolt"?I(2):0,99);var e=DruidFirestorm.Endpoints(v,I(3),SlowMissilePayload.Normal);
            Require(e.Min==r[2][4].GetInt32()&&e.Max==r[2][5].GetInt32()&&v.DurationFrames==200+(I(1)-1)*25&&v.Mana256==(49+I(1))*128,"Complete-source Frozen Orb endpoints/length/cost");count++;
        }
        Require(count==9180,"Incomplete Orb endpoint scope");using var collisions=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"frozen-orb-collision-source-cases.json")));count=0;
        foreach(var r in collisions.RootElement.GetProperty("contexts").EnumerateArray())
        {
            int I(int n)=>r[0][n].GetInt32();int kind=I(0),remaining=I(1),moves=0,writes=0,finds=0,fills=0,history=I(6),x=40,bursts=0;
            uint target=I(7)==3&&history!=10?10u:history!=20?20u:0;
            int Hit(uint id,bool end){if(kind==0&&remaining==0){bursts+=16;}if(!end&&id!=0){history=(int)id;fills++;return kind==0?1:2;}return 2;}
            var result=BasicMissileUpdate.Run(new(true,true,1,remaining,2,kind==0?30:25,true),new(()=>{moves++;if(I(2)==1){x++;}return I(2);},v=>{remaining=v;writes++;},()=>I(4)!=0,()=>true,()=>I(3),()=>[new(40,70)],_=>I(5)&0x184,_=>{finds++;return target;},Hit));
            Require(new[]{result.Result,remaining,moves,finds,fills,fills,history,writes,x}.SequenceEqual(r[1].EnumerateArray().Take(9).Select(v=>v.GetInt32())),"Complete source update/return trace");
            Require(bursts==r[2].EnumerateArray().Count(q=>q[1].GetInt32()==262),"Exact-zero terminal gate");count++;
        }
        Require(count==9216,"Incomplete Orb update scope");PlayerFrozenOrbs.Route();
    }
    private static void Admission()
    {
        var p=Owner();var t=FireBoltChecks.Target(p,4);try
        {
            int mana=NecromancerEffects.ManaBalance256(p);Require(ClassCombat.ContinueCast(p,CombatAbility.FrozenOrb,t,t.Location,Core.TickCount),"Orb windup");long at=ClassCombat.For(p).EffectAt;Require(Own(p).Length==0&&NecromancerEffects.ManaBalance256(p)==mana,"Orb paid/emitted before release");ClassCombat.Stop(p);Require(!ClassCombat.ResolveAction(p,at),"Cancelled Orb fired");
            NecromancerEffects.SetMana256(p,6399);Require(!SorceressCombat.Release(p,CombatAbility.FrozenOrb,t.Serial,t.Location)&&Own(p).Length==0&&NecromancerEffects.ManaBalance256(p)==6399,"Insufficient Orb emitted/paid");NecromancerEffects.SetMana256(p,mana);
            Require(ClassCombat.ContinueCast(p,CombatAbility.FrozenOrb,t,t.Location,Math.Max(Core.TickCount,ClassCombat.For(p).NextAction)),"Second Orb windup");at=ClassCombat.For(p).EffectAt;t.MoveToWorld(new(p.X+8,p.Y+2,p.Z),p.Map);EnchantCastChecks.Train(p,CombatAbility.FrozenOrb,2);
            Require(ClassCombat.ResolveAction(p,at)&&mana-NecromancerEffects.ManaBalance256(p)==6528&&Own(p).Single().Rank==2&&Own(p).Single().Vx==MissileVectors.VelocityFp8(8,2,1920).X&&Own(p).Single().Vy==MissileVectors.VelocityFp8(8,2,1920).Y,"Orb live selected aim/rank/fractional mana");mana=NecromancerEffects.ManaBalance256(p);Require(!SorceressCombat.Release(p,CombatAbility.FrozenOrb,t.Serial,t.Location)&&NecromancerEffects.ManaBalance256(p)==mana,"Orb localdelay payment");var c=FireGround.Snapshot().Cooldowns.Single(x=>x.Owner==p.Serial.Value&&x.Ability==CombatAbility.FrozenOrb);Require((c.UntilUtc-TimedEffects.Now).TotalMilliseconds is >900 and <=1000,"Orb25frame delay");p.Internalize();Require(!SorceressCombat.Release(p,CombatAbility.FrozenOrb,Serial.Zero,t.Location)&&NecromancerEffects.ManaBalance256(p)==mana,"Internal Orb paid");
        }
        finally{ClassCombat.Stop(p);Clean(p,t);}
    }
    private static void WorldChecks()
    {
        var p=Owner();try
        {
            var gear=PoisonDaggerChecks.Named(p,"Eschuta's temper");FrozenOrbs.Cast(p,new(p.X+12,p.Y,p.Z),SkillMechanics.Values(p,CombatAbility.FrozenOrb));var root=Own(p).Single();Require(root.Source is{VelocityFp8:1920}&&root.Frame==0&&root.DirectionIndex==0&&root.VectorX==0&&root.Min256==0&&root.ColdFrames==0,"Fresh source root state");p.Backpack.DropItem(gear);ClassGear.Refresh(p);EnchantCastChecks.Train(p,CombatAbility.IceBolt,20);FrozenOrbs.Advance(p,root.NextUtc,true);var states=Own(p);var bolt=states.Single(x=>x.Kind==FrozenKind.Bolt);var current=SkillMechanics.Values(p,CombatAbility.FrozenOrb,root.Rank);var mods=DruidFirestorm.Modifiers(p);var ends=DruidFirestorm.Endpoints(current,mods.ColdMastery,SlowMissilePayload.Normal);
            Require(bolt.X==root.X&&bolt.Y==root.Y&&bolt.Rank==root.Rank&&bolt.VectorX==0&&bolt.VectorY==0&&bolt.Min256==ends.Min&&bolt.Max256==ends.Max&&bolt.Source.Modifiers==mods&&states.Single(x=>x.Kind==FrozenKind.Orb).DirectionIndex==19,"Pre-movement emission/live child capture/fresh stored vector");
            FrozenOrbs.Cancel(p);int births=0;var seed=Blazes.SeedLow;try{Blazes.SeedLow=()=>{births++;return 1;};FrozenOrbs.Cast(p,new(p.X+12,p.Y,p.Z),SkillMechanics.Values(p,CombatAbility.FrozenOrb));root=Own(p).Single();FrozenOrbs.Advance(p,root.UntilUtc,true);states=Own(p);Require(births==47&&states.All(x=>x.Kind!=FrozenKind.Orb)&&states.Count(x=>x.Kind==FrozenKind.Nova)==16&&states.Where(x=>x.Kind==FrozenKind.Nova).All(x=>x.X==Math.Floor((root.X+30*root.Vx)*2+.5)/2&&x.Frame==0),"30trail/16terminal/final moved coordinate");}finally{Blazes.SeedLow=seed;}
            var curve=states.First(x=>x.Kind==FrozenKind.Nova&&x.DirectionIndex==0);Replace(p,curve);FrozenOrbs.Advance(p,curve.NextUtc,true);Require(Own(p).Single() is{Frame:1,VectorX:15,VectorY:15},"First nova turn");FrozenOrbs.Advance(p,curve.NextUtc.AddMilliseconds(80),true);Require(Own(p).Single() is{Frame:3,VectorX:0,VectorY:15},"Second nova turn");FrozenOrbs.Advance(p,curve.NextUtc.AddMilliseconds(160),true);Require(Own(p).Single() is{Frame:5,VectorX:-7,VectorY:7},"Signed third nova turn");FrozenOrbs.Advance(p,curve.UntilUtc,true);Require(Own(p).Length==0,"Independent nova expiry");
            FrozenOrbs.Cast(p,p.Location,SkillMechanics.Values(p,CombatAbility.FrozenOrb));Require(Own(p).Single().Vx>0&&Own(p).Single().Vy>0,"Self aim factory nudge");FrozenOrbs.Cancel(p);
            FrozenOrbs.Cast(p,new(p.X+12,p.Y,p.Z),SkillMechanics.Values(p,CombatAbility.FrozenOrb));root=Own(p).Single();FrozenOrbs.Advance(p,root.NextUtc.AddMilliseconds(200),false);Require(Own(p).Single().Frame==6&&Own(p).Single().X==root.X&&Own(p).Single().Source.Seed==root.Source.Seed,"Offline replay/emission/RNG");FrozenOrbs.Advance(p,root.UntilUtc,false);Require(Own(p).Length==0,"Offline root burst/reset");
            FrozenOrbs.Cast(p,new(p.X+12,p.Y,p.Z),SkillMechanics.Values(p,CombatAbility.FrozenOrb));root=Own(p).Single();var missedAt=TimedEffects.Now.AddMilliseconds(-80);Replace(p,root with{NextUtc=missedAt,UntilUtc=missedAt+(root.UntilUtc-root.NextUtc)});FrozenOrbs.Restore(FrozenOrbs.Snapshot(),true);FrozenOrbs.Advance(p,TimedEffects.Now,true);Require(Own(p).Length==1&&Own(p)[0].Frame>0&&Own(p)[0].X==root.X&&Own(p)[0].Source.Seed==root.Source.Seed,"Loaded root replayed");
            var legacy=root with{Source=null};Replace(p,legacy);var json=JsonSerializer.Serialize(FrozenOrbs.Snapshot());FrozenOrbs.Restore(JsonSerializer.Deserialize<FrozenMissile[]>(json));Require(Own(p).Single()==legacy,"Legacy orb reconstructed");bool refused=false;try{Replace(p,root with{Source=root.Source with{VelocityFp8=1}});}catch(InvalidDataException){refused=true;}Require(refused&&Own(p).Single()==legacy,"Orb restore was not atomic");
        }
        finally{Clean(p);}
    }
    private static FrozenMissile Bolt(PlayerMobile p,Mobile t,int damage=100)
    {
        var at=TimedEffects.Now;double x=t.X-.421875;long id=FrozenOrbs.Snapshot().Select(x=>x.Id).DefaultIfEmpty().Max()+1;
        return new(id,p.Serial.Value,1,p.Map.MapID,FrozenKind.Bolt,x,t.Y,t.Z,.421875,0,0,0,0,0,damage*256,damage*256,200,0,at.AddMilliseconds(1000),at.AddMilliseconds(40))
            {Source=new(3456,new([new((int)Math.Floor(x*2+.5),t.Y*2)]),DruidFirestorm.Modifiers(p),SourceRandom.InitLow(1))};
    }
    private static void Contacts()
    {
        var p=Owner();var a=FireBoltChecks.Target(p,4);var b=FireBoltChecks.Target(p,7);try
        {
            a.Frozen=false;a.CantWalk=true;a.SetHits(400);SkillHealth.SetBalance256(a,400*256);var shot=Bolt(p,a);Replace(p,shot);int far=SkillHealth.Balance256(b);FrozenOrbs.Advance(p,shot.NextUtc,true);Require(Own(p).Length==0&&SkillHealth.Balance256(a)==300*256&&SkillHealth.Balance256(b)==far&&CreatureHitRecovery.Remaining(a)>0&&GearEffects.ChillPercent(a)>0,"Typed child contact/cold/get-hit/nonpierce");TimedEffects.Clear(a);a.Frozen=true;
            var root=PlayerFrozenOrbs.Prepare(p,a.Location,SkillMechanics.Values(p,CombatAbility.FrozenOrb),TimedEffects.Now);root=root with{X=a.X-.234375,Source=root.Source with{Steps=new([new(a.X*2,a.Y*2)])}};FrozenOrbs.Commit(root);var stored=Own(p).Single();int life=SkillHealth.Balance256(a);FrozenOrbs.Advance(p,stored.NextUtc,true);Require(SkillHealth.Balance256(a)==life&&GearEffects.ChillPercent(a)==0&&Own(p).Single(x=>x.Kind==FrozenKind.Orb).LastCollision==a.Serial.Value,"Root zero payload/history");
            FrozenOrbs.Cancel(p);a.SetHits(50000);SkillHealth.SetBalance256(a,50000*256);a.SetResistance(ResistanceType.Cold,100);shot=Bolt(p,a);Replace(p,shot);life=SkillHealth.Balance256(a);FrozenOrbs.Advance(p,shot.NextUtc,true);Require(SkillHealth.Balance256(a)==life,"Orb child immunity");Require(CombatPotions.Apply(p,3),"Orb potion");shot=Bolt(p,a);Replace(p,shot);FrozenOrbs.Advance(p,shot.NextUtc,true);Require(SkillHealth.Balance256(a)<life,"Live separate potion lanes");TimedEffects.Clear(p);life=SkillHealth.Balance256(a);shot=Bolt(p,a);Replace(p,shot);FrozenOrbs.Advance(p,shot.NextUtc,true);Require(SkillHealth.Balance256(a)==life,"Cleared potion retained");a.SetResistance(ResistanceType.Cold,0);SkillHealth.SetBalance256(a,50*256);shot=Bolt(p,a);Replace(p,shot);FrozenOrbs.Advance(p,shot.NextUtc,true);Require(!a.Alive,"Orb child lethal ordered delivery");
        }
        finally{Clean(p,a,b);}
    }
    private static void Hold(PlayerMobile p)
    {var at=TimedEffects.Now.Add(FixtureSpace.Retention);Replace(p,Own(p).Select(x=>x with{NextUtc=at,UntilUtc=at+(x.UntilUtc-x.NextUtc)}).ToArray());}
    private static Saved Capture(PlayerMobile p,BaseCreature a,BaseCreature b,Item orb,int gen,int map,Point3D point)=>new(p.Serial.Value,a.Serial.Value,b.Serial.Value,orb.Serial.Value,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(p),SkillHealth.Balance256(a),SkillHealth.Balance256(b),map,point,Own(p),gen);
    private static void Save()
    {
        var p=Owner();EnchantCastChecks.Train(p,CombatAbility.FrozenOrb,20);var orb=PoisonDaggerChecks.Named(p,"Eschuta's temper");FrozenOrbs.Cast(p,new(p.X+12,p.Y,p.Z),SkillMechanics.Values(p,CombatAbility.FrozenOrb));FrozenOrbs.Advance(p,Own(p).Single().UntilUtc,true);var curve=Own(p).First(x=>x.Kind==FrozenKind.Nova&&x.DirectionIndex==0);Replace(p,curve);
        Require(SorceressCombat.Release(p,CombatAbility.FrozenOrb,Serial.Zero,new(p.X+12,p.Y,p.Z)),"Saved learned Orb");var root=Own(p).Single(x=>x.Kind==FrozenKind.Orb);FrozenOrbs.Advance(p,root.NextUtc,true);root=Own(p).Single(x=>x.Kind==FrozenKind.Orb);Replace(p,root,curve);
        var a=FireBoltChecks.Target(p,1);a.MoveToWorld(new(p.X,p.Y+1,p.Z),p.Map);var b=FireBoltChecks.Target(p,1);b.MoveToWorld(new(p.X+1,p.Y-1,p.Z),p.Map);Hold(p);SkillHealth.Damage(p,p,5*256+17);var saved=Capture(p,a,b,orb,0,p.Map.MapID,p.Location);p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var a=(BaseCreature)World.FindMobile((Serial)s.First);var b=(BaseCreature)World.FindMobile((Serial)s.Second);var orb=World.FindItem((Serial)s.Orb);
        Require(s.Generation<2&&orb?.Parent==p&&Own(p).SequenceEqual(s.Missiles)&&NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillHealth.Balance256(p)==s.Life&&SkillHealth.Balance256(a)==s.FirstLife&&SkillHealth.Balance256(b)==s.SecondLife,"Original Orb actors/gear/state/wounds/resources changed");p.MoveToWorld(s.Point,Map.Maps[s.Map]);a.AIObject.AITimer.Stop();b.AIObject.AITimer.Stop();var account=FixtureNetwork.Authenticate(network,p,"orb-reload-");var at=TimedEffects.Now.AddMilliseconds(40);Replace(p,s.Missiles.Select(x=>x with{NextUtc=at,UntilUtc=at+(x.UntilUtc-x.NextUtc)}).ToArray());long deadline=Core.TickCount+5000;
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline,"Original saved Orb natural timeout");var states=Own(p);if(SkillHealth.Balance256(a)+SkillHealth.Balance256(b)>=s.FirstLife+s.SecondLife){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(s.Missiles.All(old=>states.Any(x=>x.Id==old.Id&&x.Frame>old.Frame&&x.Min256==old.Min256&&x.Source.Modifiers==old.Source.Modifiers&&x.Source.Seed==old.Source.Seed))&&states.Any(x=>s.Missiles.All(old=>old.Id!=x.Id))&&NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillHealth.Balance256(p)==s.Life,"Saved original root/curve reset payload/frame/seed/resources or did not emit");Hold(p);p.Internalize();network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();File.WriteAllText(Manifest,JsonSerializer.Serialize(Capture(p,a,b,orb,s.Generation+1,s.Map,s.Point)));Log.Information("player-frozen-orb-reload-result: passed");
            }
            catch(Exception e){Log.Error(e,"player-frozen-orb-reload-result: failed");}
        }
        Observe();
    }
    private static void Input(NetState network,string label)
    {
        var p=Owner();var t=FireBoltChecks.Target(p,4);var account=FixtureNetwork.Authenticate(network,p,"orb-input-");int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(t);bool began=false;long deadline=Core.TickCount+7000;
        void Finish(){ClassCombat.Stop(p);Clean(p,t);account.Delete();}
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline,"Orb wire/natural source timeout");if(ClassCombat.For(p).PendingAbility==CombatAbility.FrozenOrb&&!began){Require(Own(p).Length==0&&NecromancerEffects.ManaBalance256(p)==mana,"Wire Orb paid in windup");began=true;Log.Information("{Label}: windup",label);}
                if(SkillHealth.Balance256(t)>=life){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}Require(began&&NecromancerEffects.ManaBalance256(p)==mana-6400&&Own(p).Any(x=>x.Kind==FrozenKind.Orb&&x.Source!=null),"Wire Orb payment/natural source");Finish();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Finish();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.FrozenOrb,(int)DiabloClass.Sorceress,t.Serial.Value,t.X,t.Y,t.Z);Observe();});
    }
}
