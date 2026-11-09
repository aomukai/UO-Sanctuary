// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text.Json;
using Server;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class CleaveCastChecks
{
    private const CombatAbility Ability=CombatAbility.Cleave;
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(CleaveCastChecks));
    private sealed record Row(uint Owner,uint Weapon,uint Target,bool Female,int X,int Y,int Mana256,int Life256);
    private sealed record Saved(int Generation,Row[] Rows);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"cleave-cast-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Learned approved Cleave and real input",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure()
    {
        Register("cleave-cast-check",()=>Check(false));Register("cleave-cast-reload-check",()=>Check(true));
        Register("cleave-cast-presave-check",PreSave);
        foreach(string value in new[]{"left","right","quick"}){string mode=value;Register("cleave-cast-"+mode+"-check",()=>FixtureNetwork.Connected("cleave-cast-"+mode+"-result",n=>Input(n,mode)));}
    }
    private static void Bind(PlayerMobile p)
    {
        Require(WarlockCombat.Available(p,Ability)&&WarlockCombat.Implemented(Ability)&&CombatTiming.WeaponAttack(Ability)
            &&CombatTiming.Range(p,Ability)==PlayerMeleeGeometry.SearchRange(p),"Cleave learned availability/timing/range metadata");
        Require(SkillBindings.Instance.Assign(p,true,Ability)&&SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Cleave left/right/quick assignment");
    }
    private static void PublicAction(PlayerMobile p,CleaveActionTarget target)
    {
        CleaveActionChecks.Ready(p);int mana=NecromancerEffects.ManaBalance256(p),hp=SkillHealth.Balance256(target);
        var hit=CleaveActionProposal.HitRoll;var block=SourceBlocking.BlockRoll;CleaveActionProposal.HitRoll=()=>0;SourceBlocking.BlockRoll=n=>n-1;
        try
        {
            Require(WarlockCombat.Begin(p,Ability,target.Serial,target.Location,Core.TickCount),"Cleave learned begin");var a=ClassCombat.For(p).Cleave;
            Require(a!=null&&CombatTiming.For(p,Ability)==a.Timing&&mana==NecromancerEffects.ManaBalance256(p),"Cleave learned captured clock/unpaid entry");
            Require(ClassCombat.ResolveAction(p,a.EffectAt)&&a.Released&&a.Successful==1&&hp>SkillHealth.Balance256(target)&&mana-NecromancerEffects.ManaBalance256(p)==3*256,"Cleave learned ordered release/payment");
            ClassCombat.ResolveAction(p,a.Until);Require(a.Finished,"Cleave learned end");
        }
        finally{CleaveActionProposal.HitRoll=hit;SourceBlocking.BlockRoll=block;ClassCombat.Stop(p);}
    }
    private static void Check(bool reload)
    {
        Saved saved;
        if(reload){saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));}
        else
        {
            var rows=new Row[2];for(int i=0;i<2;i++)
            {var p=CleaveActionChecks.Fresh(i==1);var t=CleaveActionChecks.Enemy(p,1);Bind(p);rows[i]=new(p.Serial.Value,CombatTiming.Equipped(p).Serial.Value,t.Serial.Value,p.Female,p.X,p.Y,0,0);}saved=new(0,rows);
        }
        var next=new Row[saved.Rows.Length];for(int i=0;i<saved.Rows.Length;i++)
        {
            var row=saved.Rows[i];var p=World.FindMobile((Serial)row.Owner)as PlayerMobile;var t=World.FindMobile((Serial)row.Target)as CleaveActionTarget;
            Require(p!=null&&t!=null&&p.Female==row.Female&&CombatTiming.Equipped(p)?.Serial.Value==row.Weapon,"Original learned Cleave Warlock/Witch identity or weapon missing");
            if(reload)
            {
                Require(NecromancerEffects.ManaBalance256(p)==row.Mana256&&SkillHealth.Balance256(p)==row.Life256,
                    $"Original learned Cleave wounds/mana changed owner={p.Serial} name={p.Name} mana={NecromancerEffects.ManaBalance256(p)}/{row.Mana256} life={SkillHealth.Balance256(p)}/{row.Life256} maxima={p.ManaMax}/{p.HitsMax}");
                var b=SkillBindings.Instance.Get(p);Require(b.Left==Ability&&b.Right==Ability&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Warlock,7)==Ability,"Original Cleave bindings changed");
                FixtureSpace.Place(p,new(row.X,row.Y,0),Map.Trammel);t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();
            }
            Bind(p);PublicAction(p,t);
            // The resource persistence control is no longer a live fight.
            // Keep the original target,without autonomous follow-up contacts.
            p.Combatant=null;t.Combatant=null;t.AIObject.AITimer.Stop();t.MoveToWorld(t.Location,Map.Internal);
            SkillHealth.SetBalance256(p,p.HitsMax*256-129);NecromancerEffects.SetMana256(p,p.ManaMax*256-17);next[i]=row with{X=p.X,Y=p.Y,Mana256=NecromancerEffects.ManaBalance256(p),Life256=SkillHealth.Balance256(p)};
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(saved.Generation+(reload?1:0),next)));
        Log.Information("Cleave learned:approved route/physical clock/range metadata,left-right-quick bindings,ordinary ordered contacts,once3mana and original Warlock/Witch weapons/resources pass.");
    }
    private static void PreSave()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));foreach(var row in saved.Rows)
        {
            var p=(PlayerMobile)World.FindMobile((Serial)row.Owner);var t=(CleaveActionTarget)World.FindMobile((Serial)row.Target);
            Require(t.Map==Map.Internal&&t.Combatant==null&&p.Combatant==null&&NecromancerEffects.ManaBalance256(p)==row.Mana256&&SkillHealth.Balance256(p)==row.Life256,
                $"Cleave resource control changed before save owner={p.Serial} mana={NecromancerEffects.ManaBalance256(p)}/{row.Mana256} life={SkillHealth.Balance256(p)}/{row.Life256}");
        }
    }
    private static void Input(NetState network,string mode)
    {
        var p=CleaveActionChecks.Fresh(mode=="right");var t=CleaveActionChecks.Enemy(p,1);Bind(p);var account=FixtureNetwork.Authenticate(network,p,"cleave-wire-");CleaveActionChecks.Ready(p);
        string label="cleave-cast-"+mode;int mana=NecromancerEffects.ManaBalance256(p),hp=SkillHealth.Balance256(t);long deadline=Core.TickCount+7000;bool done=false;CleaveActionProposal.Action action=null;
        var hit=CleaveActionProposal.HitRoll;var block=SourceBlocking.BlockRoll;CleaveActionProposal.HitRoll=()=>0;SourceBlocking.BlockRoll=n=>n-1;
        void Cleanup(){CleaveActionProposal.HitRoll=hit;SourceBlocking.BlockRoll=block;ClassCombat.Stop(p);p.Delete();t.Delete();account.Delete();}
        void Inspect()
        {
            if(done){return;}try
            {
                if(Core.TickCount>=deadline){throw new InvalidOperationException("Cleave wire timeout");}
                if(action==null&&ClassCombat.For(p).Cleave is{} a)
                {action=a;Require(!a.Released&&mana==NecromancerEffects.ManaBalance256(p),"Cleave wire entry spent mana");Log.Information("{Label}: windup",label);}
                if(action?.Finished==true)
                {
                    Require(!action.Cancelled&&action.Contacts==1&&action.Successful==1&&hp>SkillHealth.Balance256(t)&&mana-NecromancerEffects.ManaBalance256(p)==3*256,"Cleave wire contact/payment/end");
                    done=true;Cleanup();Log.Information("{Label}-result: passed",label);return;
                }
                Timer.StartTimer(TimeSpan.FromMilliseconds(10),()=>Core.LoopContext.Post(Inspect));
            }
            catch(Exception e){done=true;Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(()=>
        {
            if(mode=="right"){Log.Information("{Label}: ready ability={Ability} class={Class} x={X} y={Y} z={Z}",label,(int)Ability,(int)DiabloClass.Warlock,t.X,t.Y,t.Z);}
            else{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)Ability,(int)DiabloClass.Warlock,t.Serial.Value,t.X,t.Y,t.Z);}Inspect();
        }));
    }
}
