#!/usr/bin/env python3
"""Pin Warlock's current data and extended callback aliases; preserve limits."""
import argparse,csv,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/warlock-foundation'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False,extract=False):
    installation=json.loads((ROOT/'tools/reference/d2r-local-installation-evidence.json').read_text());animation=json.loads((ROOT/'tools/reference/d2r-local-animation-evidence.json').read_text())
    assert installation['referenceVersion']==animation['referenceVersion']=='3.3.93847'
    reader=STAGE/'casc-reference-reader';readerSource=reader.with_suffix('.cpp');assert sha(readerSource)==animation['reader']['sourceSha256']
    owned=STAGE/'skillcalc.txt';receipt=STAGE/'skillcalc-extraction.json'
    if extract:
        game=Path(installation['installationPath']);exe=game/installation['executable']['filename'];info=game/'.build.info'
        assert sha(exe)==installation['executable']['sha256'] and sha(info)==installation['buildInfo']['sha256']
        subprocess.run([str(reader),str(game),'data:data/global/excel/skillcalc.txt',str(owned)],check=True)
        assert sha(exe)==installation['executable']['sha256'] and sha(info)==installation['buildInfo']['sha256']
        receipt.write_text(json.dumps(dict(referenceVersion='3.3.93847',executableSha256=sha(exe),buildInfoSha256=sha(info),readerSha256=sha(reader),readerSourceSha256=sha(readerSource),archivePath='data:data/global/excel/skillcalc.txt',size=owned.stat().st_size,sha256=sha(owned),localReadOnly=True,processMemoryOrInput=False),indent=2)+'\n')
    local=json.loads(receipt.read_text());assert local['referenceVersion']=='3.3.93847' and local['sha256']==sha(owned) and local['readerSha256']==sha(reader)
    assert local['executableSha256']==installation['executable']['sha256'] and local['buildInfoSha256']==installation['buildInfo']['sha256']
    records=list(csv.DictReader(owned.read_text(encoding='utf-8-sig').splitlines(),delimiter='\t'));callbacks={r['code']:r['*Description'] for r in records if r['code']}
    curves=[]
    for suffix,a,b in [('12',1,2),('34',3,4),('56',5,6),('78',7,8),('91',9,10),('21',11,12),('43',13,14),('65',15,16),('87',17,18),('92',19,20)]:
        def p(n):return 'par'+str(n) if n<10 else 'pa'+str(n)
        assert callbacks['ln'+suffix]==f'{p(a)} + (lvl - 1) * {p(b)}'
        assert p(a) in callbacks['dm'+suffix] and p(b) in callbacks['dm'+suffix]
        curves.append(dict(suffix=suffix,first=a,second=b))
    directory=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((directory/'sources.json').read_text());pins={};tables={}
    for name in ['skills','missiles','states','weapons','uniqueitems','itemstatcost','charstats']:
        p=directory/f'json/{name}.json';pins[name]=sha(p);assert pins[name]==manifest['files'][f'json/{name}.json']['sha256'];tables[name]=json.loads(p.read_text())
    licensed=ROOT/'.local/research/d2r-3.3.93847/skills.txt';rows={r['skill']:r for r in csv.DictReader(licensed.read_text(encoding='utf-8-sig').splitlines(),delimiter='\t') if r.get('skill')}
    skills=[];count=0;aliases=[];representations=[];unbalanced=[]
    for number in range(373,403):
        r=tables['skills'][str(number)];current=rows[r['skill']];assert r['charclass']=='war'
        for key,value in r.items():
            if key.startswith('*') or key=='lineNumber':continue
            text=str(value)
            if text.startswith('"') and text.endswith('"'):text=text[1:-1]
            actual=current.get(key,'')
            if actual!=text:
                assert actual.strip()==text.strip(),(r['skill'],key)
                representations.append(dict(skill=r['skill'],field=key,public=text,licensed=actual,kind='outer whitespace'))
            count+=1
        skills.append(dict(id=number,key=r['skill'],start=r.get('srvstfunc',0),do=r.get('srvdofunc',0),missile=r.get('srvmissile',r.get('srvmissilea')),passive=bool(r.get('passive'))))
        for key,value in r.items():
            if isinstance(value,str) and 'calc' in key.lower() and value.count('(')!=value.count(')'):
                unbalanced.append(dict(skill=r['skill'],field=key,expression=value))
            if isinstance(value,str) and not key.startswith('*'):
                for token in set(re.findall(r'\b(?:ln|dm)(?:21|43|65|87|92)\b',value)):aliases.append(dict(skill=r['skill'],field=key,token=token))
    bind=tables['skills']['382'];assert bind['Param13']==50 and bind['Param14']==25 and bind['passivecalc9']=='ln43'
    # The user's normal-weapon readings distinguish subtraction of a truncated
    # reduction from truncation of the remaining percentage (e.g.70 ->68).
    observed=[dict(name='Ripsaw',baseCode='flb',strength=68,dexterity=48),dict(name='Rogue Bow',baseCode='cbw',strength=24,dexterity=34),dict(name='Rusthandle',baseCode='gsc',strength=36,dexterity=0,displayedLevel=18)]
    for x in observed:
        base=next(r for r in tables['weapons'].values() if r.get('code')==x['baseCode']);x['baseStrength']=base.get('reqstr',0);x['baseDexterity']=base.get('reqdex',0)
        assert x['strength']==x['baseStrength']-x['baseStrength']*4//100 and x['dexterity']==x['baseDexterity']-x['baseDexterity']*4//100
    community=ROOT/'.local/research/warlock-community/warlock-skills-notes.txt';raw=community.read_text(encoding='utf-8-sig');assert 'Warren/Kabryxis' in raw and 'October 26th, 2026' in raw
    linearEvidence=ROOT/'tools/reference/skill-linear-source-evidence.json';legacy=json.loads(linearEvidence.read_text());linear=ROOT/legacy['fixture']['path'];assert sha(linear)==legacy['fixture']['sha256']
    controls=[x for x in json.loads(linear.read_text()) if x[0]==0];assert len(controls)==2313
    fixture=ROOT/'.local/class-skills/warlock-foundation-source-probe.json'
    fixture.write_text(json.dumps(dict(referenceVersion='3.3.93847',aliases=curves,linearControls=controls),separators=(',',':'))+'\n')
    artifacts=[Path(__file__).resolve(),reader,readerSource,owned,receipt,licensed,community,fixture,linear,linearEvidence,ROOT/'tools/reference/d2r-local-installation-evidence.json']
    result=dict(referenceVersion='3.3.93847',publicCommit=manifest.get('commit',manifest.get('sourceCommit')),handlerEnabled=False,currentExecutableParity=False,
        localExtraction=local,pinnedInputHashes=pins,licensedSkillSha256=sha(licensed),playerSkills=skills,comparedPlayerFields=count,representationDifferences=representations,curveAliases=curves,extendedAliasUses=aliases,unbalancedSourceFormulaFields=unbalanced,
        observations=dict(version='3.3.93847',reportedBy='user',effectiveLevitationRank=2,hardRank=1,normalWeaponReadings=observed,rusthandleUnarmedAndEquippedMatch=True,
            established='All reported attribute values match a4percent reduction with the reduction amount truncated. A weapon need not be equipped for its observed requirement display. No item-ease/ethereal stacking model is established.',
            levelNote='Rusthandle has pinned unique level17 and a Vengeance grant; Vengeance unlocks at18. User identifies its skill grants as likely cause of observed level18; exact requirement override still needs verification.'),
        community=dict(url='https://docs.google.com/document/d/1gtM3sQ1gzo8kpmTmiJ8CQAiqp7LLu0SeaWE6fUdVg6Y/edit?tab=t.0',exportUrl='https://docs.google.com/document/d/1gtM3sQ1gzo8kpmTmiJ8CQAiqp7LLu0SeaWE6fUdVg6Y/export?format=txt',retrieved='2026-10-28',sha256=sha(community),sizeBytes=community.stat().st_size,selfAttributedAuthors='Warren/Kabryxis',latestListedRevision='2026-10-26',exactBuildStated=False,notes='Public text export succeeds although the web reader lacks the body. Self-described testing includes community reports and explicitly untested claims; revision tags include patch3.2.'),
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in artifacts},
        established=['All30current Warlock player rows match populated pinned public fields against licensed3.3.93847 skills.txt. Runtime sourceParameters must include1..20:Bind Demon requires13/14,which the old1..12projection discarded.',
            'Strict read-only local CASC skillcalc.txt pins ten named linear/diminishing pairs. ln21/dm21mean11/12,ln43/dm43mean13/14; names are registered aliases,not arbitrary digit pairs. This is current table/metadata evidence,not execution of modern callback bodies.'],
        remaining=['Diminishing arithmetic/rounding remains the existing consumer model and needs per-consumer current-build acceptance; descriptions alone do not prove its exact execution. This change fixes alias routing,not the shared diminishing curve.',
            'Malformed Warlock source expressions,implicit engine passives,new callbacks,ordinary cast rates,weapon handling and skill lifecycle remain separate. No Warlock handler/class completion or installation is claimed.',
            'The fan site now labels explanations AI-interpreted and potentially erroneous; treat them as leads. Miasma Bolt/Chain require new41/43/45/167behavior,not an element-only Sentinel copy. Levitation equip/requirement stacking and modifiers still need dedicated native/current checks.'])
    p=ROOT/'tools/reference/warlock-foundation-preflight.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert p.read_text()==text
    else:p.write_text(text)
    print(f'Warlock foundation:{count}licensed/public fields,30skills,10extended curve pairs; no handler enabled.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');p.add_argument('--extract',action='store_true');a=p.parse_args();build(a.check,a.extract)
