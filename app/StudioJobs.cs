// SPDX-License-Identifier: GPL-2.0-or-later
using System.ComponentModel;
namespace RedBladePlus.Studio;
internal sealed partial class StudioForm
{
    async Task PerformHardware(Func<Task<JobRequest?>> prepare,bool isEffect=false,Action? afterSuccess=null)
    {
        if(updates?.Installing==true||!await operationGate.WaitAsync(0))throw new InvalidOperationException(T("A keyboard operation is already running.","Une opération clavier est déjà en cours."));
        cancelPreparation=false;SendState();
        try
        {
            if(effectActive)await StopEffectsOrCancel();
            if(cancelPreparation){job=null;return;}
            job=new(){Stage="prepare",Message=T("Preparing the keyboard operation…","Préparation de l’opération clavier…")};SendState();
            var request=await prepare();
            if(request==null||cancelPreparation){job=null;return;}
            if(isEffect)runningJob=RunHardware(request,true);else{await RunHardware(request);afterSuccess?.Invoke();}
        }
        catch(Exception error){if(job?.Done!=true)job=new(){Done=true,Stage="error",Message=error.Message};throw;}
        finally{operationGate.Release();SendState();}
    }
    async Task RunHardware(JobRequest request,bool isEffect=false)
    {
        if(preview)throw new InvalidOperationException("Hardware disabled in preview.");
        if(busy)throw new InvalidOperationException("A keyboard operation is already active.");
        busy=true;effectActive=isEffect;lastError="";job=new();if(updates!=null)updates.HardwareBusy=true;SendState();
        try
        {
            var started=HardwareJobs.Start(request);jobId=started.Id;using var process=started.Process;
            while(true)
            {
                var result=HardwareJobs.Poll(jobId);if(result!=null){job=result;SendState();if(result.Done)break;}
                if(process.HasExited){job=HardwareJobs.Poll(jobId)??job;if(job?.Done!=true)throw new IOException(T("The keyboard worker ended before reporting its result. Consult the local diagnostic log.","Le processus clavier s’est arrêté sans résultat. Consultez le rapport local de diagnostic."));break;}
                await Task.Delay(200);
            }
            if(job?.Success!=true)throw new IOException(job?.Message??"No keyboard result.");
            if(!isEffect)ApplyHardwareResult(job,request.Operation is HardwareOperation.Read or HardwareOperation.Flash);
        }
        catch(Exception error)
        {
            string message=error is Win32Exception win&&win.NativeErrorCode==1223?T("Windows authorization was cancelled.","L’autorisation Windows a été annulée."):error.Message;
            lastError=message;Diagnostics.Record(error,"Keyboard operation "+request.Operation);job=new(){Done=true,Success=false,Stage="error",Message=message};
            if(!isEffect)throw new IOException(message,error);
        }
        finally
        {
            jobId=null;busy=false;effectActive=false;if(updates!=null)updates.HardwareBusy=false;SendState();
            if(closeAfterEffect){closeAfterEffect=false;BeginInvoke((Action)Close);}
        }
    }
    async Task StopEffectsOrCancel(bool explicitCancel=false)
    {
        if(explicitCancel&&Transitioning&&jobId==null)cancelPreparation=true;
        if(jobId!=null&&job?.Programming!=true)HardwareJobs.Cancel(jobId);
        if(effectActive&&runningJob!=null)await runningJob;
    }
    internal void ApplyHardwareResult(JobResult result,bool resetHistory=true)
    {
        firmware=result.Firmware;
        if(firmware.Length>0)Version.TryParse(firmware.Split(' ').Last(),out installedVersion);
        connection=result.Unsupported?"upgrade":"connected";
        if(result.Payload.Length>0){config=ConfigCodec.Decode(Convert.FromBase64String(result.Payload));generation=result.Generation;dirty=false;if(resetHistory)history.Clear();SaveDraft();}
        if(result.Unsupported)generation=null;SendState();
    }
}
