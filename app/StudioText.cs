// SPDX-License-Identifier: GPL-2.0-or-later
using System.Text.RegularExpressions;
namespace RedBladePlus.Studio;
internal static class StudioText
{
    static readonly Dictionary<string,string> French = new(StringComparer.Ordinal)
    {
        ["OpenRGB devices changed; checking the keyboard again..."]="Les périphériques OpenRGB ont changé ; nouvelle vérification du clavier…",
        ["OpenRGB keyboard rediscovered; effects resumed."]="Clavier OpenRGB retrouvé ; reprise de l’animation.",
        ["OpenRGB device list keeps changing; effects stopped safely."]="La liste des périphériques OpenRGB change sans cesse ; l’effet a été arrêté.",
        ["Preparing hardware operation…"]="Préparation de l’opération sur le clavier…",
        ["Pausing current RGB clients for this operation…"]="Mise en pause des applications RVB pendant l’opération…",
        ["Starting OpenRGB effects…"]="Démarrage des effets OpenRGB…",
        ["Applying temporary keyboard control…"]="Application temporaire de la commande au clavier…",
        ["Temporary keyboard control applied."]="Commande temporaire appliquée au clavier.",
        ["Reading four onboard profiles…"]="Lecture des quatre profils enregistrés dans le clavier…",
        ["Four profiles read. RGB control is being returned to your applications."]="Les quatre profils ont été lus. Le contrôle RVB est rendu à vos applications.",
        ["Profile settings require a newer Full firmware; modifier dimming requires Full 0.14 and maximum highlight intensity requires Full 0.15."]="Les réglages des profils nécessitent une version Full plus récente ; l’atténuation nécessite Full 0.14 et les repères à intensité maximale nécessitent Full 0.15.",
        ["All four profiles saved and verified."]="Les quatre profils ont été enregistrés et vérifiés.",
        ["Invalid effect update ignored; the previous settings remain active."]="Modification d’effet invalide ignorée ; les réglages précédents restent actifs.",
        ["Effect update could not be read; the previous settings remain active."]="Impossible de lire la modification d’effet ; les réglages précédents restent actifs.",
        ["Effect stopped. Restoring your RGB applications…"]="Effet arrêté. Restauration de vos applications RVB…",
        ["Temporary per-key preview · 5 seconds · saved profiles are unchanged."]="Aperçu temporaire par touche · 5 secondes · les profils enregistrés restent inchangés.",
        ["Temporary preview finished; original runtime lighting restored."]="Aperçu terminé ; l’éclairage initial a été rétabli.",
        ["Preparing private backup…"]="Préparation de la sauvegarde privée…",
        ["Backing up VIA layers and macros privately before bootloader entry…"]="Sauvegarde privée des couches et macros VIA avant le passage en mode de programmation…",
        ["Backup complete. Hold Fn + Esc for 2 seconds now. Waiting up to 2 minutes for the bootloader…"]="Sauvegarde terminée. Maintenez maintenant Fn + Échap pendant 2 secondes. Attente du mode de programmation pendant 2 minutes au maximum…",
        ["Programming and verifying firmware. Keep the keyboard connected; this operation cannot be cancelled."]="Programmation et vérification du micrologiciel. Gardez le clavier connecté ; cette opération ne peut pas être annulée.",
        ["Programming is still running. Keep the keyboard connected; waiting for the flasher to finish."]="La programmation est toujours en cours. Gardez le clavier connecté jusqu’à sa fin.",
        ["Firmware checksum verified. Waiting for the normal keyboard interface…"]="Somme de contrôle du micrologiciel vérifiée. Attente de l’interface normale du clavier…",
        ["Firmware identity verified. Checking VIA preservation…"]="Identité du micrologiciel vérifiée. Vérification de la conservation des données VIA…",
        ["New firmware verified. Stock VIA backup retained for manual migration; the new factory keymap is active as acknowledged before flashing."]="Nouveau micrologiciel vérifié. La sauvegarde VIA d’origine est conservée pour une migration manuelle ; la nouvelle disposition d’usine est active, comme indiqué avant la programmation.",
        ["Restoring the four backed-up onboard profiles…"]="Restauration des quatre profils sauvegardés dans le clavier…",
        ["Restoring changed VIA data from the private backup…"]="Restauration des données VIA modifiées depuis la sauvegarde privée…",
        ["Firmware, reconnection, VIA and supported onboard profiles verified. RGB applications are being restored."]="Micrologiciel, reconnexion, VIA et profils compatibles vérifiés. Restauration des applications RVB…",
        ["Firmware and reconnection verified. New factory keymap active; stock VIA backup retained for manual migration. RGB applications are being restored."]="Micrologiciel et reconnexion vérifiés. La nouvelle disposition d’usine est active ; la sauvegarde VIA d’origine est conservée pour une migration manuelle. Restauration des applications RVB…",
        ["Core flashed and verified. Full profile backup retained privately; Core cannot restore Full settings."]="Core programmé et vérifié. La sauvegarde privée des profils Full est conservée ; Core ne peut pas restaurer ces réglages.",
        ["Stock / legacy firmware identified; its version is unavailable. Install Full using the guided installer. Existing VIA data will be backed up privately; stock keycodes cannot be migrated automatically."]="Micrologiciel d’origine ou ancien détecté ; sa version est inconnue. Installez Full avec l’assistant. Les données VIA seront sauvegardées en privé ; les codes de touches d’origine ne peuvent pas être migrés automatiquement.",
        ["OpenRGB Core identified. Full profile configuration is unavailable on Core."]="OpenRGB Core détecté. La configuration des profils Full n’est pas disponible dans Core.",
        ["Older firmware identified. Upgrade to Full 0.9.0 or later to save profiles."]="Ancien micrologiciel détecté. Installez Full 0.9.0 ou une version ultérieure pour enregistrer les profils.",
        ["Downloading verified OpenRGB 1.1…"]="Téléchargement d’OpenRGB 1.1 avec vérification…",
        ["Checking OpenRGB and the RedBlade…"]="Vérification d’OpenRGB et du RedBlade…",
        ["OpenRGB connected"]="OpenRGB connecté",
        ["Lighting test complete. Restoring your RGB applications…"]="Test d’éclairage terminé. Restauration de vos applications RVB…",
        ["No OpenRGB server detected the RedBlade with 109 LEDs. Run OpenRGB setup and check the keyboard connection."]="Aucun serveur OpenRGB n’a détecté le RedBlade et ses 109 LED. Lancez la configuration OpenRGB et vérifiez la connexion du clavier.",
        ["OpenRGB started, but the RedBlade was not detected. Check its USB connection and firmware."]="OpenRGB a démarré, mais le RedBlade n’a pas été détecté. Vérifiez sa connexion USB et son micrologiciel.",
        ["Managed OpenRGB did not start."]="L’instance OpenRGB gérée n’a pas démarré.",
        ["Managed OpenRGB exited before its SDK was ready."]="L’instance OpenRGB gérée s’est arrêtée avant que son SDK soit prêt.",
        ["Managed OpenRGB SDK did not start."]="Le SDK OpenRGB géré n’a pas démarré.",
        ["No compatible release found."]="Aucune version compatible trouvée.",
        ["Release metadata retrieved from GitHub."]="Informations de version récupérées depuis GitHub.",
        ["VIA dimensions changed. Automatic restore refused; private backup retained."]="Les dimensions VIA ont changé. Restauration automatique refusée ; sauvegarde privée conservée.",
        ["Invalid VIA dimensions."]="Dimensions VIA invalides.",
        ["VIA restore failed complete readback. Private backup retained."]="La vérification complète de la restauration VIA a échoué. Sauvegarde privée conservée.",
        ["The previous firmware keycode generation is unknown. Automatic VIA restore refused; private backup retained for manual migration."]="La génération des codes de touches de l’ancien micrologiciel est inconnue. Restauration VIA automatique refusée ; sauvegarde privée conservée pour une migration manuelle.",
        ["Stock / legacy VIA data was backed up privately. Connect the keyboard first, then restart the guided installer and acknowledge that the stock keymap/macros cannot be restored automatically. No firmware was programmed."]="Les données VIA d’origine ou anciennes ont été sauvegardées en privé. Connectez d’abord le clavier, puis relancez l’assistant et confirmez que la disposition et les macros d’origine ne peuvent pas être restaurées automatiquement. Aucun micrologiciel n’a été programmé.",
        ["The backed-up profiles contain settings unsupported by this older Full firmware. Flashing stopped; the private backup was retained."]="Les profils sauvegardés contiennent des réglages incompatibles avec cette ancienne version Full. Programmation arrêtée ; sauvegarde privée conservée.",
        ["A unique Redblade USB port could not be identified. Flashing refused."]="Impossible d’identifier un port USB Redblade unique. Programmation refusée.",
        ["A Sonix bootloader is already connected. Disconnect it before starting the flash."]="Un appareil Sonix en mode de programmation est déjà connecté. Déconnectez-le avant de commencer.",
        ["Flash cancelled before programming. Private backup retained."]="Installation annulée avant programmation. Sauvegarde privée conservée.",
        ["Flash cancelled before programming."]="Installation annulée avant programmation.",
        ["Several matching Sonix bootloaders are connected. Disconnect the others before flashing."]="Plusieurs appareils Sonix en mode de programmation sont connectés. Déconnectez les autres avant de continuer.",
        ["Bootloader USB port does not match the original Redblade. Flashing refused."]="Le port USB du mode de programmation ne correspond pas au Redblade initial. Programmation refusée.",
        ["No unique Sonix bootloader appeared. No firmware was programmed."]="Aucun appareil Sonix unique en mode de programmation n’a été détecté. Aucun micrologiciel n’a été programmé.",
        ["Bootloader must identify SN32F24XB, 64 KB and CS0. Programming refused."]="Le mode de programmation doit identifier SN32F24XB, 64 Ko et CS0. Programmation refusée.",
        ["Bootloader identity became ambiguous."]="L’identité de l’appareil en mode de programmation est devenue ambiguë.",
        ["Firmware programming/checksum verification failed. Private backups and flash log were retained; use recovery guidance."]="Échec de la programmation ou de la vérification du micrologiciel. Les sauvegardes privées et le journal sont conservés ; consultez les instructions de récupération.",
        ["Flash verification did not finish."]="La vérification de la programmation n’a pas abouti.",
        ["The keyboard returned an unsupported identity while reconnecting."]="Le clavier a renvoyé une identité non prise en charge lors de la reconnexion.",
        ["Reconnected firmware edition/version does not match the flashed image. Backups retained."]="L’édition ou la version du micrologiciel reconnecté ne correspond pas à l’image programmée. Sauvegardes conservées.",
        ["Operation failed and RGB restoration also needs attention."]="L’opération a échoué et la restauration RVB nécessite également votre attention.",
        ["RGB restoration needs attention."]="La restauration RVB nécessite votre attention.",
        ["Original keyboard lighting could not be fully restored."]="L’éclairage initial du clavier n’a pas pu être entièrement restauré.",
        ["Another Studio hardware job is already running."]="Une autre opération matérielle Studio est déjà en cours.",
        ["This explicit hardware job requires administrator access to pause the OpenRGB service."]="Cette opération nécessite les droits administrateur pour mettre le service OpenRGB en pause.",
        ["Core has no onboard Studio configuration. Install Full to enable it."]="Core ne dispose pas de configuration Studio intégrée. Installez Full pour l’activer.",
        ["This firmware needs the bundled Full upgrade before profile operations."]="Ce micrologiciel doit être mis à jour vers la version Full intégrée avant toute opération sur les profils.",
        ["Keyboard rejected stop command."]="Le clavier a refusé la commande d’arrêt.",
        ["Unsupported runtime lighting snapshot."]="État d’éclairage actuel non pris en charge.",
        ["Unsupported prior RGB mode."]="Mode RVB précédent non pris en charge.",
        ["Unsupported prior VialRGB mode."]="Mode VialRGB précédent non pris en charge.",
        ["Unsupported firmware. Upgrade Full firmware to use effects."]="Micrologiciel non pris en charge. Mettez à jour Full pour utiliser les effets.",
        ["Select Host with Fn+F1 or upgrade to Full 0.10 before starting software effects."]="Sélectionnez Host avec Fn+F1 ou installez Full 0.10 avant de démarrer les effets logiciels.",
        ["Managed OpenRGB installation verification failed."]="La vérification de l’installation gérée d’OpenRGB a échoué.",
        ["Another OpenRGB SDK provider is still active; exclusive keyboard control refused."]="Un autre fournisseur du SDK OpenRGB est encore actif ; le contrôle exclusif du clavier est refusé.",
        ["Could not restart OpenRGB service."]="Impossible de redémarrer le service OpenRGB.",
        ["Could not pause OpenRGB service."]="Impossible de mettre le service OpenRGB en pause.",
        ["OpenRGB service transition timed out."]="Le changement d’état du service OpenRGB a expiré.",
        ["Protected firmware copy verification failed."]="La vérification de la copie protégée du micrologiciel a échoué.",
        ["Bundled flasher could not start."]="L’outil de programmation intégré n’a pas pu démarrer.",
        ["Cannot preserve RGB startup command. No clients were paused."]="Impossible de conserver la commande de démarrage RVB. Aucune application n’a été mise en pause.",
        ["Saved profiles did not pass complete readback."]="La vérification complète des profils enregistrés a échoué.",
        ["Mismatched save acknowledgement; transaction stopped."]="Accusé d’enregistrement inattendu ; transaction arrêtée.",
        ["Unexpected VIA reply; close competing clients."]="Réponse VIA inattendue ; fermez les autres applications utilisant le clavier.",
        ["Unsupported VIA protocol."]="Protocole VIA non pris en charge.",
        ["Mismatched keyboard identity response. Close competing clients and read again."]="Réponse d’identité du clavier inattendue. Fermez les autres applications puis relancez la lecture.",
        ["HID response timed out. The write may have reached the keyboard; it was not retried."]="Délai de réponse HID dépassé. L’écriture a peut-être atteint le clavier ; elle n’a pas été répétée.",
        ["Cannot open keyboard. Close VIA and other keyboard tools."]="Impossible d’ouvrir le clavier. Fermez VIA et les autres logiciels de gestion du clavier.",
        ["OpenRGB archive SHA-256 mismatch."]="La somme SHA-256 de l’archive OpenRGB ne correspond pas.",
        ["Untrusted OpenRGB download host."]="La source du téléchargement OpenRGB n’est pas fiable.",
        ["Untrusted OpenRGB download redirect."]="La redirection du téléchargement OpenRGB n’est pas fiable.",
        ["OpenRGB download exceeds the limit."]="Le téléchargement OpenRGB dépasse la taille autorisée.",
        ["Release asset length or SHA-256 mismatch."]="La taille ou la somme SHA-256 du fichier de mise à jour ne correspond pas.",
        ["Firmware identity, version, edition, length or SHA-256 mismatch."]="L’identité, la version, l’édition, la taille ou la somme SHA-256 du micrologiciel ne correspond pas.",
    };
    internal static string Localize(string text, bool french, bool diagnosticFallback = false)
    {
        if (!french || string.IsNullOrEmpty(text)) return text;
        if (French.TryGetValue(text, out var translated)) return translated;
        var m = Regex.Match(text, @"^VIA dimensions changed: backup protocol (\d+), (\d+) layers, (\d+) macro bytes; keyboard protocol (\d+), (\d+) layers, (\d+) macro bytes\. Automatic restore refused; private backup retained\.$", RegexOptions.CultureInvariant);
        if (m.Success) return $"Les dimensions VIA ont changé : sauvegarde, protocole {m.Groups[1].Value}, {m.Groups[2].Value} couches, {m.Groups[3].Value} octets de macros ; clavier, protocole {m.Groups[4].Value}, {m.Groups[5].Value} couches, {m.Groups[6].Value} octets de macros. Restauration automatique refusée ; sauvegarde privée conservée.";
        m = Regex.Match(text, @"^Saving onboard profiles · (\d+)%$", RegexOptions.CultureInvariant);
        if (m.Success) return $"Enregistrement des profils dans le clavier · {m.Groups[1].Value} %";
        m = Regex.Match(text, @"^OpenRGB SDK did not (start|return) on port (\d+)\.$", RegexOptions.CultureInvariant);
        if (m.Success) return $"Le SDK OpenRGB n’a pas {(m.Groups[1].Value == "start" ? "démarré" : "redémarré")} sur le port {m.Groups[2].Value}.";
        m = Regex.Match(text, @"^RedBlade detected in OpenRGB on port (\d+)\.$", RegexOptions.CultureInvariant);
        if (m.Success) return $"RedBlade détecté dans OpenRGB sur le port {m.Groups[1].Value}.";
        m = Regex.Match(text, @"^OpenRGB is ready and the RedBlade was detected on port (\d+)\.$", RegexOptions.CultureInvariant);
        if (m.Success) return $"OpenRGB est prêt et le RedBlade a été détecté sur le port {m.Groups[1].Value}.";
        m = Regex.Match(text, @"^Another OpenRGB SDK provider is still active on port (\d+); exclusive keyboard control refused\.$", RegexOptions.CultureInvariant);
        if (m.Success) return $"Un autre fournisseur du SDK OpenRGB est encore actif sur le port {m.Groups[1].Value} ; le contrôle exclusif du clavier est refusé.";
        if (text.StartsWith("Flasher checksum passed, but keyboard identity could not be verified within the reconnect window. Backups retained. Last HID error: ", StringComparison.Ordinal))
            return "Somme de contrôle vérifiée, mais l’identité du clavier n’a pas pu être vérifiée dans le délai de reconnexion. Sauvegardes conservées. Consultez le diagnostic pour les détails.";
        if (text.StartsWith("Update check unavailable:", StringComparison.Ordinal)) return "Recherche de mises à jour indisponible. Consultez le diagnostic pour les détails.";
        if(French.ContainsValue(text))return text;
        if(text.StartsWith("Both supported OpenRGB SDK ports are already in use.",StringComparison.Ordinal))return "Les deux ports SDK OpenRGB sont déjà utilisés. Les serveurs existants ont été conservés ; fermez un serveur inutilisé puis relancez la configuration.";
        return diagnosticFallback ? "Opération interrompue. Consultez le diagnostic pour les détails." : text;
    }
}
