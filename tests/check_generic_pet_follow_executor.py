#!/usr/bin/env python3
"""Current follow executor and retained saved Goatman/GenericPet contracts."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    probes=('goatman-formula','goatman-creation','generic-pet-follow-executor','generic-pet-decisions','generic-pet-movement','generic-pet-parameters')
    files={n+'-source-probe.json':ROOT/'.local/class-skills'/(n+'-source-probe.json') for n in probes}
    files['goatman-tooltip-observation.json']=ROOT/'.local/class-skills/goatman-tooltip-observation.json'
    check(stage=ROOT/'.local/class-skills/generic-pet-follow-executor',extra_files=files,
        extra_families=('goatman-body','goatman-foundation'),
        extra_checks=tuple(n+'-check' for n in ('generic-pet-follow-executor','generic-pet-decisions','generic-pet-movement','generic-pet-parameters')),
        extra_inputs=(Path(__file__).resolve(),))
