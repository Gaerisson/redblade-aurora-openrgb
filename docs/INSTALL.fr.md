# Installation et référence technique

**Studio et firmware 0.12 en aperçu :** commencez par la page **Configuration** de Studio pour identifier le clavier, installer le firmware avec l’assistant, préparer OpenRGB et tester les LED. Full/Core 0.12 sont compilés et testés sur PC ; la validation physique 0.12 reste à effectuer. Les précédents essais sur clavier concernent Full 0.11. Studio nécessite Microsoft Edge WebView2 Runtime. Voir [Studio](../app/README.md) et l’[audit](AUDIT.md).

[English](INSTALL.md) · **Français** · [Projet](../README.fr.md) · [Raccourcis](SHORTCUTS.md)


Firmware SonixQMK pour le **Designed by GG Red Blade Full Size ISO**, avec pavé numérique, USB **320F:5136**, contrôleur **SN32F248B**, **109 LED**.

**État : expérimental.** La version 0.1.0 a été flashée et vérifiée sur un Red Blade Full Size : écriture et checksum, VIA 13, VialRGB 1, 109 LED, saisie et couleurs d’Aurora. La version 0.7.0 ajoute les commandes Fn, les fonctions de jeu et la sauvegarde des préférences lumineuses ; son niveau de validation figure dans `dist/manifest.json`. La latence matérielle et les essais prolongés restent à mesurer. Ce dépôt ne contient aucune sauvegarde personnelle.

```text
Aurora (effets et jeux) → serveur SDK OpenRGB → VialRGB → Red Blade FS
```

OpenRGB reste utilisable directement, ou avec d'autres logiciels compatibles avec son SDK. Les jeux utilisent les intégrations des logiciels hôtes ; ce firmware ne promet pas une compatibilité directe avec tous les jeux. VIA conserve le remappage des touches et les macros. L'extension implémente le sous-ensemble VialRGB nécessaire à OpenRGB, pas l'interface complète de configuration Vial.

## Éclairage de secours et vague rouge — 0.7.0

- **USB suspendu ou non configuré :** priorité à l’extinction, même si le port reste alimenté. Les changements d’état USB sont suivis explicitement en plus de `RGB_MATRIX_SLEEP`. Les anciennes couleurs sont effacées ; ni le secours blanc ni l’animation ne rallument le clavier dans cet état.
- **Absence de données RGB en mode Direct :** passage en **blanc à 25 % maximum après 5 secondes**, au prochain rendu (intervalle minimal de 33 ms). Les lectures VIA, diagnostics et paquets invalides ne prolongent pas ce délai. Un nouveau paquet reprend la main ; les anciennes couleurs effacées ne sont pas rejouées.
- **Calculatrice, Muet, Volume −, Volume + :** blanc discret si le flux leur attribue une valeur noire ou ne leur fournit pas de couleur. Une couleur non noire de l’hôte prend priorité. Les quatre LED correspondent aux indices physiques 16..19, vérifiés contre la carte épinglée. Ce choix remplace aussi un noir volontaire sur ces quatre touches ; le protocole ne permet pas de le distinguer d’une touche laissée noire par Aurora. La commande globale **Off** et la suspension USB restent prioritaires.
- **Démarrage / reprise USB :** une vague courbe de **2 secondes**, dégradé rouge pur avec dégradé d’intensité avec traînée douce, entrée progressive et fondu vers les dernières couleurs reçues pendant les 300 dernières millisecondes. Les paquets continuent d’être reçus pendant l’animation ; elle ne bloque pas la saisie. Un éclairage désactivé ou un effet autonome enregistré est respecté. Les notifications USB ordinaires (Num Lock, protocole HID…) ne relancent pas l’animation.
- **Effets autonomes :** le secours blanc et le traitement des quatre touches ne s’appliquent qu’au mode Direct. Les effets autonomes VIA restent utilisables et s’éteignent aussi à la suspension USB.

Les durées et luminosités sont réglables dans `firmware/aurora_via/config.h` avant compilation : `REDBLADE_RGB_TIMEOUT_MS`, `REDBLADE_BOOT_ANIMATION_MS`, `REDBLADE_BOOT_BRIGHTNESS`, `REDBLADE_FALLBACK_BRIGHTNESS`, `REDBLADE_MEDIA_BRIGHTNESS`. Le contrôle du délai se fait une fois par image RGB et la vague utilise le même rendu par blocs de 8 LED, en arithmétique entière. Aucun temporisateur de balayage des touches n’est arrêté ; aucune attente bloquante ni allocation n’est ajoutée. Seul Fn + F9 enregistre explicitement les préférences RedBlade+ ; les commandes ordinaires et le flux RGB n’écrivent pas en EEPROM.

Le firmware connaît l’état du bus USB, pas celui de Windows. L’extinction à l’arrêt du PC fonctionne lorsque le contrôleur USB suspend ou déconfigure la liaison. Si un hôte maintient réellement le bus configuré et actif pendant l’arrêt, l’absence de RGB seule ne permet pas de distinguer cet état d’Aurora fermé : le secours blanc s’applique alors. Ce cas demanderait une configuration d’alimentation USB/ErP ou un signal d’arrêt explicite côté PC. Un arrêt réel et une sortie de veille doivent donc être vérifiés sur la machine cible.

En mode Direct, l’application hôte doit continuer à envoyer des couleurs, même pour un profil visuellement fixe. Un profil OpenRGB fixe envoyé une seule fois passe en blanc après 5 secondes ; pour une couleur fixe sans logiciel actif, choisir le mode autonome **Static**.

Diagnostic en lecture seule (OpenRGB, Aurora et VIA arrêtés) :

```powershell
python tools/redblade.py status
```

Cette commande ne prolonge pas le délai. Elle indique la version, le mode, la fraîcheur du flux, l’âge du dernier paquet et le compteur de paquets acceptés. Son extension privée `FE 80`, format 1, est distincte du protocole VialRGB : version aux octets 3..5, flags à 6 (flux frais, animation, suspension, RGB activé), mode à 7, âge/délai/compteur/durée de démarrage en entiers little-endian 32 bits aux octets 8/12/16/20, signature `RDBLFS01` aux octets 24..31. Un âge `FFFFFFFF` signifie aucune couleur conservée.

## Choix de performance

- Polling USB demandé : **1 ms**. NKRO, matrice, broches et anti-rebond `sym_eager_pk` de 20 ms conservés. Cet anti-rebond est de type eager : il n'ajoute pas une attente initiale de 20 ms à chaque appui.
- Horloge matérielle Sonix conservée à la valeur calculée par le pilote amont pour cette carte : **720 896 Hz**. C'est l'horloge de la minuterie partagée, pas une mesure de la fréquence de balayage complète ni de la latence.
- Le patch découple cette horloge de la taille des blocs de rendu. Le rendu traite au plus **8 LED** par passage et attend au moins **33 ms** entre images.
- Une seule table HSV de **327 octets** garde les dernières couleurs. Aucune allocation dynamique, aucune file d'images et aucune écriture EEPROM dans le chemin RGB direct. Les deux buffers du pilote Sonix amont sont conservés.
- Un paquet direct valide copie au plus **9 couleurs**. La conversion HSV → RGB se fait dans la tâche de rendu, pas dans le gestionnaire USB.
- Les effets autonomes conservés sont simples : couleur fixe, respiration, cycle horizontal, dégradés et alphas/modificateurs. Les effets complexes restent fournis par Aurora/OpenRGB. Full propose aussi des styles réactifs autonomes bornés ; les anciens buffers d’effets complexes restent absents.
- Configuration Aurora proposée : intervalle de **34 ms**, environ **29,4 FPS**. Pas de service Windows supplémentaire.

Ces choix bornent le travail ajouté. Ils ne constituent pas une mesure matérielle de latence : comparer le firmware d'origine et celui-ci avec un analyseur USB ou un dispositif de mesure reste nécessaire. Le transport VialRGB standard utilise 13 échanges pour une image complète de 109 LED ; ses paquets peuvent arriver pendant le rendu d'une image.

## Compilation

Sources amont épinglées : [SonixQMK `62973ee48a976c6f7cc6d4f7b198bd48a2fdfe60`](https://github.com/SonixQMK/qmk_firmware/tree/62973ee48a976c6f7cc6d4f7b198bd48a2fdfe60).

Dans un environnement QMK configuré (QMK MSYS sous Windows, ou les outils QMK sous Linux) :

```sh
git clone https://github.com/SonixQMK/qmk_firmware.git qmk_firmware
git -C qmk_firmware checkout 62973ee48a976c6f7cc6d4f7b198bd48a2fdfe60
bash tools/build.sh /chemin/vers/qmk_firmware
```

`prepare_firmware.py` refuse un autre commit, vérifie la carte des 109 LED, installe le keymap et applique un seul changement au pilote : ajout d'un paramètre d'horloge, avec comportement inchangé par défaut pour les autres cartes. Les sources QMK et leurs sous-modules gardent leurs licences respectives.

## Sauvegarde et flashage

Le flashage remplace le firmware et peut réinitialiser les touches/macros VIA. Sauvegarder avant de continuer, garder un autre moyen de saisie et le firmware de restauration du fabricant.

```powershell
python tools/redblade.py backup --output C:\chemin-prive\via-backup.json
```

La sauvegarde contient les couches et macros : **la garder privée**. Elle ne constitue pas un dump complet du firmware. Ne pas restaurer aveuglément des codes internes entre générations QMK différentes.

1. Vérifier le modèle Full Size, pas le Red Blade 75.
2. Sur RedBlade+ 0.7.0+, maintenir **Fn + Échap pendant 2 secondes**. Les anciennes versions peuvent utiliser Fn + Échap immédiatement.
3. Avec [SonixFlasherC](https://github.com/SonixQMK/SonixFlasherC), vérifier l'identité du bootloader **0C45:7040 / SN32F24xB** avant toute écriture.
4. Pour ce contrôleur, utiliser le binaire brut, à l'offset **0** ; ne pas utiliser un jumploader SN32F26x.

```powershell
sonixflasher.exe -v 0c45/7040 --info
# Seulement après vérification du contrôleur et décision explicite de flasher :
sonixflasher.exe -v 0c45/7040 -f dist/redblade-fs-aurora-via.bin -o 0
```

La référence actuelle est [SonixFlasherC](https://github.com/SonixQMK/SonixFlasherC). L'ancien dépôt `sonix-flasher` est archivé et déconseillé par ses mainteneurs.

Pour revenir au firmware constructeur, suivre la [procédure Designed by GG](https://designedby.gg/qmk-installateur-simplifie/) et utiliser l'archive officielle **Red Blade Full Size**. Le fabricant demande de repasser par le firmware d'origine avant d'utiliser ses installateurs de mise à jour QMK.

## Configuration Windows

OpenRGB doit contenir le contrôleur **QMK VialRGB Devices** (version vérifiée sur le PC de préparation : OpenRGB 1.1). Ajouter dans ses réglages VialRGB : nom `Designed by GG Red Blade FS (VialRGB)`, VID `0x320F`, PID `0x5136`, puis redémarrer complètement OpenRGB.

Le script PowerShell 7 fait cette configuration, sauvegarde les fichiers existants, règle le SDK local et Aurora, et prépare le démarrage des applications :

```powershell
pwsh -File tools/configure-windows.ps1        # lecture / description uniquement
pwsh -File tools/configure-windows.ps1 -Apply # applique puis relance les applications RGB
```

Si un ancien calque VIA a survécu au flash, installer les [nouveaux raccourcis Fn](SHORTCUTS.md#upgrading-an-existing-via-layer) avec les clients RGB arrêtés, puis effectuer la configuration ci-dessous depuis la racine du projet.

La liaison utilise **127.0.0.1:6742**. Dans Aurora, activer **OpenRGB (RGB.NET)**. Choisir **Fn + F1** pour rendre le contrôle au PC. Le clavier doit apparaître dans OpenRGB avec un mode **Direct** avant de valider les effets dans Aurora. Le script ne flashe rien et n'active pas le mode dangereux des périphériques sans mode direct.

Après flashage, relancer `configure-windows.ps1 -Apply`. Il arrête temporairement les clients RGB, sélectionne explicitement **Direct** dans le clavier et enregistre ce réglage une fois, avant de relancer OpenRGB/Aurora. Cela corrige le cas où des couleurs sont envoyées par le SDK alors que le clavier reste sur son effet autonome (par exemple rouge uniforme). L’écriture de configuration ne se produit pas pendant le streaming RGB.

Pour ne changer que ce réglage, avec OpenRGB, Aurora et VIA arrêtés :

```powershell
python tools/set-direct-mode.py --save
```

Fermer VIA pendant l'utilisation d'OpenRGB : les deux outils partagent la même interface Raw HID et peuvent recevoir la réponse destinée à l'autre.

## Vérification

```powershell
python tools/redblade.py verify
```

Cette vérification est en lecture seule et contrôle la signature RGB, la version, les 109 LED et leurs positions uniques. Vérifier ensuite dans OpenRGB les couleurs rouge/vert/bleu, le pavé numérique, les touches ISO, puis le mode Direct avec Aurora, la saisie NKRO, la sortie de veille et le rebranchement USB.

Arrêter OpenRGB, Aurora et VIA avant les commandes Raw HID `redblade.py backup`, `redblade.py verify` ou `set-direct-mode.py`. Une interface partagée avec un client actif peut recevoir une réponse destinée à l'autre outil. Le script de mappage ci-dessous utilise le SDK OpenRGB et peut être exécuté lorsque son serveur est actif.

Après apparition du clavier dans OpenRGB, établir le mappage physique explicite dans Aurora :

```powershell
pwsh -File tools/configure-aurora-mapping.ps1        # vérifie les 109 indices réels
pwsh -File tools/configure-aurora-mapping.ps1 -Apply # sauvegarde et écrit le mappage
```

Relancer ensuite Aurora. Le script utilise le fournisseur RGB.NET livré avec Aurora, donc le mappage ne dépend pas de la reconnaissance des noms de touches QMK dans OpenRGB. Le pavé numérique, les touches ISO et les quatre touches supplémentaires sont inclus.

Si OpenRGB est installé comme service Windows, `configure-windows.ps1` réutilise ce service et sa configuration dans `service_config`. L'application de ces réglages demande alors une console élevée ; aucun deuxième serveur n'est ajouté.

Les tests C compilent le véritable gestionnaire du firmware contre une petite simulation de QMK : passage des commandes VIA, découverte, limites des buffers, toutes les positions de LED, couleurs et modes. Ils couvrent aussi la vague et son fondu vers les couleurs récentes, les quatre touches de secours, la limite exacte de 5 secondes, les requêtes qui ne doivent pas prolonger le délai, la reprise sans ancienne image, le débordement du compteur temporel et la suspension/reprise. Le workflow GitHub les exécute avec AddressSanitizer et UndefinedBehaviorSanitizer. Il ne remplace pas un test sur clavier réel.

## Références

- [Fabricant : Red Blade Full Size](https://designedby.gg/product/red-blade-01/)
- [Définition de carte SonixQMK](https://github.com/SonixQMK/qmk_firmware/tree/62973ee48a976c6f7cc6d4f7b198bd48a2fdfe60/keyboards/designedbygg/redblade/rbfs)
- [Protocole VialRGB](https://github.com/vial-kb/vial-qmk/blob/vial/quantum/vialrgb.c)
- [Contrôleur VialRGB OpenRGB](https://github.com/CalcProgrammer1/OpenRGB/tree/master/Controllers/QMKController/QMKVialRGBController)
- [Aurora](https://github.com/Aurora-RGB/Aurora)

Le keymap est dérivé de celui de Dimitris Mantzouranis dans SonixQMK. Les sources ajoutées sont distribuées sous GPL-2.0-or-later ; voir `LICENSE` et les mentions des composants amont.

## Diagnostic RedBlade+

`python tools/redblade.py status` affiche aussi le mode jeu, le profil, la luminosité, les options, la cible/cadence Turbo et le maintien pour avancer à partir de la 0.7.0. Commande en lecture seule ; détails dans [le guide des raccourcis](SHORTCUTS.md).

## Compatibilité du mode Direct — 0.7.0

En mode Direct, chaque pixel porte sa luminosité. Les champs HSV/vitesse globaux inutilisés, parfois à zéro dans OpenRGB, ne modifient plus la luminosité du moteur QMK. Le mode autonome continue à les utiliser. Le rendu personnalisé et les indicateurs utilisent la conversion QMK sans seconde courbe CIE : les couleurs faibles du PC et les voyants restent visibles. La vague rouge dure 2 secondes, avec une crête HSV 255 avant les limites de luminosité ; le secours multimédia reste à 64 en valeur linéaire.

FE81, octet 19 : bit 0 = choix Turbo armé/en attente ; bit 1 = guide lumineux temporaire. Les octets 16/17 restent la touche HID et la cadence cible, désormais une touche standard prise en charge et 5–100/s. Ces lectures ne prolongent pas le délai RGB. [Guide français des raccourcis](SHORTCUTS.fr.md).

## Recorder — 0.7.0

Les trois slots Recorder utilisent une arène RAM distincte des macros VIA. Aucun enregistrement n’est écrit en EEPROM. Le diagnostic FE82 expose seulement l’état, le slot, la vitesse, les tailles et la limite : jamais les frappes enregistrées. Le [guide français](SHORTCUTS.fr.md) détaille les commandes, la mémoire et la durée de vie USB. L’installateur Fn applique 33 commandes et 3 effacements explicites.
