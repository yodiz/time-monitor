let createNotifyIcon icon (window:System.Windows.Window) = 
    let notifyIcon = new System.Windows.Forms.NotifyIcon();
    notifyIcon.Icon <- icon

    notifyIcon.DoubleClick 
    |> Event.add 
        (fun _ -> 
            if window.WindowState = System.Windows.WindowState.Minimized then
                window.WindowState <- System.Windows.WindowState.Normal;
            window.Show();        
        )

    window.Loaded |> Event.add (fun _ -> notifyIcon.Visible <- true;)
    window.Closing |> Event.add (fun _ -> notifyIcon.Visible <- false;)

    window.ShowInTaskbar <- true
    window.Topmost <- true

    window.StateChanged 
    |> Event.add 
        (fun _ -> 
            if (window.WindowState = System.Windows.WindowState.Minimized) then
                window.Topmost <- false;
                window.ShowInTaskbar <- false;
            else
                window.ShowInTaskbar <- true;
                window.Topmost <- true;
        )

    notifyIcon

let createCommand action =
    let event1 = Event<_, _>()
    {
        new System.Windows.Input.ICommand with
            member this.CanExecute(obj) = true
            member this.Execute(obj) = action(obj)
            member this.add_CanExecuteChanged(handler) = event1.Publish.AddHandler(handler)
            member this.remove_CanExecuteChanged(handler) = event1.Publish.RemoveHandler(handler)
    }
    
type BindableBase() = 
    let propertyChanged = new Event<_,_>()
    interface System.ComponentModel.INotifyPropertyChanged with
        [<CLIEvent>] member x.PropertyChanged = propertyChanged.Publish 
    member x.Changed(property) = 
        propertyChanged.Trigger(x, new System.ComponentModel.PropertyChangedEventArgs(property))

type MyItem = {
    mutable Id : string
    Name : string
}

open System.Linq 

type myModel(store:Raven.Client.DocumentStoreBase) as this =
    inherit BindableBase() 

    let mutable stop = false

    let queryCommand = createCommand (fun _ -> this.Query())
        
    let insert x = 
        use session = store.OpenSession()
        session.Store({ Id = null; Name = "Micke" })
        session.SaveChanges()

    let test (a:myModel) = 
        async {
            seq {
                while not stop do
                    yield ()
                    System.Threading.Thread.Sleep(1)
            }
            |> Seq.iteri (fun i _ -> a.Changed("Now"); insert i)
            return ()        
        }      
        |> Async.Start 
        
    member x.Terminate() = stop <- true

    member x.Start() = test x

    member x.Now with get() = System.DateTime.Now

    member x.QueryCommand = queryCommand

    member x.Query() = 
        use session = store.OpenSession()
        let results = session.Query<MyItem>()
        printfn "Querying %A" (results.Count())
//        results |> Seq.iter (printfn "%A")
//        results        

        ()
[<EntryPoint>]
[<System.STAThreadAttribute>]
let main args = 
    let app = System.Windows.Application.LoadComponent(System.Uri("App.xaml", System.UriKind.Relative)) :?> System.Windows.Application

    let store = new Raven.Client.Embedded.EmbeddableDocumentStore()
    store.DataDirectory <- System.IO.Path.Combine(System.Environment.CurrentDirectory, "RavenData")
    store.Initialize() |> ignore

    let myModel = myModel(store)
    myModel.Start() 
    let mainWindow = System.Windows.Application.LoadComponent(System.Uri("MainWindow.xaml", System.UriKind.Relative)) :?> System.Windows.Window
    mainWindow.DataContext <- myModel
    use icon = new System.Drawing.Icon("Icon1.ico")
    use notifyIcon = createNotifyIcon icon mainWindow 

    let _ = app.Run(mainWindow);
    myModel.Terminate()
    0