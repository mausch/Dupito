module Program

open Dupito
open System
open System.Configuration
open System.Data.SqlServerCe
open System.IO
open Castle.ActiveRecord
open Castle.ActiveRecord.Framework
open Castle.ActiveRecord.Framework.Config
open NHibernate.Cfg
open NHibernate.Connection
open NHibernate.Dialect
open NHibernate.Driver
open NHibernate.ByteCode.Castle

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

    ()

let help () =
    printfn "a: add files in this directory and subdirectories to database"
    printfn "c: cleanup database"
    printfn "l: list dupes"
    printfn "r: rehash files in database"
    printfn "d: deletes duplicate files interactively"
    printfn "dd: deletes duplicate files automatically"
    0

let add () =
    failwith "not implemented"
    0

let cleanup () =
    failwith "not implemented"
    0

let printList () =
    failwith "not implemented"
    0

let rehash () =
    failwith "not implemented"
    0

let delete () =
    failwith "not implemented"
    0

let deleteWithoutAsking () =
    failwith "not implemented"
    0

[<EntryPoint>]
let main args = 
    setupAR ()
    if args.Length = 0
        then help()
        else match args.[0] with
             | "a" -> add ()
             | "c" -> cleanup ()
             | "l" -> printList ()
             | "r" -> rehash ()
             | "d" -> delete ()
             | "dd" -> deleteWithoutAsking ()
             | _ -> help ()