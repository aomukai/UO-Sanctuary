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

public static class HolyShieldCastChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(HolyShieldCastChecks));
    private sealed record Saved(uint Owner,uint Shield,HolyShieldState State,int Mana,int Life,Point3D Origin,int Map,
        uint PendingOwner,uint PendingShield,Point3D PendingOrigin,int PendingMana,int PendingLife,int SaveStarts=0,int Reloads=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"holy-shield-cast-checks.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Holy Shield paid start,do18 rank rules and original saved state",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure()
    {
        Register("holy-shield-cast-check",Check);Register("holy-shield-cast-reload-check",Reload);Register("holy-shield-cast-save",StageSave);
        foreach(string mode in new[]{"right","quick"}){string label="holy-shield-cast-"+mode;Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,label)));}
    }
    private static PlayerMobile Owner()
    {
        var p=BlessedHammerDamageChecks.Owner();p.RawStr=1000;p.RawDex=75;
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.HolyShield)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.HolyShield)&&!SkillBindings.Instance.Assign(p,true,CombatAbility.HolyShield),"Holy Shield right/quick only");return p;
    }
    private static WoodenKiteShield Shield(PlayerMobile p)
    {var s=new WoodenKiteShield();ClassGear.Profiles.Convert(s,99,GearRarity.Normal,new Random(173));Require(p.EquipItem(s)&&HolyShields.Shield(p)==s,"Actual Holy Shield equipment");return s;}
    private static void Ready(PlayerMobile p){ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;}
    private static bool Start(PlayerMobile p){Ready(p);return ClassCombat.Perform(p,CombatAbility.HolyShield,null,p.Location,Core.TickCount);}
    private static bool Finish(PlayerMobile p)=>ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt);
    private static void Replace(HolyShieldState state)=>HolyShields.Restore(HolyShields.Snapshot().Where(s=>s.Owner!=state.Owner).Append(state).ToArray());
    private static void Clean(PlayerMobile p)
    {ClassCombat.Stop(p);HolyShields.Clear(p);p.Corpse?.Delete();p.Delete();}
    private static void Check()
    {
        // Parent-compatible APIs first, so a stale DLL fails on behavior.
        var p=Owner();Shield(p);try{NecromancerEffects.SetMana256(p,8977);Require(Start(p),"Holy Shield start regression setup");Require(NecromancerEffects.ManaBalance256(p)==17,"Holy Shield did not pay35mana at successful start");}finally{Clean(p);}
        SourceCases();RecastsAndCoexistence();Cancellation();Atomic();Save();
        Log.Information("Holy Shield cast:120ordinary source/native start/do components;35mana start/none on do;weaker paid refusal/equal refresh/higher replace;separate cold group;7cancellations;atomic restore;original buff plus paid starts staged immediately before actual saves");
    }
    private static void SourceCases()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"holy-shield-cast-source-cases.json")));int count=0;
        foreach(var r in doc.RootElement.EnumerateArray())
        {
            // Native players always have an inventory and no source bypass
            // permission. Compare only those120declared ordinary cases.
            if(r[0].GetInt32()!=1||r[5].GetInt32()!=0){continue;}
            var p=Owner();WoodenKiteShield shield=null;
            try
            {
                if(r[1].GetInt32()!=0){shield=Shield(p);}int old=r[3].GetInt32(),rank=r[4].GetInt32();
                if(old>0){HolyShields.Cast(p,SkillMechanics.Values(p,CombatAbility.HolyShield,old));}
                bool ready=r[2].GetInt32()!=0;NecromancerEffects.SetMana256(p,ready?8977:0);bool began=Start(p);
                Require(began==(r[7].GetInt32()!=0)&&NecromancerEffects.ManaBalance256(p)==(began?17:ready?8977:0),"Holy Shield source/native start gate/payment");
                if(began)
                {
                    if(r[6].GetInt32()!=0){p.Backpack.DropItem(shield);}
                    // This compares the bare do18 component at supplied rank,
                    // not a client cast bypassing equipment cancellation.
                    bool applied=HolyShields.Apply(p,SkillMechanics.Values(p,CombatAbility.HolyShield,rank),TimedEffects.Now);
                    Require(applied==(r[8].GetInt32()!=0)&&NecromancerEffects.ManaBalance256(p)==17,"Holy Shield source/native bare do18/result/no second payment");
                }
                Require((HolyShields.State(p)?.Rank??0)==r[11].GetInt32(),"Holy Shield source/native rank retention");count++;
            }
            finally{Clean(p);}
        }
        Require(count==120,"Incomplete ordinary Holy Shield source/native cohort");
    }
    private static void RecastsAndCoexistence()
    {
        var p=Owner();var shield=Shield(p);
        try
        {
            HolyShields.Cast(p,SkillMechanics.Values(p,CombatAbility.HolyShield,20));var strong=HolyShields.State(p);NecromancerEffects.SetMana256(p,8977);
            Require(Start(p)&&NecromancerEffects.ManaBalance256(p)==17&&!Finish(p)&&HolyShields.State(p)==strong&&!Finish(p),"Paid weaker Holy Shield replaced or replayed");
            Replace(strong with{UntilUtc=TimedEffects.Now.AddSeconds(1)});HolyShields.Cast(p,SkillMechanics.Values(p,CombatAbility.HolyShield,20));
            Require(HolyShields.State(p) is{Rank:20,BlockBonus:35}&&HolyShields.State(p).UntilUtc>TimedEffects.Now.AddSeconds(500),"Equal Holy Shield did not refresh");
            HolyShields.Clear(p);HolyShields.Cast(p,SkillMechanics.Values(p,CombatAbility.HolyShield));HolyShields.Cast(p,SkillMechanics.Values(p,CombatAbility.HolyShield,20));
            Require(HolyShields.State(p).Rank==20&&HolyShields.SmiteBonus(p)==(14592,15360),"Higher Holy Shield did not replace");
            var cold=SkillMechanics.Values(p,CombatAbility.FrozenArmor,1);SorceressEffects.CastArmor(p,CombatAbility.FrozenArmor,cold);var armor=SorceressEffects.Armor(p);
            HolyShields.Cast(p,SkillMechanics.Values(p,CombatAbility.HolyShield,20));Require(SorceressEffects.Armor(p)==armor,"Holy Shield cleared group1cold armor");
            var state=HolyShields.State(p);p.Backpack.DropItem(shield);
            Require(HolyShields.State(p)==state&&HolyShields.BlockBonus(p)==0&&HolyShields.DefensePercent(p)==0&&HolyShields.SmiteBonus(p)==(0,0),"Shieldless timed state lost identity or retained consumers");
            Require(p.EquipItem(shield)&&HolyShields.State(p)==state&&HolyShields.BlockBonus(p)==35,"Re-equipped shield did not resume original state");
            int hp=shield.HitPoints;shield.HitPoints=0;
            Require(HolyShields.State(p)==state&&HolyShields.Shield(p)==null&&HolyShields.DefensePercent(p)==0,"Broken shield retained Holy Shield defense");shield.HitPoints=hp;
            Require(HolyShields.Shield(p)==shield&&HolyShields.State(p)==state,"Repaired shield reset timed state");
        }
        finally{SorceressEffects.Profiles.Clear(p);Clean(p);}
    }
    private static void Cancellation()
    {
        foreach(string mode in new[]{"stop","movement","shield","helmet","freeze","death","wardrobe"})
        {
            var p=Owner();var shield=Shield(p);
            try
            {
                NecromancerEffects.SetMana256(p,8977);Require(Start(p)&&NecromancerEffects.ManaBalance256(p)==17,"Holy Shield cancellation paid start "+mode);
                if(mode=="stop"){ClassCombat.Stop(p);}if(mode=="movement"){p.Location=new(p.X+1,p.Y,p.Z);}if(mode=="shield"){p.Backpack.DropItem(shield);}
                if(mode=="helmet"){PoisonDaggerChecks.Named(p,"Tarnhelm");}if(mode=="freeze"){p.Frozen=true;}if(mode=="death"){p.Kill();}
                if(mode=="wardrobe"){Require(WardrobeTransaction.ChangeClass(p,DiabloClass.Sorceress,out _,out _),"Holy Shield wardrobe cancellation");}
                int after=NecromancerEffects.ManaBalance256(p);
                Require(mode=="death"?after==0:mode=="wardrobe"?after<8960:after==17,"Holy Shield cancellation resource policy "+mode+" mana="+after);
                Require(!Finish(p)&&HolyShields.State(p)==null&&ClassCombat.For(p).HolyShieldStart==null&&NecromancerEffects.ManaBalance256(p)==after,"Holy Shield cancellation/refund/replay "+mode+" mana="+after);
            }
            finally{p.Frozen=false;Clean(p);}
        }
        var owner=Owner();Shield(owner);
        try{NecromancerEffects.SetMana256(owner,8959);Require(!Start(owner)&&ClassCombat.For(owner).HolyShieldStart==null&&NecromancerEffects.ManaBalance256(owner)==8959,"Underfunded Holy Shield consumed mana or created ticket");}
        finally{Clean(owner);}
    }
    private static void Atomic()
    {
        var p=Owner();Shield(p);
        try
        {
            HolyShields.Cast(p,SkillMechanics.Values(p,CombatAbility.HolyShield));var all=HolyShields.Snapshot();var own=HolyShields.State(p);bool refused=false;
            try{HolyShields.Restore(all.Append(own with{Owner=uint.MaxValue,Rank=0}).ToArray());}catch(InvalidDataException){refused=true;}
            Require(refused&&HolyShields.Snapshot().SequenceEqual(all),"Malformed Holy Shield tail partially replaced pool");refused=false;
            try{HolyShields.Restore(all.Append(own).ToArray());}catch(InvalidDataException){refused=true;}
            Require(refused&&HolyShields.Snapshot().SequenceEqual(all),"Duplicate Holy Shield partially replaced pool");
            var copy=JsonSerializer.Deserialize<HolyShieldState[]>(JsonSerializer.Serialize(all));HolyShields.Restore(copy);Require(HolyShields.Snapshot().SequenceEqual(all),"Version0Holy Shield payload changed meaning");
        }
        finally{Clean(p);}
    }
    private static void Save()
    {
        var p=Owner();var shield=Shield(p);NecromancerEffects.SetMana256(p,8977);SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-7*256-17);
        Require(Start(p)&&Finish(p)&&NecromancerEffects.ManaBalance256(p)==17,"Original paid Holy Shield saved cast");
        var held=HolyShields.State(p) with{UntilUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};Replace(held);
        var pending=Owner();var pendingShield=Shield(pending);NecromancerEffects.SetMana256(pending,26897);SkillHealth.SetBalance256(pending,SkillHealth.Maximum256(pending)-5*256-17);
        Require(NecromancerEffects.ManaBalance256(pending)==26897,"Three paid Holy Shield save starts need original mana budget");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,shield.Serial.Value,held,17,SkillHealth.Balance256(p),p.Location,p.Map.MapID,
            pending.Serial.Value,pendingShield.Serial.Value,pending.Location,26897,SkillHealth.Balance256(pending))));p.Internalize();pending.Internalize();
    }
    private static void StageSave()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var pending=(PlayerMobile)World.FindMobile((Serial)s.PendingOwner);pending.MoveToWorld(s.PendingOrigin,Map.Maps[s.Map]);
        Require(HolyShields.State(pending)==null&&NecromancerEffects.ManaBalance256(pending)==s.PendingMana&&SkillHealth.Balance256(pending)==s.PendingLife,"Pending Holy Shield original resources changed before save");
        Require(Start(pending)&&ClassCombat.For(pending).HolyShieldStart!=null&&HolyShields.State(pending)==null&&NecromancerEffects.ManaBalance256(pending)==s.PendingMana-8960,"Paid Holy Shield was not staged immediately before save");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{PendingMana=NecromancerEffects.ManaBalance256(pending),SaveStarts=s.SaveStarts+1}));
        // Preserve the inherited full-suite save preparation as well.
        if(PlayerTeleportChecks.HasSavedFixture){PlayerTeleportChecks.StageSave();}else{World.Save();}
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var shield=World.FindItem((Serial)s.Shield);var pending=(PlayerMobile)World.FindMobile((Serial)s.PendingOwner);
        Require(s.Reloads<2&&s.SaveStarts==s.Reloads+1&&HolyShields.State(p)==s.State&&shield.Parent==p&&HolyShields.Shield(p)==shield
            &&NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillHealth.Balance256(p)==s.Life&&SkillBindings.Instance.Get(p).Right==CombatAbility.HolyShield&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Paladin,7)==CombatAbility.HolyShield,"Original Holy Shield buff/gear/bindings/resources changed on reload");
        Require(World.FindItem((Serial)s.PendingShield).Parent==pending&&ClassCombat.For(pending).PendingAbility==CombatAbility.None&&ClassCombat.For(pending).HolyShieldStart==null&&HolyShields.State(pending)==null
            &&NecromancerEffects.ManaBalance256(pending)==s.PendingMana&&SkillHealth.Balance256(pending)==s.PendingLife,"Saved paid Holy Shield action replayed or refunded");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Reloads=s.Reloads+1}));
    }
    private static void Input(NetState network,string label)
    {
        var p=Owner();Shield(p);var account=FixtureNetwork.Authenticate(network,p,"holy-shield-cast-input-");NecromancerEffects.SetMana256(p,8977);int life=SkillHealth.Balance256(p);bool began=false;long deadline=Core.TickCount+5000;
        void Cleanup(){Clean(p);account.Delete();}
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline,"Holy Shield wire timeout");
                if(ClassCombat.For(p).PendingAbility==CombatAbility.HolyShield&&!began)
                {Require(NecromancerEffects.ManaBalance256(p)==17&&HolyShields.State(p)==null&&ClassCombat.For(p).HolyShieldStart!=null,"Holy Shield wire start did not pay before effect");began=true;Log.Information("{Label}: windup",label);}
                if(HolyShields.State(p) is not{} state){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(began&&state is{Rank:1,BlockBonus:14}&&state.UntilUtc>TimedEffects.Now.AddSeconds(29)&&NecromancerEffects.ManaBalance256(p)==17&&SkillHealth.Balance256(p)==life&&ClassCombat.For(p).HolyShieldStart==null,"Holy Shield actual event/once start payment/state");Cleanup();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target=0 x={X} y={Y} z={Z}",label,(int)CombatAbility.HolyShield,(int)DiabloClass.Paladin,p.X,p.Y,p.Z);Observe();});
    }
}
