#!/usr/bin/env python3
"""Complete connected Goatman host candidate and native-search regression."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    names=tuple(f'{family}-{suffix}'for family in ('goatman-native-host','goatman-native-search')for suffix in ('check','reload-check'))
    check(stage=ROOT/'.local/class-skills/goatman-native-host',extra_families=('goatman-native-host','goatman-native-search'),
          extra_connected_checks=names,extra_wait_results={n:n+'-result'for n in names},extra_connection_settle=.05,extra_inputs=(Path(__file__).resolve(),))
