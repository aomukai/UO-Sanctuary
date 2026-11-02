#!/usr/bin/env python3
"""Bind Conversion windup allocation,successful allegiance bypass and ordered failed contact to native evidence."""
import argparse,json,subprocess,sys
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/conversion-contact'
PARENT='127862c1ce191f9c271a1d304797b3f74bb8a59e'
sha=inventory.sha

def historical(path):return subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT)
def passed(path,total,boots):
 text=path.read_text();counts=markers(path)
 assert sum(counts.values())==total and ': failed' not in text and ' ERR]' not in text
 assert text.count('Shutting down')==text.count('Writing world save snapshot done')==boots
 return text,counts

def record(check=False):
 inventory.STAGE=STAGE
 tested=inventory.validate_inventory(STAGE/'birth-inputs.json');snapshot(STAGE/'pre-build-8.json',STAGE/'birth-inputs.json')
 log,counts=passed(STAGE/'birth-native-checks.log',911,3)
 assert 'pass across3boots/2reloads' in (STAGE/'full-run-8.log').read_text()
 sys.path.insert(0,str(ROOT/'tests'));from check_conversion_contact import configuration
 cfg=configuration(True);assert len(cfg['extra_families'])==69 and len(cfg['extra_checks'])==16 and len(cfg['extra_target_input_commands'])==84
 for name in cfg['extra_families']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2,name
 for name in cfg['extra_checks']:assert counts[name]==3,name
 for name in cfg['extra_target_input_commands']:
  assert counts[name+'-check']==counts[name+'-result']==3,name
  assert log.count(' '+name+': windup')==3,name
 phrase='Conversion contact:ordered failed-chance lethal return/credit;353FP windup snapshot;hit/miss/block successful bypass;aura removal/Enchant preservation;failed miss/block flat return;7cancellation/target cases;2predecessor weapon round trips;real Sapphire/Wormskull;545FP fractional leech/Life Tap;5pre-roll refusals'
 assert log.count(phrase)==3
 assert counts['conversion-check']==1 and counts['conversion-reload-check']==2
 for name in ('zeal-contact-check','vengeance-contact-check','smite-contact-check','sacrifice-contact-check','poison-dagger-contact-check'):assert counts[name]==3
 focus=STAGE/'focused-accepted-8';fi=inventory.validate_inventory(focus/'birth-inputs.json');passed(focus/'birth-native-checks.log',155,3)
 assert fi['artifacts']==tested['artifacts'] and fi['copiedData']==tested['copiedData']
 for part in ('content','integration'):
  path=part+'/Sanctuary.'+part.title()+'.dll';assert sha(STAGE/path)==sha(focus/path)
  text=(STAGE/(part+'-build-8.log')).read_text();assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
 negative=STAGE/'negative-native-death';ni=json.loads((negative/'birth-inputs.json').read_text());text=(negative/'birth-native-checks.log').read_text()
 assert 'conversion-contact-check: failed' in text and 'Failed Conversion must commit target HP before retaliation and defer target death until return' in text
 for name in ('PaladinConversion.cs','ClassCombat.cs','PlayerWeaponContact.cs'):
  assert (negative/name).read_bytes()==historical('src/Sanctuary.Content/'+name)
  assert sha(negative/name)==ni['sourceInputs']['src/Sanctuary.Content/'+name]
 assert sha(negative/'content/Sanctuary.Content.dll')==ni['artifacts']['Assemblies/Sanctuary.Content.dll']!=sha(STAGE/'content/Sanctuary.Content.dll')
 assert sha(negative/'ConversionContactChecks.cs')==ni['sourceInputs']['tests/Sanctuary.Integration/ConversionContactChecks.cs']
 prior=json.loads((ROOT/'.local/class-skills/zeal-contact/birth-inputs.json').read_text())
 for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert prior['artifacts'][name]==tested['artifacts'][name]
 assert prior['copiedData'].keys()==tested['copiedData'].keys()
 assert {p for p in tested['copiedData'] if tested['copiedData'][p]!=prior['copiedData'][p]}=={'Data/Sanctuary/Balance/class-skill-status.json'}
 now=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());old=json.loads(historical('data/Sanctuary/Balance/class-skill-status.json'));changed=[]
 for a,b in zip(now['skills'],old['skills'],strict=True):
  if a!=b:assert a['talentId']=='paladin.conversion';changed.append(a['talentId']);b['remaining']=a['remaining']
 assert changed==['paladin.conversion'] and now==old and not now['completedClasses']
 parent=ROOT/'tools/reference/zeal-contact-native-evidence.json';assert parent.read_bytes()==historical(str(parent.relative_to(ROOT)))
 source=ROOT/'tools/reference/conversion-engine-evidence.json';assert source.read_bytes()==historical(str(source.relative_to(ROOT)))
 probe=ROOT/'.local/class-skills/conversion-source-probe.json';source_record=json.loads(source.read_text());assert json.loads(probe.read_text())==source_record['probe']
 assert {n:len(v) for n,v in source_record['probe'].items()}==dict(eligibilityCases=64,expiryCases=16)
 for path,digest in source_record['sourceHashes'].items():assert sha(ROOT/path)==digest,path
 start_source=ROOT/'tools/reference/barbarian-attack-engine-evidence.json';assert start_source.read_bytes()==historical(str(start_source.relative_to(ROOT)))
 start_record=json.loads(start_source.read_text());start_probe=ROOT/start_record['probe']['dataPath'];sp=json.loads(start_probe.read_text())
 assert len(sp['cases'])==start_record['probe']['cases']==72 and sp['guards']==[0,0,0]
 for hit,flat,multiplier,resist,src,ok,physical,fill,total,elements,used,stored,lost in sp['cases']:
  expected=150*256*multiplier*(100-resist)//100 if hit else 0
  assert (ok,physical,fill,total,elements,used,stored,lost)==(1,expected+flat*256,hit,hit,hit,128 if hit else 0,expected,expected)
 base=ROOT/'.local/class-skills/d2moo-pinned-source'
 for path,digest in start_record['sourceHashes'].items():assert sha(base/path)==digest,path
 tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416' and not tree['truncated']
 import hashlib
 entries={row['path']:row['sha'] for row in tree['tree']}
 for path in start_record['sourceHashes']:
  blob=(base/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(blob)).encode()+b'\0'+blob).hexdigest()==entries[path]
 equipment=STAGE/'focused-diagnostic-2';ei=json.loads((equipment/'birth-inputs.json').read_text())
 assert 'Conversion did not immediately cancel: weapon' in (equipment/'birth-native-checks.log').read_text()
 assert (equipment/'DualEquip.cs').read_bytes()==historical('src/Sanctuary.Content/DualEquip.cs')
 assert sha(equipment/'DualEquip.cs')==ei['sourceInputs']['src/Sanctuary.Content/DualEquip.cs']!=sha(ROOT/'src/Sanctuary.Content/DualEquip.cs')
 assert sha(equipment/'content/Sanctuary.Content.dll')==ei['artifacts']['Assemblies/Sanctuary.Content.dll']!=sha(STAGE/'content/Sanctuary.Content.dll')
 assert sha(equipment/'ConversionContactChecks.cs')==ei['sourceInputs']['tests/Sanctuary.Integration/ConversionContactChecks.cs']
 before_aura=STAGE/'pre-aura-7';bi=json.loads((before_aura/'birth-inputs.json').read_text());passed(before_aura/'birth-native-checks.log',911,3)
 assert sha(before_aura/'content/Sanctuary.Content.dll')==bi['artifacts']['Assemblies/Sanctuary.Content.dll']
 assert sha(before_aura/'integration/Sanctuary.Integration.dll')==bi['artifacts']['Assemblies/Sanctuary.Integration.dll']
 assert sha(before_aura/'PaladinConversion.cs')==bi['sourceInputs']['src/Sanctuary.Content/PaladinConversion.cs']
 assert sha(before_aura/'ConversionContactChecks.cs')==bi['sourceInputs']['tests/Sanctuary.Integration/ConversionContactChecks.cs']
 assert sha(before_aura/'class-skill-status.json')==bi['copiedData']['Data/Sanctuary/Balance/class-skill-status.json']
 aura=STAGE/'negative-aura-retention';ai=json.loads((aura/'birth-inputs.json').read_text())
 assert 'Successful Conversion retained an aura state' in (aura/'birth-native-checks.log').read_text()
 assert ai['copiedData']==bi['copiedData']
 assert sha(aura/'content/Sanctuary.Content.dll')==ai['artifacts']['Assemblies/Sanctuary.Content.dll']==sha(before_aura/'content/Sanctuary.Content.dll')!=sha(STAGE/'content/Sanctuary.Content.dll')
 assert sha(aura/'PaladinConversion.cs')==ai['sourceInputs']['src/Sanctuary.Content/PaladinConversion.cs']==sha(before_aura/'PaladinConversion.cs')
 assert sha(aura/'ConversionContactChecks.cs')==ai['sourceInputs']['tests/Sanctuary.Integration/ConversionContactChecks.cs']
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
 files=[Path(__file__).resolve(),ROOT/'tests/check_conversion_contact.py',parent,source,probe,start_source,start_probe,ROOT/'tools/probe_conversion_reference.py',ROOT/'tools/probe_barbarian_attack_reference.py',
  *(ROOT/'docs'/name for name in ('paladin-conversion-contact-checkpoint.md','paladin-conversion-contact-preflight.md','paladin-charge-contact-preflight.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','skill-effects.md','skill-icons.md')),
  *(STAGE/name for name in ('pre-build-8.json','birth-inputs.json','birth-native-checks.log','full-run-8.log','conversion-checks.json','zeal-checks.json','vengeance-checks.json','smite-checks.json','sacrifice-checks.json','audit-inputs.json','audit-native-checks.log','audit-run-8.log','python-tests.log','catalogue-checks.log','content-build-8.log','integration-build-8.log')),
  *(focus/name for name in ('birth-inputs.json','birth-native-checks.log','focused-run-8.log','conversion-checks.json','zeal-checks.json','vengeance-checks.json','smite-checks.json','sacrifice-checks.json')),
  *(negative/name for name in ('birth-inputs.json','birth-native-checks.log','PaladinConversion.cs','ClassCombat.cs','PlayerWeaponContact.cs','ConversionContactChecks.cs','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll')),
  *(equipment/name for name in ('birth-inputs.json','birth-native-checks.log','DualEquip.cs','PaladinConversion.cs','ClassCombat.cs','ConversionContactChecks.cs','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll')),
  *(before_aura/name for name in ('birth-inputs.json','birth-native-checks.log','full-run-7.log','pre-build-7.json','PaladinConversion.cs','ConversionContactChecks.cs','class-skill-status.json','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll')),
  *(aura/name for name in ('birth-inputs.json','birth-native-checks.log','PaladinConversion.cs','ConversionContactChecks.cs','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll')),
  *(ROOT/'tools/reference'/name for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'))]
 result=dict(referenceVersion='3.3.93847',component='Conversion windup contact capture,successful allegiance bypass and ordered failed delivery',installed=False,graphicalClientVerified=False,completedClasses=[],
  verified=dict(nativeMarkers=911,focusedMarkers=155,boots=3,reloads=2,saves=3,newServerWireActions=18,retainedServerWireActions=234,sourceEligibilityCases=64,sourceExpiryCases=16,sourceStartCases=72,sourceStartGuards=3,auditMarkers=4,auditFingerprints=fingerprints,pythonTests=71,unchangedInstalledArtifacts=106),
  markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
  established=['The previous content DLL fails the preserved target-death-before-retaliation regression. The preserved build7also fails the new aura-retention regression;successful onset now reuses ClearAuras after alignment/AI and caster-target changes,clearing Thorns/Orders/Shout/Command while preserving Enchant. Start32 now captures result/block and ordinary typed damage/mitigation/cold/poison/drain via shared services,retaining calc1 enhanced physical damage and one1024FP256start payment.',
   'Do79 successful conversion after hit/miss/block preserves the one-HP recipient and bypasses damage/status/leech/attempt/return/wear. Failed conversion uses shared ordered notification/credit/health/retaliation/deferred death.353FPcaptured damage survives changed rank/resistance;545FPphysical leech/Life Tap,real Sapphire/Wormskull and five pre-roll refusals pass.',
   'Seven cancellation/target cases include out-and-back movement,weapon round trip,rejected equip,Stop,range,held cursor and late unsupported recipient. The preserved candidate2fails immediate weapon cancellation because equip transaction hooks were suppressed;successful commits now notify combat after refresh. Rejected equips preserve pending actions,and Sacrifice/Vengeance round trips also cancel without HP or mana loss.',
   'Eighteen new actual success/failure left/right/quick actions and234predecessor actions pass through both reloads. Original converted actor allegiance/AI/protection/expiry/ownership/resource/save controls remain. Retained actors are held after actual AI checks and on load,then released only for synchronous protection checks;the original saved life remains required without healing/reset. Wire recipients use existing Prevent Monster Heal to isolate delivery from their custom MonStats regeneration;gameplay regeneration is unchanged. Four audits preserve decisions;71Python/573named/240catalogue/generators pass;106protected installed artifacts are unchanged.',
   'The unchanged older probes reproduce64eligibility,16expiry and72shared start/allocation cases/three guards with declared services. Higher-level life restoration retains its recorded legacy discrepancy;no modern executable or class certificate is claimed.'],
  remaining=['Higher-level restoration,mounted Imps and unclassified source eligibility remain individually gated. Existing live release chance/rank semantics,exact modern animation/event/mode/seed/world geometry,critical result bits and full item/durability order,special/PvP/charged/oskill,pending-action persistence,old-content migration and graphical-client parity remain separate.',
   'Unsupported incoming native modifiers/Paragon/uninterruptible/death-delay contexts refuse before relevant RNG;late refusal retains the already-paid start without a second charge/refund. Native whole-HP notifications retain their existing boundary;source fractional health is separate. Region/callback mutation remains fail-fast and nontransactional.',
   'Continue with Charge,preserving the observed nine-mana payment with and without enemy target. Final ordinary-contact/allegiance/expiry/audio and talent/hotkey assets remain separately identified placeholders. No class completion,deployment or save reset.'])
 output=ROOT/'tools/reference/conversion-contact-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert output.read_text()==encoded
 else:output.write_text(encoded)
 print('Conversion contact:911combined/155focused markers,18new/234retained wire actions,2reloads,4audits/71Python;106installed unchanged;no class completion.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
