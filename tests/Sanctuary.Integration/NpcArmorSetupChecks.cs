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

public static class NpcArmorSetupChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(NpcArmorSetupChecks));
    private sealed record Row(uint Source,uint Target,int Life,int SourceLife,ulong Seed,IronGolemContact.Saved Packet);
    private sealed record Saved(uint Owner,uint Revive,uint Golem,uint Material,uint Weapon,string Binding,Row[] Rows,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-armor-setup-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public sealed class Recipient:ReviveEventChecks.Recipient
    {
        public override MonsterProfile CombatProfile=>base.CombatProfile with{Defense=350};
        [Constructible]public Recipient(){}
        public Recipient(Serial serial):base(serial){}
        public override void Serialize(IGenericWriter writer){base.Serialize(writer);writer.Write(0);}
        public override void Deserialize(IGenericReader reader){base.Deserialize(reader);reader.ReadInt();}
    }
    public static void Configure()
    {
        Register("npc-armor-setup-check",Check);Register("npc-armor-setup-reload-check",Reload);
        ConsoleInputHandler.RegisterCommand("npc-armor-setup-live","Actual original A1 armor-before-HP",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("npc-armor-setup-live-result",Live)));
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"NPC flat defense reduction during allocation",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"npc-armor-setup-source-cases.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var p=row[0];var q=row[1];int delta=p[4].GetInt32(),src=p[5].GetInt32(),initial=p[6].GetInt32();
            bool applies=(p[0].GetInt32()==0||p[2].GetInt32()==2)&&p[1].GetInt32()==1&&delta!=0;
            int armor=applies?Math.Max(0,p[3].GetInt32()+delta):p[3].GetInt32();
            Require(q[0].GetInt32()==armor&&q[1].GetInt32()==(applies?1:0)&&q[2].GetInt32()==armor,"Complete fill armor/RNG phase changed");
            Require(q[3].GetInt32()==(initial!=0?initial:(src==0?128:src)*p[7].GetInt32()/128),"Complete raw stun fill changed");count++;
        }
        Require(count==17280,"Missing full armor/stun source contexts");
        foreach(int value in new[]{-100,-50,-1,0})
        {Require(NpcCombatEvents.FromStats([new(EquipmentStatKey.Pack(120),value)]).Subscriptions().Length==0,"Armor setup became a registered event");}
        foreach(var input in new[]{new EquipmentStatValue(EquipmentStatKey.Pack(120),1),new EquipmentStatValue(EquipmentStatKey.Pack(120,1),-100),new EquipmentStatValue(EquipmentStatKey.Pack(66),25)})
        {bool no=false;try{_=NpcCombatEvents.FromStats([input]);}catch(NotSupportedException){no=true;}Require(no,"Unsupported armor/stun input was discarded");}
    }
    private static Recipient Target(PlayerMobile owner,BaseCreature source)
    {
        Require(source.Map==owner.Map&&source.Map!=Map.Internal,"Armor contact fixture needs an explicitly placed source");
        var target=new Recipient{NoKillAwards=true,Frozen=true,CantWalk=true};target.SetHits(60000);
        foreach(var resistance in Enum.GetValues<ResistanceType>()){target.SetResistance(resistance,0);}
        target.MoveToWorld(new(source.X+1,source.Y,source.Z),owner.Map);target.AIObject.AITimer.Stop();
        Require(CreatureProfiles.Defense(target)==350&&NpcEquipmentCombat.Defense(target)==0&&ClassStats.Defense(target)==350,"Armor recipient needs its explicit350base");return target;
    }
    private static void Clean(Recipient target){TimedEffects.Clear(target);target.Corpse?.Delete();target.Delete();}
    private static IronGolemContact.Prepared Prepare(PlayerMobile owner,BaseCreature source,Recipient target,int flags,int expectedLoss)
    {
        int hp=SkillHealth.Balance256(target),before=TimedEffects.DefenseLoss(target);var seed=NpcEquipmentRandom.State(source);
        var rules=IronGolemContact.Require(source,owner,target);
        Require(TimedEffects.DefenseLoss(target)==before&&NpcEquipmentRandom.State(source)==seed,"Armor preflight mutated target or RNG");int draws=0;
        void Observe(){draws++;Require(TimedEffects.DefenseLoss(target)==expectedLoss&&SkillHealth.Balance256(target)==hp,"Armor was not committed before damage RNG,or HP arrived during setup");}
        int Limited(int bound){Observe();return NpcEquipmentRandom.Limited(source,bound);}
        uint Raw(){Observe();return NpcEquipmentRandom.Raw(source);}
        var impact=source is SkeletonGuardian guardian?NpcEquipmentImpact.Prepare(guardian,target,flags,Limited,Raw):ReviveEquipmentImpact.Prepare(source,target,flags,Limited,Raw);
        Require(TimedEffects.DefenseLoss(target)==expectedLoss&&SkillHealth.Balance256(target)==hp&&(NpcEquipmentImpact.Fills(flags)?draws>0:draws==0),"Armor fill/skip qualification changed");return new(rules,impact);
    }
    private static void Exercise(PlayerMobile owner,BaseCreature source)
    {
        Require(NpcArmorOnHit.Delta(source)==-100&&NpcCombatEvents.ForSource(source).Subscriptions().SequenceEqual(new[]{136}),"Stone Crusher lost its raw armor reduction or independent CB");
        foreach(int old in new[]{0,250,300,349,350})
        {
            var target=Target(owner,source);try
            {TimedEffects.ReduceDefense(target,old,TimedEffects.Now);var seed=NpcEquipmentRandom.State(source);NpcArmorOnHit.Apply(source,target);Require(TimedEffects.DefenseLoss(target)==Math.Min(350,old+100)&&ClassStats.Defense(target)==Math.Max(0,350-old-100)&&NpcEquipmentRandom.State(source)==seed,"Persistent armor stack/zero floor or no-RNG setup differs");}
            finally{Clean(target);}
        }
        foreach(int flags in new[]{0,16,129})
        {var target=Target(owner,source);try{var p=Prepare(owner,source,target,flags,0);p.Cancel();Require(TimedEffects.DefenseLoss(target)==0,"Miss/block/no-fill mutated armor");}finally{Clean(target);}}
        var victim=Target(owner,source);
        try
        {
            int hp=SkillHealth.Balance256(victim);var first=Prepare(owner,source,victim,1,100);var codec=IronGolemContact.Capture(first);var seed=NpcEquipmentRandom.State(source);
            var restored=IronGolemContact.Restore(JsonSerializer.Deserialize<IronGolemContact.Saved>(JsonSerializer.Serialize(codec)));restored.Cancel();first.Cancel();
            Require(TimedEffects.DefenseLoss(victim)==100&&SkillHealth.Balance256(victim)==hp&&NpcEquipmentRandom.State(source)==seed,"Cancel/codec replayed or rolled back allocated armor loss");
            var escaped=Prepare(owner,source,victim,1,200);seed=NpcEquipmentRandom.State(source);
            _=IronGolemContact.Release(source,owner,victim,escaped,_=>false,TimedEffects.Now);
            Require(TimedEffects.DefenseLoss(victim)==200&&SkillHealth.Balance256(victim)==hp&&NpcEquipmentRandom.State(source)==seed,"Out-of-range release replayed armor or damage");
            var hit=Prepare(owner,source,victim,1,300);var result=IronGolemContact.Release(source,owner,victim,hit,_=>true,TimedEffects.Now);
            Require(TimedEffects.DefenseLoss(victim)==300&&victim.Notifications==1&&SkillHealth.Balance256(victim)<hp,"Contact did not preserve one setup before ordinary damage: loss="+TimedEffects.DefenseLoss(victim)+", notifications="+victim.Notifications+", life="+SkillHealth.Balance256(victim)+"/"+hp+", source="+source.GetType().Name+", result="+JsonSerializer.Serialize(result)+", hostile="+ClassCombat.IsHostile(owner,victim)+", town="+Server.Spells.SpellHelper.IsTown(victim.Location,owner)+", killable="+hit.Rules.Metadata.Killable+", canDamage="+victim.CanBeDamaged()+", maps="+source.Map+"/"+victim.Map);
            Prepare(owner,source,victim,1,350).Cancel();Require(ClassStats.Defense(victim)==0,"Repeated setup failed zero floor");
        }
        finally{Clean(victim);}
        var rawTarget=Target(owner,source);
        try
        {
            var roll=source is SkeletonGuardian guardian?NpcEquipmentDamage.Roll(guardian,rawTarget):ReviveEquipmentOffense.Roll(source,rawTarget);bool no=false;
            try{_=NpcEquipmentImpact.FromRoll(source,rawTarget,1,roll);}catch(NotSupportedException){no=true;}
            Require(no&&TimedEffects.DefenseLoss(rawTarget)==0&&rawTarget.Notifications==0,"Unstaged rolled packet guessed late armor setup");
        }
        finally{Clean(rawTarget);}
    }
    private static Row Retain(PlayerMobile owner,BaseCreature source)
    {
        var target=Target(owner,source);var packet=Prepare(owner,source,target,1,100);var encoded=IronGolemContact.Capture(packet);
        // Saved contact data is an explicit codec control,not an active runtime
        // swing. The existing retained A1 family covers actual pending saves.
        target.MoveToWorld(new(7105,3320,0),Map.Felucca);target.AIObject.AITimer.Stop();
        SkillHealth.SetBalance256(source,SkillHealth.Maximum256(source)*2/3+17);
        return new(source.Serial.Value,target.Serial.Value,SkillHealth.Balance256(target),SkillHealth.Balance256(source),NpcEquipmentRandom.State(source).Seed,encoded);
    }
    private static void Check()
    {
        Source();var owner=ReviveEquippedChecks.Owner(3320);MagePetBuffChecks.Train(owner,"necromancer.irongolem",1);
        while(owner.RawStr<250){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Strength),"Actual Stone Crusher requirements");}
        var weapon=ReviveEquippedChecks.Named("Stone Crusher");owner.AddToBackpack(weapon);Require(owner.EquipItem(weapon),"Stone Crusher player control equip failed");ClassGear.Refresh(owner);
        var loot=ReviveEquippedChecks.Named("Stone Crusher");var corpse=ReviveEquippedChecks.Die(owner,new Skeleton(),[loot]);var revived=ReviveOriginals.Find(corpse);
        Require(ReviveCasts.Plan(owner,corpse,out _,out _,out var issue),"Stone Crusher Revive preflight: "+issue);NpcPreventHealChecks.Start(owner,CombatAbility.Revive,corpse);Require(loot.Parent==corpse&&revived.Alive,"Armor Revive lost original/loot");loot.Delete();
        var material=ReviveEquippedChecks.Named("Stone Crusher");material.MoveToWorld(new(owner.X+4,owner.Y,owner.Z),owner.Map);
        Require(IronGolemCasting.Plan(owner,material,out _,out issue),"Stone Crusher Iron preflight: "+issue);NpcPreventHealChecks.Start(owner,CombatAbility.IronGolem,material);
        var golem=NecromancerGolems.For(owner).OfType<IronGolemBody>().Single();Require(material.Parent==golem,"Armor original material lost");
        var rows=new BaseCreature[]{revived,golem}.Select(source=>{source.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);source.Blessed=false;source.AIObject.AITimer.Stop();Exercise(owner,source);return Retain(owner,source);}).ToArray();
        var saved=new Saved(owner.Serial.Value,revived.Serial.Value,golem.Serial.Value,material.Serial.Value,weapon.Serial.Value,ReviveEquipmentLife.Identity(ReviveNativeActivation.State(revived)),rows);
        PersistentMinions.Suspend(owner);owner.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        Require(World.FindItem((Serial)saved.Material)?.Parent==World.FindMobile((Serial)saved.Golem)&&World.FindItem((Serial)saved.Weapon)?.Parent==owner
            &&ReviveEquipmentLife.Identity(ReviveNativeActivation.State((BaseCreature)World.FindMobile((Serial)saved.Revive)))==saved.Binding,"Armor original items/binding changed");
        foreach(var row in saved.Rows)
        {
            var source=(BaseCreature)World.FindMobile((Serial)row.Source);var target=(Recipient)World.FindMobile((Serial)row.Target);
            Require(source.Map==Map.Internal&&source.ControlMaster==owner&&SkillHealth.Balance256(source)==row.SourceLife&&SkillHealth.Balance256(target)==row.Life
                &&TimedEffects.DefenseLoss(target)==100&&ClassStats.Defense(target)==250&&NpcEquipmentRandom.State(source).Seed==row.Seed,"Saved armor/HP/source stream changed");
            var restored=IronGolemContact.Restore(row.Packet);restored.Cancel();
            Require(TimedEffects.DefenseLoss(target)==100&&SkillHealth.Balance256(target)==row.Life&&NpcEquipmentRandom.State(source).Seed==row.Seed,"Restored prepared-contact data applied armor twice");
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
    }
    private static void Live(NetState net)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        var source=(BaseCreature)World.FindMobile((Serial)saved.Revive);var golem=(IronGolemBody)World.FindMobile((Serial)saved.Golem);bool frozen=golem.Frozen;source.Frozen=golem.Frozen=true;
        FixtureNetwork.Authenticate(net,owner,"armor-live-");owner.MoveToWorld(new(7100,3320,0),Map.Trammel);PersistentMinions.Resume(owner);
        golem.Frozen=true;source.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);source.AIObject.AITimer.Stop();source.Combatant=null;source.FocusMob=null;
        var enemy=Target(owner,source);int hp=SkillHealth.Balance256(enemy);NpcEquipmentRandom.Set(source,ReviveNativeSwingChecks.HitSeed());ReviveSwings.Cancel(source,false);source.Frozen=false;
        source.IssueOrder(OrderType.Guard,null);source.AIObject.AITimer.Start();bool observed=false;var deadline=TimedEffects.Now.AddSeconds(20);
        void CleanLive(){source.AIObject.AITimer.Stop();source.Combatant=null;source.FocusMob=null;Clean(enemy);golem.Frozen=frozen;PersistentMinions.Suspend(owner);owner.Internalize();}
        void Inspect()
        {
            try
            {
                Require(net.Running&&TimedEffects.Now<deadline,"Armor A1 live timeout");var swing=ReviveSwings.State(source);
                if(swing?.Pending!=null)
                {Require(swing.Target==enemy.Serial.Value&&TimedEffects.DefenseLoss(enemy)==100&&SkillHealth.Balance256(enemy)==hp,"Live armor setup was late,duplicated or dealt early HP");observed=true;}
                if(SkillHealth.Balance256(enemy)<hp)
                {
                    Require(observed&&enemy.Notifications==1&&TimedEffects.DefenseLoss(enemy)==100,"Live contact lacked its one prior armor allocation");CleanLive();
                    var rows=saved.Rows.Select(r=>r with{SourceLife=SkillHealth.Balance256(World.FindMobile((Serial)r.Source)),Seed=NpcEquipmentRandom.State((BaseCreature)World.FindMobile((Serial)r.Source)).Seed}).ToArray();
                    File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Rows=rows}));Log.Information("npc-armor-setup-live-result: passed");return;
                }
                Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Inspect));
            }
            catch(Exception e){CleanLive();Log.Error(e,"npc-armor-setup-live-result: failed");}
        }
        Log.Information("npc-armor-setup-live: ready");Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Inspect));
    }
}
