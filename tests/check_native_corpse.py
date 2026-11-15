#!/usr/bin/env python3
"""Explicit native Lich corpse support with existing mapped-corpse mechanics."""
from pathlib import Path
from check_iron_golem_birth import ROOT,check

if __name__=='__main__':
    check(stage=ROOT/'.local/class-skills/native-corpse',extra_families=('native-corpse','corpse-base'),
        extra_files={'corpse-base-life-source-probe.json':ROOT/'.local/class-skills/corpse-base-life-source-probe.json'},
        extra_wait_results={'corpse-base-check':'corpse-base-result','corpse-base-reload-check':'corpse-base-result'},
        extra_target_input_commands=('native-corpse-ce-right','native-corpse-poison-quick'),
        extra_input_timeouts={'native-corpse-ce-right':12,'native-corpse-poison-quick':12},
        extra_inputs=(Path(__file__).resolve(),))
