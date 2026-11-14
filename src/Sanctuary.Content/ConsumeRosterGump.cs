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
    private readonly uint[] Pets;
    internal ConsumeRosterGump(PlayerMobile owner,SkeletonGuardian[] pets):base(60,60)
    {
        Owner=owner.Serial.Value;Pets=pets.Select(b=>b.Serial.Value).ToArray();
        AddBackground(0,0,470,130+90*pets.Length,9270);AddLabel(20,15,1152,"Consume a demon");
        AddLabel(20,40,1152,"The power occupies one summon slot until it expires.");
        for(int i=0;i<pets.Length;i++)
        {
            var body=pets[i];var s=ConsumePrepared.Capture(owner,body.SourceMonsterId,SummonDifficulties.For(owner.Map));int y=75+90*i;
            AddButton(20,y,4005,4007,i+1,GumpButtonType.Reply,0);AddLabel(60,y,1152,body.Name);
            AddLabel(60,y+23,1152,$"Life +{s.LifePercent}%, Run/Walk +{s.VelocityPercent}%");
            string bonus=s.Monster switch
            {"wargoatman"=>$"Damage +{s.Bonus.DamagePercent}%, Crushing Blow {s.Bonus.CrushingChance}%",
                "warbighead"=>$"Fire damage +{s.Bonus.FireMastery}%, enemy fire resist -{s.Bonus.FirePierce}%",
                _=>$"Magic damage +{s.Bonus.MagicMastery}%, enemy magic resist -{s.Bonus.MagicPierce}%"};
            AddLabel(60,y+46,1152,bonus);
        }
    }
    public override void OnResponse(NetState sender,in RelayInfo info)
    {
        if(sender.Mobile is not PlayerMobile owner||owner.Serial.Value!=Owner||info.ButtonID<1||info.ButtonID>Pets.Length){return;}
        ConsumeCasting.BeginPrepared(owner,(Serial)Pets[info.ButtonID-1],Core.TickCount);
    }
}
