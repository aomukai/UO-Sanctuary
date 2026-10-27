#!/usr/bin/env python3
"""Oak Sage numeric preparation plus retained Creeper/Wolf/Raven reload acceptance."""
import check_creeper_stats as retained


if __name__ == '__main__':
    retained.STAGE = retained.ROOT / '.local/class-skills/oak-sage'
    retained.check(extra_commands=['oak-sage-stats-check'], extra_probes=['oak-sage'])
    print('Oak Sage numeric preparation passes with retained three-boot/two-reload consumers; no Oak actor/lease/learned handler claimed.')
