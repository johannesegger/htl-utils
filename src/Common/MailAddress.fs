namespace global

type MailAddress = {
    UserName: string
    Domain: string
}

module MailAddress =
    let tryParse (v: string) =
        match v.IndexOf('@') with
        | -1 -> None
        | idx -> Some <| { UserName = v.Substring(0, idx); Domain = v.Substring(idx + 1) }

    let toString v =
        sprintf "%s@%s" v.UserName v.Domain
