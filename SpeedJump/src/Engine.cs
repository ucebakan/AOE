namespace SpeedJump;

sealed class PinLoop : IDisposable
{
    CancellationTokenSource? cancellation;
    Task? task;
    public Exception? Failure {get;private set;}
    public bool Active=>task is {IsCompleted:false};
    public void Start(Action write)
    {
        if(task is not null)throw new InvalidOperationException("Writer already started.");
        write(); // Initial failure must not paint a feature active.
        Failure=null;cancellation=new();var token=cancellation.Token;
        task=Task.Run(async()=>
        {
            try{using var timer=new PeriodicTimer(TimeSpan.FromMilliseconds(25));while(await timer.WaitForNextTickAsync(token))write();}
            catch(OperationCanceledException)when(token.IsCancellationRequested){}
            catch(Exception ex){Failure=ex;}
        });
    }
    public void Dispose()
    {
        cancellation?.Cancel();task?.GetAwaiter().GetResult();task=null;cancellation?.Dispose();cancellation=null;Failure=null;
    }
}

record View(bool Ready,bool Speed,bool Jump,string Message);
sealed class Engine : IDisposable
{
    readonly object gate=new();
    readonly Func<ITarget> connect;
    ITarget? target;
    readonly PinLoop speed=new(),jump=new();
    string message="4Unity bekleniyor…";
    bool faulted;
    public Engine(Func<ITarget>? factory=null){connect=factory??(()=>Session.Connect());}
    public View View {get{lock(gate)return new(target is not null&&!faulted,speed.Active,jump.Active,message);}}
    public void Poll()
    {
        lock(gate)
        {
            try
            {
                if(faulted){StopAll();target?.Dispose();target=null;faulted=false;return;}
                if(target is null){target=connect();message="Bağlı · Hazır";return;}
                if(speed.Failure is not null)throw new IOException("Speed writer: "+speed.Failure.Message);
                if(jump.Failure is not null)throw new IOException("Jump writer: "+jump.Failure.Message);
                target.Health();message="Bağlı · "+(speed.Active||jump.Active?"Etkin":"Hazır");
            }
            catch(Exception ex){Fault(ex);}
        }
    }
    public void Toggle(bool isJump)
    {
        lock(gate)
        {
            if(target is null||faulted)return;
            try
            {
                target.Health();
                if(isJump)
                {
                    if(jump.Active){jump.Dispose();target.RestorePatch();Files.Log("Jump OFF");}
                    else {target.EnablePatch();jump.Start(()=>target.Pin(true));Files.Log("Jump ON");}
                }
                else
                {
                    if(speed.Active){speed.Dispose();Files.Log("Speed OFF");}
                    else {speed.Start(()=>target.Pin(false));Files.Log("Speed ON");}
                }
                message="Bağlı · "+(speed.Active||jump.Active?"Etkin":"Hazır");
            }
            catch(Exception ex){Fault(ex);}
        }
    }
    void StopAll(){speed.Dispose();jump.Dispose();target?.RestorePatch();}
    void Fault(Exception ex)
    {
        faulted=true;message=ex.Message;Files.Log("Validation/failure: "+ex);
        try{StopAll();target?.Dispose();target=null;faulted=false;}
        catch(Exception cleanup){message+=" | Restore gerekli: "+cleanup.Message;Files.Log("Restore failed: "+cleanup);}
    }
    public void Dispose()
    {
        lock(gate){StopAll();target?.Dispose();target=null;Files.Log("Closed; writers stopped and owned patch restored.");}
    }
}
