namespace InputFlowStudio;
public sealed class RecordingPreparation {
 readonly Func<TimeSpan,CancellationToken,Task> delay;
 CancellationTokenSource? source;
 public RecordingPreparation(Func<TimeSpan,CancellationToken,Task>? delay=null){this.delay=delay??((span,token)=>Task.Delay(span,token));}
 public bool IsPending=>source!=null;
 public void Cancel()=>source?.Cancel();
 public async Task<bool> Run(Action<int> update,int seconds=3){if(seconds<0||seconds>10)throw new ArgumentOutOfRangeException(nameof(seconds));if(source!=null)return false;var owner=new CancellationTokenSource();source=owner;try{for(int i=seconds;i>=1;i--){update(i);await delay(TimeSpan.FromSeconds(1),owner.Token);}owner.Token.ThrowIfCancellationRequested();return true;}catch(OperationCanceledException)when(owner.IsCancellationRequested){return false;}finally{source=null;owner.Dispose();}}
}
