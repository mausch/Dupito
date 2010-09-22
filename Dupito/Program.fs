module Program

open System
open System.Configuration
open System.Data
open System.Data.SqlServerCe
open System.IO
open System.Security.Cryptography
open Microsoft.FSharp.Collections

let dsfLocation () =
    let dsfc = ConfigurationManager.AppSettings.["DsfLocation"]
    if dsfc <> null 
        then dsfc
        else Path.GetDirectoryName AppDomain.CurrentDomain.BaseDirectory

let dbFilename = Path.Combine (dsfLocation(), "dupito.dsf")
let connectionString = sprintf "Data Source=%A;" dbFilename

let createConn() = 
    let conn = new SqlCeConnection(connectionString)
    conn.Open()
    conn :> IDbConnection

let cmgr = Sql.withNewConnection createConn

let createDB() =
    let exec sql = Sql.execNonQuery cmgr sql [] |> ignore
    use engine = new SqlCeEngine(connectionString)
    engine.CreateDatabase ()
    exec "create table filehash (filepath varchar(1000), hash varchar(16))"
    for key in ["filepath"; "hash"] do
        let sql = sprintf "create index IX_%s on filehash(%s)" key key
        exec sql

let setupDB () = 
    if not (File.Exists dbFilename) then
        createDB()
    ()

let help () =
    printfn "a: add files in this directory and subdirectories to database"
    printfn "c: cleanup database"
    printfn "l: list dupes"
    printfn "r: rehash files in database"
    printfn "d: deletes duplicate files interactively"
    printfn "dd: deletes duplicate files automatically"
    0

let hashFile f = 
    use fs = new FileStream(f, FileMode.Open)
    use hashFunction = new SHA512Managed()
    hashFunction.ComputeHash fs |> Convert.ToBase64String

let hashAsync bufferSize (hashFunction: HashAlgorithm) (stream: Stream) progressReport =
    let rec hashBlock currentBlock count = async {
        progressReport stream.Position
        let buffer = Array.zeroCreate<byte> bufferSize
        let! readCount = stream.AsyncRead buffer
        if readCount = 0 then
            hashFunction.TransformFinalBlock(currentBlock, 0, count) |> ignore
        else 
            hashFunction.TransformBlock(currentBlock, 0, count, currentBlock, 0) |> ignore
            return! hashBlock buffer readCount
    }
    async {
        let buffer = Array.zeroCreate<byte> bufferSize
        let! readCount = stream.AsyncRead buffer
        do! hashBlock buffer readCount
        return hashFunction.Hash |> Convert.ToBase64String
    }

let consolelock = obj()
let lprintfn t = 
    let flush (a: string) = 
        System.Console.WriteLine(a)
        //printfn "%s" a
        System.Console.Out.Flush()
    lock consolelock (fun () -> Printf.kprintf flush t)

let hashFileAsync f =
    let bufferSize = 32768
    async {
        use! fs = File.AsyncOpenRead f
        use hashFunction = new SHA512Managed()
        let total = fs.Length
        let report (pos: int64) = lprintfn "%s: %s" f (((double pos)/(double total)).ToString("P"))
        return! hashAsync bufferSize hashFunction fs ignore
    }

[<CustomComparison>]
[<StructuralEquality>]
type FileHash = {
    Hash: string
    FilePath: string
} with
    interface IComparable with
        member x.CompareTo y = 
            compare x.Hash (y :?> FileHash).Hash
        
    
let indexFile (save : FileHash -> unit) f = 
    printfn "Indexing file %A" f
    try
        let hash = hashFile f
        save {Hash = hash; FilePath = f}
    with e -> ()

let indexFileAsync (save: FileHash -> unit) f =
    async {
        lprintfn "Indexing file %A" f
        try
            let! hash = hashFileAsync f
            save {Hash = hash; FilePath = f}
        with e -> ()
    }


let add (fileHashEnumerate : unit -> FileHash seq) (fileHashSave : FileHash -> unit) =
    let allFiles = Directory.EnumerateFiles(Directory.GetCurrentDirectory(), "*.*", SearchOption.AllDirectories)
    let filesInDb = fileHashEnumerate() |> Seq.map (fun h -> h.FilePath) |> Seq.cache
    let filesToInsert = allFiles |> Seq.except filesInDb
    filesToInsert |> Seq.map (indexFileAsync fileHashSave)
    |> Async.Parallel 
    |> Async.RunSynchronously
    |> ignore
    0

let cleanup (fileHashEnumerate : unit -> FileHash seq) delete =
    fileHashEnumerate () 
    |> Seq.filter (fun f -> File.Exists f.FilePath)
    |> Seq.iter delete
    0

let asFileHash = Sql.asRecord<FileHash>

let getDupes () =
    let fields = Sql.recordFieldsAlias typeof<FileHash>
    let sql = sprintf "select %s,%s from filehash a join filehash b on a.hash = b.hash where a.filepath <> b.filepath" (fields "a") (fields "b")
    Sql.execReader cmgr sql []
    |> Sql.map (fun r -> asFileHash "a" r, asFileHash "b" r)
    |> Set.ofSeq

let printList () =
    getDupes()
    |> Seq.iter (fun (f1,f2) -> printfn "%A %A" f1.FilePath f2.FilePath)
    0

let rehash () =
    failwith "not implemented"
    0

let deleteInteractively () =
    failwith "not implemented"
    0

let deleteWithoutAsking () =
    failwith "not implemented"
    0

let findAll () =
    Sql.execReader cmgr "select * from filehash" [] |> Sql.map (asFileHash "")

let save (f: FileHash) =
    Sql.execNonQuery cmgr "insert into filehash (hash, filepath) values (@h, @p)"
        (Sql.parameters ["@h",box f.Hash;"@p",box f.FilePath]) |> ignore

let delete (f: FileHash) = 
    Sql.execNonQueryF cmgr "delete from filehash where filepath = %s" f.FilePath |> ignore

[<EntryPoint>]
let main args = 
    if args.Length = 0
        then help()
        else match args.[0] with
             | "a" -> setupDB();add findAll save
             | "c" -> setupDB();cleanup findAll delete
             | "l" -> setupDB();printList ()
             | "r" -> setupDB();rehash ()
             | "d" -> setupDB();deleteInteractively ()
             | "dd" -> setupDB();deleteWithoutAsking ()
             | _ -> help ()