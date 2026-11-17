#!/usr/bin/env python3
"""Bind the current thirty-talent Warlock/Witch integration and actual input proof."""
import argparse,hashlib,sys,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/warlock-lifecycle-acceptance'
FOCUS=ROOT/'.local/class-skills/echoing-input-retention'
sys.path.insert(0,str(ROOT/'tests'))
from check_warlock_lifecycle_acceptance import configuration

def expected():
    c=configuration();m=Counter()
    def add(n,v):
        m[n]+=v;res=c['extra_wait_results'].get(n,())
        for a in ((res,)if isinstance(res,str)else res):m[a]+=v
    add('iron-golem-birth-check',3)
    for f in ('iron-golem-items','npc-resource-callbacks',*c['extra_families']):add(f+'-check',1);add(f+'-reload-check',2)
    for n in c['extra_checks']:add(n,3)
    for n in c['extra_target_input_commands']:m[n+'-check']+=3;m[n+'-result']+=3
    for n in c['extra_pre_save_commands']:add(n,3)
    m['defiler-link-casting-save-result']+=3
    for n in (1,9):m[f'bind-learned-wire-{n}-result']+=3
    assert sum(m.values())==906
    return m

def record(check=False):
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/druid-lifecycle-acceptance/birth-inputs.json')
    snapshot(STAGE/'source-snapshot-3.json',STAGE/'birth-inputs.json')
    assert {p:d for p,d in born['artifacts'].items()if p!='Assemblies/Sanctuary.Integration.dll'}=={p:d for p,d in parent['artifacts'].items()if p!='Assemblies/Sanctuary.Integration.dll'}
    assert born['copiedData']==parent['copiedData']
    wanted='tests/Sanctuary.Integration/BindDemonLearnedChecks.cs';echo='tests/Sanctuary.Integration/EchoingStrikeInputChecks.cs'
    assert sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d)==[wanted,echo]
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d
    with tarfile.open(STAGE/'failed-native-1.tar.gz')as a:
        old=a.extractfile(wanted).read().decode();assert hashlib.sha256(old.encode()).hexdigest()==parent['sourceInputs'][wanted]
        new=old.replace('Require(!EngorgeEffects.Approved&&!BloodBoilPrepared.Approved&&!WarlockCombat.Implemented(CombatAbility.Consume)&&!WarlockCombat.Implemented(CombatAbility.BloodOath),"Bind approval enabled another pending skill");','Require(EngorgeEffects.Approved&&BloodBoilPrepared.Approved&&WarlockCombat.Implemented(CombatAbility.Consume)&&WarlockCombat.Implemented(CombatAbility.BloodOath),"Bind fixture lost the separately approved final four talents");',1).replace('four other pending skills remain gated','the final four talents retain their separate approval',1)
        assert(ROOT/wanted).read_text()==new
        prefix=str(STAGE.relative_to(ROOT))+'/'
        assert 'Bind approval enabled another pending skill'in a.extractfile(prefix+'birth-native-checks.log').read().decode()
    with tarfile.open(STAGE/'failed-native-2.tar.gz')as a:
        old=a.extractfile(echo).read().decode();assert hashlib.sha256(old.encode()).hexdigest()==parent['sourceInputs'][echo]
        current=(ROOT/echo).read_text();start=current.index('            // Retain the real captured Hex');end=current.index('            rows[index++]',start)
        assert current[:start]+current[end:]==old
        assert all(t in current[start:end]for t in ('HexBane.Restore','EchoingStrikeInputs.Capture(p)==input','SkillHealth.Balance256(p)==life','NecromancerEffects.ManaBalance256(p)==mana','Debuffs.SequenceEqual(surrounding.Debuffs)'))
        assert 'Saved source inputs/Hex/items/ranks/fractional resources'in a.extractfile(prefix+'birth-native-checks.log').read().decode()
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz')as a:
        for k in ('Content','Integration'):assert hashlib.sha256(a.extractfile(k.lower()+f'/Sanctuary.{k}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{k}.dll']
    assert all(t in(STAGE/'integration-build-2.log').read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    assert 'pass across3boots/2reloads'in(STAGE/'native-run-3.log').read_text()
    snapshot(FOCUS/'source-snapshot-1.json',FOCUS/'birth-inputs.json');assert e.load(FOCUS/'birth-inputs.json')['artifacts']==born['artifacts']
    e.cohort(FOCUS,Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'echoing-strike-inputs-check':1,'echoing-strike-inputs-reload-check':2}),12)
    log=e.cohort(STAGE,expected(),906);assert log.count('Client: 127.0.0.1: Connected.')==378
    for name,count in (('demons-acceptance',180),('weapons-acceptance',240),('chaos-acceptance',315),('final-talents',150),('direct-link',141)):
        old=Counter(e.load(ROOT/f'tools/reference/warlock-{name}-native-evidence.json')['markers']);assert sum(old.values())==count and not(old-expected()),name
    audit=e.load(ROOT/'tools/reference/warlock-current-input-coverage.json');assert(audit['activeTalents'],audit['passiveTalents'],audit['verifiedActiveInputRoutes'],audit['heldInputSequences'])==(26,4,26,6)
    assert audit['artifacts']==born['artifacts']
    for p,d in audit['files'].items():assert e.sha(ROOT/p)==d,p
    approval=e.load(ROOT/'tools/reference/warlock-final-talents-approval.json');assert approval['approved']and approval['referenceVersion']=='3.3.93847'and len(approval['approvedTalents'])==4
    named=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert named==e.load(STAGE/'hellslayer-readiness-fixtures.json')
    from build_named_readiness import validate
    validate(named,assembly=STAGE/'content/Sanctuary.Content.dll')
    warnings=[l for l in log.splitlines()if 'Warning: Attempted to delete'in l]
    unsupported=[l for l in log.splitlines()if ' WRN]'in l and 'unsupported'in l.lower()]
    c=configuration();assert tuple(len(c[k])for k in ('extra_families','extra_checks','extra_target_input_commands','extra_files','extra_pre_save_commands'))==(91,6,73,17,2)
    assert c['extra_pre_save_commands']==('cleave-cast-presave-check','consume-aura-save-check')and c['save_command']=='defiler-link-casting-save'and c['connected_save']
    ledger=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert len(ledger['skills'])==240 and not ledger['completedClasses']
    paths=['tools/record_warlock_lifecycle_acceptance.py','tools/review_warlock_input_coverage.py','tools/class_input_coverage.py','tools/reference/warlock-current-input-coverage.json','docs/warlock-lifecycle-acceptance-checkpoint.md','docs/warlock-coverage-matrix.md','docs/warlock-planned-acceptance-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skill-status-overview.md','docs/main-consolidation-plan.md','README.md','docs/skill-effects.md','docs/skill-icons.md']
    files=[*(ROOT/p for p in paths),*c['extra_inputs'],ROOT/'tests/check_echoing_input_retention.py',*e.stage_files(STAGE,FOCUS),STAGE/'accepted-assemblies-1.tar.gz',FOCUS/'accepted-assemblies-1.tar.gz',STAGE/'failed-native-1.tar.gz',STAGE/'failed-native-2.tar.gz']
    result=dict(component='Current all-thirty Warlock/Witch lifecycle/input and direct/item/native integration',referenceVersion='3.3.93847',baselineCommit='4e4a20d97571018b400ac9491a5975f7853ef5a1',nativeMarkers=906,markers=dict(sorted(expected().items())),fixtureFamilies=91,sharedChecks=6,ordinaryInputCommands=73,explicitWarlockInputCases=audit['ordinaryInputCases'],foreignInputCases=audit['foreignInputCasesSeparatelyExcluded'],heldBindSequences=6,activeTalents=26,passiveTalents=4,connections=378,boots=3,saves=3,reloads=2,artifacts=born['artifacts'],productionCodeChanged=False,runtimeDataChanged=False,contentRebuilds=0,integrationRebuilds=2,nativeRebuilds=0,sourceSnapshot=3,nativeRun=3,frozenNativeFailures=2,focusedMarkers=12,changedTestSources=[wanted,echo],saveDeletionWarnings=warnings,unsupportedWarnings=unsupported,classCertified=False,goalComplete=False,installed=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='All five earlier Warlock result inventories retain on current binaries,with26active input routes and four separate passive consumers. Bind uses actual held packets,compiled Warlock/Witch ownership,free release/locked target/nine-mana continuation and original-body saving;heartbeats do not inflate counts. Three Python preconditions and one C# Bind assertion now retain the separately approved final four rather than demanding historical pending gates. Echoing input inspection retains its original real Hex payload with an extended test deadline,exact capture/resources and foreign-state checks;normal gameplay expiry remains. No gameplay/numeric/data/native change. Existing source/event/RNG/stat/world/geometry/species/elite/Sunder/item/recipient/PvP and graphical-client limits remain explicit;effects/audio and icons stay separate. Defiler mid-windup and Consume short-aura save paths both retain their own assertions on the existing connected save.')
    e.write('warlock-lifecycle-acceptance-native-evidence.json',result,files,check)
    print('Warlock/Witch:906exact results/26active+4passive scopes/378connections/two reloads;accepted production reused.')

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
