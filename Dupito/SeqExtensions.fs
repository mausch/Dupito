module Seq

open System
open System.Collections.Generic

let iterf functions x = Seq.iter ((|>) x) functions

let contains e = Seq.exists ((=) e)

let notContains e = (contains e) >> not

let except s2 s1 = 
    s1 |> Seq.filter (fun i -> s2 |> notContains i)

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
