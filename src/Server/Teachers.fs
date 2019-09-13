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

let clearContacts (graphServiceClient: GraphServiceClient) = async {
    let! existingContactIds = AAD.getContactIds graphServiceClient

    List.length existingContactIds
    |> printfn "Deleting existing contacts (%d)"
    
    do!
        existingContactIds
        |> Seq.map (AAD.removeContact graphServiceClient)
        |> Async.Parallel
        |> Async.Ignore
}

let private addTeacherContacts (graphServiceClient: GraphServiceClient) teachers = async {
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
            let contactData =
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
            
            let! contact = AAD.addContact graphServiceClient contactData

            match teacher.PhotoPath with
            | Some photoPath ->
                use photoStream = resizePhoto photoPath
                do!
                    AAD.setContactPhoto graphServiceClient contact.Id photoStream
                    |> Async.Ignore
            | None -> ()
        })
        |> Async.sequence
        |> Async.Ignore
}

let private getBirthdayCalendarId (graphServiceClient: GraphServiceClient) = async {
    let! calendars = AAD.getCalendars graphServiceClient

    return
        calendars
        |> Seq.tryFind (fun c -> String.equalsCaseInsensitive c.Name "Birthdays")
        |> Option.map (fun c -> c.Id)
        |> Option.defaultWith (fun () ->
            let calendarNames =
                calendars
                |> Seq.map (fun c -> c.Name)
                |> String.concat ", "
            failwithf "Birthday calendar not found. Found calendars (%d): %s" (List.length calendars) calendarNames
        )
}


let private turnOffBirthdayReminders (graphServiceClient: GraphServiceClient) = async {
    let! birthdayCalendarId = getBirthdayCalendarId graphServiceClient
    let! birthdayEvents = AAD.getCalendarEvents graphServiceClient birthdayCalendarId

    printfn "Turning off reminders for all birthday events (%d)." (List.length birthdayEvents)

    do!
        birthdayEvents
        |> Seq.map (fun event -> async {
            let event' = Event(IsReminderOn = Nullable<_> false)
            return! AAD.updateCalendarEvent graphServiceClient birthdayCalendarId event.Id event'
        })
        |> Async.Parallel
        |> Async.Ignore
}

let import graphServiceClient teachers = async {
    do! clearContacts graphServiceClient
    do! addTeacherContacts graphServiceClient teachers
    do! turnOffBirthdayReminders graphServiceClient
}