module Monitor

open System.Diagnostics;
open System.Runtime.InteropServices;
open Model

type Setting = {
  ///How long a user can be idle and still considered working
  IdleLimitMs : int
  //How long to sleep before checking for work or idle
  SleepMs : int
  ReportIntervalMs : int
}

module private Impl = 
  [<Struct>]
  type LASTINPUTINFO = 
      val mutable cbSize : System.UInt32
      val mutable dwTime : System.UInt32

  module API = 
    //This Function is used to get Handle for Active Window...
    [<System.Runtime.InteropServices.DllImport("user32.dll", CharSet=System.Runtime.InteropServices.CharSet.Auto)>]
    extern System.IntPtr GetForegroundWindow();
  
    [<System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Auto)>]
    extern int GetWindowText(System.IntPtr hwnd, string lpString, int cch);

    //This Function is used to get Active process ID...
    [<System.Runtime.InteropServices.DllImport("user32.dll", CharSet=System.Runtime.InteropServices.CharSet.Auto)>]
    extern System.UInt32 GetWindowThreadProcessId(System.IntPtr hWnd, System.Int32& lpdwProcessId);

    [<System.Runtime.InteropServices.DllImport("user32.dll")>]
    extern bool GetLastInputInfo(LASTINPUTINFO& plii);

  open Model

  let getCurrentApplication() = 
    let fg = API.GetForegroundWindow()
    if fg = System.IntPtr.Zero then
      Application.Empty 
    else
      let q = new System.String(' ', 100)      
      let textLength = API.GetWindowText(fg, q, 100)
      let applicationText = q.Substring(0, textLength)
      let mutable processId = System.Int32()
      let threadId = API.GetWindowThreadProcessId(fg, &processId)
      let proc = Process.GetProcessById(processId)
      {
        Application.Name = proc.ProcessName
        Title = applicationText
      }

  let getLastInput() =  
    let mutable input = LASTINPUTINFO()
    input.cbSize <- uint32 (System.Runtime.InteropServices.Marshal.SizeOf(input));  
    let a = API.GetLastInputInfo(&input)
    int input.dwTime 

  type Input = {
  //  CurrentApplication : Application
    CurrentMs : int
    LastRunMs : int
    LastInputMs : int
    PreviousIdleTimeMs : int
    IdleLimitMs : int
  }

  type Output = 
    |Worked of int
    |Idle of int
    |IdleLimitReached


  let figureOutIfWorkedOrIdle (input:Input) = 
    let idleTimeMs = input.CurrentMs - input.LastInputMs
    let timeSinceLastRunMs = input.CurrentMs - input.LastRunMs

    let isWorked = idleTimeMs < timeSinceLastRunMs
    if isWorked then
      let workedTimeMs = timeSinceLastRunMs + input.PreviousIdleTimeMs 
      Output.Worked workedTimeMs
    else
      if input.PreviousIdleTimeMs  + timeSinceLastRunMs > input.IdleLimitMs then
        Output.IdleLimitReached 
      else
      Output.Idle (timeSinceLastRunMs)


  type S = {
    LastRunTickCount : int
    IdleTimeMs : int
    Worked : Map<Application, int>
    LastReport : System.DateTime 
  }

  let processer setting (event:Event<Activity>) s = 
    System.Threading.Thread.Sleep(setting.SleepMs)
    let tickCount = System.Environment.TickCount 
    let application = getCurrentApplication()
    let result =
      figureOutIfWorkedOrIdle
        {
          IdleLimitMs = setting.IdleLimitMs
          CurrentMs = tickCount
          LastRunMs = s.LastRunTickCount 
          LastInputMs = getLastInput()
          PreviousIdleTimeMs = s.IdleTimeMs
        }

    match result with 
    |Output.IdleLimitReached -> 
      printfn "%A" result
      setting,{ LastRunTickCount = tickCount; IdleTimeMs = 0; Worked = s.Worked; LastReport = s.LastReport }
    |Output.Idle idleTime -> 
      setting,{ LastRunTickCount = tickCount; IdleTimeMs = s.IdleTimeMs + idleTime; Worked = s.Worked ; LastReport = s.LastReport }
    |Output.Worked w -> 
      let worked = 
        Map.add 
          application 
          (match Map.tryFind application s.Worked with |Some k -> k+w |None -> w)
          s.Worked 

      let sum = worked |> Map.toSeq |> Seq.map snd |> Seq.sum
      let (worked, lastReport) = 
        if sum >= setting.ReportIntervalMs then 
          let now = System.DateTime.Now 
          worked 
          |> Map.toSeq
          |> Seq.iter 
            (fun (k,v) -> 
              event.Trigger { Activity.Application = k; From = s.LastReport; To = now; Duration = System.TimeSpan.FromMilliseconds(float v)  }
            ) 
          Map.empty, now
        else
          worked, s.LastReport 
      setting,{ LastRunTickCount = tickCount; IdleTimeMs = 0; Worked = worked; LastReport = lastReport }  



  type BackgroundExecutor<'a>(executionFunction : 'a -> 'a) = 
    let mutable isRunning = false
    let lockObj = System.Object()
    let completed = Event<_>()

    let rec execute s =
      if not isRunning then s
      else execute (lock lockObj (fun () -> executionFunction s))

    member x.Start initState = 
      if 
        lock lockObj (fun () -> if not isRunning then isRunning <- true; true else false)
      then
        async { 
          let finalResult = execute initState
          completed.Trigger finalResult
          return ()
        }
        |> Async.Start 
        true
      else
        false
    member x.Stop() = 
      lock lockObj (fun () -> if isRunning then isRunning <- false; true else false)
    member x.IsRunning = isRunning 
    member x.Completed = completed.Publish 


open Impl

type DefaultActivityReporter() as this =
  let event = Event<_>()
 
  let defaultSetting = { 
    Setting.IdleLimitMs = int (System.TimeSpan(0, 1, 0).TotalMilliseconds)
    SleepMs = 1000
    ReportIntervalMs = int (System.TimeSpan(0, 0, 5).TotalMilliseconds)
  } 
  
  let backgroundExecutor = BackgroundExecutor(fun (_,s) -> processer this.Setting event s )

  member val Setting = defaultSetting with get, set

  interface IActivityReporter with
    member x.Start () = 
      backgroundExecutor.Start 
        (this.Setting,{ LastRunTickCount = System.Environment.TickCount; IdleTimeMs = 0; Worked = Map.empty; LastReport = System.DateTime.Now  })
    member x.Stop () = 
      backgroundExecutor.Stop()
    member x.ActivityUpdate = event.Publish 
