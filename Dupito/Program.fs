module Program

open System
open System.Configuration
open System.Data
open System.IO
open System.Security.Cryptography
open FirebirdSql.Data.FirebirdClient
open Microsoft.FSharp.Collections

let dbLocation () =
    let dbfile = ConfigurationManager.AppSettings.["DbLocation"]
    if dbfile <> null 
        then dbfile
        else Path.GetDirectoryName AppDomain.CurrentDomain.BaseDirectory

let dbFilename = Path.Combine (dbLocation(), "dupito.db")
let connectionStringForFile = sprintf "Database=%s;ServerType=1;User=SYSDBA;Password=masterkey"
let connectionString = connectionStringForFile dbFilename

let createConn() = 
    let conn = new FbConnection(connectionString)
    conn.Open()
    conn :> IDbConnection

let cmgr = Sql.withNewConnection createConn

let createDB() =
    let exec sql = Sql.execNonQuery cmgr sql [] |> ignore
    FbConnection.CreateDatabase connectionString
    exec "create table filehash (filepath varchar(1000), hash varchar(100))"
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

type FileHash = {
    Hash: string
    FilePath: string
}        
    
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
        with e -> 
            lprintfn "Exception: %s\n%s" e.Message e.StackTrace
            ()
    }


let add (fileHashEnumerate : unit -> FileHash seq) (fileHashSave : FileHash -> unit) =
    let allFiles = Directory.EnumerateFiles(Directory.GetCurrentDirectory(), "*.*", SearchOption.AllDirectories)
    let filesInDb = fileHashEnumerate() |> Seq.map (fun h -> h.FilePath) |> Seq.toList
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

let comparePairs (x1,y1) (x2,y2) = 
    (x1 = x2 && y1 = y2) || (x1 = y2 && y1 = x2)

let applyToPair f (x,y) =
    (f x, f y)

let getHash (h: FileHash) = h.Hash
let getHashes = applyToPair getHash

let getDupes () =
    let fields = Sql.recordFieldsAlias typeof<FileHash>
    let sql = sprintf "select %s,%s from filehash a join filehash b on a.hash = b.hash where a.filepath <> b.filepath" (fields "a") (fields "b")
    Sql.execReader cmgr sql []
    |> Sql.map (fun r -> asFileHash "a" r, asFileHash "b" r)
    |> Seq.distinctWith (fun x y -> comparePairs (getHashes x) (getHashes y))

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

let printAll() =
    findAll()
    |> Seq.iter (fun h -> printfn "%s\t%s" h.FilePath h.Hash)
    0

[<EntryPoint>]
let main args = 
    if args.Length = 0
        then help()
        else match args.[0] with
             | "a" -> setupDB();add findAll save
             | "c" -> setupDB();cleanup findAll delete
             | "l" -> setupDB();printList()
             | "p" -> setupDB();printAll()
             | "r" -> setupDB();rehash()
             | "d" -> setupDB();deleteInteractively ()
             | "dd" -> setupDB();deleteWithoutAsking ()
             | _ -> help ()