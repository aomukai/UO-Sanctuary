#!/usr/bin/env python3
"""Only the remaining native landing/broken-hand contexts; retain earlier cohorts."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT, check

if __name__ == '__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT), SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT / '.local/class-skills/barbarian-closing-edges',
          extra_checks=('barbarian-closing-edges-check',),
          extra_inputs=(Path(__file__).resolve(),), between_boot_delay=1.1)
