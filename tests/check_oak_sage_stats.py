#!/usr/bin/env python3
"""Oak Sage numeric preparation plus retained Creeper/Wolf/Raven reload acceptance."""
import check_creeper_stats as retained


if __name__ == '__main__':
    retained.STAGE = retained.ROOT / '.local/class-skills/oak-sage'
    retained.check(extra_commands=['oak-sage-stats-check', 'oak-sage-body-check', 'battle-orders-check'],
                   extra_reload_commands=['oak-sage-body-reload-check', 'battle-orders-reload-check'],
                   extra_probes=['oak-sage', 'oak-aura'], extra_fixtures=['oak-sage-body-fixtures.json'])
    print('Oak Sage numeric and saved body/life-aura checks pass with retained three-boot/two-reload consumers; Totem movement and learned casting remain unavailable.')
