#!/usr/bin/env python3
"""Bind Holy Bolt typed receiver,impact potion and optional saved capture."""
import argparse,json,subprocess,sys
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/holy-bolt-damage'
PARENT='335f31036cb084e7ffbea9276d67943797f7a3db';sha=inventory.sha
BUILD=3

def historical(path):return subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT)
def passed(path,total,boots):
 text=path.read_text();counts=markers(path)
 assert sum(counts.values())==total and ': failed' not in text and ' ERR]' not in text,path
 assert text.count('Shutting down')==text.count('Writing world save snapshot done')==boots,path
 return text,counts

def record(check=False):
 inventory.STAGE=STAGE;tested=inventory.validate_inventory(STAGE/'birth-inputs.json');snapshot(STAGE/f'pre-build-{BUILD}.json',STAGE/'birth-inputs.json')
 log,counts=passed(STAGE/'birth-native-checks.log',971,3);assert 'pass across3boots/2reloads' in (STAGE/f'full-run-{BUILD}.log').read_text()
 sys.path.insert(0,str(ROOT/'tests'));from check_holy_bolt_damage import configuration
 cfg=configuration(True);assert len(cfg['extra_families'])==70 and len(cfg['extra_checks'])==17 and len(cfg['extra_target_input_commands'])==93
 for name in cfg['extra_families']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2,name
 for name in cfg['extra_checks']:assert counts[name]==3,name
 for name in cfg['extra_target_input_commands']:
  assert counts[name+'-check']==counts[name+'-result']==3,name
  assert log.count(' '+name+': windup')==3,name
 phrase='Holy Bolt damage:impact-time four-lane potion;undead immunity/demon/beast rules;ordered notification/death/credit;no weapon leech/Thorns;captured slow;atomic/legacy restore;3pre-roll refusals'
 assert log.count(phrase)==1
 assert log.count('Holy Bolt saved context before fixture placement:')==2
 focus=STAGE/f'focused-accepted-{BUILD}';fi=inventory.validate_inventory(focus/'birth-inputs.json');passed(focus/'birth-native-checks.log',230,3)
 assert fi['artifacts']==tested['artifacts'] and fi['copiedData']==tested['copiedData']
 for part in ('content','integration'):
  path=part+'/Sanctuary.'+part.title()+'.dll';assert sha(STAGE/path)==sha(focus/path)
  text=(STAGE/f'{part}-build-{BUILD}.log').read_text();assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
 parent=ROOT/'tools/reference/charge-contact-native-evidence.json';assert parent.read_bytes()==historical(str(parent.relative_to(ROOT)))
 prior=json.loads((ROOT/'.local/class-skills/charge-contact/birth-inputs.json').read_text())
 for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert prior['artifacts'][name]==tested['artifacts'][name]
 assert prior['copiedData'].keys()==tested['copiedData'].keys()
 assert {p for p in tested['copiedData'] if tested['copiedData'][p]!=prior['copiedData'][p]}=={'Data/Sanctuary/Balance/class-skill-status.json'}
 now=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());old=json.loads(historical('data/Sanctuary/Balance/class-skill-status.json'));changed=[]
 for a,b in zip(now['skills'],old['skills'],strict=True):
  if a!=b:assert a['talentId']=='paladin.holybolt';changed.append(a['talentId']);b['remaining']=a['remaining']
 assert changed==['paladin.holybolt'] and now==old and not now['completedClasses']
 negative=STAGE/'negative-potion-launch';ni=json.loads((negative/'birth-inputs.json').read_text());text=(negative/'birth-native-checks.log').read_text()
 assert 'holy-bolt-damage-check: failed' in text and 'Holy Bolt incorrectly snapshots potion as extra original magic at launch' in text
 assert sha(negative/'content/Sanctuary.Content.dll')==ni['artifacts']['Assemblies/Sanctuary.Content.dll']==prior['artifacts']['Assemblies/Sanctuary.Content.dll']!=sha(STAGE/'content/Sanctuary.Content.dll')
 assert (negative/'HolyBolts.cs').read_bytes()==historical('src/Sanctuary.Content/HolyBolts.cs')
 diagnostic=STAGE/'focused-diagnostic-1'
 assert sha(diagnostic/'HolyBoltDamageChecks.cs')==ni['sourceInputs']['tests/Sanctuary.Integration/HolyBoltDamageChecks.cs']
 assert sha(negative/'integration/Sanctuary.Integration.dll')==sha(diagnostic/'integration/Sanctuary.Integration.dll')
 assert sha(diagnostic/'content/Sanctuary.Content.dll')==sha(STAGE/'content/Sanctuary.Content.dll')
 assert 'Saved Holy Bolt capture did not deliver after load' in (diagnostic/'birth-native-checks.log').read_text()
 # Binary substitution proof:negative inventory lists then-current workspace
 # sources;the parent DLL hash and separately archived parent source identify
 # the actual replaced binary. Do not claim a parent source-build inventory.
 source=ROOT/'tools/reference/holy-bolt-hit-engine-evidence.json';assert source.read_bytes()==historical(str(source.relative_to(ROOT)))
 sr=json.loads(source.read_text());assert sr['counts']==dict(eligibility=768,healing=16,guards=4) and sr['sourceCommit']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 for path,digest in sr['artifacts'].items():assert sha(ROOT/path)==digest,path
 legacy=ROOT/'tools/reference/holy-bolt-evidence.json';assert legacy.read_bytes()==historical(str(legacy.relative_to(ROOT)))
 for path,digest in json.loads(legacy.read_text())['sourceHashes'].items():assert sha(ROOT/path)==digest,path
 audit=json.loads((STAGE/'audit-inputs.json').read_text());assert audit['artifacts']==tested['artifacts'] and audit['copiedData']==tested['copiedData']
 text=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in text and ' ERR]' not in text and sum(markers(STAGE/'audit-native-checks.log').values())==4
 fingerprints=[]
 for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'):
  value=json.loads((STAGE/name).read_text());assert value==json.loads((ROOT/'tools/reference'/name).read_text())
  assert value['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
  for path,digest in value['InputHashes'].items():assert sha(ROOT/path)==digest,path
  previous=json.loads(historical('tools/reference/'+name));ignore={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
  assert {k:v for k,v in value.items() if k not in ignore}=={k:v for k,v in previous.items() if k not in ignore};fingerprints.append(len(value['InputHashes']))
 assert fingerprints==[907,21]
 text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text and 'FAILED' not in text
 text=(STAGE/'catalogue-checks.log').read_text()
 for value in ('Named readiness: 573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.'):assert value in text
 installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
 for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
 files=[Path(__file__).resolve(),ROOT/'tests/check_holy_bolt_damage.py',parent,source,legacy,ROOT/'tools/probe_holy_bolt_hit_reference.py',
  *(ROOT/'docs'/name for name in ('paladin-holy-bolt-damage-checkpoint.md','paladin-holy-bolt-contact-preflight.md','paladin-holy-bolt-flight-preflight.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','skill-effects.md','skill-icons.md')),
  *(STAGE/name for name in (f'pre-build-{BUILD}.json','birth-inputs.json','birth-native-checks.log',f'full-run-{BUILD}.log','holy-bolt-damage-checks.json','holy-bolt-checks.json','heaven-fist-checks.json','charge-checks.json','audit-inputs.json','audit-native-checks.log',f'audit-run-{BUILD}.log','python-tests.log','catalogue-checks.log',f'content-build-{BUILD}.log',f'integration-build-{BUILD}.log')),
  *(focus/name for name in ('birth-inputs.json','birth-native-checks.log',f'focused-run-{BUILD}.log','holy-bolt-damage-checks.json','holy-bolt-checks.json','heaven-fist-checks.json','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll')),
  *(negative/name for name in ('birth-inputs.json','birth-native-checks.log','run.log','HolyBolts.cs','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll')),
  *(diagnostic/name for name in ('birth-inputs.json','birth-native-checks.log','HolyBoltDamageChecks.cs','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll')),
  *(ROOT/'tools/reference'/name for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'))]
 result=dict(referenceVersion='3.3.93847',component='Holy Bolt ordered typed receiver,impact potion and optional saved slow/mastery capture',installed=False,graphicalClientVerified=False,completedClasses=[],
  verified=dict(nativeMarkers=971,focusedMarkers=230,boots=3,reloads=2,saves=3,newServerWireActions=9,retainedServerWireActions=270,legacySourceCases=788,auditMarkers=4,auditFingerprints=fingerprints,pythonTests=71,unchangedInstalledArtifacts=106),
  markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
  established=['The parent content DLL fails the retained launch-potion regression. New ordinary Holy Bolt excludes potion from original magic endpoints and captures optional item masteries/Slow Missiles. At impact,T29adds four noncompounding elemental lanes with independent defenses and no poison/chill statuses. Active/after-launch/removed/expired and mixed-resistance controls pass.',
   'The receiver reuses InstantSpellNpcDamage and ordered native notification/fractional HP/credit/death/return. Original magic bypasses positive resistance/MDR/absorb for undead only;demon magic retains ordinary defenses. Real Hell LichLord immunity,demon/beast filtering,no weapon leech/Thorns,and three pre-roll refusals pass. Unclassified actors keep native reactions without invented D2 identities.',
   'Captured50percent slow halves damage and velocity;removal does not recapture. Optional version0payloads survive both original-actor reloads with exact resources. A synchronous copied receiver check is separate from nine natural left/right/quick wire casts and does not certify a naturally resumed post-load source trajectory. Malformed/duplicate restore preserves the live collection;legacy uncaptured/FoH flights retain their original receiver.',
   'Original Holy Bolt healing/piercing/payment/Prayer synergy and FoH checks remain in focused/full cohorts. Nine new and270retained wire actions pass. Four audits preserve decisions;71Python/573named/240catalogue/generators pass;106installed hashes are unchanged.'],
  remaining=['Continue saved source-flight steps/per-flight RNG,Slow Missiles transport,owner/facet/death/offline scheduling,range/terrain/body/room/collision order. Current double-vector/catch-up flight remains a documented native adapter,not a source-flight certificate.',
   'Modern damage function15 and complete missile/event registrations/critical flags/PvP/charged/oskill/durability/old-content/client parity remain explicit. Sand Leaper/uninterruptible/death-delay/unsupported incoming contexts remain individually gated. Region/callback mutation is fail-fast and nontransactional.',
   'FoH secondary and Blessed Hammer migration remain separate. Final holy projectile/healing/impact/audio and talent/hotkey artwork remain identified placeholders. No class completion,deployment or save reset.'])
 output=ROOT/'tools/reference/holy-bolt-damage-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert output.read_text()==encoded
 else:output.write_text(encoded)
 print('Holy Bolt damage:971combined/230focused markers,9new/270retained wire actions,2reloads,4audits/71Python;106installed unchanged;no class completion.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
