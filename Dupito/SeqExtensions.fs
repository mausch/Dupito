module Seq

let hasElem e s = 
    s |> Seq.exists (fun i -> i = e)

let except s2 s1 = 
    let notHasElem e = (hasElem e) >> not
    s1 |> Seq.filter (fun i -> s2 |> notHasElem i)