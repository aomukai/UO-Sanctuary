#!/usr/bin/env python3
"""Bind the current thirty-talent Warlock/Witch integration and actual input proof."""
import argparse,hashlib,sys,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/warlock-lifecycle-acceptance'
FOCUS=ROOT/'.local/class-skills/echoing-input-retention'
SIGIL=ROOT/'.local/class-skills/sigil-actor-lifecycle'
CONSUME=ROOT/'.local/class-skills/consume-aura-lifecycle'
LETH=ROOT/'.local/class-skills/sigil-lethargy-lifecycle'
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
    snapshot(STAGE/'source-snapshot-9.json',STAGE/'birth-inputs.json')
    assert {p:d for p,d in born['artifacts'].items()if p!='Assemblies/Sanctuary.Integration.dll'}=={p:d for p,d in parent['artifacts'].items()if p!='Assemblies/Sanctuary.Integration.dll'}
    assert born['copiedData']==parent['copiedData']
    wanted='tests/Sanctuary.Integration/BindDemonLearnedChecks.cs';echo='tests/Sanctuary.Integration/EchoingStrikeInputChecks.cs';apoc='tests/Sanctuary.Integration/ApocalypseImpactChecks.cs'
    sigils=sorted('tests/Sanctuary.Integration/'+n+'Checks.cs'for n in ('SigilActor','SigilFamily','RancorPulse','SigilDeath','SigilLethargy'))
    consume='tests/Sanctuary.Integration/ConsumeAuraChecks.cs'
    changed=sorted([apoc,wanted,echo,consume,*sigils])
    assert sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d)==changed
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
    focused=e.load(FOCUS/'birth-inputs.json');saved=e.load(FOCUS/'source-snapshot-1.json')['inputs']
    assert saved['sourceInputs']=={p:d for p,d in focused['sourceInputs'].items()if p.endswith('.cs')}and saved['copiedData']==focused['copiedData']==born['copiedData']
    with tarfile.open(STAGE/'failed-native-3.tar.gz')as a:
        old=a.extractfile(apoc).read().decode();assert hashlib.sha256(old.encode()).hexdigest()==parent['sourceInputs'][apoc]
        assert(ROOT/apoc).read_text()==old.replace('var curse=new NecromancerCurse(CombatAbility.Weaken,owner.Serial.Value,1,TimedEffects.Now.AddMinutes(3),target.X,target.Y);','var curse=new NecromancerCurse(CombatAbility.Weaken,owner.Serial.Value,1,TimedEffects.Now.Add(FixtureSpace.Retention),target.X,target.Y);',1)
        assert e.load(STAGE/'failed-native-3-native-inputs.json')==focused['artifacts']
        for p,d in focused['sourceInputs'].items():
            if p.endswith('.cs')or p.startswith('tests/'):
                assert hashlib.sha256(a.extractfile(p).read()).hexdigest()==d,p
            else:assert e.sha(ROOT/p)==d,p
    # This focused pass ran before the unrelated Apocalypse fixture edit.
    # Bind its original I2 DLLs/source rather than relabeling it current I3.
    with tarfile.open(FOCUS/'accepted-assemblies-1.tar.gz')as a:
        for k in ('Content','Integration'):assert hashlib.sha256(a.extractfile(k.lower()+f'/Sanctuary.{k}.dll').read()).hexdigest()==focused['artifacts'][f'Assemblies/Sanctuary.{k}.dll']
    assert{p:d for p,d in focused['artifacts'].items()if p!='Assemblies/Sanctuary.Integration.dll'}=={p:d for p,d in born['artifacts'].items()if p!='Assemblies/Sanctuary.Integration.dll'}
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz')as a:
        for k in ('Content','Integration'):assert hashlib.sha256(a.extractfile(k.lower()+f'/Sanctuary.{k}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{k}.dll']
    assert all(t in(STAGE/'integration-build-9.log').read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    assert 'pass across3boots/2reloads'in(STAGE/'native-run-7.log').read_text()
    e.cohort(FOCUS,Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'echoing-strike-inputs-check':1,'echoing-strike-inputs-reload-check':2}),12)
    sigilBorn=e.load(SIGIL/'birth-inputs.json');sigilSource=e.load(SIGIL/'source-snapshot-2.json')['inputs']
    assert sigilSource['sourceInputs']=={p:d for p,d in sigilBorn['sourceInputs'].items()if p.endswith('.cs')}
    assert sigilSource['copiedData']==sigilBorn['copiedData']==born['copiedData']
    with tarfile.open(STAGE/'failed-native-5.tar.gz')as a:
        assert e.load(STAGE/'failed-native-5-native-inputs.json')==sigilBorn['artifacts']
        for p,d in sigilBorn['sourceInputs'].items():
            if p.endswith('.cs')or p.startswith('tests/'):assert hashlib.sha256(a.extractfile(p).read()).hexdigest()==d,p
            else:assert e.sha(ROOT/p)==d,p
        old=a.extractfile('tests/Sanctuary.Integration/SigilLethargyChecks.cs').read().decode()
        assert 'Original held recipient/creator/rank/expiry or marker lost on reload'in a.extractfile(prefix+'birth-native-checks.log').read().decode()
        new=old.replace('        Require(saved.Generation<2&&p!=null&&target!=null&&SigilLethargy.State(target)==saved.Status','        bool expired=SigilActorChecks.ExpiredOriginal(saved.Actor);\n        Require(saved.Generation<2&&p!=null&&target!=null&&SigilLethargy.State(target)==saved.Status',1).replace('&&SigilLethargy.Defense(target)==-232&&World.FindItem((Serial)saved.Actor.Marker)is{Deleted:false},','&&SigilLethargy.Defense(target)==-232&&(expired||World.FindItem((Serial)saved.Actor.Marker)is{Deleted:false}),',1)
        assert(ROOT/'tests/Sanctuary.Integration/SigilLethargyChecks.cs').read_text()==new
    positive=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})
    for name in ('sigil-actor','sigil-family','rancor-pulse','sigil-death'):
        positive[name+'-check']+=1;positive[name+'-result']+=1
        positive[name+'-reload-check']+=2;positive[name+'-reload-check-result']+=2
    # Result labels include '-check-result', including the first generation.
    for name in ('sigil-actor','sigil-family','rancor-pulse','sigil-death'):
        positive[name+'-check-result']=positive.pop(name+'-result')
    positiveLog=e.cohort(SIGIL,positive,33)
    assert 'Sigil expiry:original'not in positiveLog,'Focused run must prove active original marker resumes'
    assert 'pass across3boots/2reloads'in(SIGIL/'native-run-2.log').read_text()
    with tarfile.open(SIGIL/'accepted-assemblies-1.tar.gz')as a:
        assert hashlib.sha256(a.extractfile('integration/Sanctuary.Integration.dll').read()).hexdigest()==sigilBorn['artifacts']['Assemblies/Sanctuary.Integration.dll']
    lethBorn=e.load(LETH/'birth-inputs.json');lethSaved=e.load(LETH/'source-snapshot-1.json')['inputs']
    assert lethSaved['sourceInputs']=={p:d for p,d in lethBorn['sourceInputs'].items()if p.endswith('.cs')}and lethSaved['copiedData']==born['copiedData']
    with tarfile.open(STAGE/'failed-native-6.tar.gz')as a:
        assert e.load(STAGE/'failed-native-6-native-inputs.json')==lethBorn['artifacts']
        for p,d in lethBorn['sourceInputs'].items():
            if p.endswith('.cs')or p.startswith('tests/'):assert hashlib.sha256(a.extractfile(p).read()).hexdigest()==d,p
            else:assert e.sha(ROOT/p)==d,p
        old=a.extractfile(consume).read().decode();assert hashlib.sha256(old.encode()).hexdigest()==parent['sourceInputs'][consume]
        assert 'Pre-save pulse changed saved buff/deadline/wounds'in a.extractfile(prefix+'birth-native-checks.log').read().decode()
    with tarfile.open(LETH/'accepted-assemblies-1.tar.gz')as a:
        assert hashlib.sha256(a.extractfile('integration/Sanctuary.Integration.dll').read()).hexdigest()==lethBorn['artifacts']['Assemblies/Sanctuary.Integration.dll']
    lethargy=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'sigil-lethargy-check':1,'sigil-lethargy-check-result':1,'sigil-lethargy-reload-check':2,'sigil-lethargy-reload-check-result':2})
    assert 'Sigil expiry:original'not in e.cohort(LETH,lethargy,15)
    assert 'pass across3boots/2reloads'in(LETH/'native-run-2.log').read_text()
    snapshot(CONSUME/'source-snapshot-1.json',CONSUME/'birth-inputs.json');assert e.load(CONSUME/'birth-inputs.json')['artifacts']==born['artifacts']
    consumeMarkers=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'consume-aura-check':1,'consume-aura-check-result':1,'consume-aura-reload-check':2,'consume-aura-reload-check-result':2,'consume-aura-save-check':3})
    assert 'Consume saved expiry:original'not in e.cohort(CONSUME,consumeMarkers,18)
    assert 'pass across3boots/2reloads'in(CONSUME/'native-run-1.log').read_text()
    log=e.cohort(STAGE,expected(),906);assert log.count('Client: 127.0.0.1: Connected.')==378
    assert 'Consume saved expiry:original'in log
    for ability in ('SigilLethargy','SigilRancor','SigilDeath'):
        assert f'Sigil expiry:original {ability}'in log,'Full cohort must prove natural expiry: '+ability
    assert all('ExpiredOriginal'in(ROOT/p).read_text()for p in sigils)
    with tarfile.open(STAGE/'failed-native-4.tar.gz')as a:
        assert 'SigilActorChecks.Reload'in a.extractfile(prefix+'birth-native-checks.log').read().decode()
        for p in sigils:assert hashlib.sha256(a.extractfile(p).read()).hexdigest()==parent['sourceInputs'][p]

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
    files=[*(ROOT/p for p in paths),*c['extra_inputs'],ROOT/'tests/check_echoing_input_retention.py',ROOT/'tests/check_sigil_actor_lifecycle.py',ROOT/'tests/check_sigil_lethargy_lifecycle.py',ROOT/'tests/check_consume_aura_lifecycle.py',*e.stage_files(STAGE,FOCUS,SIGIL,LETH,CONSUME),STAGE/'accepted-assemblies-1.tar.gz',FOCUS/'accepted-assemblies-1.tar.gz',SIGIL/'accepted-assemblies-1.tar.gz',SIGIL/'retained-active-1.tar.gz',LETH/'accepted-assemblies-1.tar.gz',LETH/'failed-native-1.tar.gz',CONSUME/'accepted-assemblies-1.tar.gz',*(STAGE/f'failed-native-{n}.tar.gz'for n in (1,2,3,4,5,6)),*(STAGE/f'failed-build-{n}.tar.gz'for n in (4,6))]
    result=dict(component='Current all-thirty Warlock/Witch lifecycle/input and direct/item/native integration',referenceVersion='3.3.93847',baselineCommit='4e4a20d97571018b400ac9491a5975f7853ef5a1',nativeMarkers=906,markers=dict(sorted(expected().items())),fixtureFamilies=91,sharedChecks=6,ordinaryInputCommands=73,explicitWarlockInputCases=audit['ordinaryInputCases'],foreignInputCases=audit['foreignInputCasesSeparatelyExcluded'],heldBindSequences=6,activeTalents=26,passiveTalents=4,connections=378,boots=3,saves=3,reloads=2,artifacts=born['artifacts'],productionCodeChanged=False,runtimeDataChanged=False,contentRebuilds=0,integrationBuild=9,integrationBuildAttempts=9,integrationBuildFailures=2,nativeRebuilds=0,sourceSnapshot=9,nativeRun=7,frozenNativeFailures=6,focusedMarkers=12,focusedIntegrationBuild=2,positiveSigilMarkers=33,positiveLethargyMarkers=15,focusedLethargyDriverFailures=1,positiveConsumeMarkers=18,changedTestSources=changed,saveDeletionWarnings=warnings,unsupportedWarnings=unsupported,classCertified=False,goalComplete=False,installed=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='All five earlier Warlock result inventories retain on current binaries,with26active input routes and four separate passive consumers. Bind uses actual held packets,compiled Warlock/Witch ownership,free release/locked target/nine-mana continuation and original-body saving;heartbeats do not inflate counts. Three Python preconditions and one C# Bind assertion now retain the separately approved final four rather than demanding historical pending gates. Echoing input inspection retains its original real Hex payload with an extended test deadline,exact capture/resources and foreign-state checks;normal gameplay expiry remains. Apocalypse constructs its saved Weaken inspection with the established retention interval;ordinary curse durations and every resistance/coexistence assertion retain. Focused12 remains bound to original I2/source3 and its frozen source archive,not the later fixture DLL. Focused33 retains positive original Sigil-family/Rancor/Death identity,pulse and recipient-state checks;focused15retains Lethargy held recipient,source and active marker checks;the longer full cohort verifies their real204second expiry without replacing markers,resetting clocks,healing or changing held recipient RNG. Consume inspection captures its original maximum and checks normal seven-minute expiry with shared relative-life rescaling,without replacing the buff or resetting its deadline;focused18retains positive provider/save paths. No gameplay/numeric/data/native change. Existing source/event/RNG/stat/world/geometry/species/elite/Sunder/item/recipient/PvP and graphical-client limits remain explicit;effects/audio and icons stay separate. Defiler mid-windup and Consume short-aura save paths both retain their own assertions on the existing connected save.')
    e.write('warlock-lifecycle-acceptance-native-evidence.json',result,files,check)
    print('Warlock/Witch:906exact results/26active+4passive scopes/378connections/two reloads;accepted production reused.')

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
