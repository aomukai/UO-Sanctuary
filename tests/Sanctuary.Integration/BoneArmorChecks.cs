// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class BoneArmorChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BoneArmorChecks));
    private sealed record Saved(uint Owner,Point3D Point,int Map,int Remaining256,BoneArmorState Pool,int ManaFraction,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"bone-armor-fixtures.json");
    public static void Configure()
    {
        Register("bone-armor-check",Check);Register("bone-armor-reload-check",Reload);
        foreach(bool quick in new[]{false,true})
        {bool selected=quick;Register(selected?"bone-armor-quick-check":"bone-armor-right-check",()=>FixtureNetwork.Connected(selected?"bone-armor-quick-result":"bone-armor-right-result",n=>Input(n,selected)));}
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Bone Armor exact shield,learned casts and persistence",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Train(PlayerMobile p,CombatAbility ability)
    {
        void Add(string id)
        {if(ClassCreation.Profiles.GetRank(p.Serial,id)>0){return;}TalentCatalog.TryGet(DiabloClass.Necromancer,id,out var row);foreach(string prior in row.Prerequisites){Add(prior);}Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}
        Add(SkillMechanics.For(ability).TalentId);
    }
    private static PlayerMobile Owner()
    {
        var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureBoneArmor";Train(p,CombatAbility.BoneArmor);
        Require(!SkillBindings.Instance.Assign(p,true,CombatAbility.BoneArmor)&&SkillBindings.Instance.Assign(p,false,CombatAbility.BoneArmor)
            &&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.BoneArmor),"Bone Armor right/quick bindings");return p;
    }
    private static void Ready(PlayerMobile p){ClassCombat.Stop(p);ClassCombat.For(p).NextAction=0;}
    private static void Cast(PlayerMobile p)
    {
        Ready(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);int before=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.BoneArmor).Mana256;
        Require(ClassCombat.Perform(p,CombatAbility.BoneArmor,null,p.Location,Core.TickCount),"Bone Armor learned begin");var s=ClassCombat.For(p);
        Require(s.PendingAbility==CombatAbility.BoneArmor&&NecromancerEffects.ManaBalance256(p)==before&&!ClassCombat.ResolveAction(p,s.EffectAt-1),"Bone Armor windup paid early");
        Require(ClassCombat.ResolveAction(p,s.EffectAt)&&NecromancerEffects.ManaBalance256(p)==before-cost&&!ClassCombat.ResolveAction(p,s.EffectAt),"Bone Armor release/replay payment");
    }
    private static void Source(PlayerMobile p)
    {
        var definition=SkillMechanics.For(CombatAbility.BoneArmor);int count=0;
        for(int rank=1;rank<=255;rank++)foreach(int wall in new[]{0,1,20})foreach(int prison in new[]{0,1,20})
        {
            var v=new SkillValues(definition,key=>key==definition.SourceKey?rank:255,key=>key=="Bone Wall"?wall:key=="Bone Prison"?prison:0,99);
            Require(v.Eval("aurastatcalc1")==256*(20+15*(rank-1)+15*(wall+prison))&&v.Eval("aurastatcalc1")==v.Eval("aurastatcalc2"),"Pinned armor hard synergy/rank capacity");count++;
        }
        Require(count==2295,"Missing armor formula contexts");
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"bone-armor-source-cases.json")));count=0;int applicable=0,exhaustion=0;
        foreach(var row in json.RootElement.EnumerateArray())
        {
            int V(int i)=>row[i].GetInt32();count++;
            if(V(0)!=0||V(2)<=0){continue;}
            if(V(1)>V(3))
            {
                NecromancerEffects.Profiles.Store(p,new(BoneArmor:(V(1)+255)/256,BoneArmorPool:new(null,V(1),null)));
                Require(NecromancerEffects.AbsorbPhysical(p,V(3)/256.0)==V(7)/256.0&&NecromancerEffects.Armor256(p)==V(6),"Within-pool event22 FP comparison");applicable++;
            }
            else{Require(V(7)==V(3),"Older event22 exhaustion discrepancy was silently normalized");exhaustion++;}
        }
        Require(count==262&&applicable==45&&exhaustion>0,"Incomplete event22 scope");NecromancerEffects.ClearBoneArmor(p);
    }
    private static void Combat(PlayerMobile p)
    {
        Cast(p);Require(NecromancerEffects.Armor256(p)==5120&&NecromancerEffects.ArmorMaximum256(p)==5120&&NecromancerEffects.Profiles.Find(p).BoneArmorPool.Rank==1,"Learned rank1 shield capture");
        var enemy=PoisonDaggerChecks.Target(p);try
        {
            int life=SkillHealth.Balance256(p);for(int i=0;i<20;i++){SkillCombat.HitParts(enemy,p,new(.25,0,0,0,0),recover:false);}
            Require(NecromancerEffects.Armor256(p)==15*256&&SkillHealth.Balance256(p)==life,"Fractional hits consumed whole shield points");
            AOS.Damage(p,enemy,1,false,100,0,0,0,0,weaponDamage:true);
            Require(NecromancerEffects.Armor256(p)==14*256&&SkillHealth.Balance256(p)==life,"Actual AOS physical event bypassed shield");
            SkillCombat.HitParts(enemy,p,new(0,1,1,1,0),recover:false);
            Require(NecromancerEffects.Armor256(p)==14*256&&SkillHealth.Balance256(p)<life,"Nonphysical channels consumed shield");
            Cast(p);PoisonDaggerChecks.Named(p,"Shaftstop");var result=SkillCombat.MitigatePrepared(enemy,p,new(30,0,0,0,0));
            Require(result.Physical==7&&NecromancerEffects.Armor256(p)==0&&NecromancerEffects.Profiles.Find(p).BoneArmorPool==null,"Shield exhaustion or before30percent physical reduction adapter");
            p.AddToBackpack(p.ChestArmor);ClassGear.Refresh(p);Cast(p);NecromancerEffects.AbsorbPhysical(p,1/256.0);
            var old=NecromancerEffects.Profiles.Find(p).BoneArmorPool;Train(p,CombatAbility.BoneWall);Train(p,CombatAbility.BonePrison);PoisonDaggerChecks.Named(p,"Wormskull");
            Require(NecromancerEffects.Profiles.Find(p).BoneArmorPool==old,"Gear/talent changes refilled or rewrote active armor");
            Cast(p);Require(NecromancerEffects.Armor256(p)==65*256&&NecromancerEffects.Profiles.Find(p).BoneArmorPool.Rank==2,"Recast ignored soft rank/hard-only synergies");
            NecromancerEffects.AbsorbPhysical(p,.25);Cast(p);Require(NecromancerEffects.Armor256(p)==65*256,"Recast stacked or failed to refill");
            var before=NecromancerEffects.Profiles.Find(p).BoneArmorPool;Ready(p);NecromancerEffects.SetMana256(p,0);
            Require(!ClassCombat.Perform(p,CombatAbility.BoneArmor,null,p.Location,Core.TickCount)&&NecromancerEffects.Profiles.Find(p).BoneArmorPool==before,"Unpaid cast removed existing armor");
            NecromancerEffects.SetMana256(p,p.ManaMax*256);Ready(p);Require(ClassCombat.Perform(p,CombatAbility.BoneArmor,null,p.Location,Core.TickCount),"Interrupted armor start");long at=ClassCombat.For(p).EffectAt;int mana=NecromancerEffects.ManaBalance256(p);p.Paralyzed=true;
            Require(!ClassCombat.ResolveAction(p,at)&&NecromancerEffects.ManaBalance256(p)==mana&&NecromancerEffects.Profiles.Find(p).BoneArmorPool==before,"Interrupted cast consumed/replaced armor");p.Paralyzed=false;
            SorceressEffects.CastArmor(p,CombatAbility.FrozenArmor,new(SkillMechanics.For(CombatAbility.FrozenArmor),_=>1,_=>0,99));
            Require(NecromancerEffects.Armor256(p)==0&&NecromancerEffects.Profiles.Find(p).BoneArmorPool==null,"Cold armor left exact Bone Armor active");Cast(p);
            Require(SorceressEffects.Armor(p)==null,"Bone Armor failed shared armor group replacement");
        }
        finally{enemy.Delete();}
    }
    private static void Lifecycle()
    {
        foreach(string mode in new[]{"death","respec","history"})
        {
            var p=Owner();try
            {
                Cast(p);NecromancerEffects.AbsorbPhysical(p,1/256.0);
                if(mode=="death"){p.Kill();p.Resurrect();}
                if(mode=="respec"){ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);}
                // Exercise the actual transaction that owns armor cleanup.
                // The restricted history-fixture helper refuses cast recovery
                // and deliberately does not implement the wardrobe transaction.
                if(mode=="history"){Require(WardrobeTransaction.ChangeClass(p,DiabloClass.Paladin,out _,out _),"Armor wardrobe transaction");}
                Require(NecromancerEffects.Armor256(p)==0&&NecromancerEffects.Profiles.Find(p)?.BoneArmorPool==null,"Lifecycle restored stale armor: "+mode);
            }
            finally{p.Corpse?.Delete();p.Delete();}
        }
    }
    private static Saved Save(PlayerMobile p)
    {
        var state=NecromancerEffects.Profiles.Find(p);var saved=new Saved(p.Serial.Value,p.Location,p.Map.MapID,NecromancerEffects.Armor256(p),state.BoneArmorPool,state.ManaFraction,0);p.Internalize();return saved;
    }
    private static void Check()
    {
        var p=Owner();Source(p);Combat(p);Lifecycle();NecromancerEffects.AbsorbPhysical(p,1/256.0);
        var legacy=Owner();legacy.Mana=legacy.ManaMax/2;
        NecromancerEffects.Profiles.Store(legacy,JsonSerializer.Deserialize<NecromancerState>("{\"BoneArmor\":20,\"ManaFraction\":128}"));
        Require(NecromancerEffects.Armor256(legacy)==5120&&NecromancerEffects.ArmorMaximum256(legacy)==null,"Legacy JSON invented original rank/maximum");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new[]{Save(p),Save(legacy)}));
        Log.Information("Bone Armor:2295capacity and45within-pool source comparisons;actual learned casts,fractional physical/native AOS,element exclusion,DR order,recast,gear,group,cancellation and lifecycle pass;legacy overflow discrepancy remains explicit");
    }
    private static void Reload()
    {
        var rows=JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest));Require(rows.Length==2,"Missing armor fixtures");
        for(int i=0;i<rows.Length;i++)
        {
            var s=rows[i];var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var state=NecromancerEffects.Profiles.Find(p);
            Require(p.Map==Map.Internal&&NecromancerEffects.Armor256(p)==s.Remaining256&&state.BoneArmorPool==s.Pool&&state.ManaFraction==s.ManaFraction,"Reload altered exact armor/capture or legacy representation");
            p.MoveToWorld(s.Point,Map.Maps[s.Map]);Require(NecromancerEffects.AbsorbPhysical(p,1/256.0)==0&&NecromancerEffects.Armor256(p)==s.Remaining256-1,"Loaded shield did not resume exact absorption");
            if(i==1){Require(NecromancerEffects.Profiles.Find(p).BoneArmorPool is{Rank:null,Maximum256:null},"Legacy migration guessed rank/maximum");}
            rows[i]=Save(p) with{Generation=s.Generation+1};
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(rows));
    }
    private static void Input(NetState network,bool quick)
    {
        var p=Owner();Ready(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.BoneArmor).Mana256;
        string label=quick?"bone-armor-quick":"bone-armor-right";network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var field=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);field.SetValue(network,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        bool began=false;long deadline=Core.TickCount+6000;
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Bone Armor input timeout");var s=ClassCombat.For(p);
                if(s.PendingAbility==CombatAbility.BoneArmor&&!began){Require(NecromancerEffects.Armor256(p)==0&&NecromancerEffects.ManaBalance256(p)==mana,"Input paid/applied during windup");began=true;Log.Information("{Label}: windup",label);}
                if(NecromancerEffects.Armor256(p)==0){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Inspect);return;}
                Require(began&&NecromancerEffects.Armor256(p)==5120&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Real right/quick self cast pool/payment");p.Delete();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){p.Delete();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.BoneArmor,(int)DiabloClass.Necromancer,p.Serial.Value,p.X,p.Y,p.Z);Inspect();});
    }
}
