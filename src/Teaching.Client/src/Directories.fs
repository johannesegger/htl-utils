namespace global

open Thoth.Json

[<Fable.Core.Erase>] // enables equality
type DirectoryPath = DirectoryPath of string list

module DirectoryPath =
    let empty =
        DirectoryPath []

    let getName (DirectoryPath path) =
        List.head path

    let combine (DirectoryPath path) children =
        DirectoryPath (List.rev children @ path)

    let isChildDirectory (DirectoryPath ``base``) (DirectoryPath child) =
        let lengthDiff = List.length child - List.length ``base``
        if lengthDiff >= 0 then
            List.skip lengthDiff child = ``base``
        else false

    let isRoot (DirectoryPath path) =
        List.isEmpty path

    let toNormalized (DirectoryPath path) = List.rev path

    let toString = toNormalized >> String.concat "/"

type DirectoryChildren =
    | LoadedDirectoryChildren of Directory list
    | NotLoadedDirectoryChildren
    | FailedToLoadDirectoryChildren
and Directory =
    {
        Path: DirectoryPath
        IsSelected: bool
        IsLoading: bool
        Children: DirectoryChildren
    }

module Directory =
    let private update path map directory =
        let rec fn path directory =
            match path, directory with
            | [], dir ->
                map dir
            | path :: xs, ({ Children = LoadedDirectoryChildren children } as dir) ->
                let childDirs =
                    children
                    |> List.map (fun childDir ->
                        if DirectoryPath.getName childDir.Path = path
                        then fn xs childDir
                        else childDir
                    )
                { dir with Children = LoadedDirectoryChildren childDirs }
            | _ :: _, { Children = NotLoadedDirectoryChildren }
            | _ :: _, { Children = FailedToLoadDirectoryChildren } ->
                directory
        fn (DirectoryPath.toNormalized path) directory

    let setChildDirectories path childDirectories directory =
        let fn dir =
            let childDirectories' =
                childDirectories
                |> List.map (fun n -> {
                    Path = DirectoryPath.combine dir.Path [ n ]
                    IsSelected = false
                    IsLoading = false
                    Children = NotLoadedDirectoryChildren
                })
            { dir with Children = LoadedDirectoryChildren childDirectories' }

        update path fn directory

    let setChildDirectoriesFailedToLoad path directory =
        let fn dir =
            { dir with Children = FailedToLoadDirectoryChildren }

        update path fn directory

    let select path directory =
        let rec fn (directory: Directory) =
            { directory with
                IsSelected = DirectoryPath.isChildDirectory directory.Path path
                Children =
                    match directory.Children with
                    | LoadedDirectoryChildren children ->
                        LoadedDirectoryChildren (List.map fn children)
                    | NotLoadedDirectoryChildren ->
                        NotLoadedDirectoryChildren
                    | NotLoadedDirectoryChildren
                    | FailedToLoadDirectoryChildren as x -> x
            }
        fn directory

    let setLoading path value directory =
        update path (fun d -> { d with IsLoading = value }) directory

    let addChildDirectory path name directory =
        let fn dir =
            match dir with
            | { Children = LoadedDirectoryChildren children } ->
                let children' =
                    let child = {
                        Path = DirectoryPath.combine dir.Path [ name ]
                        IsSelected = false
                        IsLoading = false
                        Children = NotLoadedDirectoryChildren
                    }
                    child :: children
                { dir with Children = LoadedDirectoryChildren children' }
            | x -> x
        update path fn directory

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

    let mapSelected fn directory =
        let rec map' level directory =
            match directory with
            | { Children = LoadedDirectoryChildren children } as dir when dir.IsSelected ->
                List.append
                    [ fn level dir ]
                    (List.collect (map' (level + 1)) children)
            | dir when dir.IsSelected ->
                [ fn level dir ]
            | _ -> []
        map' 0 directory
