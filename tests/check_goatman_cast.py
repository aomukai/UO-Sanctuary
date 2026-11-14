#!/usr/bin/env python3
"""Approved learned Goatman:real input,automatic saved AI and native-host regression."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    names=tuple(f'{family}-{suffix}'for family in ('goatman-cast','goatman-native-host')for suffix in ('check','reload-check'))
    check(stage=ROOT/'.local/class-skills/goatman-cast',extra_families=('goatman-cast','goatman-native-host','goatman-body'),
          extra_connected_checks=names,extra_wait_results={n:n+'-result'for n in names},extra_connection_settle=.05,
          extra_target_input_commands=('goatman-cast-right','goatman-cast-quick'),
          extra_inputs=(Path(__file__).resolve(),ROOT/'tools/reference/goatman-native-host-approval.json'))
