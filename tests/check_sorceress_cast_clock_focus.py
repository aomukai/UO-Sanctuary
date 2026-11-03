#!/usr/bin/env python3
"""Focused Sorceress SC/FCR starts and original Teleport resource/scroll reloads."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/sorceress-cast-clock-focus',
        extra_files={'sorceress-cast-clock-source-probe.json':ROOT/'.local/class-skills/sorceress-cast-clock-source-probe.json'},
        extra_checks=('sorceress-cast-clock-check',),extra_families=('player-teleport',),
        extra_connected_checks=('player-teleport-reload-check',),extra_wait_results={'player-teleport-reload-check':'player-teleport-reload-result'},
        save_command='player-teleport-save',save_result='player-teleport-save',
        extra_ground_input_commands=('player-teleport-left','player-teleport-right','player-teleport-quick'),
        extra_target_input_commands=('sorceress-cast-clock-right','sorceress-cast-clock-quick','player-teleport-left','player-teleport-right','player-teleport-quick'),extra_inputs=(Path(__file__).resolve(),))
