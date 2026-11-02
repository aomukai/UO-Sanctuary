#!/usr/bin/env python3
"""Bind Hydra reference preparation while proving accepted runtime inputs unchanged."""
import argparse,json
from pathlib import Path
from record_blaze import proof
from snapshot_skill_build import check as snapshot
import record_grizzly_body as inventory
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';sha=inventory.sha
def record(check=False):
 frozen=STAGE/'sorceress-player-frozen-orb';inventory.STAGE=frozen
 tested=inventory.validate_inventory(frozen/'birth-inputs.json');snapshot(frozen/'pre-build-5.json',frozen/'birth-inputs.json')
 specs=(('creation',12800,9),('ai',15552,8),('clock',576,8),('damage',41310,16),('bolt-creation',5760,10))
 proofs=[proof('hydra-'+name+'-source-evidence.json',cases,routines) for name,cases,routines in specs];assert sum(x[1] for x in specs)==75998
 animation=ROOT/'tools/reference/d2r-local-hydra-animation-evidence.json';a=json.loads(animation.read_text());assert a['referenceVersion']=='3.3.93847' and len(a['records'])==20 and a['monsterCodes']=={'hydra1':'HX','hydra2':'21','hydra3':'HZ'}
 for name,digest in a['artifacts'].items():assert sha(ROOT/name)==digest,name
 assert json.loads(proofs[1].read_text())['percentageDecisionDifferencesFromLow32']==624
 log=STAGE/'hydra-reference-checks-1.log';text=log.read_text()
 for phrase in ('Hydra current local animation:20records','Hydra creation:12800','Hydra AI:15552','Hydra ordinary clock:576','Hydra damage:41310','Hydra bolt creation:5760'):assert phrase in text,phrase
 assert 'Traceback' not in text and 'runtime error:' not in text
 parent=ROOT/'tools/reference/sorceress-player-frozen-orb-native-evidence.json';installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
 for name,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/name)==digest,name
 tools=[ROOT/'tools'/name for name in ('record_hydra_preflight.py','corroborate_local_hydra_animation.py','probe_hydra_creation.py','probe_hydra_ai.py','probe_hydra_clock.py','probe_hydra_damage.py','probe_hydra_bolt_creation.py')]
 docs=[ROOT/'docs'/name for name in ('sorceress-hydra-preflight.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','skill-effects.md','skill-icons.md')]
 paths=[*tools,*docs,*proofs,animation,parent,log,frozen/'birth-inputs.json',frozen/'pre-build-5.json']
 result=dict(referenceVersion='3.3.93847',component='Hydra creation,AI,ordinary animation clock,numeric capture and bolt factory preparation',runtimeChanged=False,installed=False,completedClasses=[],
  verified=dict(sourceCases=75998,headCreation=12800,aiTacticsSeed=15552,ordinaryClock=576,numericDamage=41310,boltFactory=5760,currentAnimationRecords=20,low32PercentageDecisionDifferences=624,unchangedInstalledArtifacts=106),unchangedRuntimeArtifacts=tested['artifacts'],artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(paths))},
  established=['Complete older-engine source compositions with explicit services and pinned current tables;current owned animation records corroborate12frame emergence/attack,frame8marker2and10frame death for HX/21/HZ.',
  'Per-head FIFO18cap and partial allocation semantics,owner substitution,hard/accrued birth capture,strict expiry/range,full64bit chance,ordinary clock handoff and monster-owned337to247fire projectiles have bounded source controls. The accepted Frozen Orb source/data/native assembly inventory is unchanged.'],
  remaining=['Native Hydra controller replacement,full targeting/world/path/InitSteps/Slow Missiles/interception,ordered collision/HP/return/events and final head/creator GUID retirement remain next. Full live scheduler/global/modern parity is unproved. No new native runtime acceptance is claimed.',
  'Approved temporary-trap facet/offline expiry rules must reuse existing lifecycle machinery. Preserve historical saved payloads;test actual inputs/resources and original saved heads/projectiles across two reloads. Final head/bolt/death effects/audio and talent/hotkey icons remain independent. No deployment or class certificate.'])
 target=ROOT/'tools/reference/hydra-preflight-evidence.json';render=json.dumps(result,indent=2)+'\n'
 if check:assert target.read_text()==render
 else:target.write_text(render)
 print('Hydra preflight:75998source cases/current20animation records;accepted Frozen Orb source/data/DLLs and106installed hashes unchanged;native controller work remains next.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
