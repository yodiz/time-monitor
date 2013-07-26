//module Raven
//
//open Model
//open System.Linq 
//
//
//
//type RavenActivityService(store:Raven.Client.DocumentStoreBase) = 
//    interface IActivityService with
//        member x.Save a = 
//            use session = store.OpenSession()
//            session.Store(a)
//            session.SaveChanges()
//
//        member x.Load fromTime toTime = 
//            use session = store.OpenSession()
//            let results = session.Query<IdentifyableActivity>().Where(new System.Func<IdentifyableActivity,bool>(fun a -> a.Activity.From >= fromTime && a.Activity.To <= toTime))
//            let count = results.Count()
//            printfn "Temp Count: %A" count
//            let pageSize = 100
//            [0..count/pageSize]
//            |> List.map (fun page -> results.Skip(pageSize*page).Take(pageSize) |> Seq.toList)
//            |> Seq.collect id
//
//        member x.GetBounds () = 
//            let a = (x :> IActivityService).Load System.DateTime.MinValue System.DateTime.MaxValue 
//            if a |> Seq.isEmpty then
//                { 
//                    Earliest = System.DateTime.MinValue
//                    Latest = System.DateTime.MinValue
//                }
//            else
//                { 
//                    Earliest = a |> Seq.map (fun a -> a.Activity.From) |> Seq.min
//                    Latest = a |> Seq.map (fun a -> a.Activity.From) |> Seq.max 
//                }
////            use session = store.OpenSession()
////            let earliest = session.Query<IdentifyableActivity>().Max(new System.Func<_,_>(fun a -> a.Activity.From))
////            let latest = session.Query<IdentifyableActivity>().Max(new System.Func<_,_>(fun a -> a.Activity.To))
//
//        