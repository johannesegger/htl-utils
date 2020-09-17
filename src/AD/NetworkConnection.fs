module NetworkConnection

// see https://stackoverflow.com/a/33634690/1293659

open System
open System.ComponentModel
open System.Runtime.InteropServices
open System.Text.RegularExpressions

type private ResourceScope =
    | Connected = 1
    | GlobalNetwork = 2
    | Remembered = 3
    | Recent = 4
type private ResourceType =
    | Any = 0
    | Disk = 1
    | Print = 2
    | Reserved = 8
type private ResourceDisplayType =
    | Generic = 0x0
    | Domain = 0x01
    | Server = 0x02
    | Share = 0x03
    | File = 0x04
    | Group = 0x05
    | Network = 0x06
    | Root = 0x07
    | Shareadmin = 0x08
    | Directory = 0x09
    | Tree = 0x0a
    | Ndscontainer = 0x0b

[<Struct; StructLayout(LayoutKind.Sequential)>]
type private NetResource =
    val mutable Scope : ResourceScope
    val mutable ResourceType : ResourceType
    val mutable DisplayType : ResourceDisplayType
    val mutable Usage : int
    val mutable LocalName : string
    val mutable RemoteName : string
    val mutable Comment : string
    val mutable Provider : string
    new(name) = {
      Scope = ResourceScope.GlobalNetwork
      ResourceType = ResourceType.Disk
      DisplayType = ResourceDisplayType.Share
      Usage = 0
      LocalName = null
      RemoteName = name
      Comment = null
      Provider = null
    }

module private Win32 =
    [<DllImport("mpr.dll")>]
    extern int WNetAddConnection2(NetResource netResource, string password, string username, int flags)
    [<DllImport("mpr.dll")>]
    extern int WNetCancelConnection2(string name, int flags, bool force)

let create userName password path =
    let networkName =
        let m = Regex.Match(path, @"^\\\\[^\\]+\\[^\\]+")
        if m.Success then m.Value
        else failwithf "Can't get share name from path \"%s\"" path
    let result = Win32.WNetAddConnection2(NetResource(networkName), password, userName, 0)
    if result <> 0 then raise (Win32Exception(result, sprintf "Error connecting to remote share %s" networkName))

    { new IDisposable with member _.Dispose() = Win32.WNetCancelConnection2(networkName, 0, true) |> ignore }
