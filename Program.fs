module NvmFs.Main

open System
open FSharp.SystemCommandLine
open System.Threading.Tasks

let installCommand =
    let version = Input.Argument<string>("version", "", "Installs the specified node version")
    let lts = Input.Option<bool Nullable>(["-l"; "--lts"], Nullable(), "Ignores version and pulls down the latest LTS version")
    let current = Input.Option<bool Nullable>(["-c"; "--current"], Nullable(), "Ignores version and pulls down the latest Current version")
    let isDefault = Input.Option<bool>(["-d"; "--default"], false, "Sets the downloaded version as default (default: false)")

    command "install" {
        description "Installs the specified node version or the latest LTS by default"
        inputs (version, lts, current, isDefault)
        setHandler Handlers.installHandler
    }

let uninstallCommand = 
    let version = Input.Argument<string>("version", "", "Installs the specified node version")

    command "uninstall" {
        description "Uninstalls the specified node version"
        inputs version
        setHandler Handlers.uninstallHandler
    }

let useCommand =
    let version = Input.Argument<string>("version", "", "Installs the specified node version")
    let lts = Input.Option<bool Nullable>(["-l"; "--lts"], Nullable(), "Ignores version and pulls down the latest LTS version")
    let current = Input.Option<bool Nullable>(["-c"; "--current"], Nullable(), "Ignores version and pulls down the latest Current version")

    command "use" {
        description "Sets the Node Version"
        inputs (version, lts, current)
        setHandler Handlers.useHandler
    }

let listCommand = 
    let remote = Input.Option<bool Nullable>(["-r"; "--remote"], Nullable(), "Displays the last downloaded version index in the console")
    let updateIndex = Input.Option<bool Nullable>(["-u"; "--update"], Nullable(), "Use together with --remote, pulls the version index from the node website")

    command "list" {
        description "Shows the available node versions"
        inputs (remote, updateIndex)
        setHandler Handlers.listHandler
    }

[<EntryPoint>]
let main argv = 
    rootCommand argv {
        description "nvmfs"
        setHandler Task.FromResult
        addCommand installCommand
        addCommand uninstallCommand
        addCommand useCommand
        addCommand listCommand
    }
    |> Async.AwaitTask
    |> Async.RunSynchronously


//[<EntryPoint>]
//let main argv =

//    let result = Parser.Default.ParseArguments<Install, Use, List, Uninstall>(argv)

//    let result =
//        task {
//            match result with
//            | :? (Parsed<obj>) as cmd ->
//                match cmd.Value with
//                | :? Install as opts -> return! Actions.Install opts
//                | :? Use as opts -> return! Actions.Use opts
//                | :? Uninstall as opts -> return! Actions.Uninstall opts
//                | :? List as opts -> return! Actions.List opts
//                | _ -> return 1
//            | _ -> return 1
//        }
//        |> Async.AwaitTask
//        |> Async.Catch
//        |> Async.RunSynchronously

//    match result with
//    | Choice1Of2 result -> result
//    | Choice2Of2 ex ->
//        AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything)
//        1
