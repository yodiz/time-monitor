module Db4o


open Model
open System.Linq 
open Model

type Db4oActivityService(db:Db4objects.Db4o.IEmbeddedObjectContainer) = 
  interface IActivityService with
    member x.Save a = 
      db.Store(a)

    member x.Load fromTime toTime = 
      let items = db.Query<IdentifyableActivity>().Where(System.Func<_,_>(fun a -> a.Activity.From >= fromTime && a.Activity.To <= toTime))
      items

    member x.GetBounds () = 
      let a = (x :> IActivityService).Load System.DateTime.MinValue System.DateTime.MaxValue 
      if a |> Seq.isEmpty then
          { 
              Earliest = System.DateTime.MinValue
              Latest = System.DateTime.MinValue
          }
      else
          { 
              Earliest = a |> Seq.map (fun a -> a.Activity.From) |> Seq.min
              Latest = a |> Seq.map (fun a -> a.Activity.From) |> Seq.max 
          }