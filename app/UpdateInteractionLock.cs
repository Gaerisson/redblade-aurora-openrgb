// SPDX-License-Identifier: GPL-2.0-or-later
namespace RedBladePlus.Studio;

// Freeze editing and new hardware actions across the asynchronous updater handshake.
internal sealed class UpdateInteractionLock:IDisposable
{
    readonly (Form Form,bool Enabled)[] windows;bool completed;
    internal UpdateInteractionLock(Form owner)
    {
        windows=owner.OwnedForms.Prepend(owner).Select(f=>(f,f.Enabled)).ToArray();
        foreach(var window in windows){window.Form.FormClosing+=PreventClosing;window.Form.Enabled=false;}
    }
    void PreventClosing(object? sender,FormClosingEventArgs e){if(!completed)e.Cancel=true;}
    internal void Complete()=>completed=true;
    public void Dispose()
    {
        foreach(var window in windows)
        {
            window.Form.FormClosing-=PreventClosing;
            if(!completed&&!window.Form.IsDisposed)window.Form.Enabled=window.Enabled;
        }
    }
}
