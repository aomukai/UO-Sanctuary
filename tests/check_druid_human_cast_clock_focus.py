#!/usr/bin/env python3
"""Focused Druid human SC with retained form transactions and shared cooldowns."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/druid-human-cast-clock-focus',
        extra_files={'druid-form-cast-clock-source-probe.json':ROOT/'.local/class-skills/druid-form-cast-clock-source-probe.json'},
        extra_checks=('druid-human-cast-clock-check','druid-form-delay-live-check'),extra_families=('druid-form-casting',),
        extra_wait_results={'druid-form-delay-live-check':'druid-form-delay-result'},
        extra_target_input_commands=('druid-human-cast-clock-right','druid-human-cast-clock-quick'),extra_inputs=(Path(__file__).resolve(),))
