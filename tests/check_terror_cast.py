#!/usr/bin/env python3
"""Check learned Terror and the shared blind/Howl/Taunt curse and save paths."""
from pathlib import Path
from check_iron_golem_birth import ROOT, check

if __name__ == '__main__':
    check(stage=ROOT/'.local/class-skills/terror-cast',
        extra_files={
            'necromancer-ai-curse-source-cases.json':ROOT/'.local/class-skills/necromancer-ai-curse-source/cases.json',
            'blind-policy-source-probe.json':ROOT/'.local/class-skills/blind-policy-source-probe.json'},
        extra_families=('terror-cast','dim-vision-cast','blind-world','howl','taunt'),
        extra_checks=('class-skills-ai-check','blind-policy-check'),
        extra_live_commands=('blind-world-live',),
        extra_target_input_commands=('terror-cast-right','terror-cast-quick'),
        extra_wait_results={
            'class-skills-ai-check':'class-skills-ai-result',
            'howl-check':'howl-result','howl-reload-check':'howl-result',
            'taunt-check':'taunt-result','taunt-reload-check':'taunt-result'},
        extra_manifest_names={'howl':'howl.json','taunt':'taunt.json'},
        extra_input_timeouts={'terror-cast-right':15,'terror-cast-quick':15},
        extra_inputs=(Path(__file__).resolve(),ROOT/'.local/class-skills/howl-source-probe.json',ROOT/'.local/class-skills/taunt-source-probe.json'))
