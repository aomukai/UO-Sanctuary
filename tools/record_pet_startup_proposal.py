#!/usr/bin/env python3
"""Authenticate the unapproved normal-demon startup review checkpoint."""
import argparse,json,subprocess
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/pet-startup-proposal';PARENT='b1c4867868a218ef8ddc64a0d21e768b53dd6c19'
def record(check=False):
    inputs={}
    def pin(path):
        inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text())if path.suffix=='.json'else path.read_bytes()
    production=pin(ROOT/'src/Sanctuary.Content/GenericPetStartupProposal.cs').decode()
    assert 'internal const bool Approved=false;'in production and 'proposed-normal-demon-startup-v1'in production and 'new(-1,0,0)'in production
    users=[]
    for path in (ROOT/'src/Sanctuary.Content').glob('*.cs'):
        if path.name!='GenericPetStartupProposal.cs'and 'GenericPetStartupProposal.'in path.read_text():users.append(path.name)
    assert not users,'Pending startup proposal acquired a production caller'
    for name in ['GenericPetAiEntry.cs','GenericPetAiSelection.cs','GenericPetAiPreprocess.cs','GenericPetAiPostTarget.cs','GenericPetAiOverrides.cs','GenericPetOwnerLoss.cs','GenericPetInitialization.cs','GenericPetDecisions.cs','GenericPetCombat.cs','GenericPetOwnerSurvey.cs','GenericPetOwnerIterator.cs','GenericPetTimedEvents.cs','DeathMarkWorld.cs','WarlockCombat.cs']:
        path='src/Sanctuary.Content/'+name;assert (ROOT/path).read_bytes()==subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT),path
    old=pin(STAGE/'compile-diagnostics-1/source-snapshot-2.json')['inputs'];current=pin(STAGE/'source-snapshot-3.json')['inputs']
    assert old['projectInputs']==current['projectInputs']and old['copiedData']==current['copiedData']
    changed=[n for n in old['sourceInputs']if old['sourceInputs'][n]!=current['sourceInputs'][n]]
    assert changed==['tests/Sanctuary.Integration/GenericPetStartupProposalChecks.cs']
    before=pin(STAGE/'compile-diagnostics-1/GenericPetStartupProposalChecks.cs').decode()
    assert before.replace('Target48:drafts[i].CommandTarget,Type4c:drafts[i].CommandType','Target48=drafts[i].CommandTarget,Type4c=drafts[i].CommandType')==(ROOT/changed[0]).read_text()
    assert 'CS1003'in pin(STAGE/'compile-diagnostics-1/integration-build-1.log').decode()
    early=pin(STAGE/'compile-diagnostics-1/native-run-1.log').decode();assert 'FileNotFoundError'in early and 'Sanctuary.Integration.dll'in early
    for name in ['GenericPetStartupProposal.cs','GenericPetStartupProposalChecks.cs','source-snapshot-1.json','content-build-1.log','Sanctuary.Content.dll']:pin(STAGE/'pre-review-1'/name)
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-3.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    for kind in ['content','integration']:
        name=f'Sanctuary.{kind.title()}.dll';assert e.sha(STAGE/kind/name)==born['artifacts']['Assemblies/'+name]
        log=pin(STAGE/f'{kind}-build-2.log').decode();assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'pet-command-target-check':1,'pet-command-target-reload-check':2,'pet-startup-proposal-check':1,'pet-startup-proposal-reload-check':2})
    log=e.cohort(STAGE,expected,15);assert log.count('Startup proposal original:normal entry/default root/empty combat decision')==9
    assert log.count('AI override original:index0 actual table callback')==6 and log.count('AI override original:index1 actual table callback')==3
    pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-2.log');review=pin(STAGE/'pet-startup-proposal-fixtures.json');original=pin(STAGE/'pet-command-target-fixtures.json')
    assert review['Generation']==original['Generation']==2 and len(review['Entries'])==len(original['Entries'])==3 and len(review['Owners'])==2
    for entry,cls,owner in zip(review['Entries'],[742,743,744],[review['Owners'][0],review['Owners'][1],review['Owners'][0]]):
        draft=entry['Draft'];assert draft==dict(SourceClass=cls,OwnerGame=71,OwnerId=owner,OwnerKind=0,State=0,Function=0x4b60c0,Mode10=0,Field14=2,CommandTarget=0,CommandType=0,Flags38=0,TargetId3c=-1,TargetKind40=1,Ai90=0,Words=dict(Value0=-1,Value1=0,Value2=0))
        assert entry['Command']['Control']==draft['Words']and entry['Command']['Target48']==entry['Command']['Type4c']==0
        assert entry['Life']>0 and entry['Birth']
    for i,entry in enumerate(original['Entries']):assert entry['Command']['Control']==dict(Value0=-2147483648+i+6,Value1=-1-i-42,Value2=2147483647-i)
    installed=pin(ROOT/'tools/reference/creeper-frame-installation-evidence.json');assert len(installed['installedArtifacts'])==106
    for name,h in installed['installedArtifacts'].items():assert e.sha(Path(installed['runtimeRoot'])/name)==h,name
    protected=pin(ROOT/'.local/class-skills/creeper-frame-installation.json')['protected']
    for kind,relative,count in [('saves','Saves',38),('configuration','Configuration',20)]:
        folder=Path(installed['runtimeRoot'])/'.local/server'/relative
        assert len(protected[kind])==count and {str(p.relative_to(folder)):e.sha(p)for p in folder.rglob('*')if p.is_file()}==protected[kind]
    client=pin(ROOT/'tools/reference/ring-cast-client-evidence.json');clientInputs=pin(ROOT/client['inputManifest']['path'])
    assert e.sha(ROOT/client['inputManifest']['path'])==client['inputManifest']['sha256']and client['patches']==len(clientInputs['patchHashes'])==43
    for group in ['patchHashes','sharedInputs']:
        for name,h in clientInputs[group].items():assert e.sha(ROOT/name)==h,name
    for name in ['docs/pet-startup-proposal.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skill-reuse-notes.md','docs/skill-mechanical-reference.md','tests/check_pet_startup_proposal.py']:pin(ROOT/name)
    pin(Path(__file__).resolve())
    result=dict(referenceVersion='3.3.93847',handlers=213,warlockHandlers=4,completedClasses=[],approved=False,reviewOnly=True,productionCallers=[],automaticAiEnabled=False,learnedEnabled=False,
        proposalPolicy='proposed-normal-demon-startup-v1',originalSpecies=3,additionalOriginalReviewActors=3,normalEntryRuns=9,newReferenceReadBytes=0,oldLargeOfflineMatricesRerun=0,
        nativeMarkers=15,markers=dict(expected),boots=3,reloads=2,saves=3,contentBuild=2,integrationBuild=2,sourceSnapshot=3,nativeRun=2,
        artifacts=born['artifacts'],preservation=dict(installedArtifacts=106,saves=38,configuration=20,clientPatches=43,installed=False),
        verified=['Pending factory produces explicit proposed normal startup for only actual three-species GenericPet metadata. Unknown owner/world/species refuse;mutable control/queue/early-target instances do not alias. Drafting leaves unknown registered controller state unchanged;approval guard refuses.',
            'Additional original Warlock/Witch demon review actors explicitly install the draft only in disposable tests. Native owner registry,registered initializer,full prepared entry and root/empty combat policy run;no previous skill query,target/attack or special override is invented. Birth/wounds/mana/commands/control words retain across two reloads.',
            'Separate existing original-demon cohort still retains its supplied nondefault control values and both override callbacks. Existing source policies and production birth/learned gates are unchanged.'],
        diagnostics=['Content1review made empty command explicit and selected existing null-selection finish. Original source/log/snapshot/DLL retainedpre-review-1.',
            'First Integration had record-initializer colon syntax error;test-only punctuation fixed. Snapshots2/3 differ only in that test;Content2reused. First native launcher exited before server startup because Integration DLL was absent;exact diagnostics retained. First actual native server cohort is run2 and passes.'],
        limits=['This is a tested Sanctuary proposal,not verified D2R constructor behavior. Current do56/control producer remains unqualified. Approved=false and no production caller;no save migration or automatic AI.',
            'World/status/path/search/follow inputs remain explicit review fixtures. Review manifest carries draft values,not a production live-header persistence schema. Automatic clock,world/action binding,pet-cap and life-cycle rollout are outside this approval.',
            'No learned route,class certificate,deployment or assets. User approval required before dependent startup rollout;independent work can continue.'],inputs=inputs)
    path=ROOT/'tools/reference/pet-startup-proposal-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Startup proposal:3additional original species/9normal entries,15checks/two reloads;approval pending,production unchanged.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
