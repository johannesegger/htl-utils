module Toast

open Fable.React
open Fable.React.Props
open Fulma
open Fable.FontAwesome
open Thoth.Elmish

let create message =
    Toast.message message
    |> Toast.position Toast.BottomRight
    |> Toast.noTimeout
    |> Toast.withCloseButton
    |> Toast.dismissOnClick

// https://github.com/thoth-org/Thoth.Elmish.Toast/blob/main/demo/src/App.fs#L25-L68
let renderToastWithFulma =
    { new Toast.IRenderer<ReactElement> with
        member __.Toast children color =
            Notification.notification [ Notification.CustomClass color ]
                children

        member __.CloseButton onClick =
            Notification.delete [ Props [ OnClick onClick ] ]
                [ ]

        member __.Title txt =
            Heading.h5 []
                       [ str txt ]

        member __.Icon icon =
            icon

        member __.SingleLayout title message =
            div [ ]
                [ title; message ]

        member __.Message txt =
            span [ ]
                 [ str txt ]

        member __.SplittedLayout iconView title message =
            Columns.columns [ Columns.IsGapless
                              Columns.IsVCentered ]
                [ Column.column [ Column.Width (Screen.All, Column.Is2) ]
                    [ iconView ]
                  Column.column [ ]
                    [ title
                      message ] ]

        member __.StatusToColor status =
            match status with
            | Toast.Success -> "is-success"
            | Toast.Warning -> "is-warning"
            | Toast.Error -> "is-danger"
            | Toast.Info -> "is-info"
    }
