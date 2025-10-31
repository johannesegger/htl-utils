module Sokrates

open FSharp.Data
open System
open System.IO
open System.Net.Http
open System.Security.Cryptography.X509Certificates
open System.Text
open System.Text.RegularExpressions
open System.Xml
open System.Xml.Linq
open System.Xml.XPath

module Option =
    let ofString (v: string) =
        if v.Trim() = "" then None
        else Some v

type Config = {
    WebServiceUrl: string
    UserName: string
    Password: string
    SchoolId: string
    ClientCertificate: byte[]
    ClientCertificatePassphrase: string
}
module Config =
    open Microsoft.Extensions.Configuration

    type SokratesConfig() =
        member val WebServiceUrl = "" with get, set
        member val UserName = "" with get, set
        member val Password = "" with get, set
        member val SchoolId = "" with get, set
        member val ClientCertificatePath = "" with get, set
        member val ClientCertificatePassphrase = "" with get, set
        member x.Build() : Config = {
            WebServiceUrl = x.WebServiceUrl
            UserName = x.UserName
            Password = x.Password
            SchoolId = x.SchoolId
            ClientCertificate = File.ReadAllBytes(x.ClientCertificatePath)
            ClientCertificatePassphrase = x.ClientCertificatePassphrase
        }

    let fromEnvironment () =
        let config =
            ConfigurationBuilder()
                .AddEnvironmentVariables()
                .AddUserSecrets<Config>()
                .Build()
        ConfigurationBinder.Get<SokratesConfig>(config.GetSection("Sokrates")).Build()

type SokratesId =
    SokratesId of string
        member this.Value =
            let (SokratesId v) = this
            v

type Gender = Male | Female

type Student = {
    Id: SokratesId
    LastName: string
    FirstName1: string
    FirstName2: string option
    DateOfBirth: DateTime
    SchoolClass: string
    Gender: Gender
}

type Phone =
    | Home of string
    | Mobile of string

type Address = {
    Country: string
    Zip: string
    City: string
    Street: string
}

type Teacher = {
    Id: SokratesId
    Title: string option
    LastName: string
    FirstName: string
    ShortName: string
    DateOfBirth: DateTime
    DegreeFront: string option
    DegreeBack: string option
    Phones: Phone list
    Address: Address option
    Gender: Gender option
}

type StudentAddress = {
    StudentId: SokratesId
    Address: Address option
    Phone1: string option
    Phone2: string option
    From: DateTimeOffset option
    Till: DateTimeOffset option
    UpdateDate: DateTimeOffset option
}

type StudentContactAddress = {
    Type: string
    Name: string
    EMailAddress: string option
    Address: Address option
    Phones: string list
    From: DateTimeOffset option
    Till: DateTimeOffset option
    UpdateDate: DateTimeOffset option
}

type StudentContact = {
    StudentId: SokratesId
    ContactAddresses: StudentContactAddress list
}

[<Literal>]
let private SchemaFile = __SOURCE_DIRECTORY__ + "/sokrates.xsd"
type private SokratesWebService = XmlProvider<Schema = SchemaFile>

type private ParameterType =
    | Simple of string
    | List of (string * string) list

type SokratesApi(config: Config) =
    let getSchoolYear (date: DateTime) =
        if date.Month < 9 then date.Year - 1
        else date.Year

    let getRequestContent messageName parameters =
        let xmlParameters =
            parameters
            |> List.map (fun (key, value) ->
                match value with
                | Simple text -> $"<%s{key} xmlns=\"\">%s{text}</%s{key}>"
                | List values ->
                    let children =
                        values
                        |> List.map (fun (key, value) -> $"<%s{key}>%s{value}</%s{key}>")
                        |> String.concat ""
                    $"<%s{key} xmlns=\"\">%s{children}</%s{key}>"
            )
            |> String.concat Environment.NewLine
        $"""<?xml version="1.0" encoding="utf-8"?>
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"
    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
    xmlns:xsd="http://www.w3.org/2001/XMLSchema">
    <soap:Header>
        <UsernameToken xmlns="http://wservices.sokrateslfs.siemens.at/">
            <username xmlns="">%s{config.UserName}</username>
            <password xmlns="">%s{config.Password}</password>
        </UsernameToken>
    </soap:Header>
    <soap:Body>
        <%s{messageName} xmlns="http://wservices.sokrateslfs.siemens.at/">
            <schoolID xmlns="">%s{config.SchoolId}</schoolID>
%s{xmlParameters}
        </%s{messageName}>
    </soap:Body>
</soap:Envelope>"""

    let createHttpClient () =
        let httpClientHandler = new HttpClientHandler()
        let cert = new X509Certificate2(config.ClientCertificate, config.ClientCertificatePassphrase)
        httpClientHandler.ClientCertificates.Add(cert) |> ignore
        new HttpClient(httpClientHandler)

    let fetch requestContent = async {
        use httpClient = createHttpClient ()
        use! response =
            httpClient.PostAsync(
                config.WebServiceUrl,
                new StringContent(requestContent, Encoding.UTF8, "text/xml")
            )
            |> Async.AwaitTask
        response.EnsureSuccessStatusCode() |> ignore
        use! contentStream = response.Content.ReadAsStreamAsync() |> Async.AwaitTask

        let doc = XDocument.Load(contentStream)
        let namespaceManager = XmlNamespaceManager(NameTable())
        namespaceManager.AddNamespace("S", "http://schemas.xmlsoap.org/soap/envelope/")
        namespaceManager.AddNamespace("ns2", "http://wservices.sokrateslfs.siemens.at/")
        let dataExchangeFault = doc.XPathSelectElement("/S:Envelope/S:Body/ns2:*/return/dataExchangeFault", namespaceManager)
        let faultCode = dataExchangeFault.Element(XName.Get "faultCode").Value
        let faultText = dataExchangeFault.Element(XName.Get "faultText").Value
        if faultCode <> "0" then
            failwith $"Sokrates returned error response: Code %s{faultCode}: %s{faultText}"
        return
            dataExchangeFault.ElementsAfterSelf()
            |> Seq.tryExactlyOne
            |> Option.defaultWith (fun () -> failwith $"Sokrates returned empty response: %A{doc}")
    }

    let tryGetAddress street streetNumber zip city country =
        match street, streetNumber, zip, city, country with
        | Some street, streetNumber, Some zip, Some city, Some country ->
            Some {
                Country = country
                Zip = zip
                City = city
                Street =
                    match streetNumber with
                    | Some streetNumber -> sprintf "%s %s" street streetNumber
                    | None -> street
            }
        | _ -> None

    let parsePhoneNumber (text: string) =
        let text = Regex.Replace(text, @"[\/\s]", "")
        let prefix = text.Substring(0, 4) |> int
        // see https://de.wikipedia.org/wiki/Telefonvorwahl_(%C3%96sterreich)#Mobilfunk
        if ((prefix >= 650 && prefix <= 653) ||
            (prefix = 655) || (prefix = 657) ||
            (prefix >= 659 && prefix <= 661) ||
            (prefix >= 663 && prefix <= 699)) then
            Mobile text
        else
            Home text

    let parseGender text =
        if CIString text = CIString "m" then Male
        elif CIString text = CIString "w" then Female
        else failwith $"Unknown gender \"%s{text}\""

    let parseTeachers (xmlElement: XElement) =
        SokratesWebService.TeacherList(xmlElement).TeacherEntries
        |> Seq.choose (fun node ->
            let phones =
                node.AddressHomes
                |> Seq.collect (fun n -> [ n.Phone1; n.Phone2 ])
                |> Seq.collect Option.toList
                |> Seq.map parsePhoneNumber
                |> Seq.toList
            let address =
                Seq.tryHead node.AddressHomes
                |> Option.bind (fun addressNode ->
                    match addressNode.Country, addressNode.Plz, addressNode.City, addressNode.Street, addressNode.StreetNumber with
                    | Some country, Some zip, Some city, Some street, streetNumber ->
                        Some {
                            Country = country
                            Zip = zip
                            City = city
                            Street =
                                match streetNumber with
                                | Some streetNumber -> sprintf "%s %s" street streetNumber
                                | None -> street
                        }
                    | _ -> None
                )
            node.Teacher
            |> Option.bind (fun teacher ->
                match teacher.XElement.Element(XName.Get "token") |> Option.ofObj |> Option.map (fun n -> n.Value) with
                | Some token ->
                    Some {
                        Id = SokratesId teacher.PersonId
                        Title = teacher.XElement.Element(XName.Get "title") |> Option.ofObj |> Option.map (fun n -> n.Value) |> Option.bind Option.ofString
                        LastName = teacher.LastName
                        FirstName = teacher.FirstName
                        ShortName = token
                        DateOfBirth = DateTimeOffset(teacher.DateOfBirth).DateTime.Date
                        DegreeFront = teacher.XElement.Element(XName.Get "degree") |> Option.ofObj |> Option.map (fun n -> n.Value) |> Option.bind Option.ofString
                        DegreeBack = teacher.XElement.Element(XName.Get "degree2") |> Option.ofObj |> Option.map (fun n -> n.Value) |> Option.bind Option.ofString
                        Phones = phones
                        Address = address
                        Gender = teacher.XElement.Element(XName.Get "gender") |> Option.ofObj |> Option.map (_.Value >> parseGender)
                    }
                | None -> None
            )
        )
        |> Seq.toList

    let parseClassName text =
        Regex.Replace(text, "_(WS|SS)$", "")

    let parseClasses (xmlElement: XElement) =
        SokratesWebService.TsnClassList(xmlElement).TsnClassEntries
        |> Seq.filter (fun n -> Regex.IsMatch(n.ClassName, @"^\d")) // AP_, VT
        |> Seq.map (fun n -> parseClassName n.ClassName)
        |> Seq.distinct
        |> Seq.toList

    let parseStudents (mainStudentXmlData: XElement) (additionalStudentXmlData: XElement) =
        let additionalData =
            SokratesWebService.TsnPupilList(additionalStudentXmlData).TsnPupilEntries
            |> Seq.map (fun v -> string v.PupilId, {| Gender = parseGender v.Sex |})
            |> Map.ofSeq

        SokratesWebService.PupilList(mainStudentXmlData).PupilEntries
        |> Seq.choose (fun n -> n.Pupil)
        |> Seq.map (fun student ->
            {
                Id = SokratesId student.SokratesId
                LastName = student.LastName
                FirstName1 = student.FirstName1
                FirstName2 = student.XElement.Element(XName.Get "firstName2") |> Option.ofObj |> Option.map (fun n -> n.Value)
                DateOfBirth = DateTimeOffset(student.DateOfBirth).DateTime.Date
                SchoolClass = parseClassName student.SchoolClass
                Gender =
                    Map.tryFind student.SokratesId additionalData
                    |> Option.map (fun v -> v.Gender)
                    |> Option.defaultWith (fun () -> failwith $"Can't find gender of student %A{student}")
            }
        )
        |> Seq.toList

    let parseStudentAddresses (xmlElement: XElement) =
        SokratesWebService.PupilList(xmlElement).PupilEntries
        |> Seq.collect (fun n ->
            match n.Pupil with
            | Some student ->
                [
                    yield! n.AddressHomes
                    |> Seq.map (fun address -> {| Street = address.Street; StreetNumber = address.StreetNumber; Plz = address.Plz; City = address.City; Country = address.Country; Phone1 = address.Phone1; Phone2 = address.Phone2; From = address.From; Till = address.Till; UpdateDate = address.UpdateDate |})
                    yield! n.AddressPayments
                    |> Seq.map (fun address -> {| Street = address.Street; StreetNumber = address.StreetNumber; Plz = address.Plz; City = address.City; Country = address.Country; Phone1 = address.Phone1; Phone2 = address.Phone2; From = address.From; Till = address.Till; UpdateDate = address.UpdateDate |})
                ]
                |> List.distinct
                |> List.map (fun address -> (student, address))
            | None -> []
        )
        |> Seq.map (fun (student, address) ->
            {
                StudentId = SokratesId student.SokratesId
                Address = tryGetAddress address.Street address.StreetNumber address.Plz address.City address.Country
                Phone1 = address.Phone1
                Phone2 = address.Phone2
                From = address.From
                Till = address.Till
                UpdateDate = address.UpdateDate
            }
        )
        |> Seq.toList

    let parseContactInfos (xmlElement: XElement) =
        SokratesWebService.ContactInfoList(xmlElement).ContactEntries
        |> Seq.choose (fun contactEntry ->
            match contactEntry.PersonId with
            | Some studentId ->
                Some {
                    StudentId = SokratesId studentId
                    ContactAddresses =
                        contactEntry.Addresses
                        |> Seq.map (fun address ->
                            {
                                Type = Option.defaultValue "" address.Type
                                Name = [ address.FirstName; address.LastName ] |> Seq.choose id |> String.concat " "
                                EMailAddress = address.Email
                                Address = tryGetAddress address.Street address.StreetNumber address.Plz address.City address.Country
                                Phones = [ address.Phone1; address.Phone2 ] |> List.choose id
                                From = address.From
                                Till = address.Till
                                UpdateDate = address.UpdateDate
                            }
                        )
                        |> Seq.toList
                }
            | None -> None
        )
        |> Seq.toList

    member _.FetchTeachers = async {
        let! xmlElement = getRequestContent "getTeacher" [] |> fetch
        return parseTeachers xmlElement
    }

    member _.FetchClasses schoolYear = async {
        let schoolYear =
            schoolYear
            |> Option.defaultWith (fun () -> getSchoolYear DateTime.Today)
        let! xmlElement = getRequestContent "getTSNClasses" [ "schoolYear", Simple (string schoolYear) ] |> fetch
        return parseClasses xmlElement
    }

    member _.FetchStudents className date = async {
        let date = date |> Option.defaultValue DateTime.Today
        let schoolYear = getSchoolYear date
        let! mainStudentXmlData = getRequestContent "getPupils" [ "dateOfInterest", Simple (date.ToString("s")) ] |> fetch
        let! additionalStudentXmlData = getRequestContent "getTSNPupils" [ "schoolYear", Simple (string schoolYear); "dateOfInterest", Simple (date.ToString("s")) ] |> fetch
        let students = parseStudents mainStudentXmlData additionalStudentXmlData
        match className with
        | Some className -> return students |> List.filter (fun student -> CIString student.SchoolClass = CIString className)
        | None -> return students
    }

    member _.FetchStudentAddresses date = async {
        let date = date |> Option.defaultValue DateTime.Today
        let! xmlElement = getRequestContent "getPupils" [ "dateOfInterest", Simple (date.ToString("s")) ] |> fetch
        return parseStudentAddresses xmlElement
    }

    member _.FetchStudentContactInfos studentIds date = async {
        let date = date |> Option.defaultValue DateTime.Today
        let personIdsParameter =
            studentIds
            |> List.map (fun (SokratesId sokratesId) -> "personIDEntry", sokratesId)
            |> List
        let! xmlElement = getRequestContent "getContactInfos" [ "dateOfInterest", Simple (date.ToString("s")); "personIDs", personIdsParameter ] |> fetch
        return parseContactInfos xmlElement
    }

    static member FromEnvironment () =
        SokratesApi(Config.fromEnvironment ())
