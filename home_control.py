#!/usr/bin/env python3
"""Manage Home/startup on an already prepared, device-specific installation."""
import argparse
import json
import sys


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=('query', 'restore', 'boot-enable', 'boot-restore'))
    args = parser.parse_args()
    from tv import TV
    base = '/opt/usr/home/owner/share/deck-home-control/'
    command = '/usr/bin/dotnet ' + base + 'DeckHomeControl.dll ' + args.action
    if args.action != 'query':
        command += ' ' + base + 'baseline.json'
    result = TV().sdk(command)
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0 if result['exit_code'] == 0 else 1


if __name__ == '__main__':
    try:
        sys.exit(main())
    except (OSError, RuntimeError, ValueError) as error:
        print(str(error), file=sys.stderr)
        sys.exit(1)
