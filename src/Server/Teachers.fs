module Teachers

open System
open System.IO
open System.Threading
open Microsoft.Graph
open Polly
open SixLabors.ImageSharp
open SixLabors.ImageSharp.Processing
open SixLabors.Primitives
open Db

type Teacher = {
    ShortName: string
    FirstName: string
    LastName: string
    Birthday: DateTime option
    Contacts: ContactKind list
    PhotoPath: string option
}

let private mapDbTeacher contacts photos dbTeacher =
    let birthday =
        dbTeacher.SocialInsuranceNumber
        |> Option.bind (String.trySubstringFrom 4)
        |> Option.bind (DateTime.tryParseExact "ddMMyy")
    let photoName =
        sprintf "%s_%s" dbTeacher.LastName dbTeacher.FirstName
        |> String.toLower
    { ShortName = dbTeacher.ShortName
      FirstName = dbTeacher.FirstName
      LastName = dbTeacher.LastName
      Birthday = birthday
      Contacts = Map.tryFind dbTeacher.ShortName contacts |> Option.defaultValue []
      PhotoPath = photos |> Map.tryFind photoName }

let mapDbTeachers teacherImageDir dbContacts dbTeachers = async {
    let! contacts = dbContacts

    let photos =
        try
            Directory.GetFiles teacherImageDir
            |> Seq.map (fun f -> Path.GetFileNameWithoutExtension f |> String.toLower, f)
            |> Map.ofSeq
        with :? DirectoryNotFoundException -> Map.empty

    let! dbTeachers = dbTeachers

    return List.map (mapDbTeacher contacts photos) dbTeachers
}

let private retryGraphApiRequest (fn: 'a -> System.Threading.Tasks.Task<_>) arg =
    Policy
        .HandleInner<ServiceException>()
        .WaitAndRetryAsync(
            6,
            Func<_, _, _, _>(fun (i: int) (ex: exn) ctx ->
                ex :?> ServiceException |> Option.ofObj
                |> Option.bind (fun p -> p.ResponseHeaders |> Option.ofObj)
                |> Option.bind (fun p -> p.RetryAfter |> Option.ofObj) 
                |> Option.bind (fun p -> p.Delta |> Option.ofNullable)
                |> Option.defaultValue (TimeSpan.FromSeconds (pown 2. i))
            ),
            Func<_, _, _, _, _>(fun ex t i ctx -> System.Threading.Tasks.Task.CompletedTask))
        .ExecuteAsync(fun () -> fn arg)
    |> Async.AwaitTask

let rec readAll (initialRequest: 'req) (getItems: 'req -> System.Threading.Tasks.Task<'items>) (getNextRequest: 'items -> 'req) = async {
    let rec fetchNextItems currentItems allItems = async {
        match getNextRequest currentItems |> Option.ofObj with
        | Some request ->
            let! nextItems = retryGraphApiRequest getItems request
            return!
                nextItems
                |> Seq.toList
                |> List.append allItems
                |> fetchNextItems nextItems
        | None -> return allItems
    }

    let! initialItems = getItems initialRequest |> Async.AwaitTask
    return! fetchNextItems initialItems (Seq.toList initialItems)
}

let clearContacts (graphApiClient: GraphServiceClient) = async {
    let! existingContacts =
        retryGraphApiRequest
            (fun () ->
                readAll
                    (graphApiClient.Me.Contacts.Request().Select("id"))
                    (fun r -> r.GetAsync())
                    (fun items -> items.NextPageRequest)
                |> Async.StartAsTask)
            ()

    List.length existingContacts
    |> printfn "Deleting existing contacts (%d)"
    
    do!
        existingContacts
        |> Seq.map (fun c ->
            retryGraphApiRequest
                (fun () -> graphApiClient.Me.Contacts.[c.Id].Request().DeleteAsync() |> Async.AwaitTask |> Async.StartAsTask)
                ()
        )
        |> Async.Parallel
        |> Async.Ignore
}

let private addTeacherContacts (graphApiClient: GraphServiceClient) teachers = async {
    printfn "Adding teachers (%d)" (List.length teachers)

    let addContactInfos contactInfos contact =
        let folder (contact: Contact) contactInfo =
            match contactInfo with
            | Mobile number -> contact.MobilePhone <- number
            | Email address ->
                let list = contact.EmailAddresses :?> EmailAddress list
                contact.EmailAddresses <- list @ [ EmailAddress(Address = address) ]
            | Home number ->
                let list = contact.HomePhones :?> string list
                contact.HomePhones <- list @ [ number ]
            contact
        List.fold folder contact contactInfos

    let resizePhoto (path: string) =
        use image = Image.Load path
        image.Mutate(fun x ->
            let resizeOptions =
                ResizeOptions(
                    Size = Size 200,
                    Mode = ResizeMode.Crop,
                    CenterCoordinates = [ 0.f; 0.4f ]
                )
            x.Resize(resizeOptions) |> ignore
        )
        let target = new MemoryStream() :> Stream
        image.SaveAsJpeg target
        target.Seek(0L, SeekOrigin.Begin) |> ignore
        target

    do!
        teachers
        |> List.map (fun teacher -> async {
            let contact =
                let birthday =
                    teacher.Birthday
                    |> Option.map (fun date -> DateTimeOffset(date.Year, date.Month, date.Day, 0, 0, 0, TimeSpan.Zero))
                    |> Option.toNullable
                Contact(
                    GivenName = teacher.FirstName,
                    Surname = teacher.LastName,
                    DisplayName = sprintf "%s - %s %s" teacher.ShortName teacher.LastName teacher.FirstName,
                    Birthday = birthday,
                    EmailAddresses = [],
                    HomePhones = []
                )
                |> addContactInfos teacher.Contacts
            
            let! contact =
                retryGraphApiRequest
                    (fun p -> graphApiClient.Me.Contacts.Request().AddAsync(p))
                    contact

            match teacher.PhotoPath with
            | Some photoPath ->
                do!
                    use photoStream = resizePhoto photoPath
                    retryGraphApiRequest
                        (fun p -> graphApiClient.Me.Contacts.[contact.Id].Photo.Content.Request().PutAsync(p))
                        photoStream
                    |> Async.Ignore
            | None -> ()
        })
        |> Async.sequence
        |> Async.Ignore
}

let private getBirthdayCalendarId (graphApiClient: GraphServiceClient) = async {
    let! calendars =
        retryGraphApiRequest
            (fun () -> graphApiClient.Me.Calendars.Request().Select("id,name").GetAsync())
            ()

    return
        calendars
        |> Seq.tryFind (fun c -> String.equalsCaseInsensitive c.Name "Birthdays")
        |> Option.map (fun c -> c.Id)
        |> Option.defaultWith (fun () ->
            let calendarNames =
                calendars
                |> Seq.map (fun c -> c.Name)
                |> String.concat ", "
            failwithf "Birthday calendar not found. Found calendars (%d): %s" calendars.Count calendarNames
        )
}


let private turnOffBirthdayReminders (graphApiClient: GraphServiceClient) = async {
    let! birthdayCalendarId = getBirthdayCalendarId graphApiClient
    let! birthdayEvents =
        retryGraphApiRequest
            (fun () ->
                readAll
                    (graphApiClient.Me.Calendars.[birthdayCalendarId].Events.Request())
                    (fun r -> r.GetAsync())
                    (fun items -> items.NextPageRequest)
                |> Async.StartAsTask)
            ()

    List.length birthdayEvents
    |> printfn "Turning off reminders for all birthday events (%d)."

    do!
        birthdayEvents
        |> Seq.map (fun event -> async {
            let event' = Event(IsReminderOn = Nullable<_> false)
            return!
                retryGraphApiRequest
                    (fun p -> graphApiClient.Me.Calendars.[birthdayCalendarId].Events.[event.Id].Request().UpdateAsync(p))
                    event'
        })
        |> Async.Parallel
        |> Async.Ignore
}

let import graphApiClient teachers = async {
    do! clearContacts graphApiClient
    do! addTeacherContacts graphApiClient teachers
    do! turnOffBirthdayReminders graphApiClient
}