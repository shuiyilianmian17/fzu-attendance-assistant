using System;using System.Threading;using System.Threading.Tasks;
namespace FzuAttendance {
static class Operation {
 static readonly AsyncLocal<CancellationToken> current=new AsyncLocal<CancellationToken>();
 public static CancellationTokenSource Start(){var source=new CancellationTokenSource();current.Value=source.Token;return source;}
 public static void Throw(){current.Value.ThrowIfCancellationRequested();}
 public static Task Delay(int milliseconds){return Task.Delay(milliseconds,current.Value);}
 public static IDisposable KillOnCancel(System.Diagnostics.Process process){return current.Value.Register(delegate{try{if(!process.HasExited)process.Kill();}catch{}});}
}
}
