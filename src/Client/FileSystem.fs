// ts2fable 0.6.1
module rec FileSystem
open System
open Fable.Core
open Fable.Import.Browser
open Fable.Import.JS


type [<AllowNullLiteral>] FileSystemEntry =
    abstract name: string with get, set
    abstract isDirectory: bool with get, set
    abstract isFile: bool with get, set

type [<AllowNullLiteral>] FileSystemEntryMetadata =
    abstract modificationTime: DateTime option with get, set
    abstract size: float option with get, set

type [<AllowNullLiteral>] FileSystemDirectoryReader =
    abstract readEntries: successCallback: (ResizeArray<FileSystemEntry> -> unit) * ?errorCallback: (DOMException -> unit) -> unit

type [<AllowNullLiteral>] FileSystemFlags =
    abstract create: bool option with get, set
    abstract exclusive: bool option with get, set

type [<AllowNullLiteral>] FileSystemDirectoryEntry =
    inherit FileSystemEntry
    abstract isDirectory: obj with get, set
    abstract isFile: obj with get, set
    abstract createReader: unit -> FileSystemDirectoryReader
    // abstract getFile: ?path: USVString * ?options: FileSystemFlags * ?successCallback: (FileSystemFileEntry -> unit) * ?errorCallback: (DOMError -> unit) -> unit
    // abstract getDirectory: ?path: USVString * ?options: FileSystemFlags * ?successCallback: (FileSystemDirectoryEntry -> unit) * ?errorCallback: (DOMError -> unit) -> unit

type [<AllowNullLiteral>] FileSystemFileEntry =
    inherit FileSystemEntry
    abstract isDirectory: obj with get, set
    abstract isFile: obj with get, set
    abstract file: callback: (File -> unit) -> unit