#!/usr/bin/env python3
"""Keep Consume's common skill/callback boundary separate from imported monpet data."""
import argparse,hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
    ref=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((ref/'sources.json').read_text())
    for name in ('skills','states','pettype','monstats'):
        assert sha(ref/f'json/{name}.json')==manifest['files'][f'json/{name}.json']['sha256']
    row=json.loads((ref/'json/skills.json').read_text())['381'];data=ROOT/'data/Sanctuary/Balance/class-skills.json'
    runtime=next(r for r in json.loads(data.read_text())if r['sourceId']==381)
    assert runtime['sourceFields']=={k:v for k,v in row.items()if not k.startswith('*')and k!='lineNumber'}
    pets=json.loads((ref/'json/pettype.json').read_text());assert pets['demon']['pool']==pets['binddemon']['pool']==1
    state=json.loads((ref/'json/states.json').read_text())['consume'];assert state['*ID']==208 and state['group']==9
    capture=ROOT/'.local/class-skills/warlock-runtime-reference/warlock-do-159-20261108/capture.json';saved=json.loads(capture.read_text());candidate=saved['records'][0]
    assert saved['referenceVersion']=='3.3.93847'and candidate['slot']==159 and len(candidate['fragments'])==5 and not any(f['captured']for f in candidate['fragments'])
    files=[ref/'sources.json',data,*(ref/f'json/{n}.json'for n in ('skills','states','pettype','monstats')),capture,Path(__file__).resolve()]
    result=dict(referenceVersion='3.3.93847',component='Consume common supplier and unavailable callback boundary',skill=381,start=row['srvstfunc'],do=row['srvdofunc'],
        state=208,group=9,duration=row['auralencalc'],lifeStat=row['aurastat1'],lifeFormula=row['aurastatcalc1'],velocityStat=row['aurastat2'],velocityFormula=row['aurastatcalc2'],
        inTown=row['InTown'],left=row['leftskill'],right=row['rightskill'],period=row['perdelay'],periodicClear=row['periodicClearAura'],
        sourceCandidate=dict(rva=candidate['rva'],fragments=5,readableCodeBytes=0),newLiveReferenceReads=0,
        importedSource='tools/reference/d2r-consume/sources.json',numericContext='Explicit supplied caster Consume effective rank,Death Mark hard rank and1/2/3difficulty. Scoped sksrc parser alias;not recovered do159 caller.',
        externalReferences=[dict(url='https://diabloforfans.com/skill/consume?lang=enUS',kind='Author datamined reference',reportedVersion='3.3.93847',observedOn='2026-11-15',scope='Corroborates common/typed bonuses and caps;not a current startup/stat writer.'),
            dict(url='https://upload3.inven.co.kr/upload/2026/03/02/bbs/WarlockSkillsNotesandFunctionality.pdf',kind='Original Warren/Kabryxis tester notes',version='Unspecified older upload',scope='Reports additive life,effective run/walk and Consume occupying the shared demon pool;does not verify current native implementation.')],
        established=['Existing28-file manifest stays intact;new immutable monpet JSON and independent TSV match21fields for the three original species.',
            'Separate source pet types demon and binddemon both have pool1;the old separate-pool comment is incorrect,but Bind Demon itself remains unsupported.',
            'Consume381 is right-only/TargetPet/InTown1,with common max-life and velocity fields plus periodic25/clearAura;state208isgroup9.'],
        unresolved=['Current start69/do159,source caller/difficulty lookup,typed stat binding,life transition,periodic lifetime/removal,slot replacement and payment remain unverified. Numerical capture installs no gameplay effect.',
            'Town InTown1must be reconciled with approved hidden/parked armies,using an explicit owned-roster targeting path rather than quietly treating Consume as outside-town only.',
            'Bound-monster bonuses and source-underling exceptions remain for future Bind Demon integration;only the three original species are admitted by this numerical kernel.'],
        inputs={str(p.relative_to(ROOT)):sha(p)for p in files})
    out=ROOT/'tools/reference/consume-source-review.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==text,'Consume boundary review stale'
    else:out.write_text(text)
    print('Consume381:start69/do159/5protected fragments,right-only/town/periodic and sharedpool1 authenticated;no gameplay activation.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
