#!/usr/bin/env python3
"""Adopt the exact tested Bone Armor policy without rebuilding unchanged code."""
import argparse,hashlib,json,subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
R=e.ROOT;S=R/'.local/class-skills/bone-armor-rule'

def record(check=False):
    approval=e.load(R/'tools/reference/bone-armor-rule-approval.json')
    assert approval['approved'] and approval['answer']=='Approve this Bone Armor rule (Recommended)'
    assert approval['questionItemId']=='["request_user_input_async","call_541b88c9ed154b53b1d60e03d6744789",0]'
    assert approval['candidateCommit']=='ac3948d287a88c0dd3f80308bed265957ad69a2a'
    candidate=R/approval['testedCandidateEvidence'];assert e.sha(candidate)==approval['testedCandidateEvidenceSha256']
    tested=e.load(candidate);assert tested['nativeMarkers']==36 and tested['nativeArmorIgnorePhysicalFix']
    original=subprocess.check_output(['git','show',approval['candidateCommit']+':'+approval['proposal']],cwd=R)
    assert hashlib.sha256(original).hexdigest()==approval['proposalAtApprovalSha256']
    assert original.decode().replace('**Pending explicit adoption.**',
        '**Approved by the user on 2026-11-16 JST.** [Exact decision](../tools/reference/bone-armor-rule-approval.json). The body below is the tested proposal; its current-code adoption is recorded separately.',1)==(R/approval['proposal']).read_text()
    snapshot(S/'source-snapshot-2.json',S/'birth-inputs.json')
    born=e.load(S/'birth-inputs.json')
    for n,h in born['sourceInputs'].items():assert e.sha(R/n)==h,n
    for kind in('Content','Integration'):assert e.sha(S/kind.lower()/f'Sanctuary.{kind}.dll')==tested['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    log=e.cohort(S,Counter(tested['markers']),36);assert log.count('Client: 127.0.0.1: Connected.')==6
    assert e.sha(S/'birth-native-checks.log')==tested['inputs'][str((S/'birth-native-checks.log').relative_to(R))]
    files=['tools/reference/bone-armor-rule-approval.json',approval['testedCandidateEvidence'],approval['proposal'],
        'tools/record_bone_armor_adoption.py','docs/bone-armor-rule-checkpoint.md','docs/class-skill-roadmap.md',
        'docs/class-skills-resume-checkpoint.md','docs/necromancer-mechanical-audit.md','docs/necromancer-coverage-matrix.md',
        'docs/class-skill-reuse-notes.md','docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md']
    for n in tested['frozenPendingFiles']:
        assert e.sha(R/n)==hashlib.sha256(subprocess.check_output(['git','show',approval['candidateCommit']+':'+n],cwd=R)).hexdigest();files.append(n)
    result=dict(referenceVersion='3.3.93847',component='Approved Bone Armor absorption policy adoption',approved=True,
        testedCandidateCommit=approval['candidateCommit'],contentBuild=2,integrationBuild=2,sourceSnapshot=2,nativeRun=2,
        nativeMarkers=36,actualInputs=6,reloads=2,handlers=236,completedClasses=[],runtimeChangedForAdoption=False,
        gameplaySourceChangedForAdoption=False,dataChangedForAdoption=False,assembliesRebuiltForAdoption=False,nativeRerunForAdoption=False,
        scope='The original tested policy is explicitly adopted. Native armor-ignore physical repair and conventional overflow/order remain the exact C2/I2/S2/N2code;no claim of recovered modern event22/order or unsupported legacy typed lanes.',
        preservation=e.protection('tools/reference/slow-missiles-learned-client-evidence.json',44),
        inputs={n:e.sha(R/n)for n in files})
    p=R/'tools/reference/bone-armor-rule-adoption-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert p.read_text()==encoded
    else:p.write_text(encoded)
    print('Bone Armor policy adopted with exact unchanged C2/I2/S2/N2proof;no rebuild or native rerun.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
