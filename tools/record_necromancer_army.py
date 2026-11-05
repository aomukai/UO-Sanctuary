#!/usr/bin/env python3
"""Bind approved army progression to actual casts, gear, parking and saved originals."""
import argparse
from collections import Counter
from pathlib import Path
from snapshot_skill_build import check as snapshot
import skill_checkpoint_evidence as e
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/necromancer-army';PARENT='3e73282dd5493f112715983a3fc0c9ea409aaf35'
CHANGED={'src/Sanctuary.Content/'+n+'.cs'for n in('NecromancerArmyLimits','NecromancerSkeletons','ReviveNativeActivation','BattleCommand','ClassGear','PersistentMinions','TalentGump','SkeletonPets')}|{'tests/Sanctuary.Integration/'+n+'.cs'for n in('NecromancerArmyChecks','ReviveNativeActivationChecks','SkeletonRosterChecks')}
def build(check=False):
 born=e.load(STAGE/'birth-inputs.json');audit=e.load(STAGE/'audit-inputs.json');assert born['artifacts']==audit['artifacts']and born['copiedData']==audit['copiedData']
 for path in(STAGE/'birth-inputs.json',STAGE/'audit-inputs.json'):
  snapshot(STAGE/'pre-build-5.json',path)
  for p,h in e.load(path)['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
 for kind in('Content','Integration'):assert e.sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
 old=e.load(ROOT/'.local/class-skills/generated-npc-physical-stats/pre-build-1.json')['inputs'];now=e.load(STAGE/'pre-build-5.json')['inputs']
 assert {p for p in old['sourceInputs'].keys()|now['sourceInputs'].keys()if old['sourceInputs'].get(p)!=now['sourceInputs'].get(p)}==CHANGED
 assert old['projectInputs']==now['projectInputs']
 assert {p for p in old['copiedData'].keys()|now['copiedData'].keys()if old['copiedData'].get(p)!=now['copiedData'].get(p)}=={'Data/Sanctuary/Balance/necromancer-army-limits.json'}
 parent=e.load(ROOT/'tools/reference/generated-npc-physical-stats-native-evidence.json')
 for n in('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][n]==parent['artifacts'][n]
 wanted=Counter({'iron-golem-birth-check':3,'necromancer-army-live-result':3,'safe-area-minion-live-result':3})
 for family in('iron-golem-items','npc-resource-callbacks','necromancer-army','skeleton-roster','revive-native-activation','safe-area-minion','full-wardrobe'):
  wanted[family+'-check']=1;wanted[family+'-reload-check']=2
 log=e.cohort(STAGE,wanted,30)
 decision=e.load(ROOT/'tools/reference/necromancer-army-cap-decision.json');policy=e.load(ROOT/'data/Sanctuary/Balance/necromancer-army-limits.json')
 assert decision['status']=='user-approved'and decision['approval']=='yes, this looks like a good starting point'
 expected=[(1,1,1),(2,2,1),(3,3,1),(10,4,2),(20,5,3),(30,6,4),(40,7,5)]
 assert [(r['minimumRank'],r['mages'],r['revives'])for r in decision['steps']]==expected
 assert policy['ReferenceVersion']=='3.3.93847'and [(r['MinimumRank'],r['Mages'],r['Revives'])for r in policy['Steps']]==expected
 army=e.load(STAGE/'necromancer-army-fixtures.json');assert army['Generation']==2 and sum(p['Mage']for p in army['Pets'])==4 and sum(not p['Mage']for p in army['Pets'])==2
 assert len({p['Id']for p in army['Pets']})==6 and all(p['Rank']==20 and p['Order']>0 and p['Life']>0 for p in army['Pets'])
 for kind in('content','integration'):assert all(t in(STAGE/(kind+'-build-5.log')).read_text()for t in('Build succeeded.','0 Warning(s)','0 Error(s)'))
 audits=e.audits(STAGE,PARENT,born['artifacts']);commands=e.load(STAGE/'verification-commands.json');assert len(commands)==4 and all(c['exitCode']==0 for c in commands)
 assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text()and '\nOK\n'in(STAGE/'python-tests.log').read_text();protected=e.protection()
 files=[Path(__file__).resolve(),ROOT/'tools/skill_checkpoint_evidence.py',ROOT/'tests/check_necromancer_army.py',ROOT/'tools/reference/necromancer-army-cap-decision.json',ROOT/'data/Sanctuary/Balance/necromancer-army-limits.json']+[ROOT/p for p in CHANGED]
 files += [ROOT/'docs'/n for n in('necromancer-army-checkpoint.md','pet-adjustment-review.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-mechanical-reference.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')]
 files += e.stage_files(STAGE)+[p for d in STAGE.glob('failed-native-*')for p in d.rglob('*')if p.is_file()]
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert not status['completedClasses']and sum(r['implementation']=='handler-present'for r in status['skills'])==197
 receipt=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=197,completedClasses=[],artifacts=born['artifacts'],copiedData=born['copiedData'],contentBuild=5,integrationBuild=5,sourceSnapshot=5,nativeRun=5,nativeMarkers=30,boots=3,actualReloads=2,saves=3,explicitBoundaryControls=16,realGear='19hard+Tarnhelm1=20effective',maxMages=7,maxRevives=5,warriorGolemDruidChanged=False,pythonTests=71,verificationCommands=4,auditFingerprints=audits,protection=protected,
  scope='Approved current effective-rank quantity policy binds Mage successful creation/current-cap FIFO and original Revive learned creation/current-cap FIFO. Real learned casts preserve original actor/payment/corpse behavior. Stable deferred gear,resume/login and BattleCommand hooks enforce caps without intermediate wardrobe pruning. Maximal army failed wardrobe rollback and successful return,parked lowering,original wounds/ranks/order and actual two reloads pass alongside retained Skeleton/original Revive/safe-area/full-wardrobe/item/resource suites. Talent/pet count text updated.',
  limits=['No class completion,new handler,deployment,installed build/save/config/client change or new skill effect/audio/icon identity. Existing broader source AI/stat/event/current-engine and learned Revive capability gaps remain separate.',
   'Connected tests verify town gear callbacks,parking/survivors and server Gump construction;rendered UI/client delivery is not certified. Fake protocol burst can exhaust its4096byte send buffer;no production network setting was changed.',
   'Native1fixture corridor unavailable;native2revealed unrelated Warrior enforcement scope now fixed;native3and4exact diagnostics isolate integral-only prepared Revives recovering to full. Accepted fixture uses actual learned source-life/regeneration casts and retains exact wound assertions. Historical integral prototype recovery is not globally changed or certified.',
   'Existing two-original Revive fixture now trains10hard points for its unchanged two-slot conditions;no old-source petmax table was rewritten. Slot count uses current rank;per-pet combat/ability snapshots remain born values.'])
 e.write('necromancer-army-native-evidence.json',receipt,files,check)
 print('Necromancer army:approved limits7/5;real learned casts/gear/wardrobe/parking;30native markers/two reloads;71Python/audits/protection pass.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
