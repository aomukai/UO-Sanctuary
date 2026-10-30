#!/usr/bin/env python3
"""Run the prepared saved Iron Golem body with retained graph/original/resource checks."""
from pathlib import Path
from check_iron_golem_birth import ROOT, check


if __name__ == '__main__':
    check(stage=ROOT / '.local/class-skills/iron-golem-body',
        extra_files={'iron-golem-birth-graph-source-cases.json': ROOT / '.local/class-skills/iron-golem-birth-graph-source/cases.json',
            'iron-golem-body-source-cases.json': ROOT / '.local/class-skills/iron-golem-body-source/cases.json'},
        extra_families=('iron-golem-birth-graph', 'iron-golem-body'), extra_inputs=(Path(__file__).resolve(),))
