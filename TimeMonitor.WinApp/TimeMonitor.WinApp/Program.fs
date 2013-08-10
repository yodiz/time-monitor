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

[<NoComparison>][<NoEquality>]
type GroupRule = {
  Sorter : Activity list -> Activity list
  Reducer : Activity -> Activity -> Activity option
}

module GroupRules = 
  let combine a b = 
     { 
      From = min a.From b.From;
      To = max a.To b.To; Duration = a.Duration + b.Duration;
      Application = 
        { 
          Name = if a.Application.Name = b.Application.Name then a.Application.Name else "*"
          Title = if a.Application.Title = b.Application.Title then a.Application.Title else "*" 
        }
      Machine = if a.Machine = b.Machine then a.Machine else "*"
     }
  let nearByTime delta (a:Activity) (b:Activity) : Activity option=
    let diff = 
      min (abs ((a.From - b.To).TotalMilliseconds)) (abs ((b.From - a.To).TotalMilliseconds))
      |> System.TimeSpan.FromMilliseconds 
    if diff < delta then
      Some (combine a b)
    else 
      None     
  let sameDay = 
    { Sorter = List.sortBy (fun a -> a.From); Reducer = (fun a b -> if a.From.Date = b.From.Date then Some (combine a b) else None); }
  let withinNminutes n = 
    { Sorter = List.sortBy (fun a -> a.From) >> List.rev; Reducer = nearByTime (System.TimeSpan(0,n,0)); }
  let within15minutes = withinNminutes 15
  let applicationName = 
    { Sorter = List.sortBy (fun a -> a.Application.Name); Reducer = (fun a b -> if a.Application.Name = b.Application.Name then (Some (combine a b)) else None); }
  let application = 
    { Sorter = List.sortBy (fun a -> a.Application); Reducer = (fun a b -> if a.Application = b.Application then (Some (combine a b)) else None); }

  let reduceActivityList (activities:Activity list) (groupRule:GroupRule) = 
    //Lopa igenom activities
    //Kolla om det går att 'groupa'
    //Om det går, lägg till activity i listan och kör group funktionen
    //Om inte lägg till nuvarande listan i yttre listan och kör vidare
    let (lastAct,currentGroup,groups) = 
      activities 
      |> groupRule.Sorter 
      |> List.fold 
        (fun (lastActivity,currentGroup,groups) a -> 
          match lastActivity with
          |Some b -> 
            match groupRule.Reducer a b with
            |Some g -> 
              let currentGroup = a :: currentGroup
              Some g, currentGroup, groups
            |None -> 
              Some a, [], (b,currentGroup) :: groups
          |None -> Some a, currentGroup, groups
        )
        (None,[],[])
    let groups = match lastAct with Some b -> (b,currentGroup) :: groups | None -> groups
    groups
    
type GroupingHirarchy = {
  GroupRule : GroupRule
  ChildGroupRule : GroupingHirarchy option
  Description : Activity -> string
}

type ActivityGroupViewModel(displayActivity, activities : Activity list, groupingHirarchy : GroupingHirarchy) as this = 
    let mutable loaded = false
//    let groups = Wpf.ObservableValue([])
    let expanded = Wpf.ObservableValue(false)
    let calculateCommand = Wpf.createCommand (fun _ -> this.Calculate())
    let description = sprintf "%s" (groupingHirarchy.Description displayActivity)

    let children, isBottom = 
      match groupingHirarchy.ChildGroupRule with
      |Some childRule -> 
        let childs = 
          GroupRules.reduceActivityList activities groupingHirarchy.GroupRule 
          |> List.map 
            (fun (groupedActivity,childActs) -> 
              ActivityGroupViewModel(groupedActivity, childActs, childRule)
            )
        childs, false
      |None -> [], true
    let calculate () = 
//      groups.Value <- getChildren()
      ()

    do
      expanded.ValueChanged |> Event.add (fun v -> if v && not loaded then calculate())

    member x.Activity = displayActivity
    member x.Description = description
    member x.Calculate() = 
      calculate()
    member x.CalculateCommand = calculateCommand
    member x.Activities = activities
    member x.Groups = children
    member x.IsBottom = isBottom
    member x.Expanded = expanded

   

type ReportViewModel(store:IActivityService) as this = 
  inherit Wpf.BindableBase()

  let queryCommand = Wpf.createCommand (fun _ -> this.Query())
  let fetchCommand = Wpf.createCommand (fun _ -> this.Fetch())

  let fromDate =  Wpf.ObservableValue(System.DateTime.Today.AddDays(1.0 - float System.DateTime.Today.Day))
  let toDate   =  Wpf.ObservableValue(System.DateTime.Today.AddDays(1.0 - float System.DateTime.Today.Day).AddMonths(1).AddDays(-1.0).Date)
  let activities = Wpf.ObservableValue([]);
  let groupedActivities = Wpf.ObservableValue([]);
  let groups = Wpf.ObservableValue(None);
  

  member x.QueryCommand = queryCommand
  member x.FetchCommand = fetchCommand
  member x.Groups = groups
  
  member x.FromDate = fromDate
  member x.ToDate = toDate
  member x.GroupedActivities = groupedActivities

  member x.Fetch() = 
    activities.Value <- store.Load fromDate.Value toDate.Value |> Seq.toList 
          
  
  member x.Query() = 
    let groupingHirarchy = 
      {
        GroupRule = GroupRules.sameDay
        Description = (fun _ -> "Root")
        ChildGroupRule = Some 
          {
            GroupRule = GroupRules.within15minutes
            Description = (fun a -> sprintf "%A" (a.From.ToShortDateString()))
            ChildGroupRule = Some 
              {
                GroupRule = GroupRules.applicationName 
                Description = (fun a -> sprintf "%s - %s" (a.From.ToShortTimeString()) (a.To.ToShortTimeString()))
                ChildGroupRule = Some
                  {
                    Description = (fun a -> sprintf "%s" a.Application.Name)
                    GroupRule = GroupRules.application
                    ChildGroupRule = None
                  }
              }
          }
      }

    let test = 
      let q = activities.Value |> List.map (fun a -> a.Activity) |> List.reduce GroupRules.combine 
      ActivityGroupViewModel(q, activities.Value |> List.map (fun a -> a.Activity), groupingHirarchy)
    test.Calculate()
    groups.Value <- Some test
//    async {  
//      //Group by date
//      //Group by sub-date (time)
//      //Group by app
//      //Group by title
//
////      let grouped = 
////        activities.Value
////        |> Seq.map (fun a -> { a.Activity with Application = { a.Activity.Application with Title = ""; Name = "" }})
////        |> Seq.groupBy (fun a -> a.From.Date)
////        |> Seq.map 
////          (fun (k,v) -> 
////            { 
////              GroupDescription = k
////              Activities = 
////                v 
////                |> Seq.toList
////                |> Activity.CombineList (System.TimeSpan(0,15,0))
////                |> List.sortBy (fun a -> a.From)
//////                |> Activity.Group  (fun a -> ()) |> Seq.map snd |> Seq.toList
////            }
////          )
////        |> Seq.toList
////        |> List.sortBy (fun a -> a.GroupDescription) 
////      groupedActivities.Value <- grouped
//  //    printfn "%A" grouped.Length 
//    }
//    |> Async.Start  
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