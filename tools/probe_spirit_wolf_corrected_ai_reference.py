#!/usr/bin/env python3
"""Pin and execute the separately corrected Spirit Wolf reconstruction."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
COMMIT = '7f6d773f31060e97124241e6bd30d606bcab6d4e'
BLOB = '89e229d5b21550edcaf03bb9c22166e8548e9850'
RELATIVE = 'source/D2Game/src/AI/AiThink.cpp'


def routine(text, name):
    match = re.search(r'^void __fastcall ' + name + r'\([^\n]+\)\s*\n\{', text, re.M); assert match
    end, depth = text.index('{', match.start()) + 1, 1
    while depth:
        depth += (text[end] == '{') - (text[end] == '}'); end += 1
    return text[match.start():end]


def build(check=False):
    path = BASE / 'd2moo-corrections' / RELATIVE; raw = path.read_bytes()
    assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == BLOB
    name = 'AITHINK_Fn108_SpiritWolf'; body = routine(raw.decode(), name)
    old = routine((BASE / 'd2moo-pinned-source' / RELATIVE).read_text(), name)
    renames = dict(sub_6FCE34E0='AITHINK_TryPetFollowOwner', sub_6FCCFD40='AITACTICS_FindTargetInRange',
                   sub_6FCF2110='AIUTIL_FindAiTarget', sub_6FCF14D0='AIUTIL_HasClearApproachToTarget')
    normalized = old
    for before, after in renames.items():
        normalized = normalized.replace(before, after)
    assert normalized.replace('if (pAiTickParam->pMonstatsTxt)', 'if (bCombat)') == body
    source = (BASE / 'spirit-wolf-ai-source-probe.cpp').read_text()
    start = source.index('void __fastcall ' + name); old_body = routine(source, name)
    prefix = source[:start]
    prefix = prefix.replace('int params[5]', 'int ownerTargetDistance=0;int params[5]')
    prefix = prefix.replace('int UNITS_GetDistanceToOtherUnit(D2UnitStrc*,D2UnitStrc*){return ownerDistance;}',
        'int UNITS_GetDistanceToOtherUnit(D2UnitStrc*a,D2UnitStrc*b){return a==&unit||b==&unit?ownerDistance:ownerTargetDistance;}')
    assert 'return a==&unit' in prefix
    aliases = ''.join('#define ' + after + ' ' + before + '\n' for before, after in renames.items())
    tail = source[start + len(old_body):source.index('int main()')]
    assert tail.count('],%d,%d,[') == 1
    tail = tail.replace('],%d,%d,[', ',%d,%d,%d],%d,%d,[')
    tail = tail.replace('teleport,fighting,control.dwAiParam[2]', 'teleport,fighting,ownerTargetDistance,monster.nVelocity,monster.nRun,control.dwAiParam[2]')
    main = source[source.index('int main(){printf("[");') + len('int main(){printf("[");'):source.index('printf("]\\n");}')]
    cpp = prefix + aliases + body + tail + 'int main(){printf("[");monster.nVelocity=5;monster.nRun=10;\n'
    cpp += 'for(int targetDistance:{19,20,21}){ownerTargetDistance=targetDistance;\n' + main + '\n}\n'
    cpp += r'''for(int distance:{9,10,11,14})for(int p:{0,1})for(int combat:{0,1})for(int velocity:{0,5})for(int run:{0,5,9,10,100})for(int targetDistance:{19,20,21})for(int chance:{0,99}){
ownerTargetDistance=targetDistance;monster.nVelocity=velocity;monster.nRun=run;emit(4,1,0,1,0,p,0,1,1,19,distance,1,0,0,chance,1,combat);
}
printf("]\n");}
'''
    generated = BASE / 'spirit-wolf-corrected-ai-source-probe.cpp'; generated.write_text(cpp)
    binary = generated.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(generated), '-o', str(binary)], check=True)
    result = subprocess.run([str(binary)], capture_output=True)
    (BASE / 'spirit-wolf-corrected-ai-sanitizer.log').write_bytes(result.stderr)
    assert result.returncode == 0 and not result.stderr, result.stderr.decode()[-4000:]
    cases = json.loads(result.stdout); assert len(cases) == 6366, len(cases)
    assert any(not r[1][15] and any(a[0] == 9 for a in r[4]) for r in cases)
    assert any(not r[1][15] and any(a[0] == 7 for a in r[4]) for r in cases)
    fixture = BASE / 'spirit-wolf-corrected-ai-source-probe.json'; fixture.write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', supplementaryCommit=COMMIT, sourceBlob=BLOB,
        sourceUrl=f'https://github.com/ThePhrozenKeep/D2MOO/blob/{COMMIT}/{RELATIVE}',
        sourcePath=str(path.relative_to(ROOT)), sourceSha256=hashlib.sha256(raw).hexdigest(),
        routineSha256=hashlib.sha256(body.encode()).hexdigest(), sourceChanges=dict(symbolRenames=renames, behavior='Only nonnull-monstats versus bCombat attack/chase condition differs in this complete routine after normalizing the listed symbol renames.'),
        generatedSourceSha256=hashlib.sha256(cpp.encode()).hexdigest(),
        reusedServiceSourceSha256=hashlib.sha256(source.encode()).hexdigest(),
        fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), cases=6366),
        sanitizers='ASan/UBSan clean', installed=False, learnedHandler=False,
        established=[
            'The separate pinned reconstruction corrects the exact suspect SpiritWolf attack/chase branch to bCombat. This is a source-backed correction; the older pinned routine and its1802literal contexts remain intact for comparison. D2R mechanical reference remains3.3.93847.',
            'Complete corrected routine executes6366contexts: three owner-target distances over the prior1802contexts plus960owner-distance9/10/11/14, target/combat, velocity0/5, run0/5/9/10/100 and chance controls. Request ordering, remembered owner and random-draw count are captured.',
            'In combat it requests A1/stall22. Otherwise an owner-target distance>=20selects idle15within owner distance10or walk-radius8/6outside it; a closer owner-target selects velocity then run-to-target. Run bonus caps at100, keeps negative raw ratios and returns100when base velocity<=0. Existing fallback LOS polarity and follow/teleport branches are unchanged.'
        ], limits=[
            'This pins a corrected older-engine reconstruction, not live D2R code. Target/visibility/distance/room/tactics/AI/event/RNG services remain declared as in the earlier probe. Exact current executable, global scheduling and geometric behavior remain unverified.',
            'Common movement outputs are stubbed for this outer-routine proof; complete common/six-mode movement has independent source/native evidence. No actual saved actor follow/chase/teleport/damage/learned cast is inferred.',
            'The source fallback still requires the negated potential-target clear-approach helper. Current semantics remain uncertain; the corrected combat condition does not establish that separate branch.'
        ])
    destination = ROOT / 'tools/reference/spirit-wolf-corrected-ai-source-evidence.json'; encoded = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert destination.read_text() == encoded
    else:
        destination.write_text(encoded)
    print('Corrected Spirit Wolf AI:6366complete source contexts; separately pinned combat/chase correction, unchanged current D2R reference.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
