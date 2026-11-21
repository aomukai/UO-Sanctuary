#!/usr/bin/env python3
"""Author T11 service sites from the pinned native Britannian regions/vendor spawns."""
import argparse
import hashlib
import subprocess
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DATA = ROOT / 'upstream/ModernUO/Distribution/Data'
OUT = ROOT / 'data/Sanctuary/Balance/town-markets.json'
TOWNS = ['Cove', 'Britain', 'Jhelom', 'Minoc', 'Ocllo', 'New Haven', 'Trinsic', 'Vesper', 'Yew',
         'Wind', "Serpent's Hold", 'Skara Brae', "Nujel'm", 'Moonglow', 'Magincia', "Buccaneer's Den", 'Delucia', 'Papua']
ROLES = dict(Smith=['Blacksmith','Weaponsmith','Armorer','BlacksmithGuildmaster'],
             Tailor=['Tailor','Weaver','TailorGuildmaster'], Bowyer=['Bowyer','Ranger','BowyerGuildmaster'],
             Carpenter=['Carpenter','CarpenterGuildmaster'], Alchemist=['Alchemist','Herbalist','AlchemistGuildmaster'],
             Cook=['Cook','Baker','InnKeeper'], Cartographer=['Mapmaker','MapmakerGuildmaster'],
             Scribe=['Scribe','ScribeGuildmaster','Mage'], Tinker=['Tinker','TinkerGuildmaster'],
             Imbuer=['Alchemist','Mage','Scribe'], Miner=['Miner','Blacksmith','BlacksmithGuildmaster'],
             Lumberjack=['Lumberjack','Carpenter','Bowyer'], Fisher=['Fisherman','FisherGuildmaster'],
             Jeweller=['Jeweler','Tinker','Provisioner'])
MAGE_TOWER = {'Alchemist': (3482, 2492, True), 'Scribe': (3487, 2494, True), 'Scholar': (3485, 2493, False)}
ROADSIDE_TRAINERS = {'Fisher': (2733, 1061, 0), 'Cook': (2745, 1066, 0)}


def build():
    regions = json.loads((DATA / 'regions.json').read_text())
    result = []
    for facet in ['Felucca','Trammel']:
        sources = [DATA / f'Spawns/{era}/{facet.lower()}/Vendors.json' for era in ['shared','uoml','post-uoml']]
        spawns = [(r, str(p.relative_to(DATA))) for p in sources if p.exists() for r in json.loads(p.read_text())]
        for name in TOWNS:
            if facet == 'Trammel' and name == 'Ocllo' or facet == 'Felucca' and name == 'New Haven':
                continue
            region = next(r for r in regions if r.get('Map') == facet and r.get('Name') == name)
            inside = lambda p: any(a['x1'] <= p[0] < a['x2'] and a['y1'] <= p[1] < a['y2'] for a in region['Area'])
            choices = [(r,p) for r,p in spawns if inside(r['location'])]
            assert choices, (facet, name)
            roles = list(ROLES)
            if facet == 'Trammel' and name == 'Cove':
                roles = ['Jeweller']
            elif facet == 'Trammel' and name == 'New Haven':
                roles.insert(roles.index('Scribe') + 1, 'Scholar')
            for role in roles:
                if facet == 'Trammel' and name == 'New Haven' and role in MAGE_TOWER:
                    x, y, relocate = MAGE_TOWER[role]
                    site = dict(id=f'{facet}/{name}/{role}', town=name, map=facet, role=role, x=x, y=y, z=52,
                                source='New Haven mage tower')
                    if relocate:
                        site['relocate'] = True
                    site['sharedWorkshop'] = False
                    result.append(site)
                    continue
                types = ROLES[role]
                ranked = [(min((types.index(e['name']) for e in r['entries'] if e['name'] in types),default=999), r,p)
                          for r,p in choices]
                _, spawn, path = min(ranked,key=lambda row:(row[0],row[1]['guid']))
                x,y,z = spawn['location']
                result.append(dict(id=f'{facet}/{name}/{role}',town=name,map=facet,role=role,x=x,y=y,z=z,
                                   source=f'{path}#{spawn["guid"]}',sharedWorkshop=not any(e['name'] in types for e in spawn['entries'])))
    for role, (x, y, z) in ROADSIDE_TRAINERS.items():
        result.append(dict(id=f'Trammel/Britannia/Roadside/{role}', town='Britannia', map='Trammel',
                           role=role, x=x, y=y, z=z, source='Roadside trainer', sharedWorkshop=False))
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    rows = build()
    text = json.dumps(rows, indent=2) + '\n'
    source_paths = [DATA / 'regions.json'] + sorted(DATA.glob('Spawns/*/felucca/Vendors.json')) + sorted(DATA.glob('Spawns/*/trammel/Vendors.json'))
    evidence = dict(nativeRevision=subprocess.check_output(['git','rev-parse','HEAD'], cwd=DATA.parent.parent, text=True).strip(),
                    catalogSha256=hashlib.sha256(text.encode()).hexdigest(),
                    sources={str(p.relative_to(DATA)):hashlib.sha256(p.read_bytes()).hexdigest() for p in source_paths},
                    townsFacets=len({(r['map'],r['town']) for r in rows}), services=len(rows))
    evidence_path = ROOT / 'tools/reference/town-markets-evidence.json'
    evidence_text = json.dumps(evidence, indent=2) + '\n'
    if args.check:
        assert OUT.read_text() == text, 'Stale town market catalogue'
        assert evidence_path.read_text() == evidence_text, 'Stale town market source evidence'
    else:
        OUT.write_text(text); evidence_path.write_text(evidence_text)
    print(f'Verified {len(rows)} service sites for {evidence["townsFacets"]} towns/facets')
