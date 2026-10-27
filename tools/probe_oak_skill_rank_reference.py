#!/usr/bin/env python3
"""Pin current experience ceiling and execute complete older NPC skill-rank getters."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(local_root, installation, check=False):
    prior = json.loads((ROOT / 'tools/reference/d2r-local-installation-evidence.json').read_text())
    assert sha(installation / 'D2R.exe') == prior['executable']['sha256']
    assert sha(installation / '.build.info') == prior['buildInfo']['sha256']
    experience = local_root / 'experience-oak-verified.txt'
    rows = list(csv.DictReader(experience.read_text(encoding='utf-8-sig').splitlines(), delimiter='\t'))
    maximum = next(r for r in rows if r['Level'] == 'MaxLvl')
    classes = ['Amazon', 'Sorceress', 'Necromancer', 'Paladin', 'Barbarian', 'Druid', 'Assassin', 'Warlock']
    assert all(maximum[c] == '99' for c in classes)
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes, routines = {}, {}

    def source(path):
        raw = (STAGE / 'd2moo-pinned-source' / path).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == path)
        hashes[path] = hashlib.sha256(raw).hexdigest(); return raw.decode()

    def routine(text, name):
        match = re.search(r'^.*\b' + name + r'\([^;\n]*\)\s*\n\{', text, re.M); assert match, name
        end, depth = text.index('{', match.start()) + 1, 1
        while depth:
            depth += (text[end] == '{') - (text[end] == '}'); end += 1
        body = text[match.start():end]; routines[name] = hashlib.sha256(body.encode()).hexdigest(); return body

    common = source('source/D2Common/src/D2Skills.cpp')
    cpp = (STAGE / 'battle-command-source-probe.cpp').read_text().split('int main()', 1)[0]
    for name in ['SKILLS_GetBonusSkillLevel', 'SKILLS_GetSkillLevel']:
        assert routine(common, name) == routine(cpp, name)
    cap = routine(source('source/D2Common/src/DataTbls/DataTbls.cpp'), 'DATATBLS_GetMaxLevel')
    assign = routine(source('source/D2Game/src/SKILLS/Skills.cpp'), 'D2GAME_SetSkills_6FD14C60')
    assert 'SKILLS_AssignSkill(pUnit, nSkillId, nSkillLevel, bRemove' in assign
    assert 'pSkill->nOwnerGUID = -1;' in common
    cpp += r'''
int main(){bool first=true;printf("[");
for(int rank=1;rank<=255;rank++)for(int command:{0,1})for(int owner:{-1,9})for(int bonuses:{0,1}){
 D2UnitStrc unit;unit.dwUnitType=UNIT_MONSTER;unit.stats[STAT_ITEM_ALLSKILLS]=command;
 rows[0].nSkillId=0;rows[0].nCharClass=-1;rows[0].nEType=0;D2SkillStrc skill{&rows[0],rank,0,owner};
 int result=SKILLS_GetSkillLevel(&unit,&skill,bonuses);
 if(!first)printf(",");first=false;printf("[%d,%d,%d,%d,%d]",rank,command,owner,bonuses,result);
}printf("]\n");}
'''
    path = STAGE / 'oak-skill-rank-source-probe.cpp'; path.write_text(cpp)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    result = subprocess.run([str(binary)], capture_output=True, check=True); assert not result.stderr
    cases = json.loads(result.stdout); assert len(cases) == 2040
    for rank, bonus, owner, enabled, value in cases:
        assert value == min(99, rank + (bonus if owner == -1 and enabled else 0))
    fixture = STAGE / 'oak-skill-rank-source-probe.json'; fixture.write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], handlerEnabled=False, installed=False,
        sourceHashes=hashes, routineHashes=routines,
        experience=dict(archivePath='data:data/global/excel/experience.txt', localFilename=experience.name, sha256=sha(experience),
            maximumLevels={c: int(maximum[c]) for c in classes}, scope='Current local MaxLvl row; older getter requests class0maximum.'),
        executableSha256=prior['executable']['sha256'], buildInfoSha256=prior['buildInfo']['sha256'],
        fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=sha(fixture), generatedSourceSha256=sha(path), cases=len(cases), sanitizers='ASan+UBSan'),
        established=[
            'Complete older bonus/level getters add item_allskills for monsters as well as players. Innate non-item-owned skills use the bonus path; item-owned and bonus-disabled controls do not. The skill getter clamps through DATATBLS_GetMaxLevel(0),whose current local MaxLvl field is99.',
            'Ordinary granted Oak aura starts from the birth sumskill rank. Battle Command supplies item_allskills1through the existing state. Its next periodic pulse reads the effective rank; birth life/defense and base aura rank remain immutable.',
            '2040unchanged getter contexts cover supplied base ranks1..255,Command0/1,item-owned controls and bonus enabled/disabled. Current-executable getter/event equivalence is not certified by older source.'
        ], services=[
            'Reuse the bounded Battle Command skill/stat ABI and supply no NPC gear,shrine,oskill,single-skill or elemental skill bonuses. The two getters execute unchanged. Class0experience maximum99is a supplied current-table value; the table getter is reviewed,not compiled in this probe.',
            'Current pulse/replacement/expiry and actual Command application/removal/save consumers need separate native acceptance. The existing same-state weaker-rank refusal must not be bypassed on Command expiry.'
        ])
    output = ROOT / 'tools/reference/oak-skill-rank-engine-evidence.json'; rendered = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print('Oak innate rank:2040complete getter contexts and current MaxLvl99pin; actual Command aura lifecycle acceptance remains separate.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--local-root', type=Path, required=True)
    parser.add_argument('--installation', type=Path, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    build(args.local_root, args.installation, args.check)
