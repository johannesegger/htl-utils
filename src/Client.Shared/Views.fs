module Views

open Fulma
open Fable.FontAwesome
open Fable.React
open Fable.React.Props

let errorWithRetryButton text onRetryClick =
    Notification.notification [ Notification.Color IsDanger ] [
        Level.level []
            [
                Level.left []
                    [
                        Level.item []
                            [
                                Icon.icon [] [ Fa.i [ Fa.Solid.ExclamationTriangle ] [] ]
                                span [] [ str text ]
                            ]
                        Level.item []
                            [
                                Button.button
                                    [
                                        Button.Color IsSuccess
                                        Button.OnClick (fun _ev -> onRetryClick ())
                                    ]
                                    [
                                        Icon.icon [] [ Fa.i [ Fa.Solid.Sync ] [] ]
                                        span [] [ str "Retry" ]
                                    ]
                            ]
                    ]
            ]
    ]

let signInNotification text =
    Notification.notification [ Notification.Color IsWarning ]
        [
            Icon.icon [ Icon.Props [ Style [ MarginRight "10px" ] ] ] [ Fa.i [ Fa.Solid.ExclamationTriangle ] [] ]
            span [] [ str text ]
        ]
