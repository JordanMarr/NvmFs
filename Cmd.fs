namespace NvmFs.Cmd

open System
open System.Threading.Tasks
open FSharp.Control.Tasks
open Spectre.Console
open CommandLine
open NvmFs


[<Verb("install", HelpText = "Installs the specified node version or the latest LTS by default")>]
type Install =
    { [<Option('n', "node", Group = "version", HelpText = "Installs the specified node version")>]
      version: string
      [<Option('l', "lts", Group = "version", HelpText = "Ignores version and pulls down the latest LTS version")>]
      lts: Nullable<bool>
      [<Option('c', "current", Group = "version", HelpText = "Ignores version and pulls down the latest Current version")>]
      current: Nullable<bool>
      [<Option('d', "default", Required = false, HelpText = "Sets the downloaded version as default (default: false)")>]
      isDefault: Nullable<bool> }

[<Verb("uninstall", HelpText = "Uninstalls the specified node version or the latest Current by default")>]
type Uninstall =
    { [<Option('n', "node", Group = "version", HelpText = "Removes the specified node version")>]
      version: string
      [<Option('l', "lts", Group = "version", HelpText = "Ignores version and removes the latest LTS version")>]
      lts: Nullable<bool>
      [<Option('c', "current", Group = "version", HelpText = "Ignores version and removes latest Current version")>]
      current: Nullable<bool> }

[<Verb("use", HelpText = "Sets the Node Version")>]
type Use =
    { [<Option('n', "node", Group = "version", HelpText = "sets the specified node version in the PATH")>]
      version: string
      [<Option('l',
               "lts",
               Group = "version",
               HelpText = "Ignores version and sets the latest downloaded LTS version in the PATH")>]
      lts: Nullable<bool>
      [<Option('c',
               "current",
               Group = "version",
               HelpText = "Ignores version and sets the latest downloaded Current version in the PATH")>]
      current: Nullable<bool> }

[<Verb("list", HelpText = "Sets the Node Version")>]
type List =
    { [<Option('r', "remote", Required = false, HelpText = "Pulls the version list from the node website")>]
      remote: Nullable<bool> }

[<RequireQualifiedAccess>]
module Actions =
    let private validateVersion (num: string) =
        if num.IndexOf('v') = 0 then
            let (parsed, _) = num.Substring 1 |> System.Int32.TryParse
            parsed
        else
            let (parsed, _) = System.Int32.TryParse(num)
            parsed

    let private getInstallType (isLts: Nullable<bool>)
                               (isCurrent: Nullable<bool>)
                               (version: string)
                               : Result<InstallType, string> =
        let isLts = isLts |> Option.ofNullable
        let isCurrent = isCurrent |> Option.ofNullable
        let version = version |> Option.ofObj

        match isLts, isCurrent, version with
        | Some lts, None, None ->
            if lts
            then Ok LTS
            else Result.Error "No valid version was presented"
        | None, Some current, None ->
            if current
            then Ok Current
            else Result.Error "No valid version was presented"
        | None, None, Some version ->
            match version.Split(".") with
            | [| major |] ->
                if validateVersion major
                then Ok(SpecificM major)
                else Result.Error $"{version} is not a valid node version"
            | [| major; minor |] ->
                if validateVersion major && validateVersion minor
                then Ok(SpecificMM(major, minor))
                else Result.Error $"{version} is not a valid node version"
            | [| major; minor; patch |] ->
                if validateVersion major
                   && validateVersion minor
                   && validateVersion patch then
                    Ok(SpecificMMP(major, minor, patch))
                else
                    Result.Error $"{version} is not a valid node version"
            | _ -> Result.Error $"{version} is not a valid node version"
        | _ -> Result.Error $"Use only one of --lts 'boolean', --current 'boolean', or --version 'string'"

    let private setVersionAsDefault (version: string)
                                    (codename: string)
                                    (os: string)
                                    (arch: string)
                                    : Task<Result<unit, string>> =
        task {
            let directory = Common.getVersionDirName version os arch

            let symlinkpath = IO.getSymlinkPath codename directory os

            match Env.setEnvVersion os symlinkpath with
            | Ok _ ->
                AnsiConsole.MarkupLine("[yellow]Setting permissions for node[/]")

                match os with
                | "win" -> return Ok()
                | _ ->
                    let! result = IO.trySetPermissionsUnix symlinkpath

                    if result.ExitCode <> 0 then
                        let errors =
                            result.Errors
                            |> List.fold (fun value next -> $"{value}\n{next}") ""

                        return Result.Error($"[red]Error while setting permissions[/]: {errors}")
                    else
                        return Ok()
            | Error err -> return Result.Error err
        }

    let private runPreInstallChecks () =
        let homedir = IO.createHomeDir ()
        AnsiConsole.MarkupLine("[bold yellow]Updating node versions[/]")

        task {
            let! file = Network.downloadNodeVersions (homedir.FullName)
            AnsiConsole.MarkupLine($"[green]Updated node versions on {file}[/]")
        }
        :> Task

    let Install (options: Install) =
        task {
            do! runPreInstallChecks ()

            let! versions = IO.getIndex ()

            match getInstallType options.lts options.current options.version,
                  (Option.ofNullable options.isDefault
                   |> Option.defaultValue false) with
            | Ok install, setAsDefault ->
                let version = Common.getVersionItem versions install

                match version with
                | Some version ->
                    let os = Common.getOS ()
                    let arch = Common.getArch ()

                    let codename = Common.getCodename version

                    let! checksums = Network.downloadChecksumsForVersion $"{version.version}"
                    let! node = Network.downloadNode $"{version.version}" version.version os arch
                    AnsiConsole.MarkupLine $"[#5f5f00]Downloaded[/]: {checksums} - {node}"

                    match IO.getChecksumForVersion checksums version.version os arch with
                    | Some checksum ->
                        if not (IO.verifyChecksum node checksum) then
                            let compares =
                                $"download: {IO.getChecksumForFile node}]\nchecksum: {checksum}"

                            AnsiConsole.MarkupLine $"[bold red]The Checksums didnt match\n{compares}[/]"
                            return 1
                        else
                            let what = $"[yellow]{node}[/]"

                            let target =
                                $"[yellow]{Common.getHome ()}/latest-{codename}[/]"

                            AnsiConsole.MarkupLine $"[#5f5f00]Extracting[/]: {what} to {target}"

                            IO.extractContents os node (IO.fullPath (Common.getHome (), [ $"latest-{codename}" ]))

                            AnsiConsole.MarkupLine "[green]Extraction Complete![/]"
                            IO.deleteFile node

                            if setAsDefault then
                                let! result = setVersionAsDefault version.version codename os arch

                                match result with
                                | Ok () ->
                                    AnsiConsole.MarkupLine
                                        $"[bold green]Node version {version.version} installed and set as default[/]"
                                | Error err -> AnsiConsole.MarkupLine err

                            return 0
                    | None ->
                        AnsiConsole.MarkupLine
                            $"[bold red]The Checksums didnt match\ndownload: {IO.getChecksumForFile node}\nchecksum: None[/]"

                        return 1
                | None ->
                    AnsiConsole.MarkupLine "[bold red]Version Not found[/]"
                    return 1
            | Result.Error err, _ ->
                AnsiConsole.MarkupLine $"[bold red]{err}[/]"
                return 1
        }


    let Use (options: Use) =
        task {
            AnsiConsole.MarkupLine $"[bold yellow]Checking local versions[/]"

            let! versions = IO.getIndex ()

            match getInstallType options.lts options.current options.version with
            | Ok install ->
                let version = Common.getVersionItem versions install

                match version with
                | Some version ->

                    let os = Common.getOS ()
                    let arch = Common.getArch ()

                    let codename = Common.getCodename version

                    if not (IO.codenameExistsInDisk codename) then
                        let l1 = "[bold red]We didn't find version[/]"
                        let l2 = $"[bold yellow]%s{version.version}[/]"
                        AnsiConsole.MarkupLine $"{l1} {l2} within [bold yellow]%s{codename}[/]"
                        return 1
                    else
                        AnsiConsole.MarkupLine $"[bold yellow]Setting version[/] [green]%s{version.version}[/]"

                        let! result = setVersionAsDefault version.version codename os arch

                        match result with
                        | Ok () ->
                            AnsiConsole.MarkupLine $"[bold green]Node version {version.version} set as the default[/]"
                        | Error err -> AnsiConsole.MarkupLine err

                        return 0
                | None ->
                    AnsiConsole.MarkupLine "[bold red]Version Not found[/]"
                    return 1
            | Error err ->
                AnsiConsole.MarkupLine $"[bold red]{err}[/]"
                return 1
        }

    let Uninstall (options: Uninstall) = 0
    let List (options: List) = 0
