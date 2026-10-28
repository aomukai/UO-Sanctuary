#!/usr/bin/env python3
"""Bind prepared Miasma suppliers without claiming an implemented learned skill."""
import argparse,hashlib,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/miasma-packets'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
    inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json')
    names=['stats-inputs.json','stats-native-checks.log','run.log','python-tests.log','miasma-packet-fixtures.json','levitation-fixtures.json','barbarian-masteries.json',
        'warlock-foundation-fixtures.json','arctic-mode-fixture.json','arctic-stream-fixtures.json','recycler-casting-fixtures.json',
        'recycler-world-fixtures.json','recycler-body-fixtures.json','recycler-recovery-fixtures.json','body-fixtures.json',
        'world-fixtures.json','ground-fixtures.json','poison-fixtures.json','casting-fixtures.json','wolf-world-fixtures.json']
    artifacts=[STAGE/n for n in names]
    for kind in ['content','integration']:
        p=STAGE/(kind+'-build.log');assert all(s in p.read_text() for s in ['Build succeeded.','0 Warning(s)','0 Error(s)']);artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
    log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==114
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    for n in ['miasma-packets','levitation','barbarian-masteries','warlock-foundation']:
        assert log.count(n+'-check: passed')==1 and log.count(n+'-reload-check: passed')==2,n
    assert log.count('Miasma packets:9180full-source comparisons, magic-only distinct synergy rounding, own parameters, entropy thresholds/cap and fractional mana pass; learned cast gated')==3
    assert log.count('levitation-validation-check: passed')==log.count('levitation-validation-result: passed')==3
    for n in ['arctic-mode-live','arctic-stream-live','recycler-recovery-live','recycler-world-live','recycler-casting-live']:assert log.count(n+'-result: passed')==3,n
    saved=json.loads((STAGE/'miasma-packet-fixtures.json').read_text());assert len(saved)==2 and {x['Female'] for x in saved}=={True,False}
    assert all(x['Damage']['CloudFrames']==60 and x['Damage']['CloudRadius']==2 for x in saved)
    proof=ROOT/'tools/reference/miasma-packets-source-evidence.json';source=json.loads(proof.read_text());assert source['cases']==9180 and source['handlerEnabled']==False;artifacts.append(proof)
    for group in ['inputs','artifacts']:
        for name,digest in source[group].items():assert sha(ROOT/name)==digest,name;artifacts.append(ROOT/name)
    notes=ROOT/'tools/reference/miasma-reference-notes.json';reference=json.loads(notes.read_text());cache=ROOT/reference['primaryReleasedPatch']['cache'];assert sha(cache)==reference['primaryReleasedPatch']['sha256'];artifacts += [notes,cache]
    frozen_receipt('97e3140','tools/reference/levitation-mastery-native-evidence.json')
    audits=[]
    for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
        p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text());assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
        for name,digest in a['InputHashes'].items():assert sha(ROOT/name)==digest,name
        audits.append(a);artifacts.append(p)
    old=json.loads(subprocess.check_output(['git','show','97e3140:tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audits[0]['Definitions'])==573 and audits[0]['Definitions']==old['Definitions']
    assert [len(a['InputHashes']) for a in audits]==[690,21]
    py=(STAGE/'python-tests.log').read_text();assert 'Ran 70 tests' in py and '\nOK\n' in py and 'FAILED' not in py
    artifacts += [ROOT/n for n in ['tests/check_miasma_packets.py','tools/record_miasma_packets.py','tools/probe_miasma_packets.py',
        'tools/build_class_skills.py','tools/build_skill_roadmap.py','data/Sanctuary/Balance/class-skill-status.json','data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json']]
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());handlers=[s for s in status['skills'] if s['implementation']=='handler-present'];assert len(handlers)==183 and status['completedClasses']==[]
    warlock=[s for s in handlers if s['characterClass']=='Warlock'];assert len(warlock)==1 and warlock[0]['talentId']=='warlock.levitationmastery'
    miasma=next(s for s in status['skills'] if s['talentId']=='warlock.miasmabolt');assert miasma['implementation']=='blocked' and miasma['verification']=='partial-native-coverage'
    result=dict(referenceVersion='3.3.93847',component='Prepared Miasma Bolt magic packet suppliers',installed=False,handlerEnabled=False,currentExecutableParity=False,goalComplete=False,
        handlers=183,warlockHandlers=1,completedClasses=[],verified=dict(passMarkers=114,retainedMarkers=111,worldBoots=3,actualWorldReloads=2,
            fullSourcePacketComparisonsPerBoot=9180,savedWarlockWitchPlayers=2,pythonTests=70,unchangedNamedDefinitions=573,inputFingerprints=[690,21]),
        inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in artifacts},frozenPredecessor=dict(commit='97e3140',receipt='tools/reference/levitation-mastery-native-evidence.json'),
        established=['Miasma Bolt and its cloud deal magic, not poison. Four current missile rows are now projected without rewriting their formulas or treating the unused dot row as the actual cloud. Prepared consumers have no learned cast/binding route and take no mana.',
            'Full generic legacy skill/missile getters, creator and stat writer execute with current Miasma rows under clean ASan/UBSan. Actual C# suppliers match 9,180 cases per boot across all 255 ranks, separate hard Chain/Abyss points and noisy owner/origin stats. No weapon/item/physical/other-element/critical contribution leaks into either packet.',
            'The bolt applies its 10% per-hard-point synergy after HitShift7. The cloud applies its 20% per-hard-point synergy before HitShift8, preserving its different integer rounding. MissileValues keeps missile-local parameters separate from explicit skill references and delegates captured hard/effective skill lookups through the existing formula context.',
            'Enhanced Entropy effective ranks supply cloud duration 50+min(5*rank,100) frames and radius thresholds 2/3/4 at ranks 0/5/10. Fractional mana is 2+0.5*(rank-1). Numeric rank1/rank20 endpoints match the released patch values. Current Warlock/Witch hard points, a real Tarnhelm, capture immutability and JSON packet values are checked; saved players/gear/ranks reproduce the payload across both real reloads.',
            'All retained Levitation, Barbarian mastery, formula and prepared Druid checks pass again. Item audits preserve all 573 eligibility decisions. No main runtime or save format is changed.'],
        remaining=['Learned Miasma Bolt remains individually gated by the new hit41/cloud-do41 callbacks: actual projectile-to-cloud creation, cloud layer geometry, first/last contact timing, shared hit-delay identity and world allocation/ownership/lifecycle integration need work. Existing source comments/metadata do not prove those bodies.',
            'Magic mastery/pierce behavior and modern capture/roll timing are unverified. The owner supplier explicitly rejects nonzero magic mastery; its dedicated nonzero-gear branch still needs an eligible item fixture. Packet tests do not certify contact mitigation or modern executable rounding.',
            'Released patch notes require no double tick or extra expiry damage. Chain-specific NHD notes must not be silently generalized to Bolt. The optional maximum-range-expiry observation is pending and nonblocking.',
            'Final cast/bolt/impact/cloud/expiry/audio effects and talent/left/right/quick icons remain separately inventoried. Main4c5d3a0 build, original saves/configuration/client and frozen prior stages remain preserved; no complete-class, full regression, installation or goal completion is claimed.'])
    p=ROOT/'tools/reference/miasma-packets-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert p.read_text()==text
    else:p.write_text(text)
    print('Miasma suppliers:114 native checks/3 boots/2 reloads,70 Python;183 handlers unchanged, learned Miasma gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
