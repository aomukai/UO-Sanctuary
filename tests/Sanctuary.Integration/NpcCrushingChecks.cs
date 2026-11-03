// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class NpcCrushingChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(NpcCrushingChecks));
    private sealed record Saved(uint Owner,uint Source,uint Item,uint Target,int Life,ulong Seed,string Gear);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-crushing-fixtures.json");
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("npc-crushing-check",Check);Register("npc-crushing-reload-check",Reload);}
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Registered NPC136/function16 and original Goblin Toe",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static ulong Seed(int roll)
    {for(ulong n=1;n<100000;n++){ulong next=n;if(SourceRandom.Limited(ref next,100)==roll){return n;}}throw new InvalidOperationException("No NPC Crushing Blow seed");}
    private static void Check()
    {
        var p=IronGolemDamageChecks.Owner(rank:1,mastery:0,row:3380);var item=IronGolemDamageChecks.Original(p,"Goblin Toe");var b=IronGolemDamageChecks.Learned(p,item);
        var t=new IronGolemOrderedContactChecks.Recipient{Source=b,NoKillAwards=true};t.SetHits(20000);t.MoveToWorld(b.Location,p.Map);t.AIObject?.AITimer.Stop();foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}
        var blocks=SourceBlocking.BlockRoll;SourceBlocking.BlockRoll=_=>99;
        try
        {
            Require(item.Parent==b&&NpcCrushing.Chance(b)==25,"Original Goblin Toe CB25");var events=NpcCombatEvents.ForSource(b);
            foreach(bool ranged in new[]{false,true})foreach(int resistance in new[]{-50,0,33,100})foreach(int roll in new[]{24,25})
            {
                t.SetResistance(ResistanceType.Physical,resistance);SkillHealth.SetBalance256(t,1025);int calls=0;
                events.Dispatch(ranged?"domissiledamage":"domeleedamage",null,b,t,TimedEffects.Now,n=>{Require(n==100,"CB wrong RNG bound");calls++;return roll;});
                int share=1025/(ranged?8:4);int expected=roll<25?share-share*Math.Clamp(resistance,0,100)/100:0;
                Require(calls==1&&SkillHealth.Balance256(t)==1025-expected&&t.Notifications==0,"NPC shared fractional CB/ranged/resistance/chance or extra damage event");
            }
            var gear=ClassGear.State(item);ClassGear.Profiles.Store(item,gear with{Identified=false});int skipped=0;events.Dispatch("domeleedamage",null,b,t,TimedEffects.Now,_=>{skipped++;return 0;});Require(skipped==0&&NpcCrushing.Chance(b)==0,"Inactive current original rolled CB");ClassGear.Profiles.Store(item,gear);
            t.SetResistance(ResistanceType.Physical,0);SkillHealth.SetBalance256(t,500000);var rules=IronGolemContact.Require(b,p,t);var prepared=IronGolemContact.Prepare(b,t,rules,()=>5);
            ulong seed=Seed(24),after=seed;SourceRandom.Roll(ref after);NpcEquipmentRandom.Set(b,seed);int hp=SkillHealth.Balance256(t),cb=hp/4;
            IronGolemContact.Release(b,p,t,prepared,_=>true,TimedEffects.Now);
            Require(SkillHealth.Balance256(t)==hp-cb-prepared.Impact.Packet.Total256&&t.Notifications==1&&t.AtNotification==hp-cb
                &&NpcEquipmentRandom.State(b).Seed==after,"Actual registered CB before native packet/once-only notification or extra RNG");
            // One actual original item/source stream and fractional recipient are
            // retained across both process reloads; no new item is generated.
            SkillHealth.SetBalance256(t,1025);NpcEquipmentRandom.Set(b,Seed(24));PersistentMinions.Suspend(p);
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,b.Serial.Value,item.Serial.Value,t.Serial.Value,SkillHealth.Balance256(t),NpcEquipmentRandom.State(b).Seed,JsonSerializer.Serialize(ClassGear.State(item)))));
            b=null;p=null;t=null;
        }
        finally{SourceBlocking.BlockRoll=blocks;t?.Delete();b?.Delete();p?.Delete();}
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=World.FindMobile((Serial)s.Owner) as PlayerMobile;var b=World.FindMobile((Serial)s.Source) as IronGolemBody;
        var item=World.FindItem((Serial)s.Item);var t=World.FindMobile((Serial)s.Target) as BaseCreature;
        Require(p!=null&&b!=null&&item?.Parent==b&&b.ControlMaster==p&&JsonSerializer.Serialize(ClassGear.State(item))==s.Gear&&SkillHealth.Balance256(t)==s.Life
            &&NpcEquipmentRandom.State(b).Seed==s.Seed&&NpcCrushing.Chance(b)==25,"Original CB item/source/fraction/stream changed on reload");
        ulong expected=s.Seed;SourceRandom.Roll(ref expected);NpcCombatEvents.ForSource(b).Dispatch("domeleedamage",null,b,t,TimedEffects.Now);
        Require(SkillHealth.Balance256(t)==s.Life-s.Life/4&&NpcEquipmentRandom.State(b).Seed==expected,"Original NPC CB after reload");
        SkillHealth.SetBalance256(t,s.Life);NpcEquipmentRandom.Set(b,s.Seed); // Explicit inspection rewind for the second reload.
    }
}
