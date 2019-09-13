module Async

let map fn a = async {
    let! v = a
    return fn v
}

let sequence list =
    let folder item state = async {
        let! state' = state
        let! item' = item
        return item' :: state'
    }
    List.foldBack folder list (async { return [] })