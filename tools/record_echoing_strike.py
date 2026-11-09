#!/usr/bin/env python3
"""Authenticate current calc aliases, Echoing suppliers and native persistence."""
import argparse,csv,json,re
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/echoing-strike'
def record(check=False):
    inputs={}
    def pin(path):
        path=ROOT/path if isinstance(path,str)else path;inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text())if path.suffix=='.json'else path.read_text()
    foundation=pin('tools/reference/warlock-foundation-preflight.json');licensed=foundation['localExtraction']
    owned=ROOT/'.local/class-skills/fire-golem-mode-source/skillcalc.txt';text=pin(owned)
    assert licensed['referenceVersion']=='3.3.93847'and e.sha(owned)==licensed['sha256']
    aliases=[dict(alias=r['code'],field=int(re.search(r'calc(\d+)',r['*Description']).group(1)))for r in csv.DictReader(text.splitlines(),delimiter='\t')if r['code'].startswith('clc')]
    assert aliases==pin(STAGE/'calc-aliases.json')and len(aliases)==10
    primary=pin('tools/reference/d2r-named-equipment/json/skills.json');rows=primary.values()if isinstance(primary,dict)else primary
    uses=[dict(skill=r['skill'],field=k,expression=v)for r in rows for k,v in r.items()if isinstance(v,str)and re.search(r'\bclc[0-9]\b',v)]
    assert len(uses)==6 and {r['skill']for r in uses}=={'Echoing Strike','Blade Warp','Cleave','Mirrored Blades','Goatman Cleave'}
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
    first=pin(STAGE/'source-snapshot-1.json')['inputs'];last=pin(STAGE/'source-snapshot-2.json')['inputs']
    assert first['sourceInputs']==last['sourceInputs']and first['projectInputs']==last['projectInputs']
    assert {n for n in first['copiedData']if first['copiedData'][n]!=last['copiedData'][n]}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    for kind in ['content','integration']:
        log=pin(STAGE/f'{kind}-build-2.log');assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    expected=Counter({'iron-golem-birth-check':3,**{n+'-check':1 for n in ['iron-golem-items','npc-resource-callbacks','echoing-strike','mirrored-blades']},**{n+'-reload-check':2 for n in ['iron-golem-items','npc-resource-callbacks','echoing-strike','mirrored-blades']}})
    log=e.cohort(STAGE,expected,15);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-1.log')
    assert log.count('Echoing suppliers:10pinned aliases/176rank-hard-synergy controls')==3
    saved=pin(STAGE/'echoing-strike-fixtures.json');assert [r['Female']for r in saved]==[False,True]
    assert all(r['Values']['Rank']==8 and r['Values']['HardMirroredBlades']==4 and r['Values']['HardBladeWarp']==3 and r['Values']['TotalAttacks']==1 and r['Life256']%256==127 and r['Mana256']%256==239 for r in saved)
    pin(STAGE/'mirrored-blades-fixtures.json')
    previous=pin('tools/reference/ordered-damage-events-native-evidence.json')
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:assert born['artifacts'][name]==previous['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    protected=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    status=pin('data/Sanctuary/Balance/class-skill-status.json')['skills'];assert sum(r['implementation']=='handler-present'for r in status)==218 and sum(r['implementation']=='handler-present'and r['talentId'].startswith('warlock.')for r in status)==9
    assert next(r for r in status if r['talentId']=='warlock.echoingstrike')['verification']=='partial-native-coverage'
    for name in ['src/Sanctuary.Content/SkillMechanics.cs','src/Sanctuary.Content/EchoingStrikePrepared.cs','tests/Sanctuary.Integration/EchoingStrikeChecks.cs','tests/check_echoing_strike.py','tools/record_echoing_strike.py','tools/build_skill_roadmap.py','docs/echoing-strike-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md','data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json']:pin(name)
    result=dict(referenceVersion='3.3.93847',component='registered calc aliases and prepared Echoing Strike suppliers',handlers=218,warlockHandlers=9,completedClasses=[],echoingLearnedEnabled=False,aliases=aliases,affectedExpressions=uses,aliasSourceExecutionClaim=False,nativeMarkers=15,markers=dict(expected),boots=3,reloads=2,saves=3,contentBuild=2,integrationBuild=2,sourceSnapshot=2,nativeRun=1,nativeRebuilt=False,clientRebuilt=False,newLiveReferenceReads=0,newTimers=0,newPersistenceIds=0,serializationVersionChanged=False,optionalAudit=False,artifacts=born['artifacts'],preservation={**protected,'installed':False},limits=['Echoing do166/missile706 do44/hit34/damage16 remain unrecovered;no flight or damage delivery implemented.','First-versus-duplicate scaling,geometry,weapon/item/poison chronology,hit-delay identity and learned input/payment remain unresolved.','Supplier and persistence checks do not certify full class mechanics or graphical acceptance.'],inputs=inputs)
    path=ROOT/'tools/reference/echoing-strike-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Echoing suppliers/calc aliases:15markers/3boots/2reloads;176boundary cases,real gear and Mirrored controls pass;218handlers;installed files preserved.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
