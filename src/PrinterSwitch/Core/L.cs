// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 typekborispav

using System.Globalization;
using System.Windows.Markup;

namespace PrinterSwitch.Core;

/// <summary>
/// User interface texts. Every entry lists the languages in the order of
/// <see cref="Codes"/>; a missing or empty translation falls back to English.
/// To add a language: add its code to Codes and Languages and one more string
/// to each entry (or leave it out and English is used).
/// </summary>
public static class L
{
    public static readonly string[] Codes = ["en", "cs", "de", "sk", "pl"];

    /// <summary>Choices offered to the user, with each language's own name.</summary>
    public static readonly (string Code, string Name)[] Languages =
    [
        ("en", "English"),
        ("cs", "Čeština"),
        ("de", "Deutsch"),
        ("sk", "Slovenčina"),
        ("pl", "Polski"),
    ];

    static int index;
    public static string Code => Codes[index];

    /// <summary>"" follows Windows; unknown Windows languages get English.</summary>
    public static void Init(string language)
    {
        string code = language.Length > 0 ? language : CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        index = Math.Max(0, Array.IndexOf(Codes, code));
    }

    public static string T(string key)
    {
        if (!Texts.TryGetValue(key, out var t)) return key;
        return index < t.Length && t[index].Length > 0 ? t[index] : t[0];
    }

    public static string F(string key, params object[] args)
        => string.Format(CultureInfo.GetCultureInfo(Code), T(key), args);

    //                                  en, cs, de, sk, pl
    static readonly Dictionary<string, string[]> Texts = new()
    {
        ["app.name"] = ["Printer Switch", "Přepínač tiskárny", "Druckerumschalter", "Prepínač tlačiarne", "Przełącznik drukarki"],

        // ------------------------------------------------------------ states
        ["state.unknown.t"] = ["Checking…", "Zjišťuji stav…", "Status wird geprüft…", "Zisťujem stav…", "Sprawdzanie…"],
        ["state.unknown.d"] = ["", "", "", "", ""],
        ["state.notconfigured.t"] = ["Not set up yet", "Ještě není nastaveno", "Noch nicht eingerichtet", "Ešte nie je nastavené", "Jeszcze nie skonfigurowano"],
        ["state.notconfigured.d"] = ["The setup wizard takes a few minutes.", "Průvodce nastavením zabere pár minut.", "Der Einrichtungsassistent dauert nur ein paar Minuten.", "Sprievodca nastavením zaberie pár minút.", "Kreator konfiguracji zajmie kilka minut."],
        ["state.unreachable.t"] = ["Print server is not responding", "Tiskový server neodpovídá", "Druckserver antwortet nicht", "Tlačový server neodpovedá", "Serwer wydruku nie odpowiada"],
        ["state.unreachable.d"] = ["", "", "", "", ""],
        ["state.network.t"] = ["Printer is on the network", "Tiskárna je v síti", "Drucker ist im Netzwerk", "Tlačiareň je v sieti", "Drukarka jest w sieci"],
        ["state.network.d"] = ["Anyone on the network can print.", "Tisknout může kdokoli v síti.", "Jeder im Netzwerk kann drucken.", "Tlačiť môže ktokoľvek v sieti.", "Każdy w sieci może drukować."],
        ["state.cupsdown.t"] = ["Print service is not running", "Tisková služba neběží", "Druckdienst läuft nicht", "Tlačová služba nebeží", "Usługa drukowania nie działa"],
        ["state.cupsdown.d"] = ["The printer is not lent out, but CUPS on the server is stopped.", "Tiskárna není zapůjčená, ale CUPS na serveru je zastavený.", "Der Drucker ist nicht ausgeliehen, aber CUPS auf dem Server ist gestoppt.", "Tlačiareň nie je zapožičaná, ale CUPS na serveri je zastavený.", "Drukarka nie jest wypożyczona, ale CUPS na serwerze jest zatrzymany."],
        ["state.usbhere.t"] = ["Printer is attached to this PC", "Tiskárna je připojená k tomuto PC", "Drucker ist mit diesem PC verbunden", "Tlačiareň je pripojená k tomuto PC", "Drukarka jest podłączona do tego komputera"],
        ["state.usbhere.d"] = ["It works like a USB printer. Nobody else can print right now.", "Chová se jako USB tiskárna. Ostatní teď netisknou.", "Er verhält sich wie ein USB-Drucker. Niemand sonst kann gerade drucken.", "Správa sa ako USB tlačiareň. Ostatní teraz netlačia.", "Działa jak drukarka USB. Nikt inny nie może teraz drukować."],
        ["state.usbelsewhere.t"] = ["Another computer has the printer", "Tiskárnu má jiný počítač", "Ein anderer Computer hat den Drucker", "Tlačiareň má iný počítač", "Drukarkę ma inny komputer"],
        ["state.usbelsewhere.d"] = ["Nobody on the network can print until it is given back.", "Dokud ji nevrátí, nikdo v síti netiskne.", "Bis er zurückgegeben wird, kann im Netzwerk niemand drucken.", "Kým ju nevráti, nikto v sieti netlačí.", "Dopóki nie zostanie oddana, nikt w sieci nie drukuje."],

        ["busy.connecting"] = ["Attaching the printer to this PC…", "Připojuji tiskárnu k tomuto PC…", "Drucker wird mit diesem PC verbunden…", "Pripájam tlačiareň k tomuto PC…", "Podłączanie drukarki do tego komputera…"],
        ["busy.returning"] = ["Giving the printer back to the network…", "Vracím tiskárnu do sítě…", "Drucker wird ans Netzwerk zurückgegeben…", "Vraciam tlačiareň do siete…", "Oddawanie drukarki do sieci…"],

        // ----------------------------------------------------------- actions
        ["action.connect"] = ["Attach to this PC", "Připojit k tomuto PC", "Mit diesem PC verbinden", "Pripojiť k tomuto PC", "Podłącz do tego komputera"],
        ["action.return"] = ["Give back to the network", "Vrátit do sítě", "Ans Netzwerk zurückgeben", "Vrátiť do siete", "Oddaj do sieci"],
        ["action.forcereturn"] = ["Give back to the network anyway", "Vrátit do sítě i tak", "Trotzdem ans Netzwerk zurückgeben", "Aj tak vrátiť do siete", "Mimo to oddaj do sieci"],
        ["action.cups"] = ["Printer administration (CUPS)", "Správa tiskárny (CUPS)", "Druckerverwaltung (CUPS)", "Správa tlačiarne (CUPS)", "Administracja drukarką (CUPS)"],
        ["action.addprinter"] = ["Add the network printer to Windows", "Přidat síťovou tiskárnu do Windows", "Netzwerkdrucker zu Windows hinzufügen", "Pridať sieťovú tlačiareň do Windows", "Dodaj drukarkę sieciową do Windows"],
        ["action.settings"] = ["Settings…", "Nastavení…", "Einstellungen…", "Nastavenia…", "Ustawienia…"],
        ["action.setup"] = ["Run setup", "Spustit průvodce", "Einrichtung starten", "Spustiť sprievodcu", "Uruchom kreator"],
        ["action.log"] = ["Show log", "Zobrazit záznam", "Protokoll anzeigen", "Zobraziť záznam", "Pokaż dziennik"],
        ["action.refresh"] = ["Refresh", "Obnovit stav", "Aktualisieren", "Obnoviť stav", "Odśwież"],
        ["action.exit"] = ["Exit", "Ukončit", "Beenden", "Ukončiť", "Zakończ"],
        ["action.about"] = ["About", "O aplikaci", "Über", "O aplikácii", "O programie"],
        ["about.notices"] = ["Licences and components", "Licence a součásti", "Lizenzen und Komponenten", "Licencie a súčasti", "Licencje i komponenty"],
        ["about.text"] = [
            "© 2026 typekborispav. Free software under the GNU GPL v3 or later: you may use, share and modify it; modified versions must stay free under the same licence.\n\nBuilt with .NET, SSH.NET and Bouncy Castle (MIT); uses the usbip-win2 driver (GPL-3.0). Details in Licences and components.",
            "© 2026 typekborispav. Svobodný software pod licencí GNU GPL v3 nebo novější: můžeš ho používat, sdílet i upravovat; upravené verze musí zůstat svobodné pod stejnou licencí.\n\nPostaveno na .NET, SSH.NET a Bouncy Castle (MIT); používá ovladač usbip-win2 (GPL-3.0). Podrobnosti v Licence a součásti.",
            "© 2026 typekborispav. Freie Software unter der GNU GPL v3 oder neuer: Du darfst sie nutzen, weitergeben und ändern; geänderte Versionen müssen unter derselben Lizenz frei bleiben.\n\nGebaut mit .NET, SSH.NET und Bouncy Castle (MIT); nutzt den Treiber usbip-win2 (GPL-3.0). Details unter Lizenzen und Komponenten.",
            "© 2026 typekborispav. Slobodný softvér pod licenciou GNU GPL v3 alebo novšou: môžeš ho používať, zdieľať aj upravovať; upravené verzie musia zostať slobodné pod rovnakou licenciou.\n\nPostavené na .NET, SSH.NET a Bouncy Castle (MIT); používa ovládač usbip-win2 (GPL-3.0). Podrobnosti v Licencie a súčasti.",
            "© 2026 typekborispav. Wolne oprogramowanie na licencji GNU GPL v3 lub nowszej: możesz go używać, udostępniać i modyfikować; zmienione wersje muszą pozostać wolne na tej samej licencji.\n\nZbudowane na .NET, SSH.NET i Bouncy Castle (MIT); używa sterownika usbip-win2 (GPL-3.0). Szczegóły w Licencje i komponenty."],
        ["action.cancel"] = ["Cancel", "Zrušit", "Abbrechen", "Zrušiť", "Anuluj"],
        ["action.yes"] = ["Yes", "Ano", "Ja", "Áno", "Tak"],
        ["action.no"] = ["No", "Ne", "Nein", "Nie", "Nie"],

        ["confirm.connect"] = [
            "The printer will be attached to this computer as a USB device.\n\nUntil you give it back, nobody else on the network can print.",
            "Tiskárna se připojí k tomuto počítači jako USB zařízení.\n\nDokud ji nevrátíš, nikdo jiný v síti nemůže tisknout.",
            "Der Drucker wird als USB-Gerät mit diesem Computer verbunden.\n\nBis du ihn zurückgibst, kann niemand sonst im Netzwerk drucken.",
            "Tlačiareň sa pripojí k tomuto počítaču ako USB zariadenie.\n\nKým ju nevrátiš, nikto iný v sieti nemôže tlačiť.",
            "Drukarka zostanie podłączona do tego komputera jako urządzenie USB.\n\nDopóki jej nie oddasz, nikt inny w sieci nie może drukować."],
        ["confirm.force"] = [
            "Another computer has the printer. If you give it back from here, that computer is left with a dead device.\n\nGive it back anyway?",
            "Tiskárnu má zapůjčenou jiný počítač. Když ji vrátíš odsud, zůstane mu nefunkční zařízení.\n\nPřesto vrátit?",
            "Ein anderer Computer hat den Drucker. Gibst du ihn von hier zurück, bleibt dort ein totes Gerät.\n\nTrotzdem zurückgeben?",
            "Tlačiareň má zapožičanú iný počítač. Keď ju vrátiš odtiaľto, zostane mu nefunkčné zariadenie.\n\nPredsa vrátiť?",
            "Drukarkę ma inny komputer. Jeśli oddasz ją stąd, tamten zostanie z niedziałającym urządzeniem.\n\nMimo to oddać?"],
        ["confirm.exit"] = [
            "The printer is still attached to this PC and nobody else can print.\n\nGive it back to the network before exiting?",
            "Tiskárna je pořád připojená k tomuto PC a nikdo jiný netiskne.\n\nVrátit ji před ukončením do sítě?",
            "Der Drucker ist noch mit diesem PC verbunden und niemand sonst kann drucken.\n\nVor dem Beenden ans Netzwerk zurückgeben?",
            "Tlačiareň je stále pripojená k tomuto PC a nikto iný netlačí.\n\nVrátiť ju pred ukončením do siete?",
            "Drukarka jest nadal podłączona do tego komputera i nikt inny nie drukuje.\n\nOddać ją do sieci przed zamknięciem?"],

        ["ok.attached"] = ["Attached: {0}. You can now start the printer's software.", "Připojeno: {0}. Teď můžeš spustit software tiskárny.", "Verbunden: {0}. Jetzt kannst du die Druckersoftware starten.", "Pripojené: {0}. Teraz môžeš spustiť softvér tlačiarne.", "Podłączono: {0}. Możesz teraz uruchomić oprogramowanie drukarki."],
        ["ok.attachednodevice"] = ["Attached. Windows is still installing the driver, give it a moment.", "Připojeno. Windows ještě instalují ovladač, chvíli to potrvá.", "Verbunden. Windows installiert noch den Treiber, einen Moment bitte.", "Pripojené. Windows ešte inštaluje ovládač, chvíľu to potrvá.", "Podłączono. Windows jeszcze instaluje sterownik, chwilę to potrwa."],
        ["ok.returned"] = ["The printer is back on the network.", "Tiskárna je zpátky v síti.", "Der Drucker ist wieder im Netzwerk.", "Tlačiareň je späť v sieti.", "Drukarka wróciła do sieci."],
        ["notify.taken"] = ["Another computer has borrowed the printer.", "Tiskárnu si zapůjčil jiný počítač.", "Ein anderer Computer hat sich den Drucker ausgeliehen.", "Tlačiareň si zapožičal iný počítač.", "Inny komputer wypożyczył drukarkę."],
        ["notify.failed"] = ["That did not work", "Nepovedlo se", "Das hat nicht geklappt", "Nepodarilo sa", "Nie udało się"],
        ["flyout.lastcheck"] = ["Checked at {0:HH:mm}", "Zkontrolováno {0:HH:mm}", "Geprüft um {0:HH:mm}", "Skontrolované {0:HH:mm}", "Sprawdzono o {0:HH:mm}"],

        // ------------------------------------------------------------ errors
        ["err.hostkey"] = [
            "The server's host key has changed. Either it was reinstalled or something is impersonating it. Run the setup wizard again.",
            "Klíč serveru se změnil. Buď byl server přeinstalován, nebo se za něj někdo vydává. Spusť znovu průvodce nastavením.",
            "Der Hostschlüssel des Servers hat sich geändert. Entweder wurde er neu installiert oder jemand gibt sich als er aus. Starte den Einrichtungsassistenten erneut.",
            "Kľúč servera sa zmenil. Buď bol server preinštalovaný, alebo sa zaň niekto vydáva. Spusti znova sprievodcu nastavením.",
            "Klucz serwera się zmienił. Albo serwer zainstalowano od nowa, albo ktoś się pod niego podszywa. Uruchom ponownie kreator konfiguracji."],
        ["err.auth"] = ["The server rejected the login.", "Server odmítl přihlášení.", "Der Server hat die Anmeldung abgelehnt.", "Server odmietol prihlásenie.", "Serwer odrzucił logowanie."],
        ["err.hostnotfound"] = ["No server with this name was found.", "Server s tímto jménem nelze najít.", "Kein Server mit diesem Namen gefunden.", "Server s týmto menom sa nedá nájsť.", "Nie znaleziono serwera o tej nazwie."],
        ["err.refused"] = ["The server refused the connection (is SSH running?).", "Server odmítl spojení (běží na něm SSH?).", "Der Server hat die Verbindung abgelehnt (läuft SSH?).", "Server odmietol spojenie (beží na ňom SSH?).", "Serwer odrzucił połączenie (czy działa SSH?)."],
        ["err.unreachable"] = ["The server cannot be reached.", "Server je nedostupný.", "Der Server ist nicht erreichbar.", "Server je nedostupný.", "Serwer jest nieosiągalny."],
        ["err.badreply"] = ["Unexpected reply from the server.", "Server odpověděl něco nečekaného.", "Unerwartete Antwort vom Server.", "Server odpovedal niečo neočakávané.", "Nieoczekiwana odpowiedź serwera."],
        ["err.nousbip"] = [
            "The usbip-win2 driver is not installed on this PC. The setup wizard can install it.",
            "V tomto PC chybí ovladač usbip-win2. Nainstaluješ ho v průvodci nastavením.",
            "Der Treiber usbip-win2 ist auf diesem PC nicht installiert. Der Einrichtungsassistent kann ihn installieren.",
            "V tomto PC chýba ovládač usbip-win2. Nainštaluješ ho v sprievodcovi nastavením.",
            "Na tym komputerze brakuje sterownika usbip-win2. Kreator konfiguracji może go zainstalować."],
        ["err.notprepared"] = ["The server is not prepared yet. Run the setup wizard.", "Server ještě není připravený. Spusť průvodce nastavením.", "Der Server ist noch nicht vorbereitet. Starte den Einrichtungsassistenten.", "Server ešte nie je pripravený. Spusti sprievodcu nastavením.", "Serwer nie jest jeszcze przygotowany. Uruchom kreator konfiguracji."],
        ["err.portbusy"] = ["Local port {0} is already used by another program.", "Místní port {0} už používá jiný program.", "Der lokale Port {0} wird bereits von einem anderen Programm verwendet.", "Miestny port {0} už používa iný program.", "Lokalny port {0} jest już używany przez inny program."],
        ["err.share"] = ["The server did not lend the printer.", "Server tiskárnu nezapůjčil.", "Der Server hat den Drucker nicht freigegeben.", "Server tlačiareň nezapožičal.", "Serwer nie wypożyczył drukarki."],
        ["err.nodevice"] = [
            "The printer is not visible on the server. Is it switched on and connected by USB?\n\nNothing was switched; network printing keeps working.",
            "Tiskárna není na serveru vidět. Je zapnutá a připojená USB kabelem?\n\nNic se nepřepnulo, síťový tisk běží dál.",
            "Der Drucker ist auf dem Server nicht sichtbar. Ist er eingeschaltet und per USB angeschlossen?\n\nEs wurde nichts umgeschaltet; das Drucken im Netzwerk läuft weiter.",
            "Tlačiareň nie je na serveri vidieť. Je zapnutá a pripojená USB káblom?\n\nNič sa neprepnulo, sieťová tlač beží ďalej.",
            "Drukarka nie jest widoczna na serwerze. Czy jest włączona i podłączona przez USB?\n\nNic nie przełączono; drukowanie w sieci działa dalej."],
        ["err.nomodule"] = [
            "The server could not load the usbip_host kernel module. After a kernel update, restarting the server usually helps.",
            "Na serveru se nepodařilo načíst jaderný modul usbip_host. Po aktualizaci jádra obvykle pomůže restart serveru.",
            "Der Server konnte das Kernelmodul usbip_host nicht laden. Nach einem Kernel-Update hilft meist ein Neustart des Servers.",
            "Na serveri sa nepodarilo načítať jadrový modul usbip_host. Po aktualizácii jadra zvyčajne pomôže reštart servera.",
            "Serwer nie mógł załadować modułu jądra usbip_host. Po aktualizacji jądra zwykle pomaga ponowne uruchomienie serwera."],
        ["err.attach"] = [
            "Attaching the device on this PC failed. The printer was given back to the network.",
            "Připojení zařízení v tomto PC selhalo. Tiskárna byla vrácena do sítě.",
            "Das Verbinden des Geräts auf diesem PC ist fehlgeschlagen. Der Drucker wurde ans Netzwerk zurückgegeben.",
            "Pripojenie zariadenia v tomto PC zlyhalo. Tlačiareň bola vrátená do siete.",
            "Podłączenie urządzenia na tym komputerze nie powiodło się. Drukarka wróciła do sieci."],
        ["err.unshare"] = ["Giving it back reported an error on the server.", "Vrácení na serveru hlásí chybu.", "Die Rückgabe meldet einen Fehler auf dem Server.", "Vrátenie na serveri hlási chybu.", "Oddanie zgłosiło błąd na serwerze."],
        ["usbip.badhash"] = [
            "The downloaded file does not match the expected checksum; it was not run.",
            "Stažený soubor nesouhlasí s očekávaným kontrolním součtem, nespustil jsem ho.",
            "Die heruntergeladene Datei stimmt nicht mit der erwarteten Prüfsumme überein; sie wurde nicht ausgeführt.",
            "Stiahnutý súbor nesúhlasí s očakávaným kontrolným súčtom, nespustil som ho.",
            "Pobrany plik nie zgadza się z oczekiwaną sumą kontrolną; nie został uruchomiony."],

        ["printer.added"] = ["The network printer was added to Windows.", "Síťová tiskárna byla přidána do Windows.", "Der Netzwerkdrucker wurde zu Windows hinzugefügt.", "Sieťová tlačiareň bola pridaná do Windows.", "Drukarka sieciowa została dodana do Windows."],
        ["printer.needfeature"] = [
            "Windows needs the optional feature \"Internet Printing Client\" to connect the network printer.\n\nTurn it on now? This asks for administrator rights.",
            "K připojení síťové tiskárny potřebují Windows funkci „Klient internetového tisku“.\n\nZapnout ji teď? Vyžádá si oprávnění správce.",
            "Windows benötigt das optionale Feature „Internetdruckclient“, um den Netzwerkdrucker zu verbinden.\n\nJetzt aktivieren? Dafür sind Administratorrechte nötig.",
            "Na pripojenie sieťovej tlačiarne potrebuje Windows funkciu „Klient internetovej tlače“.\n\nZapnúť ju teraz? Vyžiada si oprávnenie správcu.",
            "Do podłączenia drukarki sieciowej Windows potrzebuje funkcji „Klient drukowania internetowego”.\n\nWłączyć ją teraz? Wymaga to uprawnień administratora."],
        ["printer.failed"] = [
            "The printer could not be added (error {0}). Try Settings → Bluetooth & devices → Printers & scanners.",
            "Tiskárnu se nepodařilo přidat (chyba {0}). Zkus Nastavení → Bluetooth a zařízení → Tiskárny a skenery.",
            "Der Drucker konnte nicht hinzugefügt werden (Fehler {0}). Versuche Einstellungen → Bluetooth und Geräte → Drucker und Scanner.",
            "Tlačiareň sa nepodarilo pridať (chyba {0}). Skús Nastavenia → Bluetooth a zariadenia → Tlačiarne a skenery.",
            "Nie udało się dodać drukarki (błąd {0}). Spróbuj Ustawienia → Bluetooth i urządzenia → Drukarki i skanery."],
        ["printer.noqueue"] = ["No print queue was found on the server.", "Na serveru jsem nenašel žádnou tiskovou frontu.", "Auf dem Server wurde keine Druckwarteschlange gefunden.", "Na serveri som nenašiel žiadny tlačový front.", "Na serwerze nie znaleziono kolejki wydruku."],

        // ------------------------------------------------------------ wizard
        // ---------------------------------------------------------- settings
        ["set.title"] = ["Settings", "Nastavení", "Einstellungen", "Nastavenia", "Ustawienia"],
        ["set.nav.general"] = ["General", "Obecné", "Allgemein", "Všeobecné", "Ogólne"],
        ["set.nav.connection"] = ["Connection", "Připojení", "Verbindung", "Pripojenie", "Połączenie"],
        ["set.nav.printer"] = ["Printer", "Tiskárna", "Drucker", "Tlačiareň", "Drukarka"],
        ["set.nav.pc"] = ["This PC", "Tento počítač", "Dieser PC", "Tento počítač", "Ten komputer"],
        ["set.wizard.t"] = ["Setup wizard", "Průvodce nastavením", "Einrichtungsassistent", "Sprievodca nastavením", "Kreator konfiguracji"],
        ["set.wizard.d"] = ["Walks you through connecting to the server, preparing it and installing the driver, step by step.", "Krok za krokem projde připojení k serveru, jeho přípravu i instalaci ovladače.", "Führt Schritt für Schritt durch die Verbindung zum Server, seine Vorbereitung und die Treiberinstallation.", "Krok za krokom prejde pripojenie k serveru, jeho prípravu aj inštaláciu ovládača.", "Krok po kroku przeprowadzi przez połączenie z serwerem, jego przygotowanie i instalację sterownika."],
        ["set.group.app"] = ["App", "Aplikace", "App", "Aplikácia", "Aplikacja"],
        ["set.lang.t"] = ["Language", "Jazyk", "Sprache", "Jazyk", "Język"],
        ["set.lang.d"] = ["Language of the app and its notifications", "Jazyk aplikace a jejích oznámení", "Sprache der App und ihrer Benachrichtigungen", "Jazyk aplikácie a jej oznámení", "Język aplikacji i jej powiadomień"],
        ["set.autostart.t"] = ["Start when I sign in", "Spouštět po přihlášení", "Beim Anmelden starten", "Spúšťať po prihlásení", "Uruchamiaj po zalogowaniu"],
        ["set.autostart.d"] = ["The app waits quietly in the notification area.", "Aplikace tiše čeká v oznamovací oblasti.", "Die App wartet still im Infobereich.", "Aplikácia potichu čaká v oblasti oznámení.", "Aplikacja czeka po cichu w obszarze powiadomień."],
        ["set.pin.t"] = ["Show the icon on the taskbar", "Zobrazovat ikonu na hlavním panelu", "Symbol auf der Taskleiste anzeigen", "Zobrazovať ikonu na paneli úloh", "Pokazuj ikonę na pasku zadań"],
        ["set.pin.d"] = ["Otherwise it hides behind the arrow next to the clock.", "Jinak se schová pod šipku vedle hodin.", "Sonst versteckt es sich hinter dem Pfeil neben der Uhr.", "Inak sa schová pod šípku vedľa hodín.", "W przeciwnym razie chowa się pod strzałką obok zegara."],
        ["set.pin.na"] = ["Windows has not registered the icon yet; it will be pinned as soon as it does.", "Windows ikonu ještě nezaregistroval; připne se, jakmile to udělá.", "Windows hat das Symbol noch nicht registriert; es wird angeheftet, sobald das geschieht.", "Windows ikonu ešte nezaregistroval; pripne sa, hneď ako to urobí.", "Windows jeszcze nie zarejestrował ikony; zostanie przypięta, gdy tylko to zrobi."],
        ["set.interval.t"] = ["Check the printer every", "Kontrolovat stav tiskárny každých", "Druckerstatus prüfen alle", "Kontrolovať stav tlačiarne každých", "Sprawdzaj stan drukarki co"],
        ["set.interval.d"] = ["How often the app asks the server about the printer.", "Jak často se aplikace ptá serveru na tiskárnu.", "Wie oft die App den Server nach dem Drucker fragt.", "Ako často sa aplikácia pýta servera na tlačiareň.", "Jak często aplikacja pyta serwer o drukarkę."],
        ["toggle.on"] = ["On", "Zapnuto", "Ein", "Zapnuté", "Wł."],
        ["toggle.off"] = ["Off", "Vypnuto", "Aus", "Vypnuté", "Wył."],
        ["set.group.server"] = ["Print server", "Tiskový server", "Druckserver", "Tlačový server", "Serwer wydruku"],
        ["set.port.ssh"] = ["SSH port", "Port SSH", "SSH-Port", "Port SSH", "Port SSH"],
        ["set.port.invalid"] = ["Enter a port number from 1 to 65535.", "Zadej číslo portu od 1 do 65535.", "Gib eine Portnummer von 1 bis 65535 ein.", "Zadaj číslo portu od 1 do 65535.", "Podaj numer portu od 1 do 65535."],
        ["set.key.t"] = ["SSH key", "SSH klíč", "SSH-Schlüssel", "SSH kľúč", "Klucz SSH"],
        ["set.key.change"] = ["Change…", "Změnit…", "Ändern…", "Zmeniť…", "Zmień…"],
        ["set.key.none"] = ["Not set; the usual keys will be tried.", "Nenastaveno; zkusí se obvyklé klíče.", "Nicht festgelegt; die üblichen Schlüssel werden versucht.", "Nenastavené; vyskúšajú sa obvyklé kľúče.", "Nie ustawiono; zostaną wypróbowane typowe klucze."],
        ["set.key.filter"] = ["Private key", "Soukromý klíč", "Privater Schlüssel", "Súkromný kľúč", "Klucz prywatny"],
        ["set.fp.t"] = ["Server identity", "Identita serveru", "Serveridentität", "Identita servera", "Tożsamość serwera"],
        ["set.fp.d0"] = ["Not known yet; it is remembered at the next connection.", "Zatím neznámá; zapamatuje se při příštím připojení.", "Noch unbekannt; sie wird bei der nächsten Verbindung gespeichert.", "Zatiaľ neznáma; zapamätá sa pri ďalšom pripojení.", "Jeszcze nieznana; zostanie zapamiętana przy następnym połączeniu."],
        ["set.fp.forget"] = ["Forget", "Zapomenout", "Vergessen", "Zabudnúť", "Zapomnij"],
        ["set.fp.forgetconfirm"] = ["Only do this when the server was reinstalled or its SSH keys were changed on purpose. The next connection will trust whatever answers at this address.", "Udělej to jen tehdy, když byl server přeinstalován nebo se mu záměrně změnily SSH klíče. Příští připojení bude důvěřovat čemukoli, co na této adrese odpoví.", "Tu das nur, wenn der Server neu installiert wurde oder seine SSH-Schlüssel absichtlich geändert wurden. Die nächste Verbindung vertraut allem, was unter dieser Adresse antwortet.", "Urob to iba vtedy, keď bol server preinštalovaný alebo sa mu zámerne zmenili SSH kľúče. Ďalšie pripojenie bude dôverovať čomukoľvek, čo na tejto adrese odpovie.", "Zrób to tylko wtedy, gdy serwer został przeinstalowany lub celowo zmieniono jego klucze SSH. Następne połączenie zaufa temu, co odpowie pod tym adresem."],
        ["set.conn.test"] = ["Test connection", "Otestovat připojení", "Verbindung testen", "Otestovať pripojenie", "Testuj połączenie"],
        ["set.conn.save"] = ["Save and connect", "Uložit a připojit", "Speichern und verbinden", "Uložiť a pripojiť", "Zapisz i połącz"],
        ["set.conn.revert"] = ["Discard changes", "Zahodit změny", "Änderungen verwerfen", "Zahodiť zmeny", "Odrzuć zmiany"],
        ["set.conn.testing"] = ["Connecting…", "Připojuji…", "Verbinde…", "Pripájam…", "Łączenie…"],
        ["set.conn.ok"] = ["Connected as {0}.", "Připojeno jako {0}.", "Verbunden als {0}.", "Pripojené ako {0}.", "Połączono jako {0}."],
        ["set.conn.lent"] = ["Give the printer back to the network before changing the connection.", "Před změnou připojení vrať tiskárnu do sítě.", "Gib den Drucker ans Netzwerk zurück, bevor du die Verbindung änderst.", "Pred zmenou pripojenia vráť tlačiareň do siete.", "Przed zmianą połączenia oddaj drukarkę do sieci."],
        ["set.conn.needkey"] = ["The setup wizard can install a new key using the server password.", "Průvodce nastavením umí s heslem k serveru nahrát nový klíč.", "Der Einrichtungsassistent kann mit dem Serverpasswort einen neuen Schlüssel hinterlegen.", "Sprievodca nastavením vie s heslom k serveru nahrať nový kľúč.", "Kreator konfiguracji może wgrać nowy klucz przy użyciu hasła do serwera."],
        ["set.group.maint"] = ["Server maintenance", "Údržba serveru", "Serverwartung", "Údržba servera", "Konserwacja serwera"],
        ["set.prep.t"] = ["Prepare the server", "Připravit server", "Server vorbereiten", "Pripraviť server", "Przygotuj serwer"],
        ["set.prep.d"] = ["Checks and installs the helper, USB/IP and the print service.", "Zkontroluje a nainstaluje pomocníka, USB/IP a tiskovou službu.", "Prüft und installiert das Hilfsprogramm, USB/IP und den Druckdienst.", "Skontroluje a nainštaluje pomocníka, USB/IP a tlačovú službu.", "Sprawdza i instaluje program pomocniczy, USB/IP i usługę drukowania."],
        ["set.pi.t"] = ["Raspberry Pi tuning", "Vyladění Raspberry Pi", "Raspberry Pi optimieren", "Vyladenie Raspberry Pi", "Optymalizacja Raspberry Pi"],
        ["set.pi.d"] = ["Makes the Pi more reliable as an always-on print server.", "Spolehlivější Pi jako trvale zapnutý tiskový server.", "Macht den Pi als dauerhaft laufenden Druckserver zuverlässiger.", "Spoľahlivejšie Pi ako trvalo zapnutý tlačový server.", "Bardziej niezawodne Pi jako stale włączony serwer wydruku."],
        ["set.cups.d"] = ["Opens the CUPS web page of the server.", "Otevře webovou správu CUPS na serveru.", "Öffnet die CUPS-Weboberfläche des Servers.", "Otvorí webovú správu CUPS na serveri.", "Otwiera stronę CUPS na serwerze."],
        ["set.dev.d"] = ["The USB device the server lends to this PC.", "USB zařízení, které server půjčuje tomuto PC.", "Das USB-Gerät, das der Server diesem PC leiht.", "USB zariadenie, ktoré server požičiava tomuto PC.", "Urządzenie USB, które serwer wypożycza temu komputerowi."],
        ["set.queue.d"] = ["Used when adding the network printer to Windows.", "Použije se při přidání síťové tiskárny do Windows.", "Wird beim Hinzufügen des Netzwerkdruckers zu Windows verwendet.", "Použije sa pri pridaní sieťovej tlačiarne do Windows.", "Używana przy dodawaniu drukarki sieciowej do Windows."],
        ["set.printer.load"] = ["Load from server", "Načíst ze serveru", "Vom Server laden", "Načítať zo servera", "Wczytaj z serwera"],
        ["set.printer.loading"] = ["Loading from the server…", "Načítám ze serveru…", "Wird vom Server geladen…", "Načítavam zo servera…", "Wczytywanie z serwera…"],
        ["set.printer.offline"] = ["The server is not reachable; showing the saved choice.", "Server není dostupný; zobrazuji uloženou volbu.", "Der Server ist nicht erreichbar; die gespeicherte Auswahl wird angezeigt.", "Server nie je dostupný; zobrazujem uloženú voľbu.", "Serwer jest niedostępny; pokazuję zapisany wybór."],
        ["set.group.windows"] = ["Windows", "Windows", "Windows", "Windows", "Windows"],
        ["set.add.d"] = ["Lets you print over the network while the printer is not lent out.", "Umožní tisk přes síť, když tiskárna není zapůjčená.", "Ermöglicht das Drucken übers Netzwerk, solange der Drucker nicht ausgeliehen ist.", "Umožní tlač cez sieť, keď tlačiareň nie je zapožičaná.", "Umożliwia drukowanie przez sieć, gdy drukarka nie jest wypożyczona."],
        ["set.add.button"] = ["Add", "Přidat", "Hinzufügen", "Pridať", "Dodaj"],
        ["set.winprinters.t"] = ["Printers & scanners", "Tiskárny a skenery", "Drucker & Scanner", "Tlačiarne a skenery", "Drukarki i skanery"],
        ["set.winprinters.d"] = ["Windows settings for printers", "Nastavení tiskáren ve Windows", "Windows-Einstellungen für Drucker", "Nastavenia tlačiarní vo Windows", "Ustawienia drukarek w Windows"],
        ["set.usbip.t"] = ["USB/IP driver (usbip-win2)", "Ovladač USB/IP (usbip-win2)", "USB/IP-Treiber (usbip-win2)", "Ovládač USB/IP (usbip-win2)", "Sterownik USB/IP (usbip-win2)"],
        ["set.group.advanced"] = ["Advanced", "Pokročilé", "Erweitert", "Rozšírené", "Zaawansowane"],
        ["set.port.t"] = ["Local tunnel port", "Místní port tunelu", "Lokaler Tunnelport", "Miestny port tunela", "Lokalny port tunelu"],
        ["set.port.d"] = ["USB/IP reaches the server through SSH on this port. Change it only if another program uses it.", "Přes tento port jde USB/IP na server tunelem SSH. Měň ho, jen když ho používá jiný program.", "Über diesen Port erreicht USB/IP den Server durch SSH. Ändere ihn nur, wenn ein anderes Programm ihn verwendet.", "Cez tento port ide USB/IP na server tunelom SSH. Meň ho, iba ak ho používa iný program.", "Przez ten port USB/IP łączy się z serwerem tunelem SSH. Zmień go tylko, jeśli używa go inny program."],
        ["set.log.t"] = ["Log", "Záznam činnosti", "Protokoll", "Záznam činnosti", "Dziennik"],
        ["set.log.d"] = ["What the app did and why something failed.", "Co aplikace dělala a proč něco selhalo.", "Was die App getan hat und warum etwas fehlgeschlagen ist.", "Čo aplikácia robila a prečo niečo zlyhalo.", "Co robiła aplikacja i dlaczego coś się nie udało."],
        ["set.folder.t"] = ["Settings folder", "Složka s nastavením", "Einstellungsordner", "Priečinok s nastaveniami", "Folder ustawień"],
        ["set.version"] = ["Version {0}", "Verze {0}", "Version {0}", "Verzia {0}", "Wersja {0}"],
        ["set.license.t"] = ["Licence", "Licence", "Lizenz", "Licencia", "Licencja"],
        ["set.group.links"] = ["Links", "Odkazy", "Links", "Odkazy", "Linki"],
        ["set.github.t"] = ["Source code on GitHub", "Zdrojový kód na GitHubu", "Quellcode auf GitHub", "Zdrojový kód na GitHube", "Kod źródłowy na GitHubie"],
        ["set.github.d"] = ["Report a problem or download a new version.", "Nahlásit chybu nebo stáhnout novou verzi.", "Problem melden oder neue Version herunterladen.", "Nahlásiť chybu alebo stiahnuť novú verziu.", "Zgłoś problem lub pobierz nową wersję."],
        ["set.notices.d"] = ["Licences of the components the app is built from.", "Licence součástí, ze kterých je aplikace postavená.", "Lizenzen der Komponenten, aus denen die App besteht.", "Licencie súčastí, z ktorých je aplikácia postavená.", "Licencje komponentów, z których zbudowano aplikację."],

        // ----------------------------------------------------------- updates
        ["set.nav.updates"] = ["Updates", "Aktualizace", "Updates", "Aktualizácie", "Aktualizacje"],
        ["upd.uptodate"] = ["You're up to date", "Máš nejnovější verzi", "Du bist auf dem neuesten Stand", "Máš najnovšiu verziu", "Masz najnowszą wersję"],
        ["upd.available"] = ["Version {0} is available", "Je k dispozici verze {0}", "Version {0} ist verfügbar", "Je k dispozícii verzia {0}", "Dostępna jest wersja {0}"],
        ["upd.checking"] = ["Checking for updates…", "Hledám aktualizace…", "Suche nach Updates…", "Hľadám aktualizácie…", "Szukanie aktualizacji…"],
        ["upd.notchecked"] = ["Not checked yet", "Zatím nezkontrolováno", "Noch nicht geprüft", "Zatiaľ neskontrolované", "Jeszcze nie sprawdzono"],
        ["upd.installed"] = ["Installed version {0}", "Nainstalovaná verze {0}", "Installierte Version {0}", "Nainštalovaná verzia {0}", "Zainstalowana wersja {0}"],
        ["upd.lastcheck"] = ["last checked {0:g}", "naposledy zkontrolováno {0:g}", "zuletzt geprüft {0:g}", "naposledy skontrolované {0:g}", "ostatnio sprawdzono {0:g}"],
        ["upd.check"] = ["Check for updates", "Zkontrolovat aktualizace", "Nach Updates suchen", "Skontrolovať aktualizácie", "Sprawdź aktualizacje"],
        ["upd.install"] = ["Download and install", "Stáhnout a nainstalovat", "Herunterladen und installieren", "Stiahnuť a nainštalovať", "Pobierz i zainstaluj"],
        ["upd.downloading"] = ["Downloading… {0:P0}", "Stahuji… {0:P0}", "Wird heruntergeladen… {0:P0}", "Sťahujem… {0:P0}", "Pobieranie… {0:P0}"],
        ["upd.starting"] = ["Verified. Starting the installer; the app starts again when it is done.", "Ověřeno. Spouštím instalátor; po dokončení se aplikace sama znovu spustí.", "Geprüft. Das Installationsprogramm startet; danach startet die App neu.", "Overené. Spúšťam inštalátor; po dokončení sa aplikácia sama znova spustí.", "Zweryfikowano. Uruchamianie instalatora; po zakończeniu aplikacja uruchomi się ponownie."],
        ["upd.declined"] = ["The installation was not allowed.", "Instalace nebyla povolena.", "Die Installation wurde nicht erlaubt.", "Inštalácia nebola povolená.", "Instalacja nie została dozwolona."],
        ["upd.failed"] = ["The update failed: {0}", "Aktualizace selhala: {0}", "Update fehlgeschlagen: {0}", "Aktualizácia zlyhala: {0}", "Aktualizacja nie powiodła się: {0}"],
        ["upd.checkfailed"] = ["Could not reach GitHub", "Nepodařilo se spojit s GitHubem", "GitHub ist nicht erreichbar", "Nepodarilo sa spojiť s GitHubom", "Nie udało się połączyć z GitHubem"],
        ["upd.badhash"] = ["The downloaded file does not match the published checksum and was deleted.", "Stažený soubor neodpovídá zveřejněnému kontrolnímu součtu a byl smazán.", "Die heruntergeladene Datei stimmt nicht mit der veröffentlichten Prüfsumme überein und wurde gelöscht.", "Stiahnutý súbor nezodpovedá zverejnenému kontrolnému súčtu a bol zmazaný.", "Pobrany plik nie zgadza się z opublikowaną sumą kontrolną i został usunięty."],
        ["upd.badsig"] = ["The update is not signed by the author of Printer Switch and was deleted.", "Aktualizace není podepsaná autorem Přepínače tiskárny a byla smazána.", "Das Update ist nicht vom Autor des Druckerumschalters signiert und wurde gelöscht.", "Aktualizácia nie je podpísaná autorom Prepínača tlačiarne a bola zmazaná.", "Aktualizacja nie jest podpisana przez autora Przełącznika drukarki i została usunięta."],
        ["upd.manual"] = ["This release can't be installed from the app yet; download it from its page.", "Toto vydání zatím nejde nainstalovat z aplikace; stáhni ho z jeho stránky.", "Diese Version lässt sich noch nicht aus der App installieren; lade sie von ihrer Seite herunter.", "Toto vydanie zatiaľ nejde nainštalovať z aplikácie; stiahni ho z jeho stránky.", "Tego wydania nie można jeszcze zainstalować z aplikacji; pobierz je z jego strony."],
        ["upd.lent"] = ["The printer is attached to this PC. Give it back to the network and install the update?", "Tiskárna je připojená k tomuto PC. Vrátit ji do sítě a nainstalovat aktualizaci?", "Der Drucker ist mit diesem PC verbunden. Ans Netzwerk zurückgeben und das Update installieren?", "Tlačiareň je pripojená k tomuto PC. Vrátiť ju do siete a nainštalovať aktualizáciu?", "Drukarka jest podłączona do tego komputera. Oddać ją do sieci i zainstalować aktualizację?"],
        ["upd.notes.t"] = ["What's new", "Co je nového", "Neuigkeiten", "Čo je nové", "Co nowego"],
        ["upd.notes.d"] = ["Release notes for version {0} on GitHub", "Poznámky k verzi {0} na GitHubu", "Versionshinweise zu {0} auf GitHub", "Poznámky k verzii {0} na GitHube", "Informacje o wersji {0} na GitHubie"],
        ["upd.auto.t"] = ["Check for updates automatically", "Automaticky kontrolovat aktualizace", "Automatisch nach Updates suchen", "Automaticky kontrolovať aktualizácie", "Automatycznie sprawdzaj aktualizacje"],
        ["upd.auto.d"] = ["Once a day the app asks GitHub for a new version. Nothing about you is sent.", "Jednou denně se aplikace zeptá GitHubu na novou verzi. Nic o tobě se neposílá.", "Einmal am Tag fragt die App GitHub nach einer neuen Version. Es werden keine Daten über dich gesendet.", "Raz denne sa aplikácia opýta GitHubu na novú verziu. Nič o tebe sa neposiela.", "Raz dziennie aplikacja pyta GitHub o nową wersję. Nie są wysyłane żadne dane o tobie."],
        ["upd.all.t"] = ["All releases", "Všechna vydání", "Alle Versionen", "Všetky vydania", "Wszystkie wydania"],
        ["upd.all.d"] = ["Older versions and installers on GitHub", "Starší verze a instalátory na GitHubu", "Ältere Versionen und Installationsprogramme auf GitHub", "Staršie verzie a inštalátory na GitHube", "Starsze wersje i instalatory na GitHubie"],
        ["upd.notify"] = ["Version {0} is available. Click to install it.", "Je k dispozici verze {0}. Klepnutím ji nainstaluješ.", "Version {0} ist verfügbar. Zum Installieren klicken.", "Je k dispozícii verzia {0}. Kliknutím ju nainštaluješ.", "Dostępna jest wersja {0}. Kliknij, aby ją zainstalować."],
        ["upd.done"] = ["Updated to version {0}.", "Aktualizováno na verzi {0}.", "Auf Version {0} aktualisiert.", "Aktualizované na verziu {0}.", "Zaktualizowano do wersji {0}."],
        ["upd.flyout"] = ["Update {0} is available", "Je k dispozici aktualizace {0}", "Update {0} ist verfügbar", "Je k dispozícii aktualizácia {0}", "Dostępna aktualizacja {0}"],

        ["wiz.title"] = ["Setup wizard – Printer Switch", "Průvodce nastavením – Přepínač tiskárny", "Einrichtungsassistent – Druckerumschalter", "Sprievodca nastavením – Prepínač tlačiarne", "Kreator konfiguracji – Przełącznik drukarki"],
        ["wiz.back"] = ["Back", "Zpět", "Zurück", "Späť", "Wstecz"],
        ["wiz.next"] = ["Next", "Další", "Weiter", "Ďalej", "Dalej"],
        ["wiz.finish"] = ["Finish", "Dokončit", "Fertig", "Dokončiť", "Zakończ"],
        ["wiz.step1"] = ["Welcome", "Vítejte", "Willkommen", "Vitajte", "Witamy"],
        ["wiz.step2"] = ["Print server", "Tiskový server", "Druckserver", "Tlačový server", "Serwer wydruku"],
        ["wiz.step3"] = ["Prepare server", "Příprava serveru", "Server vorbereiten", "Príprava servera", "Przygotowanie serwera"],
        ["wiz.step4"] = ["Raspberry Pi", "Raspberry Pi", "Raspberry Pi", "Raspberry Pi", "Raspberry Pi"],
        ["wiz.step5"] = ["This PC", "Tento počítač", "Dieser PC", "Tento počítač", "Ten komputer"],
        ["wiz.step6"] = ["Done", "Hotovo", "Fertig", "Hotovo", "Gotowe"],
        ["wiz.lang.auto"] = ["Same as Windows", "Podle Windows", "Wie Windows", "Podľa Windows", "Jak w Windows"],

        ["wiz.welcome.h"] = [
            "Your USB printer on the network – and on USB whenever you need it",
            "Tvoje USB tiskárna v síti – a kdykoli potřebuješ, zase přes USB",
            "Dein USB-Drucker im Netzwerk – und per USB, wann immer du ihn brauchst",
            "Tvoja USB tlačiareň v sieti – a kedykoľvek potrebuješ, zase cez USB",
            "Twoja drukarka USB w sieci – a gdy trzeba, znowu przez USB"],
        ["wiz.welcome.p1"] = [
            "Connect a USB printer to a Raspberry Pi and this wizard turns it into a print server, so everyone on the network can print. Then one click makes the printer behave as if it were plugged into this PC with a cable – for the manufacturer's software: toner levels, cleaning, firmware updates. Another click gives it back to the network.",
            "Připoj USB tiskárnu k Raspberry Pi a průvodce z něj udělá tiskový server, takže na ni bude tisknout celá síť. Pak se tiskárna jedním kliknutím začne chovat, jako by byla zapojená kabelem přímo do tohoto PC – kvůli softwaru výrobce: stav toneru, čištění, aktualizace firmwaru. Dalším kliknutím ji vrátíš do sítě.",
            "Schließe einen USB-Drucker an einen Raspberry Pi an, und dieser Assistent macht daraus einen Druckserver, sodass alle im Netzwerk drucken können. Dann verhält sich der Drucker mit einem Klick so, als wäre er per Kabel an diesen PC angeschlossen – für die Herstellersoftware: Tonerstand, Reinigung, Firmware-Updates. Ein weiterer Klick gibt ihn ans Netzwerk zurück.",
            "Pripoj USB tlačiareň k Raspberry Pi a sprievodca z neho urobí tlačový server, takže na ňu bude tlačiť celá sieť. Potom sa tlačiareň jedným kliknutím začne správať, ako keby bola zapojená káblom priamo do tohto PC – kvôli softvéru výrobcu: stav tonera, čistenie, aktualizácie firmvéru. Ďalším kliknutím ju vrátiš do siete.",
            "Podłącz drukarkę USB do Raspberry Pi, a kreator zrobi z niego serwer wydruku, aby cała sieć mogła drukować. Potem jednym kliknięciem drukarka zacznie zachowywać się tak, jakby była podłączona kablem prosto do tego komputera – dla oprogramowania producenta: poziom tonera, czyszczenie, aktualizacje firmware. Kolejne kliknięcie oddaje ją do sieci."],
        ["wiz.welcome.need"] = ["What you need", "Co budeš potřebovat", "Was du brauchst", "Čo budeš potrebovať", "Czego potrzebujesz"],
        ["wiz.welcome.b1"] = ["A Linux print server with a USB printer – or a Raspberry Pi the wizard turns into one", "Linuxový tiskový server s USB tiskárnou – nebo Raspberry Pi, ze kterého ho průvodce udělá", "Einen Linux-Druckserver mit USB-Drucker – oder einen Raspberry Pi, den der Assistent dazu macht", "Linuxový tlačový server s USB tlačiarňou – alebo Raspberry Pi, z ktorého ho sprievodca urobí", "Linuksowy serwer wydruku z drukarką USB – albo Raspberry Pi, z którego kreator go zrobi"],
        ["wiz.welcome.b2"] = ["SSH access to the server (user name and password or key)", "Přihlášení na server přes SSH (jméno a heslo nebo klíč)", "SSH-Zugang zum Server (Benutzername und Passwort oder Schlüssel)", "Prihlásenie na server cez SSH (meno a heslo alebo kľúč)", "Dostęp SSH do serwera (nazwa użytkownika i hasło lub klucz)"],
        ["wiz.welcome.b3"] = ["The usbip-win2 driver on this PC – the wizard can install it", "Ovladač usbip-win2 v tomto PC – průvodce ho umí nainstalovat", "Den Treiber usbip-win2 auf diesem PC – der Assistent kann ihn installieren", "Ovládač usbip-win2 v tomto PC – sprievodca ho vie nainštalovať", "Sterownik usbip-win2 na tym komputerze – kreator może go zainstalować"],

        ["wiz.server.h"] = ["Where is the print server?", "Kde je tiskový server?", "Wo ist der Druckserver?", "Kde je tlačový server?", "Gdzie jest serwer wydruku?"],
        ["wiz.server.p"] = ["Enter the server's address and the user you log in with over SSH.", "Zadej adresu serveru a uživatele, pod kterým se na něj přihlašuješ přes SSH.", "Gib die Adresse des Servers und den Benutzer ein, mit dem du dich per SSH anmeldest.", "Zadaj adresu servera a používateľa, pod ktorým sa naň prihlasuješ cez SSH.", "Podaj adres serwera i użytkownika, którym logujesz się przez SSH."],
        ["wiz.find.hint"] = ["Don't know the address? Search the network.", "Nevíš adresu? Zkus server najít v síti.", "Adresse unbekannt? Durchsuche das Netzwerk.", "Nevieš adresu? Skús server nájsť v sieti.", "Nie znasz adresu? Przeszukaj sieć."],
        ["wiz.find.button"] = ["Search network", "Najít v síti", "Netzwerk durchsuchen", "Nájsť v sieti", "Szukaj w sieci"],
        ["wiz.find.searching"] = ["Looking for print servers…", "Hledám tiskové servery…", "Suche nach Druckservern…", "Hľadám tlačové servery…", "Szukanie serwerów wydruku…"],
        ["wiz.find.pick"] = ["Found – pick a server:", "Nalezeno – vyber server:", "Gefunden – wähle einen Server:", "Nájdené – vyber server:", "Znaleziono – wybierz serwer:"],
        ["wiz.find.none"] = ["Nothing found. Enter the address manually.", "Nic jsem nenašel. Zadej adresu ručně.", "Nichts gefunden. Gib die Adresse von Hand ein.", "Nič som nenašiel. Zadaj adresu ručne.", "Nic nie znaleziono. Wpisz adres ręcznie."],
        ["wiz.server.host"] = ["Address (IP or host name)", "Adresa (IP nebo jméno)", "Adresse (IP oder Hostname)", "Adresa (IP alebo meno)", "Adres (IP lub nazwa hosta)"],
        ["wiz.server.user"] = ["User", "Uživatel", "Benutzer", "Používateľ", "Użytkownik"],
        ["wiz.server.port"] = ["Port", "Port", "Port", "Port", "Port"],
        ["wiz.server.connect"] = ["Connect", "Připojit", "Verbinden", "Pripojiť", "Połącz"],
        ["wiz.server.ok"] = ["Connected as {0}.", "Připojeno jako {0}.", "Verbunden als {0}.", "Pripojené ako {0}.", "Połączono jako {0}."],
        ["wiz.server.fp"] = ["Server key fingerprint: SHA256:{0}", "Otisk klíče serveru: SHA256:{0}", "Fingerabdruck des Serverschlüssels: SHA256:{0}", "Odtlačok kľúča servera: SHA256:{0}", "Odcisk klucza serwera: SHA256:{0}"],
        ["wiz.server.keyused"] = ["Logging in with key {0}.", "Přihlašuji se klíčem {0}.", "Anmeldung mit Schlüssel {0}.", "Prihlasujem sa kľúčom {0}.", "Logowanie kluczem {0}."],
        ["wiz.server.needpw"] = [
            "Key login does not work yet. Enter the password – it is used once to set up key login and is never stored.",
            "Klíčem se přihlásit nejde. Zadej heslo – použije se jen jednou k nastavení přihlašování klíčem a nikam se neukládá.",
            "Die Anmeldung per Schlüssel funktioniert noch nicht. Gib das Passwort ein – es wird nur einmal zum Einrichten der Schlüsselanmeldung verwendet und nie gespeichert.",
            "Kľúčom sa prihlásiť nedá. Zadaj heslo – použije sa len raz na nastavenie prihlasovania kľúčom a nikam sa neukladá.",
            "Logowanie kluczem jeszcze nie działa. Podaj hasło – zostanie użyte raz do skonfigurowania logowania kluczem i nigdzie nie jest zapisywane."],
        ["wiz.server.password"] = ["Password", "Heslo", "Passwort", "Heslo", "Hasło"],
        ["wiz.server.setupkey"] = ["Set up key login", "Nastavit přihlášení klíčem", "Schlüsselanmeldung einrichten", "Nastaviť prihlásenie kľúčom", "Skonfiguruj logowanie kluczem"],
        ["wiz.server.missing"] = ["Fill in both the address and the user.", "Vyplň adresu i uživatele.", "Fülle Adresse und Benutzer aus.", "Vyplň adresu aj používateľa.", "Wypełnij adres i użytkownika."],

        ["wiz.prep.h"] = ["Prepare the server", "Příprava serveru", "Server vorbereiten", "Príprava servera", "Przygotowanie serwera"],
        ["wiz.mode.existing.t"] = ["Use my existing print server", "Použít stávající tiskový server", "Meinen bestehenden Druckserver verwenden", "Použiť existujúci tlačový server", "Użyj istniejącego serwera wydruku"],
        ["wiz.mode.existing.d"] = [
            "Installs only the small helper (/usr/local/sbin/printswitch), the usbip package and a sudo rule for that one script. Nothing else on the server changes.",
            "Nainstaluje jen malý pomocný skript (/usr/local/sbin/printswitch), balík usbip a pravidlo pro sudo pro tento jediný skript. Nic jiného se na serveru nemění.",
            "Installiert nur das kleine Hilfsskript (/usr/local/sbin/printswitch), das Paket usbip und eine sudo-Regel für genau dieses Skript. Sonst ändert sich nichts am Server.",
            "Nainštaluje len malý pomocný skript (/usr/local/sbin/printswitch), balík usbip a pravidlo pre sudo pre tento jediný skript. Nič iné sa na serveri nemení.",
            "Instaluje tylko mały skrypt pomocniczy (/usr/local/sbin/printswitch), pakiet usbip i regułę sudo dla tego jednego skryptu. Nic innego na serwerze się nie zmienia."],
        ["wiz.mode.full.t"] = ["Set up a print server from scratch", "Nastavit tiskový server od začátku", "Druckserver neu einrichten", "Nastaviť tlačový server od začiatku", "Skonfiguruj serwer wydruku od zera"],
        ["wiz.mode.full.d"] = [
            "Also installs CUPS and a driver for the printer, adds the printer and shares it on the network. For a fresh Raspberry Pi; the printer must be switched on.",
            "Navíc nainstaluje CUPS a ovladač tiskárny, přidá tiskárnu a zapne její sdílení v síti. Pro čisté Raspberry Pi; tiskárna musí být zapnutá.",
            "Installiert zusätzlich CUPS und einen Druckertreiber, fügt den Drucker hinzu und gibt ihn im Netzwerk frei. Für einen frischen Raspberry Pi; der Drucker muss eingeschaltet sein.",
            "Navyše nainštaluje CUPS a ovládač tlačiarne, pridá tlačiareň a zapne jej zdieľanie v sieti. Pre čisté Raspberry Pi; tlačiareň musí byť zapnutá.",
            "Dodatkowo instaluje CUPS i sterownik drukarki, dodaje drukarkę i udostępnia ją w sieci. Dla świeżego Raspberry Pi; drukarka musi być włączona."],
        ["wiz.prep.cups"] = ["CUPS print service", "Tisková služba CUPS", "Druckdienst CUPS", "Tlačová služba CUPS", "Usługa drukowania CUPS"],
        ["wiz.prep.helper"] = ["Helper script and sudo rule", "Pomocný skript a pravidlo pro sudo", "Hilfsskript und sudo-Regel", "Pomocný skript a pravidlo pre sudo", "Skrypt pomocniczy i reguła sudo"],
        ["wiz.prep.usbip"] = ["usbip package", "Balík usbip", "Paket usbip", "Balík usbip", "Pakiet usbip"],
        ["wiz.prep.ok"] = ["OK", "v pořádku", "OK", "v poriadku", "OK"],
        ["wiz.prep.missing"] = ["missing", "chybí", "fehlt", "chýba", "brak"],
        ["wiz.prep.notrunning"] = ["not running", "neběží", "läuft nicht", "nebeží", "nie działa"],
        ["wiz.prep.checking"] = ["checking…", "zjišťuji…", "wird geprüft…", "zisťujem…", "sprawdzanie…"],
        ["wiz.prep.device"] = ["Printer", "Tiskárna", "Drucker", "Tlačiareň", "Drukarka"],
        ["wiz.prep.auto"] = ["Automatic (first USB printer)", "Automaticky (první USB tiskárna)", "Automatisch (erster USB-Drucker)", "Automaticky (prvá USB tlačiareň)", "Automatycznie (pierwsza drukarka USB)"],
        ["wiz.prep.nodevice"] = [
            "No USB printer is visible on the server right now – is it switched on? You can continue; it is picked automatically when attaching.",
            "Na serveru teď nevidím žádnou USB tiskárnu – je zapnutá? Klidně pokračuj, vybere se sama při připojení.",
            "Auf dem Server ist gerade kein USB-Drucker zu sehen – ist er eingeschaltet? Du kannst fortfahren; er wird beim Verbinden automatisch gewählt.",
            "Na serveri teraz nevidím žiadnu USB tlačiareň – je zapnutá? Pokojne pokračuj, vyberie sa sama pri pripojení.",
            "Na serwerze nie widać teraz żadnej drukarki USB – czy jest włączona? Możesz kontynuować; zostanie wybrana automatycznie przy podłączaniu."],
        ["wiz.prep.queue"] = ["Print queue", "Tisková fronta", "Druckwarteschlange", "Tlačový front", "Kolejka wydruku"],
        ["wiz.prep.run"] = ["Prepare server", "Připravit server", "Server vorbereiten", "Pripraviť server", "Przygotuj serwer"],
        ["wiz.prep.rerun"] = ["Prepare again", "Připravit znovu", "Erneut vorbereiten", "Pripraviť znova", "Przygotuj ponownie"],
        ["wiz.prep.running"] = [
            "Preparing the server… installing packages can take several minutes on a small Pi.",
            "Připravuji server… instalace balíků může na malém Pi trvat i několik minut.",
            "Server wird vorbereitet… die Paketinstallation kann auf einem kleinen Pi einige Minuten dauern.",
            "Pripravujem server… inštalácia balíkov môže na malom Pi trvať aj niekoľko minút.",
            "Przygotowywanie serwera… instalacja pakietów na małym Pi może potrwać kilka minut."],
        ["wiz.prep.sudopw"] = ["sudo password", "Heslo pro sudo", "sudo-Passwort", "Heslo pre sudo", "Hasło sudo"],
        ["wiz.prep.sudoneeded"] = [
            "The server asks for a sudo password. Enter it – it is used only now and never stored.",
            "Server chce pro sudo heslo. Zadej ho – použije se jen teď a neukládá se.",
            "Der Server verlangt ein sudo-Passwort. Gib es ein – es wird nur jetzt verwendet und nie gespeichert.",
            "Server chce pre sudo heslo. Zadaj ho – použije sa len teraz a neukladá sa.",
            "Serwer prosi o hasło sudo. Podaj je – zostanie użyte tylko teraz i nie jest zapisywane."],
        ["wiz.prep.done"] = ["The server is ready.", "Server je připravený.", "Der Server ist bereit.", "Server je pripravený.", "Serwer jest gotowy."],
        ["wiz.prep.failed"] = ["Preparing the server failed:", "Přípravu se nepodařilo dokončit:", "Die Vorbereitung ist fehlgeschlagen:", "Prípravu sa nepodarilo dokončiť:", "Przygotowanie serwera nie powiodło się:"],
        ["wiz.prep.skipwarn"] = [
            "The server is not prepared, so the printer cannot be attached to this PC. Continue anyway?",
            "Server není připravený, takže tiskárnu nepůjde připojit k tomuto PC. Přesto pokračovat?",
            "Der Server ist nicht vorbereitet, daher kann der Drucker nicht mit diesem PC verbunden werden. Trotzdem fortfahren?",
            "Server nie je pripravený, takže tlačiareň nepôjde pripojiť k tomuto PC. Predsa pokračovať?",
            "Serwer nie jest przygotowany, więc drukarki nie da się podłączyć do tego komputera. Kontynuować mimo to?"],

        ["wiz.pi.h"] = ["Tune the Raspberry Pi", "Vyladění Raspberry Pi", "Raspberry Pi optimieren", "Vyladenie Raspberry Pi", "Optymalizacja Raspberry Pi"],
        ["wiz.pi.p"] = [
            "Settings that keep a small Pi stable as a print server running day and night. Pick what you want – nothing changes until you press Apply, and everything can be undone.",
            "Nastavení, se kterými malé Pi vydrží stabilně běžet jako tiskový server ve dne v noci. Vyber, co chceš – nic se nezmění, dokud nestiskneš Použít, a vše jde vrátit.",
            "Einstellungen, mit denen ein kleiner Pi als Druckserver Tag und Nacht stabil läuft. Wähle aus, was du möchtest – nichts ändert sich, bevor du auf Anwenden drückst, und alles lässt sich rückgängig machen.",
            "Nastavenia, s ktorými malé Pi vydrží stabilne bežať ako tlačový server vo dne v noci. Vyber, čo chceš – nič sa nezmení, kým nestlačíš Použiť, a všetko sa dá vrátiť.",
            "Ustawienia, dzięki którym małe Pi działa stabilnie jako serwer wydruku dzień i noc. Wybierz, co chcesz – nic się nie zmieni, dopóki nie klikniesz Zastosuj, a wszystko można cofnąć."],
        ["wiz.pi.notpi"] = ["The server is not a Raspberry Pi – there is nothing to do here.", "Server není Raspberry Pi – tady není co dělat.", "Der Server ist kein Raspberry Pi – hier gibt es nichts zu tun.", "Server nie je Raspberry Pi – tu nie je čo robiť.", "Serwer nie jest Raspberry Pi – nie ma tu nic do zrobienia."],
        ["wiz.pi.dedicated.t"] = ["This Raspberry Pi is only a print server", "Tento Raspberry Pi slouží jen jako tiskový server", "Dieser Raspberry Pi ist nur Druckserver", "Tento Raspberry Pi slúži len ako tlačový server", "To Raspberry Pi służy tylko jako serwer wydruku"],
        ["wiz.pi.dedicated.d"] = ["Then services it does not need (sound, Bluetooth…) can be turned off too.", "Pak jde vypnout i služby, které nepotřebuje (zvuk, Bluetooth…).", "Dann können auch Dienste abgeschaltet werden, die er nicht braucht (Ton, Bluetooth…).", "Potom sa dajú vypnúť aj služby, ktoré nepotrebuje (zvuk, Bluetooth…).", "Wtedy można też wyłączyć niepotrzebne usługi (dźwięk, Bluetooth…)."],
        ["wiz.pi.applied"] = ["already set", "už nastaveno", "bereits eingestellt", "už nastavené", "już ustawione"],
        ["wiz.pi.unavailable"] = ["not available on this server", "na tomto serveru nejde použít", "auf diesem Server nicht verfügbar", "na tomto serveri sa nedá použiť", "niedostępne na tym serwerze"],
        ["wiz.pi.apply"] = ["Apply", "Použít", "Anwenden", "Použiť", "Zastosuj"],
        ["wiz.pi.applying"] = ["Applying…", "Nastavuji…", "Wird angewendet…", "Nastavujem…", "Stosowanie…"],
        ["wiz.pi.nothing"] = ["Nothing selected.", "Nic není vybráno.", "Nichts ausgewählt.", "Nič nie je vybrané.", "Nic nie wybrano."],
        ["wiz.pi.done"] = ["Done. Some changes take effect after a restart.", "Hotovo. Některé změny se projeví až po restartu.", "Fertig. Manche Änderungen wirken erst nach einem Neustart.", "Hotovo. Niektoré zmeny sa prejavia až po reštarte.", "Gotowe. Niektóre zmiany zadziałają po ponownym uruchomieniu."],
        ["wiz.pi.failed"] = ["Some changes failed, see the output below.", "Některé změny se nepovedly, podrobnosti jsou ve výpisu.", "Einige Änderungen sind fehlgeschlagen, siehe Ausgabe unten.", "Niektoré zmeny sa nepodarili, podrobnosti sú vo výpise.", "Niektóre zmiany się nie powiodły, szczegóły poniżej."],
        ["wiz.pi.reboot"] = ["Restart the Pi", "Restartovat Pi", "Pi neu starten", "Reštartovať Pi", "Uruchom Pi ponownie"],
        ["wiz.pi.rebootconfirm"] = ["The print server restarts now and is back in about two minutes. Nobody can print meanwhile.", "Tiskový server se teď restartuje a zhruba za dvě minuty bude zpátky. Mezitím nikdo netiskne.", "Der Druckserver startet jetzt neu und ist in etwa zwei Minuten wieder da. In der Zwischenzeit kann niemand drucken.", "Tlačový server sa teraz reštartuje a zhruba o dve minúty bude späť. Medzitým nikto netlačí.", "Serwer wydruku uruchomi się ponownie i wróci za około dwie minuty. W tym czasie nikt nie drukuje."],
        ["wiz.pi.rebooting"] = ["Restarting… the server will be back in a couple of minutes.", "Restartuje se… server bude za pár minut zpátky.", "Neustart läuft… der Server ist in ein paar Minuten zurück.", "Reštartuje sa… server bude o pár minút späť.", "Ponowne uruchamianie… serwer wróci za kilka minut."],

        ["opt.journal.t"] = ["Keep the system log across restarts", "Uchovávat systémový záznam i po restartu", "Systemprotokoll über Neustarts hinweg behalten", "Uchovávať systémový záznam aj po reštarte", "Zachowuj dziennik systemowy po ponownym uruchomieniu"],
        ["opt.journal.d"] = ["Limited to 64 MB. When the Pi freezes, the log shows why.", "Omezený na 64 MB. Když Pi zamrzne, záznam ukáže proč.", "Auf 64 MB begrenzt. Wenn der Pi einfriert, zeigt das Protokoll warum.", "Obmedzený na 64 MB. Keď Pi zamrzne, záznam ukáže prečo.", "Ograniczony do 64 MB. Gdy Pi się zawiesi, dziennik pokaże dlaczego."],
        ["opt.wifi-powersave.t"] = ["Turn off Wi-Fi power saving", "Vypnout úsporný režim Wi-Fi", "Wi-Fi-Energiesparmodus ausschalten", "Vypnúť úsporný režim Wi-Fi", "Wyłącz oszczędzanie energii Wi-Fi"],
        ["opt.wifi-powersave.d"] = ["Power saving makes small Pis drop off the network now and then.", "Úsporný režim způsobuje, že malé Pi občas vypadne ze sítě.", "Der Energiesparmodus lässt kleine Pis ab und zu aus dem Netzwerk fallen.", "Úsporný režim spôsobuje, že malé Pi občas vypadne zo siete.", "Oszczędzanie energii sprawia, że małe Pi czasem znika z sieci."],
        ["opt.watchdog.t"] = ["Hardware watchdog", "Hardwarový hlídací pes", "Hardware-Watchdog", "Hardvérový strážny pes", "Sprzętowy watchdog"],
        ["opt.watchdog.d"] = ["If the Pi freezes, it restarts itself within a minute instead of staying dead.", "Když Pi zamrzne, do minuty se samo restartuje, místo aby zůstalo mrtvé.", "Friert der Pi ein, startet er innerhalb einer Minute selbst neu, statt tot zu bleiben.", "Keď Pi zamrzne, do minúty sa samo reštartuje, namiesto toho, aby zostalo mŕtve.", "Gdy Pi się zawiesi, samo uruchomi się ponownie w ciągu minuty."],
        ["opt.services.t"] = ["Turn off services a print server does not need", "Vypnout služby, které tiskový server nepotřebuje", "Dienste abschalten, die ein Druckserver nicht braucht", "Vypnúť služby, ktoré tlačový server nepotrebuje", "Wyłącz usługi niepotrzebne serwerowi wydruku"],
        ["opt.services.d"] = ["Bluetooth, colour management, modem manager, cloud-init. Saves memory and CPU on small Pis.", "Bluetooth, správa barev, správce modemů, cloud-init. Ušetří paměť a procesor malého Pi.", "Bluetooth, Farbverwaltung, Modem-Manager, cloud-init. Spart Speicher und CPU auf kleinen Pis.", "Bluetooth, správa farieb, správca modemov, cloud-init. Ušetrí pamäť a procesor malého Pi.", "Bluetooth, zarządzanie kolorami, menedżer modemów, cloud-init. Oszczędza pamięć i procesor małego Pi."],
        ["opt.audio.t"] = ["Turn off sound", "Vypnout zvuk", "Ton ausschalten", "Vypnúť zvuk", "Wyłącz dźwięk"],
        ["opt.audio.d"] = ["A print server does not need it. Takes effect after a restart.", "Tiskový server ho nepotřebuje. Projeví se po restartu.", "Ein Druckserver braucht ihn nicht. Wirkt nach einem Neustart.", "Tlačový server ho nepotrebuje. Prejaví sa po reštarte.", "Serwer wydruku go nie potrzebuje. Zadziała po ponownym uruchomieniu."],
        ["opt.updates.t"] = ["Automatic security updates", "Automatické bezpečnostní aktualizace", "Automatische Sicherheitsupdates", "Automatické bezpečnostné aktualizácie", "Automatyczne aktualizacje zabezpieczeń"],
        ["opt.updates.d"] = ["Installed every day, but never with an unannounced restart.", "Instalují se každý den, ale nikdy s nečekaným restartem.", "Täglich installiert, aber nie mit einem unangekündigten Neustart.", "Inštalujú sa každý deň, ale nikdy s nečakaným reštartom.", "Instalowane codziennie, ale nigdy z niezapowiedzianym restartem."],
        ["opt.auto-restart.t"] = ["Weekly restart window", "Týdenní okno pro restart", "Wöchentliches Neustartfenster", "Týždenné okno na reštart", "Cotygodniowe okno restartu"],
        ["opt.auto-restart.d"] = ["Sunday 4:05 – only if an update needs it, nothing is printing and the printer is not lent.", "Neděle 4:05 – jen když to aktualizace vyžaduje, nic se netiskne a tiskárna není zapůjčená.", "Sonntag 4:05 – nur wenn ein Update es verlangt, nichts gedruckt wird und der Drucker nicht ausgeliehen ist.", "Nedeľa 4:05 – len keď to aktualizácia vyžaduje, nič sa netlačí a tlačiareň nie je zapožičaná.", "Niedziela 4:05 – tylko gdy wymaga tego aktualizacja, nic się nie drukuje i drukarka nie jest wypożyczona."],

        ["wiz.pc.h"] = ["USB/IP driver on this PC", "Ovladač USB/IP v tomto počítači", "USB/IP-Treiber auf diesem PC", "Ovládač USB/IP v tomto počítači", "Sterownik USB/IP na tym komputerze"],
        ["wiz.pc.p"] = [
            "For the printer to show up on this PC as a USB device, the open-source usbip-win2 driver is needed. It is signed by Microsoft; Secure Boot and test mode stay as they are.",
            "Aby se tiskárna v tomto PC objevila jako USB zařízení, je potřeba open-source ovladač usbip-win2. Je podepsaný Microsoftem, Secure Boot ani testovací režim se měnit nemusí.",
            "Damit der Drucker auf diesem PC als USB-Gerät erscheint, wird der Open-Source-Treiber usbip-win2 benötigt. Er ist von Microsoft signiert; Secure Boot und Testmodus bleiben unverändert.",
            "Aby sa tlačiareň v tomto PC objavila ako USB zariadenie, je potrebný open-source ovládač usbip-win2. Je podpísaný Microsoftom, Secure Boot ani testovací režim sa meniť nemusí.",
            "Aby drukarka pojawiła się na tym komputerze jako urządzenie USB, potrzebny jest otwarty sterownik usbip-win2. Jest podpisany przez Microsoft; Secure Boot i tryb testowy pozostają bez zmian."],
        ["wiz.pc.installed"] = ["Installed: usbip-win2 {0}", "Nainstalováno: usbip-win2 {0}", "Installiert: usbip-win2 {0}", "Nainštalované: usbip-win2 {0}", "Zainstalowano: usbip-win2 {0}"],
        ["wiz.pc.bad"] = [
            "Version {0} has a known bug that can crash Windows with a blue screen. Installing {1} is recommended.",
            "Verze {0} má známou chybu, která může shodit Windows modrou obrazovkou. Doporučuji nainstalovat {1}.",
            "Version {0} hat einen bekannten Fehler, der Windows mit einem Bluescreen abstürzen lassen kann. Empfohlen wird die Installation von {1}.",
            "Verzia {0} má známu chybu, ktorá môže zhodiť Windows modrou obrazovkou. Odporúčam nainštalovať {1}.",
            "Wersja {0} ma znany błąd, który może powodować niebieski ekran. Zalecana jest instalacja {1}."],
        ["wiz.pc.missing"] = [
            "usbip-win2 is not installed. Without it you can only watch the state and give the printer back.",
            "usbip-win2 není nainstalovaný. Bez něj půjde jen sledovat stav a vracet tiskárnu do sítě.",
            "usbip-win2 ist nicht installiert. Ohne ihn kannst du nur den Status sehen und den Drucker zurückgeben.",
            "usbip-win2 nie je nainštalovaný. Bez neho pôjde len sledovať stav a vracať tlačiareň do siete.",
            "usbip-win2 nie jest zainstalowany. Bez niego można tylko obserwować stan i oddawać drukarkę."],
        ["wiz.pc.install"] = ["Download and install usbip-win2 {0}", "Stáhnout a nainstalovat usbip-win2 {0}", "usbip-win2 {0} herunterladen und installieren", "Stiahnuť a nainštalovať usbip-win2 {0}", "Pobierz i zainstaluj usbip-win2 {0}"],
        ["wiz.pc.downloading"] = ["Downloading… {0:P0}", "Stahuji… {0:P0}", "Herunterladen… {0:P0}", "Sťahujem… {0:P0}", "Pobieranie… {0:P0}"],
        ["wiz.pc.installing"] = ["The driver installer is running – please confirm it.", "Běží instalátor ovladače – potvrď ho prosím.", "Der Treiber-Installer läuft – bitte bestätigen.", "Beží inštalátor ovládača – potvrď ho, prosím.", "Instalator sterownika działa – potwierdź go."],
        ["wiz.pc.restart"] = ["Done. Windows needs a restart after the driver is installed for the first time.", "Hotovo. Po první instalaci ovladače je potřeba restartovat Windows.", "Fertig. Nach der ersten Treiberinstallation muss Windows neu gestartet werden.", "Hotovo. Po prvej inštalácii ovládača treba reštartovať Windows.", "Gotowe. Po pierwszej instalacji sterownika trzeba uruchomić Windows ponownie."],
        ["wiz.pc.installfailed"] = ["Installation failed: {0}", "Instalace se nepovedla: {0}", "Installation fehlgeschlagen: {0}", "Inštalácia sa nepodarila: {0}", "Instalacja nie powiodła się: {0}"],
        ["wiz.pc.source"] = [
            "Source: github.com/vadimgrn/usbip-win2 (GPL-3.0) · the download is verified against a checksum",
            "Zdroj: github.com/vadimgrn/usbip-win2 (GPL-3.0) · stažený soubor se ověřuje kontrolním součtem",
            "Quelle: github.com/vadimgrn/usbip-win2 (GPL-3.0) · der Download wird per Prüfsumme verifiziert",
            "Zdroj: github.com/vadimgrn/usbip-win2 (GPL-3.0) · stiahnutý súbor sa overuje kontrolným súčtom",
            "Źródło: github.com/vadimgrn/usbip-win2 (GPL-3.0) · pobrany plik jest sprawdzany sumą kontrolną"],
        ["wiz.pc.optional"] = ["You can skip this step and come back later.", "Tento krok můžeš přeskočit a vrátit se k němu později.", "Du kannst diesen Schritt überspringen und später zurückkommen.", "Tento krok môžeš preskočiť a vrátiť sa k nemu neskôr.", "Możesz pominąć ten krok i wrócić do niego później."],

        ["wiz.done.h"] = ["All set", "Hotovo", "Alles bereit", "Hotovo", "Gotowe"],
        ["wiz.done.p"] = [
            "Printer Switch lives in the notification area next to the clock. Click its icon to switch the printer with one button.",
            "Přepínač poběží v oznamovací oblasti vedle hodin. Klikni na jeho ikonu a tiskárnu přepneš jedním tlačítkem.",
            "Der Druckerumschalter lebt im Infobereich neben der Uhr. Klicke auf sein Symbol, um den Drucker mit einer Taste umzuschalten.",
            "Prepínač pobeží v oblasti oznámení vedľa hodín. Klikni na jeho ikonu a tlačiareň prepneš jedným tlačidlom.",
            "Przełącznik działa w obszarze powiadomień obok zegara. Kliknij jego ikonę, aby przełączyć drukarkę jednym przyciskiem."],
        ["wiz.done.autostart"] = ["Start when I sign in to Windows", "Spouštět po přihlášení do Windows", "Beim Anmelden bei Windows starten", "Spúšťať po prihlásení do Windows", "Uruchamiaj po zalogowaniu do Windows"],
        ["wiz.done.promote"] = ["Show the icon on the taskbar, not hidden behind the arrow", "Zobrazit ikonu přímo na hlavním panelu, ne pod šipkou", "Symbol direkt in der Taskleiste zeigen, nicht hinter dem Pfeil", "Zobraziť ikonu priamo na paneli úloh, nie pod šípkou", "Pokazuj ikonę na pasku zadań, a nie za strzałką"],
        ["wiz.done.language"] = ["Language", "Jazyk", "Sprache", "Jazyk", "Język"],
        ["wiz.done.printer.h"] = ["Regular network printing from this PC", "Běžný síťový tisk z tohoto PC", "Normales Drucken über das Netzwerk von diesem PC", "Bežná sieťová tlač z tohto PC", "Zwykłe drukowanie sieciowe z tego komputera"],
        ["wiz.done.printer.p"] = [
            "Optional: adds the server's printer to Windows so you can print in network mode too.",
            "Volitelné: přidá do Windows tiskárnu ze serveru, aby šlo tisknout i v síťovém režimu.",
            "Optional: fügt den Drucker des Servers zu Windows hinzu, damit du auch im Netzwerkmodus drucken kannst.",
            "Voliteľné: pridá do Windows tlačiareň zo servera, aby sa dalo tlačiť aj v sieťovom režime.",
            "Opcjonalnie: dodaje drukarkę serwera do Windows, aby można było drukować także w trybie sieciowym."],
    };
}

/// <summary>XAML: Text="{core:Tr wiz.next}"</summary>
public sealed class TrExtension(string key) : MarkupExtension
{
    public string Key { get; set; } = key;
    public override object ProvideValue(IServiceProvider serviceProvider) => L.T(Key);
}
