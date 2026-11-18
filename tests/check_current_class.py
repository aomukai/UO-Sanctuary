#!/usr/bin/env python3
"""Run a class against explicitly selected current binaries, using disposable outputs."""
import argparse
import importlib
import os
from pathlib import Path
import tempfile

ROOT = Path(__file__).resolve().parents[1]
CLASSES = ('necromancer', 'sorceress', 'paladin', 'barbarian', 'amazon', 'assassin', 'druid', 'warlock')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('character_class', choices=CLASSES)
    parser.add_argument('--runtime-root', type=Path, default=ROOT)
    parser.add_argument('--content-assembly', type=Path)
    parser.add_argument('--integration-assembly', type=Path)
    parser.add_argument('--plan', action='store_true', help='Validate required fixture inputs without starting a server.')
    args = parser.parse_args()
    runtime = args.runtime_root.resolve()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT), SANCTUARY_FIRST_LOOP_ROOT=str(ROOT),
                      SANCTUARY_TEST_SERVER=str(runtime / '.local/server'))
    cohort = importlib.import_module('check_' + args.character_class + '_lifecycle_acceptance')
    config = cohort.configuration()
    for path in config.get('extra_files', {}).values():
        if not path.is_file():
            raise FileNotFoundError('Missing current regression input: ' + str(path))
    if args.plan:
        print(f"{args.character_class}: {len(config.get('extra_files', {}))} runtime fixture inputs present; "
              'current binaries will be selected explicitly; historical runs are not used.')
        return
    from check_iron_golem_birth import check
    from check_assassin_planned_acceptance import paced_clients
    content = (args.content_assembly or runtime / '.local/content/Sanctuary.Content.dll').resolve()
    integration = (args.integration_assembly or runtime / '.local/integration/Sanctuary.Integration.dll').resolve()
    for path in (content, integration):
        if not path.is_file():
            raise FileNotFoundError('Build current test binaries first: ' + str(path))
    with tempfile.TemporaryDirectory(prefix='sanctuary-current-class-') as folder:
        config.update(stage=Path(folder), native_distribution=runtime / '.local/server',
                      content_assembly=content, integration_assembly=integration)
        with paced_clients():
            check(**config)


if __name__ == '__main__':
    main()
