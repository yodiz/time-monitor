module Program

open Model

type myModel(store:IActivityService) as this =
    inherit Wpf.BindableBase() 

    let mutable stop = false

    let queryCommand = Wpf.createCommand (fun _ -> this.Query())

    let test (a:myModel) = 
        async {
            seq {
                while not stop do
                    yield ()
                    System.Threading.Thread.Sleep(1)
            }
            |> Seq.iteri 
                (fun i _ -> 
                    store.Save(
                        {
                            Id = System.Guid.Empty 
                            Machine = "Test"
                            Activity = 
                            {
                                From = System.DateTime.Now 
                                Duration = System.TimeSpan(0,0,1)
                                Application = 
                                {
                                    Title = ""
                                    Name = ""
                                }                        
                            }
                        }
                    )
                    a.Changed("Now");
                )
            return ()        
        }      
        |> Async.Start 
        
    member x.Terminate() = stop <- true

    member x.Start() = test x

    member x.Now with get() = System.DateTime.Now

    member x.QueryCommand = queryCommand

    member x.Query() = 
        let bounds = store.GetBounds()
        printfn "%A" bounds
        let today = store.Load System.DateTime.Now.Date (System.DateTime.Now.Date.AddDays(1.0))
        printfn "%A" (today |> Seq.length)
//        today |> Seq.iter (printfn "%A")
//        results        

        ()
[<EntryPoint>]
[<System.STAThreadAttribute>]
let main args = 
    let app = System.Windows.Application.LoadComponent(System.Uri("App.xaml", System.UriKind.Relative)) :?> System.Windows.Application

    let store = new Raven.Client.Embedded.EmbeddableDocumentStore()
    store.DataDirectory <- System.IO.Path.Combine(System.Environment.CurrentDirectory, "RavenData")
    store.Initialize() |> ignore

    let activityService : IActivityService = upcast Raven.RavenActivityService(store)

    let myModel = myModel(activityService)
    myModel.Start() 
    let mainWindow = System.Windows.Application.LoadComponent(System.Uri("MainWindow.xaml", System.UriKind.Relative)) :?> System.Windows.Window
    mainWindow.DataContext <- myModel
    use icon = new System.Drawing.Icon("Icon1.ico")
    use notifyIcon = Wpf.createNotifyIcon icon mainWindow 

    let _ = app.Run(mainWindow);
    myModel.Terminate()
    0