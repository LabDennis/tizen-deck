# Independent boot research — tested M7, 2026-10-25

Target: LS32BM700UUXEN / T-NKLBWWC-1720.7 / Tizen 6.5, ARMv7.
The Home mapping and launcher autostart already survive a full reboot. The native
root backend does not: its systemd unit is currently created in `/run`.

A related-family reference reports a boot-time .NET app using localhost SDB and
the archive-launcher root route. Source reviewed at commit
`2b0629381b3eaf5a78f84332d630d6d4b10681e7`:

- https://github.com/chris-ritsen/samsung-tv-root/issues/1
- https://github.com/levkropp/samsung-tv-root-untethered/tree/untethered-boot-agent

This is reference evidence, not validation on our M7. That implementation uses
an existing Samsung application registration and needs the Developer Host IP to
be localhost. Changing that IP would remove the current direct PC SDB recovery
route; no such change was made here.

## Isolated own-app experiment

A headless ServiceApplication proof was built against the extracted device's own
Tizen assemblies. It only attempted to write its identity/boot ID and exit.

- Ordinary SDK TPK installation failed with Security error -22.
- Dedicated package/app rows plus a dedicated security-manager registration did
  not produce a running app, including after a full reboot with on-boot enabled.
- No boot proof was written. The exact app-start rejection is still unresolved.
- The dedicated app security registration, package rows and test files were
  removed afterward; a database snapshot was retained privately.
- Existing Samsung app binaries were not changed by these experiments.

## Temporary registered-service proof

With explicit approval, only the DLL of the unused Samsung Gaming Hub service
was temporarily replaced with the same minimal proof adapted to its existing
application identity. Autostart remained **false** and the Developer Host IP was
not changed. A shell EXIT/signal trap and an independent host-side finally block
both restored the original DLL.

- The one-shot launch succeeded on this M7.
- The proof ran as UID 5001, SMACK `User::Pkg::com.samsung.tv.ghservice`.
- It recorded the current boot ID and exited; this was **not a root proof** or a
  cold-boot test of the registered-service route.
- The original DLL was restored and its SHA-256 verified successfully, including
  an independent second readback. The database still reported on-boot `false`.
- The original DLL/runtime configuration/manifest and checksums are retained
  privately on the monitor. No Samsung binary or firmware dump is in this repo.

This identifies a viable application launch context, but does not yet establish
an independent root bootstrap. No replacement service was left installed.

Do not use the test as an installation recipe. A viable approach still needs a
verified launch chain, reversible installation, bounded attempts, recovery access
when localhost SDB is enabled, and successful cold-boot tests without a PC.
The reference's broad writable markers and arbitrary root module runner have not
been adopted. No firmware, bootloader, or boot partition was flashed.
