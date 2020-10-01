module Async

let retn v = async { return v }

let bind fn a = async {
    let! v = a
    return! fn v
}

let map fn = bind (fn >> retn)
