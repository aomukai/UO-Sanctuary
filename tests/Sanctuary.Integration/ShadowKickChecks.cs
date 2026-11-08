// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ShadowKickChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowKickChecks));
    private sealed record Held(uint Actor,string Body,string Resources,string[] Items,NpcEquipmentSeed Random,int Life,KickDamageInput Input);
    private sealed record Saved(uint Owner,Held[] Bodies,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shadow-kick-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("shadow-kick-check",false);Register("shadow-kick-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Own Shadow kick capture, original graph and saved actor RNG",_=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static Held Capture(AssassinShadowBody b)=>new(b.Serial.Value,b.SavedText,Json(GeneratedNpcLiveResources.State(b)),
        ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray(),NpcEquipmentRandom.Require(b),SkillHealth.Balance256(b),ShadowKickDamage.Capture(b,138));
    private static void Numeric(AssassinShadowBody b,PlayerMobile owner)
    {
        var graph=b.EquipmentNumbers;var binding=ShadowLoadout.Binding(b);var seed=NpcEquipmentRandom.Require(b);
        var items=ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray();int mana=NecromancerEffects.ManaBalance256(owner),life=SkillHealth.Balance256(b);
        var input=ShadowKickDamage.Capture(b,138);
        int level=b.Prepared.Capture.Level;
        Require(!input.Player&&input.Level==level&&DragonKickDamage.Body256(input)==(3*level/4*256,level*256),"NPC body used player attributes or live owner level");
        var boots=ShadowLoadout.Originals(b).SingleOrDefault(i=>i.Snapshot.Equipment.Body==9);
        Require(input.Boot==(boots==null?null:DragonKickDamage.Base(boots.Snapshot.Metadata.Code)),"Kick did not use original raw feet-slot base");
        Require(input.Strength==graph.Read(0)&&input.Dexterity==graph.Read(2)&&graph.DamageItems.Contains(binding.Primary)
            &&!graph.DamageItems.Contains(binding.Secondary)&&NpcEquipmentRandom.Require(b)==seed,"Capture lost nondamage attributes, primary restoration or consumed RNG");
        // Exercise real original hand lists via the shared stat writer. These
        // temporary fixture modifiers are restored before any save.
        var hands=new[]{binding.Primary,binding.Secondary}.Where(id=>id!=0).Select(id=>graph.Item(id).Unit).ToArray();
        var old=hands.Select(h=>EquipmentStatValues.Raw(h.Stats.BaseValues,EquipmentStatKey.Pack(17))).ToArray();
        try
        {
            for(int i=0;i<hands.Length;i++){graph.Values.SetBase(hands[i].Stats,EquipmentStatKey.Pack(17),old[i]+217);}
            var offhand=graph.Weapons(binding.Primary,binding.Secondary,true,true,3,256,0);offhand.Enter();
            Require(offhand.Selected==binding.Secondary,"Fixture did not actually select offhand");
            var other=ShadowKickDamage.Capture(b,138);
            Require(other==input,"Weapon ED or previous offhand damage posting contaminated kick arithmetic");
        }
        finally{for(int i=0;i<hands.Length;i++){graph.Values.SetBase(hands[i].Stats,EquipmentStatKey.Pack(17),old[i]);}graph.Weapons(binding.Primary,binding.Secondary,false,false,0,0,0).Restore();}
        // The saved NPC stream supplies exactly the existing limited draw;
        // capture itself leaves actor and global factory streams untouched.
        var world=NpcWorldAllocation.Capture();ulong expected=seed.Seed;var range=DragonKickDamage.Range256(input);
        int roll=(int)SourceRandom.Limited(ref expected,range.Max-range.Min);
        Require(ShadowKickDamage.Roll256(b,input)==range.Min+roll&&NpcEquipmentRandom.Require(b).Seed==expected
            &&NpcWorldAllocation.Capture()==world,"Kick used owner/global RNG or wrong exclusive range");
        Require(ShadowLoadout.Originals(b).Select(i=>i.SavedText).SequenceEqual(items)&&NecromancerEffects.ManaBalance256(owner)==mana
            &&SkillHealth.Balance256(b)==life&&!AssassinCharges.Any(b),"Prepared kick changed item payload, resource, wounds or charges");
        Require(ShadowKickDamage.Capture(b,138)==input,"Temporary fixture modifiers were not restored");
    }
    private static void Check(bool reload)
    {
        PlayerMobile owner;AssassinShadowBody[] bodies;int generation=0;
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=saved.Generation+1;
            owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);bodies=saved.Bodies.Select(h=>(AssassinShadowBody)World.FindMobile((Serial)h.Actor)).ToArray();
            for(int i=0;i<bodies.Length;i++){Require(Json(Capture(bodies[i]))==Json(saved.Bodies[i])&&ShadowActions.State(bodies[i])==null,"Reload changed original kick graph/resources/RNG/wounds or replayed an action");}
        }
        else
        {
            owner=BladeBodyChecks.Fresh(6290,12,3470);ClassSkillChecks.Train(owner,"assassin.shadowwarrior",20);ClassSkillChecks.Train(owner,"assassin.shadowmaster",20);
            var world=NpcWorldAllocation.Capture();bodies=new AssassinShadowBody[2];
            try
            {
                int i=0;foreach(var a in new[]{CombatAbility.ShadowWarrior,CombatAbility.ShadowMaster})
                {
                    NpcWorldAllocation.Restore(new(0,NpcWorldAllocation.Profile,1,SourceRandom.InitLow(1)));
                    bodies[i]=AssassinShadowBody.CreateInitialized(owner,a,TimedEffects.Now);GeneratedNpcLiveResources.ActivateItemEvents(bodies[i]);ShadowEquipmentDefense.Activate(bodies[i]);i++;
                }
            }
            finally{NpcWorldAllocation.Restore(world);}
        }
        foreach(var b in bodies){Numeric(b,owner);SkillHealth.SetBalance256(b,b.MaximumLife256*2/3-1);PersistentMinions.Park(b);}
        GeneratedNpcLiveResources.Snapshot();PersistentMinions.Suspend(owner);owner.Internalize();NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());
        File.WriteAllText(Manifest,Json(new Saved(owner.Serial.Value,bodies.Select(Capture).ToArray(),generation)));

        Log.Information("Own Shadow kick: original NPC body level/boots, both actual weapon damage-list suppression with nondamage stats retained, primary restoration, saved actor-only physical RNG and exact original resource/item/wound persistence; typed fill, startup/repeated kick action and progressive emission remain gated.");
    }
}
