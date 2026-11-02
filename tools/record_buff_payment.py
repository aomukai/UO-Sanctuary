#!/usr/bin/env python3
"""Bind Enchant failed-release payment with retained class/native contracts."""
import argparse,json,subprocess,sys
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/sorceress-buff-payment'
BUILD=3
PARENT='9ba6f12'
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
    assert sum(counts.values())==538 and ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert 'pass across3boots/2reloads' in (STAGE/f'full-run-{BUILD}.log').read_text()
    sys.path.insert(0,str(ROOT/'tests'))
    from check_buff_payment import configuration
    c=configuration(True);assert len(c['extra_families'])==39 and len(c['extra_checks'])==10
    for n in c['extra_families']:assert counts[n+'-check']==1 and counts[n+'-reload-check']==2,n
    for n in c['extra_checks']:assert counts[n]==3,n
    assert len(c['extra_target_input_commands'])==50
    for n in c['extra_target_input_commands']:
        assert counts[n+'-check']==counts[n+'-result']==3,n
        assert log.count(' '+n+': windup')==3,n
    for text in ('Buff payment:45native self/ally/fallback comparisons with complete generic wrapper',
                 'Energy Shield:765rank/hard goldens,25344complete-source kernel/792native absorption comparisons,15cast-list comparisons',
                 'Shiver Armor:765rank/hard goldens,7680complete-source roll/160native HP-return-cold comparisons',
                 'TK source/native:rolls=7680 deliveries=2560 deaths=864 pushes=848',
                 'Ordered monster release:1616native complete-source compositions','NPC return:60488complete-source policies/exact traces'):
        assert text in log,text
    saved=json.loads((STAGE/'buff-payment-fixtures.json').read_text())
    assert saved['Generation']==2 and saved['Refusals']==6 and saved['Self']['Rank']>20 and saved['Other']['Rank']==saved['Self']['Rank']
    assert saved['Self']['Caster']==saved['Other']['Caster']==saved['Owner'] and saved['Mana']>0 and saved['Life']>0 and saved['AllyLife']>0
    focused=json.loads((STAGE/'diagnostic-2/birth-inputs.json').read_text());flog=(STAGE/'diagnostic-2/birth-native-checks.log').read_text()
    assert focused['artifacts']==tested['artifacts'] and focused['copiedData']==tested['copiedData']
    assert sum(markers(STAGE/'diagnostic-2/birth-native-checks.log').values())==67 and ' ERR]' not in flog and ': failed' not in flog
    assert flog.count('Shutting down')==flog.count('Writing world save snapshot done')==3
    assert 'pass across3boots/2reloads' in (STAGE/'diagnostic-run-2.log').read_text()
    for p,d in focused['sourceInputs'].items():assert sha(ROOT/p)==d,p
    for kind in ('content','integration'):
        text=(STAGE/f'{kind}-build-{BUILD}.log').read_text();assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
        n=f'{kind}/Sanctuary.{"Content" if kind=="content" else "Integration"}.dll';assert sha(STAGE/n)==sha(STAGE/'focused-accepted-2'/n)
    old=json.loads((ROOT/'.local/class-skills/sorceress-energy-shield/birth-inputs.json').read_text())
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert old['artifacts'][n]==tested['artifacts'][n]
    assert old['copiedData'].keys()==tested['copiedData'].keys()
    assert {p for p in tested['copiedData'] if old['copiedData'][p]!=tested['copiedData'][p]}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    proof_path=ROOT/'tools/reference/buff-payment-source-evidence.json';source_proof(proof_path,60,5)
    assert proof_path.read_bytes()==historical(str(proof_path.relative_to(ROOT)))
    blaze=ROOT/'tools/reference/blaze-emitter-source-evidence.json';source_proof(blaze,960,2)
    parent=ROOT/'tools/reference/energy-shield-native-evidence.json';assert parent.read_bytes()==historical(str(parent.relative_to(ROOT)))
    now=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());before=json.loads(historical('data/Sanctuary/Balance/class-skill-status.json'))
    new=next(s for s in now['skills'] if s['talentId']=='sorceress.enchant');old=next(s for s in before['skills'] if s['talentId']=='sorceress.enchant')
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
    files=[Path(__file__).resolve(),ROOT/'tests/check_buff_payment.py',ROOT/'tools/probe_buff_payment.py',ROOT/'tools/probe_blaze_emitter.py',proof_path,blaze,parent,ROOT/'tools/build_skill_roadmap.py',
           *(ROOT/'docs'/n for n in ('sorceress-buff-payment-checkpoint.md','sorceress-buff-payment-preflight.md','sorceress-fire-ground-preflight.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-effects.md','skill-icons.md')),
           *(STAGE/n for n in (f'pre-build-{BUILD}.json','birth-inputs.json','birth-native-checks.log',f'full-run-{BUILD}.log','buff-payment-fixtures.json','diagnostic-2/birth-inputs.json','diagnostic-2/birth-native-checks.log','diagnostic-run-2.log','audit-inputs.json','audit-native-checks.log',f'audit-run-{BUILD}.log','python-tests.log',f'checks-{BUILD}.log',f'content-build-{BUILD}.log',f'integration-build-{BUILD}.log')),
           *(ROOT/'tools/reference'/n for n in ('named-item-runtime-audit.json','first-loop-affix-audit.json'))]
    result=dict(referenceVersion='3.3.93847',component='Enchant failed-release payment',installed=False,graphicalClientVerified=False,completedClasses=[],
        verified=dict(nativeMarkers=538,focusedMarkers=67,boots=3,reloads=2,saves=3,olderGenericBuffPaymentCompositions=60,blazePreparationCompositions=960,nativeRecipientPaymentComparisons=45,sameOriginalSavedRefusals=6,newServerWireActions=18,retainedServerWireActions=132,fixtureFamilies=39,sharedChecks=10,auditMarkers=4,auditFingerprints=fingerprints,pythonTests=71,unchangedInstalledArtifacts=106),
        markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
        established=['The retained complete older generic handler/do23/do25/list/passive composition qualifies failed-weaker-callback refusal before payment-service invocation. Formula/stat/list/used-skill/room/events/animation/payment-call services remain explicit;not modern binary or full outer player-mode proof.',
            'Player Enchant resolves its real self/ally/hostile fallback once and checks the existing rank before payment,reusing the same apply helper.45native recipient/rank/payment comparisons preserve weaker payload/deadline/caster/mana,equal original-caster refresh and stronger paid replacement. Real gear removal,live release rank/hard changes,cancellation,insufficient mana and recovery retention pass.',
            'Separate next-skill Blaze preparation composes960older emitter/factory contexts with a supplied evaluated lifetime;outer movement/cadence/modern expression/damage/field behavior remains unproved and no Blaze runtime change is claimed.',
            '18new failed right/quick actions retain132previous successful inputs. Actual original caster/ally/hostile/orb,wounds,mana and naturally active higher buffs survive two reloads;6new refusals spend no mana or reset payloads/resources. Focused/final DLL hashes match with only Enchant remaining metadata changed. Four fresh audits,71Python/573named/240catalogue/generators preserve item decisions and106installed hashes.'],
        remaining=['Modern failed-cast presentation/recovery,outer player-mode/mana-helper,arbitrary NPC/isatt/alignment/item/state-allocation and source/weapon-event/PvP/charged/oskill/client contexts remain open. This fixes the payment mismatch without certifying every Enchant or class path.',
            'Existing Enchant effects/audio and tree/hotkey icons remain separate identified placeholders;no new effect/icon/action is introduced. No class certificate or deployment.'])
    out=ROOT/'tools/reference/buff-payment-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('Enchant payment:538combined/67focused markers,60source/45native recipient comparisons,18new/132retained wire actions,6original saved refusals;four audits/71Python;106installed hashes preserved;modern/client/assets gaps explicit.')

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
