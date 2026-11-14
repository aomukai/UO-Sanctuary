#!/usr/bin/env python3
"""Real ordered Goatman contacts with original saved stream/body controls."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    names=('goatman-npc-contact-check','goatman-npc-contact-reload-check')
    check(stage=ROOT/'.local/class-skills/goatman-npc-contact',extra_families=('goatman-npc-contact','goatman-npc-body-action'),
          extra_checks=('goatman-npc-action-check',),extra_connected_checks=names,
          extra_wait_results={n:n+'-result'for n in names},extra_connection_settle=.05,extra_inputs=(Path(__file__).resolve(),))
