#!/usr/bin/env python3
"""Exercise new curse families with existing learned casting/world/save suites."""
from pathlib import Path
import os
from check_iron_golem_birth import ROOT,check

if __name__=='__main__':
    os.environ['SANCTUARY_PROJECT_ROOT']=str(ROOT)
    check(stage=ROOT/'.local/class-skills/curse-ai-families',extra_files={
        'curse-ai-transitions-source.json':ROOT/'.local/class-skills/curse-ai-transitions-source/cases.json',
        'blind-policy-source-probe.json':ROOT/'.local/class-skills/blind-policy-source-probe.json',
        'necromancer-ai-curse-source-cases.json':ROOT/'.local/class-skills/necromancer-ai-curse-source/cases.json'},
        extra_families=('curse-ai-families','blind-world','dim-vision-cast','terror-cast','howl','taunt','grim-ward-transport'),
        extra_checks=('blind-policy-check',),extra_live_commands=('blind-world-live','grim-ward-transport-live'),
        extra_wait_results={'howl-check':'howl-result','howl-reload-check':'howl-result','taunt-check':'taunt-result','taunt-reload-check':'taunt-result'},
        extra_manifest_names={'howl':'howl.json','taunt':'taunt.json'},
        extra_target_input_commands=('dim-vision-cast-right','dim-vision-cast-quick','terror-cast-right','terror-cast-quick'),
        extra_input_timeouts={'dim-vision-cast-right':20,'dim-vision-cast-quick':20},
        extra_inputs=(Path(__file__).resolve(),))
