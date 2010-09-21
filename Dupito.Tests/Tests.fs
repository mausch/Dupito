module Dupito.Tests

open System
open System.IO
open NUnit.Framework

[<Test>]
let hasElemTest() =
    [1;2;3] |> Seq.hasElem 1 
    |> Assert.IsTrue

[<Test>]
let notHasElemTest() = 
    [1;2;3] |> Seq.hasElem 5
    |> Assert.IsFalse    

[<Test>]
let notHasElemTest2() =
    let notHasElem e = (Seq.hasElem e) >> not
    [1;2;3] |> notHasElem 5
    |> Assert.IsTrue

[<Test>]
let notHasElemTest3() =
    let notHasElem e = (Seq.hasElem e) >> not
    [1;2;3] |> notHasElem 3
    |> Assert.IsFalse

[<Test>]
let exceptTest() = 
    let a = [1;2;3]
    let b = [3]
    let r = a |> Seq.except b |> Seq.toList
    Assert.AreEqual([1;2], r)

[<Test>]
let fileHashing() = 
    let file = "Castle.ActiveRecord.xml"
    let h1 = Program.hashFile file
    let h2 = Program.hashFileAsync file |> Async.RunSynchronously
    Assert.AreEqual(h1, h2)