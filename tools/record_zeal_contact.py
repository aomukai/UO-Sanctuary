#!/usr/bin/env python3
"""Bind Zeal per-event allocation and ordered series contact to native evidence."""
import argparse,json,subprocess,sys
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/zeal-contact'
PARENT='d26cf4fce1d1f40367028f68faf597e1dc4fdb34'
sha=inventory.sha

def historical(path):return subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT)
def passed(path,total,boots):
 text=path.read_text();counts=markers(path)
 assert sum(counts.values())==total and ': failed' not in text and ' ERR]' not in text
 assert text.count('Shutting down')==text.count('Writing world save snapshot done')==boots
 return text,counts

def record(check=False):
 inventory.STAGE=STAGE
 tested=inventory.validate_inventory(STAGE/'birth-inputs.json');snapshot(STAGE/'pre-build-7.json',STAGE/'birth-inputs.json')
 log,counts=passed(STAGE/'birth-native-checks.log',872,3)
 assert 'pass across3boots/2reloads' in (STAGE/'full-run-7.log').read_text()
 sys.path.insert(0,str(ROOT/'tests'));from check_zeal_contact import configuration
 cfg=configuration(True);assert len(cfg['extra_families'])==69 and len(cfg['extra_checks'])==15 and len(cfg['extra_target_input_commands'])==78
 for name in cfg['extra_families']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2,name
 for name in cfg['extra_checks']:assert counts[name]==3,name
 for name in cfg['extra_target_input_commands']:
  assert counts[name+'-check']==counts[name+'-result']==3,name
  assert log.count(' '+name+': windup')==3,name
 phrase='Zeal contact:source/native deferred lethal return;unrolled windup/live per-event synergy and mitigation;once fractional start payment;miss/block flat return;latched initial/cyclic next target;real Sapphire/Wormskull;fractional leech/Life Tap;3start and1late pre-roll refusals'
 assert log.count(phrase)==3
 assert counts['zeal-check']==1 and counts['zeal-reload-check']==2
 for name in ('vengeance-contact-check','smite-contact-check','sacrifice-contact-check','poison-dagger-contact-check'):assert counts[name]==3
 focus=STAGE/'focused-accepted-7';fi=inventory.validate_inventory(focus/'birth-inputs.json');passed(focus/'birth-native-checks.log',112,3)
 assert fi['artifacts']==tested['artifacts'] and fi['copiedData']==tested['copiedData']
 for part in ('content','integration'):
  path=part+'/Sanctuary.'+part.title()+'.dll';assert sha(STAGE/path)==sha(focus/path)
  text=(STAGE/(part+'-build-7.log')).read_text();assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
 negative=STAGE/'negative-native-death';ni=json.loads((negative/'birth-inputs.json').read_text());text=(negative/'birth-native-checks.log').read_text()
 assert 'zeal-contact-check: failed' in text and 'Zeal must commit target HP before retaliation and defer target death until return' in text
 for name in ('PaladinZeal.cs','PlayerWeaponContact.cs','ClassCombat.cs'):
  assert (negative/name).read_bytes()==historical('src/Sanctuary.Content/'+name)
  assert sha(negative/name)==ni['sourceInputs']['src/Sanctuary.Content/'+name]
 assert sha(negative/'content/Sanctuary.Content.dll')==ni['artifacts']['Assemblies/Sanctuary.Content.dll']!=sha(STAGE/'content/Sanctuary.Content.dll')
 assert sha(negative/'ZealContactChecks.cs')==ni['sourceInputs']['tests/Sanctuary.Integration/ZealContactChecks.cs']
 prior=json.loads((ROOT/'.local/class-skills/vengeance-contact/birth-inputs.json').read_text())
 for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert prior['artifacts'][name]==tested['artifacts'][name]
 assert prior['copiedData'].keys()==tested['copiedData'].keys()
 assert {p for p in tested['copiedData'] if tested['copiedData'][p]!=prior['copiedData'][p]}=={'Data/Sanctuary/Balance/class-skill-status.json'}
 now=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());old=json.loads(historical('data/Sanctuary/Balance/class-skill-status.json'));changed=[]
 for a,b in zip(now['skills'],old['skills'],strict=True):
  if a!=b:assert a['talentId']=='paladin.zeal';changed.append(a['talentId']);b['remaining']=a['remaining']
 assert changed==['paladin.zeal'] and now==old and not now['completedClasses']
 parent=ROOT/'tools/reference/vengeance-contact-native-evidence.json';assert parent.read_bytes()==historical(str(parent.relative_to(ROOT)))
 source=ROOT/'tools/reference/zeal-engine-evidence.json';assert source.read_bytes()==historical(str(source.relative_to(ROOT)))
 probe=ROOT/'.local/class-skills/zeal-source-probe.json';source_record=json.loads(source.read_text());assert json.loads(probe.read_text())==source_record['probe']
 assert {n:len(v) for n,v in source_record['probe'].items()}==dict(selectionCases=21,startCases=5,strikeCases=12,timingCases=60)
 for path,digest in source_record['sourceHashes'].items():assert sha(ROOT/path)==digest,path
 current=ROOT/'tools/reference/zeal-current-animation-evidence.json';owned=json.loads(current.read_text());assert owned['referenceVersion']=='3.3.93847'
 import struct
 raw=ROOT/owned['ownedSource'];blob=raw.read_bytes();assert sha(raw)==owned['sha256']=='61ea70ede691e88a4bf375d872668d74d6cdca4ed5235034188ed1eb0d1e8ee5'
 at=0;rows=[];codes={r['code'] for r in source_record['animations']}
 for bucket in range(256):
  count=struct.unpack_from('<i',blob,at)[0];at+=4
  for _ in range(count):
   row=blob[at:at+160];at+=160;code=row[:8].rstrip(b'\0').decode()
   if code in codes:
    frames,speed=struct.unpack_from('<ii',row,8);events=[[i,v] for i,v in enumerate(row[16:]) if v];old=next(r for r in source_record['animations'] if r['code']==code)
    assert (frames,speed,events)==(old['frames'],old['speed'],[[old['event'],1]])
    rows.append(dict(code=code,frames=frames,speed=speed,events=events))
 assert at==len(blob) and len(rows)==6 and sorted(rows,key=lambda r:r['code'])==owned['records']
 route=next(r for r in json.loads((ROOT/'data/Sanctuary/Balance/class-skills.json').read_text()) if r['talentId']=='paladin.zeal');fields=route['sourceFields']
 assert owned['skillRoute']==dict(sourceId=route['sourceId'],start=fields['srvstfunc'],do=fields['srvdofunc'],sourceDamage=fields['SrcDam'],rollback=fields['Param2'])
 audited=inventory.validate_inventory(STAGE/'audit-inputs.json');assert audited['artifacts']==tested['artifacts'] and audited['copiedData']==tested['copiedData']
 assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
 assert ' ERR]' not in (STAGE/'audit-native-checks.log').read_text()
 fingerprints=[]
 for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'):
  value=json.loads((STAGE/name).read_text());assert value==json.loads((ROOT/'tools/reference'/name).read_text())
  assert value['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
  for path,digest in value['InputHashes'].items():assert sha(ROOT/path)==digest,path
  previous=json.loads(historical('tools/reference/'+name));ignore={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
  assert {k:v for k,v in value.items() if k not in ignore}=={k:v for k,v in previous.items() if k not in ignore};fingerprints.append(len(value['InputHashes']))
 assert fingerprints==[906,21]
 text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text and 'FAILED' not in text
 text=(STAGE/'catalogue-checks.log').read_text()
 for value in ('Named readiness: 573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.'):
  assert value in text
 installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
 for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
 files=[Path(__file__).resolve(),ROOT/'tests/check_zeal_contact.py',parent,source,probe,current,raw,ROOT/'tools/probe_zeal_reference.py',
  *(ROOT/'docs'/name for name in ('paladin-zeal-contact-checkpoint.md','paladin-conversion-contact-preflight.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','skill-effects.md','skill-icons.md')),
  *(STAGE/name for name in ('pre-build-7.json','birth-inputs.json','birth-native-checks.log','full-run-7.log','zeal-checks.json','vengeance-checks.json','smite-checks.json','sacrifice-checks.json','audit-inputs.json','audit-native-checks.log','audit-run-7.log','python-tests.log','catalogue-checks.log','content-build-7.log','integration-build-7.log')),
  *(focus/name for name in ('birth-inputs.json','birth-native-checks.log','focused-run-7.log','zeal-checks.json','vengeance-checks.json','smite-checks.json','sacrifice-checks.json')),
  *(negative/name for name in ('birth-inputs.json','birth-native-checks.log','PaladinZeal.cs','PlayerWeaponContact.cs','ClassCombat.cs','ZealContactChecks.cs','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll')),
  *(ROOT/'tools/reference'/name for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'))]
 result=dict(referenceVersion='3.3.93847',component='Zeal per-event ordinary weapon allocation and ordered series contact',installed=False,graphicalClientVerified=False,completedClasses=[],
  verified=dict(nativeMarkers=872,focusedMarkers=112,boots=3,reloads=2,saves=3,newServerWireActions=9,retainedServerWireActions=225,sourceSelectionCases=21,sourceStartCases=5,sourceStrikeCases=12,sourceTimingCases=60,currentAnimationRecords=6,auditMarkers=4,auditFingerprints=fingerprints,pythonTests=71,unchangedInstalledArtifacts=106),
  markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
  established=['The previous content DLL fails the preserved target-death-before-retaliation regression. Each do13 event now uses shared ordinary result/typed allocation and ordered player-to-NPC notification,credit,HP,leech,attempt,retaliation,deferred death and return. Start37 retains its acquired first target against held-cursor changes.',
   'Unrolled windup,once-only512FP256start payment and17remainder,independent live hard synergy/resistance with190/412FP256goldens,miss/block flat return,cyclic next target,mapped/native lethal credit,real Sapphire/Wormskull cold/poison,fractional physical leech/Life Tap,three start and one late pre-roll refusal pass.',
   'The existing native notification bridge skips a sub-whole-HP loss while source fractional health still changes;the following whole-HP notification runs before health commitment. The original series fixture now explicitly suppresses shield blocks in successful-hit timing/damage controls;new independent checks cover actual block/miss paths. The new contacts reuse its empty-area finder,avoiding legitimate cyclic retargeting to unrelated saved fixture monsters.',
   'Nine new actual left/right/quick two-hit sequences and225predecessor actions pass through both reloads. Original Zeal input/low-mana fallback,bindings,timing,cyclic target,gear,hard synergy,late-tick,cancellation,health/mana and transient save controls remain. Four audits,71Python/573named/240catalogue/generators and106protected installed hashes pass.',
   'The unchanged supplementary probe reproduces21selection,five start,twelve strike and60timing cases with declared services. Six owned current Paladin A1records corroborate animation inputs without certifying modern outer dispatch.'],
  remaining=['Exact modern mode/seed/world geometry and outer scheduling,critical/deadly result bits and complete item/kill-proc/durability order,special/PvP/charged/oskill,pending-action persistence,old-content migration and graphical-client parity remain separate.',
   'Unclassified UO actors retain native reactions without invented D2 shield/species/mode identities. Unsupported modifiers/Paragon/uninterruptible/death-delay contexts refuse before contact RNG;a newly unsupported rollback recipient ends the already-paid series without a new payment/refund. Region/callback mutation remains fail-fast and nontransactional.',
   'Continue the Paladin review with Conversion and Charge using existing services where their distinct contracts permit. Final rapid melee/weapon impact/miss/block/return,cold/poison/leech/audio and talent/hotkey art remain separately tracked placeholders. No class completion,deployment or save reset.'])
 output=ROOT/'tools/reference/zeal-contact-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert output.read_text()==encoded
 else:output.write_text(encoded)
 print('Zeal contact:872combined/112focused markers,9new/225retained wire actions,2reloads,4audits/71Python;106installed unchanged;no class completion.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
