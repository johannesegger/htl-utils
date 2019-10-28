namespace global

open System
open System.Globalization

[<CustomEquality; CustomComparison>]
type CIString =
    CIString of string
        override this.Equals(arg) =
            if isNull arg then false
            elif obj.ReferenceEquals(this, arg) then true
            else
                match arg with
                | :? CIString as other ->
                    let (CIString x) = this
                    let (CIString y) = other
                    String.Equals(x, y, StringComparison.InvariantCultureIgnoreCase)
                | _ -> false

        override this.GetHashCode() =
            let (CIString v) = this
            CultureInfo.InvariantCulture.CompareInfo.GetSortKey(v, CompareOptions.IgnoreCase).KeyData
            |> Array.fold (curry HashCode.Combine) 0

        interface System.IComparable with
            member this.CompareTo other =
                match other with
                | :? CIString as other ->
                    let (CIString x) = this
                    let (CIString y) = other
                    String.Compare(x, y, StringComparison.InvariantCultureIgnoreCase)
                | _ -> invalidArg "other" "Cannot compare objects of different types"
