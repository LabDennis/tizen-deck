#!/usr/bin/env python3
"""Enroll this PC's private feed using an already authenticated TV administration API."""
import argparse
import getpass
import json
from pathlib import Path
from pc_remote import Remote


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--url', required=True, help='Native TV HTTPS URL')
    parser.add_argument('--ca', required=True, help='Existing TV certificate')
    parser.add_argument('--user', default='admin')
    parser.add_argument('--state', type=Path, default=Path(__file__).resolve().parent / '.state/release-server')
    args = parser.parse_args()
    config = json.loads((args.state / 'config.json').read_text())
    remote = Remote(args.url, args.ca)
    try:
        remote.login(args.user, getpass.getpass('Deck password: '))
        result = remote.request('/api/backend/feed', {
            'url': f'https://{config["bind"]}:{config["port"]}/',
            'certificate_sha256': config['certificate_sha256'],
            'access_token': config['access_token'],
        })
        if not all(result.get(k) for k in ('configured', 'pinned', 'authenticated')):
            raise ValueError('Feed enrollment was not confirmed')
        print('Private HTTPS feed enrolled; use the TV update menu to check packages.')
    finally:
        remote.logout()


if __name__ == '__main__':
    main()
