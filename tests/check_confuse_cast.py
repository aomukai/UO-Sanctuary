#!/usr/bin/env python3
"""Check Confuse neutral alignment, generic override and shared AI curse/save paths."""
import sys
from pathlib import Path
from check_iron_golem_birth import ROOT, check

if __name__ == '__main__':
    if '--input-smoke' in sys.argv:
        check(stage=ROOT/'.local/class-skills/confuse-input-smoke',extra_target_input_commands=('confuse-cast-right',),
            extra_input_timeouts={'confuse-cast-right':18},extra_inputs=(Path(__file__).resolve(),))
        raise SystemExit(0)
    check(stage=ROOT/'.local/class-skills/confuse-cast',
        extra_files={
            'necromancer-ai-curse-source-cases.json':ROOT/'.local/class-skills/necromancer-ai-curse-source/cases.json',
            'mind-blast-source-probe.json':ROOT/'.local/class-skills/mind-blast-source-probe.json',
            'blind-policy-source-probe.json':ROOT/'.local/class-skills/blind-policy-source-probe.json'},
        extra_families=('confuse-cast','terror-cast','dim-vision-cast','blind-world','howl','taunt','mind-blast'),
        extra_checks=('class-skills-ai-check','blind-policy-check'),
        extra_live_commands=('blind-world-live',),
        extra_target_input_commands=('confuse-cast-right','confuse-cast-quick'),
        extra_wait_results={
            'class-skills-ai-check':'class-skills-ai-result',
            'mind-blast-check':'mind-blast-result','mind-blast-reload-check':'mind-blast-reload-result',
            'howl-check':'howl-result','howl-reload-check':'howl-result',
            'taunt-check':'taunt-result','taunt-reload-check':'taunt-result'},
        extra_manifest_names={'howl':'howl.json','taunt':'taunt.json'},
        extra_input_timeouts={'confuse-cast-right':18,'confuse-cast-quick':18},
        extra_inputs=(Path(__file__).resolve(),ROOT/'.local/class-skills/howl-source-probe.json',ROOT/'.local/class-skills/taunt-source-probe.json'))
