module Model


type Application = {
    Name : string
    Title : string
}

type Activity = {
    From : System.DateTime 
    Duration : System.TimeSpan
    Application : Application
}
    with 
        member x.To = x.From + x.Duration 

type IdentifyableActivity = {
    mutable Id : System.Guid
    Machine : string 
    Activity : Activity
}

type IActivityReporter = 
    abstract member Start : unit -> unit
    abstract member Stop : unit -> unit
    abstract member ActivityUpdate : IEvent<Activity>

type Bound = {
    Earliest : System.DateTime 
    Latest : System.DateTime 
}

type IActivityService = 
    abstract member Save : IdentifyableActivity -> unit
    abstract member Load : System.DateTime -> System.DateTime -> IdentifyableActivity seq
    abstract member GetBounds : unit -> Bound
    