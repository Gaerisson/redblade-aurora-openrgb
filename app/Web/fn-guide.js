(function (root, factory) {
    const api = factory();
    if (typeof module === 'object' && module.exports) module.exports = api;
    if (root) root.RedBladeFnGuide = api;
})(typeof globalThis !== 'undefined' ? globalThis : this, function () {
    'use strict';
    // Default factory Fn layer; VIA remapping may change the physical bindings.
    // Entries: LEDs, key label, group, required module mask (any bit), EN, FR, shifted EN/FR.
    const rows = [
        [[0],'Esc','safety',0,'Stop automation and unlock Windows keys; hold for 2 seconds to enter bootloader.','Arrête les automatismes et déverrouille les touches Windows ; maintenir 2 secondes pour le bootloader.'],
        [[1],'F1','lighting',0,'Select the Host lighting profile.','Sélectionne le profil lumineux Host.'],
        [[2],'F2','lighting',0,'Select the white lighting profile.','Sélectionne le profil lumineux blanc.'],
        [[3],'F3','lighting',0,'Select the red lighting profile.','Sélectionne le profil lumineux rouge.'],
        [[4],'F4','lighting',0,'Turn lighting off.','Éteint l’éclairage.'],
        [[5],'F5','lighting',0,'Toggle lighting animation.','Active ou désactive l’animation lumineuse.'],
        [[6],'F6','lighting',0,'Toggle the media lighting fallback.','Active ou désactive le mode lumineux de secours multimédia.'],
        [[7],'F7','lighting',0,'Toggle the idle white lighting fallback.','Active ou désactive le blanc de secours au repos.'],
        [[8],'F8','safety',0,'Reset lighting and speeds to defaults, unlock Windows and stop automation; recorded slots are preserved.','Rétablit l’éclairage et les vitesses par défaut, déverrouille Windows et arrête les automatismes ; les enregistrements sont conservés.'],
        [[9],'F9','lighting',0,'Save lighting settings only.','Enregistre uniquement les réglages d’éclairage.'],
        [[15],'Pause','media',0,'Stop media playback.','Arrête la lecture multimédia.'],
        [[16],'Calc','recorder',128,'Arm recording or stop the active recorder; then select slot 1, 2 or 3.','Arme l’enregistrement ou arrête l’enregistreur actif ; choisir ensuite l’emplacement 1, 2 ou 3.'],
        [[17],'Mute','media',0,'Play or pause media. After Fn + T, choose middle-click Turbo (Full 0.13).','Lance ou met en pause la lecture multimédia. Après Fn + T, choisit le Turbo clic milieu (Full 0.13).','Stop media playback.','Arrête la lecture multimédia.'],
        [[18],'Volume −','media',0,'Previous media track.','Piste multimédia précédente.','Decrease lighting brightness.','Diminue la luminosité de l’éclairage.'],
        [[19],'Volume +','media',0,'Next media track.','Piste multimédia suivante.','Increase lighting brightness.','Augmente la luminosité de l’éclairage.'],
        [[21],'1','recorder',128,'Record into slot 1 if armed, otherwise play slot 1 once.','Enregistre dans l’emplacement 1 si armé, sinon le lit une fois.','Record into slot 1 if armed, otherwise toggle loop playback of slot 1.','Enregistre dans l’emplacement 1 si armé, sinon active ou arrête sa lecture en boucle.'],
        [[22],'2','recorder',128,'Record into slot 2 if armed, otherwise play slot 2 once.','Enregistre dans l’emplacement 2 si armé, sinon le lit une fois.','Record into slot 2 if armed, otherwise toggle loop playback of slot 2.','Enregistre dans l’emplacement 2 si armé, sinon active ou arrête sa lecture en boucle.'],
        [[23],'3','recorder',128,'Record into slot 3 if armed, otherwise play slot 3 once.','Enregistre dans l’emplacement 3 si armé, sinon le lit une fois.','Record into slot 3 if armed, otherwise toggle loop playback of slot 3.','Enregistre dans l’emplacement 3 si armé, sinon active ou arrête sa lecture en boucle.'],
        [[33],'Backspace','lighting',0,'Toggle lighting.','Allume ou éteint l’éclairage.'],
        [[36,106],'Brightness +','lighting',0,'Increase lighting brightness.','Augmente la luminosité de l’éclairage.'],
        [[56,104],'Brightness −','lighting',0,'Decrease lighting brightness.','Diminue la luminosité de l’éclairage.'],
        [[40],'Numpad −','automation',224,'Decrease the current turbo rate or recorder playback speed; hold to repeat.','Diminue la cadence turbo ou la vitesse de lecture de l’enregistreur selon le contexte ; maintenir pour répéter.'],
        [[60],'Numpad +','automation',224,'Increase the current turbo rate or recorder playback speed; hold to repeat.','Augmente la cadence turbo ou la vitesse de lecture de l’enregistreur selon le contexte ; maintenir pour répéter.'],
        [[43],'W','automation',256,'Toggle automatic W hold.','Active ou arrête le maintien automatique de W.'],
        [[49],'I','automation',256,'Full 0.13: toggle Anti-AFK; after 30 seconds idle, moves the mouse and briefly presses W/S. These keys can type outside games. Typing or another helper stops it.','Full 0.13 : active ou arrête Anti-AFK ; après 30 secondes sans activité, bouge la souris et appuie brièvement sur W/S (Z/S en AZERTY). Ces touches peuvent saisir du texte hors jeu. Une frappe ou une autre fonction l’arrête.'],
        [[44],'E','automation',32,'Toggle E turbo.','Active ou arrête le turbo E.'],
        [[65],'F','automation',32,'Toggle F turbo.','Active ou arrête le turbo F.'],
        [[99],'Space','automation',32,'Toggle Space turbo.','Active ou arrête le turbo Espace.'],
        [[45],'R','automation',96,'Cycle turbo rate forward through 25, 50 and 100.','Fait défiler les cadences turbo 25, 50 et 100 dans l’ordre.','Cycle turbo rate backward through 100, 50 and 25.','Fait défiler les cadences turbo 100, 50 et 25 en sens inverse.'],
        [[46],'T','automation',96,'Select a key or supported mouse button for turbo, or stop the current turbo.','Sélectionne une touche ou un bouton de souris pris en charge pour le turbo, ou arrête le turbo actif.'],
        [[61],'Caps Lock','automation',1024,'Toggle Caps Word.','Active ou désactive Caps Word.'],
        [[97],'Left Windows','safety',512,'Toggle Windows key lock.','Active ou désactive le verrouillage des touches Windows.','Explicitly unlock Windows keys.','Déverrouille explicitement les touches Windows.'],
        [[101],'Right Windows','safety',512,'Toggle Windows key lock.','Active ou désactive le verrouillage des touches Windows.','Explicitly unlock Windows keys.','Déverrouille explicitement les touches Windows.']
    ];
    const byLed = new Map();
    for (const row of rows) for (const led of row[0]) byLed.set(led, row);
    const leds = Object.freeze(Array.from(byLed.keys()).sort((a, b) => a - b));
    function helpFor(led, shift, language, profile = { modules: 1023 }) {
        const row = byLed.get(led);
        if (!row) return null;
        const fr = String(language || '').toLowerCase().startsWith('fr');
        const enabled = row[3] === 0 || !!((profile && profile.modules || 0) & row[3]);
        const index = shift && row[6] ? 6 : 4;
        let description = row[index + (fr ? 1 : 0)];
        if (!enabled) description += fr ? ' Module requis désactivé dans ce profil.' : ' Required module is disabled in this profile.';
        let key = row[1];
        if (led === 36) key = 'Page Up'; else if (led === 106) key = 'Right';
        else if (led === 56) key = 'Page Down'; else if (led === 104) key = 'Left';
        return { keys: 'Fn + ' + (shift ? (fr ? 'Maj + ' : 'Shift + ') : '') + key, description, group: row[2], enabled };
    }
    return { helpFor, leds };
});
