module Tuple

let mapFst fn (a, b) = (fn a, b)
let mapSnd fn (a, b) = (a, fn b)
