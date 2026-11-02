#!/usr/bin/env python3
"""Bind Might/shared aura atomic persistence and honest instant-input acceptance."""
import argparse,json,subprocess,sys
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/might-aura';sha=inventory.sha
PARENT='e9e578079a41348fa77c83bd70a180a53b180fcc';FOCUS=1;BUILD=2;META='Data/Sanctuary/Balance/class-skill-status.json'
def historical(path):return subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT)
def passed(path,total):
 s=path.read_text();m=markers(path)
 assert sum(m.values())==total and ': failed' not in s and ' ERR]' not in s,path
 assert s.count('Shutting down')==s.count('Writing world save snapshot done')==3,path
 return s,m

def record(check=False):
 inventory.STAGE=STAGE;tested=inventory.validate_inventory(STAGE/'birth-inputs.json');snapshot(STAGE/f'pre-build-{BUILD}.json',STAGE/'birth-inputs.json')
 log,counts=passed(STAGE/'birth-native-checks.log',1121);assert 'pass across3boots/2reloads' in (STAGE/f'full-run-{BUILD}.log').read_text()
 sys.path.insert(0,str(ROOT/'tests'));from check_might_aura import configuration
 c=configuration(True);assert (len(c['extra_families']),len(c['extra_checks']),len(c['extra_target_input_commands']))==(77,18,112)
 for name in c['extra_families']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2,name
 for name in c['extra_checks']:assert counts[name]==3,name
 for name in c['extra_target_input_commands']:
  assert counts[name+'-check']==counts[name+'-result']==3,name
  marker=': acknowledged' if name in c['extra_instant_input_commands'] else ': windup';assert log.count(' '+name+marker)==3,name
 assert counts['might-aura-reload-result']==2 and log.count('Might:atomic two-pool restore/version0;zero-mana instant selection;')==1
 saved=json.loads((STAGE/'might-aura-checks.json').read_text());assert saved['Generation']==2 and saved['Mana']==0
 s=saved['State'];assert len(s['Emitters'])==1 and s['Emitters'][0]['Owner']==saved['Owner'] and s['Emitters'][0]['Ability']==102
 lease=next(l for l in s['Leases'] if l['Target']==saved['Ally']);assert (lease['Source'],lease['Rank'],lease['Percent'])==(saved['Owner'],1,40)
 focus=STAGE/f'focused-accepted-{FOCUS}';fi=json.loads((focus/'birth-inputs.json').read_text());passed(focus/'birth-native-checks.log',466)
 assert fi['artifacts']==tested['artifacts'] and fi['sourceInputs'].items()<=tested['sourceInputs'].items()
 for part in ('content','integration'):
  p=part+'/Sanctuary.'+part.title()+'.dll';assert sha(focus/p)==sha(STAGE/p)
  for build in (FOCUS,BUILD):
   s=(STAGE/f'{part}-build-{build}.log').read_text();assert all(x in s for x in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
 a=json.loads((STAGE/f'pre-build-{FOCUS}.json').read_text())['inputs'];b=json.loads((STAGE/f'pre-build-{BUILD}.json').read_text())['inputs']
 assert a['sourceInputs']==b['sourceInputs'] and a['projectInputs']==b['projectInputs']
 assert a['copiedData']==fi['copiedData'] and b['copiedData']==tested['copiedData']
 assert {p for p in fi['copiedData'] if fi['copiedData'][p]!=tested['copiedData'][p]}=={META}
 now=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());old=json.loads(historical('data/Sanctuary/Balance/class-skill-status.json'));changed=[]
 for x,y in zip(now['skills'],old['skills'],strict=True):
  if x!=y:assert x['talentId']=='paladin.might';changed.append(x['talentId']);y['remaining']=x['remaining']
 assert changed==['paladin.might'] and now==old and not now['completedClasses']
 parent=ROOT/'tools/reference/holy-shield-cast-native-evidence.json';assert parent.read_bytes()==historical(str(parent.relative_to(ROOT)))
 prior=json.loads((ROOT/'.local/class-skills/holy-shield-cast/birth-inputs.json').read_text())
 for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert prior['artifacts'][p]==tested['artifacts'][p]
 retained=dict(counts)
 for name in ('might-aura-check','might-aura-reload-check','might-aura-reload-result','might-aura-right-check','might-aura-right-result','might-aura-quick-check','might-aura-quick-result'):retained.pop(name)
 assert retained==json.loads(parent.read_text())['markers'],'Retained marker scope changed'
 negative=STAGE/'negative-atomic-restore';ni=json.loads((negative/'birth-inputs.json').read_text());s=(negative/'birth-native-checks.log').read_text()
 assert 'might-aura-check: failed' in s and 'Aura atomic restore emitter' in s
 assert sha(negative/'content/Sanctuary.Content.dll')==ni['artifacts']['Assemblies/Sanctuary.Content.dll']==prior['artifacts']['Assemblies/Sanctuary.Content.dll']!=sha(STAGE/'content/Sanctuary.Content.dll')
 assert (negative/'PaladinAuras.cs').read_bytes()==historical('src/Sanctuary.Content/PaladinAuras.cs')
 assert sha(negative/'MightAuraChecks.cs')==ni['sourceInputs']['tests/Sanctuary.Integration/MightAuraChecks.cs']
 source=ROOT/'tools/reference/might-callback-source-evidence.json';sr=json.loads(source.read_text());assert sr['cases']==640 and len(sr['routineHashes'])==3
 for p,h in (sr['sourceHashes']|sr['currentTables']|sr['artifacts']).items():assert sha(ROOT/p)==h,p
 for name in ('paladin-aura-engine-evidence.json','wolverine-aura-engine-evidence.json','paladin-aura-user-observations.json'):
  p=ROOT/'tools/reference'/name;assert p.read_bytes()==historical(str(p.relative_to(ROOT)))
 audit=json.loads((STAGE/'audit-inputs.json').read_text());assert audit['artifacts']==tested['artifacts'] and audit['copiedData']==tested['copiedData']
 s=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in s and ' ERR]' not in s and sum(markers(STAGE/'audit-native-checks.log').values())==4
 fingerprints=[]
 for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'):
  n=json.loads((STAGE/name).read_text());assert n==json.loads((ROOT/'tools/reference'/name).read_text()) and n['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
  for p,h in n['InputHashes'].items():assert sha(ROOT/p)==h,p
  o=json.loads(historical('tools/reference/'+name));ignore={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
  assert {k:v for k,v in n.items() if k not in ignore}=={k:v for k,v in o.items() if k not in ignore};fingerprints.append(len(n['InputHashes']))
 assert fingerprints==[911,21]
 s=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in s and '\nOK\n' in s and 'FAILED' not in s
 s=(STAGE/'catalogue-checks.log').read_text()
 for text in ('Named readiness: 573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.'):assert text in s
 installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
 for p,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==h,p
 docs=[ROOT/'docs'/n for n in ('paladin-might-aura-checkpoint.md','paladin-might-aura-preflight.md','paladin-defiance-aura-preflight.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')]
 files=[Path(__file__).resolve(),ROOT/'tests/check_might_aura.py',ROOT/'tests/check_iron_golem_birth.py',parent,source,*docs,
  *(ROOT/p for p in sr['artifacts']),*(STAGE/n for n in ('birth-inputs.json','birth-native-checks.log','might-aura-checks.json','paladin-aura-checks.json','holy-shield-cast-checks.json',f'pre-build-{FOCUS}.json',f'pre-build-{BUILD}.json',f'full-run-{BUILD}.log','audit-inputs.json','audit-native-checks.log',f'audit-run-{BUILD}.log','python-tests.log','catalogue-checks.log',*(f'{part}-build-{b}.log' for part in ('content','integration') for b in (FOCUS,BUILD)))),
  *(focus/n for n in ('birth-inputs.json','birth-native-checks.log',f'focused-run-{FOCUS}.log','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll')),
  *(negative/n for n in ('birth-inputs.json','birth-native-checks.log','MightAuraChecks.cs','PaladinAuras.cs','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll')),
  *(ROOT/'tools/reference'/n for n in ('named-item-runtime-audit.json','first-loop-affix-audit.json'))]
 result=dict(referenceVersion='3.3.93847',component='Might and shared Paladin aura atomic persistence',installed=False,graphicalClientVerified=False,completedClasses=[],
  verified=dict(nativeMarkers=1121,focusedMarkers=466,boots=3,reloads=2,saves=3,newServerWireActions=6,retainedServerWireActions=330,sourceCallbackCases=640,auditMarkers=4,auditFingerprints=fingerprints,pythonTests=71,unchangedInstalledArtifacts=106),
  markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
  established=['Shared aura snapshot occurs before background writing;both candidate dictionaries validate before commit. Malformed,duplicate and null tails preserve both live pools. Version0and all sibling aura fields remain unchanged;the parent DLL fails atomic emitter restoration.',
   'Might retains zero-mana instant selection,physical-only material composition,declared closed radius/height/hostile/no-LOS projection and rank/creator/linger semantics. Focused sibling aura and retained full class controls pass. Actual right-hold/quick packets use honest instant acknowledgements,with no invented windup or action-cycle reservation.',
   'Original emitter/recipient leases,owner/ally/sword identities,bindings,injuries and zero mana persist across two actual reloads. Inspection clocks explicitly hold/rearm,then the production timer emits fresh two-second leases without another selection or mana payment. This does not certify uninterrupted short-lived clocks through long downtime.',
   'Complete unchanged basic-aura/list/modifier composition passes640bounded cases with original stat/state enums and current table pins. Zero-payload controls are source-only boundaries. Focused1/final2compiled sources/projects/DLLs match,with one reviewed remaining-note metadata field. Fresh audits/71Python/573named/240catalogue/generators pass;106installed artifacts unchanged.'],
  remaining=['Modern executable aura callback/search/event/mode/ownership and complete recipient/pet/item/granted-skill/client parity remain bounded. Party friendliness,tile/subtile/height,town rooms,metadata/candidate order and no-lag-burst policy are explicit host projections.',
   'The source callback proof supplies allocation/store/formula/event/rate/metadata services and does not execute modern world search or the complete downstream physical-damage pipeline. Positive-rank Might zero-mana behavior remains distinct from generic supplied-zero controls and Fanaticism self-passives.',
   'Final rings/overlays,selection/refresh/expiry audio and talent/right/quick artwork remain separate placeholders. No class completion,deployment or save reset.'])
 output=ROOT/'tools/reference/might-aura-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert output.read_text()==encoded
 else:output.write_text(encoded)
 print('Might:1121combined/466focused,6instant/330retained wire actions,640source callbacks,2qualified aura reloads,4audits/71Python;106installed unchanged.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
