module Async

let sequence list =
    let folder item state = async {
        let! state' = state
        let! item' = item
        return item' :: state'
    }
    List.foldBack folder list (async { return [] })