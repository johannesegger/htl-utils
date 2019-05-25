namespace Shared

module CreateStudentDirectories =
    type Input =
        {
            ClassName: string
            Path: string * string list
        }
