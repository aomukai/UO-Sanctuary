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
    for r in sorted(rows,key=lambda r:r['ability']):
        name = ''.join(w[0].upper()+w[1:] for w in re.findall(r'[A-Za-z0-9]+',r['name']))
        if r['talentId'] == 'necromancer.raiseskeleton': name = 'RaiseSkeleton'
        if r['talentId'] == 'barbarian.swordmastery': name = 'SwordMastery'  # Saved/wire identity stays stable.
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
    public static bool AllyTarget(CombatAbility value) => value is CombatAbility.Enchant or CombatAbility.HolyBolt;
    public static bool CorpseTarget(CombatAbility value) => value is CombatAbility.RaiseSkeleton or CombatAbility.RaiseSkeletalMage or CombatAbility.CorpseExplosion or CombatAbility.PoisonExplosion or CombatAbility.Revive or CombatAbility.FindItem;
    public static bool SelfTarget(CombatAbility value) => value is CombatAbility.BoneArmor or CombatAbility.PoisonNova or CombatAbility.Nova or CombatAbility.FrostNova or CombatAbility.StaticField
        or CombatAbility.FrozenArmor or CombatAbility.ShiverArmor or CombatAbility.ChillingArmor or CombatAbility.EnergyShield or CombatAbility.ThunderStorm or CombatAbility.Blaze or CombatAbility.Might or CombatAbility.Defiance or CombatAbility.BlessedAim or CombatAbility.Prayer or CombatAbility.Cleansing or CombatAbility.Meditation or CombatAbility.ResistFire or CombatAbility.ResistCold or CombatAbility.ResistLightning or CombatAbility.Salvation or CombatAbility.Vigor or CombatAbility.Redemption or CombatAbility.Concentration or CombatAbility.Fanaticism or CombatAbility.HolyFire or CombatAbility.HolyShock or CombatAbility.HolyFreeze or CombatAbility.Sanctuary or CombatAbility.BlessedHammer or CombatAbility.Thorns or CombatAbility.Conviction or CombatAbility.HolyShield or CombatAbility.Shout or CombatAbility.BattleOrders or CombatAbility.BattleCommand or CombatAbility.Howl or CombatAbility.BattleCry or CombatAbility.WarCry or CombatAbility.InnerSight or CombatAbility.BurstOfSpeed or CombatAbility.Fade or CombatAbility.Venom or CombatAbility.BladeShield or CombatAbility.CycloneArmor or CombatAbility.Hurricane or CombatAbility.Armageddon or CombatAbility.Werewolf or CombatAbility.Werebear;
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
    monsters = json.loads((SOURCE / 'json/monstats.json').read_text())
    missiles = json.loads((SOURCE / 'json/missiles.json').read_text())
    levels = json.loads((SOURCE / 'json/monlvl.json').read_text())
    summons = [{'key':r['Id'],'fields':{k:v for k,v in r.items() if isinstance(v,int)}} for r in monsters.values()
               if r.get('Id','').lower() in ['druidbear','fenris','spiritofbarbs','heartofwolverine','oaksage','spiritwolf','plaguepoppy','cycleoflife','vinecreature','druidhawk','necroskeleton','necromage','claygolem','bloodgolem','irongolem','firegolem','bonewall','hydra1','hydra2','hydra3','dopplezon','bladecreeper','chargeboltsentry','wakeofdestruction','lightningsentry','infernosentry','deathsentry']]
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
                if r.get('Missile') in ['miasmabolt','miasmaboltcloud','miasmaboltcloudmaker','miasmaboltdot','arcticblast1','recycler delay','vine recycler delay','shockwave','rabiesplague','rabiescontagion','plague vines','plague vines trail','armageddoncontrol','armageddonfire','tornado','volcano','volcano debris 2','volcano small fire','volcano overlay fire','volcano explosion','twister','erruption center','erruption crack 1','erruption crack 2','erruption smoke 1','erruption smoke 2','moltenboulderemerge','moltenboulder','moltenboulderfirepath','firestormmaker','firestorm','inferno sentry 1','inferno sentry 2','sentrylightningbolt','sentrylightningbolt2','wake of destruction maker','wake of destruction','royalstrikemeteorcenter','royalstrikemeteor','royalstrikemeteortail','royalstrikemeteorexplode','royalstrikemeteorfire','royalstrikechainlightning','royalstrikechaosice','bladesoficecubes','clawsofthundernova','clawsofthunderbolt','fistsoffirefirewall','bladefragment1','bladefragment2','bladefragment3','bomb in air','bomb on ground','bomb explosion','shock field in air','shock field on ground','lightningfury','furylightning','plaguejavelin','plaguejavcloud','lightningjavelin','poisonjav','poisonjavcloud','necromage1','necromage2','necromage3','necromage4','poisonexplosioncloud','bonewallmaker','chillingarmorbolt','firebolt','arrow','magicarrow','firearrow','coldarrow','icearrow','freezingarrow','freezingarrowexp3','immolationarrow','immolationfire','strafearrow','strafebolt','guidedarrow','explodingarrow','explodingarrowexp2','multipleshotarrow','multipleshotbolt','thunderstorm1','lightningbolt','chargedbolt','sentrychargedbolt','chargedstrikebolt','blaze','firewall','firewallmaker','meteorcenter','meteorfire','blizzardcenter','blizzard1','frozenorb','frozenorbbolt','frozenorbnova','hydra','infernoflame1','chainlightning','holybolt','fistoftheheavensdelay','fistoftheheavensbolt','blessedhammer','shout','battleorders','battlecommand','howl','battlecry','warcry']]
    summons += [{'key':'level-'+str(r['Level']),'fields':r} for r in levels.values() if r.get('Level',0) in range(1,100)]
    icons = [{k:v for k,v in asset.items() if k not in ['effects','sourceEffects','visualEffect','soundEffect']} | {'placeholder':'Text label; final icon pending'} for asset in assets]
    staged_effects = {
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
        'assassin.wakeofinferno':'Prepared stationary body0x9C/hue0x489 and0x36D4 hue0x489 fire markers are identified placeholders. Final E9 S1/sequence/S2/NU/DT actor, source523/524 Flamethrower/Flamethrower2 beam/trail/contact and sorceress_inferno audio remain pending. Separate talent/right/quick icons pending; learned cast/retirement remain gated.',
        'assassin.lightningsentry':'Prepared stationary body0x9C/hue0x482 and travelling0x3818 hue0x482 lightning markers are identified placeholders. Final LG S1/S2/NU/DT actor, source526 straight Size2 bolt/trail/contact and audio remain pending. Separate talent/right/quick icons pending; learned cast/retirement remain gated.',
        'assassin.dragontail':'Prepared fire-burst component uses native explosion 0x36BD, hue 0x489 as an identified placeholder. Learned KK kick/contact, charge release, knockback and assassin_kick_1 audio remain pending. Final fire burst needs its own effect; talent/left/right/quick icons remain in the separate inventory.',
        'assassin.phoenixstrike':'Native A1/0x238, charge0x375A, warning0x3709, impact0x36BD, flame0x398C, lightning0x3818 and frost0x374A are identified placeholders. Final Royal Strike charge tiers, meteor indicator/fall/explosion/fire, lightning paths, chaos-ice/trail/freeze and audio remain pending; talent/left/right/quick icons are separate',
        'assassin.bladesofice':'Shared native claw animation/sound, charge0x375A and cold area/freeze/stationary cube0x374A hue0x480 are identified placeholders. Final charge tiers, cold hand/contact, bladesoficeexplode/bladesoficecubes/bladesoficemelt and audio remain pending; talent/left/right/quick-hotkey icons are separately inventoried',
        'assassin.fistsoffire':'Staged claw sequence uses ordinary UO weapon pose/audio0x238 and0x375A hue0x489 charge markers. Ordinary-Attack fire release uses0x36BD explosion and shared0x398C ground flames as identified placeholders. Final progressive_fire charge1/2/3 orbiting art,fistsoffireexplode,groundFireSmall/fistsoffirefirewall,claw sequence/contact and skill audio remain pending. Separate talent/left/right/quick icons remain pending.',
        'assassin.cobrastrike':'Staged ordinary UO weapon pose/audio0x238,0x375A hue0x26 charge flash and shared0x376A life/mana recovery marker are identified placeholders. Final cobrastrike1 charge1/2/3 orbital overlays, life/mana return and assassin_chargeup_cobra_1 audio pending. Separate talent/left/right/quick icons remain pending.',
        'assassin.tigerstrike':'Staged ordinary UO weapon pose/audio0x238 and0x375A hue0x481 charge marker are identified placeholders. Final tigerstrike1 charge1/2/3 orbiting overlays, assassinfootimpact and assassin_chargeup_tiger_1 audio pending. Separate talent/left/right/quick icons remain pending.',
        'assassin.bladefury':'Prepared bladefragment1 travel uses shared UO0xF42 marker, explicitly a placeholder. Prepared server SQ23 timer has no final player sequence/channel rendering. Final SQ/channel/cast, Expansion bladefury bladefragment1/2/3 travel/impact and assassin_bladefury_throw_1/hit audio remain pending. Separate talent/left/right/quick icons pending; learned input remains gated.',
        'assassin.clawsofthunder':'Shared UO claw pose/audio0x238,charge flash0x375A hue0x481 and sampled travelling0x3818 hue0x482 markers are identified placeholders; all mechanical missiles remain active. Final orbital charges,claw/contact,nova/charged-bolt travel/impact and audio pending. Talent/left/right/quick icons remain separate.',
        'assassin.chargedboltsentry':'Prepared stationary body0x9C/hue0x482 and0x3818 hue0x482 travelling markers are identified placeholders. The saved mechanical S1 phase uses labelled cached-lg-s1-15-v1 timing; its visible animation remains pending. Unregistered free-cell do45 release uses0x23B as an identified assassin_summon audio placeholder; final S2 cast/audio remains pending. Final chargeboltsentry S1/S2/NU/DT actor, sentrychargedbolt travel/impact and audio pending. Separate talent/right/quick icons pending; learned input remains gated.',
        'assassin.shockweb':'Staged throw animation9/audio0x23B,0x36E4 caltrop lobs and0x374A hue0x481 electric fields. Final caltrops/ShockField animations and assassin_staticfield sound pending. Separate talent/left/right/quick icons pending.',
        'assassin.fireblast':'Staged UO throw animation9,0x36E4 travel/landing,0x36BD explosion and0x23B sound. Final FireTraumaMissile/FireTraumaGround/FireTraumaXplosion and throw/impact audio pending. Separate talent/left/right/quick icons pending.',
        'assassin.mindblast':'Staged UO0x373A center,0x374A victim and0x1E5 sound; final mindblast center/hit, fist_will_cast overlay and assassin_psychichammer audio pending. Shared stun/knockback/conversion presentation remains placeholder. Separate talent/right-hotkey icons pending.',
        'assassin.weaponblock':'Shared native block animation20 is a placeholder; final dual-claw block animation/audio and talent icon pending. No active hotkey.',
        'druid.firestorm':'Firestorm emitters and burning fields use identified UO0x398C hue0x489 markers; SC uses the shared native pose and0x1E5 cast-audio placeholder. Final druid_fire_cast_1 overlay, one direct and two winding expansion/FirestormEmitter trails, groundFireSmall burning patches, contact/expiry presentation and druid_firecast_b/object_fire_loop_house audio remain pending. Separate talent/left/right/quick icons remain pending.',
        'druid.cyclonearmor':'Identified UO0x375A hue0x481 cast marker and0x1E5 audio placeholder. Final cyclonearmor1front/2front/3front shield overlays, cast/depletion presentation and druid cast audio remain pending. Separate talent/right/quick-hotkey icons remain pending.',
        'assassin.deathsentry':'Prepared automatic actor uses identified UO0x9C body/hue0x482, travelling bolt0x3818 and corpse explosion0x36BD placeholders. Final DeathSentry emergence/S2/SQ/neutral/retirement, sentrylightningbolt2, corpse blast/contact and assassin trap audio remain pending. Talent/right-hotkey icons remain separate. Learned placement/payment and final retirement acceptance are not cleared by these placeholders.',
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
        'amazon.slowmissiles':'Cast/target presentation pending. Slowed native encounter bolt positions use identified UO0xF42/0x36D4/0x3818 markers at logical travel positions; final cast_slowmissiles, innersight target overlay and amazon_handofathena audio pending. Talent/right-hotkey icons remain separate',
        'amazon.evade':'Staged native0x1A4 sound only, with no S1/movement/recovery lock or visual marker. Final block_arrow_1 / amazon_dodge_1 audio pending; source row S1 alone does not establish a bitmap effect. No active hotkey',
        'amazon.avoid':'Staged UO0x375A hue0x481 successful-avoidance marker; final interruptible S1 lean-back animation and amazon_dodge_1 sound pending; passive has no active hotkey',
        'amazon.dodge':'Staged UO0x375A hue0x481 successful-avoidance marker; final interruptible S1 lean-back animation and amazon_dodge_1 sound pending; passive has no active hotkey',
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
    assert set(staged_effects).issubset({a['talentId'] for a in assets}), 'Unknown skill asset entry'
    effects = [{k:v for k,v in asset.items() if k not in ['talentIcon','hotkeyIcon']} | {'placeholder':staged_effects.get(asset['talentId'],
        'Existing UO art; final effect pending' if asset['effects']!='not-applicable' else 'None required')} for asset in assets]
    class_stats = [r for r in json.loads((ROOT/'data/Sanctuary/Balance/classes.json').read_text()) if r['name'] != 'Warlock'] + [warlock_stats()]
    hydra_animation = json.loads((ROOT/'tools/reference/hydra-engine-evidence.json').read_text())['animation']
    monstats2=json.loads((SOURCE/'json/monstats2.json').read_text())
    corpse_rules=[dict(source=r['Id'],selectable=bool(monstats2.get(r.get('MonStatsEx'),{}).get('corpseSel')),noAura=bool(r.get('noAura'))) for r in monsters.values() if r.get('Id')]
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
           ROOT/'data/Sanctuary/Balance/druid-spirit-auras.json':json.dumps(spirit_auras,indent=2)+'\n',
           ROOT/'data/Sanctuary/Balance/class-states.json':json.dumps([dict(state=r['state'],curse=bool(r.get('curse')),curable=bool(r.get('curable'))) for r in json.loads((SOURCE/'json/states.json').read_text()).values() if r.get('state')],indent=2)+'\n',
           ROOT/'data/Sanctuary/Balance/skill-icons.json':json.dumps(icons,indent=2)+'\n',
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
