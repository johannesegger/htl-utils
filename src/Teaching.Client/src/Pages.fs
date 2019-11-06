module Pages

open Elmish.UrlParser

type Page =
    | Home
    | WakeUp

let toHash = function
    | Home -> ""
    | WakeUp -> "#wake-up"

let pageParser: Parser<Page->Page,Page> =
    oneOf [
        map Home top
        map WakeUp (s "wake-up")
    ]
