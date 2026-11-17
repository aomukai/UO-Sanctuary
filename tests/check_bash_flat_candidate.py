#!/usr/bin/env python3
"""Exercise the explicit flat-damage proposal without enabling it on learned Bash."""
from pathlib import Path

from check_iron_golem_birth import ROOT, check

if __name__ == '__main__':
    check(stage=ROOT / '.local/class-skills/bash-flat-candidate',
          extra_checks=('bash-flat-candidate-check',),
          extra_inputs=(Path(__file__).resolve(),), between_boot_delay=1.1)
