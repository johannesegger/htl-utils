module Directories

open Shared.Common

type DirectoryChildren =
    | LoadedDirectoryChildren of Directory list
    | NotLoadedDirectoryChildren
    | FailedToLoadDirectoryChildren
and Directory =
    {
        Path: DirectoryPath
        IsSelected: bool
        Children: DirectoryChildren
    }

let private updateDirectory path fn directory =
    let rec updateDirectory' path directory =
        match path, directory with
        | [], dir ->
            fn dir
        | path :: xs, ({ Children = LoadedDirectoryChildren children } as dir) ->
            let childDirs =
                children
                |> List.map (fun childDir ->
                    if DirectoryPath.getName childDir.Path = path
                    then updateDirectory' xs childDir
                    else childDir
                )
            { dir with Children = LoadedDirectoryChildren childDirs }
        | _ :: _, { Children = NotLoadedDirectoryChildren }
        | _ :: _, { Children = FailedToLoadDirectoryChildren } ->
            directory
    updateDirectory' (DirectoryPath.toNormalized path) directory

let setChildDirectories path childDirectories directory =
    let fn dir =
        let childDirectories' =
            childDirectories
            |> List.map (fun n -> { Path = DirectoryPath.combine dir.Path [ n ]; IsSelected = false; Children = NotLoadedDirectoryChildren })
        { dir with Children = LoadedDirectoryChildren childDirectories' }

    updateDirectory path fn directory

let setChildDirectoriesFailedToLoad path directory =
    let fn dir =
        { dir with Children = FailedToLoadDirectoryChildren }

    updateDirectory path fn directory

let selectDirectory path directory =
    let rec selectDirectory' (directory: Directory) =
        { directory with
            IsSelected = DirectoryPath.isChildDirectory directory.Path path
            Children =
                match directory.Children with
                | LoadedDirectoryChildren children ->
                    LoadedDirectoryChildren (List.map selectDirectory' children)
                | NotLoadedDirectoryChildren ->
                    NotLoadedDirectoryChildren
                | NotLoadedDirectoryChildren
                | FailedToLoadDirectoryChildren as x -> x
        }
    selectDirectory' directory

let addChildDirectory path name directory =
    let fn dir =
        match dir with
        | { Children = LoadedDirectoryChildren children } ->
            let children' =
                let child = { Path = DirectoryPath.combine dir.Path [ name ]; IsSelected = false; Children = NotLoadedDirectoryChildren }
                child :: children
            { dir with Children = LoadedDirectoryChildren children' }
        | x -> x
    updateDirectory path fn directory

let rec getSelectedDirectory directory =
    if not directory.IsSelected
    then None
    else
        match directory with
        | { Children = LoadedDirectoryChildren children } ->
            children
            |> List.tryPick getSelectedDirectory
            |> Option.orElse (Some directory)
        | _ -> Some directory

let mapDirectory fn directory =
    let rec mapDirectory' fn level directory =
        match directory with
        | { Children = LoadedDirectoryChildren children } as dir when dir.IsSelected ->
            List.append
                [ fn level directory ]
                (List.collect (mapDirectory' fn (level + 1)) children)
        | _ -> []
    mapDirectory' fn 0 directory
