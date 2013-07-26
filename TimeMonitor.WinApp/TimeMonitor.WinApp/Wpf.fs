module Wpf

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
