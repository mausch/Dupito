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

[<EntryPoint>]
let main args = 
    setupAR ()
    0