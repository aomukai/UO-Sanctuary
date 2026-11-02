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

public static class PlayerMeteorChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(PlayerMeteorChecks));
    private sealed record Saved(uint Owner,uint Target,uint Orb,int Mana,int Life,int TargetLife,int Map,Point3D Point,MeteorDrop[] Warnings,FirePatch[] Fields,long[] Completed,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"player-meteor-fixtures.json");
    public static void Configure()
    {
        var prior=ClassGearHooks.CanRegenerateMana;ClassGearHooks.CanRegenerateMana=p=>p.Name!="PlayerMeteorFixture"&&prior?.Invoke(p)!=false;
        Register("player-meteor-check",Check);Register("player-meteor-reload-check",()=>FixtureClock.NextLoop("player-meteor-reload-result",()=>FixtureNetwork.Connected("player-meteor-reload-result",Reload)));
        foreach(string mode in new[]{"right","quick"}){string label="player-meteor-"+mode;Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,label)));}
    }
    [CallPriority(int.MaxValue)]public static void Initialize(){var prior=ClassGearHooks.CanRegenerateHits;ClassGearHooks.CanRegenerateHits=p=>p.Name!="PlayerMeteorFixture"&&prior?.Invoke(p)!=false;}
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Prepared player Meteor,typed area/fields and original saved warnings",_=>Core.LoopContext.Post(()=>{try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {
        var p=FireBoltChecks.Owner(CombatAbility.FireBolt,6150);p.Name="PlayerMeteorFixture";
        foreach(var a in new[]{CombatAbility.Inferno,CombatAbility.Blaze,CombatAbility.Fireball,CombatAbility.FireWall,CombatAbility.Meteor}){EnchantCastChecks.Train(p,a,1);}
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.Meteor)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Meteor)&&!SkillBindings.Instance.Assign(p,true,CombatAbility.Meteor),"Meteor right/quick only");return p;
    }
    private static MeteorDrop[] Warnings(PlayerMobile p)=>FireGround.Snapshot().Meteors.Where(x=>x.Owner==p.Serial.Value).OrderBy(x=>x.Id).ToArray();
    private static FirePatch[] Fields(PlayerMobile p)=>FireGround.Snapshot().Patches.Where(x=>x.Owner==p.Serial.Value).OrderBy(x=>x.Id).ToArray();
    private static void Replace(PlayerMobile p,MeteorDrop[] warnings,FirePatch[] fields)
    {var all=FireGround.Snapshot();FireGround.Restore(all with{Meteors=all.Meteors.Where(x=>x.Owner!=p.Serial.Value).Concat(warnings).ToArray(),Patches=all.Patches.Where(x=>x.Owner!=p.Serial.Value).Concat(fields).ToArray()});}
    private static void Clean(PlayerMobile p,params Mobile[] others){FireGround.Cancel(p);FireBoltChecks.Clean(p,others);}
    private static void Check(){Source();Admission();WorldChecks();ReturnChecks();Save();Log.Information("Player Meteor:55080source endpoint comparisons,prepared moving-target release/fractional mana/30frame delay,60frame warning/copied impact/live18typed fields,immunity/potion/offline/load/legacy controls and original saved warnings pass");}
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"meteor-damage-source-cases.json")));int count=0;
        foreach(var r in doc.RootElement.EnumerateArray())
        {
            int I(int n)=>r[0][n].GetInt32();var row=SkillMechanics.For(CombatAbility.Meteor);var v=new SkillValues(row,k=>k==row.SourceKey?I(1):255,k=>k=="Fire Bolt"?I(2):k=="Fire Ball"?I(3):k=="Inferno"?I(4):0,99);
            var ends=I(0)==0?DruidFirestorm.Endpoints(v,I(5),SlowMissilePayload.Normal):DruidFirestorm.Endpoints(FireGround.MeteorFireDamage256(v,false),FireGround.MeteorFireDamage256(v,true),I(5),SlowMissilePayload.Normal);
            Require(ends.Min==r[2][2].GetInt32()&&ends.Max==r[2][3].GetInt32()&&v.Mana256==(33+I(1))*128,"Complete-source impact/field endpoints or fractional mana");count++;
        }
        Require(count==55080,"Incomplete endpoint comparison");Meteors.Route();
    }
    private static void Admission()
    {
        var p=Owner();var t=FireBoltChecks.Target(p,4);try
        {
            int mana=NecromancerEffects.ManaBalance256(p);Require(ClassCombat.ContinueCast(p,CombatAbility.Meteor,t,t.Location,Core.TickCount),"Meteor windup");long at=ClassCombat.For(p).EffectAt;
            Require(Warnings(p).Length==0&&NecromancerEffects.ManaBalance256(p)==mana,"Meteor paid before release");ClassCombat.Stop(p);Require(!ClassCombat.ResolveAction(p,at),"Cancelled Meteor fired");
            NecromancerEffects.SetMana256(p,4351);Require(!SorceressCombat.Release(p,CombatAbility.Meteor,t.Serial,t.Location)&&Warnings(p).Length==0&&NecromancerEffects.ManaBalance256(p)==4351,"Insufficient Meteor emitted/paid");NecromancerEffects.SetMana256(p,mana);
            Require(ClassCombat.ContinueCast(p,CombatAbility.Meteor,t,t.Location,Math.Max(Core.TickCount,ClassCombat.For(p).NextAction)),"Meteor second windup");at=ClassCombat.For(p).EffectAt;t.MoveToWorld(new(p.X+6,p.Y,p.Z),p.Map);EnchantCastChecks.Train(p,CombatAbility.Meteor,2);
            Require(ClassCombat.ResolveAction(p,at)&&mana-NecromancerEffects.ManaBalance256(p)==4480&&Warnings(p).Single().X==t.X&&Warnings(p).Single().Rank==2,"Live release target/rank/fractional mana");
            var m=Warnings(p).Single();t.MoveToWorld(new(p.X+10,p.Y,p.Z),p.Map);Require(Warnings(p).Single()==m,"Released warning followed moving target");mana=NecromancerEffects.ManaBalance256(p);
            Require(!SorceressCombat.Release(p,CombatAbility.Meteor,t.Serial,t.Location)&&NecromancerEffects.ManaBalance256(p)==mana,"Local delay recast paid");var c=FireGround.Snapshot().Cooldowns.Single(x=>x.Owner==p.Serial.Value&&x.Ability==CombatAbility.Meteor);Require((c.UntilUtc-TimedEffects.Now).TotalMilliseconds is >1100 and <=1200&&m.ImpactUtc>c.UntilUtc,"30frame delay confused with60frame fall");
            p.Internalize();Require(!SorceressCombat.Release(p,CombatAbility.Meteor,Serial.Zero,t.Location)&&NecromancerEffects.ManaBalance256(p)==mana,"Internal Meteor paid");
        }
        finally{ClassCombat.Stop(p);Clean(p,t);}
    }
    private static void WorldChecks()
    {
        var p=Owner();var a=FireBoltChecks.Target(p,6);var b=FireBoltChecks.Target(p,6);try
        {
            var orb=PoisonDaggerChecks.Named(p,"Eschuta's temper");FireGround.CastMeteor(p,a.Location,SkillMechanics.Values(p,CombatAbility.Meteor));var m=Warnings(p).Single();var map=p.Map;var pos=p.Location;
            p.Backpack.DropItem(orb);ClassGear.Refresh(p);EnchantCastChecks.Train(p,CombatAbility.Inferno,20);int life=SkillHealth.Balance256(a);
            FireGround.AdvanceMeteor(p,m.ImpactUtc.AddMilliseconds(-1),true);Require(SkillHealth.Balance256(a)==life&&Fields(p).Length==0,"Early Meteor impact");
            FireGround.AdvanceMeteor(p,m.ImpactUtc,true);var fields=Fields(p);var v=SkillMechanics.Values(p,CombatAbility.Meteor,m.Rank);var mods=DruidFirestorm.Modifiers(p);var ends=DruidFirestorm.Endpoints(FireGround.MeteorFireDamage256(v,false),FireGround.MeteorFireDamage256(v,true),mods.FireMastery,SlowMissilePayload.Normal);
            Require(Warnings(p).Length==0&&fields.Length==18&&SkillHealth.Balance256(a)<life&&SkillHealth.Balance256(a)==SkillHealth.Balance256(b)&&fields.All(f=>f.SkillRank==m.Rank&&f.MeteorModifiers==mods&&f.Min256==ends.Min&&f.Max256==ends.Max&&f.Size==2&&f.DamageRate==41&&f.UntilUtc==m.ImpactUtc.AddMilliseconds((30+15*(m.Rank-1))*40L)),"Copied impact/live child endpoints/rank/lifetime");
            int combined=SkillHealth.Balance256(a)+SkillHealth.Balance256(b);FireGround.AdvanceMeteor(p,m.ImpactUtc.AddMilliseconds(40),true);Require(SkillHealth.Balance256(a)+SkillHealth.Balance256(b)<combined,"Typed Meteor fields did not burn");
            FireGround.Cancel(p);b.Internalize();a.SetResistance(ResistanceType.Fire,100);FireGround.CastMeteor(p,a.Location,SkillMechanics.Values(p,CombatAbility.Meteor));m=Warnings(p).Single();life=SkillHealth.Balance256(a);FireGround.AdvanceMeteor(p,m.ImpactUtc,true);Require(SkillHealth.Balance256(a)==life,"Fire immunity failed");
            Require(CombatPotions.Apply(p,3),"Meteor potion");FireGround.AdvanceMeteor(p,m.ImpactUtc.AddMilliseconds(40),true);Require(SkillHealth.Balance256(a)<life,"Separate potion field elements failed on fire immune");TimedEffects.Clear(p);life=SkillHealth.Balance256(a);FireGround.AdvanceMeteor(p,m.ImpactUtc.AddMilliseconds(80),true);Require(SkillHealth.Balance256(a)==life,"Cleared potion persisted");
            FireGround.Cancel(p);FireGround.CastMeteor(p,a.Location,SkillMechanics.Values(p,CombatAbility.Meteor));m=Warnings(p).Single();p.Internalize();FireGround.AdvanceMeteor(p,m.ImpactUtc,false);Require(Warnings(p).Length==0&&Fields(p).Length==0&&SkillHealth.Balance256(a)==life,"Offline warning replayed");p.MoveToWorld(pos,map);
            FireGround.CastMeteor(p,a.Location,SkillMechanics.Values(p,CombatAbility.Meteor));m=Warnings(p).Single();Replace(p,[m with{ImpactUtc=TimedEffects.Now.AddSeconds(-1)}],[]);FireGround.Restore(FireGround.Snapshot(),true);FireGround.AdvanceMeteor(p,TimedEffects.Now,true);Require(Warnings(p).Length==0&&Fields(p).Length==0,"Missed disk warning replayed after connecting");
            FireGround.CastMeteor(p,a.Location,SkillMechanics.Values(p,CombatAbility.Meteor));m=Warnings(p).Single();FireGround.AdvanceMeteor(p,m.ImpactUtc,true);fields=Fields(p);FireGround.AdvanceMeteor(p,m.ImpactUtc.AddMilliseconds(100),false);Require(Fields(p).All(f=>fields.Any(old=>old.Id==f.Id&&old.Seed==f.Seed))&&SkillHealth.Balance256(a)==life,"Offline fields damaged or drew RNG");
            fields=Fields(p);FireGround.Restore(FireGround.Snapshot(),true);FireGround.AdvanceMeteor(p,fields.Min(x=>x.NextUtc).AddMilliseconds(100),true);Require(Fields(p).All(f=>fields.Any(old=>old.Id==f.Id&&old.Seed==f.Seed)),"Loaded fields replayed missed frames");
            var legacy=m with{MeteorModifiers=null,Seed=0};var oldField=fields[0] with{MeteorModifiers=null,SkillRank=0,Seed=0};Replace(p,[legacy],[oldField]);var json=JsonSerializer.Serialize(FireGround.Snapshot());FireGround.Restore(JsonSerializer.Deserialize<FireGroundSave>(json));Require(Warnings(p).Single()==legacy&&Fields(p).Single()==oldField,"Legacy payload reconstructed");
            bool refused=false;try{Replace(p,[legacy with{MeteorModifiers=mods,Rank=0}],[oldField]);}catch(InvalidDataException){refused=true;}Require(refused&&Warnings(p).Single()==legacy&&Fields(p).Single()==oldField,"Malformed restore was not atomic");
        }
        finally{Clean(p,a,b);}
    }
    private static void ReturnChecks()
    {
        var p=Owner();var t=FireBoltChecks.Target(p,6);try
        {
            t.Frozen=false;t.CantWalk=true;t.SetHits(400);SkillHealth.SetBalance256(t,400*256);var mods=DruidFirestorm.Modifiers(p);
            var m=Meteors.Prepare(p,t.Location,SkillMechanics.Values(p,CombatAbility.Meteor),TimedEffects.Now) with{Min256=200*256,Max256=200*256};
            FireGround.CommitMeteor(p,m);FireGround.AdvanceMeteor(p,Warnings(p).Single().ImpactUtc,true);
            Require(SkillHealth.Balance256(t)==200*256&&NativeMonsterReturn.Session(t).LastMode==PreparedMonsterReturn.GetHit&&CreatureHitRecovery.Remaining(t)>0,"Meteor impact failed ordinary ordered get-hit");
            var field=Fields(p).First(f=>ImmolationArrows.Recipient(p,f)==t);
            // SOFTHIT requests a recovery notification without entering get-hit
            // mode. Exercise both packet branches instead of depending on the
            // random field seed to omit SOFTHIT.
            foreach(bool soft in new[]{false,true})
            {
                ulong seed=0;PreparedMonsterDamagePacket packet;
                do{ulong roll=++seed;packet=SourceFireFields.Roll(field.Min256,field.Max256,ref roll);}while(((packet.ResultFlags&16384)!=0)!=soft);
                field=field with{Seed=seed};Replace(p,[],[field]);
                var state=NativeMonsterReturn.Session(t);int notifications=state.RecoveryNotifications,mode=state.LastMode;
                FireGround.AdvanceMeteor(p,field.NextUtc,true);
                Require(state.RecoveryNotifications==notifications+(soft?1:0)&&state.LastMode==mode,"Meteor field SOFTHIT notification/mode differs from source return");
                field=Fields(p).Single();
            }
            FireGround.Cancel(p);SkillHealth.SetBalance256(t,100*256);FireGround.CommitMeteor(p,m);FireGround.AdvanceMeteor(p,Warnings(p).Single().ImpactUtc,true);Require(!t.Alive,"Meteor lethal impact did not enter native death");
        }
        finally{Clean(p,t);}
    }
    private static void Hold(PlayerMobile p)
    {
        var at=TimedEffects.Now.Add(FixtureSpace.Retention);Replace(p,Warnings(p).Select(m=>m with{ImpactUtc=at}).ToArray(),Fields(p).Select(f=>f with{NextUtc=at,UntilUtc=at+(f.UntilUtc-f.NextUtc)}).ToArray());
    }
    private static Saved Capture(PlayerMobile p,BaseCreature target,Item orb,int generation,int map,Point3D point,long[] completed)=>new(p.Serial.Value,target.Serial.Value,orb.Serial.Value,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(p),SkillHealth.Balance256(target),map,point,Warnings(p),Fields(p),completed,generation);
    private static void Save()
    {
        var p=Owner();EnchantCastChecks.Train(p,CombatAbility.Meteor,20);var orb=PoisonDaggerChecks.Named(p,"Eschuta's temper");var t=FireBoltChecks.Target(p,6);
        FireGround.CastMeteor(p,t.Location,SkillMechanics.Values(p,CombatAbility.Meteor));FireGround.AdvanceMeteor(p,Warnings(p).Single().ImpactUtc,true);
        Require(SorceressCombat.Release(p,CombatAbility.Meteor,t.Serial,t.Location),"Saved learned Meteor");FireGround.CastMeteor(p,t.Location,SkillMechanics.Values(p,CombatAbility.Meteor));Require(Warnings(p).Length==2&&Fields(p).Length==18,"Saved warnings/fields");Hold(p);SkillHealth.Damage(p,p,5*256+17);
        var saved=Capture(p,t,orb,0,p.Map.MapID,p.Location,[]);p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var t=(BaseCreature)World.FindMobile((Serial)s.Target);var orb=World.FindItem((Serial)s.Orb);
        Require(s.Generation<2&&orb?.Parent==p&&Warnings(p).SequenceEqual(s.Warnings)&&Fields(p).SequenceEqual(s.Fields)&&NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillHealth.Balance256(p)==s.Life&&SkillHealth.Balance256(t)==s.TargetLife,"Original Meteor actors/gear/payload/wounds/resources changed");
        p.MoveToWorld(s.Point,Map.Maps[s.Map]);t.AIObject.AITimer.Stop();var account=FixtureNetwork.Authenticate(network,p,"player-meteor-reload-");var at=TimedEffects.Now;var current=s.Warnings[0];
        Replace(p,s.Warnings.Select(m=>m.Id==current.Id?m with{ImpactUtc=at.AddMilliseconds(200)}:m).ToArray(),s.Fields.Select(f=>f with{NextUtc=at.AddMilliseconds(40),UntilUtc=at.AddMilliseconds(40)+(f.UntilUtc-f.NextUtc)}).ToArray());long deadline=Core.TickCount+5000;
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline,"Saved natural Meteor timeout");var warnings=Warnings(p);var fields=Fields(p);
                if(warnings.Any(m=>m.Id==current.Id)||fields.Length<=s.Fields.Length){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(SkillHealth.Balance256(t)<s.TargetLife&&s.Fields.All(old=>fields.Any(f=>f.Id==old.Id&&f.Min256==old.Min256&&f.MeteorModifiers==old.MeteorModifiers))&&s.Fields.Any(old=>fields.Any(f=>f.Id==old.Id&&f.Seed!=old.Seed))&&NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillHealth.Balance256(p)==s.Life,"Original warning/field natural continuation reset payload or resources");
                Hold(p);p.Internalize();network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();File.WriteAllText(Manifest,JsonSerializer.Serialize(Capture(p,t,orb,s.Generation+1,s.Map,s.Point,s.Completed.Append(current.Id).ToArray())));Log.Information("player-meteor-reload-result: passed");
            }
            catch(Exception e){Log.Error(e,"player-meteor-reload-result: failed");}
        }
        Observe();
    }
    private static void Input(NetState network,string label)
    {
        var p=Owner();var t=FireBoltChecks.Target(p,6);var account=FixtureNetwork.Authenticate(network,p,"player-meteor-input-");int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(t);bool began=false,sawWarning=false;long deadline=Core.TickCount+7000;
        void Finish(){ClassCombat.Stop(p);Clean(p,t);account.Delete();}
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline,"Meteor wire/natural warning timeout");if(ClassCombat.For(p).PendingAbility==CombatAbility.Meteor&&!began){Require(NecromancerEffects.ManaBalance256(p)==mana&&Warnings(p).Length==0,"Wire Meteor paid in windup");began=true;Log.Information("{Label}: windup",label);}
                if(Warnings(p).Length==1){sawWarning=true;Require(SkillHealth.Balance256(t)==life,"Wire Meteor damaged during warning");}
                if(SkillHealth.Balance256(t)>=life){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(began&&sawWarning&&NecromancerEffects.ManaBalance256(p)==mana-4352&&Warnings(p).Length==0&&Fields(p).Length==18,"Wire Meteor payment/warning/impact/fields");Finish();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Finish();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.Meteor,(int)DiabloClass.Sorceress,t.Serial.Value,t.X,t.Y,t.Z);Observe();});
    }
}
