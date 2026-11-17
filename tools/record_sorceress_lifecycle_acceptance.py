#!/usr/bin/env python3
"""Bind current Sorceress mechanics,ordinary/held input and saved lifecycles."""
import argparse,hashlib,json,subprocess,sys,tarfile
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/sorceress-lifecycle-acceptance';BASE='bf148267222036d589223680a262a8e8818f71b2'
sys.path.insert(0,str(ROOT/'tests'))
from check_sorceress_lifecycle_acceptance import configuration
def expected():
    m=Counter(e.load(ROOT/'tools/reference/sorceress-spell-link-native-evidence.json')['markers']);assert sum(m.values())==759
    for f in ('explosive-terrain','unresisted-shields','player-melee-return','cold-corpse-proposal','corpse-shatter-guard'):m[f+'-check']+=1;m[f+'-reload-check']+=2
    m['corpse-shatter-guard-result']+=3
    for spell in ('fireball','glacial'):
        for mode in ('right','quick'):
            n='explosive-terrain-'+spell+'-'+mode;m[n+'-check']+=3;m[n+'-result']+=3
    assert sum(m.values())==801
    return m
def prior(path):return subprocess.check_output(['git','show',BASE+':'+path],cwd=ROOT)
def record(check=False):
    assert 'pass across3boots/2reloads'in (STAGE/'native-run-1.log').read_text()
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/cold-corpse-rule-adoption/birth-inputs.json');old=e.load(ROOT/'.local/class-skills/sorceress-spell-link/birth-inputs.json')
    snapshot(STAGE/'source-snapshot-1.json',STAGE/'birth-inputs.json')
    assert born['artifacts']==parent['artifacts']and born['copiedData']==parent['copiedData']
    current_cs={p:d for p,d in born['sourceInputs'].items()if p.endswith('.cs')};assert current_cs=={p:d for p,d in parent['sourceInputs'].items()if p.endswith('.cs')}
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]
    for kind in ('Content','Integration'):assert e.sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as t:
        for kind in ('Content','Integration'):assert hashlib.sha256(t.extractfile(kind.lower()+f'/Sanctuary.{kind}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    log=e.cohort(STAGE,expected(),801);assert log.count('Client: 127.0.0.1: Connected.')==314
    warnings=[l for l in log.splitlines()if 'Warning: Attempted to delete'in l];assert len(warnings)==14 and all('"WardrobeSet"'in l for l in warnings)
    unsupported=[l for l in log.splitlines()if ' WRN]'in l and 'unsupported'in l.lower()]
    scalar=[l for l in unsupported if 'Defiler link sharing refused unsupported context'in l];maker=[l for l in unsupported if 'Fire Wall maker zero-damage'in l];orb=[l for l in unsupported if 'Root zero-packet event subscribers'in l];shiver=[l for l in unsupported if 'before late reaction'in l]
    assert tuple(map(len,(scalar,maker,orb,shiver)))==(12,3,1,1)and len(unsupported)==17
    scroll=[l for l in log.splitlines()if ' WRN]'in l and 'Amount changed for non-stackable item'in l];assert len(scroll)==1 and 'TeleportScroll'in scroll[0]
    for name in ('inferno-held-input','hydra-world','player-teleport','unresisted-shields','cold-corpse-proposal'):assert e.load(STAGE/(name+'-fixtures.json'))['Generation']==2
    held=e.load(STAGE/'inferno-held-input-fixtures.json');assert held['HeldAtFirstSave']and held['UnresumedSaves']==2
    terrain=e.load(STAGE/'explosive-terrain-fixtures.json');assert len(terrain)==2 and all(r['Generation']==2 for r in terrain)
    audit=e.load(ROOT/'tools/reference/sorceress-current-input-coverage.json');assert (audit['verifiedActiveInputRoutes'],audit['activeTalents'],audit['passiveTalents'],audit['ordinaryInputCases'],audit['heldInputSequences'],audit['negativeInputCasesSeparatelyExcluded'],audit['foreignInputCasesSeparatelyExcluded'])==(26,26,4,216,9,24,0)
    assert audit['artifacts']==born['artifacts']
    for p,d in audit['files'].items():assert e.sha(ROOT/p)==d,p
    named=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert named==e.load(STAGE/'hellslayer-readiness-fixtures.json')and named['Definitions']==json.loads(prior('tools/reference/named-item-runtime-audit.json'))['Definitions']
    from build_named_readiness import validate
    validate(named,assembly=STAGE/'content/Sanctuary.Content.dll')
    status='data/Sanctuary/Balance/class-skill-status.json';assert (ROOT/status).read_bytes()==prior(status);ledger=e.load(ROOT/status);rows=[r for r in ledger['skills']if r['characterClass']=='Sorceress'];assert len(rows)==30 and all(r['implementation']=='handler-present'for r in rows)and len(ledger['skills'])==240 and not ledger['completedClasses']
    changed=sorted(p for p,d in current_cs.items()if old['sourceInputs'].get(p)!=d)
    paths=['tools/record_sorceress_lifecycle_acceptance.py','tools/class_input_coverage.py','tools/review_sorceress_input_coverage.py','tools/reference/sorceress-current-input-coverage.json','docs/sorceress-lifecycle-acceptance-checkpoint.md','docs/sorceress-coverage-matrix.md','docs/class-skill-roadmap.md','docs/skill-effects.md','docs/skill-icons.md','tools/reference/named-item-runtime-audit.json',status]
    files=[*(ROOT/p for p in paths),*configuration()['extra_inputs'],*e.stage_files(STAGE),STAGE/'accepted-assemblies-1.tar.gz']
    result=dict(component='Current combined Sorceress lifecycle and ordinary/held input acceptance',referenceVersion='3.3.93847',baselineCommit=BASE,contentReused='cold-corpse-rule-adoption Content1',integrationReused='cold-corpse-rule-adoption Integration1',nativeRun=1,nativeMarkers=801,markers=dict(sorted(expected().items())),retainedPredecessorMarkers=759,fixtureFamilies=64,sharedChecks=11,declaredLiveActions=27,ordinaryInputCases=216,negativeOrdinaryControlsSeparatelyExcluded=24,heldInputSequences=9,totalWireCases=225,verifiedActiveTalents=26,passiveTalentsWithSeparateStatRecipientChecks=4,connections=314,boots=3,saves=3,reloads=2,artifacts=born['artifacts'],gameplayChangedDuringRequalification=False,runtimeDataChangedDuringRequalification=False,rebuilds=0,correctedNativeFailures=0,correctedPostRunAuditFailures=0,knownWardrobeSaveWarnings=14,intentionalScalarRefusals=12,knownMakerEventRefusals=3,knownOrbRootEventRefusals=1,intentionalDisabledMeleeHookWarning=1,syntheticTeleportScrollAmountWarnings=1,changedSourcesSincePreviousSorceress=changed,changedCopiedDataSincePreviousSorceress=sorted(p for p,d in born['copiedData'].items()if old['copiedData'].get(p)!=d),installed=False,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),archiveRoot='/home/aomukai/Sanctuary-checkpoint-archives/revive-respec/sorceress-lifecycle-acceptance',limits='All759predecessor markers retain;current adopted terrain,cold corpses,native armor-ignore shields and lethal ordinary-melee return add42results. Actual216ordinary input cases include24refusal controls excluded from positive route accounting;9real held Inferno sequences establish press/heartbeat/release/natural damage and nonreplay. All26active talents covered;4passives retain separate stat/recipient checks. Original spells,Hydra actors/bolts,held flames,resources,gear,bindings,unpaid Teleport and corpse/shield/terrain records retain declared reload assertions. Existing warnings are classified explicitly;unknown root/maker event subscribers,drain/special/PvP/mixedshield/world services,modern geometry/global RNG/event/NHD and unheld/oldbinary/client parity remain flagged. No source compiler,reference-version substitution,asset generation,deployment or class certificate;installed build/saves protected.')
    e.write('sorceress-lifecycle-acceptance-native-evidence.json',result,files,check);print('Sorceress:801exactresults/216ordinary+9held inputs/26active routes/2reloads;no rebuild or gameplay edit.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
