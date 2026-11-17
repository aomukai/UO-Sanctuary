#!/usr/bin/env python3
"""Run the two omitted birth-snapshot families on the installed class package."""
from pathlib import Path

from check_iron_golem_birth import ROOT, check


def configuration():
    return dict(stage=ROOT / '.local/class-skills/necro-pet-snapshot-refresh',
                extra_families=('warrior-snapshot', 'necro-snapshot'),
                extra_inputs=(Path(__file__).resolve(),), between_boot_delay=1.1)


if __name__ == '__main__':
    check(**configuration())
