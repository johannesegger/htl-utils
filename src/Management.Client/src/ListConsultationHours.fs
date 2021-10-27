module ListConsultationHours

open ConsultationHours.DataTransferTypes
open Fable.Core
open Fable.React
open Fable.React.Props
open FSharp.Control
open Fulma
open Thoth.Fetch
open Thoth.Json

type ConsultationHoursPerClass = {
    Class: string
    ConsultationHours: ConsultationHourEntry list
}

type LoadableConsultationHours =
    | NotLoadedConsultationHours
    | LoadingConsultationHours
    | LoadedConsultationHours of ConsultationHoursPerClass list
    | FailedToLoadConsultationHours

type Model = LoadableConsultationHours

type Msg =
    | LoadConsultationHours
    | LoadConsultationHoursResponse of Result<ConsultationHourEntry list, exn>

let update msg model =
    match msg with
    | LoadConsultationHours -> LoadingConsultationHours
    | LoadConsultationHoursResponse (Ok consultationHours) ->
        let consultationHoursPerClass =
            consultationHours
            |> Seq.collect (fun consultationHour ->
                consultationHour.Subjects
                |> Seq.map (fun teacherSubject -> teacherSubject.Class)
                |> Seq.distinct
                |> Seq.map (fun ``class`` -> ``class``, consultationHour)
            )
            |> Seq.groupBy fst
            |> Seq.map (fun (g, vs) ->
                {
                    Class = g
                    ConsultationHours = vs |> Seq.map snd |> Seq.sortBy (fun v -> v.Teacher.LastName, v.Teacher.FirstName) |> Seq.toList
                }
            )
            |> Seq.sortBy (fun v -> v.Class)
            |> Seq.toList
        LoadedConsultationHours consultationHoursPerClass
    | LoadConsultationHoursResponse (Error ex) ->
        FailedToLoadConsultationHours

let init = NotLoadedConsultationHours

let view model dispatch =
    Container.container [] [
        match model with
        | NotLoadedConsultationHours
        | LoadingConsultationHours ->
            Section.section [] [
                Progress.progress [ Progress.Color IsDanger ] []
            ]
        | FailedToLoadConsultationHours ->
            Section.section [] [ Views.errorWithRetryButton "Error while loading consultation hours" (fun () -> dispatch LoadConsultationHours) ]
        | LoadedConsultationHours data ->
            yield!
                data
                |> List.map (fun consultationHoursPerClass ->
                    Section.section [] [
                        Heading.h3 [] [ str consultationHoursPerClass.Class ]
                        Table.table [] [
                            thead [] [
                                tr [] [
                                    th [] [ str "Short name" ]
                                    th [] [ str "Last name" ]
                                    th [] [ str "First name" ]
                                    th [] [ str "Subject(s)" ]
                                    th [] [ str "Day of week" ]
                                    th [] [ str "Time" ]
                                    th [] [ str "Location" ]
                                ]
                            ]
                            tbody [] [
                                yield!
                                    consultationHoursPerClass.ConsultationHours
                                    |> List.map (fun consultationHour ->
                                        tr [ if consultationHour.FormTeacherOfClasses |> List.exists (String.equalsCaseInsensitive consultationHoursPerClass.Class) then Style [ FontWeight "bold" ] ] [
                                            td [] [ str consultationHour.Teacher.ShortName ]
                                            td [] [ str consultationHour.Teacher.LastName ]
                                            td [] [ str consultationHour.Teacher.FirstName ]
                                            let subjects =
                                                consultationHour.Subjects
                                                |> Seq.filter (fun s -> String.equalsCaseInsensitive s.Class consultationHoursPerClass.Class)
                                                |> Seq.map (fun s -> sprintf "%s - %s" s.Subject.ShortName s.Subject.FullName)
                                                |> Seq.sort
                                                |> Seq.map str
                                                |> Seq.toList
                                                |> List.intersperse (br [])
                                            td [] subjects
                                            match consultationHour.Details with
                                            | Some details ->
                                                td [] [ str details.DayOfWeek ]
                                                td [] [ str (sprintf "%02d:%02d - %02d:%02d" details.BeginTime.Hours details.BeginTime.Minutes details.EndTime.Hours details.EndTime.Minutes) ]
                                                td [] [ str (Room.toString details.Location) ]
                                            | None -> td [ ColSpan 3 ] [ str "Nach telefonischer Terminvereinbarung" ]
                                        ]
                                    )
                            ]
                        ]
                    ]
                )
    ]

let stream (pageActive: IAsyncObservable<bool>) (states: IAsyncObservable<Msg option * Model>) (msgs: IAsyncObservable<Msg>) =
    pageActive
    |> AsyncRx.flatMapLatest (function
        | true ->
            [
                msgs

                let loadConsultationHours =
                    AsyncRx.defer (fun () ->
                        AsyncRx.ofAsync (async {
                            let coders = Extra.empty |> Thoth.addCoders
                            let! (consultationHourEntries: ConsultationHourEntry list) = Fetch.get("/api/consultation-hours", extra = coders) |> Async.AwaitPromise
                            return consultationHourEntries
                        })
                        |> AsyncRx.map Ok
                        |> AsyncRx.catch (Error >> AsyncRx.single)
                    )

                states
                |> AsyncRx.choose (fst >> function | Some LoadConsultationHours -> Some loadConsultationHours | _ -> None)
                |> AsyncRx.switchLatest
                |> AsyncRx.showSimpleErrorToast (fun e -> "Loading consultation hours failed", e.Message)
                |> AsyncRx.map LoadConsultationHoursResponse

                AsyncRx.single LoadConsultationHours
            ]
            |> AsyncRx.mergeSeq
        | false -> AsyncRx.empty ()
    )
