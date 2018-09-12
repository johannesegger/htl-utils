module ClassList

type GetClassListError =
    | GetClassListError of string

let getClassList (classList: Async<string list>) = async {
    let! students = async {
        try
            let! list = classList
            return Ok list
        with e -> return GetClassListError (e.ToString()) |> Error
    }
    return students
}