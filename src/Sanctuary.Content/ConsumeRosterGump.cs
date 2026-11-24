// SPDX-License-Identifier: GPL-3.0-or-later
using System.Linq;
using Server;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary;

internal sealed class ConsumeRosterGump:Gump
{
    private readonly uint Owner;
    private readonly (uint Serial,long Order)[] Pets;
    internal ConsumeRosterGump(PlayerMobile owner,BaseCreature[] pets):base(60,60)
    {
        Owner=owner.Serial.Value;Pets=pets.Select(b=>(b.Serial.Value,BattleCommand.PetOrder(b))).ToArray();
        AddBackground(0,0,470,130+90*pets.Length,9270);AddLabel(20,15,1152,"Consume a demon");
        AddLabel(20,40,1152,"The power occupies one summon slot until it expires.");
        for(int i=0;i<pets.Length;i++)
        {
            var body=pets[i];var s=ConsumePrepared.Capture(owner,ConsumeCasting.Monster(body),SummonDifficulties.For(owner));int y=75+90*i;
            AddButton(20,y,4005,4007,i+1,GumpButtonType.Reply,0);AddLabel(60,y,1152,body.Name);
            AddLabel(60,y+23,1152,$"Life +{s.LifePercent}%, Run/Walk +{s.VelocityPercent}%");
            AddHtml(60,y+46,390,40,ConsumePrepared.Description(s),false,false);
        }
    }
    public override void OnResponse(NetState sender,in RelayInfo info)
    {
        if(!ConsumeEffects.Approved||sender.Mobile is not PlayerMobile owner||owner.Serial.Value!=Owner||info.ButtonID<1||info.ButtonID>Pets.Length){return;}
        var selected=Pets[info.ButtonID-1];
        if(World.FindMobile((Serial)selected.Serial)is BaseCreature body&&BattleCommand.PetOrder(body)==selected.Order)
        {ConsumeCasting.BeginPrepared(owner,body.Serial,Core.TickCount);}
    }
}
