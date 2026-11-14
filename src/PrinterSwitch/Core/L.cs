using System.Globalization;
using System.Windows.Markup;

namespace PrinterSwitch.Core;

/// <summary>Czech and English texts. Kept in code so the app stays one simple binary.</summary>
public static class L
{
    public static bool Czech { get; private set; }

    public static void Init(string language)
    {
        Czech = language switch
        {
            "cs" => true,
            "en" => false,
            _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName is "cs" or "sk"
        };
    }

    public static string T(string key)
        => Texts.TryGetValue(key, out var t) ? (Czech ? t.Cs : t.En) : key;

    public static string F(string key, params object[] args)
        => string.Format(Czech ? CultureInfo.GetCultureInfo("cs-CZ") : CultureInfo.InvariantCulture, T(key), args);

    static readonly Dictionary<string, (string Cs, string En)> Texts = new()
    {
        ["app.name"] = ("Přepínač tiskárny", "Printer Switch"),

        // ---- states
        ["state.unknown.t"] = ("Zjišťuji stav…", "Checking…"),
        ["state.unknown.d"] = ("", ""),
        ["state.notconfigured.t"] = ("Ještě není nastaveno", "Not set up yet"),
        ["state.notconfigured.d"] = ("Průvodce nastavením zabere pár minut.", "The setup wizard takes a few minutes."),
        ["state.unreachable.t"] = ("Tiskový server neodpovídá", "Print server is not responding"),
        ["state.network.t"] = ("Tiskárna je v síti", "Printer is on the network"),
        ["state.network.d"] = ("Tisknout může kdokoli v síti.", "Anyone on the network can print."),
        ["state.cupsdown.t"] = ("Tisková služba neběží", "Print service is not running"),
        ["state.cupsdown.d"] = ("Tiskárna není zapůjčená, ale CUPS na serveru je zastavený.",
                                "The printer is not lent out, but CUPS on the server is stopped."),
        ["state.usbhere.t"] = ("Tiskárna je připojená k tomuto PC", "Printer is attached to this PC"),
        ["state.usbhere.d"] = ("Chová se jako USB tiskárna. Ostatní teď netisknou.",
                               "It works like a USB printer. Nobody else can print right now."),
        ["state.usbelsewhere.t"] = ("Tiskárnu má jiný počítač", "Another computer has the printer"),
        ["state.usbelsewhere.d"] = ("Dokud ji nevrátí, nikdo v síti netiskne.",
                                    "Nobody on the network can print until it is given back."),

        ["busy.connecting"] = ("Připojuji tiskárnu k tomuto PC…", "Attaching the printer to this PC…"),
        ["busy.returning"] = ("Vracím tiskárnu do sítě…", "Giving the printer back to the network…"),
        ["busy.printer"] = ("Přidávám tiskárnu do Windows…", "Adding the printer to Windows…"),

        // ---- actions
        ["action.connect"] = ("Připojit k tomuto PC", "Attach to this PC"),
        ["action.return"] = ("Vrátit do sítě", "Give back to the network"),
        ["action.forcereturn"] = ("Vrátit do sítě i tak", "Give back to the network anyway"),
        ["action.cups"] = ("Správa tiskárny (CUPS)", "Printer administration (CUPS)"),
        ["action.addprinter"] = ("Přidat síťovou tiskárnu do Windows", "Add the network printer to Windows"),
        ["action.settings"] = ("Nastavení…", "Settings…"),
        ["action.setup"] = ("Spustit průvodce", "Run setup"),
        ["action.log"] = ("Zobrazit záznam", "Show log"),
        ["action.refresh"] = ("Obnovit stav", "Refresh"),
        ["action.exit"] = ("Ukončit", "Exit"),
        ["action.cancel"] = ("Zrušit", "Cancel"),
        ["action.yes"] = ("Ano", "Yes"),
        ["action.no"] = ("Ne", "No"),

        ["confirm.connect"] = ("Tiskárna se připojí k tomuto počítači jako USB zařízení.\n\nDokud ji nevrátíš, nikdo jiný v síti nemůže tisknout.",
                               "The printer will be attached to this computer as a USB device.\n\nUntil you give it back, nobody else on the network can print."),
        ["confirm.force"] = ("Tiskárnu má zapůjčenou jiný počítač. Když ji vrátíš odsud, zůstane mu nefunkční zařízení.\n\nPřesto vrátit?",
                             "Another computer has the printer. If you give it back from here, that computer is left with a dead device.\n\nGive it back anyway?"),
        ["confirm.exit"] = ("Tiskárna je pořád připojená k tomuto PC a nikdo jiný netiskne.\n\nVrátit ji před ukončením do sítě?",
                            "The printer is still attached to this PC and nobody else can print.\n\nGive it back to the network before exiting?"),

        ["ok.attached"] = ("Připojeno: {0}. Teď můžeš spustit software tiskárny.",
                           "Attached: {0}. You can now start the printer's software."),
        ["ok.attachednodevice"] = ("Připojeno. Windows ještě instalují ovladač, chvíli to potrvá.",
                                   "Attached. Windows is still installing the driver, give it a moment."),
        ["ok.returned"] = ("Tiskárna je zpátky v síti.", "The printer is back on the network."),
        ["notify.taken"] = ("Tiskárnu si zapůjčil jiný počítač.", "Another computer has borrowed the printer."),
        ["notify.failed"] = ("Nepovedlo se", "That did not work"),

        // ---- errors
        ["err.hostkey"] = ("Klíč serveru se změnil. Buď byl server přeinstalován, nebo se za něj někdo vydává. Spusť znovu průvodce nastavením.",
                           "The server's host key has changed. Either it was reinstalled or something is impersonating it. Run the setup wizard again."),
        ["err.auth"] = ("Server odmítl přihlášení.", "The server rejected the login."),
        ["err.hostnotfound"] = ("Server s tímto jménem nelze najít.", "No server with this name was found."),
        ["err.refused"] = ("Server odmítl spojení (běží na něm SSH?).", "The server refused the connection (is SSH running?)."),
        ["err.unreachable"] = ("Server je nedostupný.", "The server cannot be reached."),
        ["err.badreply"] = ("Server odpověděl něco nečekaného.", "Unexpected reply from the server."),
        ["err.nousbip"] = ("V tomto PC chybí ovladač usbip-win2. Nainstaluješ ho v průvodci nastavením.",
                           "The usbip-win2 driver is not installed on this PC. The setup wizard can install it."),
        ["err.notprepared"] = ("Server ještě není připravený. Spusť průvodce nastavením.",
                               "The server is not prepared yet. Run the setup wizard."),
        ["err.portbusy"] = ("Místní port {0} už používá jiný program.", "Local port {0} is already used by another program."),
        ["err.share"] = ("Server tiskárnu nezapůjčil.", "The server did not lend the printer."),
        ["err.nodevice"] = ("Tiskárna není na serveru vidět. Je zapnutá a připojená USB kabelem?\n\nNic se nepřepnulo, síťový tisk běží dál.",
                            "The printer is not visible on the server. Is it switched on and connected by USB?\n\nNothing was switched; network printing keeps working."),
        ["err.nomodule"] = ("Na serveru se nepodařilo načíst jaderný modul usbip_host. Po aktualizaci jádra obvykle pomůže restart serveru.",
                            "The server could not load the usbip_host kernel module. After a kernel update, restarting the server usually helps."),
        ["err.attach"] = ("Připojení zařízení v tomto PC selhalo. Tiskárna byla vrácena do sítě.",
                          "Attaching the device on this PC failed. The printer was given back to the network."),
        ["err.unshare"] = ("Vrácení na serveru hlásí chybu.", "Giving it back reported an error on the server."),
        ["usbip.badhash"] = ("Stažený soubor nesouhlasí s očekávaným kontrolním součtem, nespustil jsem ho.",
                             "The downloaded file does not match the expected checksum; it was not run."),

        ["printer.added"] = ("Síťová tiskárna byla přidána do Windows.", "The network printer was added to Windows."),
        ["printer.needfeature"] = ("K připojení síťové tiskárny potřebují Windows funkci „Klient internetového tisku“.\n\nZapnout ji teď? Vyžádá si oprávnění správce.",
                                   "Windows needs the optional feature \"Internet Printing Client\" to connect the network printer.\n\nTurn it on now? This asks for administrator rights."),
        ["printer.failed"] = ("Tiskárnu se nepodařilo přidat (chyba {0}). Zkus Nastavení → Bluetooth a zařízení → Tiskárny a skenery.",
                              "The printer could not be added (error {0}). Try Settings → Bluetooth & devices → Printers & scanners."),
        ["printer.noqueue"] = ("Na serveru jsem nenašel žádnou tiskovou frontu.", "No print queue was found on the server."),

        // ---- wizard
        ["wiz.title"] = ("Nastavení – Přepínač tiskárny", "Printer Switch setup"),
        ["wiz.back"] = ("Zpět", "Back"),
        ["wiz.next"] = ("Další", "Next"),
        ["wiz.finish"] = ("Dokončit", "Finish"),
        ["wiz.step1"] = ("Vítejte", "Welcome"),
        ["wiz.step2"] = ("Tiskový server", "Print server"),
        ["wiz.step3"] = ("Příprava serveru", "Prepare server"),
        ["wiz.step4"] = ("Tento počítač", "This PC"),
        ["wiz.step5"] = ("Hotovo", "Done"),

        ["wiz.welcome.h"] = ("Tiskárna v síti – a když je potřeba, jako přes USB",
                             "A network printer – and a USB one when you need it"),
        ["wiz.welcome.p1"] = ("Přepínač tiskárny umí na chvíli přenést USB tiskárnu z tiskového serveru (třeba Raspberry Pi s CUPS) do tohoto počítače, jako by byla připojená kabelem. Hodí se pro software výrobce: stav toneru, údržbu, aktualizace.",
                              "Printer Switch temporarily moves a USB printer from your print server (for example a Raspberry Pi running CUPS) to this computer, as if it were plugged in with a cable. Handy for the manufacturer's software: toner levels, maintenance, firmware updates."),
        ["wiz.welcome.need"] = ("Co budeš potřebovat", "What you need"),
        ["wiz.welcome.b1"] = ("Linuxový tiskový server s CUPS a tiskárnou na USB", "A Linux print server with CUPS and a USB printer"),
        ["wiz.welcome.b2"] = ("Přihlášení na server přes SSH (jméno a heslo nebo klíč)", "SSH access to the server (user name and password or key)"),
        ["wiz.welcome.b3"] = ("Ovladač usbip-win2 v tomto PC – průvodce ho umí nainstalovat", "The usbip-win2 driver on this PC – the wizard can install it"),

        ["wiz.server.h"] = ("Kde je tiskový server?", "Where is the print server?"),
        ["wiz.server.p"] = ("Zadej adresu serveru a uživatele, pod kterým se na něj přihlašuješ přes SSH.",
                            "Enter the server's address and the user you log in with over SSH."),
        ["wiz.find.hint"] = ("Nevíš adresu? Zkus server najít v síti.", "Don't know the address? Search the network."),
        ["wiz.find.button"] = ("Najít v síti", "Search network"),
        ["wiz.find.searching"] = ("Hledám tiskové servery…", "Looking for print servers…"),
        ["wiz.find.pick"] = ("Nalezeno – vyber server:", "Found – pick a server:"),
        ["wiz.find.none"] = ("Nic jsem nenašel. Zadej adresu ručně.", "Nothing found. Enter the address manually."),
        ["wiz.server.host"] = ("Adresa (IP nebo jméno)", "Address (IP or host name)"),
        ["wiz.server.user"] = ("Uživatel", "User"),
        ["wiz.server.port"] = ("Port", "Port"),
        ["wiz.server.connect"] = ("Připojit", "Connect"),
        ["wiz.server.connecting"] = ("Připojuji…", "Connecting…"),
        ["wiz.server.ok"] = ("Připojeno jako {0}.", "Connected as {0}."),
        ["wiz.server.fp"] = ("Otisk klíče serveru: SHA256:{0}", "Server key fingerprint: SHA256:{0}"),
        ["wiz.server.keyused"] = ("Přihlašuji se klíčem {0}.", "Logging in with key {0}."),
        ["wiz.server.needpw"] = ("Klíčem se přihlásit nejde. Zadej heslo – použije se jen jednou k nastavení přihlašování klíčem a nikam se neukládá.",
                                 "Key login does not work yet. Enter the password – it is used once to set up key login and is never stored."),
        ["wiz.server.password"] = ("Heslo", "Password"),
        ["wiz.server.setupkey"] = ("Nastavit přihlášení klíčem", "Set up key login"),
        ["wiz.server.missing"] = ("Vyplň adresu i uživatele.", "Fill in both the address and the user."),

        ["wiz.prep.h"] = ("Příprava serveru", "Prepare the server"),
        ["wiz.prep.p"] = ("Na server se nainstaluje malý pomocný skript /usr/local/sbin/printswitch, balík usbip a pravidlo pro sudo, které dovoluje bez hesla spouštět jen tento skript.",
                          "This installs a small helper script /usr/local/sbin/printswitch, the usbip package and a sudo rule that allows running only that script without a password."),
        ["wiz.prep.cups"] = ("Tisková služba CUPS", "CUPS print service"),
        ["wiz.prep.helper"] = ("Pomocný skript a pravidlo pro sudo", "Helper script and sudo rule"),
        ["wiz.prep.usbip"] = ("Balík usbip", "usbip package"),
        ["wiz.prep.ok"] = ("v pořádku", "OK"),
        ["wiz.prep.missing"] = ("chybí", "missing"),
        ["wiz.prep.notrunning"] = ("neběží", "not running"),
        ["wiz.prep.checking"] = ("zjišťuji…", "checking…"),
        ["wiz.prep.device"] = ("Tiskárna", "Printer"),
        ["wiz.prep.auto"] = ("Automaticky (první USB tiskárna)", "Automatic (first USB printer)"),
        ["wiz.prep.nodevice"] = ("Na serveru teď nevidím žádnou USB tiskárnu – je zapnutá? Klidně pokračuj, vybere se sama při připojení.",
                                 "No USB printer is visible on the server right now – is it switched on? You can continue; it is picked automatically when attaching."),
        ["wiz.prep.queue"] = ("Tisková fronta", "Print queue"),
        ["wiz.prep.run"] = ("Připravit server", "Prepare server"),
        ["wiz.prep.rerun"] = ("Připravit znovu", "Prepare again"),
        ["wiz.prep.running"] = ("Připravuji server… instalace balíku může trvat i pár minut.",
                                "Preparing the server… installing the package can take a few minutes."),
        ["wiz.prep.sudopw"] = ("Heslo pro sudo", "sudo password"),
        ["wiz.prep.sudoneeded"] = ("Server chce pro sudo heslo. Zadej ho – použije se jen teď a neukládá se.",
                                   "The server asks for a sudo password. Enter it – it is used only now and never stored."),
        ["wiz.prep.done"] = ("Server je připravený.", "The server is ready."),
        ["wiz.prep.failed"] = ("Přípravu se nepodařilo dokončit:", "Preparing the server failed:"),
        ["wiz.prep.skipwarn"] = ("Server není připravený, takže tiskárnu nepůjde připojit k tomuto PC. Přesto pokračovat?",
                                 "The server is not prepared, so the printer cannot be attached to this PC. Continue anyway?"),

        ["wiz.pc.h"] = ("Ovladač USB/IP v tomto počítači", "USB/IP driver on this PC"),
        ["wiz.pc.p"] = ("Aby se tiskárna v tomto PC objevila jako USB zařízení, je potřeba open-source ovladač usbip-win2. Je podepsaný Microsoftem, Secure Boot ani testovací režim se měnit nemusí.",
                        "For the printer to show up on this PC as a USB device, the open-source usbip-win2 driver is needed. It is signed by Microsoft; Secure Boot and test mode stay as they are."),
        ["wiz.pc.installed"] = ("Nainstalováno: usbip-win2 {0}", "Installed: usbip-win2 {0}"),
        ["wiz.pc.bad"] = ("Verze {0} má známou chybu, která může shodit Windows modrou obrazovkou. Doporučuji nainstalovat {1}.",
                          "Version {0} has a known bug that can crash Windows with a blue screen. Installing {1} is recommended."),
        ["wiz.pc.missing"] = ("usbip-win2 není nainstalovaný. Bez něj půjde jen sledovat stav a vracet tiskárnu do sítě.",
                              "usbip-win2 is not installed. Without it you can only watch the state and give the printer back."),
        ["wiz.pc.install"] = ("Stáhnout a nainstalovat usbip-win2 {0}", "Download and install usbip-win2 {0}"),
        ["wiz.pc.downloading"] = ("Stahuji… {0:P0}", "Downloading… {0:P0}"),
        ["wiz.pc.installing"] = ("Běží instalátor ovladače – potvrď ho prosím.", "The driver installer is running – please confirm it."),
        ["wiz.pc.restart"] = ("Hotovo. Po první instalaci ovladače je potřeba restartovat Windows.",
                              "Done. Windows needs a restart after the driver is installed for the first time."),
        ["wiz.pc.installfailed"] = ("Instalace se nepovedla: {0}", "Installation failed: {0}"),
        ["wiz.pc.source"] = ("Zdroj: github.com/vadimgrn/usbip-win2 · stažený soubor se ověřuje kontrolním součtem",
                             "Source: github.com/vadimgrn/usbip-win2 · the download is verified against a checksum"),
        ["wiz.pc.optional"] = ("Tento krok můžeš přeskočit a vrátit se k němu později.", "You can skip this step and come back later."),

        ["wiz.done.h"] = ("Hotovo", "All set"),
        ["wiz.done.p"] = ("Přepínač poběží v oznamovací oblasti vedle hodin. Klikni na jeho ikonu a tiskárnu přepneš jedním tlačítkem.",
                          "Printer Switch lives in the notification area next to the clock. Click its icon to switch the printer with one button."),
        ["wiz.done.autostart"] = ("Spouštět po přihlášení do Windows", "Start when I sign in to Windows"),
        ["wiz.done.promote"] = ("Zobrazit ikonu přímo na hlavním panelu, ne pod šipkou", "Show the icon on the taskbar, not hidden behind the arrow"),
        ["wiz.done.language"] = ("Jazyk", "Language"),
        ["wiz.done.printer.h"] = ("Běžný síťový tisk z tohoto PC", "Regular network printing from this PC"),
        ["wiz.done.printer.p"] = ("Volitelné: přidá do Windows tiskárnu ze serveru, aby šlo tisknout i v síťovém režimu.",
                                  "Optional: adds the server's printer to Windows so you can print in network mode too."),
        ["wiz.lang.auto"] = ("Podle Windows", "Same as Windows"),
        ["wiz.lang.note"] = ("Změna jazyka se projeví po restartu aplikace.", "A language change applies after restarting the app."),

        ["flyout.lastcheck"] = ("Zkontrolováno {0:HH:mm}", "Checked at {0:HH:mm}"),
    };
}

/// <summary>XAML: Text="{core:Tr wiz.next}"</summary>
public sealed class TrExtension(string key) : MarkupExtension
{
    public string Key { get; set; } = key;
    public override object ProvideValue(IServiceProvider serviceProvider) => L.T(Key);
}
