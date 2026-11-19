#!/usr/bin/env python3
"""Compile all eight skill trees and retain source mechanics and asset requirements."""
import argparse
import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'tools/reference/d2r-named-equipment'
CODES = ('ama', 'sor', 'nec', 'pal', 'bar', 'dru', 'ass', 'war')
CLASSES = ('Amazon', 'Sorceress', 'Necromancer', 'Paladin', 'Barbarian', 'Druid', 'Assassin', 'Warlock')
normalize = lambda value: re.sub('[^a-z0-9]', '', value.lower())

def warlock_stats():
    raw = SOURCE / 'json/charstats.json'
    pin = json.loads((SOURCE/'sources.json').read_text())['files']['json/charstats.json']['sha256']
    if hashlib.sha256(raw.read_bytes()).hexdigest() != pin: raise ValueError('Changed Warlock CharStats source')
    r = next(r for r in json.loads(raw.read_text()).values() if r.get('class') == 'Warlock')
    return dict(name='Warlock',strength=r['str'],dexterity=r['dex'],vitality=r['vit'],energy=r['int'],life=r['vit']+r['hpadd'],
                stamina=r['stamina'],mana=r['int'],lifeLevel4=r['LifePerLevel'],staminaLevel4=r['StaminaPerLevel'],manaLevel4=r['ManaPerLevel'],
                lifeVitality4=r['LifePerVitality'],staminaVitality4=r['StaminaPerVitality'],manaEnergy4=r['ManaPerMagic'],
                attackBonus=r['ToHitFactor'],statPerLevel=r['StatPerLevel'])

def compile_skills():
    manifest = json.loads((SOURCE / 'sources.json').read_text())
    for relative, pin in manifest['files'].items():
        if hashlib.sha256((SOURCE / relative).read_bytes()).hexdigest() != pin['sha256']:
            raise ValueError('Pinned D2R source hash mismatch: ' + relative)
    source = json.loads((SOURCE / 'json/skills.json').read_text())
    desc = {r['skilldesc']: r for r in json.loads((SOURCE / 'json/skilldesc.json').read_text()).values() if r.get('skilldesc')}
    strings = json.loads((SOURCE / 'json/allstrings-eng.json').read_text())
    talents = json.loads((ROOT / 'data/Sanctuary/talents.json').read_text())
    war = [r for r in source.values() if r.get('charclass') == 'war']
    ids = {r['skill']: 'warlock.' + normalize(strings.get(desc[r['skilldesc']].get('str name'), r['skill'])) for r in war}
    trees = []
    for page, name in enumerate(('Demon', 'Eldritch', 'Chaos'), 1):
        rows = sorted([r for r in war if desc[r['skilldesc']]['SkillPage'] == page], key=lambda r: (r['reqlevel'], desc[r['skilldesc']].get('SkillRow', 0), desc[r['skilldesc']].get('SkillColumn', 0), r['*Id']))
        trees.append(dict(name=name, source='https://news.blizzard.com/en-us/article/24243863/rain-annihilation-in-reign-of-the-warlock',
                          skills=[dict(id=ids[r['skill']],name=strings.get(desc[r['skilldesc']].get('str name'),r['skill']),requiredLevel=r['reqlevel'],
                                       prerequisites=[ids[r[f'reqskill{i}']] for i in range(1,4) if r.get(f'reqskill{i}')]) for r in rows]))
    talents = [c for c in talents if c['class'] != 'Warlock'] + [{'class':'Warlock','trees':trees}]
    # Correct the legacy Amazon entry that omitted its direct prerequisite.
    # Other legacy entries remain covered by the separate dependency-closure audit.
    for entry in talents:
        for tree in entry['trees']:
            for talent in tree['skills']:
                if talent['id'] == 'amazon.immolationarrow':
                    talent['prerequisites'] = ['amazon.' + normalize(source['27']['reqskill1'])]

    lookup = {}
    for row in source.values():
        if row.get('charclass') in CODES:
            d = desc[row['skilldesc']]
            for name in (row['skill'], strings.get(d.get('str name'), row['skill'])):
                lookup[normalize(name)] = row
    mechanics = []
    stable_ids = json.loads((ROOT/'data/Sanctuary/ability-ids.json').read_text())
    if [stable_ids[t] for t in ['necromancer.teeth','necromancer.raiseskeleton','sorceress.teleport']] != [2,3,4]:
        raise ValueError('Legacy ability IDs changed')
    for cls in talents:
        for tree in cls['trees']:
            for talent in tree['skills']:
                key = normalize(talent['name'])
                row = lookup['blademastery' if key == 'swordmastery' else key]
                ability = stable_ids[talent['id']]
                d = desc[row['skilldesc']]
                if talent['id'] == 'barbarian.swordmastery':
                    talent['name'] = strings.get(d.get('str name'), row['skill'])
                mechanics.append(dict(talentId=talent['id'], name=talent['name'], characterClass=cls['class'],
                    tree=tree['name'], sourceId=row['*Id'], sourceKey=row['skill'], ability=ability,
                    sourceTab=CODES.index(row['charclass'])*3+d['SkillPage']-1, passive=bool(row.get('passive')),
                    left=bool(row.get('leftskill')), right=bool(row.get('rightskill')),
                    sourceFields={k:v for k,v in row.items() if not k.startswith('*') and k != 'lineNumber'},
                    sourceParameters={str(i):row.get('Param'+str(i),0) for i in range(1,21)}))
    assert len(mechanics) == 240 and len({m['ability'] for m in mechanics}) == 240
    assert all(len(t['skills'])==10 for c in talents for t in c['trees'])
    # Every talent needs a unique readable icon; passives do not occupy hotkeys.
    assets = [dict(talentId=m['talentId'],name=m['name'],characterClass=m['characterClass'],
                   talentIcon='needed',hotkeyIcon='not-applicable' if m['passive'] else 'needed',
                   effects='not-applicable' if m['passive'] else 'needed',
                   sourceEffects={k:v for k,v in m['sourceFields'].items() if any(s in k.lower() for s in ('missile','overlay','summon','aurastate','passivestate'))},
                   generation='pending',clientMapping='pending',inGameVerification='pending') for m in mechanics]
    # These passives have reaction assets even though they cannot bind hotkeys.
    for asset,mechanic in zip(assets,mechanics,strict=True):
        if mechanic['talentId'] in {'amazon.dodge','amazon.avoid','amazon.evade','assassin.weaponblock'}:
            asset['effects']='needed'
            asset['sourceEffects'].update({k:mechanic['sourceFields'][k] for k in ('anim','stsound','stsoundclass','Param8') if k in mechanic['sourceFields']})
    evade=next(asset for asset in assets if asset['talentId']=='amazon.evade')
    evade.update(visualEffect='not-established; source server presentation is sound-only',soundEffect='needed')
    slow=next(asset for asset in assets if asset['talentId']=='amazon.slowmissiles')
    slow_mechanic=next(m for m in mechanics if m['talentId']=='amazon.slowmissiles')
    slow_state=slow_mechanic['sourceFields']['auratargetstate']
    states=json.loads((SOURCE/'json/states.json').read_text())
    slow['sourceEffects'].update(auratargetstate=slow_state,stsound=slow_mechanic['sourceFields']['stsound'],targetoverlay=states[slow_state]['overlay1'])
    return talents, mechanics, assets

def generated_protocol(rows):
    enum = ['    None = 0,', '    BasicAttack = 1,']
    metadata = []
    corpse_targets = []
    for r in sorted(rows,key=lambda r:r['ability']):
        name = ''.join(w[0].upper()+w[1:] for w in re.findall(r'[A-Za-z0-9]+',r['name']))
        if r['talentId'] == 'necromancer.raiseskeleton': name = 'RaiseSkeleton'
        if r['talentId'] == 'barbarian.swordmastery': name = 'SwordMastery'  # Saved/wire identity stays stable.
        if r['sourceFields'].get('TargetCorpse'): corpse_targets.append('CombatAbility.'+name)
        enum.append(f"    {name} = {r['ability']},")
        metadata.append(f'        new(CombatAbility.{name}, "{r["talentId"]}", "{r["name"]}", DiabloClass.{r["characterClass"]}, {str(r["passive"]).lower()}, {str(r["left"]).lower()}, {str(r["right"]).lower()}),')
    return '''// Generated by tools/build_class_skills.py. Stable legacy ability IDs 0–4.
using System;

namespace Sanctuary;

public enum CombatAbility : byte
{
'''+ '\n'.join(enum)+'''
}
public readonly record struct ClassAbility(CombatAbility Ability, string TalentId, string Name,
    DiabloClass Class, bool Passive, bool Left, bool Right);
public static class ClassAbilities
{
    public static bool AllyTarget(CombatAbility value) => value is CombatAbility.Consume or CombatAbility.Enchant or CombatAbility.HolyBolt or CombatAbility.BoneWall or CombatAbility.BonePrison;
    public static bool ItemTarget(CombatAbility value) => value==CombatAbility.IronGolem;
    public static bool CorpseTarget(CombatAbility value) => value is '''+ ' or '.join(corpse_targets)+''';
    public static bool SelfTarget(CombatAbility value) => value is CombatAbility.BloodBoil or CombatAbility.BoneArmor or CombatAbility.PoisonNova or CombatAbility.Nova or CombatAbility.FrostNova or CombatAbility.StaticField or CombatAbility.RingOfFire or CombatAbility.PsychicWard or CombatAbility.EldritchBlast or CombatAbility.HexBane or CombatAbility.HexPurge or CombatAbility.HexSiphon
        or CombatAbility.FrozenArmor or CombatAbility.ShiverArmor or CombatAbility.ChillingArmor or CombatAbility.EnergyShield or CombatAbility.ThunderStorm or CombatAbility.Blaze or CombatAbility.Might or CombatAbility.Defiance or CombatAbility.BlessedAim or CombatAbility.Prayer or CombatAbility.Cleansing or CombatAbility.Meditation or CombatAbility.ResistFire or CombatAbility.ResistCold or CombatAbility.ResistLightning or CombatAbility.Salvation or CombatAbility.Vigor or CombatAbility.Redemption or CombatAbility.Concentration or CombatAbility.Fanaticism or CombatAbility.HolyFire or CombatAbility.HolyShock or CombatAbility.HolyFreeze or CombatAbility.Sanctuary or CombatAbility.BlessedHammer or CombatAbility.Thorns or CombatAbility.Conviction or CombatAbility.HolyShield or CombatAbility.Shout or CombatAbility.BattleOrders or CombatAbility.BattleCommand or CombatAbility.Howl or CombatAbility.BattleCry or CombatAbility.WarCry or CombatAbility.InnerSight or CombatAbility.SlowMissiles or CombatAbility.BurstOfSpeed or CombatAbility.Fade or CombatAbility.Venom or CombatAbility.BladeShield or CombatAbility.CycloneArmor or CombatAbility.Hurricane or CombatAbility.Armageddon or CombatAbility.Werewolf or CombatAbility.Werebear;
    public const int Count = 242;
    public const int MaskBytes = (Count + 7) / 8;
    public static bool Valid(CombatAbility value) => (int)value < Count;
    public static readonly ClassAbility[] Skills =
    [
'''+ '\n'.join(metadata)+'''
    ];
    public static ClassAbility? Find(CombatAbility value) => value is >= (CombatAbility)2 and < (CombatAbility)Count ? Skills[(int)value - 2] : null;
    public static string Label(CombatAbility value) => value == CombatAbility.BasicAttack ? "Attack" : Find(value)?.Name ?? "None";
}
'''

def products():
    talents, rows, assets = compile_skills()
    source_skills = json.loads((SOURCE / 'json/skills.json').read_text())
    oak = next(row for row in rows if row['talentId'] == 'druid.oaksage')
    oak_aura = source_skills['298']
    assert oak_aura['skill'] == 'Oak Sage Aura' and oak_aura['aurastat1'] == 'item_maxhp_percent'
    spirit_auras = [{**oak, 'name':oak_aura['skill'], 'sourceId':298, 'sourceKey':oak_aura['skill'],
        'left':False, 'right':False,
        'sourceFields':{k:v for k,v in oak_aura.items() if not k.startswith('*') and k != 'lineNumber'},
        'sourceParameters':{str(i):oak_aura.get('Param'+str(i),0) for i in range(1,21)}}]
    heart = next(row for row in rows if row['talentId'] == 'druid.heartofwolverine')
    heart_aura = source_skills['297']
    assert heart_aura['skill'] == 'Wolverine Aura' and heart_aura['aurastat1'] == 'item_tohit_percent' and heart_aura['aurastat2'] == 'damagepercent'
    spirit_auras += [{**heart, 'name':heart_aura['skill'], 'sourceId':297, 'sourceKey':heart_aura['skill'],
        'left':False, 'right':False,
        'sourceFields':{k:v for k,v in heart_aura.items() if not k.startswith('*') and k != 'lineNumber'},
        'sourceParameters':{str(i):heart_aura.get('Param'+str(i),0) for i in range(1,21)}}]
    barbs = next(row for row in rows if row['talentId'] == 'druid.spiritofbarbs')
    barbs_aura = source_skills['296']
    assert barbs_aura['skill'] == 'Barbs Aura' and barbs_aura['aurastat1'] == 'item_attackertakesdamage' and not barbs_aura.get('aurastat2')
    spirit_auras += [{**barbs, 'name':barbs_aura['skill'], 'sourceId':296, 'sourceKey':barbs_aura['skill'],
        'left':False, 'right':False,
        'sourceFields':{k:v for k,v in barbs_aura.items() if not k.startswith('*') and k != 'lineNumber'},
        'sourceParameters':{str(i):barbs_aura.get('Param'+str(i),0) for i in range(1,21)}}]
    death = next(row for row in rows if row['talentId'] == 'assassin.deathsentry')
    sentry_npc = []
    for source_id in (312, 324):
        source = next(row for row in source_skills.values() if row.get('*Id') == source_id)
        sentry_npc.append({**death, 'name':source['skill'], 'sourceId':source_id,
                          'sourceKey':source['skill'], 'left':False, 'right':False,
                          'sourceFields':{k:v for k,v in source.items() if not k.startswith('*') and k != 'lineNumber'},
                          'sourceParameters':{str(i):source.get('Param'+str(i),0) for i in range(1,21)}})
    dire = next(row for row in rows if row['talentId'] == 'druid.summondirewolf')
    rage = source_skills['314']
    assert rage['skill'] == 'fenris rage' and rage['srvdofunc'] == 137
    druid_npc = [{**dire, 'name':rage['skill'], 'sourceId':314, 'sourceKey':rage['skill'],
                  'left':False, 'right':False,
                  'sourceFields':{k:v for k,v in rage.items() if not k.startswith('*') and k != 'lineNumber'},
                  'sourceParameters':{str(i):rage.get('Param'+str(i),0) for i in range(1,21)}}]
    bear=next(row for row in rows if row['talentId']=='druid.summongrizzly')
    smite=source_skills['326']
    assert smite['skill']=='BearSmite' and smite['srvstfunc']==32 and smite['srvdofunc']==2 and smite['ResultFlags']==8
    druid_npc += [{**bear,'name':smite['skill'],'sourceId':326,'sourceKey':smite['skill'],'left':False,'right':False,
        'sourceFields':{k:v for k,v in smite.items() if not k.startswith('*') and k!='lineNumber'},
        'sourceParameters':{str(i):smite.get('Param'+str(i),0) for i in range(1,21)}}]
    for talent_id,source_id,name in [('druid.carrionvine',307,'CorpseCycler'),('druid.solarcreeper',325,'VineCycler')]:
        owner=next(row for row in rows if row['talentId']==talent_id);source=source_skills[str(source_id)]
        assert source['skill']==name and source['srvstfunc']==63
        druid_npc += [{**owner,'name':name,'sourceId':source_id,'sourceKey':name,'left':False,'right':False,
            'sourceFields':{k:v for k,v in source.items() if not k.startswith('*') and k!='lineNumber'},
            'sourceParameters':{str(i):source.get('Param'+str(i),0) for i in range(1,21)}}]
    goat=next(row for row in rows if row['talentId']=='warlock.summongoatman')
    warlock_npc=[]
    for source_id,name in [(416,'Goatman Stun'),(417,'Goatman Frenzy'),(418,'Goatman Berserk'),(419,'Goatman Cleave')]:
        source=source_skills[str(source_id)];assert source['skill']==name
        warlock_npc.append({**goat,'name':name,'sourceId':source_id,'sourceKey':name,'left':False,'right':False,
            'sourceFields':{k:v for k,v in source.items() if not k.startswith('*') and k!='lineNumber'},
            'sourceParameters':{str(i):source.get('Param'+str(i),0) for i in range(1,21)}})
    tainted=next(row for row in rows if row['talentId']=='warlock.summontainted')
    for source_id,name in [(420,'Tainted Resist Fire'),(422,'Tainted Fire Ball')]:
        source=source_skills[str(source_id)];assert source['skill']==name
        warlock_npc.append({**tainted,'name':name,'sourceId':source_id,'sourceKey':name,'left':False,'right':False,
            'sourceFields':{k:v for k,v in source.items() if not k.startswith('*') and k!='lineNumber'},
            'sourceParameters':{str(i):source.get('Param'+str(i),0) for i in range(1,21)}})
    monsters = json.loads((SOURCE / 'json/monstats.json').read_text())
    missiles = json.loads((SOURCE / 'json/missiles.json').read_text())
    levels = json.loads((SOURCE / 'json/monlvl.json').read_text())
    summons = [{'key':r['Id'],'fields':{k:v for k,v in r.items() if isinstance(v,int)}} for r in monsters.values()
               if r.get('Id','').lower() in ['wargoatman','warbighead','warputriddefiler','druidbear','fenris','spiritofbarbs','heartofwolverine','oaksage','spiritwolf','plaguepoppy','cycleoflife','vinecreature','druidhawk','necroskeleton','necromage','claygolem','bloodgolem','irongolem','firegolem','bonewall','hydra1','hydra2','hydra3','dopplezon','valkyrie','shadowwarrior','shadowmaster','bladecreeper','chargeboltsentry','wakeofdestruction','lightningsentry','infernosentry','deathsentry']]
    raven_properties = json.loads((ROOT/'tools/reference/raven-preflight.json').read_text())['currentRows']['monprop']
    assert [raven_properties[k] for k in ['Id','prop1','min1','max1','prop1 (N)','min1 (N)','max1 (N)','prop1 (H)','min1 (H)','max1 (H)']] == ['druidhawk'] + ['stupidity','-1','-1']*3
    summons += [{'key':'druidhawk-properties','fields':{'item_stupidity':int(raven_properties['min1'])}}]
    raven_animations = {row['code']:row for row in json.loads((ROOT/'tools/reference/raven-preflight.json').read_text())['animation']['records']}
    assert (raven_animations['HKS1HTH']['frames'],raven_animations['HKS1HTH']['speed'],raven_animations['HKA1HTH']['frames'],raven_animations['HKA1HTH']['speed'],raven_animations['HKA1HTH']['events']) == (11,208,23,240,[[10,1]])
    summons += [{'key':'druidhawk-animation','fields':{'S1Frames':11,'S1Speed':208,'A1Frames':23,'A1Speed':240,'A1Action':10}}]
    wolf_animations = {row['code']:row for row in json.loads((ROOT/'tools/reference/druid-beasts-preflight.json').read_text())['animation']['records']}
    wolf_fields = {}
    for mode in ['S1','A1','GH','WL','RN']:
        row=wolf_animations['WF'+mode+'HTH']
        wolf_fields[mode+'Frames']=row['frames'];wolf_fields[mode+'Speed']=row['speed']
        actions=[i for i,kind in row['events'] if kind==1]
        assert len(actions)<=1
        if actions:wolf_fields[mode+'Action']=actions[0]
    assert [wolf_fields[k] for k in ['S1Frames','S1Speed','S1Action','A1Frames','A1Speed','A1Action','GHFrames','GHSpeed']] == [13,72,3,13,176,9,7,256]
    summons += [{'key':'spiritwolf-animation','fields':wolf_fields}]
    bear_preflight=json.loads((ROOT/'tools/reference/grizzly-preflight.json').read_text())
    bear_animations={row['code']:row for row in bear_preflight['animation']['records']}
    bear_fields={}
    for mode in ['WL','NU','A1','A2','S1','GH','DT','DD']:
        row=bear_animations['B7'+mode+'HTH']
        bear_fields[mode+'Frames']=row['frames'];bear_fields[mode+'Speed']=row['speed']
        actions=[frame for frame,kind in row['events'] if kind==1]
        assert len(actions)<=1
        if actions:bear_fields[mode+'Action']=actions[0]
    assert [bear_fields[k] for k in ['A1Frames','A1Speed','A1Action','A2Frames','A2Speed','A2Action','GHFrames','GHSpeed']] == [13,256,7,17,248,8,5,256]
    summons += [{'key':'druidbear-animation','fields':bear_fields}]
    iron_preflight=json.loads((ROOT/'tools/reference/iron-golem-world-preflight.json').read_text())
    iron_fields={}
    for row in iron_preflight['animation']['records']:
        mode=row['code'][2:4]
        iron_fields[mode+'Frames']=row['frames'];iron_fields[mode+'Speed']=row['speed']
        actions=[frame for frame,kind in row['events'] if kind==1]
        assert len(actions)<=1
        if actions:iron_fields[mode+'Action']=actions[0]
    assert [iron_fields[k] for k in ['A1Frames','A1Speed','A1Action','WLSpeed','S1Frames','S1Speed']]==[16,256,10,112,15,104]
    summons += [{'key':'irongolem-animation','fields':iron_fields}]
    clay_preflight=json.loads((ROOT/'tools/reference/clay-golem-world-preflight.json').read_text())
    clay_animations={row['code']:row for row in clay_preflight['animation']['records']}
    clay_fields={}
    for mode in ['S1','A1','WL','NU','DT','DD']:
        row=clay_animations['G1'+mode+'HTH']
        clay_fields[mode+'Frames']=row['frames'];clay_fields[mode+'Speed']=row['speed']
        actions=[frame for frame,kind in row['events'] if kind==1]
        assert len(actions)<=1
        if actions:clay_fields[mode+'Action']=actions[0]
    assert [clay_fields[k] for k in ['A1Frames','A1Speed','A1Action']]==[16,232,9]
    summons += [{'key':'claygolem-animation','fields':clay_fields}]
    blood_preflight=json.loads((ROOT/'tools/reference/blood-golem-world-preflight.json').read_text())
    blood_animations={row['code']:row for row in blood_preflight['animation']['records']};blood_fields={}
    for mode in ['S1','A1','WL','NU','DT','DD']:
        row=blood_animations['G2'+mode+'HTH'];blood_fields[mode+'Frames']=row['frames'];blood_fields[mode+'Speed']=row['speed']
        actions=[frame for frame,kind in row['events'] if kind==1];assert len(actions)<=1
        if actions:blood_fields[mode+'Action']=actions[0]
    assert [blood_fields[k] for k in ['A1Frames','A1Speed','A1Action']]==[16,256,10]
    summons += [{'key':'bloodgolem-animation','fields':blood_fields}]
    fire_preflight=json.loads((ROOT/'tools/reference/fire-golem-world-preflight.json').read_text())
    fire_animations={row['code']:row for row in fire_preflight['animation']['records']};fire_fields={}
    for mode in ['S1','A1','WL','NU','DT','DD']:
        row=fire_animations['G3'+mode+'HTH'];fire_fields[mode+'Frames']=row['frames'];fire_fields[mode+'Speed']=row['speed']
        actions=[frame for frame,kind in row['events'] if kind==1];assert len(actions)<=1
        if actions:fire_fields[mode+'Action']=actions[0]
    assert [fire_fields[k] for k in ['A1Frames','A1Speed','A1Action']]==[17,256,10]
    summons += [{'key':'firegolem-animation','fields':fire_fields}]
    skeleton_preflight=json.loads((ROOT/'tools/reference/skeleton-world-preflight.json').read_text())
    skeleton_animations={row['code']:row for row in skeleton_preflight['animation']['records']}
    skeleton_fields={}
    for mode in ['S1','A1','A2','GH','BL','WL','NU','DT','DD']:
        row=skeleton_animations['SK'+mode+'1HS']
        skeleton_fields[mode+'Frames']=row['frames'];skeleton_fields[mode+'Speed']=row['speed']
        actions=[frame for frame,kind in row['events'] if kind==1]
        assert len(actions)<=1
        if actions:skeleton_fields[mode+'Action']=actions[0]
    assert [skeleton_fields[k] for k in ['A1Frames','A1Speed','A1Action','S1Frames','S1Speed']]==[16,224,10,12,128]
    summons += [{'key':'necroskeleton-animation','fields':skeleton_fields}]
    mage_fields={}
    for mode in ['S1','A1','A2','GH','BL','WL','NU','DT','DD']:
        row=skeleton_animations['SK'+mode+'HTH']
        mage_fields[mode+'Frames']=row['frames'];mage_fields[mode+'Speed']=row['speed']
        actions=[(frame,kind) for frame,kind in row['events'] if kind in [1,2]]
        assert len(actions)<=1
        if actions:mage_fields[mode+'Action'],mage_fields[mode+'ActionKind']=actions[0]
    assert [mage_fields[k] for k in ['A1Frames','A1Speed','A1Action','A1ActionKind']]==[16,208,8,2]
    assert skeleton_preflight['currentRows']['monstats']['necromage']['Sk1mode']=='A1'
    summons += [{'key':'necromage-animation','fields':mage_fields}]
    oak_animations = {row['code']:row for row in json.loads((ROOT/'tools/reference/oak-sage-preflight.json').read_text())['animation']['records']}
    assert [oak_animations['XWWLHTH'][k] for k in ['frames','speed']] == [7,128]
    summons += [{'key':'oaksage-animation','fields':{'WLFrames':7,'WLSpeed':128}}]
    heart_animations = {row['code']:row for row in json.loads((ROOT/'tools/reference/heart-of-wolverine-preflight.json').read_text())['animation']['records']}
    assert [heart_animations['X3WLHTH'][k] for k in ['frames','speed']] == [7,232]
    summons += [{'key':'heartofwolverine-animation','fields':{'WLFrames':7,'WLSpeed':232}}]
    barbs_animations = {row['code']:row for row in json.loads((ROOT/'tools/reference/spirit-of-barbs-preflight.json').read_text())['animation']['records']}
    assert [barbs_animations['X4WLHTH'][k] for k in ['frames','speed']] == [7,248]
    summons += [{'key':'spiritofbarbs-animation','fields':{'WLFrames':7,'WLSpeed':248}}]
    oak_rank_context = json.loads((ROOT/'tools/reference/oak-skill-rank-engine-evidence.json').read_text())['experience']['maximumLevels']
    assert oak_rank_context['Amazon'] == 99
    summons += [{'key':'oaksage-skill-context','fields':{'MaximumRank':oak_rank_context['Amazon']}}]
    creeper_animations = {row['code']:row for row in json.loads((ROOT/'tools/reference/creeper-preflight.json').read_text())['animation']['records']}
    assert [creeper_animations['K9S1HTH'][k] for k in ['frames','speed','events']] == [2,256,[[1,2]]]
    summons += [{'key':'plaguepoppy-animation','fields':{'S1Frames':2,'S1Speed':256,'S1SkillAction':1,
        'WLFrames':creeper_animations['K9WLHTH']['frames'],'WLSpeed':creeper_animations['K9WLHTH']['speed']}}]
    recycler_preflight=json.loads((ROOT/'tools/reference/recycler-vines-preflight.json').read_text())
    for key in ['cycleoflife','vinecreature']:
        monster=recycler_preflight['currentRows']['monstats'][key]
        body=recycler_preflight['currentRows']['monstats2'][key]
        assert monster['Code'].upper()=='K9' and body['mS1']=='1' and body.get('S1mv','0')=='0'
        assert body['SizeX']==body['SizeY']=='0' and body['MeleeRng']=='1'
    summons += [{'key':'recycler-animation','fields':{'S1Frames':2,'S1Speed':256,'S1SkillAction':1,
        'WLFrames':creeper_animations['K9WLHTH']['frames'],'WLSpeed':creeper_animations['K9WLHTH']['speed']}}]
    vine = source_skills['294']
    summons += [{'key':'Vine Attack','fields':{k:v for k,v in vine.items() if isinstance(v,(int,str)) and not k.startswith('*') and k != 'lineNumber'}}]
    models = json.loads((SOURCE / 'json/monstats2.json').read_text())
    assert [models['druidbear'][k] for k in ['SizeX','SizeY','MeleeRng','Height']] == [2,2,2,3]
    summons += [{'key':'druidbear-model','fields':{k:v for k,v in models['druidbear'].items() if isinstance(v,int)}}]
    summons += [{'key':'druidhawk-model','fields':{k:v for k,v in models['druidhawk'].items() if isinstance(v,int)}}]
    summons += [{'key':'infernosentry-model','fields':{k:v for k,v in models['infernosentry'].items() if isinstance(v,int)}}]
    summons += [{'key':r['Missile'],'fields':{k:v for k,v in r.items() if isinstance(v,(int,str)) and not k.startswith('*') and k not in ['lineNumber','Missile']}} for r in missiles.values()
                if r.get('Missile') in ['miasmachainscloud','miasmabolt','miasmaboltcloud','miasmaboltcloudmaker','miasmaboltdot','arcticblast1','recycler delay','vine recycler delay','shockwave','rabiesplague','rabiescontagion','plague vines','plague vines trail','armageddoncontrol','armageddonfire','tornado','volcano','volcano debris 2','volcano small fire','volcano overlay fire','volcano explosion','twister','erruption center','erruption crack 1','erruption crack 2','erruption smoke 1','erruption smoke 2','moltenboulderemerge','moltenboulder','moltenboulderfirepath','firestormmaker','firestorm','inferno sentry 1','inferno sentry 2','sentrylightningbolt','sentrylightningbolt2','wake of destruction maker','wake of destruction','royalstrikemeteorcenter','royalstrikemeteor','royalstrikemeteortail','royalstrikemeteorexplode','royalstrikemeteorfire','royalstrikechainlightning','royalstrikechaosice','bladesoficecubes','clawsofthundernova','clawsofthunderbolt','fistsoffirefirewall','bladefragment1','bladefragment2','bladefragment3','bomb in air','bomb on ground','bomb explosion','shock field in air','shock field on ground','lightningfury','furylightning','plaguejavelin','plaguejavcloud','lightningjavelin','poisonjav','poisonjavcloud','necromage1','necromage2','necromage3','necromage4','bonespear','teeth','bonespirit','poisonexplosioncloud','bonewallmaker','chillingarmorbolt','firebolt','arrow','magicarrow','firearrow','coldarrow','icearrow','freezingarrow','freezingarrowexp3','immolationarrow','immolationfire','strafearrow','strafebolt','guidedarrow','explodingarrow','explodingarrowexp2','multipleshotarrow','multipleshotbolt','thunderstorm1','lightningbolt','chargedbolt','sentrychargedbolt','chargedstrikebolt','blaze','firewall','firewallmaker','meteorcenter','meteorfire','blizzardcenter','blizzard1','frozenorb','frozenorbbolt','frozenorbnova','hydra','infernoflame1','chainlightning','lightningstrike','holybolt','fistoftheheavensdelay','fistoftheheavensbolt','blessedhammer','shout','battleorders','battlecommand','howl','battlecry','warcry']]
    summons += [{'key':'level-'+str(r['Level']),'fields':r} for r in levels.values() if r.get('Level',0) in range(1,100)]
    # Append this reviewed row without reordering existing saved-table inputs.
    nova = missiles['poisonnova']
    summons += [{'key':'poisonnova','fields':{k:v for k,v in nova.items() if isinstance(v,(int,str)) and not k.startswith('*') and k not in ['lineNumber','Missile']}}]
    # Approved Mage Glacial reuses the pinned row through the existing loader.
    glacial = missiles['glacialspike']
    summons += [{'key':'glacialspike','fields':{k:v for k,v in glacial.items() if isinstance(v,(int,str)) and not k.startswith('*') and k not in ['lineNumber','Missile']}}]
    # Approved basic Ice Mages use the same pinned missile lookup as spells.
    ice = missiles['icebolt']
    summons += [{'key':'icebolt','fields':{k:v for k,v in ice.items() if isinstance(v,(int,str)) and not k.startswith('*') and k not in ['lineNumber','Missile']}}]
    # Player Ice Blast uses damage callback4: cold length becomes freeze only.
    blast = missiles['iceblast']
    summons += [{'key':'iceblast','fields':{k:v for k,v in blast.items() if isinstance(v,(int,str)) and not k.startswith('*') and k not in ['lineNumber','Missile']}}]
    # Player Fire Ball uses the same complete hit1 callback as fire arrow areas.
    ball = missiles['fireball']
    summons += [{'key':'fireball','fields':{k:v for k,v in ball.items() if isinstance(v,(int,str)) and not k.startswith('*') and k not in ['lineNumber','Missile']}}]
    # Sorceress radial do22 skills share the checked64-offset emitter.
    for key in ('nova','frostnova'):
        summons += [{'key':key,'fields':{k:v for k,v in missiles[key].items() if isinstance(v,(int,str)) and not k.startswith('*') and k not in ['lineNumber','Missile']}}]
    staged_effects = {
        'warlock.bladewarp':'Identified astral travel0x36E4/hue0x48D,magic impact0x36BD/hue0x48D,throw0x23B and existing teleport particles/sound0x1FE;final astral weapon,bladewarpexplode,cast overlay and audio pending',
        'warlock.summontainted':'Learned Succubus summon/native movement and existing generic A2/Fire Ball/aura markers; shared summon sound0x1E5 is a placeholder; final cast/contact/status/audio pending',
        'assassin.dragontalon':'Learned approved human KK series uses an identified native Wrestle pose and0x238contact sound;shared charge/element/knockback cues retain their identified placeholders. Final directional kick/series/contact,charge releases,miss/block/interruption/recovery and audio remain pending. Talent and left/right/quick-hotkey icons remain separate text placeholders;mechanical acceptance does not certify final rendering.',
        'assassin.dragonclaw':'Learned approved dual-claw sequence uses identified native weapon pose/contact sound0x238 and existing charge/element cues. Final directional primary/secondary sequence,contacts,progressive releases,miss/block/interruption/recovery and audio remain pending;talent and left/right/quick icons remain separate text placeholders. Mechanical checks do not certify final rendered presentation.',
        'assassin.dragonflight':'Learned approved two-stage Flight uses identified shared Teleport departure/arrival particles0x3728 and sound0x1FE,then native Wrestle kick pose/contact sound0x238 and shared charge/element cues. Final departure/arrival,dragonflight target overlay,directional kick/contact,progressive releases,miss/block/interruption/recovery and source audio remain pending. Talent and left/right/quick icons are separate text placeholders;mechanical qualification does not certify final rendering.',
        'warlock.summongoatman':'Approved UO Gargoyle body and native animations are reused for the learned summon. The shared summon sound0x1E5 is an identified placeholder. Final warlock_shadow_cast_2 overlay, Warlock_Skill_Cast_Khazra_03 audio, Stun impact, Berserk/Frenzy status and Cleave sweep/contact presentation remain pending;mechanics do not wait for assets. Talent/hotkey icons are separately tracked.',
        'warlock.mirroredblades':'Prepared suppliers/descriptors create no mirror actors or effects. Final levitated primary weapon strike and up to4ghost weapon duplicates, rig/bone attachment, melee/bow/throw trails/projectiles, impact/miss/block and Warlock_Skill_Blades_06 audio remain pending. Learned input remains gated; talent/left/right/quick icons are separately tracked.',
        'warlock.cleave':'Prepared action candidate reuses the shared UO animation and ordinary ordered weapon hit flashes; no final levitated arc is simulated. The earlier qualified contact retains ordinary UO0x238weapon sound. Final levitated-weapon Cleave sweep/arc, impact, miss/block and cast/contact/recovery audio remain pending. Approved learned action uses these identified placeholders; final talent/left/right/quick icons are separately tracked.',
        'warlock.hexpurge':'Prepared cast/attachment uses UO0x375A hue0x48E flash and0x1E5 audio; the explicit unapproved reaction candidate uses the same flash for real area damage. Visual missile717 is not a second damaging actor. Final hex_enchant weapon state, warlock_psychic_cast_2, hexpurgedebuff, curse_hit, hex_purge_explosion, charge/depletion/expiry and cast/hit audio remain pending. Learned input/automatic events36/39 remain gated; talent/right/quick icons are separately tracked.',
        'warlock.hexsiphon':'Prepared cast uses UO0x375A hue0x48C flash and0x1E5 audio; supplied target hex uses the same identified flash. Final hex_enchant weapon state, warlock_psychic_cast_2, hexsyphondebuff, curse_hit, life/mana-return overlays and cast/hit/expiry audio remain pending. Learned input/event36 remain gated; talent/right/quick icons are separately tracked.',
        'warlock.hexbane':'Prepared cast uses UO0x375A hue0x48D flash and0x1E5 audio; supplied debuff uses the same identified flash. Final hex_enchant weapon overlay, warlock_psychic_cast_2, hexbanedebuff, curse_hit and Warlock cast/hit/expiry sounds remain pending. Learned input/event36 remain gated; talent/right/quick icons are separately tracked.',
        'warlock.psychicward':'Prepared cast uses UO0x375A hue0x48D shield flash and sound0x1E5; supplied stun events reuse the shared0x375A hue0x481 marker. Persistent psychicward overlay, cast/recast, shield contact/depletion, psychicward_hit0, stun and Warlock audio remain pending. Learned input is gated; talent/right/quick icons are separately tracked.',
        'warlock.miasmabolt':'Prepared numerical packet suppliers produce no new visual. Learned cast/bolt/cloud presentation is not enabled. Final shadow cast overlay, travelling magic bolt, impact, lingering entropy cloud, expiry and audio remain pending; talent/left/right/quick icons are tracked separately.',
        'warlock.levitationmastery':'Passive: no active cast or independent hotkey effect. Existing human/weapon appearance is a placeholder for Warlock levitated-weapon/class presentation; final class animations and passive talent-tree icon remain pending independently of the mastery mechanics.',
        'druid.fury':'Identified human Wrestle pose and0x238weapon-contact sound placeholders cover the candidate wolf multi-hit series. Final Werewolf A1multi-swing,contacts,miss/block/interruption/recovery audio and talent/left/right/quick icons remain separate. Mechanical acceptance is recorded in the isolated Fury checkpoint; final art is pending.',
        'druid.arcticblast':'Prepared stream uses UO0x374A hue0x480 as an identified cold travel/chill marker. Final ArcticBlastIce directional stream, arctic_blast_cast overlay, contact/expiry and druid_arcticblast/druid_windcast audio remain pending. Talent-tree and left/right/quick icons are recorded separately; the learned channel remains disabled.',
        'druid.shockwave':'Learned bear SC cast and five-wave fan use identified UO0x37CC hue0x481 travel/contact and shared0x375A stun placeholders. Final expansion/shockwave directional sprite, bear SC pose, cast/travel/contact/stun/expiry audio and talent/left/right/quick icons remain separate. Final assets remain pending; mechanical casting does not imply graphical acceptance.',
        'druid.rabies':'Learned S3 uses an identified human Wrestle pose and contact sound0x238. Infection and contagion have no dedicated rendering; shared poison status is a placeholder and does not own the independent infection deadline. Final Werewolf S3 bite/contact, rabies infection overlay and cast/contact/expiry audio remain pending. The source contagion missile itself has a null cel. Talent/left/right/quick icons remain separate text placeholders. Mechanical acceptance is tracked in the checkpoint.',
        'druid.hunger':'Identified human Wrestle pose represents S3bite; contact sound0x238 and the shared leech effect0x376A remain placeholders. Current source has no separate impact overlay. Final wolf/bear S3body,bite/contact,leech and audio assets are pending independently of mechanics. Talent/left/right/quick icons remain separate pending assets. Physical-only reduction,dual drain and NPC shield decisions are mechanical.',
        'druid.fireclaws':'Identified human Wrestle attack pose,weapon sound0x238 and fire contact0x374A/hue0x489 precede the prepared weapon/fire commit. Final fire_hit,both player-form A1/contact/cast audio and talent/left/right/quick icons remain pending independently. Physical damage/critical,skill fire/mastery and old charge leech/stun are mechanical; this skill adds no charge aura.',
        'druid.feralrage':'Identified human Wrestle attack pose/contact sound0x238 and generic charge flash0x375A. Final feralrage orb front/back overlays,charge-size display,wolf A1/contact and cast/expiry audio remain pending. Speed,pre-gain life drain and500frame charge lifetime are mechanical; talent/left/right/quick icons remain separate.',
        'druid.maul':'Identified human Wrestle attack pose and generic0x238contact sound; generic0x375Acharge flash. Final bearTG A1/contact/audio,maul orb front/back overlays and charge-size presentation remain pending. Damage,stun,haste and500frame lifetime are mechanical; talent/right/quick icons remain separate.',
        'druid.werewolf':'Player form keeps the human avatar as an identified placeholder. Native cast pose16,transformation flash0x373A,sound0x1E5 and ordinary melee Wrestle pose stand in for model40/wolf_into/wolf_undo/body/motion/contact/druid_morph audio. These visuals do not own form stats,attack timing or lifetime. Talent/right/quick icons remain separate text labels.',
        'druid.werebear':'Player form keeps the human avatar as an identified placeholder. Native cast pose16,transformation flash0x373A,sound0x1E5 and ordinary melee Wrestle pose stand in for modelTG/bear_into/bear_undo/body/motion/contact/druid_morph audio. These visuals do not own form stats,attack timing or lifetime. Talent/right/quick icons remain separate text labels.',
        'druid.summongrizzly':'Prepared saved vulnerable body uses silent UO bear0xD4 and the explicit Grizzly [placeholder] name. Final B7 body/emergence, walk, ordinary A1, BearSmite A2/sequence, contact/knockback, hurt/death and bear/summon audio remain pending. Saved AI112movement, ordinary physical hit and prepared BearSmite/knockback plus learned casting use this identified placeholder; current mechanical/graphical acceptance remains separately bounded. Talent/right/quick icons remain separate text labels.',
        'druid.summondirewolf':'Prepared vulnerable body is named Dire Wolf [placeholder], uses silent UO wolf body0xE1. Saved Fenris movement/AI, type-independent loot-safe feeding, rage and physical bites use this placeholder. Learned SC/right/quick casting and payment/cap/input verification are tracked in the checkpoint; exact current graphical/mechanical parity remains open. Final WF emergence/walk/run/bite/hurt/death, corpse-feeding action, direwolfcharged rage overlay, teleport, intrinsic critical/contact and wolf/summon/rage audio remain pending. Talent/right/quick icons remain separate text labels.',
        'druid.spiritofbarbs':'Prepared spirit uses silent UO wisp0x3A/violet hue0x48D and the explicit Spirit of Barbs [placeholder] name. Final X4 body/emergence/walk/hurt/death, spirit_of_barbs recipient/control overlays, retaliation contact and spirit/summon audio remain pending. Shared retaliation flash0x374A/hue0x481 is a placeholder. Talent/right/quick icons stay separate text labels. Learned SC/right/quick casting uses the shared identified pose and0x1E5 summon-sound placeholders.',
        'druid.heartofwolverine':'Prepared Heart body uses silent UO wisp0x3A/red hue0x21 and the explicit Heart of Wolverine [placeholder] name. Final X3 emergence/walk/hurt/death, heart_of_wolverine recipient/control overlays and spirit/summon audio remain pending. Talent/right/quick icons remain separate text-label placeholders. Learned SC/right/quick casting uses the shared identified pose/sound placeholders.',
        'druid.oaksage':'Prepared vulnerable spirit is named Oak Sage [placeholder], uses UO wisp body0x3A and is silent. Saved source-controlled movement has no final XW animation; life aura has no final visual. Final spirit body, emergence, walk, hurt/death, oak_sage recipient overlay, control aura and summon/spirit audio remain pending. Talent/right/quick icons are separate text-label placeholders. Learned SC/right/quick casting uses identified pose and0x1E5 summon-sound placeholders.',
        'druid.poisoncreeper':'Prepared saved body is named Poison Creeper [placeholder], uses UO corpser body0x8 and is silent. This visible vulnerable preparation does not implement underground VINE_BEAST visibility. Final K9 body, underground/surface/attack, plague-vines/head/mat/wither and vine/summon audio remain pending; talent/right/quick icons are separate.',
        'druid.carrionvine':'Prepared saved body is named Carrion Vine [placeholder], uses UO corpser body0x8 and is silent. Prepared summon sound0x1E5and generic0x376Adelayed steal-life overlay are identified placeholders. Final K9body, underground/surface/corpse-feed, recycler vines/explosion,steallife and summon/feed/expiry audio remain pending. Talent/right/quick icons are recorded separately; prepared mechanics do not establish underground exposure or learned casting.',
        'druid.solarcreeper':'Prepared saved body is named Solar Creeper [placeholder], uses UO corpser body0x8 and is silent. Prepared summon sound0x1E5and generic0x376Adelayed steal-mana overlay are identified placeholders. Final K9body, underground/surface/corpse-feed, recycler vines/explosion,stealmana and summon/feed/expiry audio remain pending. Talent/right/quick icons are separate; current-table Solar recovery is explicitly provisional and learned casting remains disabled.',
        'druid.summonspiritwolf':'Prepared actor is named Spirit Wolf [placeholder], uses UO body0xE1 and silent sound. Cold-state marker0x374A/hue0x480 is temporary. Final wolf body/emergence, walk/run, bite/contact, intrinsic critical hit-class0x10/overlay54, teleport, hurt/death and wolf/summon audio remain pending. Talent/right/quick icons stay in the separate icon register.',
        'druid.armageddon':'Native self weather, delayed fall and ground fire use identified UO0x36D4/0x3709/0x398C hue0x489 markers and0x1E5 cast-audio placeholder. Final falling rock/tail, terminal explosion/crater, persistent fire, cast/weather/expiry/audio and separate talent/right/quick icons remain pending. Marker animations do not control6/25/37-frame mechanics.',
        'druid.hurricane':'Native self wind/cold aura uses identified UO0x37CC hue0x480 cast/pulse markers and0x655 cast-audio placeholder. Final hurricane wind/debris, druid_windcast/druid_hurricane pulse/expiry/audio and separate talent/right/quick icons remain pending. Marker animations do not control mechanical20-frame pulse or weather lifetime.',
        'druid.tornado':'Native wind flight and direct/periodic contact use identified UO0x37CC hue0x481 markers and0x655 cast-audio placeholders. Final expansion/tornado, druid_windcast/druid_tornado, impact/periodic/expiry presentation and separate talent/left/right/quick icons remain pending. Marker animation does not control75-frame mechanical lifetime or damage.',
        'druid.volcano':'Native vent/lava use identified UO0x398C hue0x489 markers; noncolliding debris uses0x36D4 and contact0x3709. SC cast sound0x1E5 is an identified placeholder. Final volcano_base,volcano_Fire,volcano_rock,lava_pool/explosion, cast/expiry/audio and talent/right/quick icons remain separate; source leftskill0. Mechanical damage and lifetime do not depend on marker animations.',
        'druid.twister':'Native wind casts use identified UO0x37CC hue0x481 travelling/contact markers and0x655 cast-audio placeholders. Final expansion/twister projectile, cast/contact/stun/expiry presentation, druid_windcast/druid_twister/sorceress_blizzard_impact_1 audio and talent/left/right/quick icons remain separate. Mechanical damage and lifetime do not depend on the marker animation.',
        'druid.fissure':'Native cracks use identified UO0x398C hue0x489 ground markers and0x3709 hue0x489 fire_hit contact placeholders. Learned SC cast audio0x1E5 is identified as a placeholder. Final eruption center, expansion/EruptionCrack variants, smoke, cast/expiry and druid_firecast_a/sorceress_inferno audio remain pending. Talent/right/quick icons remain separate; source leftskill0.',
        'druid.moltenboulder':'Prepared native emergence/rolling uses identified UO0x36D4 hue0x489 travel markers,0x398C hue0x489 ground flames and0x36BD hue0x489 contact/explosion markers. Final source453emergence,452rolling,454explosion,456flyingrocks,455ground fire, cast overlay and audio remain pending. SC uses the shared native pose and0x1E5 cast-audio placeholder. Talent/left/right/quick icons remain separate; learned casting is staged pending installation acceptance.',
        'assassin.wakeofinferno':'Learned S2 uses native throw animation/sound0x23B; body0x9C/hue0x489 and0x36D4/hue0x489 fire markers remain identified placeholders. Final E9 S1/sequence/S2/neutral/death actor, source523/524 beam/trail/contact and sorceress_inferno audio remain pending. Logical25-frame retirement supplies no final collapse animation. Talent/right/quick icons are separate pending assets.',
        'assassin.lightningsentry':'Learned S2 uses native throw animation and sound0x23B; body0x9C/hue0x482 and0x3818/hue0x482 straight bolt markers are identified placeholders. Final LG S1/S2/NU/DT actor and source526 Size2 bolt travel/contact/audio remain pending. Logical death timing supplies no final collapse animation. Separate talent/right/quick icons remain pending.',
        'assassin.dragontail':'Prepared fire-burst component uses native explosion 0x36BD, hue 0x489 as an identified placeholder. Learned KK kick/contact, charge release, knockback and assassin_kick_1 audio remain pending. Final fire burst needs its own effect; talent/left/right/quick icons remain in the separate inventory.',
        'assassin.phoenixstrike':'Native A1/0x238, charge0x375A, warning0x3709, impact0x36BD, flame0x398C, lightning0x3818 and frost0x374A are identified placeholders. Final Royal Strike charge tiers, meteor indicator/fall/explosion/fire, lightning paths, chaos-ice/trail/freeze and audio remain pending; talent/left/right/quick icons are separate',
        'assassin.bladesofice':'Shared native claw animation/sound, charge0x375A and cold area/freeze/stationary cube0x374A hue0x480 are identified placeholders. Final charge tiers, cold hand/contact, bladesoficeexplode/bladesoficecubes/bladesoficemelt and audio remain pending; talent/left/right/quick-hotkey icons are separately inventoried',
        'assassin.fistsoffire':'Staged claw sequence uses ordinary UO weapon pose/audio0x238 and0x375A hue0x489 charge markers. Ordinary-Attack fire release uses0x36BD explosion and shared0x398C ground flames as identified placeholders. Final progressive_fire charge1/2/3 orbiting art,fistsoffireexplode,groundFireSmall/fistsoffirefirewall,claw sequence/contact and skill audio remain pending. Separate talent/left/right/quick icons remain pending.',
        'assassin.cobrastrike':'Staged ordinary UO weapon pose/audio0x238,0x375A hue0x26 charge flash and shared0x376A life/mana recovery marker are identified placeholders. Final cobrastrike1 charge1/2/3 orbital overlays, life/mana return and assassin_chargeup_cobra_1 audio pending. Separate talent/left/right/quick icons remain pending.',
        'assassin.tigerstrike':'Staged ordinary UO weapon pose/audio0x238 and0x375A hue0x481 charge marker are identified placeholders. Final tigerstrike1 charge1/2/3 orbiting overlays, assassinfootimpact and assassin_chargeup_tiger_1 audio pending. Separate talent/left/right/quick icons remain pending.',
        'assassin.bladesentinel':'Learned S2 cast uses native throw animation and0x23B sound; actor body0x9C hue0x481 and following collision presentation are identified placeholders. Final blade summon/emergence/travel/impact/expiry effects and audio remain pending; talent/left/right/quick icons tracked separately.',
        'assassin.bladefury':'Prepared bladefragment1 travel uses shared UO0xF42 marker, explicitly a placeholder. Prepared server SQ23 timer has no final player sequence/channel rendering. Final SQ/channel/cast, Expansion bladefury bladefragment1/2/3 travel/impact and assassin_bladefury_throw_1/hit audio remain pending. Separate talent/left/right/quick icons pending; learned input remains gated.',
        'assassin.clawsofthunder':'Shared UO claw pose/audio0x238,charge flash0x375A hue0x481 and sampled travelling0x3818 hue0x482 markers are identified placeholders; all mechanical missiles remain active. Final orbital charges,claw/contact,nova/charged-bolt travel/impact and audio pending. Talent/left/right/quick icons remain separate.',
        'assassin.chargedboltsentry':'Learned S2 casting uses native throw animation and sound0x23B; stationary body0x9C/hue0x482 and travelling0x3818/hue0x482 bolt markers are identified placeholders. Logical S1 and death clocks do not supply final animation. Final S1/S2/NU/DT actor, bolt travel/impact and audio remain pending. Talent/right/quick icons are tracked separately.',
        'assassin.shockweb':'Staged throw animation9/audio0x23B,0x36E4 caltrop lobs and0x374A hue0x481 electric fields. Final caltrops/ShockField animations and assassin_staticfield sound pending. Separate talent/left/right/quick icons pending.',
        'assassin.fireblast':'Staged UO throw animation9,0x36E4 travel/landing,0x36BD explosion and0x23B sound. Final FireTraumaMissile/FireTraumaGround/FireTraumaXplosion and throw/impact audio pending. Separate talent/left/right/quick icons pending.',
        'assassin.mindblast':'Staged UO0x373A center,0x374A victim and0x1E5 sound; final mindblast center/hit, fist_will_cast overlay and assassin_psychichammer audio pending. Shared stun/knockback/conversion presentation remains placeholder. Separate talent/right-hotkey icons pending.',
        'assassin.weaponblock':'Shared native block animation20 is a placeholder; final dual-claw block animation/audio and talent icon pending. No active hotkey.',
        'druid.firestorm':'Firestorm emitters and burning fields use identified UO0x398C hue0x489 markers; SC uses the shared native pose and0x1E5 cast-audio placeholder. Final druid_fire_cast_1 overlay, one direct and two winding expansion/FirestormEmitter trails, groundFireSmall burning patches, contact/expiry presentation and druid_firecast_b/object_fire_loop_house audio remain pending. Separate talent/left/right/quick icons remain pending.',
        'druid.cyclonearmor':'Identified UO0x375A hue0x481 cast marker and0x1E5 audio placeholder. Final cyclonearmor1front/2front/3front shield overlays, cast/depletion presentation and druid cast audio remain pending. Separate talent/right/quick-hotkey icons remain pending.',
        'assassin.deathsentry':'Learned S2 uses native throw animation/sound0x23B; body0x9C/hue0x482, lightning0x3818/hue0x482 and corpse explosion0x36BD are identified placeholders. Final LG emergence/firing/neutral/death actor, missile641 travel/contact, physical/fire explosion and audio remain pending. Talent/right/quick icons are separate pending assets.',
        'assassin.venom':'Staged UO0x373A green cast marker and0x1E5 sound. Final venomclaws weapon/cast overlay and assassin_venom audio pending. Separate talent/right-hotkey icons pending.',
        'assassin.cloakofshadows':'Staged UO0x373A/0x375A caster/target markers and0x1E5 sound. Final darkness presentation, cloaked overlays, assassin_cloakofdarkness audio and separate talent/right-hotkey icons pending.',
        'assassin.psychichammer':'Staged UO0x374A impact and0x1E5/0x1F1 sounds; final psychic_hammer_hit/cast curse overlays and assassin_psychichammer/holybolt impact audio pending. Separate talent/right-hotkey icons pending.',
        'assassin.fade':'Staged UO0x373A flash,0x1E5 sound and existing named-opacity channel; final Fade cast/overlay/translucency and assassin_fade audio pending. Talent/right-hotkey icons remain separate.',
        'assassin.burstofspeed':'Staged UO0x373A flash and0x1E5 sound; final quickness cast/overlay and assassin_quickness audio pending. Talent/right-hotkey icons remain separate.',
        'amazon.lightningfury':'Identified logical projectile0xF42 and throw sound0x238 for parent and child flights. Final LightningJavelin parent, LightningStrike secondary bolts, contacts and amazon_lightning_1/sorceress_lightning_1 audio pending. Talent/L/R/quick icons remain separate',
        'amazon.plaguejavelin':'Identified logical throw0xF42/sound0x238 and green0x374A cloud markers. Final Javelin/PoisonSparks/poisonpuff moving impact clouds, contact/cast/explosion/loop audio pending. Talent/L/R/quick icons remain separate',
        'amazon.lightningbolt':'Native logical projectile0xF42 and throw sound0x238 are identified placeholders. Final LightningJavelin travel/contact, lightning overlay and amazon_lightning_1 audio pending; talent/L/R/quick-hotkey icons remain separate',
        'amazon.poisonjavelin':'Native logical projectile0xF42, ordinary throw sound0x238 and green0x374A/hue0x3F cloud marker are identified placeholders. Final Javelin, PoisonSparks/poisonpuff, trail/contact and amazon_cast_poison/weapon_throw_1/object_poison_loop audio pending. Talent/L/R-hotkey icons remain separate',
        'amazon.strafe':'Native logical arrow/bolt0xF42 and ordinary bow sound0x238 are identified placeholders. Final rapid Arrow/XBowBolt travel/contact, amazon_multi_arrow_1/weapon_xbow_1 and attack-start audio remain pending. Talent/L/R-hotkey icons are separate',
        'amazon.immolationarrow':'Native logical travel0xF42, bow sound0x238, explosion0x36BD and flame0x398C are identified placeholders. Final FireArrow/immolationarrow, groundFireBig/disc, explosion_medium_1/object_fire_loop_magic and fire impact/audio remain pending. Talent/L/R-hotkey icons are separate',
        'amazon.freezingarrow':'Native logical travel0xF42, bow sound0x238, explosion0x36BD and freeze0x374A are identified placeholders. Final IceArrow/freezingarrow, FreezeExplodeCenter/FreezeExplodeEjecta, cold impact/audio and freeze-state visuals remain pending. Talent/L/R-hotkey icons are separate',
        'amazon.guidedarrow':'Native travel0xF42 and bow sound0x238 are identified placeholders. Final SafeArrow/guidedarrow flight/turn/contact and amazon_magicarrow_1 audio remain pending. Talent/L/R-hotkey icons are separate',
        'amazon.icearrow':'Native travel0xF42, bow sound0x238 and freeze0x374A are identified placeholders. Final IceArrow/icearrow, iceexplode, amazon_coldarrow_1, impact_cold_1 and freeze-state visuals/audio remain pending. Talent/L/R-hotkey icons are separate',
        'amazon.explodingarrow':'Native logical travel0xF42, ordinary bow sound0x238 and explosion0x36BD are identified placeholders. Final FireArrow/explodingarrow, ExpArrowExplode, fireexplosion2 and source explosion audio remain pending. Talent/L/R-hotkey icons are separate',
        'amazon.coldarrow':'Native bow/crossbow A1 and logical travel use identified UO0xF42/0x238 placeholders; cold state uses0x374A hue0x480. Final IceArrow/coldarrow, iceexplode, amazon_coldarrow_1, impact_cold_1 and cold-state audio/art pending. Talent/L/R-hotkey icons remain separate',
        'amazon.firearrow':'Native bow/crossbow A1 shot uses identified UO0xF42 logical travel and0x238 ordinary bow sound placeholders. Final firearrow travel, fire impact and source fire-arrow audio remain pending; talent and L/R-hotkey icons remain separate',
        'amazon.multipleshot':'Native fan uses identified UO0xF42 logical travel and0x238 ordinary bow sound placeholders. Final Arrow/multipleshotarrow and XBowBolt/multipleshotbolt travel/contact/cast audio remain pending; talent and L/R-hotkey icons remain separate',
        'amazon.magicarrow':'Native bow/crossbow A1 shot uses identified UO0xF42 logical trail and0x238 native bow sound placeholders. Final SafeArrow/magicarrow travel, teethexplode impact and amazon_magicarrow_1 audio remain pending; talent/hotkey icons remain separate',
        'amazon.decoy':'Static UO sex silhouette/hue0x481,0x376A appear marker and0x1E5 cast sound are identified placeholders. Final Decoy model/owner resemblance, dopllezon_appear/overlay171 and amazon_valkyrie_cast audio pending. Talent/right-hotkey icons remain separate',
        'amazon.slowmissiles':'Prepared release uses identified UO0x375A hue0x481 caster marker; final cast/target presentation pending. Slowed native encounter bolt positions use identified UO0xF42/0x36D4/0x3818 markers at logical travel positions; final cast_slowmissiles, innersight target overlay and amazon_handofathena audio pending. Talent/right-hotkey icons remain separate',
        'amazon.evade':'ImageGen olive trail during native walking and a Sonniss cloth cue.',
        'amazon.avoid':'Client-only sidestep with an ImageGen cyan flash and a Sonniss air cue.',
        'amazon.dodge':'Client-only recoil with an ImageGen amber flash and a Sonniss cloth cue.',
        'barbarian.findpotion':'Shared UO search sound0x1E5 and corpse flash0x375A hue0x481 are identified placeholders; final Find Potion cry/search/corpse/potion-discovery effects and audio pending; talent/right/quick icons tracked separately',
        'necromancer.irongolem':'Prepared moving body: silent UO body14/hue0x835 and explicit placeholder name; final G4 emergence/walk/attack/death, summon, thorns, contact/state and audio remain pending',
        'barbarian.finditem':'Staged UO0x375A corpse search marker and0x1E5 cry sound; final find-object/find-heart cry, corpse search and success/failure cues pending',
        'barbarian.warcry':'Staged shared UO0x375A cast/wave/stun/block markers and0x1E5 cry; final WarcryShockwave, cast, stun and shield block presentation pending',
        'barbarian.taunt':'Staged UO0x375A target marker and0x1E5 cry sound; final taunt overlay/cry and source feedback pending',
        'barbarian.battlecry':'Staged shared UO0x375A cast/curse markers, sparse warcry wave and sound; final Battle Cry shockwave and state overlay pending',
        'barbarian.howl':'Staged UO0x375A cast/fear markers, sparse shared wave and sound; final Howl cry, BAYellShockWave01 ring and Terror/escape cue pending',
        'barbarian.battlecommand':'Staged UO0x375A self flash, sparse travelling wave and sound; final Battle Command cry, BAYellShockWave01 ring and recipient skill bonus cue pending',
        'barbarian.battleorders':'Staged UO0x375A self flash, sparse travelling wave and sound; final Battle Orders cry, BAYellShockWave01 ring, recipient life/mana/stamina cue pending',
        'barbarian.shout':'Staged UO0x375A self flash and sparse travelling wave/sound; source BAYellShockWave01 and final Shout cast, ring and recipient defense cue pending',
        'barbarian.bash':'Staged UO weapon animation/sound and shared native knockback/get-hit placeholders; final Bash impact/knockback effects pending',
        'barbarian.berserk':'Staged ordinary UO weapon animation/sound; source berserkfront/berserkback overlays and final magic strike/zero-defense cue pending',
        'barbarian.concentrate':'Staged ordinary UO weapon animation/sound; source doubledamage1 overlay has no final asset; Concentrate swing/impact and action-long defense cue pending',
        'barbarian.stun':'Staged UO weapon animation/sound and0x375A stun marker; final Stun impact/overhead effect pending',
        'paladin.conversion':'Staged ordinary UO weapon animation/sound; final cast/impact and converted-state marker pending. Talent/hotkey icons remain in the independent icon register.',
        'paladin.charge':'Staged ordinary UO accepted walking steps, weapon animation and sound; final Charge travel trail, impact/overlay147 and directional sequence pending. Missing art is independent of remaining knockback geometry and special-mode acceptance.',
        'paladin.vengeance':'Staged UO melee elemental flash/sound; final fire, cold and lightning strike effects pending',
        'paladin.holyshield':'Staged UO shield overlay; final holy shield enchantment/glow/cast effect pending',
        'paladin.conviction':'Staged UO supplier ring/hostile aura overlays; final Conviction enemy debuff ring and supplier aura pending',
        'paladin.thorns':'Staged UO aura/retaliation flashes; final thorns aura ring and physical-return impact pending',
        'paladin.blessedhammer':'Staged UO0x36E4 hammer trail; final rotating hammer/spiral trail/impact effects pending',
        'paladin.fistoftheheavens':'Staged UO0x3709 strike overlay and holy bolt placeholders; final warning/sky lightning/burst/secondary effects pending',
        'paladin.holybolt':'Staged UO0x36E4 missile and0x376A healing overlay; final holy bolt/trail/impact/heal effects pending',
        'paladin.holyfire':'Staged UO0x375A self pulse, hue0x489; final radial pulse and weapon accents pending',
        'paladin.sanctuary':'Staged UO0x375A self pulse and ordinary get-hit/movement animation; final Sanctuary ring/bolts, undead impact and knockback presentation pending',
        'paladin.holyfreeze':'Staged UO0x375A cold pulse; final radial frost, weapon accents, slowed-state and corpse-shatter effects pending',
        'paladin.holyshock':'Staged UO0x375A self pulse, hue0x481; final radial pulse and weapon accents pending',
    }
    # The runtime uses shipped UO art and sounds for these skills.
    native_effects = {}
    for character_class, skills, presentation in (
        ('amazon', 'jab impale fend', 'Native weapon thrusts and contact audio.'),
        ('amazon', 'powerstrike chargedstrike lightningstrike', 'Native weapon thrusts and lightning effects.'),
        ('amazon', 'poisonjavelin plaguejavelin', 'Native spear and Poison Field graphics.'),
        ('amazon', 'innersight slowmissiles', 'Native spell flashes and target feedback.'),
        ('amazon', 'decoy valkyrie', 'Native humanoid bodies and summon effects.'),
        ('amazon', 'evade', 'Native Evade sound.'),
        ('amazon', 'magicarrow firearrow coldarrow multipleshot explodingarrow icearrow guidedarrow immolationarrow strafe',
         'Native arrows, bolts, spell flashes and fire fields as applicable.'),
        ('sorceress', 'icebolt frostnova iceblast glacialspike', 'Native spell projectiles and frost-tinted effects.'),
        ('sorceress', 'frozenarmor shiverarmor chillingarmor energyshield', 'Native ward flashes and retaliation effects.'),
        ('sorceress', 'chargedbolt staticfield lightning nova chainlightning thunderstorm', 'Native lightning effects and sounds.'),
        ('sorceress', 'telekinesis teleport', 'Native spell flashes and sounds.'),
        ('sorceress', 'firebolt blaze firewall enchant', 'Native fire projectiles, fields and flashes as applicable.'),
        ('necromancer', 'raiseskeleton skeletalmage revive claygolem bloodgolem irongolem firegolem',
         'Native creature bodies and summon effects. Revive keeps the original creature.'),
        ('necromancer', 'poisondagger poisonexplosion poisonnova', 'Native weapon and poison effects as applicable.'),
        ('necromancer', 'corpseexplosion', 'Native Explosion graphics and sound.'),
        ('necromancer', 'amplifydamage dimvision weaken ironmaiden terror confuse lifetap attract decrepify lowerresist',
         'Native curse graphics and sounds.'),
        ('paladin', 'prayer resistfire resistcold resistlightning defiance cleansing vigor meditation redemption salvation might thorns blessedaim concentration fanaticism',
         'Native heal, ward and effect flashes for aura pulses.'),
        ('paladin', 'sacrifice smite zeal charge vengeance conversion holyshield holybolt',
         'Native weapon, shield and Holy Bolt effects as applicable.'),
        ('barbarian', 'findpotion taunt finditem', 'Native spell sounds and target effects.'),
        ('barbarian', 'bash doubleswing doublethrow stun concentrate frenzy berserk', 'Native weapon poses, thrown art and contact effects as applicable.'),
        ('druid', 'firestorm', 'Native Fire Field graphics along the moving emitters and burning patches.'),
        ('druid', 'raven oaksage summonspiritwolf heartofwolverine summondirewolf spiritofbarbs summongrizzly',
         'Native birds, wisps, wolves and bear with summon effects.'),
        ('druid', 'werewolf', 'Native Timber Wolf sprite at 120% scale in the client.'),
        ('druid', 'werebear', 'Native Grizzly Bear sprite at 120% scale in the client.'),
        ('assassin', 'dragonclaw', 'Native claw attack poses and contact sounds.'),
        ('assassin', 'psychichammer burstofspeed cloakofshadows fade mindblast venom',
         'Native spell graphics and audio for cast, status or impact feedback.'),
        ('assassin', 'shadowwarrior shadowmaster', 'Native humanoid models and summon effects.'),
        ('assassin', 'fireblast shockweb', 'Native fire and lightning projectiles, impacts and fields.'),
        ('warlock', 'summongoatman summondefiler', 'Native Gargoyle and Gazer with summon effects.'),
        ('warlock', 'bloodboil engorge consume', 'Native explosion, heal and buff graphics with matching sounds.'),
    ):
        native_effects.update({f'{character_class}.{skill}': presentation for skill in skills.split()})

    assert (set(staged_effects) | set(native_effects)).issubset({a['talentId'] for a in assets}), 'Unknown skill asset entry'
    effects = [{k:v for k,v in asset.items() if k not in ['talentIcon','hotkeyIcon']} | {'placeholder':staged_effects.get(asset['talentId'],
        'Existing UO art; final effect pending' if asset['effects']!='not-applicable' else 'None required')} for asset in assets]
    curse_marks = {
        'amplifydamage': 'scarlet gouges',
        'weaken': 'bone chevron',
        'decrepify': 'violet hourglass',
        'lowerresist': 'broken teal shield',
        'ironmaiden': 'copper thorn jaws',
        'lifetap': 'hollow blood drop',
        'dimvision': 'closed indigo eye',
        'terror': 'sulfur-yellow specter jaws',
        'confuse': 'purple spirals',
        'attract': 'green beacon eye',
    }
    curse_reactions = {'ironmaiden': 'physical reflection burst', 'lifetap': 'life return to the attacker'}
    custom_presentation = {
        **{
            f'necromancer.{name}': dict(
                customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
                inGameVerification='verified', placeholder='ImageGen curse ring, persistent mark and expiry. Sonniss cues verified in ClassicUO.',
                customArtworkPhases=['curse cast ring', mark, 'mark dissolution']
                    + ([curse_reactions[name]] if name in curse_reactions else []),
                customAudioPhases=['whisper at cast release', 'shared expiry cue']
                    + ([curse_reactions[name]] if name in curse_reactions else []),
            )
            for name, mark in curse_marks.items()
        },
        'amazon.dodge': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='verified',
            inGameVerification='verified', customArtworkPhases=['amber reaction flash', 'recoil of native body and equipment'],
            customAudioPhases=['successful Dodge cloth cue']),
        'amazon.avoid': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='verified',
            inGameVerification='verified', customArtworkPhases=['cyan reaction flash', 'sidestep of native body and equipment'],
            customAudioPhases=['successful Avoid projectile pass cue']),
        'amazon.evade': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', visualEffect='olive trail during accepted movement', soundEffect='verified',
            customArtworkPhases=['olive walking trail'], customAudioPhases=['successful Evade cloth cue']),
        'amazon.magicarrow': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            customArtworkPhases=['violet arrow and bolt flight', 'violet contact'],
            customAudioPhases=['bow and crossbow release', 'arcane release accent', 'impact']),
        'amazon.multipleshot': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            customArtworkPhases=['directional arrow and bolt fan', 'shared physical contact'],
            customAudioPhases=['bow and crossbow release', 'shared physical impact']),
        'amazon.guidedarrow': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            customArtworkPhases=['gold arrow and bolt flight', 'steering', 'gold contact'],
            customAudioPhases=['bow and crossbow release', 'guided release accent', 'impact']),
        'amazon.strafe': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            customArtworkPhases=['directional arrows and crossbow bolts', 'shared physical contact'],
            customAudioPhases=['bow and crossbow releases', 'shared physical impact']),
        'amazon.firearrow': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            customArtworkPhases=['ember arrow and bolt flight', 'flame contact'],
            customAudioPhases=['bow and crossbow release', 'shared fire release accent', 'shared fire contact']),
        'amazon.explodingarrow': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            customArtworkPhases=['ruby charge arrow and bolt flight', 'delayed area fire burst'],
            customAudioPhases=['bow and crossbow release', 'shared fire release accent', 'area burst']),
        'amazon.immolationarrow': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            customArtworkPhases=['gold ember arrow and bolt flight', 'area ignition', 'persistent ground fire', 'shared ground ember fade'],
            customAudioPhases=['bow and crossbow release', 'shared fire release accent', 'ground ignition', 'shared active fire']),
        'amazon.coldarrow': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            customArtworkPhases=['silver-blue arrow and bolt flight', 'frost contact', 'shared chill overlay'],
            customAudioPhases=['bow and crossbow release', 'shared cold release accent', 'shared cold contact']),
        'amazon.icearrow': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            customArtworkPhases=['cyan crystal arrow and bolt flight', 'ice contact', 'shared single-target freeze and thaw'],
            customAudioPhases=['bow and crossbow release', 'shared ice release accent', 'shared ice contact']),
        'amazon.freezingarrow': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            customArtworkPhases=['cobalt payload arrow and bolt flight', 'delayed area crystal burst', 'shared freeze and thaw'],
            customAudioPhases=['bow and crossbow release', 'shared ice release accent', 'shared area ice burst']),
        'amazon.lightningbolt': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            customArtworkPhases=['electrified javelin flight', 'shared lightning contact'],
            customAudioPhases=['javelin throw', 'shared electric release accent', 'shared lightning contact']),
        'amazon.lightningfury': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            customArtworkPhases=['forked lightning javelin flight', 'split flash', 'shared physical contact', 'shared secondary bolt flight and contact'],
            customAudioPhases=['javelin throw', 'shared lightning release accent', 'split', 'shared physical contact', 'secondary contact']),
        'amazon.poisonjavelin': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            customArtworkPhases=['venom javelin flight', 'venom contact', 'lingering green trail', 'trail retirement'],
            customAudioPhases=['javelin throw', 'venom contact', 'shared cloud activity']),
        'amazon.plaguejavelin': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            customArtworkPhases=['plague javelin flight', 'venom contact', 'plague burst', 'spreading cloud', 'cloud retirement'],
            customAudioPhases=['javelin throw', 'venom contact', 'plague burst', 'shared cloud activity']),
        'amazon.jab': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            customArtworkPhases=['three individual bronze contact sparks'], customAudioPhases=['thrusts', 'shared landed contact']),
        'amazon.fend': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            customArtworkPhases=['silver crescents on successive targets'], customAudioPhases=['thrusts', 'shared landed contact']),
        'amazon.impale': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            customArtworkPhases=['heavy amber impact', 'target-bound slow marker', 'slow retirement'],
            customAudioPhases=['heavy thrust', 'heavy contact']),
        'amazon.powerstrike': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            customArtworkPhases=['cobalt impact'], customAudioPhases=['shared thrust', 'shared electric contact']),
        'amazon.chargedstrike': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            customArtworkPhases=['shared electric thrust contact', 'bolt fan opening', 'shared directional charged bolts and contact'],
            customAudioPhases=['shared thrust', 'shared landed contact', 'shared bolt release and contact']),
        'amazon.lightningstrike': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            customArtworkPhases=['shared electric thrust contact', 'chain opening', 'shared directional arcs and hop impacts'],
            customAudioPhases=['shared thrust', 'shared landed contact', 'shared chain release and contacts']),
        'amazon.innersight': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            customArtworkPhases=['gold cast ring', 'attached eye mark', 'mark retirement'], customAudioPhases=['release']),
        'amazon.slowmissiles': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            customArtworkPhases=['violet cast ring', 'attached missile mark', 'mark retirement', 'slowed native missile travel'],
            customAudioPhases=['release']),
        'amazon.decoy': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            customArtworkPhases=['mirror emergence', 'native stationary silhouette with mirror shimmer', 'expiry dissolve'],
            customAudioPhases=['appearance', 'retirement']),
        'amazon.valkyrie': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            customArtworkPhases=['gold emergence', 'attached aura on native body', 'shared spear contact', 'replacement and death dissolve'],
            customAudioPhases=['appearance', 'shared thrust and contact', 'retirement']),
        'sorceress.fireball': dict(customArtwork='approved-existing', customAudio='approved-existing',
            inGameVerification='verified', customCharacterAnimation='reuse-native',
            customArtworkPhases=['native cast pose', 'approved flame projectile', 'approved area burst'],
            customAudioPhases=['approved release']),
        'sorceress.firebolt': dict(customArtwork='verified', customAudio='verified',
            customArtworkPhases=['directional fire dart', 'single-target contact flash'],
            customAudioPhases=['release', 'contact']),
        'sorceress.inferno': dict(customArtwork='verified', customAudio='verified',
            customArtworkPhases=['held flame stream', 'steering', 'contact embers', 'released flame retirement'],
            customAudioPhases=['flame stream']),
        'sorceress.blaze': dict(customArtwork='verified', customAudio='verified',
            customArtworkPhases=['cast embers', 'movement trail', 'burning tiles', 'ember fade'],
            customAudioPhases=['release', 'active trail']),
        'sorceress.firewall': dict(customArtwork='verified', customAudio='verified',
            customArtworkPhases=['ignition', 'outward wall growth', 'burning tiles', 'tile fade'],
            customAudioPhases=['release', 'active wall']),
        'sorceress.frozenarmor': dict(customArtwork='verified', customAudio='verified',
            customArtworkPhases=['cast', 'persistent frost ward', 'melee reaction', 'shared attacker freeze', 'expiry'],
            customAudioPhases=['cast', 'shared frost reaction', 'shared cold ward expiry']),
        'sorceress.shiverarmor': dict(customArtwork='verified', customAudio='verified',
            customArtworkPhases=['cast', 'persistent violet ward', 'retaliation flash', 'shared attacker chill', 'expiry'],
            customAudioPhases=['cast', 'retaliation', 'shared cold ward expiry']),
        'sorceress.chillingarmor': dict(customArtwork='verified', customAudio='verified',
            customArtworkPhases=['cast', 'persistent teal ward', 'return emission', 'shared ice bolt flight and contact', 'expiry'],
            customAudioPhases=['cast', 'shared ice bolt release and contact', 'shared cold ward expiry']),
        'sorceress.energyshield': dict(customArtwork='verified', customAudio='verified',
            customArtworkPhases=['cast', 'persistent amber shield', 'directional absorption', 'mana depletion', 'expiry'],
            customAudioPhases=['cast', 'absorption', 'mana depletion', 'expiry']),
        'sorceress.nova': dict(customArtwork='verified', customAudio='verified',
            customArtworkPhases=['caster sparks', 'expanding electric ring', 'damage contact'],
            customAudioPhases=['wave release']),
        'sorceress.telekinesis': dict(customArtwork='verified', customAudio='verified',
            customArtworkPhases=['caster prongs', 'attached combat pulse', 'ground object interaction'],
            customAudioPhases=['release', 'contact']),
        'sorceress.teleport': dict(customArtwork='verified', customAudio='verified',
            customArtworkPhases=['departure collapse', 'arrival opening'],
            customAudioPhases=['successful transfer']),
        'sorceress.enchant': dict(customArtwork='verified', customAudio='verified',
            customArtworkPhases=['caster and recipient runes', 'active embers', 'landed weapon contact', 'expiry'],
            customAudioPhases=['application', 'weapon contact', 'expiry']),
        'sorceress.hydra': dict(customArtwork='verified', customAudio='verified',
            customArtworkPhases=['three-head emergence', 'idle heads', 'facing and jaw flash', 'directional fire globules',
                                 'ember impact', 'head retirement'],
            customAudioPhases=['summon', 'shot', 'impact']),
        'sorceress.icebolt': dict(customArtwork='verified', customAudio='verified',
            customArtworkPhases=['directional ice shard', 'contact chips', 'attached chill', 'shared cold corpse shatter'],
            customAudioPhases=['release', 'contact', 'shared cold corpse shatter']),
        'sorceress.iceblast': dict(customArtwork='verified', customAudio='verified',
            customArtworkPhases=['directional comet', 'single-target burst', 'attached freeze', 'thaw', 'cold corpse shatter'],
            customAudioPhases=['release', 'contact', 'shared cold corpse shatter']),
        'sorceress.glacialspike': dict(customArtwork='verified', customAudio='verified',
            customArtworkPhases=['directional spike', 'area burst at contact or expiry', 'attached freeze', 'thaw', 'shared cold corpse shatter'],
            customAudioPhases=['release', 'area burst', 'shared cold corpse shatter']),
        'sorceress.frostnova': dict(customArtwork='verified', customAudio='verified',
            customArtworkPhases=['expanding ground ring', 'contact frost', 'attached chill', 'shared cold corpse shatter'],
            customAudioPhases=['wave release', 'shared cold corpse shatter']),
        'sorceress.chargedbolt': dict(customArtwork='verified', customAudio='verified',
            customArtworkPhases=['directional ground sparks', 'contact crackle'],
            customAudioPhases=['release', 'contact']),
        'sorceress.lightning': dict(customArtwork='verified', customAudio='verified',
            customArtworkPhases=['straight beam flight', 'contact flash'],
            customAudioPhases=['release', 'contact']),
        'sorceress.chainlightning': dict(customArtwork='verified', customAudio='verified',
            customArtworkPhases=['opening arc', 'arcs between targets', 'contact flash'],
            customAudioPhases=['release', 'contact']),
        'sorceress.staticfield': dict(customArtwork='verified', customAudio='verified',
            customArtworkPhases=['expanding ground pulse', 'recipient sparks'],
            customAudioPhases=['area pulse']),
        'sorceress.thunderstorm': dict(customArtwork='verified', customAudio='verified',
            customArtworkPhases=['cast cloud', 'recurring charge', 'vertical strike'],
            customAudioPhases=['initial charge', 'strike']),
        'sorceress.meteor': dict(customArtwork='verified', customAudio='verified',
            customArtworkPhases=['ground warning', 'descent', 'impact', 'persistent fire'],
            customAudioPhases=['release', 'descent', 'impact', 'shared ground fire']),
        'sorceress.blizzard': dict(customArtwork='verified', customAudio='verified',
            customArtworkPhases=['falling ice', 'ground frost', 'shard impact', 'damage contact'],
            customAudioPhases=['release', 'active wind', 'damaging shard contact']),
        'sorceress.frozenorb': dict(customArtwork='verified', customAudio='verified',
            customArtworkPhases=['rotating orb', 'directional bolts', 'damage contact', 'terminal burst'],
            customAudioPhases=['release', 'shared ice contact', 'terminal burst']),
        'necromancer.poisondagger': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Native knife swing with an ImageGen contact slash and shared timed poison overlay.',
            customArtworkPhases=['knife contact slash', 'poison droplets on the victim', 'poison dissipation'],
            customAudioPhases=['knife swing', 'landed contact', 'poison clear']),
        'necromancer.poisonexplosion': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='ImageGen burst at the corpse with shared poison mist and timed poison overlays.',
            customArtworkPhases=['corpse-centered toxic bloom', 'eight traveling clouds', 'contact', 'poison droplets and dissipation'],
            customAudioPhases=['corpse bloom', 'traveling cloud', 'poison contact', 'poison clear']),
        'necromancer.poisonnova': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='ImageGen caster ring and directional wisps follow the existing radial rays.',
            customArtworkPhases=['release ring', 'directional radial wisps', 'contact', 'poison droplets and dissipation'],
            customAudioPhases=['release', 'poison contact', 'poison clear']),
        'necromancer.raiseskeleton': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='The native warrior body emerges between rising bones and departs in a shared ash fade.',
            customArtworkPhases=['arrival and return', 'landed melee contact', 'parking, dismissal and death fade'],
            customAudioPhases=['arrival and return', 'landed melee contact', 'departure']),
        'necromancer.skeletalmage': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='The native mage body emerges through violet trails and uses shared Sorceress effects for its elemental abilities.',
            customArtworkPhases=['arrival and return', 'elemental flight and contact', 'Glacial Spike, Meteor, Chain Lightning and Static Field',
                'Frozen Armor and Enchant', 'parking, dismissal and death fade'],
            customAudioPhases=['arrival and return', 'elemental spell releases and contacts', 'Enchant', 'departure']),
        'necromancer.revive': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='An emerald spirit rises at the corpse while the original creature returns with its body and hue.',
            customArtworkPhases=['arrival and return of the original creature', 'landed melee contact', 'shared ash fade on departure'],
            customAudioPhases=['arrival and return', 'landed melee contact', 'departure']),
        'necromancer.claygolem': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='The native body emerges through clay flakes and dust, with an attached marker for its timed slow.',
            customArtworkPhases=['clay arrival and return', 'landed contact', 'outgoing and reactive slow through expiry', 'rubble on departure'],
            customAudioPhases=['arrival and return', 'landed contact', 'departure']),
        'necromancer.bloodgolem': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Scarlet ribbons accompany arrival and actual life transfer. Wounds and the Life Tap proc have separate timed markers.',
            customArtworkPhases=['scarlet arrival and return', 'landed contact', 'life gained by the golem or owner', 'Open Wounds and natural fade',
                'shared Life Tap cast, recipient marks and healing', 'rubble on departure'],
            customAudioPhases=['arrival and return', 'landed contact', 'life transfer', 'Open Wounds application', 'shared Life Tap cast and healing', 'departure']),
        'necromancer.irongolem': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Iron slivers gather near the original item. Strikes and retaliation spark, with shared visuals for the five Spectral channels.',
            customArtworkPhases=['iron arrival and return', 'landed strike and retaliation', 'inherited and Spectral elemental contacts',
                'cold and poison status', 'rubble on departure'],
            customAudioPhases=['arrival and return', 'landed strike and retaliation', 'shared elemental contact cues', 'departure']),
        'necromancer.firegolem': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Embers rise at arrival, Holy Fire draws a low ring, and a separate eruption marks the delayed death damage.',
            customArtworkPhases=['ember arrival and return', 'Holy Fire pulse and recipient hits', 'landed melee contact',
                'rubble on ordinary departure', 'delayed death eruption'],
            customAudioPhases=['arrival and return', 'Holy Fire pulse', 'landed melee contact', 'departure', 'delayed death eruption']),
        'necromancer.bonearmor': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='ImageGen orbiting bones with an absorption spark and a depletion shatter.',
            customArtworkPhases=['orbiting bone ward', 'partial absorption spark', 'depletion shatter'],
            customAudioPhases=['ward creation', 'partial absorption', 'depletion']),
        'necromancer.bonewall': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='ImageGen ribs rise on each wall segment and collapse into rubble.',
            customArtworkPhases=['rising ribs along the growing wall', 'bone chips on contact', 'collision-free rubble on collapse'],
            customAudioPhases=['wall rise', 'bone contact', 'wall collapse']),
        'necromancer.boneprison': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='ImageGen ribs and low connectors show the active prison perimeter.',
            customArtworkPhases=['rising curved ribs and low connectors', 'bone chips on contact', 'perimeter collapse'],
            customAudioPhases=['prison rise', 'bone contact', 'prison collapse']),
        'necromancer.corpseexplosion': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='ImageGen fire, smoke and bone chips burst at the consumed corpse.',
            customArtworkPhases=['corpse-centered ignition', 'explosion', 'falling debris'],
            customAudioPhases=['corpse detonation']),
        'necromancer.teeth': dict(customArtwork='verified', customAudio='verified', inGameVerification='verified',
            customArtworkPhases=['directional fan', 'contact chips'],
            customAudioPhases=['release', 'shared bone contact']),
        'necromancer.bonespirit': dict(customArtwork='verified', customAudio='verified', inGameVerification='verified',
            customArtworkPhases=['directional skull flight', 'impact burst'],
            customAudioPhases=['release', 'impact']),
        'necromancer.bonespear': dict(customArtwork='verified', customAudio='verified', inGameVerification='verified',
            customArtworkPhases=['directional flight', 'piercing contact flash', 'terminal shatter'],
            customAudioPhases=['release', 'piercing contact', 'terminal shatter']),
        'druid.werewolf': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='The approved Timber Wolf emerges through silver-blue wisps and returns through a closing spiral.',
            customArtworkPhases=['moonlit transformation', 'approved client-scaled Timber Wolf', 'manual and timed return'],
            customAudioPhases=['transformation', 'return']),
        'druid.werebear': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Ochre earth and amber leaves frame the approved Grizzly Bear and settle on return.',
            customArtworkPhases=['earth and leaf transformation', 'approved client-scaled Grizzly Bear', 'return'],
            customAudioPhases=['transformation', 'return']),
        'druid.maul': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Landed Maul scatters stone and grows an amber charge orb through three visual sizes.',
            customArtworkPhases=['landed ground impact', 'orbiting charge and tier changes', 'natural expiry and form cleanup'],
            customAudioPhases=['committed swing', 'landed impact']),
        'druid.feralrage': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Landed Feral Rage rakes the target and grows a red ember that orbits the wolf.',
            customArtworkPhases=['landed rake', 'orbiting charge and tier changes', 'form cleanup'],
            customAudioPhases=['committed swing', 'landed impact']),
        'druid.fireclaws': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Gold-orange claw trails rake the target on a successful wolf or bear contact.',
            customArtworkPhases=['landed flaming rake'],
            customAudioPhases=['committed swing', 'landed flame']),
        'druid.hunger': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A closing bite flashes at the target. Red and blue effects mark life and mana actually recovered.',
            customArtworkPhases=['landed bite in either form', 'shared life recovery', 'mana recovery'],
            customAudioPhases=['committed lunge', 'landed bite', 'actual resource recovery']),
        'druid.fury': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Brief silver claw arcs flash at each target on a landed strike in the existing sequence.',
            customArtworkPhases=['individual landed slashes across the sequence'],
            customAudioPhases=['individual committed swings', 'individual landed impacts']),
        'druid.rabies': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='An olive bite flash marks the initial attack. Chartreuse motes follow each carrier through its independent deadline.',
            customArtworkPhases=['landed bite', 'new carrier flare on spread', 'attached marker after poison cleanse', 'carrier fade'],
            customAudioPhases=['committed bite swing', 'landed bite', 'new infection', 'carrier expiry']),
        'druid.shockwave': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Ground pressure crests spread from the bear along the five source paths. Impact chips and a timed stun mark follow the committed effects.',
            customArtworkPhases=['release ripple', 'directional ground travel and dissipation', 'impact chips', 'attached stun and its expiry'],
            customAudioPhases=['bear release', 'landed impact']),
        'druid.firestorm': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Three tongues of flame follow their existing paths and leave short fire beds on the active cells.',
            customArtworkPhases=['release', 'directional flame travel', 'active fire beds and retirement', 'shared fire contact'],
            customAudioPhases=['release', 'shared ground fire', 'shared fire contact']),
        'druid.moltenboulder': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A molten rock forms and rolls, throws small chips on continuing hits, and shatters at the end of its roll. Its trail retires on its own clock.',
            customArtworkPhases=['formation', 'fractional rolling motion', 'continuing hit chips', 'terminal shatter', 'active fire trail and retirement'],
            customAudioPhases=['formation', 'rolling', 'continuing hit', 'terminal shatter', 'shared ground fire', 'shared fire contact']),
        'druid.fissure': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='The ground opens at the accepted aim point. Scattered glowing cracks render on active cells and retire with the source hazard.',
            customArtworkPhases=['opening at aim', 'active scattered cracks and retirement', 'shared fire contact'],
            customAudioPhases=['opening', 'shared ground fire', 'shared fire contact']),
        'druid.twister': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Three low silver eddies follow the existing paths. Contact flashes and the shared stun mark track committed events.',
            customArtworkPhases=['release sweep', 'three fractional traveling eddies', 'landed contact and attached stun', 'path and terrain dissipation'],
            customAudioPhases=['release', 'landed wind contact']),
        'druid.tornado': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A tall blue-gray funnel follows the missile through periodic area pulses and disperses at retirement.',
            customArtworkPhases=['release sweep', 'fractional traveling funnel', 'area pulse', 'landed contact', 'dissipation'],
            customAudioPhases=['release', 'area pulse', 'landed wind contact']),
        'druid.hurricane': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='An icy ring and orbiting tracers follow the active weather. Successful pulses flash at the recipient and share the chill overlay.',
            customArtworkPhases=['opening', 'attached storm center and rotating wisps', 'cold pulse contact and chill', 'natural expiry'],
            customAudioPhases=['opening', 'active wind bed', 'shared frost contact', 'expiry']),
        'druid.arcticblast': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A narrow directional freeze stream follows the existing held aim and shows contact chips, shared chill and a short vapor fade.',
            customArtworkPhases=['directional windup', 'fractional live stream and aim changes', 'landed ice chips and shared chill', 'emission fade after release'],
            customAudioPhases=['accepted channel start', 'live emission', 'landed contact']),
        'druid.cyclonearmor': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A wind shell follows its caster while the pool is active. Absorption flares along its rim, and the ribbons unravel at depletion.',
            customArtworkPhases=['rising wind', 'attached revolving shell', 'actual absorption flare', 'unraveling at depletion', 'state cleanup'],
            customAudioPhases=['raise', 'actual absorption', 'depletion']),
        'druid.volcano': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A lava vent rises at the accepted aim and tosses tumbling rock along the existing paths. Landed pieces leave independently timed fire.',
            customArtworkPhases=['vent opening', 'active vent', 'airborne debris', 'grounded landing and lava fire', 'shared landed contact', 'vent cooling'],
            customAudioPhases=['vent opening', 'eruption', 'debris landing', 'shared ground fire', 'shared fire contact']),
        'druid.armageddon': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A ring of cinders follows the caster. Individual falls show a warning and descend into a smaller Meteor burst before leaving fire.',
            customArtworkPhases=['weather release and caster aura', 'landing warnings', 'timed descent', 'scaled Meteor burst', 'active ground fire and contact', 'weather and independent field retirement'],
            customAudioPhases=['weather release', 'descent', 'terminal impact', 'shared ground fire', 'shared fire contact']),
        'druid.raven': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A feather swirl frames the arrival. Talon contact throws silver-violet sparks and the blindness mark follows the target.',
            customArtworkPhases=['feather arrival', 'native bird and attack', 'landed talon', 'applied blindness and cleanup', 'dismissal fade'],
            customAudioPhases=['arrival', 'landed talon', 'shared departure']),
        'druid.summonspiritwolf': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Frost rises at arrival. Cold jaws flash on contact, and the wolf reappears through the opening effect after parking.',
            customArtworkPhases=['frost arrival', 'native wolf', 'landed cold bite and shared chill', 'parking fade and return'],
            customAudioPhases=['arrival and return', 'cold contact', 'shared departure']),
        'druid.summondirewolf': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Copper leaves mark arrival, and a warm aura follows the wolf for the actual duration of its feeding rage.',
            customArtworkPhases=['copper arrival', 'native wolf', 'landed physical bite', 'corpse feeding rage and expiry', 'dismissal fade'],
            customAudioPhases=['arrival', 'landed bite', 'successful feeding', 'shared departure']),
        'druid.summongrizzly': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Dust and stones rise around the native bear. Gold slashes mark ordinary hits and heavy chips mark a landed Bear Smite.',
            customArtworkPhases=['earth arrival', 'native bear', 'ordinary hit', 'Bear Smite', 'replacement and death fade'],
            customAudioPhases=['arrival', 'landed swipe', 'shared heavy impact', 'shared departure']),
        'druid.oaksage': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Oak leaves rise at arrival and circle each recipient while its maximum-life aura is active.',
            customArtworkPhases=['leaf arrival', 'native spirit', 'life aura on eligible recipients', 'range expiry and reapplication', 'replacement fade'],
            customAudioPhases=['arrival', 'shared spirit departure']),
        'druid.heartofwolverine': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Copper wisps rise at arrival. A red-orange ring follows each recipient of the attack and damage bonuses.',
            customArtworkPhases=['copper arrival', 'native spirit', 'attached attack aura', 'replacement and aura expiry'],
            customAudioPhases=['arrival', 'shared spirit departure']),
        'druid.spiritofbarbs': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Violet brambles rise at arrival. The recipient ring follows the flat-return aura and a thorn flash marks damage on the attacker.',
            customArtworkPhases=['thorn arrival', 'native spirit', 'attached return aura', 'committed retaliation', 'parking and return', 'dismissal and expiry'],
            customAudioPhases=['arrival and return', 'committed melee retaliation', 'shared spirit departure']),
        'druid.poisoncreeper': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Directional thorn heads write a venom trail across the accepted subtiles. A contact flash marks committed poison, and the mats wither on their own timers.',
            customArtworkPhases=['thorn arrival', 'native vine', 'directional ground heads', 'active and withering mats', 'committed poison and shared status overlay', 'departure'],
            customAudioPhases=['arrival', 'trail launch', 'committed poison', 'shared departure']),
        'druid.carrionvine': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Copper roots mark arrival and frame the reserved corpse. A red flash reaches the owner when the delayed life gain applies.',
            customArtworkPhases=['root arrival', 'native vine', 'successful corpse reservation', 'applied life recovery', 'departure'],
            customAudioPhases=['arrival', 'shared corpse feeding', 'applied life recovery', 'shared departure']),
        'druid.solarcreeper': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Blue sprigs mark arrival. Roots frame the reserved corpse, then a blue flash marks the delayed mana gain when it applies.',
            customArtworkPhases=['blue arrival', 'native vine', 'successful corpse reservation', 'applied mana recovery', 'departure'],
            customAudioPhases=['arrival', 'shared corpse feeding', 'applied mana recovery', 'shared departure']),
        'paladin.holyfire': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A gold flame ring follows the active aura. Each damage pulse sweeps to its live radius, and gold sparks mark damaging pulses or landed aura-imbued weapon hits.',
            customArtworkPhases=['active caster ring', 'radius-scaled fire pulse', 'damaging pulse and landed weapon accents', 'lease cleanup'],
            customAudioPhases=['fire pulse', 'fire contact']),
        'paladin.holyfreeze': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A cyan frost ring follows the caster, with a separate frost seal on targets actually slowed. Committed shatter deaths use the shared cold-corpse burst.',
            customArtworkPhases=['active caster ring', 'radius-scaled frost pulse', 'damaging pulse and landed weapon accents', 'applied slow and expiry', 'shared corpse shatter', 'lease cleanup'],
            customAudioPhases=['frost pulse', 'frost contact', 'shared corpse shatter']),
        'paladin.holyshock': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A violet electric ring follows the active aura. Each pulse uses the live radius, and contact sparks mark pulse damage or landed aura-imbued weapon hits.',
            customArtworkPhases=['active caster ring', 'radius-scaled lightning pulse', 'damaging pulse and landed weapon accents', 'lease cleanup'],
            customAudioPhases=['lightning pulse', 'lightning contact']),
        'paladin.holybolt': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='An ivory-gold dart pierces on its accepted path. Green wisps mark the actual ally heal, a brief star marks damage, and spent bolts fade at their last point.',
            customArtworkPhases=['release flash', 'directional fractional flight', 'applied ally healing', 'piercing damage contacts', 'wall and natural expiry'],
            customAudioPhases=['release', 'applied heal', 'holy contact', 'terminal fade']),
        'paladin.blessedhammer': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A shining hammer spins along its existing curve. Small gold shards mark damage, and the hammer dissolves where the curve is blocked or ends.',
            customArtworkPhases=['release flash', 'spinning fractional curve', 'damaging contact', 'wall and natural expiry'],
            customAudioPhases=['release', 'damaging contact', 'terminal fade']),
        'paladin.fistoftheheavens': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A warning follows the selected target, then a sky strike lands at its live position. The original position emits the silver-blue piercing bolts and still bursts when the pending target dies.',
            customArtworkPhases=['target warning', 'delayed primary lightning', 'original-position ring', 'directional secondary bolts and shared contacts', 'shared bolt fade'],
            customAudioPhases=['warning', 'sky strike', 'burst', 'shared bolt contact and fade']),
        'paladin.resistfire': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Copper shields and embers circle each recipient of the active fire-resistance aura. The marker clears with expiry or replacement.',
            customArtworkPhases=['committed application', 'attached fire ward', 'renewal', 'expiry and replacement'],
            customAudioPhases=['new ward application']),
        'paladin.resistcold': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Four icy shields circle the feet of protected recipients for their active cold-resistance lease.',
            customArtworkPhases=['committed application', 'attached cold ward', 'renewal', 'expiry'],
            customAudioPhases=['new ward application']),
        'paladin.resistlightning': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A violet loop of small gold forks marks each recipient of active lightning resistance.',
            customArtworkPhases=['committed application', 'attached lightning ward', 'renewal', 'expiry'],
            customAudioPhases=['new ward application']),
        'paladin.salvation': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Three ivory shields with jade edges circle each recipient of Salvation. The outer ring can sit alongside the separate elemental wards and follows the provider deadline.',
            customArtworkPhases=['committed application', 'attached Salvation ward', 'stack with elemental wards', 'expiry and provider cutoff'],
            customAudioPhases=['new ward application']),
        'paladin.prayer': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A quiet copper loop follows each recipient of the active aura. Warm ribbons rise only when life is restored, with one cue for the useful pulse.',
            customArtworkPhases=['active recipient ring', 'applied life recovery', 'lease expiry or insufficient mana'],
            customAudioPhases=['applied life recovery']),
        'paladin.cleansing': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Violet filaments scatter dark flecks when Cleansing shortens poison or a curable curse. Any actual Prayer-synergy healing shares the amber ribbons.',
            customArtworkPhases=['active recipient ring', 'applied poison or curse shortening', 'shared Prayer healing and expiry'],
            customAudioPhases=['purification or shared healing']),
        'paladin.meditation': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A blue ring follows each eligible player. Native mana recovery raises a small blue spark, and actual Prayer-synergy healing shares the warm ribbons.',
            customArtworkPhases=['active player ring', 'native mana gain', 'shared Prayer healing', 'lease expiry'],
            customAudioPhases=['new aura application', 'shared applied life recovery']),
        'paladin.redemption': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='An ochre ring marks the caster. A claimed corpse releases gold sparks. A short jade thread joins the corpse and caster when it funds a red life or blue mana flash.',
            customArtworkPhases=['active caster ring', 'successful corpse claim', 'resource transfer thread', 'actual life and mana receipts', 'lease expiry'],
            customAudioPhases=['corpse claim', 'applied resource receipt']),
        'paladin.might': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Copper pennants follow each recipient of the active damage bonus and retire when its lease expires.',
            customArtworkPhases=['committed application', 'attached Might pennants', 'renewal and lease expiry'],
            customAudioPhases=['new aura application']),
        'paladin.blessedaim': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Teal arrowheads follow recipients of the active accuracy aura. The banner tracks the applied lease.',
            customArtworkPhases=['committed application', 'attached accuracy runes', 'renewal and lease expiry'],
            customAudioPhases=['new aura application']),
        'paladin.concentration': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Amethyst standards follow the active recipients. A purple brace flashes when Concentration prevents hit recovery during a committed action.',
            customArtworkPhases=['committed application', 'attached Concentration standards', 'successful interruption guard', 'renewal and lease expiry'],
            customAudioPhases=['new aura application', 'successful interruption guard']),
        'paladin.fanaticism': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Crimson pennants circle the caster and allies with the active attack bonus. The faster animation follows the received lease.',
            customArtworkPhases=['committed application', 'attached Fanaticism pennants', 'party overlap', 'renewal and lease expiry'],
            customAudioPhases=['new aura application']),
        'paladin.defiance': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Steel-blue crenellations follow players and pets receiving the defense bonus.',
            customArtworkPhases=['new aura application', 'attached defense ring', 'renewal and lease expiry'],
            customAudioPhases=['new aura application']),
        'paladin.vigor': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A fast loop of mint leaves marks players and pets receiving the stamina and movement bonuses.',
            customArtworkPhases=['new aura application', 'attached stamina and movement ring', 'renewal and lease expiry'],
            customAudioPhases=['new aura application']),
        'paladin.thorns': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Plum hooks surround the protected recipient. A separate burst marks the attacker when a native Thorns return deals damage.',
            customArtworkPhases=['first active pulse', 'attached Thorns ring', 'damaging melee return', 'renewal and lease expiry'],
            customAudioPhases=['new aura application', 'damaging melee return']),
        'paladin.holyshield': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Gold light gathers on release into a pair of shield braces. A star flashes on a real block, and the braces fall away when the buff expires.',
            customArtworkPhases=['paid release', 'equipped shield overlay', 'successful shield block', 'stow and re-equip', 'timed expiry'],
            customAudioPhases=['paid release', 'successful shield block', 'timed expiry']),
        'paladin.sanctuary': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Ivory lanterns circle the Paladin. A blue-white wave traces the actual pulse radius, and a star marks affected undead and landed attacks benefiting from the aura.',
            customArtworkPhases=['caster lantern ring', 'radius-scaled pulse', 'applied undead damage or knockback', 'landed undead weapon contact', 'supplier expiry'],
            customAudioPhases=['pulse', 'applied undead damage or knockback', 'landed undead weapon contact']),
        'paladin.conviction': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A garnet and brass ring marks the caster. A ruby flash settles into a floating mark when an enemy receives the debuff.',
            customArtworkPhases=['caster ring', 'new debuff or stronger replacement', 'attached enemy mark', 'hostile lease expiry'],
            customAudioPhases=['first active pulse', 'new debuff or stronger replacement']),
        'paladin.sacrifice': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A garnet swing arc and impact use separate effects. Red ribbons appear at the Paladin when the life payment actually lands.',
            customArtworkPhases=['committed swing', 'life payment including lethal payment', 'qualified melee contact'],
            customAudioPhases=['swing', 'life payment', 'qualified melee contact']),
        'paladin.smite': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='An ivory shield sweep breaks into steel and gold on contact. The shared stun markers follow the target during native timed displacement.',
            customArtworkPhases=['shield swing', 'qualified shield impact', 'shared stun mark during displacement and expiry'],
            customAudioPhases=['shield swing', 'qualified shield impact']),
        'paladin.vengeance': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Fire, frost and lightning braid around the swing and split into three separate shapes on contact. Actual cold uses the shared chill status.',
            customArtworkPhases=['committed swing', 'qualified tri-element contact', 'shared active chill and expiry'],
            customAudioPhases=['swing', 'qualified tri-element contact']),
        'paladin.zeal': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Short bronze arcs follow the timed series. Only landed swings spark at the selected enemy. The low-mana fallback uses ordinary attack presentation.',
            customArtworkPhases=['each committed swing', 'landed contact on the current target', 'ordinary low-mana fallback'],
            customAudioPhases=['individual swing', 'landed contact']),
        'paladin.charge': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Teal force flares on launch, and fading afterimages mark completed steps. The impact plays at qualified hits and actual knockback. An adjacent target uses the ordinary attack presentation.',
            customArtworkPhases=['movement launch', 'afterimages on completed steps', 'qualified hit or knockback', 'blocked path and adjacent fallback'],
            customAudioPhases=['movement launch', 'completed travel', 'qualified hit or knockback']),
        'paladin.conversion': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A jade swing precedes the result. On success, a knot forms and follows the converted creature until it breaks at expiry. A failed conversion shares the ordinary bronze contact on a landed hit.',
            customArtworkPhases=['committed swing', 'successful conversion', 'moving allegiance marker', 'allegiance expiry', 'unconverted landed weapon contact'],
            customAudioPhases=['committed swing', 'successful conversion', 'allegiance expiry', 'shared weapon contact on a failed conversion']),
        'barbarian.howl': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A steel-blue cry sends wave fronts out along the live rays. An attached fear mark follows affected enemies as they retreat and fades when Terror expires.',
            customArtworkPhases=['paid release', 'collision-aware wave', 'applied Terror and retreat', 'shared expiry fade'],
            customAudioPhases=['male and female release', 'applied Terror', 'shared expiry cue']),
        'barbarian.taunt': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Copper flares at release. A rose mark follows the target while the untimed damage and attack penalties remain.',
            customArtworkPhases=['paid release', 'applied penalties and approach', 'attached mark until clear or replacement'],
            customAudioPhases=['male and female release', 'applied penalties']),
        'barbarian.battlecry': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='An amber wave follows the live rays. A broken shield marks enemies whose damage and defense are reduced, then fades at expiry.',
            customArtworkPhases=['paid release', 'collision-aware wave', 'applied defense and damage penalties', 'shared expiry fade'],
            customAudioPhases=['male and female release', 'applied penalties', 'shared expiry cue']),
        'barbarian.warcry': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='An ivory and blue pressure wave follows the live rays. An impact flashes at qualified contacts, including a stun through physical immunity. The shared stun marker follows the target for the active stun.',
            customArtworkPhases=['paid release', 'collision-aware wave', 'qualified contact', 'shared stun and expiry', 'existing shield block feedback'],
            customAudioPhases=['male and female release', 'qualified contact']),
        'barbarian.shout': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Steel-blue ribs open at release. The wave follows the live rays, and shield braces follow recipients for the duration of the defense buff.',
            customArtworkPhases=['release', 'collision-aware wave', 'caster and allied defense braces', 'independent expiry'],
            customAudioPhases=['male and female release', 'new allied defense buff', 'shared expiry cue']),
        'barbarian.battleorders': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Crimson, blue, and green ribbons carry life, mana, and stamina outward. Three lights follow each recipient and fade when their increased resource maxima expire.',
            customArtworkPhases=['release', 'collision-aware wave', 'caster and allied resource lights', 'independent expiry'],
            customAudioPhases=['male and female release', 'new allied resource buff', 'shared expiry cue']),
        'barbarian.battlecommand': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Gold chevrons advance along the live rays. A gold and lilac heraldic ring follows recipients while their skill rank is raised.',
            customArtworkPhases=['release', 'collision-aware wave', 'caster and allied skill-rank ring', 'independent expiry'],
            customAudioPhases=['male and female release', 'new allied skill-rank buff', 'shared expiry cue']),
        'barbarian.findpotion': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A green spiral and corpse sweep accompany the search. Actual potion creation brings up a glass vial, and an empty search leaves faint green dust.',
            customArtworkPhases=['committed search', 'corpse sweep', 'potion discovered', 'empty result'],
            customAudioPhases=['committed search', 'potion discovered', 'shared empty result']),
        'barbarian.finditem': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='An amber flash sweeps the corpse. Actual added loot brings up a gold gleam, and an empty search settles into brown dust.',
            customArtworkPhases=['committed search', 'corpse sweep', 'loot added', 'empty result'],
            customAudioPhases=['committed search', 'loot added', 'shared empty result']),
        'barbarian.grimward': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A bone totem rises in the corpse size class, pulses over its active reach, and collapses at expiry. Violet thorns follow the affected enemy while its curse lasts.',
            customArtworkPhases=['small, medium, and large startup', 'three active totem sizes', 'radius-scaled pulse', 'applied curse', 'totem collapse', 'shared curse fade'],
            customAudioPhases=['ward rising', 'active area pulse', 'newly affected enemy', 'ward collapse', 'shared curse fade cue']),
        'barbarian.bash': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='An amber arc leads into a bronze impact. Dust marks completed knockback steps. The impact remains visible if terrain stops the target.',
            customArtworkPhases=['committed swing', 'qualified impact', 'completed knockback'],
            customAudioPhases=['swing', 'qualified impact', 'completed knockback']),
        'barbarian.stun': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A steel-blue swing breaks into a contact flare, with a pale spiral when the target is stunned. The shared stun mark shows the active duration.',
            customArtworkPhases=['committed swing', 'qualified impact', 'applied stun and shared status', 'ordinary attack fallback'],
            customAudioPhases=['swing', 'qualified impact', 'applied stun']),
        'barbarian.concentrate': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Jade braces frame the caster throughout the defense window. A narrow sweep and a faceted impact mark the strike. The braces clear when the action ends.',
            customArtworkPhases=['action-long defense braces', 'committed swing', 'qualified impact', 'recovery or invalidation clears defense'],
            customAudioPhases=['defense begins', 'swing', 'qualified impact']),
        'barbarian.berserk': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A violet ring shows the zero-defense state. Amethyst sparks mark the swing and magic impact. The ring breaks when defense returns.',
            customArtworkPhases=['zero-defense state', 'committed swing', 'qualified magic impact', 'vulnerability expiry'],
            customAudioPhases=['zero-defense state begins', 'swing', 'qualified magic impact', 'shared expiry cue']),
        'barbarian.leap': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Dust marks takeoff, curves along the flight, and pulses over the accepted landing radius. Interrupted travel settles into a smaller dust effect.',
            customArtworkPhases=['takeoff', 'eight-direction airborne arc', 'knockback radius', 'interrupted landing'],
            customAudioPhases=['takeoff', 'landing', 'interrupted landing']),
        'barbarian.leapattack': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='The shared jump arc leads to a copper ground ring and debris at damaged targets. The later weapon swing has its own sweep and impact.',
            customArtworkPhases=['shared takeoff and arc', 'landing radius', 'area contact', 'weapon sweep', 'landed weapon impact'],
            customAudioPhases=['shared takeoff', 'area landing', 'weapon swing', 'landed weapon impact']),
        'barbarian.whirlwind': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A blue ring follows the caster, with a wake at completed steps, sparks on landed hits, and a fade at the last reached tile.',
            customArtworkPhases=['sustained spin', 'completed movement wake', 'landed contact', 'end or blocked-path fade'],
            customAudioPhases=['spin begins', 'completed movement wake', 'landed contact', 'spin ends']),
        'barbarian.doubleswing': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Two opposing steel sweeps mark the primary and secondary releases. Gold and ivory flashes mark the two landed contacts. The ordinary fallback keeps its weapon presentation.',
            customArtworkPhases=['primary swing', 'secondary swing', 'independent landed contacts', 'ordinary fallback'],
            customAudioPhases=['primary and secondary swings', 'primary and secondary impacts']),
        'barbarian.frenzy': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Opposing copper-red sweeps and sparks mark the weapon events. A low ring follows the caster while the speed charges are active and fades at expiry.',
            customArtworkPhases=['primary swing', 'secondary swing', 'landed contact', 'active speed state', 'speed expiry'],
            customAudioPhases=['primary and secondary swings', 'landed contact', 'new speed charge', 'speed expiry']),
        'barbarian.doublethrow': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Each weapon retains its launch timing and native course. Knives and axes spin. Javelins, glaives, and pila point along the flight. A compact spark marks landed contacts and blocked terrain.',
            customArtworkPhases=['per-hand launch', 'spinning knife and axe', 'directional javelin, glaive, and pilum', 'landed contact', 'terrain contact'],
            customAudioPhases=['knife, axe, and polearm releases', 'landed or terrain impact']),
        'assassin.tigerstrike': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Amber claw sweeps and a gold contact flash. Up to three tiger-eye motes follow the player and update when gained, spent, or expired.',
            customArtworkPhases=['weapon release', 'landed builder', 'one to three charge motes', 'finisher contact', 'charge retirement'],
            customAudioPhases=['swing', 'contact', 'charge gain', 'expiry']),
        'assassin.cobrastrike': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Green claw sweeps and serpent-scale sparks feed up to three motes. A common finisher shows ribbons only for life and mana actually recovered.',
            customArtworkPhases=['weapon release', 'landed builder', 'one to three charge motes', 'finisher contact', 'actual life and mana recovery', 'charge retirement'],
            customAudioPhases=['swing', 'contact', 'charge gain', 'recovery', 'expiry']),
        'assassin.fistsoffire': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Copper claws build ember motes. The second charge adds a ground pulse, and the third lights the accepted scatter tiles.',
            customArtworkPhases=['per-hand swing', 'landed builder', 'one to three embers', 'finisher contact', 'area ring', 'scatter fire and fade'],
            customAudioPhases=['swing', 'contact', 'charge gain', 'area burst', 'shared ground fire', 'expiry']),
        'assassin.clawsofthunder': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Charged claw arcs build yellow motes. At higher tiers, an electric ring opens onto radial rays and then moving bolts.',
            customArtworkPhases=['per-hand swing', 'landed builder', 'one to three charged motes', 'finisher contact', 'nova and bolt paths', 'projectile contact'],
            customAudioPhases=['swing', 'contact', 'charge gain', 'nova and bolt release', 'expiry']),
        'assassin.bladesofice': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Ice claws build cyan motes. Higher tiers add a shard ring and stationary crystals on the accepted cells. Cold statuses use the shared overlays.',
            customArtworkPhases=['per-hand swing', 'landed builder', 'one to three cyan motes', 'finisher contact', 'area ring', 'stationary crystals and cold effects'],
            customAudioPhases=['swing', 'contact', 'charge gain', 'area shatter', 'expiry']),
        'assassin.phoenixstrike': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Violet and gold claw sweeps build feather motes. Spending the first, second, or third tier releases a meteor, chain lightning, or chaos ice.',
            customArtworkPhases=['weapon release', 'landed builder', 'one to three feather motes', 'finisher contact', 'meteor warning, descent, impact and fire', 'chain lightning', 'chaos ice'],
            customAudioPhases=['swing', 'contact', 'charge gain', 'shared meteor and ground fire', 'expiry']),
        'assassin.dragontalon': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Amber sweeps mark the scheduled kicks. Landed hits flash on the selected target, blocks scatter pale sparks, and dust marks completed final knockback.',
            customArtworkPhases=['directional per-kick sweep', 'landed contact', 'shield deflection', 'completed knockback', 'charge payloads'],
            customAudioPhases=['per-kick release', 'landed contact', 'shield deflection', 'completed knockback']),
        'assassin.dragonclaw': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Opposing lilac and steel-blue sweeps mark the primary and secondary releases. Each landed strike uses its own contact flash and spends its own charges.',
            customArtworkPhases=['directional primary and secondary sweeps', 'independent landed contacts', 'shared block and charge effects'],
            customAudioPhases=['primary and secondary releases', 'separate contacts', 'shared block and charge cues']),
        'assassin.dragontail': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A low copper sweep and kick flare precede a ring matching the fire burst radius. Embers mark eligible fire contacts and completed knockback steps.',
            customArtworkPhases=['directional kick', 'landed direct contact', 'radius-scaled burst', 'fire recipients and completed knockback'],
            customAudioPhases=['kick', 'direct contact', 'fire burst', 'completed knockback']),
        'assassin.dragonflight': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Indigo ribbons close at the origin and blue wisps open at the accepted landing. The later kick has a separate directional sweep and contact flare.',
            customArtworkPhases=['committed departure', 'validated arrival', 'directional later kick', 'landed contact', 'shared block and charge effects'],
            customAudioPhases=['departure', 'arrival', 'later kick', 'landed contact', 'shared block and charge cues']),
        'assassin.bladesentinel': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A teal steel blade emerges and spins along the summoned actor’s retained position, flashes at a landed cut, and crumbles when the sentinel retires.',
            customArtworkPhases=['emergence', 'moving summon body', 'landed cut', 'shared shield deflection', 'retirement'],
            customAudioPhases=['emergence', 'landed cut', 'shared shield deflection', 'retirement']),
        'assassin.bladefury': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A hand-level glint marks each emitted blade. The blade tumbles along its original flight. Contacts, deflections and spent fragments have separate cues.',
            customArtworkPhases=['per-shot release', 'eight-direction tumbling blade', 'landed contact', 'shared shield deflection', 'spent fragment'],
            customAudioPhases=['per-shot release', 'landed contact', 'shared shield deflection', 'retirement']),
        'assassin.bladeshield': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Steel fragments rise at cast. The orbit follows the active shield and sparks mark accepted periodic contacts. The blades disperse at expiry.',
            customArtworkPhases=['cast', 'active orbit', 'periodic contact', 'shared shield deflection', 'expiration'],
            customAudioPhases=['cast', 'periodic contact', 'shared shield deflection', 'expiration']),
        'assassin.burstofspeed': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Copper ribbons rise on release and low wisps follow the active speed state. A small flare marks natural expiry.',
            customArtworkPhases=['cast', 'attached speed wisps', 'expiry or replacement'],
            customAudioPhases=['cast', 'natural expiry']),
        'assassin.fade': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Violet ribbons curl around the caster as the existing body opacity changes. The veil follows the active Fade state.',
            customArtworkPhases=['cast', 'active veil and existing body opacity', 'expiry or replacement'],
            customAudioPhases=['cast', 'natural expiry']),
        'assassin.venom': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Jade curls mark weapon coating. A small hand-level claw follows the buff and the shared poison visuals show a delivered poison.',
            customArtworkPhases=['cast', 'weapon coating', 'shared poison application and clear', 'coating fade'],
            customAudioPhases=['cast', 'shared poison clear', 'coating expiry']),
        'assassin.cloakofshadows': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A low wave marks the caster-centered radius. The mantle follows the caster while distinct dark and pale marks show blinded and defense-only recipients.',
            customArtworkPhases=['radius-scaled cast', 'caster mantle', 'blindness and defense-only marks', 'expiry'],
            customAudioPhases=['area release', 'mantle and target expiry']),
        'assassin.psychichammer': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A violet knot flashes at release and a vertical spike lands on the accepted impact tile before displacement.',
            customArtworkPhases=['cast', 'target impact'],
            customAudioPhases=['cast', 'target impact']),
        'assassin.mindblast': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Magenta fragments mark the committed area and contacts. Shared stun and temporary allegiance visuals follow their respective outcomes.',
            customArtworkPhases=['cast', 'radius-scaled release', 'target impact', 'shared stun and conversion with expiry'],
            customAudioPhases=['cast', 'area contacts', 'shared conversion and release']),
        'assassin.fireblast': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A tumbling ember follows the lob to its accepted landing. The bomb flashes through its existing fuse before a radius-scaled burst, with sparks on damaged targets.',
            customArtworkPhases=['throw', 'tumbling bomb', 'confirmed landing and fuse', 'fractional ground burst', 'shared damaged-target sparks'],
            customAudioPhases=['throw', 'landing fuse', 'detonation']),
        'assassin.shockweb': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Blue shards tumble to their accepted landings. Each half-tile node crackles in place, flashes on a landed shock, and unravels at retirement.',
            customArtworkPhases=['throw', 'tumbling shards', 'independent half-tile fields', 'confirmed shock', 'natural or terrain retirement'],
            customAudioPhases=['throw', 'arming', 'confirmed shock', 'retirement']),
        'assassin.chargedboltsentry': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A blue crystal coil emerges, throws small forks along their existing jagged paths, and drops glowing debris at retirement.',
            customArtworkPhases=['throw', 'emergence and idle', 'shot flash', 'jagged ground travel', 'shared electric contact', 'collapse'],
            customAudioPhases=['placement', 'shot', 'shared electric contact', 'collapse']),
        'assassin.wakeoffire': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A low bronze brazier emerges before sending a fire crest that spreads along the source wave paths.',
            customArtworkPhases=['throw', 'emergence and idle', 'shot flash', 'travel and perpendicular fire waves', 'shared fire contact', 'collapse'],
            customAudioPhases=['placement', 'shot', 'shared fire contact', 'collapse']),
        'assassin.lightningsentry': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A silver fork lights up violet and sends the shared Lightning streak along its own fractional missile path.',
            customArtworkPhases=['throw', 'emergence and idle', 'shot flash', 'straight directional lightning', 'shared lightning contact', 'collapse'],
            customAudioPhases=['placement', 'shot', 'shared lightning contact', 'collapse']),
        'assassin.wakeofinferno': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A tall iron brazier emits a narrow ribbon of fire during its existing bursts and collapses into embers.',
            customArtworkPhases=['throw', 'emergence and idle', 'shot flash', 'directional flame stream', 'shared fire contact', 'collapse'],
            customAudioPhases=['placement', 'burst', 'shared fire contact', 'collapse']),
        'assassin.deathsentry': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A bone and iron sentry fires lightning or erupts at the consumed corpse, with the ground ring scaled to the actual blast radius.',
            customArtworkPhases=['throw', 'emergence and idle', 'shot flash', 'directional lightning and shared contact', 'radius-scaled corpse blast', 'collapse'],
            customAudioPhases=['placement', 'lightning shot and shared contact', 'corpse blast', 'collapse']),
        'assassin.shadowwarrior': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='An indigo cast flash precedes the summon. Wisps rise at the accepted arrival tile, follow the native body at its feet, and disperse when it leaves or dies.',
            customArtworkPhases=['cast', 'arrival and return', 'moving foot veil', 'departure and death', 'shared modifier death nova'],
            customAudioPhases=['cast', 'arrival and return', 'departure and death', 'shared modifier death nova']),
        'assassin.shadowmaster': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A magenta facet opens a purple threshold. Gold sparks mark the native Shadow while it is present, then disperse in a plume at departure or death.',
            customArtworkPhases=['cast', 'arrival and return', 'moving foot veil', 'departure and death', 'shared modifier death nova'],
            customAudioPhases=['cast', 'arrival and return', 'departure and death', 'shared modifier death nova']),
        'assassin.weaponblock': dict(customArtwork='pending', customAudio='pending', customCharacterAnimation='pending'),
        'warlock.abyss': dict(customArtwork='verified', customAudio='verified',
            customArtworkPhases=['opening tear', 'persistent vortex', 'pull pulse', 'collapse'],
            customAudioPhases=['opening', 'active pull', 'terminal collapse']),
        'warlock.summongoatman': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Rust-red stone frames the original Gargoyle on arrival. Rock chips mark accepted melee hits and the shared Berserk glow follows its active state.',
            customArtworkPhases=['cast', 'arrival and return', 'melee contact', 'shared Berserk and fade', 'departure'],
            customAudioPhases=['cast', 'arrival and return', 'melee contact', 'shared Berserk and fade', 'departure']),
        'warlock.summontainted': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Embers frame the approved Succubus on arrival and circle its feet while it emits fire resistance. The pet fires the approved red flare.',
            customArtworkPhases=['cast', 'arrival and return', 'approved Succubus sprites', 'fire resistance ward', 'red flare flight and impact', 'departure'],
            customAudioPhases=['cast', 'arrival and return', 'red flare release and impact', 'departure']),
        'warlock.summondefiler': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Burgundy plumes frame the original Gazer. Health Link marks actual recipients and flashes at the target when typed damage transfers.',
            customArtworkPhases=['cast', 'arrival and return', 'link release', 'recipient link mark and clear', 'transferred damage contact', 'departure'],
            customAudioPhases=['cast', 'arrival and return', 'link release', 'transferred damage contact', 'departure']),
        'warlock.deathmark': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A violet tear frames each demon that relocates. The target glyph follows the active mark and clears when it expires.',
            customArtworkPhases=['cast', 'warp departure and landing', 'target glyph and expiry'],
            customAudioPhases=['cast', 'warp landing', 'target glyph']),
        'warlock.bloodboil': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Red blooms mark the donors and rings expand to the actual damage radius. Only damaged enemies flash.',
            customArtworkPhases=['cast', 'donor bloom', 'radius-scaled burst', 'damage contact'],
            customAudioPhases=['cast', 'donor burst', 'damage contact']),
        'warlock.engorge': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A corpse trace sends crimson threads to the affected demons. Healing flashes on each recipient and a ward follows the timed buff.',
            customArtworkPhases=['cast', 'corpse trace', 'recipient tether', 'heal contact', 'ward and expiry'],
            customAudioPhases=['cast', 'corpse trace', 'heal contact']),
        'warlock.consume': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='The selected demon dissolves into a tether. The caster wears an amber-red ward until its sacrifice slot clears.',
            customArtworkPhases=['cast', 'demon dissolve', 'owner tether', 'ward and fade'],
            customAudioPhases=['cast', 'demon dissolve', 'ward fade']),
        'warlock.binddemon': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='The caster channels an indigo rune. Paid attempts send spectral links to the target and successful binding seals the original creature.',
            customArtworkPhases=['channel and cancellation', 'paid attempt tether', 'success flash', 'bound seal'],
            customAudioPhases=['channel start', 'paid attempt', 'successful bind']),
        'warlock.ringoffire': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A low ember ring opens at the caster and spreads along sampled native rays. Embers bloom where the rays burst and flash at confirmed contacts.',
            customArtworkPhases=['cast', 'fractional ray travel', 'ray burst and retirement', 'damage contact'],
            customAudioPhases=['cast', 'confirmed damage contact']),
        'warlock.flamewave': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Saffron sparks reveal the wave, which rolls along clear lanes and leaves low flames in native burn cells. Blocked lanes break apart at the wall.',
            customArtworkPhases=['cast', 'lateral reveal', 'moving front', 'shared direct contact', 'terrain stop and lane fade', 'lingering fire and expiry'],
            customAudioPhases=['cast', 'front launch', 'confirmed direct contact']),
        'warlock.apocalypse': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A two-second fissure marks the configured radius. A tall eruption breaks through its center and scorched cinders mark damaged enemies.',
            customArtworkPhases=['cast and radius warning', 'delayed eruption and warning retirement', 'damage contact'],
            customAudioPhases=['warning', 'eruption', 'confirmed damage contact']),
        'warlock.sigillethargy': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Teal hooks trace the active radius. An hourglass follows each slowed target and clears with its lease.',
            customArtworkPhases=['cast', 'pulsing radius boundary', 'moving slow mark and expiry', 'replacement and field fade'],
            customAudioPhases=['cast', 'newly marked recipients', 'field fade']),
        'warlock.sigilrancor': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Bronze teeth trace the active radius. A triangle marks each affected target and gold curls flash when Rancor starts an effective confusion lease.',
            customArtworkPhases=['cast', 'pulsing radius boundary', 'primary mark and expiry', 'effective confusion onset', 'replacement and field fade'],
            customAudioPhases=['cast', 'effective confusion onset', 'field fade']),
        'warlock.sigildeath': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A magenta ring marks the field. A marked death bursts from the actual consumed corpse and scatters bone chips over its four-tile blast.',
            customArtworkPhases=['cast', 'pulsing radius boundary', 'target mark and expiry', 'corpse burst and radius ring', 'recipient hit', 'replacement and field fade'],
            customAudioPhases=['cast', 'newly marked recipients', 'corpse burst', 'field fade']),
        'warlock.miasmabolt': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='An indigo bolt shows its fractional travel and actual collision. Violet wisps mark the stationary magic clouds and clear at expiry.',
            customArtworkPhases=['cast', 'eight-direction bolt travel', 'recipient or terrain burst', 'confirmed magic hit', 'rank-scaled cloud and fade'],
            customAudioPhases=['cast', 'terminal burst', 'confirmed magic hit']),
        'warlock.miasmachain': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A violet clasp latches at the real head. Bright links show each accepted span and lower wisps remain along its prior stamps until they expire.',
            customArtworkPhases=['cast', 'eight-direction head and latch', 'live span and cut', 'confirmed magic hit', 'frozen path cloud and fade'],
            customAudioPhases=['cast', 'latch', 'accepted span pulse', 'confirmed magic hit', 'link fade']),
        'warlock.hexbane': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Amethyst thorns follow the animated weapon. A thistle marks accepted targets, including on an eligible melee miss, and a separate glint shows landed weapon contact.',
            customArtworkPhases=['cast', 'animated weapon coat', 'target mark and expiry', 'landed weapon contact', 'coat replacement and fade'],
            customAudioPhases=['cast', 'new target mark', 'landed weapon contact', 'coat fade']),
        'warlock.hexpurge': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Copper crescents follow the animated weapon. A triangle marks eligible targets until expiry or depletion, and confirmed charges produce a two-tile blast.',
            customArtworkPhases=['cast', 'animated weapon coat', 'charged target mark', 'radius burst and damage contact', 'depletion and coat fade'],
            customAudioPhases=['cast', 'new target mark', 'confirmed burst', 'coat fade']),
        'warlock.hexsiphon': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Teal leaves follow the animated weapon and a drop marks accepted targets. Mint and blue ribbons show actual kill-based life and mana gains independently.',
            customArtworkPhases=['cast', 'animated weapon coat', 'target mark and expiry', 'separate life and mana returns', 'coat replacement and fade'],
            customAudioPhases=['cast', 'new target mark', 'actual recovery', 'coat fade']),
        'warlock.psychicward': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Blue glass facets surround the caster while the physical pool is active. The rim flashes on absorption and shatters on depletion. Qualified melee reactions share the stun art.',
            customArtworkPhases=['cast and recast', 'physical shell', 'absorption flash', 'pool shatter', 'shared attacker stun and clear'],
            customAudioPhases=['cast', 'physical absorption', 'pool shatter', 'accepted melee stun']),
        'warlock.eldritchblast': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Lilac curls follow the caster. Accepted Hex refreshes produce a radius ring, and bright contacts are reserved for recipients that actually lose life.',
            customArtworkPhases=['cast', 'persistent caster aura', 'radius discharge', 'refresh-only spark', 'confirmed damage contact', 'shared life and mana ribbons', 'expiry fade'],
            customAudioPhases=['cast', 'accepted discharge', 'confirmed damage contact', 'shared recovery', 'expiry fade']),
        'warlock.cleave': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A silver-violet sweep follows the committed aim of a paid action. Each landed hit that reduces enemy life shows a separate flash.',
            customArtworkPhases=['eight-direction paid sweep', 'confirmed damage contact'],
            customAudioPhases=['paid sweep', 'confirmed damage contact']),
        'warlock.bladewarp': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='A pale-blue astral blade follows the collision-checked flight. Its impact marks the two-tile radius, and the departure and arrival effects play after a successful transfer.',
            customArtworkPhases=['cast', 'eight-direction fractional blade flight', 'impact and radius', 'confirmed damage contact', 'accepted departure and arrival'],
            customAudioPhases=['cast', 'impact', 'confirmed damage contact', 'accepted departure and arrival']),
        'warlock.echoingstrike': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='Mint spectral knives follow the outbound fan, turn at the existing range or wall, and take on a cooler tint when returning. Shield deflections and damaging contacts have separate flashes.',
            customArtworkPhases=['release', 'eight-direction outward and returning flight', 'range or wall turnaround', 'confirmed damage and shield deflection', 'terminal fade'],
            customAudioPhases=['release', 'turnaround', 'confirmed damage contact', 'shield deflection', 'terminal fade']),
        'warlock.mirroredblades': dict(customArtwork='verified', customAudio='verified', customCharacterAnimation='reuse-native',
            inGameVerification='verified', placeholder='One native swing or shot releases the primary stroke and spectral copies. Their screen offsets preserve the original melee and projectile collision geometry.',
            customArtworkPhases=['release', 'directional primary and copy sweeps', 'bow arrow, crossbow quarrel, and throwing flight', 'confirmed damage contact', 'undamaged projectile retirement'],
            customAudioPhases=['release', 'confirmed damage contact', 'undamaged projectile retirement']),
    }
    for effect, row in zip(effects, rows, strict=True):
        talent = effect['talentId']
        if talent in native_effects:
            effect.update(effects='native', generation='uo', clientMapping='mapped', placeholder=native_effects[talent])
            if talent in ('druid.werewolf', 'druid.werebear'):
                effect['inGameVerification'] = 'verified'
        if not row['passive']:
            effect.update(customArtwork='pending', customAudio='pending')
        effect.update(custom_presentation.get(talent, {}))
        if talent in ('amazon.magicarrow', 'amazon.multipleshot', 'amazon.guidedarrow', 'amazon.strafe',
                      'amazon.firearrow', 'amazon.explodingarrow', 'amazon.immolationarrow',
                      'amazon.coldarrow', 'amazon.icearrow', 'amazon.freezingarrow'):
            effect.update(inGameVerification='verified',
                placeholder='Native shooting poses with ImageGen arrows, bolts and effects. Sonniss cues verified in ClassicUO.')
        if talent in ('amazon.lightningbolt', 'amazon.lightningfury', 'amazon.poisonjavelin', 'amazon.plaguejavelin'):
            effect.update(inGameVerification='verified',
                placeholder='Native throwing poses with ImageGen javelins and effects. Sonniss cues verified in ClassicUO.')
        if talent in ('amazon.jab', 'amazon.fend', 'amazon.impale', 'amazon.powerstrike', 'amazon.chargedstrike', 'amazon.lightningstrike'):
            effect.update(inGameVerification='verified',
                placeholder='Native weapon poses with ImageGen impact and status art. Sonniss cues verified in ClassicUO.')
        if talent in ('amazon.innersight', 'amazon.slowmissiles', 'amazon.decoy', 'amazon.valkyrie'):
            effect.update(inGameVerification='verified',
                placeholder='ImageGen cast, status and summon effects with native poses and bodies where applicable. Sonniss cues verified in ClassicUO.')
        if talent == 'necromancer.bonespear':
            effect['placeholder'] = 'ImageGen spear flight, piercing flash and terminal shatter. Sonniss cues verified in ClassicUO.'
        if talent == 'necromancer.teeth':
            effect['placeholder'] = 'ImageGen directional bone shards and contact chips with Sonniss release and shared bone contact cues.'
        if talent == 'necromancer.bonespirit':
            effect['placeholder'] = 'ImageGen skull flight and impact burst with Sonniss release and impact cues.'
        if talent == 'warlock.abyss':
            effect.update(inGameVerification='verified',
                placeholder='ImageGen opening tear, persistent vortex and collapse with Sonniss opening, pull and collapse cues.')
        if talent.startswith('sorceress.') and effect.get('customArtwork') == 'verified' and effect.get('customAudio') == 'verified':
            effect.update(inGameVerification='verified',
                placeholder='ImageGen effects and Sonniss cues.')
        if talent == 'sorceress.fireball':
            effect['placeholder'] = 'Approved Sorceress Fireball presentation.'
    class_stats = [r for r in json.loads((ROOT/'data/Sanctuary/Balance/classes.json').read_text()) if r['name'] != 'Warlock'] + [warlock_stats()]
    hydra_animation = json.loads((ROOT/'tools/reference/hydra-engine-evidence.json').read_text())['animation']
    monstats2=json.loads((SOURCE/'json/monstats2.json').read_text())
    corpse_rules=[dict(source=r['Id'],selectable=bool(monstats2.get(r.get('MonStatsEx'),{}).get('corpseSel')),noAura=bool(r.get('noAura')),soft=bool(monstats2.get(r.get('MonStatsEx'),{}).get('soft'))) for r in monsters.values() if r.get('Id')]
    conversion_rules=[dict(source=r['Id'],base=r.get('BaseId',r['Id']),evil=not bool(r.get('Align',0)),
        switchAi=bool(r.get('switchai')),walk=bool(monstats2.get(r.get('MonStatsEx'),{}).get('mWL')),boss=bool(r.get('boss')),run=bool(monstats2.get(r.get('MonStatsEx'),{}).get('mRN')),attack1=bool(monstats2.get(r.get('MonStatsEx'),{}).get('mA1')),attackable=bool(monstats2.get(r.get('MonStatsEx'),{}).get('isAtt')),npc=bool(r.get('npc')),interact=bool(r.get('interact')),velocity=r.get('Velocity',0),runVelocity=r.get('Run',0),size=monstats2.get(r.get('MonStatsEx'),{}).get('SizeX',0),ai=r.get('AI',''))
        for r in monsters.values() if r.get('Id')]
    armor={r.get('code'):r for r in json.loads((SOURCE/'json/armor.json').read_text()).values() if r.get('code')}
    blocking_rules=[]
    for r in monsters.values():
        if not r.get('Id'): continue
        model=monstats2.get(r.get('MonStatsEx'),{})
        choices=str(model.get('SHv','')).strip('"').split(',') if model.get('SHv') else []
        blocking_rules.append(dict(source=r['Id'],normal=r.get('ToBlock',0),nightmare=r.get('ToBlock(N)',0),hell=r.get('ToBlock(H)',0),
            innate=r.get('ShieldBlockOverride',0)==1,blockMode=bool(model.get('mBL')),
            shieldCodes=choices,shieldItems=[armor.get(code,{}).get('type')=='shie' for code in choices]))
    freeze_rules=[dict(source=r['Id'],primeEvil=bool(r.get('primeevil')),normal=r.get('coldeffect',0),nightmare=r.get('coldeffect(N)',0),hell=r.get('coldeffect(H)',0)) for r in monsters.values() if r.get('Id')]
    knockback_rules=[dict(source=r['Id'],knockback=bool(monstats2.get(r.get('MonStatsEx'),{}).get('mKB')),getHit=bool(monstats2.get(r.get('MonStatsEx'),{}).get('mGH')),distance=10 if r.get('BaseId',r['Id'])=='sandleaper1' else 5,
        small=bool(monstats2[r['MonStatsEx']].get('small')) if r.get('MonStatsEx') in monstats2 else None,
        large=bool(monstats2[r['MonStatsEx']].get('large')) if r.get('MonStatsEx') in monstats2 else None)
        for r in monsters.values() if r.get('Id')]
    item_types=[dict(code=r['Code'],parents=[r[k] for k in ('Equiv1','Equiv2') if r.get(k)]) for r in json.loads((SOURCE/'json/itemtypes.json').read_text()).values() if r.get('Code')]
    attack_source=next(r for r in json.loads((SOURCE/'json/skills.json').read_text()).values() if r.get('skill')=='Attack')
    attack=dict(sourceId=0,sourceKey='Attack',ability=1,sourceFields=attack_source,sourceParameters={})
    out = {ROOT/'data/Sanctuary/Balance/common-melee-skills.json':json.dumps([attack],indent=2)+'\n',
           ROOT/'data/Sanctuary/Balance/tainted-fireball-hit.json':json.dumps([dict(
               Skill=source_skills['422']['*Id'],Missile=missiles['taintedfireball']['*ID'],
               HitFunction=missiles['taintedfireball']['pSrvHitFunc'],ResultFlags=missiles['taintedfireball']['ResultFlags'],
               HitFlags=missiles['taintedfireball']['HitFlags'],Radius=source_skills['422']['Param1'])],indent=2)+'\n',
           ROOT/'data/Sanctuary/Balance/tainted-fireball-flight.json':json.dumps([{
               k:missiles['taintedfireball'].get(k,0) for k in (
                   'pSrvDoFunc','pSrvHitFunc','pSrvDmgFunc','Vel','VelLev','MaxVel','Accel','Range','InitSteps','Activate',
                   'Size','CollideType','CollideKill','LastCollide','CanSlow','Collision','ToHit','SrcDamage','Pierce','NextHit',
                   'ReturnFire','GetHit','ResultFlags','HitFlags')}],indent=2)+'\n',
           ROOT/'data/Sanctuary/Balance/source-block-rules.json':json.dumps(blocking_rules,indent=2)+'\n',
           ROOT/'data/Sanctuary/Balance/class-item-types.json':json.dumps(item_types,indent=2)+'\n',
           ROOT/'data/Sanctuary/Balance/knockback-source-rules.json':json.dumps(knockback_rules,indent=2)+'\n',
           ROOT/'data/Sanctuary/Balance/holy-freeze-source-rules.json':json.dumps(freeze_rules,indent=2)+'\n',
           ROOT/'data/Sanctuary/Balance/corpse-source-rules.json':json.dumps(corpse_rules,indent=2)+'\n',
           ROOT/'data/Sanctuary/Balance/conversion-source-rules.json':json.dumps(conversion_rules,indent=2)+'\n',
           ROOT/'data/Sanctuary/Balance/hydra-animation.json':json.dumps([hydra_animation],indent=2)+'\n',
           ROOT/'data/Sanctuary/Balance/classes.json':json.dumps(class_stats,indent=2)+'\n',
           ROOT/'data/Sanctuary/talents.json':json.dumps(talents,indent=2)+'\n',
           ROOT/'data/Sanctuary/Balance/class-skills.json':json.dumps(rows,indent=2)+'\n',
           ROOT/'data/Sanctuary/Balance/sentry-npc-skills.json':json.dumps(sentry_npc,indent=2)+'\n',
           ROOT/'data/Sanctuary/Balance/druid-npc-skills.json':json.dumps(druid_npc,indent=2)+'\n',
           ROOT/'data/Sanctuary/Balance/warlock-npc-skills.json':json.dumps(warlock_npc,indent=2)+'\n',
           ROOT/'data/Sanctuary/Balance/druid-spirit-auras.json':json.dumps(spirit_auras,indent=2)+'\n',
           ROOT/'data/Sanctuary/Balance/class-states.json':json.dumps([dict(state=r['state'],curse=bool(r.get('curse')),curable=bool(r.get('curable'))) for r in json.loads((SOURCE/'json/states.json').read_text()).values() if r.get('state')],indent=2)+'\n',
           ROOT/'data/Sanctuary/Balance/skill-effects.json':json.dumps(effects,indent=2)+'\n',
           ROOT/'data/Sanctuary/Balance/necromancer-summons.json':json.dumps(summons,indent=2)+'\n',
           ROOT/'shared/ClassAbilities.cs':generated_protocol(rows)}
    return out

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');args=parser.parse_args()
    for path,text in products().items():
        if args.check:
            if not path.is_file() or path.read_text()!=text: raise SystemExit(f'Stale class skill output: {path}')
        else: path.write_text(text)
    print('Eight classes, 240 source skills, stable ability IDs and asset requirements verified.')

if __name__=='__main__':main()
