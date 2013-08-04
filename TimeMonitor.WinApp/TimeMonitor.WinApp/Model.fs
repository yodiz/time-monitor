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
  Machine : string 
}
  with
    static member CombineList delta (inputActivities:Activity list) = 
      inputActivities
      |> List.sortBy (fun a -> a.From)
      |> List.fold 
        (fun s activity -> 
          let (found, activitiesToReturn) = 
            s 
            |> List.fold 
              (fun (found, l) i -> 
                if not found then
                  match Activity.TryCombine delta i activity with
                  |Some newActivity -> true, (newActivity :: l)
                  |None -> found, (i :: l)
                else
                  found, (i :: l)
              )
              (false, List.empty)  
          if not found then
            activity :: activitiesToReturn
          else
            activitiesToReturn
        ) 
        List.empty 
      

    static member TryCombine delta (a:Activity) (b:Activity) : Activity option  = 
      let diff = 
        min (abs ((a.From - b.To).TotalMilliseconds)) (abs ((b.From - a.To).TotalMilliseconds))
        |> System.TimeSpan.FromMilliseconds 
      if a.Application = b.Application && a.Machine = b.Machine && diff < delta then
        Some { From = min a.From b.From; To = max a.To b.To; Duration = a.Duration + b.Duration; Application = a.Application; Machine = a.Machine }
      else 
        None
//    static member Group delta groupBy (a:Activity seq) = 
//      a
//      |> Seq.groupBy (fun x -> x.Application, x.Machine, groupBy x)
//      |> Seq.map (fun ((_,_,k),v) -> k,v |> Seq.reduce (Activity.Combine delta))


type IdentifyableActivity = {
    mutable Id : System.Guid
    Activity : Activity
}
  with
    static member New a = 
      {
        Id = System.Guid.NewGuid()
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
    