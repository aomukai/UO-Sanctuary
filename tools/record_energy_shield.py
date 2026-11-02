#!/usr/bin/env python3
"""Bind Energy Shield casting/FP absorption while retaining explicit modern gaps."""
import argparse,json,subprocess,sys
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/sorceress-energy-shield'
BUILD=2
PARENT='0d7f0ce'
sha=inventory.sha

def historical(name):return subprocess.check_output(['git','show',PARENT+':'+name],cwd=ROOT)

def source_proof(path,cases,routines):
    proof=json.loads(path.read_text());assert proof['referenceVersion']=='3.3.93847' and proof['cases']==cases
    assert len(proof['routineHashes'])==routines and proof['sanitizers']=='ASan/UBSan clean'
    for group in ('artifacts','currentTable'):
        for p,d in proof[group].items():assert sha(ROOT/p)==d,p
    for p,d in proof['sourceHashes'].items():assert sha(ROOT/'.local/class-skills/d2moo-pinned-source'/p)==d,p
    return proof

def record(check=False):
    inventory.STAGE=STAGE;tested=inventory.validate_inventory(STAGE/'birth-inputs.json')
    snapshot(STAGE/f'pre-build-{BUILD}.json',STAGE/'birth-inputs.json')
    counts=markers(STAGE/'birth-native-checks.log');log=(STAGE/'birth-native-checks.log').read_text()
    assert sum(counts.values())==499 and ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert 'pass across3boots/2reloads' in (STAGE/f'full-run-{BUILD}.log').read_text()
    sys.path.insert(0,str(ROOT/'tests'))
    from check_energy_shield import configuration
    c=configuration(True);assert len(c['extra_families'])==38 and len(c['extra_checks'])==10
    for n in c['extra_families']:assert counts[n+'-check']==1 and counts[n+'-reload-check']==2,n
    for n in c['extra_checks']:assert counts[n]==3,n
    assert len(c['extra_target_input_commands'])==44
    for n in c['extra_target_input_commands']:
        assert counts[n+'-check']==counts[n+'-result']==3,n
        assert log.count(' '+n+': windup')==3,n
    for text in ('Energy Shield:765rank/hard goldens,25344complete-source kernel/792native absorption comparisons,15cast-list comparisons',
                 'Shiver Armor:765rank/hard goldens,7680complete-source roll/160native HP-return-cold comparisons',
                 'TK source/native:rolls=7680 deliveries=2560 deaths=864 pushes=848',
                 'Ordered monster release:1616native complete-source compositions','NPC return:60488complete-source policies/exact traces'):
        assert text in log,text
    saved=json.loads((STAGE/'energy-shield-fixtures.json').read_text())
    assert saved['Generation']==2 and saved['Shield']['Rank']>20 and 0<saved['Shield']['AbsorbPercent']<=95
    assert saved['Mana']>0 and saved['Life']>0 and saved['TargetLife']>0
    focused=json.loads((STAGE/'diagnostic-2/birth-inputs.json').read_text());flog=(STAGE/'diagnostic-2/birth-native-checks.log').read_text()
    assert focused['artifacts']==tested['artifacts'] and focused['copiedData']==tested['copiedData']
    assert sum(markers(STAGE/'diagnostic-2/birth-native-checks.log').values())==46 and ' ERR]' not in flog and ': failed' not in flog
    assert flog.count('Shutting down')==flog.count('Writing world save snapshot done')==3
    assert 'pass across3boots/2reloads' in (STAGE/'diagnostic-run-2.log').read_text()
    for p,d in focused['sourceInputs'].items():assert sha(ROOT/p)==d,p
    for kind in ('content','integration'):
        text=(STAGE/f'{kind}-build-{BUILD}.log').read_text();assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
        n=f'{kind}/Sanctuary.{"Content" if kind=="content" else "Integration"}.dll';assert sha(STAGE/n)==sha(STAGE/'focused-accepted-2'/n)
    old=json.loads((ROOT/'.local/class-skills/sorceress-shiver-armor/birth-inputs.json').read_text())
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert old['artifacts'][n]==tested['artifacts'][n]
    assert old['copiedData'].keys()==tested['copiedData'].keys()
    assert {p for p in tested['copiedData'] if old['copiedData'][p]!=tested['copiedData'][p]}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    cast=ROOT/'tools/reference/energy-shield-cast-source-evidence.json';source_proof(cast,1620,3)
    callback=ROOT/'tools/reference/energy-shield-source-evidence.json';proof=source_proof(callback,25344,2)
    assert proof['guardCases']==8 and proof['damageOnlyDifferences']==92 and proof['currentRow']['*Id']==58
    assert callback.read_bytes()==historical(str(callback.relative_to(ROOT)))
    payment=ROOT/'tools/reference/buff-payment-source-evidence.json';source_proof(payment,60,5)
    parent=ROOT/'tools/reference/shiver-armor-native-evidence.json';assert parent.read_bytes()==historical(str(parent.relative_to(ROOT)))
    now=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());before=json.loads(historical('data/Sanctuary/Balance/class-skill-status.json'))
    new=next(s for s in now['skills'] if s['talentId']=='sorceress.energyshield');old=next(s for s in before['skills'] if s['talentId']=='sorceress.energyshield')
    assert new['verification']==old['verification']=='partial-native-coverage';old['remaining']=new['remaining'];assert now==before and not now['completedClasses']
    audited=inventory.validate_inventory(STAGE/'audit-inputs.json');assert audited['artifacts']==tested['artifacts'] and audited['copiedData']==tested['copiedData']
    assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    assert ' ERR]' not in (STAGE/'audit-native-checks.log').read_text();fingerprints=[]
    for n in ('named-item-runtime-audit.json','first-loop-affix-audit.json'):
        audit=json.loads((STAGE/n).read_text());assert audit==json.loads((ROOT/'tools/reference'/n).read_text())
        assert audit['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
        for p,d in audit['InputHashes'].items():assert sha(ROOT/p)==d,p
        old=json.loads(historical('tools/reference/'+n));ignore=('InputHashes','ContentAssemblySha256','ContentAssemblyPath')
        assert {k:v for k,v in audit.items() if k not in ignore}=={k:v for k,v in old.items() if k not in ignore};fingerprints.append(len(audit['InputHashes']))
    assert fingerprints==[895,21]
    text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text and 'FAILED' not in text
    text=(STAGE/f'checks-{BUILD}.log').read_text()
    for message in ('Named readiness: 573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.'):assert message in text
    assert 'Traceback' not in text
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for p,d in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==d,p
    files=[Path(__file__).resolve(),ROOT/'tests/check_energy_shield.py',ROOT/'tools/probe_energy_shield_cast.py',ROOT/'tools/probe_energy_shield.py',ROOT/'tools/probe_buff_payment.py',cast,callback,payment,parent,ROOT/'tools/build_skill_roadmap.py',
           *(ROOT/'docs'/n for n in ('sorceress-energy-shield-checkpoint.md','sorceress-energy-shield-preflight.md','sorceress-buff-payment-preflight.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-effects.md','skill-icons.md')),
           *(STAGE/n for n in (f'pre-build-{BUILD}.json','birth-inputs.json','birth-native-checks.log',f'full-run-{BUILD}.log','energy-shield-fixtures.json','diagnostic-2/birth-inputs.json','diagnostic-2/birth-native-checks.log','diagnostic-run-2.log','audit-inputs.json','audit-native-checks.log',f'audit-run-{BUILD}.log','python-tests.log',f'checks-{BUILD}.log',f'content-build-{BUILD}.log',f'integration-build-{BUILD}.log')),
           *(ROOT/'tools/reference'/n for n in ('named-item-runtime-audit.json','first-loop-affix-audit.json'))]
    result=dict(referenceVersion='3.3.93847',component='Energy Shield cast and ordered FP256 absorption',installed=False,graphicalClientVerified=False,completedClasses=[],
        verified=dict(nativeMarkers=499,focusedMarkers=46,boots=3,reloads=2,saves=3,rankHardGoldens=765,olderCallbackPercentageCompositions=25344,olderCallbackGuards=8,olderCastListPassiveCompositions=1620,olderGenericBuffPaymentCompositions=60,nativeAbsorptionComparisons=792,nativeListComparisons=15,originalSavedNativeHits=2,newServerWireActions=6,retainedServerWireActions=126,fixtureFamilies=38,sharedChecks=10,auditMarkers=4,auditFingerprints=fingerprints,pythonTests=71,unchangedInstalledArtifacts=106),
        markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
        established=['Complete older event24/percentage helper and do23/list/passive compositions establish bounded source channel ordering,integer rounding,depletion and state replacement.60additional complete generic handler/do23/do25/list compositions qualify failed-callback payment refusal. Formula/stat/list/events/payment call counting and modern outer services remain explicit.',
            'New right-only casts prepare before payment,spend5mana once and reject dead/internal/insufficient/cancelled/weaker attempts. Optional V0Rank retains cast rank;live hard Telekinesis determines efficiency. Actual JSON without Rank preserves captured legacy absorption. Shared SourcePercentage replaces naive exact division;poison remains excluded. Eight-field kernel includes separately supplied monster drains without fabricating native producers.',
            '765goldens,25344kernel/792native absorption and15list controls,actual native pre-resistance hits,real gear/current hard changes,6new/126retained wire actions and original owner/target/gear/fractional wounds/mana/unextended shield state across two reloads pass. Focused/final artifacts and copied data match;four fresh audits,71Python/573named/240catalogue/generators preserve item decisions and106installed hashes.'],
        remaining=['The native five-lane GearDamage bridge has no source drain-field producer/attacking-unit qualification adapter. Directional hit overlay,ignore-armor paths,modern mixed-shield/PvP/event/stat timing,arbitrary drain/proc subscribers,charged/oskill/client and old-DLL/unheld concurrent saves remain open. Older rounding-helper equivalence to current executable is explicitly uncertain.',
            'Psychic Ward/Energy Shield unsupported combination remains guarded. The older generic failed-callback payment branch now has60complete compositions;modern outer player-mode/mana-helper behavior and the native sibling Enchant weaker-release payment edge remain pending. Current Energy Shield refusal is covered independently. Final cast/shield/contact/depletion/expiry/audio and separate tree/hotkey icons remain identified placeholders. No class certificate or deployment.'])
    out=ROOT/'tools/reference/energy-shield-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('Energy Shield:499combined/46focused markers,25344source/792native absorption,1620source/15native lists,765goldens,two original saved hits;four audits/71Python;106installed hashes preserved;modern/drain/client/assets gaps explicit.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
