module TSet

open System
open System.Collections.Generic
open Microsoft.FSharp.Collections

type private 'a GComparer(f) = 
    interface 'a IComparer with
        override x.Compare (a,b) =
            f(a,b)

let ofSeq comparer (s: 'a seq) =    
    Tagged.Set<'a, IComparer<'a>>.Create(GComparer(comparer), s)