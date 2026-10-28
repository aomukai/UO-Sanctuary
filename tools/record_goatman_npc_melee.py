#!/usr/bin/env python3
"""Bind Goatman NPC rows, shared packets and self-state snapshots to native checks."""
import argparse,hashlib,json,re,subprocess
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/goatman-npc-melee'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
 inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json');artifacts=[]
 for kind in ['content','integration']:
  p=STAGE/(kind+'-build.log');assert all(x in p.read_text() for x in ['Build succeeded.','0 Warning(s)','0 Error(s)']);artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
 log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==177
 assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
 marker='Goatman NPC melee:24000complete-source packets/216Berserk state phases;shared rolls/conversion/critical reused,current callbacks/release/world actor remain gated';assert log.count(marker)==3
 frozen_receipt('b2bd29d','tools/reference/generic-pet-combat-native-evidence.json')
 previous=Counter(re.findall(r'\] ([A-Za-z0-9-]+): passed',(ROOT/'.local/class-skills/generic-pet-combat/stats-native-checks.log').read_text()));current=Counter(re.findall(r'\] ([A-Za-z0-9-]+): passed',log))
 assert sum(previous.values())==174 and current-previous==Counter({'goatman-npc-melee-check':1,'goatman-npc-melee-reload-check':2}) and not previous-current
 p=ROOT/'tools/reference/goatman-npc-melee-source-evidence.json';source=json.loads(p.read_text());assert (source['packetCases'],source['stateCases'],source['guardCases'])==(24000,216,16) and not source['currentExecutableParity'] and not source['handlerEnabled'];artifacts.append(p)
 for group in ['inputs','artifacts']:
  for n,h in source[group].items():assert sha(ROOT/n)==h,n;artifacts.append(ROOT/n)
 assert not (STAGE/'sanitizer.log').read_bytes()
 recheck=STAGE/'source-recheck.log';assert '24000complete-source packets,216Berserk state phases,16guards' in recheck.read_text();artifacts.append(recheck)
 proof=json.loads((ROOT/'.local/class-skills/goatman-npc-melee-source-probe.json').read_text());assert len(proof['packets'])==24000 and len(proof['states'])==216
 assert any(p[0][0]==418 and p[1][1]>0 and p[1][2]>0 for p in proof['packets'])
 rows=json.loads((ROOT/'data/Sanctuary/Balance/warlock-npc-skills.json').read_text());assert [r['sourceId'] for r in rows]==[416,417,418,419] and all(r['ability']==213 and r['talentId']=='warlock.summongoatman' and not r['left'] and not r['right'] for r in rows)
 audits=[]
 for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
  p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text());assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
  for n,h in a['InputHashes'].items():assert sha(ROOT/n)==h,n
  audits.append(a);artifacts += [p,ROOT/'tools/reference'/name]
 old=json.loads(subprocess.check_output(['git','show','b2bd29d:tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audits[0]['Definitions'])==573 and audits[0]['Definitions']==old['Definitions'] and [len(a['InputHashes']) for a in audits]==[713,21]
 py=(STAGE/'python-tests.log').read_text();assert 'Ran 70 tests' in py and '\nOK\n' in py and 'FAILED' not in py
 main=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(main['installedArtifacts'])==106
 for n,h in main['installedArtifacts'].items():assert sha(Path(main['runtimeRoot'])/n)==h,n
 for p in (STAGE/'diagnostics').iterdir():artifacts.append(p)
 assert 'KeyNotFoundException' in (STAGE/'diagnostics/missing-source-row-native.log').read_text() and 'wargoatman' in (STAGE/'diagnostics/missing-source-row-native.log').read_text()
 assert 'NPC packet differs from complete shared source at16:' in (STAGE/'diagnostics/mitigation-fixture-native.log').read_text()
 assert 'NPC packet differs from complete shared source at12041:' in (STAGE/'diagnostics/early-hit-class-native.log').read_text()
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert sum(s['implementation']=='handler-present' for s in status['skills'])==183 and status['completedClasses']==[]
 for n in ['stats-inputs.json','stats-native-checks.log','run.log','python-tests.log']:artifacts.append(STAGE/n)
 for p in STAGE.glob('*fixtures.json'):artifacts.append(p)
 for n in ['tools/record_goatman_npc_melee.py','tests/check_goatman_npc_melee.py','tools/build_skill_roadmap.py','data/Sanctuary/Balance/class-skill-status.json','data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json','tools/reference/creeper-frame-installation-evidence.json']:artifacts.append(ROOT/n)
 result=dict(referenceVersion='3.3.93847',component='Goatman NPC Stun/Berserk prepared packet and self-state snapshots',installed=False,handlerEnabled=False,currentExecutableParity=False,goalComplete=False,handlers=183,warlockHandlers=1,completedClasses=[],verified=dict(passMarkers=177,retainedMarkers=174,worldBoots=3,actualWorldReloads=2,completeSourcePacketsPerBoot=24000,berserkStatePhasesPerBoot=216,separateSourceGuards=16,npcRawRanks=255,pythonTests=70,unchangedNamedDefinitions=573,inputFingerprints=[713,21],unchangedMainInstallationArtifacts=106),inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},frozenPredecessor=dict(commit='b2bd29d',receipt='tools/reference/generic-pet-combat-native-evidence.json'),
  established=['Owned NPC416/417/418/419rows are imported separately from player talents. NPCStun has no Bash ED/WarCry duration synergy;NPCBerserk ED is105plus1per extra rank rather than player150plus15and synergies. NPCown learned AR context is explicit;owner talents are not inherited. Raw Frenzy/Cleave rows do not enable their callbacks.',
   'Stun/Berserk prepared packets reuse the checked Druid unarmed monster rolls and NamedElements reducer. Zero-width skill-element requests precede ordinary roll;NPC100percentphysical-to-magic conversion uses SourcePercentage before intrinsic monster critical5. Above the legacy threshold,early division may leave a small physical remainder. Typed damage,hit class,critical overlay,NoFillflags,stun/cold lengths and RNG order match24000complete-source packets per boot.',
   'Prepared Berserk self-state snapshots match216complete-source phases:miss-time timed state,existing flag preservation,new expiry,impact refresh without extension and temporaryflag4fallback. Source do2phase tests make the target absent to isolate self-state behavior;factory-failure early returns and flags remain separately visible. No real NPC defense,state expiry or health release is connected here.',
   'All174prior native marker counts remain exact. Both builds are clean,all70Python tests pass,573named decisions and106installed artifacts remain unchanged. No new actor or in-flight state persistence is claimed.'],
  remaining=['Complete shared older routines are supplementary evidence,not current executable parity. Actual result selection,source mode/event scheduling,Stun recipient controls,Berserk live defense/removal,persistence and real contact/release still require the eventual actor consumer.',
   'Modern extended passives,CrushingBlow/event16,Blood Oath,sunder handling,real GenericPet helper semantics,new Frenzy/Cleave callbacks,learned casting/placement/pool and owner/facet/logout/death/respec/history lifecycle remain unfinished. Final body/effects/audio and independent talent/right/quick icons remain tracked separately.',
   'The first native run exposed the missing owned wargoatman source row;the shared summon catalogue now imports it. The second exposed a one-unit rounding error in the fixture mitigation callback;it now reuses NamedElements.Reduce256. A third run caught premature typed hit-class assignment;ordinary added elements now defer that choice to release,while skill-element/intrinsic-critical assignments remain at allocation. Failed logs/input inventories are retained and source expectations remain unchanged. Original build/saves/configuration/client and frozen stages remain preserved;no installation,full-suite regression or class completion is claimed.'])
 p=ROOT/'tools/reference/goatman-npc-melee-native-evidence.json';out=json.dumps(result,indent=2)+'\n'
 if check:assert p.read_text()==out
 else:p.write_text(out)
 print('Goatman NPC melee:177native/3boots/2reloads,24000packets/216state phases per boot,70Python;live NPC/world actor remain gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
