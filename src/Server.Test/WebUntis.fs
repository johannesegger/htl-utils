module WebUntis

open Expecto
open System
open System.Net
open System.Net.Http

let tests = testList "WebUntis" [
    testCaseAsync "Login" <| async {
        let cookieContainer = CookieContainer()
        use handler = new HttpClientHandler(CookieContainer = cookieContainer)
        use httpClient = new HttpClient(handler)
        do! WebUntis.login httpClient
    }

    testCaseAsync "Get teachers" <| async {
        let cookieContainer = CookieContainer()
        do! async {
            use handler = new HttpClientHandler(CookieContainer = cookieContainer)
            use httpClient = new HttpClient(handler)
            return! WebUntis.login httpClient
        }
        let! teachers = async {
            use handler = new HttpClientHandler(CookieContainer = cookieContainer)
            use httpClient = new HttpClient(handler)
            return! WebUntis.getTeachers httpClient DateTime.Today
        }
        Expect.all teachers (fun t -> not <| String.IsNullOrEmpty t.Id) "All teachers should have an id"
        Expect.all teachers (fun t -> not <| String.IsNullOrEmpty t.ShortName) "All teachers should have a short name"
        Expect.all teachers (fun t -> not <| String.IsNullOrEmpty t.FirstName) "All teachers should have a first name"
        Expect.all teachers (fun t -> not <| String.IsNullOrEmpty t.LastName) "All teachers should have a last name"
    }

    testCaseAsync "Get teacher classes in regular week" <| async {
        let cookieContainer = CookieContainer()
        do! async {
            use handler = new HttpClientHandler(CookieContainer = cookieContainer)
            use httpClient = new HttpClient(handler)
            return! WebUntis.login httpClient
        }
        let date = DateTime(2019, 10, 07)
        let! teachers = async {
            use handler = new HttpClientHandler(CookieContainer = cookieContainer)
            use httpClient = new HttpClient(handler)
            return! WebUntis.getTeachers httpClient date
        }
        let! classNames = async {
            use handler = new HttpClientHandler(CookieContainer = cookieContainer)
            use httpClient = new HttpClient(handler)
            return! WebUntis.tryGetClassNamesFromTeacherTimetable httpClient date (List.head teachers).Id
        }
        Expect.isOk classNames "Couldn't get class names"
        Expect.isNonEmpty (classNames |> Result.toOption |> Option.defaultValue Set.empty) "Class names are empty"
    }

    testCaseAsync "Get teacher classes in irregular week" <| async {
        let cookieContainer = CookieContainer()
        do! async {
            use handler = new HttpClientHandler(CookieContainer = cookieContainer)
            use httpClient = new HttpClient(handler)
            return! WebUntis.login httpClient
        }
        let date = DateTime(2019, 09, 09)
        let! teachers = async {
            use handler = new HttpClientHandler(CookieContainer = cookieContainer)
            use httpClient = new HttpClient(handler)
            return! WebUntis.getTeachers httpClient date
        }
        let! classNames = async {
            use handler = new HttpClientHandler(CookieContainer = cookieContainer)
            use httpClient = new HttpClient(handler)
            return! WebUntis.tryGetClassNamesFromTeacherTimetable httpClient date (List.head teachers).Id
        }
        Expect.isError classNames "Unexpectedly got class names"
    }

    testCaseAsync "Get teacher classes starting in irregular week" <| async {
        let cookieContainer = CookieContainer()
        do! async {
            use handler = new HttpClientHandler(CookieContainer = cookieContainer)
            use httpClient = new HttpClient(handler)
            return! WebUntis.login httpClient
        }
        let date = DateTime(2019, 09, 09)
        let! teachers = async {
            use handler = new HttpClientHandler(CookieContainer = cookieContainer)
            use httpClient = new HttpClient(handler)
            return! WebUntis.getTeachers httpClient date
        }
        let! classNames = async {
            use handler = new HttpClientHandler(CookieContainer = cookieContainer)
            use httpClient = new HttpClient(handler)
            return! WebUntis.tryGetClassNamesFromTeacherTimetableInInterval httpClient date (DateTime(2019, 10, 07)) (List.head teachers).Id
        }
        Expect.isOk classNames "Couldn't get class names"
        Expect.isNonEmpty (classNames |> Result.toOption |> Option.defaultValue Set.empty) "Class names are empty"
    }

    testCaseAsync "Get teacher classes for all teachers" <| async {
        let cookieContainer = CookieContainer()
        do! async {
            use handler = new HttpClientHandler(CookieContainer = cookieContainer)
            use httpClient = new HttpClient(handler)
            return! WebUntis.login httpClient
        }
        let date = DateTime(2019, 09, 09)
        let! classTeachers = async {
            use handler = new HttpClientHandler(CookieContainer = cookieContainer)
            use httpClient = new HttpClient(handler)
            return! WebUntis.getClassesWithTeachers httpClient date
        }
        Expect.isOk classTeachers "Couldn't get classes with teachers"
        let classTeachers =
            classTeachers
            |> Result.toOption
            |> Option.defaultValue []
        Expect.isNonEmpty classTeachers "Classes are empty"
        Expect.all classTeachers (snd >> List.isEmpty >> not) "Teachers are empty"
    }
]