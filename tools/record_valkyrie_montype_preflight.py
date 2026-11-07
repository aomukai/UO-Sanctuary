#!/usr/bin/env python3
"""Distinguish unused current monster-type properties from missing live skills."""
import argparse,hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def record(check=False):
 paths=['data/Sanctuary/Balance/npc-item-affix-catalogue.json','data/Sanctuary/Balance/class-skills.json','tools/reference/d2r-named-equipment/json/properties.json','tools/reference/d2r-named-equipment/json/magicprefix.json','tools/reference/d2r-named-equipment/json/magicsuffix.json','tools/reference/d2r-named-equipment/json/uniqueitems.json','tools/reference/d2r-named-equipment/json/setitems.json','src/Sanctuary.Content/ValkyrieEquipmentContact.cs','docs/valkyrie-montype-preflight.md','tools/reference/d2r-named-equipment/sources.json',str(Path(__file__).resolve().relative_to(ROOT))]
 data={p:json.loads((ROOT/p).read_text())for p in paths if p.endswith('.json')}
 manifest=data['tools/reference/d2r-named-equipment/sources.json']
 for p in paths[2:7]:assert hashlib.sha256((ROOT/p).read_bytes()).hexdigest()==manifest['files']['json/'+Path(p).name]['sha256'],p
 catalog=data[paths[0]];prop=data[paths[2]]['dmg-mon%'];assert prop['stat1']=='damage_vs_montype'and prop['func1']==24 and prop['*Notes']=='Currently Not Used'
 assert len(catalog['affixes'])==991 and len(catalog['bases'])==692 and len(catalog['equipmentRows'])==19
 for group in ['affixes','bases','equipmentRows']:assert 'dmg-mon%'not in json.dumps(catalog[group])and'damage_vs_montype'not in json.dumps(catalog[group])
 assert 'damage_vs_montype'not in json.dumps(data[paths[1]])
 scanned={}
 for p in paths[3:7]:
  rows=data[p];hits=[k for k,r in rows.items()if 'dmg-mon%'in r.values()];assert not hits,p;scanned[p]=len(rows)
 assert 'Original monster-type damage needs a concrete current target-type adapter'in(ROOT/paths[7]).read_text()
 result=dict(referenceVersion='3.3.93847',runtimeChanged=False,currentProperty=prop,catalogueAffixes=991,catalogueBases=692,equipmentRows=19,scannedCurrentTables=scanned,currentCatalogProducers=0,currentSkillStatProducers=0,nonzeroGuardRetained=True,blocksCurrentValkyrieGeneratedEquipment=False,scope='No dmg-mon% producer exists in the pinned NPC catalogue or scanned current property-bearing tables;the property itself is enabled but explicitly marked unused. No target taxonomy is inferred. Manually supplied/future nonzero stat180 remains unsupported and refused. Proceed with learned casting for current supported input data.',inputs={p:hashlib.sha256((ROOT/p).read_bytes()).hexdigest()for p in paths})
 out=ROOT/'tools/reference/valkyrie-montype-preflight.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Valkyrie monster-type preflight:991affixes/692bases/19equipment rows;no current producers;nonzero guard retained,learned work can proceed.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
