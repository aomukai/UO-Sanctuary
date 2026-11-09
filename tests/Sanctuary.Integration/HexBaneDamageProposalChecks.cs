// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;
public static class HexBaneDamageProposalChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(HexBaneDamageProposalChecks));
    private sealed record Saved(uint Owner,uint Weapon,bool Female,HexBaneDamagePlan Plan,HexBaneBuff Legacy);
    private sealed record Manifest(int Generation,Saved[] Owners);
    private static string FileName=>Path.Join(Core.BaseDirectory,"bane-damage-proposal-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Prepared Bane mastery/timing rule",_=>Core.LoopContext.Post(()=>
        {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
        Register("bane-damage-proposal-check",false);Register("bane-damage-proposal-reload-check",true);
    }
    private static SkillValues Values(int rank,int synergy)=>new(SkillMechanics.For(CombatAbility.HexBane),k=>k=="Hex Bane"?rank:0,k=>k=="Hex Purge"?synergy:0,99);
    private static void Model()
    {
        int cases=0;
        foreach(int rank in new[]{1,20,40})foreach(int synergy in new[]{0,1,20})foreach(int mastery in new[]{0,10,50})
        {
            var v=Values(rank,synergy);var p=HexBaneDamageProposal.Capture(v,mastery);int min=v.Damage256(false),max=v.Damage256(true);
            Require(p.Rank==rank&&p.CapturedMagicMastery==mastery&&p.Values.Minimum==(min+(long)min*mastery/100)/256
                &&p.Values.Maximum==(max+(long)max*mastery/100)/256,"Bane proposed mastery ordering");
            if(mastery==0){Require(p.Values==HexBane.Capture(v),"Zero-mastery legacy compatibility");}cases++;
        }
        Require(cases==27,"Bane proposal context count");
        int rounded=HexBaneDamageProposal.Capture(Values(1,1),50).Values.Maximum;Require(rounded==16,$"Bane fractional synergy/mastery maximum differs:{rounded}");
        Require(HexBaneDamageProposal.Approved&&WarlockCombat.Handles(CombatAbility.HexBane),"Approved Bane admission mismatch");
        Log.Information("Bane damage candidate:27focused cast-stage contexts;synergy/mastery before whole-point storage and zero-mastery legacy values pass;public route stays gated.");
    }
    private static PlayerMobile Fresh(bool female,int x)
    {
        var p=new PlayerMobile{Player=true,Female=female,Body=female?0x191:0x190,Name="BaneDamageProposalFixture"};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Warlock);ClassCreation.Profiles.AdvanceLevel(p,99);
        ClassSkillChecks.Train(p,"warlock.hexbane",20);while(p.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Bane proposal strength");}
        FixtureSpace.Corridor(p,x,24,3500);var weapon=new Broadsword();p.AddToBackpack(weapon);Require(p.EquipItem(weapon),"Bane proposal sword");ClassGear.Refresh(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);
        Require(HexBane.CastPrepared(p,replaceGroup:true),"Zero-mastery legacy registration");return p;
    }
    private static void Equip(PlayerMobile p,Item item){p.AddToBackpack(item);Require(p.EquipItem(item),"Bane proposal equipment");ClassGear.Refresh(p);}
    private static void Contact(PlayerMobile p,HexBaneDamagePlan plan,int suppliedLiveMastery)
    {
        var t=new ShockWaveTarget{Frozen=true,CantWalk=true,NoKillAwards=true};t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();t.SetHits(50000);SkillHealth.SetBalance256(t,t.HitsMax*256);
        try
        {
            var registration=HexBane.Buff(p);Require(registration!=null,"Bane legacy registration missing");int hp=SkillHealth.Balance256(t),defense=ClassStats.Defense(t);
            bool hit=ClassStats.CheckHit(p,t,0,()=>1,notifyMelee:false,npcDefense:false)==true;Require(!hit&&HexBane.Debuff(t)==null,"Bane changed state before hit result");
            Require(!HexBaneDamageProposal.AfterMeleeResult(p,t,registration,false)&&HexBane.Debuff(t)==null,"Non-attempt applied Bane");
            Require(HexBaneDamageProposal.AfterMeleeResult(p,t,registration,true)&&!hit&&SkillHealth.Balance256(t)==hp
                &&ClassStats.Defense(t)==ClassStats.ScaleDefense(CreatureProfiles.Defense(t),registration.Values.DefensePercent)&&ClassStats.Defense(t)<=defense,"Post-result melee miss proposal");
            // Reuse the actual ordinary weapon enhancement/mitigation/HP path.
            // The candidate supplies stored magic explicitly;it does not turn
            // the unapproved cast-stage plan into an active gameplay buff.
            Require(NamedElements.MasteryPercent(p,"mag")==0,"Proposal actor must have zero live mastery");
            var raw=new GearDamage(0,0,0,0,0,Magic:NamedElements.EnhanceAtMastery(plan.Values.Minimum,suppliedLiveMastery));
            Require(raw.Magic==plan.Values.Minimum*Math.Max(0,100+suppliedLiveMastery)/100.0,"Shared delivery mastery arithmetic");
            var expected=SkillCombat.MitigatePrepared(p,t,raw);
            using(CombatItemEvents.Suppress(p,t)){GearEffects.Strike(p,t,0,new Random(173),wearWeapon:false,preparedDamage:raw);}
            Require(hp-SkillHealth.Balance256(t)==NamedElements.To256(expected.Total),"Prepared second-stage damage did not traverse actual weapon mitigation/HP");
        }
        finally{HexBane.Clear(t);t.Delete();}
        Log.Information("Bane damage candidate:actual post-roll miss debuff,shared supplied-mastery arithmetic and prepared weapon mitigation/HP pass;no same-hit reroll and no learned cast enabled.");
    }
    private static void Check(bool reload)
    {
        Model();Manifest manifest;
        if(!reload)
        {
            var rows=new Saved[2];int i=0;
            foreach(bool female in new[]{false,true})
            {
                var p=Fresh(female,female?6640:6560);var legacy=HexBane.Buff(p);
                var plan=HexBaneDamageProposal.Capture(SkillMechanics.Values(p,CombatAbility.HexBane),10);
                Contact(p,plan,10);Contact(p,plan,0);
                Require(HexBane.Buff(p)==legacy,"Candidate changed actual legacy buff");rows[i++]=new(p.Serial.Value,CombatTiming.Equipped(p).Serial.Value,female,plan,legacy);
            }
            manifest=new(0,rows);
        }
        else
        {
            var old=JsonSerializer.Deserialize<Manifest>(File.ReadAllText(FileName));Require(old.Generation is 0 or 1,"Bane proposal reload generation");
            foreach(var row in old.Owners)
            {
                var p=(PlayerMobile)World.FindMobile((Serial)row.Owner);
                Require(p.Female==row.Female&&CombatTiming.Equipped(p)?.Serial.Value==row.Weapon&&HexBane.Buff(p)==row.Legacy
                    &&HexBaneDamageProposal.Capture(SkillMechanics.Values(p,CombatAbility.HexBane),row.Plan.CapturedMagicMastery)==row.Plan,"Original Bane proposal weapon/plan/legacy registration changed");
                FixtureSpace.Corridor(p,row.Female?6640:6560,24,3500);Contact(p,row.Plan,10);
            }
            manifest=old with{Generation=old.Generation+1};
        }
        File.WriteAllText(FileName,JsonSerializer.Serialize(manifest));Log.Information("Bane candidate original:Warlock/Witch original weapon,explicit supplied-mastery plan and unchanged legacy registration persist across two reloads;no gameplay save format or admission added.");
    }
}
