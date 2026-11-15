// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Items;
namespace Sanctuary.Integration;

public static class FlameWaveBirthChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(FlameWaveBirthChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("flame-wave-birth-check","Pinned Flame Wave graph/numeric/tooltip-duration inputs",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("flame-wave-birth-check: passed");}catch(Exception e){Log.Error(e,"flame-wave-birth-check: failed");}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static SkillValues Values(int rank,int ring=0,int apocalypse=0)=>new(SkillMechanics.For(CombatAbility.FlameWave),k=>k=="Flame Wave"?rank:0,
        k=>k=="Ring of Fire"?ring:k=="Apocalypse"?apocalypse:0,99);
    private static void Check()
    {
        var raw=FlameWaveBirths.Numbers(Values(1));Require(raw is{Minimum256:3328,Maximum256:4352,GroundMinimum256:1792,GroundMaximum256:2560,Mana256:1792,HalfWidth:4,DelayFrames:17,WaveFrames:40,TailFrames:8,LingerFrames:75,CooldownFrames:15},"Rank1 raw source golden");
        var one=FlameWaveBirths.Numbers(Values(1,1));var twenty=FlameWaveBirths.Numbers(Values(20,1));
        Require(one.Minimum256/256==14&&one.Maximum256/256==19&&twenty.Minimum256/256==267&&twenty.Maximum256/256==303,"Released-note endpoints including mandatory Ring point");
        Require(one.GroundMinimum256==1792&&one.GroundMaximum256==2816&&twenty.GroundMinimum256==31488&&twenty.GroundMaximum256==42240,"Distinct field synergy truncates before HitShift");
        Require(FlameWaveBirths.Numbers(Values(20)).HalfWidth==6&&FlameWaveBirths.Numbers(Values(60)).HalfWidth==7&&twenty.Mana256==4224,"Width curve/cap or fractional mana progression");
        var p=ClassSkillChecks.Player(DiabloClass.Warlock);p.AddItem(new Backpack());ClassSkillChecks.Train(p,"warlock.flamewave");
        var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(719),true);p.AddToBackpack(item);Require(p.EquipItem(item),"Original Flame Wave soft-rank item");ClassGear.Refresh(p);
        try
        {
            var captured=FlameWaveBirths.Capture(p);int mana=NecromancerEffects.ManaBalance256(p);p.AddToBackpack(item);ClassGear.Refresh(p);var live=FlameWaveBirths.Capture(p);
            Require(captured.Numbers.Rank==live.Numbers.Rank+1&&captured.HardRing==live.HardRing&&captured.HardApocalypse==live.HardApocalypse
                &&captured.Numbers.Mana256==live.Numbers.Mana256+128&&NecromancerEffects.ManaBalance256(p)==mana,"Original item/rank/hard synergy capture paid or changed");
            FlameWaveBirths.Validate(captured);var head=FlameWaveBirths.Endpoints(captured with{Slow=new(100,0)},false);var ground=FlameWaveBirths.Endpoints(captured with{Slow=new(100,0)},true);
            Require(head==(0,0)&&ground.Min>0&&ground.Max>=ground.Min,"CanSlow head versus unslowed ground source domains");
        }
        finally{item.Delete();p.Delete();}
        Require(WarlockCombat.Implemented(CombatAbility.FlameWave)==FlameWaveWorld.Approved,"Flame Wave admission differs from complete approval");
    }
}
