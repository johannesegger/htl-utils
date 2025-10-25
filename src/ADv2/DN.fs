module internal AD.DN

open AD.Configuration
open CPI.DirectoryServices

let private child name (DistinguishedName path) =
    let dn = DN(path)
    dn.GetChild(name).ToString() |> DistinguishedName

let childOU name = child (sprintf "OU=%s" name)
let childCN name = child (sprintf "CN=%s" name)

let parent (DistinguishedName path) =
    DistinguishedName (DN(path).Parent.ToString())

let head (DistinguishedName path) =
    DN(path).RDNs
    |> Seq.tryHead
    |> Option.bind (fun v -> v.Components |> Seq.tryExactlyOne)
    |> Option.map (fun v -> v.ComponentType, v.ComponentValue)
    |> Option.defaultWith (fun () -> failwithf "Can't get head from distinguished name \"%s\"" path)

let parentsAndSelf (DistinguishedName path) =
    let rec fn (dn: DN) acc =
        if Seq.isEmpty dn.RDNs
        then acc
        else
            let acc' = DistinguishedName (dn.ToString()) :: acc
            fn dn.Parent acc'

    fn (DN(path)) []

let tryFindParent path filter =
    parentsAndSelf path
    |> Seq.tryFind (fun (DistinguishedName parentPath) -> DN(parentPath).RDNs |> Seq.head |> (fun v -> filter (v.ToString())))

let isOU (DistinguishedName path) =
    let dn = DN(path)
    dn.RDNs
    |> Seq.tryHead
    |> Option.bind (fun v -> v.Components |> Seq.tryExactlyOne)
    |> Option.map (fun v -> CIString v.ComponentType = CIString "OU")
    |> Option.defaultValue false

let tryCN path =
    let (``type``, value) = head path
    if CIString ``type`` = CIString "CN" then Some value
    else None

let domainBase path =
    parentsAndSelf path
    |> List.tryFindBack (fun v -> head v |> fst |> CIString = CIString "DC")
    |> Option.defaultWith (fun () -> failwith $"Can't find domain base of %A{path}")
