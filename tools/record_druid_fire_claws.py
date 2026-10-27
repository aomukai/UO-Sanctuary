#!/usr/bin/env python3
"""Bind Fire Claws packet ordering, typed damage, real inputs and reload evidence."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/druid-fire-claws'
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()

def record(check=False,full_class=False):
    inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json')
    names=['stats-inputs.json','stats-native-checks.log','fire-claws-run.log','python-tests.log','druid-fire-claws-fixtures.json','druid-feral-fixtures.json','druid-maul-fixtures.json',
        'druid-form-reaction-fixtures.json','druid-form-interruption-fixtures.json','druid-form-casting-fixtures.json','druid-form-world-fixtures.json','druid-form-expired-fixtures.json',
        'attempt1-stats-inputs.json','attempt1-stats-native-checks.log','attempt1-fire-claws-run.log','attempt1-named-item-runtime-audit.json','attempt1-first-loop-affix-audit.json','attempt2-integration-build.log']
    artifacts=[STAGE/name for name in names]
    for kind in ['content','integration']:
        path=STAGE/(kind+'-build.log');text=path.read_text();assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
        artifacts += [path,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
    log=(STAGE/'stats-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==156
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert 'pass3boots/2actual reloads' in (STAGE/'fire-claws-run.log').read_text()
    for name in ['druid-fire-claws-scalars-check','druid-fire-claws-check','druid-feral-check','druid-feral-live-result','druid-maul-check','druid-maul-live-result',
        'druid-charge-model-check','druid-form-reaction-check','druid-form-interruption-check','druid-form-casting-check','shape-restriction-check','druid-form-attack-check']:
        assert log.count(name+': passed')==1,name
    for name in ['druid-fire-claws-reload-check','druid-fire-claws-snapshot-check','druid-feral-reload-check','druid-maul-reload-check','druid-maul-expiry-stage-check']:
        assert log.count(name+': passed')==2,name
    assert '189unique native mastered endpoint/hard-synergy/mana/AR comparisons' in log and '1120native gain/cap/current-rank/expiry/channel comparisons' in log
    assert log.count('physical/fire split,real critical-before-fire order')==2 and 'miss/block,movement/range/weapon cancellation' in log
    for skill in ['fire-claws','feral','maul']:
        for kind in ['right','quick']:
            assert log.count(skill+'-'+kind+'-result: passed')==log.count(skill+'-'+kind+': windup')==3
    saved=json.loads((STAGE/'druid-fire-claws-fixtures.json').read_text());charge=saved['Charge']
    assert saved['Form']['Form']['Ability']==162 and saved['Form']['Form']['LycanthropyRank']==20
    assert charge['Ability']==166 and charge['Count']==1 and charge['LifeDrainMinimum']==charge['LifeDrainMaximum']==4 and charge['DamagePercent']==0
    parent_raw,parent=frozen_receipt('5db6bc5','tools/reference/druid-feral-native-evidence.json')
    assert parent['verified']['passMarkers']==138 and parent['verified']['fullClassMarkers']==1184
    source=ROOT/'tools/reference/druid-fire-claws-engine-evidence.json';source_data=json.loads(source.read_text());assert source_data['contexts']==45360 and len(source_data['completeRoutineHashes'])==13
    artifacts += [source]+[ROOT/'.local/class-skills'/('druid-fire-claws'+suffix) for suffix in ['-source-probe.cpp','-source-probe.json','-compiler.log','-sanitizer.log']]
    audits=[]
    for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
        path=STAGE/name;audit=json.loads(path.read_text());assert audit==json.loads((ROOT/'tools/reference'/name).read_text())
        assert audit['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
        for name,digest in audit['InputHashes'].items():assert sha(ROOT/name)==digest,name
        audits.append(audit);artifacts.append(path)
    baseline=json.loads(subprocess.check_output(['git','show','5db6bc5:tools/reference/named-item-runtime-audit.json'],cwd=ROOT))
    assert len(audits[0]['Definitions'])==573 and audits[0]['Definitions']==baseline['Definitions']
    python=(STAGE/'python-tests.log').read_text();assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    full_markers=full_saves=None
    if full_class:
        complete=inventory.validate_inventory(STAGE/'class-inputs.json');assert complete['artifacts']==inputs['artifacts'] and complete['copiedData']==inputs['copiedData']
        text=(STAGE/'class-native-checks.log').read_text();assert ': failed' not in text and ' ERR]' not in text and text.count('Shutting down')==3
        assert 'including two save/reload cycles.' in (STAGE/'full-run.log').read_text()
        full_markers=text.count(': passed');full_saves=text.count('Writing world save snapshot done');assert full_markers==1184 and full_saves==35
        for name,expected in [('full-named-item-runtime-audit.json',audits[0]),('full-first-loop-affix-audit.json',audits[1])]:
            path=STAGE/name;assert json.loads(path.read_text())==expected;artifacts.append(path)
        artifacts += [STAGE/name for name in ['class-inputs.json','class-native-checks.log','full-run.log']]
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());handlers=[x for x in status['skills'] if x['implementation']=='handler-present']
    assert len(handlers)==178 and sum(x['characterClass']=='Druid' for x in handlers)==22 and status['completedClasses']==[]
    artifacts += [ROOT/name for name in ['tools/record_druid_fire_claws.py','tests/check_druid_fire_claws.py','tests/check_creeper_stats.py','data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json']]
    result=dict(referenceVersion='3.3.93847',installed=False,handlers=178,druidHandlers=22,completedClasses=[],goalComplete=False,currentExecutableParity=False,
        verified=dict(passMarkers=156,boots=3,actualReloads=2,sourceContexts=45360,sourceRoutines=13,nativeUniqueEndpointComparisons=189,retainedChargeComparisons=1120,
            actualFireClawsInputs=6,retainedFeralMaulInputs=12,retainedFormInputs=15,pythonTests=70,inputFingerprints=[len(x['InputHashes']) for x in audits],unchangedNamedDefinitions=573,
            fullClassMarkers=full_markers,fullClassBoots=3 if full_class else None,fullClassReloads=2 if full_class else None,fullClassSaveSnapshots=full_saves),
        frozenPriorAcceptance=dict(commit='5db6bc5',receipt='tools/reference/druid-feral-native-evidence.json',sha256=hashlib.sha256(parent_raw).hexdigest(),passMarkers=138,fullClassMarkers=1184),
        inputInventory=inputs,artifacts={str(path.relative_to(ROOT)):sha(path) for path in artifacts},
        established=[
            'Learned wolf/bear Fire Claws239uses shared normalized A1/melee-only hands/range and source start58/do2with4mana. Full weapon damage/physical critical is captured before the added mastered skill fire; an optional factory preserves that ordering before shared mitigation. It does not refill weapon damage or enhance skill fire twice. Existing callers retain their original packet path.',
            '189unique native endpoint/hard-synergy/mastery/mana/AR cases agree with the bounded45360case/13routine source fixture. Current synergy uses hard Firestorm and Molten Boulder only,22percent each. Actual Tarnhelm/Highlord soft ranks raise the skill itself without raising that synergy. Actual Highlord critical doubles physical damage while leaving skill fire unchanged; the RNG trace is inspected during preparation,not after unrelated impact procs.',
            'Both forms retain old Maul damage/stun and Feral physical leech without adding/consuming/refreshing their states. Physical immunity preserves fire and gives no physical leech; fire immunity preserves ordinary weapon damage. Miss/block spend once without skill-fire damage; Stop,out/back movement,lost range,weapon change and unshift cancel the pending packet. Low-mana ordinary Attack follows the table/older-dispatcher adapter without4mana debit.',
            'Six actual wolf-right/bear-quick casts across three boots/two reloads pay once and deliver the prepared hit. Original form/count1Feral state,fractional health/mana,target injury and Fire Claws right/quick bindings survive reloads without replaying a packet. Twelve retained Feral/Maul and15form/interruption inputs,138previous focused checks and all70Python checks remain passing. Main installed build/configuration/client/saves remain preserved.',
            'The first run reached the third boot but its input fixture timed out after a legitimate RNG miss: the synchronous Controlled wrapper had restored the hook before the asynchronous input. Fire Claws and the retained Feral fixture now keep input setup outside that wrapper and restore their own hook after completion. Production accuracy was not changed. Added direct block/movement/range/weapon checks also pass. A shadowed test local caused a compile failure and was renamed; retained failed logs bind these corrections.',
        ],remaining=[
            'Exact modern target-loss/low-mana,reentrant effects,seed/event/geometry/reentry/current form clocks and client acceptance remain unverified. The source probe deliberately substitutes combat/critical/mitigation/health/Fog/seed services and excludes old high-rank signed overflow; do not infer full executable parity. Item-only/foreign-class/PvP/unmapped recipients remain unsupported. Ordinary low-mana fallback against shielded NPCs still needs dedicated common-Attack acceptance.',
            'Actual native fire-mastery gear verification remains deferred by the existing Ormus Effect:skill-rand item gate; integer mastered endpoints are verified without bypassing it. Remaining Druid skills,earlier class gaps and Warlock remain on the roadmap. Final fire_hit,both player-form A1/body/contact/cast audio and separate talent/left/right/quick icons remain pending; human Wrestle,sound0x238,contact0x374A/hue0x489 are identified placeholders.',
        ])
    path=ROOT/'tools/reference/druid-fire-claws-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print(f'Fire Claws:156focused/3boots/2reloads/6real inputs,189native endpoints,retainedFeral/Maul/forms,70Python;fullClass={full_markers};178handlers/Druid22,not installed.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');p.add_argument('--full-class',action='store_true');args=p.parse_args();record(args.check,args.full_class)
