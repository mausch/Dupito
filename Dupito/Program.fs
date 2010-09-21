﻿module Program

open Dupito
open System
open System.Configuration
open System.Data.SqlServerCe
open System.IO
open System.Security.Cryptography
open Castle.ActiveRecord
open Castle.ActiveRecord.Framework
open Castle.ActiveRecord.Framework.Config
open Castle.ActiveRecord.Queries
open NHibernate.Cfg
open NHibernate.Connection
open NHibernate.Dialect
open NHibernate.Driver
open NHibernate.ByteCode.Castle
open Microsoft.FSharp.Collections
open Microsoft.FSharp.Collections

let setupAR () = 
    let dsfLocation () =
        let dsfc = ConfigurationManager.AppSettings.["DsfLocation"]
        if dsfc <> null 
            then dsfc
            else Path.GetDirectoryName AppDomain.CurrentDomain.BaseDirectory
    let config = InPlaceConfigurationSource()
    let dbFilename = Path.Combine (dsfLocation(), "dupito.dsf")
    let connectionString = sprintf "Data Source=%A;" dbFilename
    let parameters = dict [
                        Environment.ConnectionProvider, typeof<DriverConnectionProvider>.FullName
                        Environment.ConnectionDriver, typeof<SqlServerCeDriver>.FullName
                        Environment.Dialect, typeof<MsSqlCeDialect>.FullName
                        Environment.ConnectionString, connectionString
                        Environment.ProxyFactoryFactoryClass, typeof<ProxyFactoryFactory>.AssemblyQualifiedName
    ]
    config.Add (typeof<ActiveRecordBase>, parameters)
    ActiveRecordStarter.Initialize (config, [| typeof<FileHash> |])
    if not (File.Exists dbFilename) then
        use engine = new SqlCeEngine(connectionString)
        engine.CreateDatabase ()
        ActiveRecordStarter.CreateSchema ()
        use conn = new SqlCeConnection(connectionString)
        conn.Open()
        for key in ["filepath"; "hash"] do
            use cmd = new SqlCeCommand()
            cmd.Connection <- conn
            cmd.CommandText <- sprintf "create index IX_%s on filehash(%s)" key key
            cmd.ExecuteNonQuery () |> ignore
    printfn "AR initialized"
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

let hashAsync bufferSize hashFunction (stream: Stream) =
    let rec hashBlock currentBlock count (s: Stream) (hash: HashAlgorithm) = async {
        let buffer = Array.zeroCreate<byte> bufferSize
        let! readCount = s.AsyncRead buffer
        if readCount = 0 then
            hash.TransformFinalBlock(currentBlock, 0, count) |> ignore
        else 
            hash.TransformBlock(currentBlock, 0, count, currentBlock, 0) |> ignore
            return! hashBlock buffer readCount s hash
    }
    async {
        let buffer = Array.zeroCreate<byte> bufferSize
        let! readCount = stream.AsyncRead buffer
        do! hashBlock buffer readCount stream hashFunction
        return hashFunction.Hash |> Convert.ToBase64String
    }

let hashFileAsync f =    
    let bufferSize = 32768
    async {
        use! fs = File.AsyncOpenRead f
        use hashFunction = new SHA512Managed()
        hashFunction.Initialize()
        return! hashAsync bufferSize hashFunction fs
    }
    
let indexFile (save : FileHash -> unit) f = 
    printfn "Indexing file %A" f
    let hash = hashFile f
    FileHash(Hash = hash, FilePath = f) |> save

let add (fileHashEnumerate : unit -> FileHash seq) (fileHashSave : FileHash -> unit) =
    let allFiles = Directory.EnumerateFiles(Directory.GetCurrentDirectory(), "*.*", SearchOption.AllDirectories)
    let filesInDb = fileHashEnumerate() |> Seq.map (fun h -> h.FilePath) |> Seq.cache
    let filesToInsert = allFiles |> Seq.except filesInDb
    filesToInsert |> PSeq.iter (indexFile fileHashSave)
    0

let cleanup (fileHashEnumerate : unit -> FileHash seq) delete =
    fileHashEnumerate () 
    |> Seq.filter (fun f -> File.Exists f.FilePath)
    |> Seq.iter delete
    0

let compare (x: #IComparable<_>) y = 
    x.CompareTo y 

let comparePairs (x1,y1) (x2,y2) = 
    (x1 = x2 && y1 = y2) || (x1 = y2 && y1 = x2)

let applyToPair f (x,y) =
    (f x, f y)

let getDupes () =
    let getHash (h: FileHash) = h.Hash
    let getHashes = applyToPair getHash
    let q = SimpleQuery<obj[]>(typeof<FileHash>, QueryLanguage.Sql, "select {a.*}, {a2.*} from filehash a join filehash a2 on a.hash = a2.hash where a.id <> a2.id")
    q.AddSqlReturnDefinition(typeof<FileHash>, "a")
    q.AddSqlReturnDefinition(typeof<FileHash>, "a2")
    q.Execute()
    |> Seq.map (fun p -> (p.[0] :?> FileHash, p.[1] :?> FileHash))
    |> TSet.ofSeq (fun (x,y) -> if comparePairs (getHashes x) (getHashes y) then 0 else 1)

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

let openStatelessSession () = 
    ActiveRecordMediator.GetSessionFactoryHolder().GetSessionFactory(typeof<obj>).OpenStatelessSession ()

let findAll () =
    ActiveRecordMediator<FileHash>.FindAll()

let save f =
    ActiveRecordMediator<FileHash>.Create f

let delete f =
    ActiveRecordMediator<FileHash>.Delete f

let arrayAsSeq<'a> (f : _ -> 'a[]) = 
    f >> (fun r -> r :> seq<'a>)


[<EntryPoint>]
let main args = 
    if args.Length = 0
        then help()
        else match args.[0] with
             | "a" -> setupAR();add (arrayAsSeq findAll) save
             | "c" -> setupAR();cleanup (arrayAsSeq findAll) delete
             | "l" -> setupAR();printList ()
             | "r" -> setupAR();rehash ()
             | "d" -> setupAR();deleteInteractively ()
             | "dd" -> setupAR();deleteWithoutAsking ()
             | _ -> help ()