#!/usr/bin/env python3
"""Retain pinned damage-related property metadata for selected-hand consumers."""
import argparse
import hashlib
import json
import re
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
SOURCE=ROOT/'tools/reference/d2r-named-equipment'

def build():
    pin=json.loads((SOURCE/'sources.json').read_text())['files'];hashes={}
    for name in ['properties','itemstatcost']:
        p=SOURCE/'json'/f'{name}.json';h=hashlib.sha256(p.read_bytes()).hexdigest()
        assert h==pin[f'json/{name}.json']['sha256'],name
        hashes[f'json/{name}.json']=h
    stats={v['Stat']:v for v in json.loads((SOURCE/'json/itemstatcost.json').read_text()).values() if v.get('Stat')}
    rules=[];evidence=[];unresolved=[]
    runtime=ROOT/'src/Sanctuary.Content/NamedPropertyRuntime.cs';text=runtime.read_text()
    supported=set()
    for key in ['_scalar','_range','_scaled']:
        body=text.split(' '+key+' =',1)[1].split(';',1)[0]
        supported.update(re.findall(r'"([^"]+)"',body))
    for row in json.loads((SOURCE/'json/properties.json').read_text()).values():
        code=row.get('code')
        if not code:continue
        names=[row['stat'+str(i)] for i in range(1,8) if row.get('stat'+str(i))]
        unknown=[name for name in names if name not in stats]
        flags=[bool(stats[name].get('damagerelated')) if name in stats else None for name in names]
        if unknown:
            assert code not in supported,(code,unknown,'resolve special function before enabling')
            unresolved.append(dict(code=code,statReferences=unknown,status='Unsupported runtime property; special function context unresolved. False runtime default does not establish a global bonus.'))
        special={'dmg-min':5,'dmg-max':6,'dmg%':7}.get(code)
        if special:assert row.get('func1')==special and not names
        damage=bool(special) or any(flags)
        # Mixed-output properties need per-stat projection before they can be
        # filtered wholesale. No such property is silently accepted here.
        assert not flags or all(flags) or not any(flags),(code,names,flags)
        rules.append(dict(code=code,damageRelated=damage))
        evidence.append(dict(code=code,stats=names,damageRelatedFlags=flags,specialDamageFunction=special))
    assert len(rules)==len({v['code'] for v in rules})
    # Numeric consumers query the runtime's aliases, while property iterators
    # query the original source code. Retain both names with the same flag.
    alias_body=text.split(' _aliases =',1)[1].split('};',1)[0]
    aliases=dict(re.findall(r'\["([^"]+)"\]\s*=\s*"([^"]+)"',alias_body));aliases['dmg']='normal-damage'
    by_code={v['code']:v['damageRelated'] for v in rules}
    for original,alias in aliases.items():
        flag=by_code[original]
        if alias in by_code:assert by_code[alias]==flag,(original,alias)
        else:rules.append(dict(code=alias,damageRelated=flag));by_code[alias]=flag
    scaled_body=text.split(' _scaled =',1)[1].split('};',1)[0]
    for original,target in re.findall(r'\["([^"]+)"\]\s*=\s*"([^"]+)"',scaled_body):
        if target not in by_code:
            rules.append(dict(code=target,damageRelated=by_code[original]));by_code[target]=by_code[original]
        assert by_code[original]==by_code[target],(original,target)
    paths={ROOT/'data/Sanctuary/Balance/class-weapon-properties.json':json.dumps(sorted(rules,key=lambda v:v['code']),indent=2)+'\n',
        ROOT/'tools/reference/weapon-property-hand-evidence.json':json.dumps(dict(referenceVersion='3.3.93847',sourceHashes=hashes,runtimeProjection=dict(path=str(runtime.relative_to(ROOT)),sha256=hashlib.sha256(runtime.read_bytes()).hexdigest(),aliases=aliases,scaledTargets=dict(re.findall(r'\["([^"]+)"\]\s*=\s*"([^"]+)"',scaled_body))),status='Pinned supported property/stat classification, not full native dual-wield certification. Unresolved unsupported rows are listed separately.',rules=evidence,unresolvedStatReferences=unresolved,limits=['Properties with mixed damage/non-damage outputs must not be filtered wholesale; generator rejects them.','Special local physical min/max/ED property functions5/6/7 have implicit weapon stats rather than stat columns.','A source property with a non-stat reference cannot be enabled in NamedPropertyRuntime until its special function/hand projection is resolved. Its false default is not proof of global behavior.','Crafted/rune/material dual-hand policy and native equip/primary identity/sequence behavior remain separate work.']),indent=2)+'\n'}
    return paths

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');args=parser.parse_args()
    for path,text in build().items():
        if args.check:assert path.read_text()==text,path
        else:path.write_text(text)
    print('Pinned selected-hand property rules verified.')
