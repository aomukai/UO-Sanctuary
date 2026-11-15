#!/usr/bin/env python3
"""Real native Lich difficulty deaths, explosions, helper controls and reloads."""
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    check(stage=ROOT/'.local/class-skills/native-corpse-difficulties',extra_families=('raised-minions','native-corpse','native-corpse-difficulties','corpse-base'),
        extra_files={'corpse-base-life-source-probe.json':ROOT/'.local/class-skills/corpse-base-life-source-probe.json'},
        extra_checks=('raised-minions-live-check',),extra_connected_checks=('raised-minions-live-check',),
        extra_wait_results={'raised-minions-live-check':'raised-minions-live-result','corpse-base-check':'corpse-base-result','corpse-base-reload-check':'corpse-base-result'},
        extra_target_input_commands=('native-corpse-ce-right','native-corpse-poison-quick','native-corpse-nightmare-ce-right','native-corpse-hell-poison-quick'),
        extra_input_timeouts={name:12 for name in('native-corpse-ce-right','native-corpse-poison-quick','native-corpse-nightmare-ce-right','native-corpse-hell-poison-quick')},
        extra_inputs=(Path(__file__).resolve(),))
