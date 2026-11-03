#!/usr/bin/env python3
"""Tainted life/owner/fire hit with original Goatman and Hydra regression."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/tainted-fireball-focus',
        extra_files={'tainted-formula-source-cases.json':ROOT/'.local/class-skills/tainted-foundation-source/formula.json',
                     'tainted-fireball-source-cases.json':ROOT/'.local/class-skills/tainted-foundation-source/fireball.json',
                     'goatman-creation-source-probe.json':ROOT/'.local/class-skills/goatman-creation-source-probe.json'},
        extra_families=('tainted-fireball','tainted-body','tainted-foundation','goatman-body','hydra-world'),
        extra_connected_checks=('tainted-fireball-reload-check','hydra-world-reload-check',),
        extra_wait_results={'tainted-fireball-reload-check':'tainted-fireball-reload-result','hydra-world-reload-check':'hydra-world-reload-result'},
        extra_target_input_commands=('hydra-world-right','hydra-world-quick'),extra_inputs=(Path(__file__).resolve(),))
