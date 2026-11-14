#!/usr/bin/env python3
"""Real timed Goatman attacks, source walking and decision-root regression."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    names=('goatman-native-combat-check','goatman-native-combat-reload-check')
    check(stage=ROOT/'.local/class-skills/goatman-native-combat',extra_families=('goatman-native-combat','goatman-native-walking','goatman-native-decision'),
          extra_connected_checks=names,extra_wait_results={n:n+'-result'for n in names},extra_connection_settle=.05,extra_inputs=(Path(__file__).resolve(),))
