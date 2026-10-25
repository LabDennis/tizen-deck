# Device settings: verified boundaries

Inspected on LS32BM700UUXEN, T-NKLBWWC-1720.7, through the existing
administrator service. No network, pairing or picture settings were changed.

| Area | Observed interface | Current product behavior |
|---|---|---|
| Volume/mute | Tizen TVAudioControl, device-tested | Own Deck menu |
| Network status | Tizen SystemInfo, device-tested | Own Deck menu |
| Wi-Fi configuration | ConnMan `net.connman.Technology` | Samsung settings retained; a new connection/password agent is not implemented |
| Bluetooth | `org.projectx.bt`, binary `service_request(iiiayayayayay)` interface | Samsung pairing lists retained; not a BlueZ control interface |
| Display power | `screend.powercontrol` vendor service | Existing tested standby route retained |
| Brightness | No `/sys/class/backlight` on this model | Samsung picture controls retained |

The presence of a method or vendor library does not establish a working pairing,
brightness or Wi-Fi configuration API. A new native implementation requires
mapping the vendor request format and testing with actual peripherals. No
Bluetooth commands were guessed and no panel configuration values were written.

Privacy masks for the five supported services remain in place. `issue_report_agent`
has an explicit `Requires` dependency from `devicetracker`; removing it needs a
functional dependency test. The short live connection snapshot still showed an
outbound connection owned by `pushd`. This confirms that the five masks do not
make the whole Samsung system offline. An encrypted connection alone does not
establish what data is sent; complete tracking removal is not claimed.

A 15-second sample filtered to the monitor's outbound DNS packets and TCP SYN
flags saw only replies to Deck HTTPS sessions on the LAN. It did not inspect
the already-established encrypted push connection. This short sample cannot
prove that other transmissions never occur.
