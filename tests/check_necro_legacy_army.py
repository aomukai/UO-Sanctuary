#!/usr/bin/env python3
"""Extend the real old-writer upgrade to original warrior/four-element Mages."""
import json
from pathlib import Path
import check_necromancer_legacy_upgrade as retained

if __name__ == '__main__':
    retained.STAGE = retained.ROOT / '.local/class-skills/necro-legacy-army'
    retained.check(extra_old_commands=('necro-legacy-army-seed',), extra_new_commands=('necro-legacy-army-check',),
        extra_manifests=('necro-legacy-army-seed.json','necro-legacy-army-progress.json'),
        extra_inputs=(Path(__file__).resolve(), retained.ROOT / 'tests/Sanctuary.LegacySeed/LegacyArmySeed.cs',
            retained.ROOT / 'tests/Sanctuary.Integration/NecromancerLegacyArmyChecks.cs'), pre_build='pre-build-5.json')
    progress = json.loads((retained.STAGE / 'runtime-1/necro-legacy-army-progress.json').read_text())
    assert progress['Generation'] == 2 and len(progress['Mages']) == 4
    print('Actual earlier-writer army: original warrior and all four Mage element slots pass two current-reader boots, adopted spells/streams and retained wounds.')
