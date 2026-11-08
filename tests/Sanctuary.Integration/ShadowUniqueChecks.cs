// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ShadowUniqueChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowUniqueChecks));
    private sealed record Saved(uint Owner,uint[] Bodies,string[] States);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shadow-unique-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("shadow-unique-check",false);Register("shadow-unique-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Original Shadow modifier42 unique recipient semantics",_=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Check(bool reload)
    {
        PlayerMobile owner;AssassinShadowBody[] bodies;
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
            bodies=saved.Bodies.Select(id=>(AssassinShadowBody)World.FindMobile((Serial)id)).ToArray();
            for(int i=0;i<bodies.Length;i++){Require(bodies[i].SavedText==saved.States[i],"Reload changed original Shadow state");}
            owner.MoveToWorld(new(6490,3570,0),Map.Trammel);
        }
        else
        {
            owner=BladeBodyChecks.Fresh(6490,30,3570);ClassSkillChecks.Train(owner,"assassin.shadowwarrior",20);ClassSkillChecks.Train(owner,"assassin.shadowmaster",20);
            var allocation=NpcWorldAllocation.Capture();bodies=new AssassinShadowBody[2];
            try{int i=0;foreach(var ability in new[]{CombatAbility.ShadowWarrior,CombatAbility.ShadowMaster}){NpcWorldAllocation.Restore(new(0,NpcWorldAllocation.Profile,1,SourceRandom.InitLow(1)));bodies[i]=AssassinShadowBody.CreateInitialized(owner,ability,TimedEffects.Now);GeneratedNpcLiveResources.ActivateItemEvents(bodies[i]);ShadowEquipmentDefense.Activate(bodies[i]);i++;}}
            finally{NpcWorldAllocation.Restore(allocation);}
        }
        foreach(var body in bodies)
        {
            body.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);var rng=NpcEquipmentRandom.Require(body);string birth=body.SavedText;
            Require(body.Prepared.Capture.Plan(body.Prepared.Initialization.SpawnLife256).Modifier==42&&body.SourceUnique&&PreparedMonsterStun.IsUnique(body),"Original modifier42 did not supply unique identity");
            Require(!NamedCombat.Elite(body)&&CreatureProfiles.For(body).Traits.Boss!=true,"Unique identity assigned encounter/boss classification");
            Require(NpcItemSlow.Cap(body)==50,"Unique Shadow retained ordinary 90% slow cap");
            TimedEffects.Clear(body);var at=TimedEffects.Now;Require(NpcItemSlow.ApplyAmount(body,body,90,false,at)&&TimedEffects.SlowAttack(body,at)==50&&TimedEffects.Slow(body,at)==50,"Actual Shadow slow application lost unique cap");TimedEffects.Clear(body);
            Require(CombatStuns.Rules(body,out bool boss,out int velocity)&&!boss&&velocity==0,"Shadow stun eligibility substituted movement speed for original zero Velocity");
            var roll=CombatStuns.Roll;int draws=0;
            try{CombatStuns.Roll=n=>{Require(n==100,"Unique stun RNG bound");draws++;return 99;};Require(!CombatStuns.Apply(body,25)&&draws==1&&CombatStuns.Remaining(body)==0,"Original zero-Velocity Shadow received ordinary stun or skipped unique roll");}
            finally{CombatStuns.Roll=roll;CombatStuns.Clear(body);}
            foreach(int r in new[]{0,89,90,99})
            {
                int n=0;int duration=PreparedMonsterStun.Resolve(25,PreparedMonsterStun.IsUnique(body),limit=>{Require(limit==100,"Unique prepared roll bound");n++;return r;},()=>false,()=>6,()=>false);
                Require(n==1&&duration==(r<90?0:25),"Shared unique roll boundary differs for explicit nonzero-velocity recipient");
            }
            Require(NpcEquipmentRandom.Require(body)==rng&&body.SavedText==birth,"Recipient classification consumed actor RNG or changed birth state");
            SkillHealth.SetBalance256(body,body.MaximumLife256*2/3-1);PersistentMinions.Park(body);
        }
        PersistentMinions.Suspend(owner);owner.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,bodies.Select(b=>b.Serial.Value).ToArray(),bodies.Select(b=>b.SavedText).ToArray())));

        Log.Information("Both original Shadows:modifier42 unique identity without encounter/boss grade,50% applied slow cap,original zero-Velocity stun rejection,unique RNG boundary and unchanged actor RNG/birth data;wounds and identity survive reload. Death nova remains unfinished.");
    }
}
