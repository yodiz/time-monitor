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


//open System.ComponentModel
open Microsoft.FSharp.Quotations.Patterns

type BindableBase() = 
    let propertyChanged = new Event<_,_>()
    let getPropertyName = 
      function 
        | PropertyGet(_,pi,_) -> pi.Name
        | _ -> invalidOp "Expecting property getter expression"
    interface System.ComponentModel.INotifyPropertyChanged with
        [<CLIEvent>] member x.PropertyChanged = propertyChanged.Publish 
    member x.Changed(property) = 
        propertyChanged.Trigger(x, new System.ComponentModel.PropertyChangedEventArgs(property))
    member x.Changed(qoutation) = 
        propertyChanged.Trigger(x, new System.ComponentModel.PropertyChangedEventArgs(getPropertyName qoutation))

let byName name (control:System.Windows.Controls.Control) = 
  control.FindName(name) :?> System.Windows.Controls.Control

[<AbstractClass>]
type ObservableBase<'a>() = 
  let propertyChanged = new Event<_,_>()
  let valueChanged = new Event<_>();
  member x.Changed(newValue:'a) = 
    propertyChanged.Trigger(x, new System.ComponentModel.PropertyChangedEventArgs("Value"))
    valueChanged.Trigger (newValue)
  interface System.ComponentModel.INotifyPropertyChanged with
      [<CLIEvent>] member x.PropertyChanged = propertyChanged.Publish 
  abstract Value : 'a with get, set
  member x.ValueChanged = valueChanged.Publish 

type ObservableValue<'a>(value:'a) = 
  inherit ObservableBase<'a>()
  let mutable value = value
  override x.Value with get () = value and set v = value <- v; x.Changed(v)
  
type ComputedValue<'a, 'b, 'c>(a:ObservableBase<'a>, b:ObservableBase<'b>, fn : 'a -> 'b -> 'c) as  this = 
  inherit ObservableBase<'c>()
  do
    a.ValueChanged |> Event.add (fun x -> this.Changed(this.Value))
    b.ValueChanged |> Event.add (fun x -> this.Changed(this.Value))
  override x.Value with get() = fn a.Value b.Value and set v = failwithf "Computed value is readonly"
