#!/usr/bin/env python3
"""Audit usable production/gathering routes through every trained tenth to120."""
import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BALANCE = ROOT / 'data/Sanctuary/Balance'
SKILLS = ['Alchemy', 'Blacksmith', 'Fletching', 'Carpentry', 'Cartography', 'Cooking', 'Imbuing', 'Inscribe', 'Tailoring', 'Tinkering']
GATHERING = ['Fishing', 'Lumberjacking', 'Mining']
DISPLAY = {'Blacksmith': 'Blacksmithing', 'Fletching': 'Bowcraft/Fletching', 'Inscribe': 'Inscription'}
DETAILS = {
    'Alchemy': ['Mortar/pestle; Alchemy workshop', 'Town reagents/bottles; gathers and chips for advanced recipes', 'Belt and manual-keg consumables', 'crafting.md#alchemy--j01j05'],
    'Blacksmith': ['Smithing hammer; public forge/anvil', 'Mined/smelted metal and mapped equipment ingredients', 'Wearable weapons/armour; sockets and salvage', 't08-recipe-book.md'],
    'Fletching': ["Fletcher tools; Bowcraft workshop", 'Harvested boards and mapped equipment ingredients', 'Bows, crossbows, wooden weapons/shields', 't08-recipe-book.md'],
    'Carpentry': ['Carpentry tool; Carpentry workshop', 'Boards, cloth, metal; crafted keg components', 'Furniture, household/storage deeds and manual kegs', 't31-carpentry.md'],
    'Cartography': ['Mapmaker pen; Cartography workshop', 'Town Blank Maps and crafted/vendor pens', 'Activated surveys, purchase discounts and dungeon/search effects', 't32-map-surveys.md'],
    'Cooking': ['Skillet; public kitchen', 'Hunted/fished/gathered food, inn supplies and farm ingredients', 'Meals with one timed food effect', 't30-cooking.md'],
    'Imbuing': ['Imbuing interface; Imbuing workshop', 'White equipment, salvaged essence and gold', 'Equipped Magic/Rare items', 'crafting.md#imbuing--e01e05'],
    'Inscribe': ['Scribe pen; Inscription workshop', 'Town scrolls/reagents, leather, moulds and Runebark', 'Identify/Portal scrolls and charged tomes; mould/rune outputs', 't34-inscription.md'],
    'Tailoring': ['Sewing kit; Tailoring workshop', 'Hunted leather/bone; bought cloth or wool/yarn/loom', 'Cloth/leather/studded/bone equipment', 't08-recipe-book.md'],
    'Tinkering': ['Tinker tools; Tinkering workshop', 'Mined ingots/gems, Runebark and other mapped components', 'Tools, keys, white jewellery, keg parts and gem upgrades', 'crafting.md#tinkering-sockets--f01f08'],
}


def build():
    sources = {}
    routes = {skill: [] for skill in SKILLS}

    def read(name):
        p = BALANCE / (name + '.json')
        sources[str(p.relative_to(ROOT))] = hashlib.sha256(p.read_bytes()).hexdigest()
        return json.loads(p.read_text())

    def add(skill, name, low, high, source, rare=False):
        assert low < high and 0 <= low <= 120
        routes[skill].append({'name': name, 'minimum': low, 'maximum': high, 'source': source, 'rareUnlock': rare})

    for r in read('equipment-recipes'):
        add(r['skill'], r['name'] + ' (' + r['tier'] + ')', r['requiredSkill'], r['maxSkill'], 'equipment-recipes', r['requiredSkill'] == 120)
    for name, skill in [('carpentry', 'Carpentry'), ('cooking-meals', 'Cooking'), ('inscription-utilities', 'Inscribe')]:
        for r in read(name):
            low = r.get('skill', r.get('Skill'))
            add(skill, r.get('name', r.get('Name')), low, low + 50, name, name == 'cooking-meals' and r['tier'] == 3)
    for r in read('alchemy-restoratives'):
        add('Alchemy', r['kind'] + '/' + str(r['tier']), r['skill'], r['skill'] + 50, 'alchemy-restoratives', r['skill'] == 120)
    for r in read('map-surveys'):
        add('Cartography', r['name'], r['skill'], r['maximum'], 'map-surveys')
    for r in read('rune-recipes'):
        add('Inscribe', 'Tier' + str(r['tier']) + ' declared rune', r['requiredSkill'], r['maximumSkill'], 'rune-recipes')
    for r in read('jewellery-grades'):
        add('Tinkering', r['name'] + ' white jewellery', r['skill'], r['skill'] + 50, 'jewellery-grades')
        add('Imbuing', r['name'] + ' jewellery imbuing', r['skill'], r['skill'] + 50, 'jewellery-grades')
    for r in read('imbuing-tiers'):
        add('Imbuing', r['tier'] + ' equipment imbuing', r['skill'], r['skill'] + 50, 'imbuing-tiers', r['skill'] == 120)
    for r in read('treasure-hunts')[0]['Tiers']:
        add('Tinkering', r['KeyName'] + ' skeleton key', r['KeySkill'], r['KeySkill'] + 50, 'treasure-hunts', r['KeySkill'] == 120)
    missing = []
    for skill in SKILLS:
        for tenth in range(700, 1200):
            value = tenth / 10
            if not any(r['minimum'] <= value < r['maximum'] for r in routes[skill]):
                missing.append([skill, value])
    assert not missing, f'Missing production gain paths: {missing}'
    for source in ['src/Sanctuary.Content/ProfessionPractice.cs', 'src/Sanctuary.Content/ProfessionProgression.cs',
                   'src/Sanctuary.Content/EquipmentCrafting.cs', 'src/Sanctuary.Content/ProfessionPowerScrolls.cs']:
        sources[source] = hashlib.sha256((ROOT / source).read_bytes()).hexdigest()
    return {'schemaVersion': 1, 'skills': SKILLS + GATHERING, 'training': [0, 30], 'craftPractice': [30, 70],
            'caps': [100, 105, 110, 115, 120], 'productionTenthChecks': 5000, 'routes': routes,
            'productionDetails': DETAILS, 'inputHashes': sources}


def render(data):
    lines = ['# Profession progression: 0–120', '',
             'T47 covers all ten crafts and all three gathering skills. Training does not grant120 automatically: '
             'the existing ordered Power Scroll caps100→105→110→115→120 remain. Skill locks and independent saved progress remain enforced.', '',
             'All thirteen skills have paid NPC training0–30. The ten crafts also have optional resource-consuming no-output practice30–70; '
             'production can be used instead where a usable recipe is available. Gathering uses real extraction throughout, without practice.', '',
             'Every trained tenth70.0–119.9 is covered by an implemented production recipe (5,000 coverage checks). '
             'A range is a native gain window, not guaranteed skill per attempt. Costs, failures, active craft, public stations and learned recipe requirements still apply. '
             'The native fixture separately checks production gain hooks, Cartography boundaries, gathering above100 and save/reload.', '',
             '| Craft | Implemented production paths after practice | Highest usable gain window |', '| --- | --- | --- |']
    labels = {'Alchemy': 'Restorative mixtures, combat potions, fortification powder', 'Blacksmith': 'Normal/Exceptional/Elite metal equipment',
              'Fletching': 'Normal/Exceptional/Elite wooden weapons and shields', 'Tailoring': 'Cloth/leather/bone equipment and belts/tabards',
              'Carpentry': 'Furniture, storage, stations and potion-keg components', 'Cartography': 'Consumable city/world/dungeon surveys',
              'Cooking': '18 usable meals, including learned tier3 recipes', 'Inscribe': 'Identify/portal scrolls and tomes; declared runes',
              'Imbuing': 'White jewellery grades and equipment transformations', 'Tinkering': 'Craft/repair tools, white jewellery, keg components, skeleton keys and gem upgrades'}
    for skill, rows in data['routes'].items():
        r = max((r for r in rows if r['minimum'] < 120), key=lambda r: r['maximum'])
        lines.append(f"| {DISPLAY.get(skill, skill)} | {labels[skill]} | {r['name']}: {r['minimum']:g}–{r['maximum']:g} (cap120) |")
    lines += ['', '## Tools, supplies and consumers', '',
              '| Profession | Production tool / public facility | Input acquisition | Useful consumers and existing perks |',
              '| --- | --- | --- | --- |']
    for skill in SKILLS:
        tool, supply, consumer, link = data['productionDetails'][skill]
        lines.append(f'| {DISPLAY.get(skill, skill)} | {tool} | {supply} | [{consumer}]({link}) |')
    lines += ['', 'At 100, all ten crafts retain recognition/wardrobe eligibility and the Grandmaster title; gathering earns the title only. '
              'At 120, all thirteen retain the Legendary title and their cap. Existing profession-specific100/120 effects and recipe gates '
              'remain in the linked specifications: this pass adds no mastery bonuses. Equipment production retains100+ durability/socket quality; '
              '120-gated equipment, rejuvenation, imbuing and key recipes retain their individual learning requirements.', '',
              '## Ten-point production examples', '',
              'At 0, 10, 20 use the already available paid trainer; at 30, 40, 50, 60 use optional resource-consuming practice or ordinary production. '
              'Practice deliberately produces no item. The table supplies real productive examples for every later ten-point band; '
              'the JSON includes all recipes and verifies every tenth 70.0–119.9, including overlapping alternatives.', '',
              '| Profession | Trained band starts at | Representative usable output | Learning gate |', '| --- | --- | --- | --- |']
    for skill in SKILLS:
        for start in range(70, 120, 10):
            choices = [r for r in data['routes'][skill] if r['minimum'] <= start < r['maximum']]
            row = min(choices, key=lambda r: (r['rareUnlock'], r['minimum'] == start, -r['minimum'], r['name']))
            lines.append(f"| {DISPLAY.get(skill, skill)} | {start} | {row['name']} | {'Learn from treasure' if row['rareUnlock'] else 'Ordinary recipe'} |")
    lines += ['', '## Gathering and sea content', '',
              '- Mining retains native ore veins, material gates and socket-gem byproducts. Lumberjacking retains native woods, byproducts and Runebark. '
              'When the extracted resource\'s native challenge is exhausted at100+, a successful extraction offers one additional native100–120 gain check. '
              'The additional check uses native gain rules with a 50% challenge; its return value does not affect yield. '
              'A still-challenging native vein does not get a second gain roll. Yield, success and byproduct probabilities are unchanged.',
              '- Fishing retains native fishing, deep-water mutations, nets, sea creatures and bottle/SOS salvage. '
              'The ordinary fish challenge ends at100, so successful catches now support the same100–120 gain check. '
              'Fishing maps continue into the custom land-treasure route; bottles/SOS remain the native sea route. '
              'No new sea loot table or encounter population is invented by this pass. Native pickaxes/ore veins, hatchets/trees and fishing poles/water '
              'remain the respective tools/locations; ores feed smithing, logs feed woodworking, fish feed food and sea catches retain their existing consumers.', '',
              '## Unlocks, supply and consumers', '',
              'Town trainers and public facilities cover all thirteen skills/ten crafts. Vendor tools and crafted Tinkering tools provide uses; '
              'ore/wood/leather come from harvesting/hunting, cloth from vendor or wool/yarn/loom, reagents/bottles/scrolls from town supply. '
              'Mining gems and dropped moulds feed Tinkering/Inscription. Meals, potions, maps, equipment, utilities, furniture and keys have real consumers.', '',
              'Power Scrolls are skill-specific boss drops with ordered permanent caps. Rare recipes are permanently learned from treasure rewards; '
              'tier3 meals and skill120 outputs retain their learning gates. Furniture/station components do not unlock private workshops. '
              'Final training speed, resource cost and economic sustainability are T48 work after complete classes and gameplay.', '',
              '## Verification and remaining scope', '',
              'Reproduce with `python3 tools/build_profession_coverage.py`; validate with `--check`. '
              'The companion is `data/Sanctuary/Balance/profession-coverage.json`. Existing native recipe/consumer suites exercise the actual transactions; '
              'coverage of a gain window alone does not certify final gameplay balance. Ten exotic equipment recipes remain excluded by the earlier user decision. '
              'New class-specific products and later world placements are separate work, not hidden completed recipes.', '']
    return '\n'.join(lines)


def main():
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--check', action='store_true'); args = parser.parse_args()
    data = build()
    for path, content in [(BALANCE / 'profession-coverage.json', json.dumps(data, indent=2) + '\n'), (ROOT / 'docs/t47-profession-coverage.md', render(data))]:
        if args.check:
            assert path.read_text() == content, f'Stale generated file: {path}'
        else:
            path.write_text(content)
    print('T47: 13 skills; all 5,000 crafting tenths70.0–119.9 covered; ordered caps preserved.')


if __name__ == '__main__':
    main()
