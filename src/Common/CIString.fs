namespace global

open System

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
                    StringComparer.InvariantCultureIgnoreCase.Equals(x, y)
                | _ -> false

        override this.GetHashCode() =
            let (CIString v) = this
            StringComparer.InvariantCultureIgnoreCase.GetHashCode(v)

        interface System.IComparable with
            member this.CompareTo other =
                match other with
                | :? CIString as other ->
                    let (CIString x) = this
                    let (CIString y) = other
                    StringComparer.InvariantCultureIgnoreCase.Compare(x, y)
                | _ -> invalidArg "other" "Cannot compare objects of different types"
