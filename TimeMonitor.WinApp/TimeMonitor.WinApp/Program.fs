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
    
type BindableBase() = 
    let propertyChanged = new Event<_,_>()
    interface System.ComponentModel.INotifyPropertyChanged with
        [<CLIEvent>] member x.PropertyChanged = propertyChanged.Publish 
    member x.Changed(property) = 
        propertyChanged.Trigger(x, new System.ComponentModel.PropertyChangedEventArgs(property))

type MyItem = {
    Name : string
}

open System.Linq 

type myModel() =
    inherit BindableBase() 

    let mutable stop = false

    let test (a:myModel) = 
        async {
            seq {
                while not stop do
                    yield ()
                    System.Threading.Thread.Sleep(1000)
            }
            |> Seq.iter (fun _ -> a.Changed("Now"))
            return ()        
        }      
        |> Async.Start 
        
    member x.Terminate() = stop <- true

    member x.Start() = test x

    member x.Now with get() = System.DateTime.Now

[<EntryPoint>]
[<System.STAThreadAttribute>]
let main args = 
    let app = System.Windows.Application.LoadComponent(System.Uri("App.xaml", System.UriKind.Relative)) :?> System.Windows.Application
    let myModel = myModel()
    myModel.Start() 
    let mainWindow = System.Windows.Application.LoadComponent(System.Uri("MainWindow.xaml", System.UriKind.Relative)) :?> System.Windows.Window
    mainWindow.DataContext <- myModel
    use icon = new System.Drawing.Icon("Icon1.ico")
    use notifyIcon = createNotifyIcon icon mainWindow 

    let _ = app.Run(mainWindow);
    myModel.Terminate()
    0