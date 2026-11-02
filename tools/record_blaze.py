#!/usr/bin/env python3
"""Bind Blaze's prepared cast, typed saved fields and retained native contracts."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess
import sys
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/sorceress-blaze'
BUILD=6
PARENT='93c759251d321211879cdd88354bf51ffaf6e2a2'
sha=inventory.sha

def historical(name):
    return subprocess.check_output(['git','show',PARENT+':'+name],cwd=ROOT)

def proof(name,cases,routines):
    p=ROOT/'tools/reference'/name
    value=json.loads(p.read_text())
    assert value['referenceVersion']=='3.3.93847' and value['cases']==cases
    assert len(value['routineHashes'])==routines and value['sanitizers']=='ASan/UBSan clean'
    for group in ('artifacts','currentTable'):
        for file,digest in value[group].items():assert sha(ROOT/file)==digest,file
    for file,digest in value['sourceHashes'].items():assert sha(ROOT/'.local/class-skills/d2moo-pinned-source'/file)==digest,file
    return p

def record(check=False):
    inventory.STAGE=STAGE
    tested=inventory.validate_inventory(STAGE/'birth-inputs.json')
    snapshot(STAGE/f'pre-build-{BUILD}.json',STAGE/'birth-inputs.json')
    counts=markers(STAGE/'birth-native-checks.log')
    log=(STAGE/'birth-native-checks.log').read_text()
    assert sum(counts.values())==555 and ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert 'pass across3boots/2reloads' in (STAGE/f'full-run-{BUILD}.log').read_text()
    sys.path.insert(0,str(ROOT/'tests'))
    from check_blaze import configuration
    c=configuration(True)
    assert len(c['extra_families'])==40 and len(c['extra_checks'])==10 and len(c['extra_target_input_commands'])==52
    for n in c['extra_families']:assert counts[n+'-check']==1 and counts[n+'-reload-check']==2,n
    for n in c['extra_checks']:assert counts[n]==3,n
    for n in c['extra_target_input_commands']:
        assert counts[n+'-check']==counts[n+'-result']==3,n
        assert log.count(' '+n+': windup')==3,n
    for text in ('Blaze:765rank/hard goldens,23040complete-source rolls/120native HP-return comparisons',
                 'Energy Shield:765rank/hard goldens,25344complete-source kernel/792native absorption comparisons',
                 'Buff payment:45native self/ally/fallback comparisons with complete generic wrapper',
                 'TK source/native:rolls=7680 deliveries=2560 deaths=864 pushes=848',
                 'Ordered monster release:1616native complete-source compositions','NPC return:60488complete-source policies/exact traces'):
        assert text in log,text
    saved=json.loads((STAGE/'blaze-fixtures.json').read_text())
    assert saved['Generation']==2 and saved['Mana']>0 and saved['Life']>0 and saved['TargetLife']>0
    assert saved['Aura']['Rank']>20 and len(saved['Fields'])==2
    assert len({p['Id'] for p in saved['Fields']})==2
    assert all(p['Owner']==saved['Owner'] and p['SkillRank']==saved['Aura']['Rank'] and p['BlazeModifiers'] is not None and p['Seed']>0 for p in saved['Fields'])
    focused=inventory.validate_inventory(STAGE/f'diagnostic-{BUILD}/birth-inputs.json')
    assert focused['artifacts']==tested['artifacts'] and focused['copiedData']==tested['copiedData']
    flog=(STAGE/f'diagnostic-{BUILD}/birth-native-checks.log').read_text()
    assert sum(markers(STAGE/f'diagnostic-{BUILD}/birth-native-checks.log').values())==39 and ' ERR]' not in flog and ': failed' not in flog
    assert flog.count('Shutting down')==flog.count('Writing world save snapshot done')==3
    assert 'pass across3boots/2reloads' in (STAGE/f'diagnostic-run-{BUILD}.log').read_text()
    for kind in ('content','integration'):
        text=(STAGE/f'{kind}-build-{BUILD}.log').read_text()
        assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
        name=f'{kind}/Sanctuary.{kind.title()}.dll'
        assert sha(STAGE/name)==sha(STAGE/f'focused-accepted-{BUILD}'/name)
    old=json.loads((ROOT/'.local/class-skills/sorceress-buff-payment/birth-inputs.json').read_text())
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert old['artifacts'][n]==tested['artifacts'][n]
    assert old['copiedData'].keys()==tested['copiedData'].keys()
    assert {p for p in tested['copiedData'] if old['copiedData'][p]!=tested['copiedData'][p]}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    emitter=proof('blaze-emitter-source-evidence.json',960,2)
    damage=proof('blaze-damage-source-evidence.json',23040,9)
    assert emitter.read_bytes()==historical(str(emitter.relative_to(ROOT)))
    parent=ROOT/'tools/reference/buff-payment-native-evidence.json'
    assert parent.read_bytes()==historical(str(parent.relative_to(ROOT)))
    now=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text())
    before=json.loads(historical('data/Sanctuary/Balance/class-skill-status.json'))
    new=next(s for s in now['skills'] if s['talentId']=='sorceress.blaze')
    old=next(s for s in before['skills'] if s['talentId']=='sorceress.blaze')
    assert new['verification']==old['verification']=='partial-native-coverage'
    old['remaining']=new['remaining']
    assert now==before and not now['completedClasses']
    audited=inventory.validate_inventory(STAGE/'audit-inputs.json')
    assert audited['artifacts']==tested['artifacts'] and audited['copiedData']==tested['copiedData']
    assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    assert ' ERR]' not in (STAGE/'audit-native-checks.log').read_text()
    fingerprints=[]
    for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'):
        audit=json.loads((STAGE/name).read_text())
        assert audit==json.loads((ROOT/'tools/reference'/name).read_text())
        assert audit['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
        for p,digest in audit['InputHashes'].items():assert sha(ROOT/p)==digest,p
        old=json.loads(historical('tools/reference/'+name))
        ignore=('InputHashes','ContentAssemblySha256','ContentAssemblyPath')
        assert {k:v for k,v in audit.items() if k not in ignore}=={k:v for k,v in old.items() if k not in ignore}
        fingerprints.append(len(audit['InputHashes']))
    assert fingerprints==[896,21]
    text=(STAGE/'python-tests.log').read_text()
    assert 'Ran 71 tests' in text and '\nOK\n' in text and 'FAILED' not in text
    text=(STAGE/f'checks-{BUILD}.log').read_text()
    for message in ('Named readiness: 573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.'):assert message in text
    assert 'Traceback' not in text
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts'])==106
    for p,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==digest,p
    files=[Path(__file__).resolve(),ROOT/'tests/check_blaze.py',ROOT/'tools/probe_blaze_damage.py',ROOT/'tools/probe_blaze_emitter.py',emitter,damage,parent,ROOT/'tools/build_skill_roadmap.py',
           *(ROOT/'docs'/n for n in ('sorceress-blaze-checkpoint.md','sorceress-fire-ground-preflight.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-effects.md','skill-icons.md')),
           *(STAGE/n for n in (f'pre-build-{BUILD}.json','birth-inputs.json','birth-native-checks.log',f'full-run-{BUILD}.log','blaze-fixtures.json',f'diagnostic-{BUILD}/birth-inputs.json',f'diagnostic-{BUILD}/birth-native-checks.log',f'diagnostic-run-{BUILD}.log','audit-inputs.json','audit-native-checks.log',f'audit-run-{BUILD}.log','python-tests.log',f'checks-{BUILD}.log',f'content-build-{BUILD}.log',f'integration-build-{BUILD}.log')),
           *(ROOT/'tools/reference'/n for n in ('named-item-runtime-audit.json','first-loop-affix-audit.json'))]
    result=dict(referenceVersion='3.3.93847',component='Blaze prepared cast and typed saved fields',installed=False,graphicalClientVerified=False,completedClasses=[],
        verified=dict(nativeMarkers=555,focusedMarkers=39,boots=3,reloads=2,saves=3,olderEmitterFactoryCompositions=960,olderFieldDamageCompositions=23040,nativeHpReturnComparisons=120,rankHardGoldens=765,newServerWireActions=6,retainedServerWireActions=150,fixtureFamilies=40,sharedChecks=10,auditMarkers=4,auditFingerprints=fingerprints,pythonTests=71,unchangedInstalledArtifacts=106),
        markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
        established=['Player Blaze prepares rank/duration/speed and checks weaker replacement before fractional payment;right-only admission,cancellation,insufficient mana,live release rank and committed movement pass.',
            'New optional typed fields reuse saved FireGround,Immolation point recipient,source roll/19-of-128 SOFTHIT,class32 and shared modifier/mitigation/native HP-return services. Existing rankless fields retain legacy payload/route. Held aura rank combines with live emission hard/gear;existing fields retain captured endpoints/modifiers. Live potion components can damage a fire-immune NPC. Offline aging uses no damage/RNG;field expiry and action continue independently of aura expiry.',
            'Same original emitted field IDs/positions/ranks/modifiers/seeds,caster,target,gear,wounds and mana survive two reloads and naturally damage under authenticated native connections. Fixture inspection holds preserve remaining frame budget rather than resetting lifetime. Six new wire actions retain150previous actions. Legacy explicit-clock regression records advanced seeds before the next reload.',
            'Four fresh audits,71Python/573named/240catalogue/generators preserve item decisions and106protected installed artifacts. No deployment/class certificate.'],
        remaining=['Older source compositions use explicit services,not modern binary equivalence. Outer movement cadence/two-subtile mapping,modern expression/factory/stat/seed/geometry/terrain,animation RNG correlation,owner-death/facet/town lifecycle mapping,passive avoidance/PvP/special actors,charged/oskill/client and old-DLL/unheld saves remain open. Older MDR can retain a negative intermediate/total without healing;native shared mitigation clamps0,an explicit discrepancy.',
            'Current aura,ground flame and cast sound are identified placeholders;final effects/audio and talent/hotkey icons remain separate.'])
    out=ROOT/'tools/reference/blaze-native-evidence.json'
    encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('Blaze:555combined/39focused markers,960emitter/23040damage source cases,120native returns,6new/150retained wire actions,two original field resumes;four audits/71Python;106installed hashes preserved;modern/client/assets gaps explicit.')

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--check',action='store_true')
    record(p.parse_args().check)
