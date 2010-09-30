module Dupito.Tests

open System
open System.IO
open NUnit.Framework

[<Test>]
let containsTest() =
    [1;2;3] |> Seq.contains 1 
    |> Assert.IsTrue

[<Test>]
let containsFalseTest() = 
    [1;2;3] |> Seq.contains 5
    |> Assert.IsFalse    

[<Test>]
let notHasElemTest() =
    [1;2;3] |> Seq.notContains 5
    |> Assert.IsTrue

[<Test>]
let notHasElemFalseTest() =
    [1;2;3] |> Seq.notContains 3
    |> Assert.IsFalse

[<Test>]
let exceptTest() = 
    let a = [1;2;3]
    let b = [3]
    let r = a |> Seq.except b |> Seq.toList
    Assert.AreEqual([1;2], r)

[<Test>]
let fileHashing() = 
    let file = "FsSql.xml"
    let h1 = Program.hashFile file
    let h2 = Program.hashFileAsync file |> Async.RunSynchronously
    Assert.AreEqual(h1, h2)