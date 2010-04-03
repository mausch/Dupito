namespace Dupito

open Castle.ActiveRecord

[<ActiveRecord>]
type FileHash() = 

    let mutable id : int = 0
    let mutable filePath : string = null
    let mutable hash : string = null

    [<PrimaryKey>]
    member this.Id
        with get () = id
        and set v = id <- v

    [<Property>]
    member this.FilePath
        with get () = filePath
        and set v = filePath <- v

    [<Property>]
    member this.Hash
        with get () = hash
        and set v = hash <- v