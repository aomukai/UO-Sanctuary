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

public static class MightAuraChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(MightAuraChecks));
    private sealed record Saved(uint Owner,uint Ally,uint Sword,Point3D Origin,int Map,int Mana,int Life,int AllyLife,PaladinAuraSave State,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"might-aura-checks.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Might selection,atomic shared aura pools and original saved emitters/leases",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure()
    {
        Register("might-aura-check",Check);Register("might-aura-reload-check",()=>FixtureNetwork.Connected("might-aura-reload-result",Reload));
        foreach(string mode in new[]{"right","quick"}){string label="might-aura-"+mode;Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,label)));}
    }
    private static PlayerMobile Owner()
    {
        var p=BlessedHammerDamageChecks.Owner();Require(SkillBindings.Instance.Assign(p,false,CombatAbility.HolyBolt)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Might)
            &&!SkillBindings.Instance.Assign(p,true,CombatAbility.Might),"Might right/quick only");return p;
    }
    private static PlayerMobile Ally(PlayerMobile p)
    {var a=ClassSkillChecks.Player(DiabloClass.Sorceress);a.Name="SacrificeAllocationFixture";a.RawStr=1000;a.RawDex=400;a.AddItem(new Backpack());a.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);return a;}
    private static void Select(PlayerMobile p,CombatAbility ability)=>Require(SkillBindings.Instance.Assign(p,false,ability),"Aura selection "+ability);
    private static void Clean(PlayerMobile p,params Mobile[] others)
    {PaladinAuras.Clear(p);foreach(var m in others){PaladinAuras.Clear(m);m.Corpse?.Delete();m.Delete();}p.Corpse?.Delete();p.Delete();}
    private static PaladinAuraSave Scope(PaladinAuraSave s,uint owner)=>new(s.Emitters.Where(e=>e.Owner==owner).ToArray(),s.Leases.Where(l=>l.Source==owner).ToArray());
    private static bool Same(PaladinAuraSave a,PaladinAuraSave b)=>JsonSerializer.Serialize(a)==JsonSerializer.Serialize(b);
    private static void Check()
    {
        // Existing APIs establish a behavior regression against the parent DLL.
        var p=Owner();NecromancerEffects.SetMana256(p,0);Select(p,CombatAbility.Might);var before=PaladinAuras.Snapshot();bool refused=false;
        try
        {
            var own=before.Leases.Single(l=>l.Target==p.Serial.Value&&l.Ability==CombatAbility.Might);
            try{PaladinAuras.Restore(before with{Leases=before.Leases.Append(own with{Target=uint.MaxValue,Percent=-1}).ToArray()});}catch(InvalidDataException){refused=true;}
            Require(refused&&Same(PaladinAuras.Snapshot(),before),"Might malformed aura tail partially replaced the live pools");
        }
        finally{PaladinAuras.Restore(before);Clean(p);}
        Atomic();Mechanics();RankOwnership();Save();
        Log.Information("Might:atomic two-pool restore/version0;zero-mana instant selection;physical-only material damage;rank/creator/closed-radius/hostile/height/LOS;original emitter/leases/resources staged;shared sibling aura coverage retained");
    }
    private static void Atomic()
    {
        var p=Owner();try
        {
            Select(p,CombatAbility.Might);var all=PaladinAuras.Snapshot();var e=all.Emitters.Single(x=>x.Owner==p.Serial.Value);var l=all.Leases.Single(x=>x.Target==p.Serial.Value&&x.Ability==CombatAbility.Might);
            foreach(string mode in new[]{"emitter","duplicate-emitter","lease","duplicate-lease","null-emitter","null-lease"})
            {
                var bad=mode switch
                {
                    "emitter"=>all with{Emitters=all.Emitters.Append(e with{Owner=uint.MaxValue,Ability=CombatAbility.BasicAttack}).ToArray()},
                    "duplicate-emitter"=>all with{Emitters=all.Emitters.Append(e).ToArray()},
                    "lease"=>all with{Leases=all.Leases.Append(l with{Target=uint.MaxValue,Percent=-1}).ToArray()},
                    "duplicate-lease"=>all with{Leases=all.Leases.Append(l).ToArray()},
                    "null-emitter"=>all with{Emitters=all.Emitters.Append((PaladinEmitter)null).ToArray()},
                    _=>all with{Leases=all.Leases.Append((PaladinAuraLease)null).ToArray()}
                };bool refused=false;try{PaladinAuras.Restore(bad);}catch(InvalidDataException){refused=true;}
                Require(refused&&Same(PaladinAuras.Snapshot(),all),"Aura atomic restore "+mode);
            }
            var round=JsonSerializer.Deserialize<PaladinAuraSave>(JsonSerializer.Serialize(all));PaladinAuras.Restore(round);Require(Same(all,PaladinAuras.Snapshot()),"Version0aura roundtrip changed fields");
        }
        finally{Clean(p);}
    }
    private static void Mechanics()
    {
        var p=Owner();var ally=Ally(p);var edge=Ally(p);edge.MoveToWorld(new(p.X+8,p.Y,p.Z),p.Map);var outside=Ally(p);outside.MoveToWorld(new(p.X+9,p.Y,p.Z),p.Map);
        var high=Ally(p);high.MoveToWorld(new(p.X+3,p.Y,p.Z+16),p.Map);var enemy=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true};enemy.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);enemy.AIObject.AITimer.Stop();
        var wall=new Static(0x80);wall.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);
        var sword=new Longsword{Resource=CraftResource.Valorite};var item=ClassGear.Profiles.Convert(sword,99,GearRarity.Normal,new Random(173));ClassGear.Profiles.Store(sword,item with{CraftedEquipment=true,MaterialRolls=[new("damage-fire",5)]});Require(ally.EquipItem(sword),"Might actual material sword");
        try
        {
            Require(!p.InLOS(ally),"Might wall fixture must block ordinary LOS");var original=GearEffects.Damage(ally,.5);int ed=ClassStats.DamageBonus(ally,sword);NecromancerEffects.SetMana256(p,0);
            var action=ClassCombat.For(p);action.NextAction=Core.TickCount+10000;long next=action.NextAction;
            Require(ClassCombat.Perform(p,CombatAbility.Might,null,p.Location,Core.TickCount)&&PaladinAuras.Selected(p)==CombatAbility.Might&&PaladinAuras.Effect(p,CombatAbility.Might) is{Rank:1,Percent:40},"Zero mana immediate Might");
            var lease=PaladinAuras.Effect(ally,CombatAbility.Might);var after=GearEffects.Damage(ally,.5);
            Require(lease is{Rank:1,Percent:40}&&PaladinAuras.Effect(edge,CombatAbility.Might)!=null&&PaladinAuras.Effect(outside,CombatAbility.Might)==null
                &&PaladinAuras.Effect(high,CombatAbility.Might)==null&&PaladinAuras.Effect(enemy,CombatAbility.Might)==null,"Might declared closed-radius/height/hostile/no-LOS projection");
            Require(ClassStats.DamageBonus(ally,sword)==ed+40&&after.Physical>original.Physical&&after.Fire==original.Fire&&after.Cold==original.Cold&&after.Lightning==original.Lightning&&after.Poison==original.Poison,"Might amplified material element or omitted physical pool");
            Require(NecromancerEffects.ManaBalance256(p)==0&&action.PendingAbility==CombatAbility.None&&action.NextAction==next&&ClassCombat.ActionInterval(p,CombatAbility.Might)==0,"Might paid mana or consumed an action cycle");
            Select(p,CombatAbility.HolyBolt);Require(PaladinAuras.Effect(p,CombatAbility.Might)==null&&PaladinAuras.Effect(ally,CombatAbility.Might)==lease,"Might switch did not preserve recipient linger");
        }
        finally{wall.Delete();Clean(p,ally,edge,outside,high,enemy);}
    }
    private static void RankOwnership()
    {
        var p=Owner();var a=Ally(p);var other=Owner();other.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);
        try
        {
            Select(p,CombatAbility.Might);var first=PaladinAuras.Effect(a,CombatAbility.Might);Select(other,CombatAbility.Might);var equal=PaladinAuras.Effect(a,CombatAbility.Might);
            Require(equal.Rank==1&&equal.Percent==40&&equal.Source==p.Serial.Value&&equal.UntilUtc>=first.UntilUtc,"Equal Might did not retain original creator");
            Require(ClassCreation.Profiles.TryTrain(other.Serial,"paladin.might",out _),"Higher Might hard rank");Select(other,CombatAbility.Might);var stronger=PaladinAuras.Effect(a,CombatAbility.Might);
            Require(stronger is{Rank:2,Percent:50}&&stronger.Source==other.Serial.Value,"Stronger Might did not replace");Select(p,CombatAbility.Might);
            Require(PaladinAuras.Effect(a,CombatAbility.Might)==stronger,"Weaker Might extended stronger lease");
            Select(other,CombatAbility.HolyBolt);Require(PaladinAuras.Effect(a,CombatAbility.Might)==stronger,"Emitter removal erased another actor's lease");
        }
        finally{Clean(p,a,other);}
    }
    private static PaladinAuraSave Hold(PlayerMobile p)
    {
        var all=PaladinAuras.Snapshot();var at=TimedEffects.Now.Add(FixtureSpace.Retention);
        all=all with{Emitters=all.Emitters.Select(e=>e.Owner==p.Serial.Value?e with{NextUtc=at}:e).ToArray(),Leases=all.Leases.Select(l=>l.Source==p.Serial.Value?l with{UntilUtc=at.AddMilliseconds(40)}:l).ToArray()};
        PaladinAuras.Restore(all);return Scope(all,p.Serial.Value);
    }
    private static void Save()
    {
        var p=Owner();var a=Ally(p);var sword=new Longsword();ClassGear.Profiles.Convert(sword,99,GearRarity.Normal,new Random(173));Require(a.EquipItem(sword),"Saved original aura receiver sword");
        NecromancerEffects.SetMana256(p,0);SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-7*256-17);SkillHealth.SetBalance256(a,SkillHealth.Maximum256(a)-5*256-17);
        Select(p,CombatAbility.Might);Require(PaladinAuras.Effect(a,CombatAbility.Might) is{Rank:1,Percent:40},"Original saved Might selection");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,a.Serial.Value,sword.Serial.Value,p.Location,p.Map.MapID,0,SkillHealth.Balance256(p),SkillHealth.Balance256(a),Hold(p))));p.Internalize();a.Internalize();
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var a=(PlayerMobile)World.FindMobile((Serial)s.Ally);
        Require(s.Generation<2&&Same(Scope(PaladinAuras.Snapshot(),p.Serial.Value),s.State)&&NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillHealth.Balance256(p)==s.Life&&SkillHealth.Balance256(a)==s.AllyLife
            &&World.FindItem((Serial)s.Sword).Parent==a&&SkillBindings.Instance.Get(p).Right==CombatAbility.Might&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Paladin,7)==CombatAbility.Might,"Original saved Might emitter/leases/gear/bindings/resources changed");
        var account=FixtureNetwork.Authenticate(network,p,"might-reload-");p.MoveToWorld(s.Origin,Map.Maps[s.Map]);a.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);
        var before=PaladinAuras.Effect(a,CombatAbility.Might);var all=PaladinAuras.Snapshot();PaladinAuras.Restore(all with{Emitters=all.Emitters.Select(e=>e.Owner==p.Serial.Value?e with{NextUtc=TimedEffects.Now}:e).ToArray()});long deadline=Core.TickCount+5000;
        void Observe()
        {
            try
            {
                var lease=PaladinAuras.Effect(a,CombatAbility.Might);
                if(lease==before){Require(Core.TickCount<deadline,"Natural saved Might refresh timeout");Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                var emitter=PaladinAuras.Snapshot().Emitters.Single(e=>e.Owner==p.Serial.Value);
                Require(lease is{Rank:1,Percent:40}&&lease.Source==p.Serial.Value&&lease.UntilUtc<TimedEffects.Now.AddSeconds(3)&&emitter.NextUtc<TimedEffects.Now.AddSeconds(3)&&NecromancerEffects.ManaBalance256(p)==s.Mana
                    &&SkillHealth.Balance256(p)==s.Life&&SkillHealth.Balance256(a)==s.AllyLife,"Natural saved Might period/lease/zero cost/resources");
                var held=Hold(p);p.Internalize();a.Internalize();network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();
                File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{State=held,Generation=s.Generation+1}));Log.Information("might-aura-reload-result: passed");
            }
            catch(Exception e){p.Internalize();a.Internalize();Log.Error(e,"might-aura-reload-result: failed");}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);
    }
    internal static void Input(NetState network,string label,CombatAbility ability=CombatAbility.Might)
    {
        var p=Owner();Require(SkillBindings.Instance.AssignQuick(p,7,ability)&&!SkillBindings.Instance.Assign(p,true,ability),"Aura right/quick binding");int value=(int)SkillMechanics.Values(p,ability).Eval("aurastatcalc1");var a=Ally(p);var account=FixtureNetwork.Authenticate(network,p,"might-input-");NecromancerEffects.SetMana256(p,0);
        bool right=label.EndsWith("right",StringComparison.Ordinal);if(right){Select(p,ability);}var lease=PaladinAuras.Effect(a,ability);var action=ClassCombat.For(p);action.NextAction=Core.TickCount+10000;long next=action.NextAction;long deadline=Core.TickCount+4000;
        void Observe()
        {
            try
            {
                bool received=right?action.Sequence==1:SkillBindings.Instance.Get(p).Right==ability;
                if(!received){Require(Core.TickCount<deadline,"Instant Might input timeout");Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(PaladinAuras.Selected(p)==ability&&PaladinAuras.Effect(a,ability) is{Rank:1} actual&&actual.Percent==value&&NecromancerEffects.ManaBalance256(p)==0&&action.PendingAbility==CombatAbility.None&&action.NextAction==next,"Instant Might input created a cast cycle or paid mana");
                if(right){Require(PaladinAuras.Effect(a,ability)==lease,"Holding selected Might refreshed its lease");}
                Log.Information("{Label}: acknowledged",label);Clean(p,a);account.Delete();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Clean(p,a);account.Delete();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target=0 x={X} y={Y} z={Z}",label,(int)ability,(int)DiabloClass.Paladin,p.X,p.Y,p.Z);Observe();});
    }
}
