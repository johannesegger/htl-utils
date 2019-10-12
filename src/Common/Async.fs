module Async

let map fn a = async {
    let! v = a
    return fn v
}
