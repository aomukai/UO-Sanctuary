#!/usr/bin/env python3
"""Bind guarded general weapon components and explicit unknown-total negatives."""
import argparse,hashlib,json,subprocess,sys,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/gear-strike-link';BASE='9f22a9629ad08d3b426d1e3a315a63e08edf7846'
sys.path.insert(0,str(ROOT/'tests'))
from check_gear_strike_link import configuration
def markers():
 c=configuration();r=Counter({'iron-golem-birth-check':3})
 def add(n,count):
  r[n]+=count;results=c.get('extra_wait_results',{}).get(n,())
  for result in ((results,)if isinstance(results,str)else results):r[result]+=count
 for f in ('iron-golem-items','npc-resource-callbacks',*c['extra_families']):add(f+'-check',1);add(f+'-reload-check',2)
 for n in c['extra_checks']:add(n,3)
 for n in c['extra_target_input_commands']:r[n+'-check']+=3;r[n+'-result']+=3
 assert sum(r.values())==51
 return r
def prior(p):return subprocess.check_output(['git','show',BASE+':'+p],cwd=ROOT,text=True)
def record(check=False):
 born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/elite-damage-link/birth-inputs.json');snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
 for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
 changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d)
 assert changed==['src/Sanctuary.Content/GearEffects.cs','tests/Sanctuary.Integration/DefilerHealthLinkChecks.cs','tests/Sanctuary.Integration/GearStrikeLinkChecks.cs'],changed
 a=e.load(STAGE/'source-snapshot-1.json')['inputs'];b=e.load(STAGE/'source-snapshot-2.json')['inputs'];assert a['copiedData']==b['copiedData']==born['copiedData']==parent['copiedData']
 assert [p for p,d in a['sourceInputs'].items()if b['sourceInputs'][p]!=d]==['src/Sanctuary.Content/GearEffects.cs']
 for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
 status='data/Sanctuary/Balance/class-skill-status.json';assert (ROOT/status).read_text()==prior(status);ledger=e.load(ROOT/status);assert len(ledger['skills'])==240 and not ledger['completedClasses']
 for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
 for kind,build in (('Content',1),('Integration',2)):
  assert e.sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
  assert all(t in (STAGE/(kind.lower()+f'-build-{build}.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
 log=e.cohort(STAGE,markers(),51);assert 'pass across3boots/2reloads'in (STAGE/'native-run-1.log').read_text()
 assert log.count('Client: 127.0.0.1: Connected.')==9 and 'denied by connection filter'not in log and 'Warning: Attempted to delete'not in log
 assert sum(' WRN]'in line and 'Defiler link sharing refused unsupported context'in line for line in log.splitlines())==12
 assert e.load(STAGE/'defiler-health-link-fixtures.json')['Generation']==2
 with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as t:
  for k in ('Content','Integration'):assert hashlib.sha256(t.extractfile(k.lower()+f'/Sanctuary.{k}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{k}.dll']
 with tarfile.open(STAGE/'failed-baseline-1.tar.gz','r:gz')as t:assert 'Gear prepared/normal typed link:fault=Direct damage lacks settled typed components;primary=5056/5056;secondary=0/2677;events=1;typed=False;commit=1'in t.extractfile(str(STAGE.relative_to(ROOT)/'birth-native-checks.log')).read().decode()
 assert e.load(STAGE/'failed-baseline-1-native-inputs.json')['Assemblies/Sanctuary.Content.dll']==parent['artifacts']['Assemblies/Sanctuary.Content.dll']!=born['artifacts']['Assemblies/Sanctuary.Content.dll']
 audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json') and audit['Definitions']==json.loads(prior('tools/reference/named-item-runtime-audit.json'))['Definitions']
 from build_named_readiness import validate
 validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll');assert e.load(ROOT/'tools/reference/defiler-health-link-rule-approval.json')['approved']
 files=[ROOT/'tools/record_gear_strike_link.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'docs/gear-strike-link-checkpoint.md',ROOT/'docs/class-skill-roadmap.md',ROOT/'docs/warlock-coverage-matrix.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',ROOT/status,*configuration()['extra_inputs'],*e.stage_files(STAGE),*(ROOT/p for p in changed),*(STAGE/(n+'.tar.gz')for n in ('accepted-assemblies-1','failed-baseline-1'))]
 result=dict(component='Known general weapon packets retain non-poison components;unclassified life overrides preserve explicit scalar refusal',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=51,markers=dict(sorted(markers().items())),newLinkContextsPerBoot=16,newUnclassifiedOverrideNegativesPerBoot=2,wireInputs=6,connections=9,boots=3,saves=3,reloads=2,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[],mechanicalRuntimeDataChanged=False,contentBuild=1,integrationBuild=2,sourceSnapshot=2,nativeRun=1,failedCorrectedNativeAttempts=0,driverFailures=0,fixtureCompileFailures=0,nativeRebuilt=False,persistenceVersionChanged=False,newSavedFields=[],installed=False,namedAdmissionChanged=False,classCertified=False,goalComplete=False,knownWardrobeSaveWarnings=0,intentionalScalarRefusalWarnings=12,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='One GearEffects.Strike health boundary publishes known settled parts when LifeDamage256 absent/equal or explicitly adds allocated poison.Rate256;poison excluded from sharing,including empty non-poison purepoison contexts. Unexplained/truncated overrides remain scalar and faultactive links,with primarybudget preserved.16contexts/boot include prepared/factory x defenses/immunity plus equal/poison/purepoison/unknown/truncated/zero/cancel/suppressed,one commit gate,exact FP8 damage,original source,no extra mana/wear,and actual poison clock. Retained gems/contact/direct-event/leech/eightclassbasicprocs,6left/rightinputs and originalsaves cover their existing scopes;not every legacy/PvP path newlycertified. No corrected-run/sourcecompiler/fullclass rerun;positiveSentinelNPCflatdrain remainsunclassified.')
 e.write('gear-strike-link-native-evidence.json',result,files,check)
 print('General weapon receiver:51results,16contexts/boot,6basicinputs,9connections,2reloads;unknown overrides remain explicit.')
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
