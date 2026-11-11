// SPDX-License-Identifier: GPL-2.0-or-later
namespace RedBladePlus.Studio;
internal static class StudioTextTests
{
    internal static void Run()
    {
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        const string flash = "Programming and verifying firmware. Keep the keyboard connected; this operation cannot be cancelled.";
        Check(StudioText.Localize(flash, false) == flash, "English messages must remain unchanged");
        Check(StudioText.Localize(flash, true).Contains("clavier connecté"), "French flashing instructions must retain safety guidance");
        const string dimensions = "VIA dimensions changed: backup protocol 13, 3 layers, 259 macro bytes; keyboard protocol 13, 3 layers, 100 macro bytes. Automatic restore refused; private backup retained.";
        var translated = StudioText.Localize(dimensions, true);
        Check(translated.Contains("259") && translated.Contains("100") && translated.Contains("refusée") && !translated.Contains("layers"), "French dimensions retain numeric facts and refusal");
        Check(StudioText.Localize("Unexpected system failure", true, true) == "Opération interrompue. Consultez le diagnostic pour les détails.", "unknown technical errors use a safe French fallback");
        Check(StudioText.Localize("", true, true) == "", "empty messages stay empty");
    }
}
