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
                    sourceParameters={str(i):row.get('Param'+str(i),0) for i in range(1,13)}))
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
        if mechanic['talentId'] in {'amazon.dodge','amazon.avoid','amazon.evade'}:
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
        or CombatAbility.FrozenArmor or CombatAbility.ShiverArmor or CombatAbility.ChillingArmor or CombatAbility.EnergyShield or CombatAbility.ThunderStorm or CombatAbility.Blaze or CombatAbility.Might or CombatAbility.Defiance or CombatAbility.BlessedAim or CombatAbility.Prayer or CombatAbility.Cleansing or CombatAbility.Meditation or CombatAbility.ResistFire or CombatAbility.ResistCold or CombatAbility.ResistLightning or CombatAbility.Salvation or CombatAbility.Vigor or CombatAbility.Redemption or CombatAbility.Concentration or CombatAbility.Fanaticism or CombatAbility.HolyFire or CombatAbility.HolyShock or CombatAbility.HolyFreeze or CombatAbility.Sanctuary or CombatAbility.BlessedHammer or CombatAbility.Thorns or CombatAbility.Conviction or CombatAbility.HolyShield or CombatAbility.Shout or CombatAbility.BattleOrders or CombatAbility.BattleCommand or CombatAbility.Howl or CombatAbility.BattleCry or CombatAbility.WarCry or CombatAbility.InnerSight;
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
    monsters = json.loads((SOURCE / 'json/monstats.json').read_text())
    missiles = json.loads((SOURCE / 'json/missiles.json').read_text())
    levels = json.loads((SOURCE / 'json/monlvl.json').read_text())
    summons = [{'key':r['Id'],'fields':{k:v for k,v in r.items() if isinstance(v,int)}} for r in monsters.values()
               if r.get('Id','').lower() in ['necroskeleton','necromage','claygolem','bloodgolem','irongolem','firegolem','bonewall','hydra1','hydra2','hydra3','dopplezon']]
    summons += [{'key':r['Missile'],'fields':{k:v for k,v in r.items() if isinstance(v,(int,str)) and not k.startswith('*') and k not in ['lineNumber','Missile']}} for r in missiles.values()
                if r.get('Missile') in ['necromage1','necromage2','necromage3','necromage4','poisonexplosioncloud','bonewallmaker','chillingarmorbolt','firebolt','arrow','magicarrow','firearrow','coldarrow','multipleshotarrow','multipleshotbolt','thunderstorm1','lightningbolt','chargedbolt','chargedstrikebolt','blaze','firewall','firewallmaker','meteorcenter','meteorfire','blizzardcenter','blizzard1','frozenorb','frozenorbbolt','frozenorbnova','hydra','infernoflame1','chainlightning','holybolt','fistoftheheavensdelay','fistoftheheavensbolt','blessedhammer','shout','battleorders','battlecommand','howl','battlecry','warcry']]
    summons += [{'key':'level-'+str(r['Level']),'fields':r} for r in levels.values() if r.get('Level',0) in range(1,100)]
    icons = [{k:v for k,v in asset.items() if k not in ['effects','sourceEffects','visualEffect','soundEffect']} | {'placeholder':'Text label; final icon pending'} for asset in assets]
    staged_effects = {
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
    knockback_rules=[dict(source=r['Id'],knockback=bool(monstats2.get(r.get('MonStatsEx'),{}).get('mKB')),getHit=bool(monstats2.get(r.get('MonStatsEx'),{}).get('mGH')),distance=10 if r.get('BaseId',r['Id'])=='sandleaper1' else 5) for r in monsters.values() if r.get('Id')]
    item_types=[dict(code=r['Code'],parents=[r[k] for k in ('Equiv1','Equiv2') if r.get(k)]) for r in json.loads((SOURCE/'json/itemtypes.json').read_text()).values() if r.get('Code')]
    out = {ROOT/'data/Sanctuary/Balance/source-block-rules.json':json.dumps(blocking_rules,indent=2)+'\n',
           ROOT/'data/Sanctuary/Balance/class-item-types.json':json.dumps(item_types,indent=2)+'\n',
           ROOT/'data/Sanctuary/Balance/knockback-source-rules.json':json.dumps(knockback_rules,indent=2)+'\n',
           ROOT/'data/Sanctuary/Balance/holy-freeze-source-rules.json':json.dumps(freeze_rules,indent=2)+'\n',
           ROOT/'data/Sanctuary/Balance/corpse-source-rules.json':json.dumps(corpse_rules,indent=2)+'\n',
           ROOT/'data/Sanctuary/Balance/conversion-source-rules.json':json.dumps(conversion_rules,indent=2)+'\n',
           ROOT/'data/Sanctuary/Balance/hydra-animation.json':json.dumps([hydra_animation],indent=2)+'\n',
           ROOT/'data/Sanctuary/Balance/classes.json':json.dumps(class_stats,indent=2)+'\n',
           ROOT/'data/Sanctuary/talents.json':json.dumps(talents,indent=2)+'\n',
           ROOT/'data/Sanctuary/Balance/class-skills.json':json.dumps(rows,indent=2)+'\n',
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
