module Seq

let hasElem e s = 
    s |> Seq.exists (fun i -> i = e)

let except s1 s2 = 
    s1 |> Seq.filter (fun i -> s2 |> hasElem i)

