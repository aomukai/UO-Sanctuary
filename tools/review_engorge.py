#!/usr/bin/env python3
"""Authenticate Engorge's owned numerical suppliers and tooltip meanings."""
import argparse,hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
    ref=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((ref/'sources.json').read_text())
    paths=[ref/'json'/n for n in ('skills.json','skilldesc.json','allstrings-eng.json')]
    for p in paths:assert sha(p)==manifest['files'][str(p.relative_to(ref))]['sha256']
    row=json.loads(paths[0].read_text())['379'];owned=json.loads(paths[0].read_text())['378']
    generated=next(r for r in json.loads((ROOT/'data/Sanctuary/Balance/class-skills.json').read_text())if r['sourceId']==379)
    assert generated['sourceFields']=={k:v for k,v in row.items()if not k.startswith('*')and k!='lineNumber'}
    assert all(generated['sourceParameters'][str(i)]==row.get('Param'+str(i),0)for i in range(1,21))
    desc=next(r for r in json.loads(paths[1].read_text()).values()if r.get('skilldesc')=='engorge');strings=json.loads(paths[2].read_text())
    assert (row['srvstfunc'],row['srvdofunc'],row['calc1'],row['auralencalc'])==(17,161,'ln34','ln56')
    assert desc['desctexta4']=='StrSkillHealPercentDemon'and desc['desccalca4']=='ln34'and strings[desc['desctexta4']]=="Heal Demon's Life: %+d%%"
    assert desc['desctexta3']=='EskillLifeSteal'and desc['desccalca3']=='ln21'
    assert desc['desccalca1']=='usmc'and desc['desccalcb1']==256 and desc['desccalcb2']==25
    assert owned['Param11']==25 and owned['Param12']==5
    capture=ROOT/'.local/class-skills/warlock-runtime-reference/warlock-do-161-20261108/capture.json';saved=json.loads(capture.read_text());candidate=saved['records'][0]
    assert saved['referenceVersion']=='3.3.93847'and len(candidate['fragments'])==7 and not any(f['captured']for f in candidate['fragments'])
    files=[*paths,ref/'sources.json',ROOT/'data/Sanctuary/Balance/class-skills.json',capture,Path(__file__).resolve()]
    result=dict(referenceVersion='3.3.93847',component='Engorge numerical supplier and owned tooltip review',skill=379,start=17,do=161,
        fields={k:v for k,v in row.items()if k.startswith(('aurastat','aura','calc','Param'))},
        tooltip={k:desc[k]for k in ('desctexta3','desccalca3','desctexta4','desccalca4','desccalca1','desccalcb1','desccalca2','desccalcb2')},
        sourceCandidate=dict(rva=candidate['rva'],fragments=7,readableCodeBytes=0),newLiveReferenceReads=0,
        externalReferences=[dict(url='https://diabloforfans.com/skill/engorge?lang=enUS',kind='Author datamined reference',reportedVersion='3.3.93847',observedOn='2026-11-15',scope='Corroborates35attack-speed cap,flat5physical resistance,and refresh rather than stacking;not executable do161 proof.'),
            dict(url='https://upload3.inven.co.kr/upload/2026/03/02/bbs/WarlockSkillsNotesandFunctionality.pdf',kind='Original Warren/Kabryxis tested notes',version='Unspecified older upload',scope='Corroborates35IASfrom21 and refresh-only;does not establish modern raw drain/stat installation or recipient ordering.')],
        established=['Owned tooltip explicitly binds calc1/ln34 to percent maximum demon life healed;it is not a life cost.',
            'Use existing SkillValues for fractional mana,duration,healing,5physical resistance,raw equal drain endpoints,capped attackrate and hard-BloodOath flat armor/raw hpregen.',
            'Soft Engorge points affect its own payload;only hard Blood Oath points feed armor and regeneration. Capture immutable values without rewriting original summon birth context.'],
        unresolved=['Current do161 remains unreadable in all seven saved fragments. Corpse eligibility/consumption,recipient membership/order/range,release/payment and state installation/lifecycle need their own verified or approved rule.',
            'Tooltip calls ln21 percent Life Steal,but existing ordinary non-hireling NPC fill treats lifedrainmin/max as flat resource amounts. Do not silently substitute player percentage leech or assume the modern binder/fill matches the older NPC path.',
            'Keep hpregen as a raw source stat. No percent-heal-over-time interpretation,stat consumer,automatic state or learned handler is enabled by numerical capture.'],
        inputs={str(p.relative_to(ROOT)):sha(p)for p in files})
    out=ROOT/'tools/reference/engorge-source-review.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==text,'Engorge source review stale'
    else:out.write_text(text)
    print('Engorge379:owned skill/tooltip/strings authenticated;calc1 is healing percent;raw NPC drain conflict explicit;no live read.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
