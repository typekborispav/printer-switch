# Third-party notices

Printer Switch is © 2026 typekborispav and licensed under the
[GNU General Public License v3.0 or later](LICENSE).

It is built on work by other people. This file lists every third-party component,
how it is used and under which licence. The full licence texts are in the
[`licenses`](licenses) folder, and the installer copies them next to the program.

## Bundled in the Windows installer

| Component | Author | Licence | Used for |
|---|---|---|---|
| [.NET runtime and Windows Desktop runtime](https://github.com/dotnet/runtime) (incl. WPF, Windows Forms, System.Management, System.CodeDom, Microsoft.Extensions.*) | .NET Foundation and Contributors, Microsoft | MIT – [licenses/dotnet-LICENSE.txt](licenses/dotnet-LICENSE.txt), third-party parts: [licenses/dotnet-THIRD-PARTY-NOTICES.txt](licenses/dotnet-THIRD-PARTY-NOTICES.txt) | runtime the app runs on (self-contained) |
| [SSH.NET](https://github.com/sshnet/SSH.NET) | Renci, Oleg Kapeljushnik, Gert Driesen and contributors | MIT – [licenses/SSH.NET-LICENSE.txt](licenses/SSH.NET-LICENSE.txt) | SSH connection and tunnel to the print server |
| [Bouncy Castle for .NET](https://www.bouncycastle.org/) | The Legion of the Bouncy Castle Inc. | MIT – [licenses/BouncyCastle-LICENSE.txt](licenses/BouncyCastle-LICENSE.txt) | cryptography used by SSH.NET |

## Used to build the installer (not part of the program)

| Component | Author | Licence |
|---|---|---|
| [Inno Setup](https://jrsoftware.org/isinfo.php) | Jordan Russell, Martijn Laan | [Inno Setup License](https://jrsoftware.org/files/is/license.txt) – the installer is created with Inno Setup |

## Downloaded on request, not bundled

| Component | Author | Licence | How |
|---|---|---|---|
| [usbip-win2](https://github.com/vadimgrn/usbip-win2) | vadimgrn and contributors | GPL-3.0 | When the user asks for it, the setup wizard downloads the official release from GitHub (version pinned, SHA-256 verified) and starts its own installer. Printer Switch only calls its command-line tool `usbip.exe`. |

## Installed on the print server from the distribution's repositories

The setup wizard can install these Debian / Raspberry Pi OS packages with `apt`.
They come straight from the distribution's repositories under their own licences;
Printer Switch does not redistribute them.

| Package | Licence |
|---|---|
| `usbip` (Linux USB/IP tools) | GPL-2.0 |
| `cups`, `cups-bsd` | Apache-2.0 |
| `avahi-daemon` | LGPL-2.1 |
| printer drivers, depending on the printer: `printer-driver-brlaser`, `printer-driver-hpcups`, `printer-driver-escpr`, `printer-driver-splix`, `printer-driver-gutenprint`, `printer-driver-postscript-hp` | GPL-2.0 or compatible, see each package |
| `unattended-upgrades` | GPL-2.0 |

## Fonts and icons

The application icon and installer images are original artwork drawn by
[`tools/make-icon.ps1`](tools/make-icon.ps1). At runtime the app shows symbols
from the Segoe Fluent Icons / Segoe MDL2 Assets fonts that ship with Windows;
the fonts themselves are not redistributed.

## Trademarks

Raspberry Pi is a trademark of Raspberry Pi Ltd. Windows is a trademark of the
Microsoft group of companies. Brother, HP, Epson, Canon and other printer brands
are trademarks of their respective owners. Printer Switch is an independent
project and is not affiliated with or endorsed by any of them.
