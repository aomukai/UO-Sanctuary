#!/usr/bin/env python3
"""Run equipped graph birth plus retained original-item/resource scopes."""
from pathlib import Path
from check_iron_golem_birth import ROOT, check


if __name__ == '__main__':
    check(stage=ROOT / '.local/class-skills/iron-golem-birth-graph',
        extra_files={'iron-golem-birth-graph-source-cases.json': ROOT / '.local/class-skills/iron-golem-birth-graph-source/cases.json'},
        extra_families=('iron-golem-birth-graph',), extra_inputs=(Path(__file__).resolve(),))
