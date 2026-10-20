#!/usr/bin/env python3
"""Author the agreed UO/D2 gem mapping and fixed quality tables from Blizzard’s gem reference."""
from pathlib import Path
import json,re
root=Path(__file__).resolve().parents[1]
def effect(code,label,values,maxima=None,durations=None):
 return [dict(code=code,label=label,minimum=v,maximum=(maxima or values)[i],durationMs=(durations or [0]*5)[i]) for i,v in enumerate(values)]
def merge(*columns):return [[col[i] for col in columns] for i in range(5)]
res=[12,16,22,28,40];stat=[3,4,6,8,10];pool=[10,17,24,31,38]
specs={
 'Amethyst':('amethyst',merge(effect('att','Attack Rating',[40,60,80,100,150])),merge(effect('ac','Defense',[8,12,18,24,30])),merge(effect('str','Strength',stat))),
 'Diamond':('diamond',merge(effect('dmg-undead%','Damage to Undead (%)',[28,34,44,54,68])),merge(effect('res-all','All Resistances (%)',[6,8,11,14,19])),merge(effect('att','Attack Rating',[20,40,60,80,100]))),
 'Emerald':('emerald',merge(effect('poison-damage','Poison Damage',[10,20,40,60,100],durations=[3000,4000,5000,6000,7000])),merge(effect('res-pois','Poison Resistance (%)',res)),merge(effect('dex','Dexterity',stat))),
 'Ruby':('ruby',merge(effect('fire-damage','Fire Damage',[3,5,8,10,15],[4,8,12,16,20])),merge(effect('res-fire','Fire Resistance (%)',res)),merge(effect('hp','Maximum Life',pool))),
 'Sapphire':('sapphire',merge(effect('cold-damage','Cold Damage',[1,3,4,6,10],[3,5,7,10,14],[1000,1400,2000,2400,3000])),merge(effect('res-cold','Cold Resistance (%)',res)),merge(effect('mana','Maximum Mana',pool))),
 'Citrine':('topaz',merge(effect('lightning-damage','Lightning Damage',[1]*5,[8,14,22,30,40])),merge(effect('res-ltng','Lightning Resistance (%)',res)),merge(effect('mag%','Magic Find (%)',[9,13,16,20,24]))),
 'Tourmaline':('skull',merge(effect('life-steal%','Life Stolen (%)',[2,2,3,3,4]),effect('mana-steal%','Mana Stolen (%)',[1,2,2,3,3])),merge(effect('thorns','Attacker Takes Damage',[4,8,12,16,20])),merge(effect('replenish-life','Replenish Life',[2,3,3,4,5]),effect('mana-regen%','Regenerate Mana (%)',[8,8,12,12,19]))),
 'Amber':('extra-thorns',*[merge(effect('thorns','Attacker Takes Damage',[4,8,12,16,20]))]*3),
 'StarSapphire':('extra-magic-reduction',*[merge(effect('magic-reduction','Magic Damage Reduced',[1,2,3,4,5]))]*3)
}
families=[]
for typename,(source,weap,shield,armor) in specs.items():
 sourcefile=root/'upstream/ModernUO/Projects/UOContent/Items/Gems'/f'{typename}.cs'
 art=int(re.search(r'base\((0x[\da-fA-F]+)\)',sourcefile.read_text())[1],16)
 name='Star Sapphire' if typename=='StarSapphire' else typename
 families.append(dict(id=typename,name=name,source=source,art=art,hue=0,effects=[dict(weapon=weap[i],shield=shield[i],armor=armor[i]) for i in range(5)]))
settings=dict(schemaVersion=1,source='https://classic.battle.net/diablo2exp/items/gems.shtml',dropDenominator=100,dropTickets=5,quantity=1,selection='uniform-family-and-unlocked-quality',fullPackAtFeet=True,minimumSkills=[40,50,60,70,80],prices=[25,75,250,500,1500],upgradeRatio=3,upgradeSkills=[40,50,60,70],families=families)
(root/'data/Sanctuary/Balance/socket-gems.json').write_text(json.dumps([settings],indent=2)+'\n')
