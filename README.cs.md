# Přepínač tiskárny

**[English](README.md)**

Malá aplikace pro Windows, která na chvíli „přenese“ USB tiskárnu ze síťového
tiskového serveru (třeba Raspberry Pi s CUPS) do tvého počítače, jako by byla
připojená kabelem, a jedním kliknutím ji zase vrátí do sítě.

K čemu to je? Spousta tiskáren ukáže stav toneru, údržbu nebo aktualizaci
firmwaru jen v softwaru výrobce pro Windows – a ten chce tiskárnu na USB.
Přepínač tiskárny udělá ze síťové tiskárny místní USB zařízení na tak dlouho,
jak potřebuješ.

<p align="center">
  <img src="docs/flyout.png" width="380" alt="Panel nad oznamovací oblastí">
</p>

- **Bydlí v oznamovací oblasti.** Kliknutím na ikonu otevřeš panel se stavem
  a jedním velkým tlačítkem.
- **Ví, co se děje.** Zelená – tiskárna je v síti, oranžová – je připojená k tomuto
  PC, červená – má ji jiný počítač. Ikona se obnovuje sama.
- **Tiskárnu nikdy nenechá viset.** Když se cokoli pokazí v půlce, tiskárna se hned
  vrátí CUPS. Při ukončení aplikace s připojenou tiskárnou se nejdřív zeptá.
- **Průvodce nastavením.** Najde tiskový server v síti, nastaví přihlašování
  klíčem, připraví server a do PC nainstaluje ovladač USB/IP.
- **Nativní vzhled Windows 11**, světlý i tmavý režim, čeština a angličtina.

## Jak to funguje

```
 Počítač s Windows                            Tiskový server (Raspberry Pi …)
┌──────────────────────┐   SSH (port 22)    ┌──────────────────────────────┐
│ Přepínač tiskárny    │ ═════════════════► │ printswitch share            │
│   ovladač usbip-win2 │   USB/IP uvnitř    │   zastaví CUPS a nabídne     │
│   127.0.0.1:3240 ────┼── SSH tunelu ─────►│   tiskárnu přes usbipd       │
└──────────────────────┘                    └──────────────┬───────────────┘
                                                           │ USB
                                                       tiskárna
```

1. Aplikace přes SSH požádá server, aby zastavil CUPS a nabídl tiskárnu přes `usbipd`.
2. Otevře SSH tunel a zařízení přes něj připojí ovladačem
   [usbip-win2](https://github.com/vadimgrn/usbip-win2). Windows vidí USB tiskárnu.
3. Vrácení zařízení odpojí, zavře tunel a znovu spustí CUPS.

Protokol USB/IP nemá přihlašování ani šifrování, proto nikdy nejde přímo po síti:
server po dobu zapůjčení zahazuje port 3240 na všech rozhraních kromě loopbacku
a veškerý provoz jde uvnitř SSH.

## Co je potřeba

- Windows 10 (20H1) nebo Windows 11, 64bitové
- Linuxový tiskový server s CUPS a tiskárnou na USB – vyzkoušeno na Raspberry Pi OS
  (Debian 13), jiné systémy odvozené od Debianu by měly fungovat; server potřebuje balík `usbip` (průvodce ho nainstaluje přes apt)
- Přístup na server přes SSH (jednou jméno a heslo, nebo existující klíč)

## Instalace

1. Stáhni `PrinterSwitch-Setup-x.y.z.exe` z [Releases](../../releases) a spusť ho.
   Instaluje se jen pro aktuálního uživatele, bez oprávnění správce.
   *Instalátor zatím není digitálně podepsaný, takže SmartScreen může varovat: Další informace → Přesto spustit.*
2. Spustí se průvodce nastavením:
   - **Tiskový server** – najdi ho v síti nebo zadej adresu a přihlas se. Když přihlášení
     klíčem ještě nefunguje, zadáš jednou heslo a průvodce klíč nastaví (heslo se neukládá).
   - **Příprava serveru** – nainstaluje pomocný skript a balík usbip, vybereš tiskárnu.
   - **Tento počítač** – nainstaluje ovladač usbip-win2. Je podepsaný Microsoftem,
     Secure Boot ani testovací režim se nemění. Po první instalaci ovladače je potřeba
     jednou restartovat Windows.
3. Klikni na ikonu u hodin a stiskni **Připojit k tomuto PC**.

## Co průvodce změní na serveru

| Co | Kde |
|---|---|
| Pomocný skript (zdroj: [`server/printswitch`](server/printswitch)) | `/usr/local/sbin/printswitch` |
| Pravidlo pro sudo, které bez hesla dovolí **jen** tento skript | `/etc/sudoers.d/printswitch` |
| Balík `usbip` a jeho jaderné moduly | apt, `/etc/modules-load.d/usbip.conf` |
| Veřejný klíč aplikace (jen když jsi zadal heslo) | `~/.ssh/authorized_keys` |

Vrácení zpět: `sudo rm /usr/local/sbin/printswitch /etc/sudoers.d/printswitch`.

Po dobu zapůjčení existuje na serveru soubor `/run/printserver-usbip.busid`.
Monitoring podle něj pozná „zapůjčeno schválně“ od „CUPS spadl“.

## Když něco nejde

| Problém | Řešení |
|---|---|
| „Tiskárna není na serveru vidět“ | Zapni ji, zkontroluj USB kabel. Na serveru `printswitch devices` vypíše, co vidí. |
| Připojení v PC selže | Po instalaci usbip-win2 restartuj Windows. Ve Správci zařízení musí být *USBip 3.X Emulated Host Controller*. |
| Tiskárna zůstala zapůjčená po pádu | V aplikaci **Vrátit do sítě**, nebo na serveru `sudo printswitch unshare`. |
| Síťový tisk se nevrátil | `sudo systemctl restart cups`; občas pomůže přepojit USB kabel. |
| Změnil se klíč serveru | Spusť znovu průvodce, ukáže nový otisk a zeptá se. |

Záznam je v `%LOCALAPPDATA%\PrinterSwitch\log.txt` (nabídka ikony → Zobrazit záznam),
nastavení v `%APPDATA%\PrinterSwitch\settings.json`.

## Sestavení ze zdrojů

Potřebuješ [.NET 10 SDK](https://dotnet.microsoft.com/download) a
[Inno Setup 6](https://jrsoftware.org/isinfo.php) (`winget install JRSoftware.InnoSetup`).

```powershell
.\build.ps1 -Version 1.0.0      # -> dist\PrinterSwitch-Setup-1.0.0.exe
```

## Poděkování a licence

- [usbip-win2](https://github.com/vadimgrn/usbip-win2) – ovladač USB/IP pro Windows (GPL-3.0, stahuje se zvlášť, není přibalený)
- [SSH.NET](https://github.com/sshnet/SSH.NET) (MIT)

Přepínač tiskárny je pod [licencí MIT](LICENSE).
