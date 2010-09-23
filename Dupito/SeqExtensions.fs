module Seq

open System.Collections.Generic

let iterf (functions: seq<'a -> unit>) = 
    fun x -> functions |> Seq.iter (fun f -> f x)

let hasElem e s = 
    s |> Seq.exists (fun i -> i = e)

let except s2 s1 = 
    let notHasElem e = (hasElem e) >> not
    s1 |> Seq.filter (fun i -> s2 |> notHasElem i)

let distinctWith (pred: 'a -> 'a -> bool) (s: 'a seq) = 
    seq {
        let comparer = { new IEqualityComparer<'a> with
                            member a.Equals(x,y) = pred x y 
                            member a.GetHashCode x = hash x }
        let dict = new Dictionary<'a,obj>(comparer)
        for v in s do
            if not (dict.ContainsKey(v)) then 
                dict.[v] <- null; 
                yield v }
