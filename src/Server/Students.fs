module Students

type GetClassListError =
    | GetClassListError of string

let getClassList (classList: Async<string list>) = async {
    let! classes = async {
        try
            let! list = classList
            return Ok list
        with e -> return GetClassListError (e.ToString()) |> Error
    }
    return classes
}

type GetStudentsError =
    | GetStudentsError of string

let getStudents (students: string -> Async<(string * string) list>) className = async {
    let! students = async {
        try
            let! list = students className
            return Ok list
        with e -> return GetStudentsError (e.ToString()) |> Error
    }
    return students
}