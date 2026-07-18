namespace SokratesPowerShell

open System
open System.Security.Cryptography.X509Certificates
open System.Management.Automation
open Sokrates

/// Connect-Sokrates: establishes a session, stores it as the module-scoped default,
/// and returns it (so it can also be captured and passed explicitly via -Session).
[<Cmdlet(VerbsCommunications.Connect, "Sokrates")>]
[<OutputType(typeof<SokratesSession>)>]
type ConnectSokratesCommand() =
    inherit PSCmdlet()

    [<Parameter(Mandatory = true, Position = 0)>]
    member val Url = "" with get, set

    [<Parameter(Mandatory = true)>]
    member val Credential: PSCredential = null with get, set

    [<Parameter(Mandatory = true)>]
    member val SchoolId = "" with get, set

    [<Parameter(Mandatory = true)>]
    member val Certificate: X509Certificate2 = null with get, set

    override this.EndProcessing() =
        let config: Config =
            { WebServiceUrl = this.Url
              UserName = this.Credential.UserName
              Password = this.Credential.GetNetworkCredential().Password
              SchoolId = this.SchoolId
              ClientCertificate = this.Certificate }

        let session =
            SokratesSession(SokratesApi(config), $"Sokrates %s{this.Credential.UserName}@%s{this.Url}")

        DefaultSession.set this.SessionState session
        this.WriteObject session

/// Disconnect-Sokrates: clears the default session for this runspace.
[<Cmdlet(VerbsCommunications.Disconnect, "Sokrates")>]
type DisconnectSokratesCommand() =
    inherit PSCmdlet()
    override this.EndProcessing() = DefaultSession.clear this.SessionState

/// Get-SokratesSession: returns the current default session, if any.
[<Cmdlet(VerbsCommon.Get, "SokratesSession")>]
[<OutputType(typeof<SokratesSession>)>]
type GetSokratesSessionCommand() =
    inherit PSCmdlet()

    override this.EndProcessing() =
        match DefaultSession.get this.SessionState with
        | Some session -> this.WriteObject session
        | None -> ()

/// Get-SokratesTeacher: all teachers.
[<Cmdlet(VerbsCommon.Get, "SokratesTeacher")>]
[<OutputType(typeof<Teacher>)>]
type GetSokratesTeacherCommand() =
    inherit SokratesCmdlet()

    override this.EndProcessing() =
        let session = this.ResolveSession()
        this.WriteAll session.Api.FetchTeachers

/// Get-SokratesClass: class names, optionally for a given school year.
[<Cmdlet(VerbsCommon.Get, "SokratesClass")>]
[<OutputType(typeof<string>)>]
type GetSokratesClassCommand() =
    inherit SokratesCmdlet()

    [<Parameter>]
    member val SchoolYear = Nullable<int>() with get, set

    override this.EndProcessing() =
        let session = this.ResolveSession()

        let schoolYear =
            if this.SchoolYear.HasValue then
                Some this.SchoolYear.Value
            else
                None

        this.WriteAll(session.Api.FetchClasses schoolYear)

/// Get-SokratesStudent: students, optionally filtered by class and/or as of a date.
[<Cmdlet(VerbsCommon.Get, "SokratesStudent")>]
[<OutputType(typeof<Student>)>]
type GetSokratesStudentCommand() =
    inherit SokratesCmdlet()

    [<Parameter>]
    member val ClassName: string = null with get, set

    [<Parameter>]
    member val Date = Nullable<DateTime>() with get, set

    override this.EndProcessing() =
        let session = this.ResolveSession()
        let className = Option.ofObj this.ClassName
        let date = if this.Date.HasValue then Some this.Date.Value else None
        this.WriteAll(session.Api.FetchStudents className date)

/// Get-SokratesStudentAddress: student addresses as of a date (default today).
[<Cmdlet(VerbsCommon.Get, "SokratesStudentAddress")>]
[<OutputType(typeof<StudentAddress>)>]
type GetSokratesStudentAddressCommand() =
    inherit SokratesCmdlet()

    [<Parameter>]
    member val Date = Nullable<DateTime>() with get, set

    override this.EndProcessing() =
        let session = this.ResolveSession()
        let date = if this.Date.HasValue then Some this.Date.Value else None
        this.WriteAll(session.Api.FetchStudentAddresses date)

/// Get-SokratesStudentContactInfo: contact infos for the given student ids
/// (accepts ids from the pipeline) as of a date (default today).
[<Cmdlet(VerbsCommon.Get, "SokratesStudentContactInfo")>]
[<OutputType(typeof<StudentContact>)>]
type GetSokratesStudentContactInfoCommand() =
    inherit SokratesCmdlet()

    [<Parameter(Mandatory = true, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)>]
    [<Alias("Id")>]
    member val StudentId: string[] = [||] with get, set

    [<Parameter>]
    member val Date = Nullable<DateTime>() with get, set

    member val private Ids = System.Collections.Generic.List<string>() with get

    override this.ProcessRecord() =
        if not (isNull this.StudentId) then
            this.Ids.AddRange this.StudentId

    override this.EndProcessing() =
        let session = this.ResolveSession()
        let date = if this.Date.HasValue then Some this.Date.Value else None
        let ids = this.Ids |> Seq.map SokratesId |> List.ofSeq
        this.WriteAll(session.Api.FetchStudentContactInfos ids date)
