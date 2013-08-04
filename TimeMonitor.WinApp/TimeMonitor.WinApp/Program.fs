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
        let today = store.Load System.DateTime.Now.Date (System.DateTime.Now.Date.AddDays(1.0))
        printfn "%A" (today |> Seq.length)
        let grouped = 
          today 
          |> Seq.map (fun a -> a.Activity)
//          |> Activity.Group (System.TimeSpan(0,15,0)) (fun a -> a.From.Date) 
          |> Seq.toList 
        printfn "%A" grouped.Length 
        ()

type GroupedActivity = {
  GroupDescription : System.DateTime
  Activities : Activity list
}

type ReportViewModel(store:IActivityService) as this = 
  inherit Wpf.BindableBase()

  let queryCommand = Wpf.createCommand (fun _ -> this.Query())
  let fetchCommand = Wpf.createCommand (fun _ -> this.Fetch())

  let fromDate =  Wpf.ObservableValue(System.DateTime.Today.AddDays(1.0 - float System.DateTime.Today.Day))
  let toDate   =  Wpf.ObservableValue(System.DateTime.Today.AddDays(1.0 - float System.DateTime.Today.Day).AddMonths(1).AddDays(-1.0).Date)
  let activities = Wpf.ObservableValue([]);
  let groupedActivities = Wpf.ObservableValue([]);
  

  member x.QueryCommand = queryCommand
  member x.FetchCommand = fetchCommand
  
  member x.FromDate = fromDate
  member x.ToDate = toDate
  member x.GroupedActivities = groupedActivities

  member x.Fetch() = 
    activities.Value <- store.Load fromDate.Value toDate.Value |> Seq.toList 
          
  
  member x.Query() = 
    async {  
      let grouped = 
        activities.Value
        |> Seq.map (fun a -> { a.Activity with Application = { a.Activity.Application with Title = ""; Name = "" }})
        |> Seq.groupBy (fun a -> a.From.Date)
        |> Seq.map 
          (fun (k,v) -> 
            { 
              GroupDescription = k
              Activities = 
                v 
                |> Seq.toList
                |> Activity.CombineList (System.TimeSpan(0,15,0))
                |> List.sortBy (fun a -> a.From)
//                |> Activity.Group  (fun a -> ()) |> Seq.map snd |> Seq.toList
            }
          )
        |> Seq.toList
        |> List.sortBy (fun a -> a.GroupDescription) 
      groupedActivities.Value <- grouped
  //    printfn "%A" grouped.Length 
    }
    |> Async.Start  
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
        activityService.Save (IdentifyableActivity.New r)
      )

    let myModel = myModel(activityService)
    let reportModel = ReportViewModel(activityService)

    let mainWindow = TimeMonitor.WinApp.Controls.MainWindow()
    mainWindow.DataContext <- myModel
    (mainWindow |> Wpf.byName "uxReport").DataContext <- reportModel

    use icon = new System.Drawing.Icon("Icon1.ico")
    use notifyIcon = Wpf.createNotifyIcon icon mainWindow 

    activityReporter.Start() |> ignore

    System.Windows.FrameworkElement.LanguageProperty.OverrideMetadata(
      typeof<System.Windows.FrameworkElement>, 
      new System.Windows.FrameworkPropertyMetadata(System.Windows.Markup.XmlLanguage.GetLanguage(System.Globalization.CultureInfo.CurrentCulture.IetfLanguageTag))
    )
//    FrameworkElement.LanguageProperty.OverrideMetadata(
//      typeof(FrameworkElement),
//      new FrameworkPropertyMetadata(
//          XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));

//
//   *
//   By Name
//   By Name & Title
//
//
//   From date - To date
//   Duration


    let _ = app.Run(mainWindow);
    activityReporter.Stop() |> ignore

    myModel.Terminate()
    0