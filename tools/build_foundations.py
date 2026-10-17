#!/usr/bin/env python3
"""Build reviewable balance catalogs from local D2 1.13 tables and pinned UO source.

No network access or persistent search index is used. See docs/balance-foundations.md.
"""
import argparse
import csv
import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'data/Sanctuary/Balance'
D2_DATA_COMMIT = '45112569deb9384738ccafe5c24ebbb71f41c7c9'
TABLE_URL = f'https://raw.githubusercontent.com/fabd/diablo2/{D2_DATA_COMMIT}/code/d2_113_data/'


def integer(value):
    return int(value or 0)


def build(source):
    def table(name):
        return list(csv.DictReader((source / (name + '.txt')).open(encoding='utf-8-sig'), delimiter='\t'))

    def save(name, value):
        OUT.mkdir(parents=True, exist_ok=True)
        (OUT / (name + '.json')).write_text(json.dumps(value, indent=2, ensure_ascii=False) + '\n')

    names = ['CharStats', 'Experience', 'MonLvl', 'MonStats', 'MonStats2',
             'Weapons', 'Armor', 'ItemTypes', 'MagicPrefix', 'MagicSuffix', 'Properties']
    save('sources', {name: {'url': TABLE_URL + name + '.txt',
                          'sha256': hashlib.sha256((source / (name + '.txt')).read_bytes()).hexdigest()}
                     for name in names})
    chars = []
    for r in table('CharStats'):
        if r['class'] in ('Expansion', ''):
            continue
        chars.append(dict(name=r['class'], strength=integer(r['str']), dexterity=integer(r['dex']),
                          vitality=integer(r['vit']), energy=integer(r['int']),
                          life=integer(r['vit']) + integer(r['hpadd']), stamina=integer(r['stamina']),
                          mana=integer(r['int']), lifeLevel4=integer(r['LifePerLevel']),
                          staminaLevel4=integer(r['StaminaPerLevel']), manaLevel4=integer(r['ManaPerLevel']),
                          lifeVitality4=integer(r['LifePerVitality']), staminaVitality4=integer(r['StaminaPerVitality']),
                          manaEnergy4=integer(r['ManaPerMagic']), attackBonus=integer(r['ToHitFactor']),
                          statPerLevel=integer(r['StatPerLevel'])))
    save('classes', chars)
    exp = {integer(r['Level']): r for r in table('Experience') if r['Level'].isdigit()}
    save('experience', [dict(level=integer(r['Level']) + 1, threshold=integer(r['Amazon']),
                             ratio1024=integer(exp[integer(r['Level']) + 1]['ExpRatio']) if integer(r['Level']) < 98 else 0) for r in table('Experience')
                        if r['Level'].isdigit() and integer(r['Level']) < 99])

    types = [dict(code=r['Code'], parents=[r[k] for k in ['Equiv1', 'Equiv2'] if r[k]],
                  alwaysMagic=r['Magic'] == '1', rare=r['Rare'] == '1', characterClass=r['Class']) for r in table('ItemTypes') if r['Code']]
    save('item-types', types)
    bases = []
    for name in ['Weapons', 'Armor']:
        for r in table(name):
            if not r['code'] or r['code'] != r['normcode'] or r['spawnable'] != '1' or integer(r['quest']):
                continue
            bases.append(dict(code=r['code'], name=r['name'], type=r['type'], qualityLevel=integer(r['level']),
                              magicLevel=integer(r['magic lvl']), requiredLevel=integer(r['levelreq']),
                              strength=integer(r['reqstr']), dexterity=integer(r.get('reqdex')),
                              minDamage=integer(r.get('mindam')), maxDamage=integer(r.get('maxdam')),
                              twoHandMin=integer(r.get('2handmindam')), twoHandMax=integer(r.get('2handmaxdam')),
                              missileMin=integer(r.get('minmisdam')), missileMax=integer(r.get('maxmisdam')),
                              minDefense=integer(r.get('minac')), maxDefense=integer(r.get('maxac')),
                              speed=integer(r['speed']), block=integer(r.get('block')),
                              strengthBonus=integer(r.get('StrBonus')), dexterityBonus=integer(r.get('DexBonus')),
                              twoHanded=r.get('2handed') == '1', oneOrTwoHanded=r.get('1or2handed') == '1',
                              maxSockets=None, tier='Normal'))
    # Jewelry has no exceptional/elite base tiers.
    for code, name, typ in [('rin', 'Ring', 'ring'), ('amu', 'Amulet', 'amul')]:
        bases.append(dict(code=code, name=name, type=typ, qualityLevel=1, magicLevel=0, requiredLevel=0,
                          strength=0, dexterity=0, minDamage=0, maxDamage=0, twoHandMin=0, twoHandMax=0,
                          missileMin=0, missileMax=0, minDefense=0, maxDefense=0, speed=0, block=0,
                          strengthBonus=0, dexterityBonus=0, maxSockets=None, tier='Normal'))
    save('normal-bases', bases)
    affixes = []
    properties = {r['code']: r for r in table('Properties') if r['code']}
    def modifier(r, i):
        code = r[f'mod{i}code']
        funcs = [integer(properties[code]['func' + str(j)]) for j in range(1, 8)]
        mode = 'Proc' if 11 in funcs else 'Charges' if 19 in funcs else 'DamageRange' if 15 in funcs else \
               'Parameter' if funcs[0] == 17 else 'Numeric'
        return dict(code=code, parameter=r[f'mod{i}param'], min=integer(r[f'mod{i}min']), max=integer(r[f'mod{i}max']), mode=mode)
    def item_type(code):
        return 'staf' if code.strip() == 'staff' else code.strip()
    for tab, kind in [('MagicPrefix', 'Prefix'), ('MagicSuffix', 'Suffix')]:
        for index, r in enumerate(table(tab)):
            if r['spawnable'] != '1' or not r['Name'] or integer(r['frequency']) < 1:
                continue
            mods = [modifier(r, i) for i in range(1, 4) if r[f'mod{i}code']]
            affixes.append(dict(id=f'{tab}:{index}', name=r['Name'], kind=kind, rare=r['rare'] == '1',
                                level=integer(r['level']), maxLevel=integer(r['maxlevel']),
                                requiredLevel=integer(r['levelreq']), frequency=integer(r['frequency']),
                                group=integer(r['group']), classSpecific=r['classspecific'],
                                characterClass=r['class'], classRequiredLevel=integer(r['classlevelreq']),
                                include=[item_type(r[f'itype{i}']) for i in range(1, 8) if r[f'itype{i}']],
                                exclude=[item_type(v) for k, v in r.items() if k.startswith('etype') and v], modifiers=mods))
    save('affixes', affixes)
    write_affix_summary(types, affixes)

    # One players-1 progression; source bands do not create dungeon variants or resistances.
    levels = table('MonLvl')
    mons = {r['Id']: r for r in table('MonStats')}
    references = []
    for r in mons.values():
        if r['enabled'] != '1' or r['killable'] != '1' or integer(r['Exp']) <= 0 or any(r[k] == '1' for k in ['npc', 'boss', 'primeevil', 'SetBoss']):
            continue
        references.append(dict(id=r['Id'], family=r['BaseId'], animationCode=r['Code'],
                               levels=[integer(r['Level' + s]) for s in ['', '(N)', '(H)']],
                               # D2 uses lowercase minHP/maxHP for Normal only.
                               difficulties=[dict(minLife=integer(r['minHP' if not s else 'MinHP' + s]),
                                                  maxLife=integer(r['maxHP' if not s else 'MaxHP' + s]),
                                                  attackRating=integer(r['A1TH' + s]), defense=integer(r['AC' + s]),
                                                  minDamage=integer(r['A1MinD' + s]), maxDamage=integer(r['A1MaxD' + s]),
                                                  experience=integer(r['Exp' + s]),
                                                  resistances=[integer(r[k + s]) for k in ['ResDm', 'ResMa', 'ResFi', 'ResLi', 'ResCo', 'ResPo']])
                                             for s in ['', '(N)', '(H)']]))
    save('d2-monsters', references)
    roles = [('Tank', 'zombie1', 150, 125, 75, 640, 360, 600, 960, 1, 'Physical'),
             ('Melee', 'skeleton1', 100, 100, 100, 760, 440, 600, 320, 1, 'Physical'),
             ('Ranged', 'sk_archer1', 80, 75, 100, 800, 400, 600, 400, 10, 'Physical'),
             ('Caster', 'fallenshaman1', 70, 70, 100, 800, 400, 600, 400, 10, 'Fire')]
    generated = []
    def source_band(level):
        return ('Normal', '') if level <= 35 else ('Nightmare', '(N)') if level <= 65 else ('Hell', '(H)')
    for role, reference, hpct, dpct, dmgpct, attack, effect, delay, move, reach, element in roles:
        mon = mons[reference]
        previous = 0
        for level in range(1, 100):
            band_name, suffix = source_band(level)
            _, next_suffix = source_band(level + 1)
            row, nxt = levels[level], levels[level + 1]
            # HP is the strict ordering anchor; integer damage can plateau in D2.
            life = max(previous + 1, integer(row['L-HP' + suffix]) * hpct // 100)
            next_life = max(life + 2, integer(nxt['L-HP' + next_suffix]) * hpct // 100)
            life_max = life + min(next_life - life - 1, max(1, life * 5 // 100))
            previous = life_max
            def band(field, percent):
                low = max(1, integer(row['L-' + field + suffix]) * percent // 100)
                high_next = max(1, integer(nxt['L-' + field + next_suffix]) * percent // 100)
                return dict(min=low, max=low + max(0, min(low * 5 // 100, (high_next - low - 1) // 2)))
            # 50–150% per-hit range around the role's D2 level damage baseline.
            damage = max(1, integer(row['L-DM' + suffix]) * dmgpct // 100)
            generated.append(dict(archetype=role, sourceBand=band_name, level=level, reference=reference,
                                  life=dict(min=life, max=life_max), attackRating=band('TH', 101),
                                  defense=band('AC', dpct), minDamage=max(1, damage // 2),
                                  maxDamage=max(1, damage * 3 // 2), attackMs=attack, effectMs=effect,
                                  recoveryMs=delay, moveMs=move, range=reach, damageType=element,
                                  criticalChance=integer(mon['Crit']),
                                  experience=max(1, integer(row['L-XP' + suffix]) * integer(mon['Exp' + suffix]) // 100)))
    # Anchor the tested D2 species in the same catalog used for future archetype spawns.
    anchored = {(r['archetype'], r['level']): r for r in generated}
    anchored['Melee', 1]['life'] = dict(min=5, max=6)
    anchored['Melee', 2].update(life=dict(min=7, max=11), attackRating=dict(min=12, max=12),
                                defense=dict(min=10, max=10), minDamage=1, maxDamage=3, effectMs=480, experience=34)
    anchored['Tank', 1].update(life=dict(min=7, max=12), attackRating=dict(min=8, max=8),
                               defense=dict(min=5, max=5), minDamage=1, maxDamage=3, experience=33)
    save('monsters', generated)
    build_gear(save)


def build_gear(save):
    folder = ROOT / 'upstream/ModernUO/Projects/UOContent'
    classes = {}
    pattern = re.compile(r'\b(?P<mods>(?:(?:public|internal|private|protected|abstract|sealed|partial|static)\s+)*)class\s+(?P<name>\w+)\s*(?:\([^)]*\))?\s*:\s*(?P<base>[\w.]+)[^{]*\{')
    for path in sorted(folder.rglob('*.cs')):
        if 'obj' in path.parts or 'bin' in path.parts:
            continue
        source = path.read_text(encoding='utf-8-sig')
        matches = list(pattern.finditer(source))
        for i, m in enumerate(matches):
            # Bound each class's evidence; inherited fields are resolved through the graph.
            depth, end = 1, m.end()
            while depth and end < len(source):
                depth += (source[end] == '{') - (source[end] == '}')
                end += 1
            body = source[m.end():end - 1]
            classes[m['name']] = dict(base=m['base'].split('.')[-1], abstract='abstract' in m['mods'],
                                     path=str(path.relative_to(folder)), body=body)

    families = {'BaseAxe': ('Axes', 'axe'), 'BaseSword': ('Swords', 'swor'),
                'BaseKnife': ('Daggers', 'knif'), 'BasePoleArm': ('Polearms', 'pole'),
                'BaseSpear': ('Spears', 'spea'), 'BaseBashing': ('Maces', 'mace'),
                'BaseStaff': ('Staves', 'staf'), 'BaseRanged': ('Bows', 'bow'),
                'BaseThrown': ('Throwing', 'tkni'), 'BaseWand': ('Wands', 'wand')}
    explicit = {'Tekagi': ('Claws', 'h2h'), 'Kama': ('Axes', 'axe'), 'Sai': ('Daggers', 'knif'),
                'Scepter': ('Scepters', 'scep'), 'MagicWand': ('Wands', 'wand'),
                'FireworksWand': ('Wands', 'wand'), 'Boomerang': ('Throwing', 'taxe'),
                'Cyclone': ('Throwing', 'taxe'), 'SoulGlaive': ('Throwing', 'jave'),
                'Lance': ('Spears', 'spea'), 'Lajatang': ('Polearms', 'pole'),
                'Kryss': ('Daggers', 'knif'), 'Fists': ('Unarmed', None)}
    ancestors = {'BaseWeapon', 'BaseArmor', 'BaseShield', 'BaseClothing', 'BaseJewel', 'BaseQuiver', 'BaseTalisman', 'Spellbook', 'BaseEquipableLight'}
    gear = []
    audited = {r['Type']: r for r in json.loads((ROOT / 'tools/reference/uo-equipment-layers.json').read_text())}
    bases = {r['code']: r for r in json.loads((OUT / 'normal-bases.json').read_text())}
    weapon_bases = {
        'Dagger': 'dgr', 'SkinningKnife': 'dir', 'ButcherKnife': 'dir', 'Cleaver': 'bld', 'Kryss': 'kri',
        'Hatchet': 'hax', 'Axe': 'axe', 'BattleAxe': 'btx', 'DoubleAxe': '2ax', 'WarAxe': 'wax',
        'TwoHandedAxe': 'bax', 'LargeBattleAxe': 'gax', 'ExecutionersAxe': 'gix', 'Pickaxe': 'mpi',
        'Halberd': 'hal', 'Bardiche': 'bar', 'Scythe': 'scy', 'Lajatang': 'wsc',
        'Broadsword': 'bsd', 'Longsword': 'lsd', 'VikingSword': 'wsd', 'Katana': 'sbr',
        'Cutlass': 'flc', 'Scimitar': 'scm', 'NoDachi': '2hs', 'Daisho': '2hs', 'Bokuto': 'ssd',
        'RuneBlade': 'crs', 'BoneHarvester': 'flc', 'CrescentBlade': 'clm',
        'Mace': 'mac', 'WarMace': 'mst', 'WarHammer': 'whm', 'Maul': 'mau', 'HammerPick': 'whm', 'Club': 'clb',
        'BlackStaff': 'wst', 'QuarterStaff': 'bst', 'GnarledStaff': 'cst', 'ShepherdsCrook': 'sst',
        'Spear': 'spr', 'ShortSpear': 'brn', 'Pitchfork': 'tri', 'WarFork': 'tri', 'Pike': 'pik', 'Lance': 'pik',
        'Bow': 'lbw', 'CompositeBow': 'cbw', 'Yumi': 'lwb', 'ElvenCompositeLongbow': 'lwb', 'MagicalShortbow': 'sbw',
        'Crossbow': 'mxb', 'HeavyCrossbow': 'hxb', 'RepeatingCrossbow': 'rxb',
        'Tekagi': 'clw', 'Sai': 'kri', 'Kama': 'hax', 'Boomerang': 'tax', 'Cyclone': 'bal', 'SoulGlaive': 'glv',
        'Buckler': 'buc', 'WoodenShield': 'sml', 'WoodenKiteShield': 'kit', 'MetalShield': 'lrg',
        'MetalKiteShield': 'tow', 'HeaterShield': 'gts', 'BronzeShield': 'lrg',
    }
    defaults = {'Axes': 'axe', 'Swords': 'ssd', 'Daggers': 'dgr', 'Maces': 'mac', 'Polearms': 'bar',
                'Spears': 'spr', 'Bows': 'lbw', 'Crossbows': 'mxb', 'Throwing': 'tkf', 'Staves': 'sst',
                'Wands': 'wnd', 'Scepters': 'scp', 'Claws': 'ktr', 'Spellbooks': 'ob1', 'Shields': 'buc'}
    unmatched = []
    for name, item in sorted(classes.items()):
        if item['abstract']:
            continue
        chain, current = [], name
        while current in classes and current not in chain:
            chain.append(current)
            current = classes[current]['base']
        chain.append(current)
        if 'Item' not in chain and not set(chain) & ancestors:
            continue
        bodies = '\n'.join(classes[n]['body'] for n in chain if n in classes)
        equipment = bool(set(chain) & ancestors) or name == 'Beads'
        direct = re.search(r'Layer\s*(?:=|=>)\s*Layer\.(\w+)', item['body'])
        if not equipment and not direct:
            continue
        if direct and direct[1] in ('Invalid', 'Backpack', 'Bank', 'Hair', 'FacialHair', 'Mount'):
            continue
        category, tag, weight = None, None, 'None'
        if 'BaseWeapon' in chain:
            selected = next((explicit[n] for n in chain if n in explicit), None)
            if selected is None:
                selected = next((families[n] for n in chain if n in families), None)
            if selected:
                category, tag = selected
            animation = re.search(r'DefAnimation\s*=>\s*WeaponAnimation\.(\w+)', bodies)
            if category == 'Bows' and animation and animation[1] == 'ShootXBow':
                category, tag = 'Crossbows', 'xbow'
        elif 'BaseShield' in chain:
            category, tag = 'Shields', 'shie'
            weight = 'Light' if any(n in chain for n in ['Buckler', 'WoodenShield', 'GargishWoodenShield']) else \
                     'Heavy' if any(n in chain for n in ['HeaterShield', 'TowerShield', 'MetalKiteShield', 'GargishKiteShield']) else 'Medium'
        elif 'BaseArmor' in chain:
            category = 'Armor'
            material = re.search(r'MaterialType\s*=>\s*(?:ArmorMaterialType|AMT)\.(\w+)', bodies)
            weight = 'Light' if material and material[1] in ('Cloth', 'Leather', 'Spined', 'Horned', 'Barbed') else \
                     'Medium' if material and material[1] in ('Studded', 'Bone', 'Ringmail', 'Chainmail', 'Wood') else 'Heavy'
            slot = re.search(r'Layer\.(\w+)', item['body'])
            tag = 'helm' if any('Helm' in n or 'Cap' in n or 'Circlet' in n or 'Jingasa' in n or 'Kabuto' in n for n in chain) or '/Helmets/' in '/' + item['path'] or '/Glasses/' in '/' + item['path'] else \
                  'glov' if any('Glove' in n or 'Mitts' in n for n in chain) else \
                  'boot' if any('Suneate' in n for n in chain) else 'tors'
            if any('Circlet' in n for n in chain):
                tag = 'circ'
        elif 'BaseClothing' in chain:
            category, weight = 'Clothing', 'Light'
            tag = 'boot' if 'BaseShoes' in chain else 'helm' if 'BaseHat' in chain else \
                  'belt' if 'BaseWaist' in chain else 'tors'
        elif 'BaseJewel' in chain:
            category, tag = ('Rings', 'ring') if 'BaseRing' in chain else ('Jewelry', 'amul')
        elif name == 'Beads':
            category, tag = 'Jewelry', 'amul'
        elif 'Spellbook' in chain:
            category, tag = 'Spellbooks', 'orb'
        elif 'BaseQuiver' in chain:
            category = 'Quivers'
        elif 'BaseTalisman' in chain:
            category = 'Talismans'
        else:
            category = 'Tools' if any(n in chain for n in ['FishingPole', 'SmithHammer', 'SledgeHammer', 'RunicHammer', 'AncientSmithyHammer']) else \
                       'Lights' if any(n in chain for n in ['LightSource', 'DarkSource', 'BaseEquipableLight']) else \
                       'Timepieces' if any(n in chain for n in ['WristWatch', 'HolidayTimepiece']) else \
                       'Talismans' if 'BaseFormTalisman' in chain else 'Blowguns' if 'Fukiya' in chain else \
                       'QuestEquipment'
            if name == 'ThrowingDagger':
                category, tag = 'Throwing', 'tkni'
            elif name == 'GrandGrimoire':
                category, tag = 'Spellbooks', 'orb'
            elif name == 'ChylothShroud':
                category, tag, weight = 'Clothing', 'tors', 'Light'
        if category is None:
            unmatched.append(name)
        audit = next((audited[n] for n in chain if n in audited), None)
        if audit and category in ('Armor', 'Clothing'):
            tag = 'circ' if 'Circlet' in name else {'Helm': 'helm', 'Gloves': 'glov', 'Shoes': 'boot', 'Waist': 'belt'}.get(audit['Layer'], 'tors')
        layer = audit['Layer'] if audit else None
        if category in defaults and (category != 'Throwing' or 'BaseWeapon' in chain):
            game_slot = 'Shield' if category == 'Shields' else 'Weapon'
        elif category == 'Rings':
            game_slot = 'Ring'
        elif 'BaseNecklace' in chain or name == 'Beads':
            game_slot = 'Amulet'
        elif category in ('Armor', 'Clothing'):
            game_slot = {'Helm': 'Head', 'Gloves': 'Hands', 'Shoes': 'Feet', 'Waist': 'Belt'}.get(layer, 'Cosmetic')
            if category == 'Armor' and (layer in ('InnerTorso', 'MiddleTorso', 'OuterTorso') or
                                        not layer and any(x in name for x in ['Chest', 'Tunic', 'Bustier', 'PlateDo', 'LeatherDo', 'StuddedDo'])):
                game_slot = 'Body'
            if not layer and category == 'Armor':
                game_slot = {'helm': 'Head', 'circ': 'Head', 'glov': 'Hands'}.get(tag, game_slot)
        else:
            game_slot = 'Cosmetic'
        base_code = None
        if game_slot == 'Weapon' or game_slot == 'Shield':
            base_code = next((weapon_bases[n] for n in chain if n in weapon_bases), defaults[category])
        elif game_slot in ('Ring', 'Amulet'):
            base_code = 'rin' if game_slot == 'Ring' else 'amu'
        elif game_slot == 'Head':
            base_code = 'ci0' if tag == 'circ' else 'cap' if weight == 'Light' else 'bhm' if weight == 'Medium' else 'fhl'
        elif game_slot == 'Hands':
            base_code = {'Light': 'lgl', 'Medium': 'mgl', 'Heavy': 'hgl'}[weight]
        elif game_slot == 'Feet':
            base_code = {'Light': 'lbt', 'Medium': 'mbt', 'Heavy': 'hbt'}[weight]
        elif game_slot == 'Belt':
            base_code = 'lbl'
        elif game_slot == 'Body':
            material = re.search(r'MaterialType\s*=>\s*(?:ArmorMaterialType|AMT)\.(\w+)', bodies)
            base_code = {'Cloth': 'qui', 'Leather': 'lea', 'Studded': 'stu', 'Bone': 'rng', 'Ringmail': 'rng',
                         'Chainmail': 'chn', 'Plate': 'plt', 'Dragon': 'ful', 'Wood': 'stu', 'Stone': 'ful'}.get(
                             material[1] if material else '', {'Light': 'lea', 'Medium': 'stu', 'Heavy': 'plt'}[weight])
        if base_code is not None:
            tag = bases[base_code]['type']
        else:
            tag = None
        # Only plain source bases enter the normal pool. Named rewards/artifacts stay inventoried.
        path = item['path']
        plain = (path.startswith(('Items/Weapons/', 'Items/Armor/', 'Items/Clothing/', 'Items/Jewels/', 'Items/Shields/'))
                 and not any(x in path.lower() for x in ['artifact', '/wooden/', '/glasses/', '/ranger/'])
                 and not any(x in item['body'] for x in ['Attributes.', 'WeaponAttributes.', 'SkillBonuses.SetValues', 'ArmorAttributes.'])
                 and not re.search(r'\bSlayer2?\s*=', item['body'])
                 and not any(x in path for x in ['Fists.cs', 'FireworksWand.cs', 'ThrowingDagger.cs']))
        if any(n in classes and not classes[n]['abstract'] and n not in ancestors for n in chain[1:]):
            plain = False
        gear.append(dict(type=name, category=category, affixType=tag, weight=weight, tier='Normal', slot=game_slot,
                         baseCode=base_code, allowsAffixes=game_slot != 'Cosmetic',
                         normalDrop=plain, maxSockets=None, source=path,
                         excludedReason=None if plain else 'Named reward, special-purpose equipment, or non-base variant'))
    if unmatched:
        raise ValueError('Unclassified weapons: ' + ', '.join(unmatched))
    save('uo-equipment', gear)


def write_affix_summary(types, affixes):
    graph = {r['code']: r['parents'] for r in types}
    def is_type(code, parent):
        return code == parent or any(is_type(p, parent) for p in graph[code])
    def eligible(affix, code):
        return affix['level'] <= 99 and any(is_type(code, t) for t in affix['include']) \
            and not any(is_type(code, t) for t in affix['exclude'])
    columns = [('swor', 'Sword'), ('tors', 'Body armour'), ('shie', 'Shield'), ('glov', 'Gloves'), ('boot', 'Boots'), ('ring', 'Ring'), ('amul', 'Amulet')]
    rows = [('Life', ['hp']), ('Mana', ['mana']), ('Strength', ['str']), ('Dexterity', ['dex']),
            ('Enhanced damage %', ['dmg%']), ('Enhanced defence %', ['ac%']),
            ('Attack speed %', ['swing1', 'swing2', 'swing3']), ('Cast rate %', ['cast1', 'cast2', 'cast3']),
            ('Run/walk speed %', ['move1', 'move2', 'move3']), ('Hit recovery %', ['balance1', 'balance2', 'balance3']),
            ('All resistances %', ['res-all']), ('Fire resistance %', ['res-fire']),
            ('Life stolen %', ['lifesteal']), ('Mana stolen %', ['manasteal']), ('Magic find %', ['mag%'])]
    lines = ['# Affix pool examples', '',
             'Generated from the pinned D2 affix and item-type catalogs. Each cell is **magic ceiling / rare ceiling** for a single affix at some reachable affix level (1–99). A dash means that property has no eligible affix on that base type. This is neither an item total nor a promise that a low-level item can roll the ceiling. Required levels, exclusions, groups, and weights remain in the full catalog.', '',
             '| Single-affix stat | ' + ' | '.join(label for _, label in columns) + ' |',
             '| --- | ' + ' | '.join('---:' for _ in columns) + ' |']
    for label, codes in rows:
        cells = []
        for code, _ in columns:
            values = []
            for rare in [False, True]:
                candidates = [m['max'] for a in affixes if eligible(a, code) and (not rare or a['rare'])
                              for m in a['modifiers'] if m['code'] in codes and m['mode'] == 'Numeric']
                values.append(str(max(candidates)) if candidates else '—')
            cells.append(' / '.join(values))
        lines.append('| ' + label + ' | ' + ' | '.join(cells) + ' |')
    lines.extend(['', 'Belts, headgear, class-specific items, and other weapon families have their own pools; consult the source eligibility rules for those types.', '',
                  'These are full source-pool ceilings. Only implemented effects are enabled for live item creation, as described in [Balance foundations](balance-foundations.md#affixes-and-rarity); monster drops are disabled.\n\nExamples of level gates: gloves gain 10% IAS at affix level 20 (required character level 15), then 20% IAS at affix level 43 (required level 35). Leather Gloves have quality level 3, so the latter needs item level 44. Boots gain 10/20/30% run/walk at affix levels 12/22/37; magic-only 40% needs affix level 51 (required level 43). Leather Boots have quality level 3, so 40% needs item level 52. Boots never roll IAS. Item level, affix level, and required character level are separate gates.', ''])
    (ROOT / 'docs/affix-pools.md').write_text('\n'.join(lines))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-dir', type=Path, required=True)
    args = parser.parse_args()
    build(args.source_dir)
