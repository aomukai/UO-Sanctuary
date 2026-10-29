#!/usr/bin/env python3
"""Bind complete current per-target scoring and routing compositions to native acceptance."""
import argparse,hashlib,json,re,subprocess
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/generic-pet-target-scoring'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
    inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json');artifacts=[]
    _,prior=frozen_receipt('f3be042','tools/reference/generic-pet-rating-routing-native-evidence.json');assert prior['verified']['passMarkers']==231
    for kind in ['content','integration']:
        p=STAGE/(kind+'-build.log');assert all(s in p.read_text() for s in ['Build succeeded.','0 Warning(s)','0 Error(s)']);artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
    log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==234
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    marker=r'\] ([A-Za-z0-9-]+): passed';before=Counter(re.findall(marker,(ROOT/'.local/class-skills/generic-pet-rating-routing/stats-native-checks.log').read_text()));after=Counter(re.findall(marker,log))
    assert sum(before.values())==231 and not before-after and after-before==Counter({'generic-pet-target-scoring-check':1,'generic-pet-target-scoring-reload-check':2})
    assert log.count('GenericPet target scoring:25115captured-current contexts,4176full routing/scorer compositions,24111scorer entries,321fatal and6unsupported-metadata boundaries;exact descriptors,metadata order and shared pet RNG pass;real world adapters remain gated')==3
    sourcepath=ROOT/'tools/reference/generic-pet-target-scoring-source-evidence.json';source=json.loads(sourcepath.read_text());artifacts.append(sourcepath);v=source['verified']
    assert {k:v[k] for k in ['contexts','byKind','scorerEntries','capturedHelperBytes','inlineTableBytes','fatalBoundaries','unsupportedType2Metadata']}==dict(contexts=25115,byKind=dict(score=20939,composed=4176),scorerEntries=24111,capturedHelperBytes=3812,inlineTableBytes=156,fatalBoundaries=321,unsupportedType2Metadata=6)
    assert len(v['branchLandmarksVisited'])==17 and v['branchLandmarksUnvisited']==['loop-selector-fatal']
    assert not source['processMemoryRead'] and source['sourceCodeExecutedInOfflineEmulator'] and not source['completeAiVerified'] and not source['handlerEnabled']
    for name,h in source['artifacts'].items():assert sha(ROOT/name)==h,name;artifacts.append(ROOT/name)
    lib=ROOT/source['emulatorLibrary']['path'];assert sha(lib)==source['emulatorLibrary']['sha256'];artifacts.append(lib)
    d=STAGE/'diagnostics/type4-health-bonus';failed=(d/'stats-native-checks.log').read_text();assert 'generic-pet-target-scoring-check: failed' in failed and 'differs at 32 score:' in failed
    oldFixture=json.loads((d/'generic-pet-target-scoring-source-probe.json').read_text());newFixture=json.loads((ROOT/'.local/class-skills/generic-pet-target-scoring-source-probe.json').read_text())
    assert len(oldFixture)==24825 and oldFixture[:24824]==newFixture[:24824] and oldFixture[-1]==newFixture[-1]
    assert oldFixture[32][-1][1][2]==22 and ',27,41,3,16,' in failed
    # A failed temporary result still consumes the source pet RNG. These added
    # compositions deliberately score INT_MIN after rolling,then retry/discard.
    discard=[r for r in newFixture if r[0]=='composed' and r[2][4]==2147483624]
    assert len(discard)==16
    for row in discard:
        entries=sum(t[0]==20 for t in row[-1][3]);assert entries in [2,3]
        expectedSeed=row[1]['seed']
        for _ in range(entries*3):expectedSeed=0x6ac690c5*(expectedSeed&0xffffffff)+(expectedSeed>>32)
        assert row[-1][2]==expectedSeed and row[-1][1][2]==-2147483648
    # Seed zero is a valid fixed point; draw consumption must not be tested
    # with a blanket seed-changed assertion.
    for p in d.rglob('*'):
        if p.is_file():artifacts.append(p)
    d2=STAGE/'diagnostics/type8-self-target';failed2=(d2/'stats-native-checks.log').read_text()
    assert 'generic-pet-target-scoring-check: failed' in failed2 and '13560 score:[0,[0,' in failed2
    assert newFixture[13560][-1][1][0]==1 and sha(d2/'generic-pet-target-scoring-source-probe.json')==sha(ROOT/'.local/class-skills/generic-pet-target-scoring-source-probe.json')
    for p in d2.rglob('*'):
        if p.is_file():artifacts.append(p)
    audits=[]
    for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
        p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text())
        assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
        for n,h in a['InputHashes'].items():assert sha(ROOT/n)==h,n
        audits.append(a);artifacts += [p,ROOT/'tools/reference'/name]
    old=json.loads(subprocess.check_output(['git','show','f3be042:tools/reference/named-item-runtime-audit.json'],cwd=ROOT))
    assert audits[0]['Definitions']==old['Definitions'] and len(audits[0]['Definitions'])==573 and [len(a['InputHashes']) for a in audits]==[738,21]
    python=(STAGE/'python-tests.log').read_text();assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    mainpath=ROOT/'tools/reference/creeper-frame-installation-evidence.json';main=json.loads(mainpath.read_text());assert len(main['installedArtifacts'])==106
    for name,h in main['installedArtifacts'].items():assert sha(Path(main['runtimeRoot'])/name)==h,name
    for name in ['class-skill-status.json','skill-effects.json','skill-icons.json']:
        p=ROOT/'data/Sanctuary/Balance'/name;assert sha(p)==hashlib.sha256(subprocess.check_output(['git','show','f3be042:'+str(p.relative_to(ROOT))],cwd=ROOT)).hexdigest();artifacts.append(p)
    artifacts += [STAGE/n for n in ['stats-inputs.json','stats-native-checks.log','run.log','source-run.log','source-recheck.log','python-tests.log']]
    artifacts += [p for p in (STAGE/'focused-fixtures').iterdir() if p.is_file()]
    artifacts += [ROOT/n for n in ['tools/record_generic_pet_target_scoring.py','tests/check_generic_pet_target_scoring.py','tools/build_skill_roadmap.py','tools/build_property_integration_backlog.py','tools/reference/item-property-integration-backlog.json']]+[mainpath]
    result=dict(referenceVersion='3.3.93847',component='Current GenericPet full per-target scoring policy and routing compositions',installed=False,handlerEnabled=False,fullAiVerified=False,fullPerTargetPolicyVerified=True,realWorldMetadataGeometryStateOwnerAdaptersAccepted=False,sharedActorSeedPersistenceAccepted=False,newActorPersistenceAccepted=False,goalComplete=False,handlers=183,warlockHandlers=1,completedClasses=[],verified=dict(passMarkers=234,retainedMarkers=231,worldBoots=3,actualWorldReloads=2,contextsPerBoot=25115,perTargetContexts=20939,fullRoutingScorerCompositions=4176,scorerEntriesPerBoot=24111,capturedHelperBytes=3812,includedInlineTableBytes=156,fatalBoundaries=321,unsupportedType2Metadata=6,visitedBranchLandmarks=17,reentrantSelectorFatalCovered=False,discardedAfterRngCompositions=16,exactDescriptorsMetadataAndSharedPetRng=True,sourceRandomReused=True,currentCoordinateReadOdditiesPreserved=True,frozenPriorFullClassMarkers=1184,newFullClassRun=False,pythonTests=70,unchangedNamedDefinitions=573,inputFingerprints=[738,21],unchangedMainInstallationArtifacts=106),inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},frozenPredecessor=dict(commit='f3be042',receipt='tools/reference/generic-pet-rating-routing-native-evidence.json'),established=[
        'The complete3092-byte current per-target scorer and720-byte routing wrapper execute offline. Their3812captured bytes include156bytes of validated inline tables. Native results match25115contexts per boot:20939direct cases and4176compositions executing both bodies,with24111scorer entries,321fatal boundaries and6explicit unsupported metadata-null reads. Seventeen selected branch landmarks are reached;this is not a full CFG certificate.',
        'The port preserves signed quarter-field truncation,rank/base bonuses,flag4stack rejection/progressive bonus,flag8field108 arithmetic,element-to-stat lookup,resistance buckets including INT_MIN absolute overflow,distance,related-state checks,owner counters/formula limits,context counts,melee/ranged/monster-row/collision gates and health-like raw-field thresholds. Field names remain neutral where actual world mapping is unfinished.',
        'SourceRandom and the existing mutable seed object are reused. Per-target randomness consumes zero/one draw except type7no-owner destinations,which consume two coordinate draws plus an optional rating draw. Sixteen compositions deliberately produce INT_MIN after rolling,then discard/retry;the shared seed still advances. Temporary routing results and all40descriptor bytes match current source. Real actor seed ownership/persistence is not accepted.',
        'Current type7midpoint calculation reuses the target-X difference for Y;the random no-owner destination uses pet X for both axes. The captured code and independent coordinate matrices establish those reads;they are preserved rather than silently corrected. World geometry and metadata providers remain required.',
        'The native comparisons caught an incorrect health bonus applied to type4 (source applies it only to type12),then an incorrect owner target in type8 (source selects self below its threshold). Both failed logs,inputs,DLLs,new C# sources and oracle versions are preserved. All24824original nonterminal oracle cases and the terminal missing-pet case remain exactly unchanged;source instructions and expectations were not weakened. Extra owner-threshold/collision and discarded-RNG controls were added after the first failure. The complete final source fixture remains byte-identical across the second managed correction.',
        'All231predecessor native markers remain. Both builds have zero warnings/errors;70Python checks,573unchanged named eligibility decisions and106unchanged main installation hashes pass. Existing saved fixtures survive two actual reloads;this policy adds no actor persistence. Frozen417cd39retains1184full-class checks separately;no new full-suite run or new live capture claimed.'
    ],remaining=[
        'Actual world skill/stat/state/monster metadata,owner-counter/formula,geometry/collision and shared actor seed bindings remain required. The loop-only selector-fatal branch needs mutation during metadata calls and is outside the fixed/reentrancy-free service matrix. Static units without required paths are unsupported source inputs.',
        'Continue target-search callbacks/selection,ordering,approach/follow execution,actual mode/skill effects,dispatcher and full actor lifecycle. Protected event/proc helpers and ordering remain unavailable;continue unaffected work. Learned Goatman/Tainted/Defiler casting,extended passives/events/Blood Oath/sunder,new Frenzy/Cleave and owner/facet/logout/death/respec/save behavior remain gated. Raw-item/Necromancer dependencies continue. Effects/audio and talent/hotkey icons remain separate placeholders;no class is complete.'
    ])
    out=ROOT/'tools/reference/generic-pet-target-scoring-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('GenericPet target scoring:234native/70Python;25115current-code contexts per boot;real world adapters unfinished.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
