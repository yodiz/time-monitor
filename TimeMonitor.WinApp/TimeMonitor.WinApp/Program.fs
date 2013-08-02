module Program

open Model

type myModel(store:IActivityService) as this =
    inherit Wpf.BindableBase() 

    let mutable stop = false

    let queryCommand = Wpf.createCommand (fun _ -> this.Query())
       
    member x.Terminate() = stop <- true

//    member x.Start() = test x

    member x.Now with get() = System.DateTime.Now

    member x.QueryCommand = queryCommand

    member x.Query() = 
//        let bounds = store.GetBounds()
//        printfn "%A" bounds
        let today = store.Load System.DateTime.Now.Date (System.DateTime.Now.Date.AddDays(1.0))
        printfn "%A" (today |> Seq.length)
//        today |> Seq.iter (printfn "%A")
//        results        

        ()
[<EntryPoint>]
[<System.STAThreadAttribute>]
let main args = 
    let app = System.Windows.Application.LoadComponent(System.Uri("App.xaml", System.UriKind.Relative)) :?> System.Windows.Application

    use db = Db4objects.Db4o.Db4oEmbedded.OpenFile(System.IO.Path.Combine(System.Environment.CurrentDirectory, "db4o.dat"))
    let activityService : IActivityService = upcast Db4o.Db4oActivityService(db)

    let activityReporter : IActivityReporter = upcast Monitor.DefaultActivityReporter()

    activityReporter.ActivityUpdate 
    |> Event.add 
      (fun r -> 
        activityService.Save (IdentifyableActivity.New r "MachineId")
      )

    let myModel = myModel(activityService)
    let mainWindow = System.Windows.Application.LoadComponent(System.Uri("MainWindow.xaml", System.UriKind.Relative)) :?> System.Windows.Window
    mainWindow.DataContext <- myModel
    use icon = new System.Drawing.Icon("Icon1.ico")
    use notifyIcon = Wpf.createNotifyIcon icon mainWindow 

    activityReporter.Start() |> ignore

    let _ = app.Run(mainWindow);
    activityReporter.Stop() |> ignore

    myModel.Terminate()
    0