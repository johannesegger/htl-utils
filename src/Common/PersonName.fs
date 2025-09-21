namespace global

open System
open System.Globalization

#if !FABLE_COMPILER
module private Helper =
    let nameComparer = System.StringComparer.Create(CultureInfo.GetCultureInfo("de-AT"), ignoreCase = true)
[<CustomEquality; CustomComparison>]
type Name =
    PersonName of string
        override this.Equals(arg) =
            if isNull arg then false
            elif obj.ReferenceEquals(this, arg) then true
            else
                match arg with
                | :? Name as other ->
                    let (PersonName x) = this
                    let (PersonName y) = other
                    Helper.nameComparer.Equals(x, y)
                | _ -> false

        override this.GetHashCode() =
            let (PersonName v) = this
            Helper.nameComparer.GetHashCode(v)

        interface System.IComparable with
            member this.CompareTo other =
                match other with
                | :? Name as other ->
                    let (PersonName x) = this
                    let (PersonName y) = other
                    Helper.nameComparer.Compare(x, y)
                | _ -> invalidArg "other" "Cannot compare objects of different types"
#endif
