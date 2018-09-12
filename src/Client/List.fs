module List

let intersperse separator list =
    let folder item = function
        | [] -> [ item ]
        | x -> item :: separator :: x
    List.foldBack folder list []