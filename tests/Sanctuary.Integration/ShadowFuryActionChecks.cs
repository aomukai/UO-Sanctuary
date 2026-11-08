// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ShadowFuryActionChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowFuryActionChecks));
    private sealed record Held(uint Actor,string Body,string Resources,string[] Items,NpcEquipmentSeed Random,int Life);
    private sealed record Saved(uint Owner,Held[] Bodies,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shadow-fury-cast-fixtures.json");
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v);
    private static void Require(bool v,string why){if(!v){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("shadow-fury-cast-check",false);Register("shadow-fury-cast-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Approved own Shadow Fury sequence,critical/world streams and natural AI",_=>Core.LoopContext.Post(()=>{try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static Held Capture(AssassinShadowBody b)=>new(b.Serial.Value,b.SavedText,Json(GeneratedNpcLiveResources.State(b)),ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray(),NpcEquipmentRandom.Require(b),SkillHealth.Balance256(b));
    private static WeaponFlight[] Flights(AssassinShadowBody b)=>WeaponMissiles.Snapshot().Flights.Where(f=>f.Owner==b.Serial.Value).ToArray();
    private static ShadowTailChecks.Witness Target(AssassinShadowBody b)
    {var t=new ShadowTailChecks.Witness{Actor=b.Serial.Value};t.SetHits(65000);t.MoveToWorld(new(b.X+8,b.Y,b.Z),b.Map);t.AIObject.AITimer.Stop();return t;}
    private static void Cast(AssassinShadowBody b,PlayerMobile p,string mode)
    {
        var t=Target(b);var at=TimedEffects.Now;var before=NpcEquipmentRandom.Require(b);var world=NpcWorldAllocation.Capture();int mana=NecromancerEffects.ManaBalance256(p),own=NecromancerEffects.ManaBalance256(b);
        NecromancerEffects.SetMana256(p,0);NecromancerEffects.SetMana256(b,0);
        try
        {
            Require(ShadowActions.BeginBladeFury(b,mode=="ground"?null:t,t.Location,at),"Own Fury startup refused");var a=ShadowActions.State(b);
            Require(a.Plan.Events.Select(e=>e.Frame).SequenceEqual(new[]{10,18})&&a.Cast.Fury is{Active:true,Expires:21,NextShot:0}&&Flights(b).Length==0,"Fury original clock or fresh action state differs");
            var chances=ShadowFuryPackets.CriticalChances(b);var graph=OriginalNpcEquipment.Graph(b);Require(chances.Passive==graph.Read(337)&&chances.Deadly==graph.Read(141),"Own critical did not read actual actor stats");
            int strength=p.RawStr;p.RawStr+=200;Require(ShadowFuryPackets.CriticalChances(b)==chances,"Critical borrowed controller stats");p.RawStr=strength;
            ShadowActions.Tick(at.AddMilliseconds(9*40),true,b.Serial.Value);Require(Flights(b).Length==0&&NpcEquipmentRandom.Require(b)==before&&NpcWorldAllocation.Capture()==world,"Fury rolled/allocated before release");
            if(mode=="move-target"){t.MoveToWorld(new(t.X+1,t.Y,t.Z),t.Map);}
            if(mode=="rank"){b.ChangeSkills(s=>s.Assign(266,14));}
            if(mode=="dead-target"){t.Delete();}
            ulong expected=before.Seed;bool critical=NpcMissileCritical.Unsigned(chances.Passive,chances.Deadly,chances.Mastery,()=>SourceRandom.Roll(ref expected));
            ulong game=world.Seed,child=SourceRandom.InitLow(unchecked((int)SourceRandom.Roll(ref game)));
            ShadowActions.Tick(at.AddMilliseconds(10*40),true,b.Serial.Value);
            if(mode=="dead-target")
            {Require(Flights(b).Length==0&&NpcEquipmentRandom.Require(b)==before&&NpcWorldAllocation.Capture()==world,"Deleted target emitted or consumed streams");}
            else
            {
                var f=Flights(b).Single();Require(f.Npc?.Packet.Actor==b.Serial.Value&&f.Npc.CreditOwner==p.Serial.Value&&f.SkillRank==b.OwnedSkillRank(266)&&f.Damage.Critical==critical&&f.Npc.Seed==child,"Fury lost actual source/rank/critical/private seed");
                Require(NpcEquipmentRandom.Require(b).Seed==expected&&NpcWorldAllocation.Capture()==world with{Seed=game}&&a.Cast.Fury is{NextShot:14},"Critical/world stream order or do48 state differs");
            }
            var rng=NpcEquipmentRandom.Require(b);var allocated=NpcWorldAllocation.Capture();int count=Flights(b).Length;
            ShadowActions.Tick(at.AddMilliseconds(10*40),true,b.Serial.Value);ShadowActions.Tick(at.AddMilliseconds(18*40),true,b.Serial.Value);
            Require(Flights(b).Length==count&&NpcEquipmentRandom.Require(b)==rng&&NpcWorldAllocation.Capture()==allocated&&ShadowActions.State(b)==null&&a.Cast.Fury==null&&b.OwnedSkills.Used==null,"Fury duplicated,rewound or carried its lease past recovery");
            if(mode=="direct")
            {
                foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}int hp=SkillHealth.Balance256(t);
                ulong hitSeed=0;for(int i=1;i<10000&&hitSeed==0;i++){ulong candidate=SourceRandom.InitLow(i),probe=candidate;if(SourceRandom.Limited(ref probe,100)<5){hitSeed=candidate;}}
                Require(hitSeed!=0,"No declared hit seed");NpcEquipmentRandom.Set(b,hitSeed);
                WeaponMissiles.Tick(Flights(b).Single().NextUtc,true,b.Serial.Value);Require(SkillHealth.Balance256(t)==hp&&Flights(b).Single().Frame==1,"Action-born blade hit before travel");
                for(int i=0;i<45&&Flights(b).Length>0;i++){WeaponMissiles.Tick(Flights(b).Single().NextUtc,true,b.Serial.Value);}
                Require(Flights(b).Length==0&&SkillHealth.Balance256(t)<hp,"Action-born blade did not travel and damage its actual target");
            }
            Require(NecromancerEffects.ManaBalance256(p)==0&&NecromancerEffects.ManaBalance256(b)==0,"Intrinsic Fury charged mana");
        }
        finally{ShadowActions.Cancel(b);WeaponMissiles.Cancel(b);t.Delete();b.ChangeSkills(s=>{s.Assign(266,12);s.Use(null);});NecromancerEffects.SetMana256(p,mana);NecromancerEffects.SetMana256(b,own);}
    }
    private static void Interrupt(AssassinShadowBody b,PlayerMobile p)
    {
        var t=Target(b);var point=b.Location;
        try
        {
            foreach(string why in new[]{"cancel","move","frozen","paralyzed","offline","used","stun","suspend"})
            {
                var at=TimedEffects.Now;var rng=NpcEquipmentRandom.Require(b);var world=NpcWorldAllocation.Capture();Require(ShadowActions.BeginBladeFury(b,t,t.Location,at),"Interrupted Fury startup refused:"+why);var cast=ShadowActions.State(b).Cast;
                switch(why){case"cancel":ShadowActions.Cancel(b);break;case"move":b.MoveToWorld(new(point.X,point.Y+1,point.Z),p.Map);break;case"frozen":b.Frozen=true;break;case"paralyzed":b.Paralyzed=true;break;case"used":b.ChangeSkills(s=>s.Use(0));break;case"stun":Require(CombatStuns.ApplyPreparedDuration(b,25,at),"Prepared stun refused");break;case"suspend":PersistentMinions.Suspend(b);break;}
                ShadowActions.Tick(at.AddMilliseconds(40),why!="offline",b.Serial.Value);
                Require(ShadowActions.State(b)==null&&cast.Fury==null&&Flights(b).Length==0&&NpcEquipmentRandom.Require(b)==rng&&NpcWorldAllocation.Capture()==world,"Interrupted Fury retained lease/emitted/rolled:"+why);
                b.Frozen=b.Paralyzed=false;CombatStuns.Clear(b);b.ChangeSkills(s=>s.Use(null));b.MoveToWorld(point,p.Map);
            }
        }
        finally{ShadowActions.Cancel(b);t.Delete();b.Frozen=b.Paralyzed=false;CombatStuns.Clear(b);b.MoveToWorld(point,p.Map);}
    }
    private static AssassinShadowBody Create(PlayerMobile p,CombatAbility ability)
    {
        var saved=NpcWorldAllocation.Capture();AssassinShadowBody b;
        try{NpcWorldAllocation.Restore(new(0,NpcWorldAllocation.Profile,1,SourceRandom.InitLow(1)));b=AssassinShadowBody.CreateInitialized(p,ability,TimedEffects.Now);}
        finally{NpcWorldAllocation.Restore(saved);}
        GeneratedNpcLiveResources.ActivateItemEvents(b);ShadowEquipmentDefense.Activate(b);return b;
    }
    private static void Natural(PlayerMobile p,CombatAbility ability)
    {
        var b=Create(p,ability);b.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);var t=Target(b);var bindings=SkillBindings.Instance.Get(p);
        try
        {
            if(ability==CombatAbility.ShadowMaster){b.ChangeSkills(s=>{foreach(var e in s.Snapshot().Entries.Where(e=>e.Id!=0&&e.Id!=266)){s.Assign(e.Id,0,true);}s.Assign(266,12);});}
            Require(SkillBindings.Instance.Assign(p,true,CombatAbility.BladeFury)&&SkillBindings.Instance.Assign(p,false,CombatAbility.BladeFury),"Natural Fury bindings refused");
            ShadowGroundMotion.Activate(b,p,TimedEffects.Now.AddMinutes(20));bool selected=false;
            for(int i=0;i<1000&&Flights(b).Length==0;i++)
            {IronGolemWorld.Advance(b,b.WorldGround.State.Controller.NextUtc,true);Require(b.WorldGround.State.Controller.Fault==null,"Natural Fury fault:"+b.WorldGround.State.Controller.Fault);selected|=ShadowActions.State(b)?.Cast.Skill==266;}
            Require(selected&&Flights(b).Length==1&&Flights(b)[0].Npc.Packet.Actor==b.Serial.Value,"Natural Shadow did not select/release its own Fury");
            var state=b.WorldGround.State.Controller;b.WorldGround.Store(state with{NextThink=int.MaxValue});
            for(int i=0;i<100&&ShadowActions.State(b)!=null;i++){IronGolemWorld.Advance(b,b.WorldGround.State.Controller.NextUtc,true);}
            Require(ShadowActions.State(b)==null&&Flights(b).Length==1,"Natural Fury did not finish exactly once");
        }
        finally{ShadowActions.Cancel(b);WeaponMissiles.Cancel(b);t.Delete();PersistentMinions.Park(b);b.Delete();SkillBindings.Instance.Assign(p,true,bindings.Right);SkillBindings.Instance.Assign(p,false,bindings.Left);}
    }
    private static void Check(bool reload)
    {
        _=NpcWorldAllocation.RequireStream(); // Explicit initialized world for seed-order comparisons.
        PlayerMobile p;AssassinShadowBody[] bodies;int generation=0;
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=saved.Generation+1;p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);bodies=saved.Bodies.Select(x=>(AssassinShadowBody)World.FindMobile((Serial)x.Actor)).ToArray();
            for(int i=0;i<bodies.Length;i++){Require(Json(Capture(bodies[i]))==Json(saved.Bodies[i])&&ShadowActions.State(bodies[i])==null&&Flights(bodies[i]).Length==0,"Reload changed original state or replayed windup");}p.MoveToWorld(new(6520,3570,0),Map.Trammel);
        }
        else
        {
            p=BladeBodyChecks.Fresh(6520,30,3570);ClassSkillChecks.Train(p,"assassin.shadowwarrior",20);ClassSkillChecks.Train(p,"assassin.shadowmaster",20);ClassSkillChecks.Train(p,"assassin.bladefury",1);
            bodies=new[]{Create(p,CombatAbility.ShadowWarrior),Create(p,CombatAbility.ShadowMaster)};
        }
        foreach(var b in bodies)
        {
            b.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);b.ChangeSkills(s=>s.Assign(266,12));foreach(string mode in new[]{"direct","ground","move-target","rank","dead-target"}){Cast(b,p,mode);}Interrupt(b,p);
            var target=Target(b);Require(ShadowActions.BeginBladeFury(b,target,target.Location,TimedEffects.Now),"Save windup entry refused");PersistentMinions.Suspend(b);target.Delete();Require(ShadowActions.State(b)==null&&b.OwnedSkills.Used==null,"Suspend retained Fury lease/selection");SkillHealth.SetBalance256(b,b.MaximumLife256*2/3-1);
        }
        Natural(p,CombatAbility.ShadowWarrior);Natural(p,CombatAbility.ShadowMaster);GeneratedNpcLiveResources.Snapshot();var held=bodies.Select(Capture).ToArray();PersistentMinions.Suspend(p);p.Internalize();NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,held,generation)));
        Require(!AssassinCombat.Implemented(CombatAbility.ShadowWarrior)&&!AssassinCombat.Implemented(CombatAbility.ShadowMaster),"Intrinsic Fury enabled unfinished learned Shadows");
        Log.Information("Approved own Fury:single original10/18 sequence,free NPC state scoped to action,once-only critical/world/private seed composition,actual NPC packet,live target/rank,deleted-target no allocation,eight interruptions and suspend/no replay;natural Warrior/Master selection/release/recovery and original saves pass. Learned admission remains separate.");
    }
}
