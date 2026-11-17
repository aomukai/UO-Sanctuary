#!/usr/bin/env python3
"""Focused safe-area admission for the ten actual prepared Druid summon types."""
from pathlib import Path

from check_iron_golem_birth import ROOT, check

if __name__ == '__main__':
    check(stage=ROOT / '.local/class-skills/druid-safe-area',
          extra_families=('druid-safe-area',),
          extra_inputs=(Path(__file__).resolve(),), between_boot_delay=1.1)
