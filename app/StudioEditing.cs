// SPDX-License-Identifier: GPL-2.0-or-later
namespace RedBladePlus.Studio;
internal sealed partial class StudioForm
{
    async Task ReadPreservingEdits()
    {
        OnboardProfile? local=null;
        await PerformHardware(()=>
        {
            if(dirty)
            {
                var answer=MessageBox.Show(this,T("Keep your local edits while connecting?\nYes: keep edits. No: load the keyboard settings. Cancel: stay offline.","Conserver vos modifications locales lors de la connexion ?\nOui : conserver les modifications. Non : charger les réglages du clavier. Annuler : rester hors ligne."),Text,MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question);
                if(answer==DialogResult.Cancel)return Task.FromResult<JobRequest?>(null);
                if(answer==DialogResult.Yes)local=KeyboardEditing.Clone(config).Profiles[config.Active];
            }
            return Task.FromResult<JobRequest?>(new(){Operation=HardwareOperation.Read});
        },afterSuccess:()=>{if(local!=null)AcceptEdit(KeyboardEditing.WithActiveProfile(config,local));});
    }
}
