#!/usr/bin/env python3
"""Ordinary learned native Lich Revive in all three difficulties."""
from pathlib import Path
from check_iron_golem_birth import ROOT,check
MODES=('normal-right','normal-quick','nightmare-right','hell-quick')
CORPSES=('ce-right','poison-quick','nightmare-ce-right','hell-poison-quick')
if __name__=='__main__':
    check(stage=ROOT/'.local/class-skills/native-lich-revive-cast',extra_families=('raised-minions','native-corpse','native-corpse-difficulties','native-lich-revive','corpse-base'),
        extra_files={'corpse-base-life-source-probe.json':ROOT/'.local/class-skills/corpse-base-life-source-probe.json'},
        extra_checks=('raised-minions-live-check','native-lich-revive-live-check'),extra_connected_checks=('raised-minions-live-check','native-lich-revive-live-check'),
        extra_wait_results={'raised-minions-live-check':'raised-minions-live-result','native-lich-revive-live-check':'native-lich-revive-live-result','corpse-base-check':'corpse-base-result','corpse-base-reload-check':'corpse-base-result'},
        extra_target_input_commands=tuple('native-corpse-'+mode for mode in CORPSES)+tuple('native-lich-revive-'+mode for mode in MODES),
        extra_input_timeouts={**{'native-corpse-'+mode:12 for mode in CORPSES},**{'native-lich-revive-'+mode:12 for mode in MODES}},extra_inputs=(Path(__file__).resolve(),))
