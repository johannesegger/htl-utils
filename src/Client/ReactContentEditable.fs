module ReactContentEditable

open Fable.Core
open Fable.Core.JsInterop
open Fable.Helpers.React
open Fable.Import.JS
open Fable.Import.React

type Props =
    | Html of string
    | OnChange of (FormEvent -> unit)
    // | OnBlur of Function
    // | Disabled of bool
    // | TagName of string
    // | ClassName of string
    // | Style of Object

let inline contentEditable (props : Props list) : ReactElement =
    ofImport "default" "react-contenteditable" (keyValueList CaseRules.LowerFirst props) []
