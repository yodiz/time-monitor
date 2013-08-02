module Model


type Application = {
  Name : string
  Title : string
}
  with
  static member Empty = { Application.Name = ""; Title = "" }

type Activity = {
  From : System.DateTime 
  To : System.DateTime 
  Duration : System.TimeSpan
  Application : Application
}
//  with 
//    member x.To = x.From + x.Duration 


type IdentifyableActivity = {
    mutable Id : System.Guid
    Machine : string 
    Activity : Activity
}
  with
    static member New a m = 
      {
        Id = System.Guid.NewGuid()
        Machine = m 
        Activity = a
      }

type IActivityReporter = 
    abstract member Start : unit -> bool
    abstract member Stop : unit -> bool
    abstract member ActivityUpdate : IEvent<Activity>

type Bound = {
    Earliest : System.DateTime 
    Latest : System.DateTime 
}

type IActivityService = 
    abstract member Save : IdentifyableActivity -> unit
    abstract member Load : System.DateTime -> System.DateTime -> IdentifyableActivity seq
    abstract member GetBounds : unit -> Bound
    