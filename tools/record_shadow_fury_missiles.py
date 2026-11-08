#!/usr/bin/env python3
"""Bind own Fury missile checks, source reuse and preserved installation."""
import argparse,hashlib,json,re
from collections import Counter
from pathlib import Path
from probe_current_item_affixes import function
from record_inferno_held_input import preservation
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'shadow-fury-missiles'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main(check=False):
 proof=json.loads((ROOT/'tools/reference/shadow-fury-missiles-source-evidence.json').read_text());assert proof['contexts']==128
 for p,h in (proof['source']|proof['artifacts']|proof['retainedProof']).items():assert sha(ROOT/p)==h,p
 assert not(BASE/'shadow-fury-missiles-source/sanitizer.log').read_text()
 impact=json.loads((ROOT/'tools/reference/weapon-missile-impact-evidence.json').read_text());miss=(BASE/'d2moo-pinned-source/source/D2Game/src/MISSILES/MissMode.cpp').read_text()
 for name in ['MISSMODE_RollDamageValue','MISSMODE_FillDamageParams','MISSMODE_SetDamageFlags']:
  assert hashlib.sha256(function(miss,name).encode()).hexdigest()==impact['probe']['blockHashes'][name],name
 fixture=ROOT/impact['probe']['dataPath'];assert len(json.loads(fixture.read_text()))==5184
 # The pinned modern state registers the same Life Tap handler on both lanes.
 rawpath=ROOT/'tools/reference/d2r-named-equipment/json/skills.json';raw=json.loads(rawpath.read_text());tap=next(r for r in raw.values()if r.get('skill')=='Life Tap')
 assert (tap['auraevent1'],tap['auraeventfunc1'],tap['auraevent2'],tap['auraeventfunc2'])==('damagedinmelee',5,'damagedbymissile',5)
 log=(STAGE/'birth-native-checks.log').read_text();counts=Counter(re.findall(r'\] ([\w-]+): passed',log));assert sum(counts.values())==21 and ': failed'not in log and ' ERR]'not in log
 for name in ['shadow-fury-missiles','shadow-fury-packets','blade-fury-missile','npc-iron-maiden']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2
 assert 'pass across3boots/2reloads'in(STAGE/'native-run-7.log').read_text()
 for kind,n in [('content',2),('integration',7)]:assert all(x in(STAGE/f'{kind}-build-{n}.log').read_text()for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 native=json.loads((STAGE/'birth-inputs.json').read_text());snap=json.loads((STAGE/'source-snapshot-7.json').read_text());first=json.loads((STAGE/'source-snapshot-1.json').read_text())
 assert native['copiedData']==snap['inputs']['copiedData']
 for p,h in native['sourceInputs'].items():assert sha(ROOT/p)==h,p
 for p,h in snap['inputs']['sourceInputs'].items():assert native['sourceInputs'][p]==h,p
 # Production assembly was unchanged while correcting fixtures; no redundant
 # content rebuild after C2. Verify all production inputs against C2 snapshot.
 for p,h in first['inputs']['sourceInputs'].items():
  if p.startswith(('src/','shared/')):assert native['sourceInputs'][p]==h,p
 assert first['inputs']['copiedData']==native['copiedData']
 saved=json.loads((STAGE/'shadow-fury-missiles-fixtures.json').read_text());assert saved['Generation']==2 and len(saved['Bodies'])==2
 assert all(x['Flight']['Frame']==2 and x['Flight']['Npc']['Packet']['Actor']==x['Actor']for x in saved['Bodies'])
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert sum(x['implementation']=='handler-present'for x in status['skills'])==208 and status['completedClasses']==[]
 files=[p for pattern in ['source-snapshot-*.json','*-build-*.log','native-run-*.log','birth-*.json','birth-*.log','*-fixtures.json','*-fixture.json']for p in STAGE.glob(pattern)]
 files += [ROOT/p for p in ['tools/probe_shadow_fury_missiles.py','tools/record_shadow_fury_missiles.py','tests/check_shadow_fury_missiles.py','tools/reference/shadow-fury-missiles-source-evidence.json']]
 receipt=dict(parent='926959b61ee022a31113a495ae48914bdd45ffca',referenceVersion='3.3.93847',handlers=208,completedClasses=[],learnedShadowsEnabled=False,intrinsicFuryConnected=False,preparedNpcFuryMissiles=True,newSourceContexts=128,completeNewSourceRoutine=proof['completeRoutine'],retainedProof=proof['retainedProof'],retainedImpactFixture={str(fixture.relative_to(ROOT)):sha(fixture)},lifeTapEvents={k:v for k,v in tap.items()if k.startswith('auraevent')},rawSkillsSha256=sha(rawpath),nativeMarkers=21,markers=dict(counts),boots=3,reloads=2,saves=3,contentBuild=2,integrationBuild=7,sourceSnapshot=7,artifacts=native['artifacts'],files={str(p.relative_to(ROOT)):sha(p)for p in sorted(set(files))},preservation=preservation(),
 diagnostic='C1 was preliminary before prebuild snapshot and final guards;C2 is accepted production assembly. Native runs1-3 exposed fixture injections erased by ChangeSkills rebuilding original numeric frames;corrected setup injects selected claw fields after skill selection and checks existing owner-credit/per-ms poison contract. Run4 needed existing base-armor floor in its expected loss. Run5 reached Master and exposed omitted fixed mana-drain healing in the test expectation. Production receiver was unchanged by these fixture corrections. Run6 passed initial checks but the reload fixture moved the Master target three tiles off its saved flight line;restored the target to that saved lane. Runs and inputs retained;final run7 passes all3boots/2reloads.',
 scope='Both original seed1 Shadow species,own captured primary96source/typed/critical data,flat missile AR with own live original stats,private damage RNG after actor accuracy,before ordinary shield block,source flags/return. Shared prepared ranged HP/events,typed immunity,flat NPC life/mana drains,Life Tap versus melee-only Iron Maiden,owner-credit source-rate poison,saved doubled armor delta after HP,independent released movement/expiry and parked/offline no-fighting lifetime. Partial flight/original actor/items/resources/wounds/private seeds survive2reloads. Existing player Fury,own Fury collector/resource and original Revive/IronGolem melee retained. No old compiler matrices rerun.',
 remaining=['Actual NPC Fury SC startup/do callback,rewind/channel hosting and AI','Critical selection and actual world/private child-seed allocation (prepared API takes supplied inputs)','Blade Sentinel/death modifier42 and learned Shadow admission','Exact modern executable/PvP/unmapped recipient/bypass/burn/caster-removal and arbitrary callback mutation parity remain unverified;unsupported burn/bypass/positive armor explicitly refuse','Shock Web NPC localdelay15 question remains separate'])
 target=ROOT/'tools/reference/shadow-fury-missiles-native-evidence.json';text=json.dumps(receipt,indent=2)+'\n'
 if check:assert target.read_text()==text
 else:target.write_text(text)
 print('Own Fury travelling receiver:128 new source controls;21native markers/3boots/2reloads;208handlers/no class completion;installed artifacts/saves preserved.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');main(p.parse_args().check)
