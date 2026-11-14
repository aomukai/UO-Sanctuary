#!/usr/bin/env python3
"""Native room/search projection through real source decisions and timed hits."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    names=tuple(f'{family}-{suffix}'for family in ('goatman-native-search','goatman-native-combat')for suffix in ('check','reload-check'))
    check(stage=ROOT/'.local/class-skills/goatman-native-search',extra_families=('goatman-native-search','goatman-native-combat'),
          extra_connected_checks=names,extra_wait_results={n:n+'-result'for n in names},extra_connection_settle=.05,extra_inputs=(Path(__file__).resolve(),))
