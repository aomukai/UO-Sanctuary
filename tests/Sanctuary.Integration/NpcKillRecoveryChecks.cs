// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;
public static class NpcKillRecoveryChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(NpcKillRecoveryChecks));
    private sealed record Held(uint Owner,uint Source,uint Item,string Gear,string Kind,string Numeric,int Life,int Mana,int OwnerLife,int OwnerMana,ulong Seed,bool Revive);
    private sealed record Saved(Held[] Actors,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-kill-recovery-fixtures.json");
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public sealed class RecoveryOwner:PlayerMobile
    {
        [Constructible]public RecoveryOwner(){}
        public RecoveryOwner(Serial serial):base(serial){}
        public override bool CanRegenHits=>false;
        public override bool CanRegenMana=>false;
        public override bool CanRegenStam=>false;
        public override void Serialize(IGenericWriter writer){base.Serialize(writer);writer.Write(0);}
        public override void Deserialize(IGenericReader reader){base.Deserialize(reader);reader.ReadInt();}
    }
    private static Item Original(string key)=>NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey==key).Id,99,new Random(173),true);
    private static void Differential()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"npc-kill-recovery-cases.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var a=row.EnumerateArray().Select(v=>v.GetInt32()).ToArray();bool eligible=a[1]!=0&&a[2]!=0&&(a[0]!=1||a[3]==0)&&(a[0]!=2||a[6]!=0);bool pools=eligible&&a[4]!=0,writes=pools&&a[5]<10000;
            int after=writes?Math.Clamp(a[5]+a[4]*256,0,10000):a[5];
            Require(a[7]==(writes?1:0)&&a[8]==after&&a[9]==(eligible?1:0)&&a[10]==(pools?1:0)&&a[11]==(pools?1:0)&&a[12]==(writes?1:0)&&a[13]==(writes?(a[0]==1?152:151):0)&&a[14]==(a[0]==2&&a[1]!=0&&a[2]!=0?1:0),"Complete kill recovery callback differs at "+count);count++;
        }
        Require(count==1728,"Incomplete three-callback source matrix");Require(NpcCombatEvents.RestoreSubscriptions([86,138,139]).Subscriptions().Order().SequenceEqual(new[]{86,138,139}),"Kill recovery subscription restore");
    }
    private static string Code(string key)=>key=="Deathcleaver"?"heal-kill":key=="Deathspade"?"mana-kill":"demon-heal";
    private static void Direct(PlayerMobile p,BaseCreature source,string key)
    {
        int amount=NpcEquipmentCombat.EventBonus(source,Code(key));Require(amount>0,"Original kill-recovery property absent");var heldLife=SkillHealth.Balance256(source);var seed=NpcEquipmentRandom.State(source);int ownerLife=SkillHealth.Balance256(p),ownerMana=NecromancerEffects.ManaBalance256(p),mana=NecromancerEffects.ManaBalance256(source);
        var ordinary=new Skeleton();var demon=new Daemon();Require(!NamedCombat.Demon(ordinary)&&NamedCombat.Demon(demon),"Existing ordinary/demon classification service differs");
        var packet=new PreparedMonsterDamagePacket(256,256,0,0,3);int maximum=SkillHealth.Maximum256(source);
        try
        {
            foreach(var target in new BaseCreature[]{ordinary,demon})foreach(int missing in new[]{0,1,amount*256+17})
            {
                SkillHealth.SetBalance256(source,maximum-missing);int before=SkillHealth.Balance256(source);
                Require(NpcCombatEvents.ForSource(source).Dispatch("kill",packet,source,target,TimedEffects.Now)==packet,"Resource callback changed kill packet");
                int healing=key=="Deathcleaver"||key=="Heaven's Light"&&NamedCombat.Demon(target)?amount*256:0;
                Require(SkillHealth.Balance256(source)==Math.Min(maximum,before+healing)&&NecromancerEffects.ManaBalance256(source)==mana,"Direct kill callback lost FP fraction/cap/demon/player-only rule");
            }
            Require(NpcEquipmentRandom.State(source)==seed&&SkillHealth.Balance256(p)==ownerLife&&NecromancerEffects.ManaBalance256(p)==ownerMana,"NPC kill recovery consumed RNG or forwarded to owner");
        }
        finally{SkillHealth.SetBalance256(source,heldLife);ordinary.Delete();demon.Delete();}
    }
    private static void Kill(PlayerMobile p,BaseCreature source,string key)
    {
        var t=new GraveyardSkeleton{NoKillAwards=true,Frozen=true};t.MoveToWorld(new(source.X+1,source.Y,source.Z),p.Map);t.AIObject?.AITimer.Stop();foreach(var type in Enum.GetValues<ResistanceType>()){t.SetResistance(type,0);}SkillHealth.SetBalance256(t,256);
        int bonus=NpcEquipmentCombat.EventBonus(source,Code(key));SkillHealth.SetBalance256(source,SkillHealth.Maximum256(source)-bonus*256-123);int before=SkillHealth.Balance256(source),ownerLife=SkillHealth.Balance256(p),ownerMana=NecromancerEffects.ManaBalance256(p),mana=NecromancerEffects.ManaBalance256(source);var block=SourceBlocking.BlockRoll;var choice=SourceBlocking.ChoiceRoll;
        try
        {
            SourceBlocking.BlockRoll=n=>n-1;SourceBlocking.ChoiceRoll=n=>Math.Min(1,n-1);var rules=IronGolemContact.Require(source,p,t);IronGolemContact.Prepared hit=null;bool found=false;
            for(ulong seed=1;seed<10000&&!found;seed++){NpcEquipmentRandom.Set(source,seed);hit=IronGolemContact.Prepare(source,t,rules,()=>5);found=(hit.Impact.Packet.ResultFlags&1)!=0;if(!found){hit.Cancel();}}
            Require(found,"No ordinary kill contact");ulong expected=NpcEquipmentRandom.State(source).Seed;
            if(key is "Deathspade" or "Heaven's Light"){SourceRandom.Roll(ref expected);} // Existing outgoing blind callback precedes HP.
            IronGolemContact.Release(source,p,t,hit,_=>true,TimedEffects.Now);
            Require(!t.Alive&&t.LastKiller==source&&SkillHealth.Balance256(source)==before+(key=="Deathcleaver"?bonus*256:0)&&NecromancerEffects.ManaBalance256(source)==mana&&NpcEquipmentRandom.State(source).Seed==expected&&SkillHealth.Balance256(p)==ownerLife&&NecromancerEffects.ManaBalance256(p)==ownerMana,"Actual direct NPC kill changed recovery/owner credit/FP/RNG order");
        }
        finally{SourceBlocking.BlockRoll=block;SourceBlocking.ChoiceRoll=choice;t.Corpse?.Delete();t.Delete();}
    }
    private static Held Hold(PlayerMobile p,BaseCreature source,Item item,string key,bool revive)
    {
        var numeric=NpcItemStatCapture.Capture(item);
        if(key=="Heaven's Light")
        {
            Require(numeric.ItemBase.Single(v=>v.Stat==194).Value==ClassGear.State(item).NamedSockets
                &&numeric.Properties.Single(v=>v.Packed==EquipmentStatKey.Pack(83,3)).Value==new ItemStatContext(item,99).Bonus("pal"),"Original socket/class-skill properties were stripped or mislayered");
        }
        Direct(p,source,key);Kill(p,source,key);TimedEffects.PreventHealing(source,TimedEffects.Now);source.AIObject?.AITimer.Stop();PersistentMinions.Suspend(p);p.Internalize();
        return new(p.Serial.Value,source.Serial.Value,item.Serial.Value,Json(ClassGear.State(item)),key,Json(NpcItemStatCapture.Capture(item)),SkillHealth.Balance256(source),NecromancerEffects.ManaBalance256(source),SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),NpcEquipmentRandom.State(source).Seed,revive);
    }
    private static PlayerMobile Owner()
    {
        var p=NpcItemKnockbackChecks.Owner(new RecoveryOwner());Require(!p.CanRegenHits&&!p.CanRegenMana,"Recovery fixture must disable native recovery from construction");p.Name="NpcKillRecoveryOwner [fixture]";
        while(p.RawStr<150){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Owner stat allocation");}while(p.RawDex<70){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Owner Dexterity allocation");}
        var gear=Original("Deathcleaver");p.AddToBackpack(gear);Require(p.EquipItem(gear),"Owner real life-after-kill gear");var boots=Original("Silkweave");p.AddToBackpack(boots);Require(p.EquipItem(boots)&&ClassGear.Bonus(p,"mana-kill")>0,"Owner real mana-after-kill gear");return p;
    }
    private static void Check(NetState net,bool reload)
    {
        Differential();
        if(!reload)
        {
            var rows=new List<Held>();foreach(string key in new[]{"Deathcleaver","Deathspade","Heaven's Light"})
            {
                foreach(bool graph in new[]{false,true})
                {
                    var p=Owner();var item=Original(key);var source=NpcItemKnockbackChecks.Cast(p,item,Code(key),new ItemStatContext(item,99).Bonus(Code(key)));source.SuspendActions();
                    if(graph){NpcItemProcs.Create(source);NpcItemProcs.Refresh(source,NpcCombatEvents.LegacyForSource(source));}
                    SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-1025);NecromancerEffects.SetMana256(p,257);rows.Add(Hold(p,source,item,key,false));
                }
                var q=Owner();FixtureNetwork.Authenticate(net,q,"npc-kill-");var sample=Original(key);int expectedBonus=new ItemStatContext(sample,99).Bonus(Code(key));sample.Delete();var revived=NpcFreezeHitChecks.Revive(q,out var gear,key,Code(key),expectedBonus);
                SkillHealth.SetBalance256(q,SkillHealth.Maximum256(q)-1025);NecromancerEffects.SetMana256(q,257);rows.Add(Hold(q,revived,gear,key,true));
            }
            File.WriteAllText(Manifest,Json(new Saved(rows.ToArray(),0)));
        }
        else
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));FixtureNetwork.Authenticate(net,(PlayerMobile)World.FindMobile((Serial)saved.Actors[0].Owner),"npc-kill-reload-");
            foreach(var r in saved.Actors)
            {
                var p=(PlayerMobile)World.FindMobile((Serial)r.Owner);var source=(BaseCreature)World.FindMobile((Serial)r.Source);var item=World.FindItem((Serial)r.Item);
                Require(source is{Alive:true,Deleted:false}&&source.ControlMaster==p&&SkillHealth.Balance256(source)==r.Life&&NecromancerEffects.ManaBalance256(source)==r.Mana&&SkillHealth.Balance256(p)==r.OwnerLife&&NecromancerEffects.ManaBalance256(p)==r.OwnerMana&&NpcEquipmentRandom.State(source).Seed==r.Seed&&Json(ClassGear.State(item))==r.Gear&&Json(NpcItemStatCapture.Capture(item))==r.Numeric,"Original kill gear/wounds/pet-owner resources/seed changed on reload: kind="+r.Kind+" revive="+r.Revive+" source="+r.Source+" living="+(source is{Alive:true,Deleted:false})+" control="+(source?.ControlMaster==p)+" life="+SkillHealth.Balance256(source)+"/"+r.Life+" mana="+NecromancerEffects.ManaBalance256(source)+"/"+r.Mana+" ownerLife="+SkillHealth.Balance256(p)+"/"+r.OwnerLife+" ownerMana="+NecromancerEffects.ManaBalance256(p)+"/"+r.OwnerMana+" seed="+NpcEquipmentRandom.State(source).Seed+"/"+r.Seed+" gear="+(Json(ClassGear.State(item))==r.Gear)+" numeric="+(Json(NpcItemStatCapture.Capture(item))==r.Numeric));
                Direct(p,source,r.Kind); // Execute the restored original subscription without replaying an old kill.
            }
            File.WriteAllText(Manifest,Json(saved with{Generation=saved.Generation+1}));
        }
        Log.Information("NPC kill recovery:1728source cases;original life/demon-life and player-only mana callbacks;direct melee credit/no owner forwarding;9wounded original actors persist and restored events execute");
    }
    public static void Configure()
    {
        foreach(string name in new[]{"npc-kill-recovery-check","npc-kill-recovery-reload-check"})ConsoleInputHandler.RegisterCommand(name,"NPC original gear kill recovery",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected(name,net=>
        {try{Check(net,name.EndsWith("-reload-check",StringComparison.Ordinal));Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}})));
    }
}
