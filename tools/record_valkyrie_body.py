#!/usr/bin/env python3
"""Bind the saved supplied-birth Valkyrie backend and retained class mechanics."""
import argparse,hashlib,json,subprocess
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from record_poison_dagger_contact import markers
from record_inferno_held_input import preservation
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'valkyrie-body';WIDE=BASE/'valkyrie-body-retained'
PARENT='6f5bde2ac23418f15406cd3e7369742de8ee5d0b'
FAMILIES=['valkyrie-body','valkyrie-creation','goatman-body','tainted-body','defiler-body','goatman-foundation']
def record(check=False):
 born=e.inventory(STAGE,WIDE,STAGE/'source-snapshot-5.json')
 extra=Counter()
 for n in FAMILIES:extra.update({n+'-check':1,n+'-reload-check':2})
 base=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})
 e.cohort(STAGE,base+extra,27)
 prior=BASE/'leap-attack-retained/birth-native-checks.log';p=e.load(ROOT/'tools/reference/leap-attack-native-evidence.json')
 assert e.sha(prior)==p['files'][str(prior.relative_to(ROOT))]
 retained=markers(prior);assert sum(retained.values())==317;e.cohort(WIDE,retained+extra,335)
 for stage in [STAGE,WIDE]:
  assert 'pass across3boots/2reloads'in(stage/'native-run-1.log').read_text()
  log=(stage/'birth-native-checks.log').read_text();assert "denied by connection filter 'auto-denylist'"not in log
  rows=e.load(stage/'valkyrie-body-fixtures.json');assert len(rows)==2
  assert len({s['Owner']for s in rows})==len({s['Body']for s in rows})==2
  for s in rows:
   assert s['Generation']==2 and s['Maximum']-s['Life']==257
   state=json.loads(s['State']);b=state['Birth']
   assert state['Profile']=='prepared-valkyrie-body-v1'and state['Owner']==s['Owner']and state['Difficulty']==0
   assert b['Rank']==21 and b['Decoy']==10 and b['Dodge']==b['Avoid']==b['Evade']==b['Critical']==1
   assert b['SpawnLife256']in(400*256,480*256)and all(b[k]==0 for k in ['ColdSunder','FireSunder','LightningSunder','PoisonSunder','PhysicalSunder'])
 for n in ['content-build-5.log','integration-build-3.log']:
  log=(STAGE/n).read_text();assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 failed=STAGE/'compile-failed-1';assert 'CS1061'in(failed/'content-build-1.log').read_text()
 assert (failed/'AmazonValkyrieBody.cs').read_text().replace('using System;\n','using System;\nusing System.Collections.Generic;\n')==(ROOT/'src/Sanctuary.Content/AmazonValkyrieBody.cs').read_text()
 data=ROOT/'data/Sanctuary/Balance/necromancer-summons.json';old=json.loads(subprocess.check_output(['git','show',PARENT+':'+str(data.relative_to(ROOT))]))
 before={r['key']:r['fields']for r in old};after={r['key']:r['fields']for r in e.load(data)}
 assert set(after)-set(before)=={'valkyrie'}and all(after[k]==v for k,v in before.items())
 monstats=ROOT/'tools/reference/d2r-named-equipment/json/monstats.json';row=e.load(monstats)['valkyrie']
 assert after['valkyrie']=={k:v for k,v in row.items()if isinstance(v,int)}and row['AI']=='NecroPet'and row['noRatio']==1
 tree=e.load(BASE/'d2moo-source-tree.json');assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 src='source/D2Game/src/MONSTER/Monster.cpp';source=BASE/'d2moo-pinned-source'/src;raw=source.read_bytes()
 assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==src)
 text=raw.decode();assert 'nLevel, 7, &monStatsInit)'in text and 'STAT_ARMORCLASS, monStatsInit.nAC'in text
 # Existing compiled creation fixture is replayed here,not regenerated or
 # promoted to current initializer execution by this body increment.
 proof=ROOT/'tools/reference/valkyrie-creation-engine-evidence.json';priorproof=e.load(proof);f=priorproof['fixture']
 assert f['cases']==5184 and e.sha(ROOT/f['path'])==f['sha256']
 commands=e.load(STAGE/'verification-commands.json');assert len(commands)==7 and all(c['exitCode']==0 for c in commands)
 tests=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests'in tests and '\nOK\n'in tests
 fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation()
 client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
 for group in ['patchHashes','sharedInputs']:
  for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(s['implementation']=='handler-present'for s in status['skills'])==202 and not status['completedClasses']
 valk=next(s for s in status['skills']if s['talentId']=='amazon.valkyrie');assert valk['implementation']=='blocked'and not valk['complete']
 files=e.stage_files(STAGE,WIDE)+[Path(__file__).resolve(),ROOT/'tests/check_valkyrie_body.py',ROOT/'tools/build_class_skills.py',ROOT/'tools/build_skill_roadmap.py',data,monstats,proof,ROOT/f['path'],source,BASE/'d2moo-source-tree.json']
 files.extend(p for p in failed.rglob('*')if p.is_file())
 files += [ROOT/'docs'/p for p in ['valkyrie-body-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-mechanical-reference.md','skill-effects.md','skill-icons.md']]
 result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=202,amazonHandlers=28,completedClasses=[],
  learnedValkyrie=False,currentExecutableParity=False,suppliedBirthBackend=True,sourceSnapshot=5,contentBuild=5,integrationBuild=3,
  focusedRun=1,retainedRun=1,auditRun=1,focusedMarkers=27,retainedMarkers=335,
  bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,pythonTests=71,verificationCommands=7,auditFingerprints=fingerprints,
  protection=protected,clientPatchesUnchanged=41,artifacts=born['artifacts'],copiedData=born['copiedData'],
  scope=['Primitive summon-time rank,owner,hard synergies/passives,independent sunder fields and explicitly supplied noRatio life feed existing ValkyrieCreation. New serialized body retains source identity,base AC/level AR,armor percent,attributes/resistances,exact fractional wounds,owner/helm/order/payload through two reloads.',
   'Existing permanent ownership,wardrobe/difficulty and explicit source-frame recovery are reused. A real Amazon-to-Warlock-to-Amazon history transition checks class-aware parking and no refill;all three existing demon bodies retain their policies through the shared wrapper.',
   'Three difficulties,gear/training snapshot retention,defensive arrays,actual native fractional damage,no offline recovery,invalid life and atomic nonzero-sunder refusal are exercised. Existing5184compiled creation contexts replay unchanged;317cross-class parent markers retain.',
   'Only valkyrie is added to packaged source rows;all prior rows and573named decisions remain unchanged. Public availability and ordinary skeleton offense remain refused.'],
  limits=['Supplied400..480whole-point life is a declared post-initializer context,not a chosen component/room/player-count/RNG/allocation-seed rule. Inspected older flags7/zero weapon-AR reasoning is not current initializer execution.',
   'Full generated equipment/stat/event delivery,inherited Dodge/Avoid/Evade/Critical execution,nonzero sunder,actual NecroPet world/contact and paid cast/placement/replacement/local delay/input remain unfinished. Bare body numbers do not certify complete incoming/offensive admission.',
   'Current clock/AI/owner/gear/PvP/multiplayer/graphical-client and final effects/audio/tree/hotkey assets remain separate. Native female silhouette is an identified placeholder. No handler,class certificate or deployment.'])
 e.write('valkyrie-body-native-evidence.json',result,files,check)
 print('Valkyrie supplied body:27focused/335retained,two reloads,5184existing creation contexts,71Python;202handlers/Amazon28,learned cast still gated;installed build/saves preserved.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
