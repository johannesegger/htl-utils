module Sokrates.Core

open FSharp.Data
open Sokrates.Configuration
open Sokrates.Domain
open System
open System.Net.Http
open System.Security.Cryptography.X509Certificates
open System.Text
open System.Text.RegularExpressions
open System.Xml
open System.Xml.Linq
open System.Xml.XPath

[<Literal>]
let private SchemaFile = __SOURCE_DIRECTORY__ + "\\sokrates.xsd"
type private SokratesApi = XmlProvider<Schema = SchemaFile>

let private parsePhoneNumber (text: string) =
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

type ParameterType =
    | Simple of string
    | List of (string * string) list

let private getRequestContent messageName parameters = reader {
    let xmlParameters =
        parameters
        |> List.map (fun (key, value) ->
            match value with
            | Simple text -> sprintf "<%s xmlns=\"\">%s</%s>" key text key
            | List values ->
                let children =
                    values
                    |> List.map (fun (key, value) -> sprintf "<%s>%s</%s>" key value key)
                    |> String.concat ""
                sprintf "<%s xmlns=\"\">%s</%s>" key children key
        )
        |> String.concat Environment.NewLine
    let! config = Reader.environment
    return sprintf """<?xml version="1.0" encoding="utf-8"?>
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"
    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
    xmlns:xsd="http://www.w3.org/2001/XMLSchema">
    <soap:Header>
        <UsernameToken xmlns="http://wservices.sokrateslfs.siemens.at/">
            <username xmlns="">%s</username>
            <password xmlns="">%s</password>
        </UsernameToken>
    </soap:Header>
    <soap:Body>
        <%s xmlns="http://wservices.sokrateslfs.siemens.at/">
            <schoolID xmlns="">%s</schoolID>
%s
        </%s>
    </soap:Body>
</soap:Envelope>""" config.UserName config.Password messageName config.SchoolId xmlParameters messageName
}

let private createHttpClient = reader {
    let! config = Reader.environment
    let httpClientHandler = new HttpClientHandler()
    let cert = new X509Certificate2(config.ClientCertificatePath, config.ClientCertificatePassphrase)
    httpClientHandler.ClientCertificates.Add(cert) |> ignore
    return new HttpClient(httpClientHandler)
}

let private fetch requestContent = asyncReader {
    use! httpClient = createHttpClient |> AsyncReader.liftReader
    let! config = Reader.environment |> AsyncReader.liftReader
    use! response =
        httpClient.PostAsync(
            config.WebServiceUrl,
            new StringContent(requestContent, Encoding.UTF8, "text/xml")
        )
        |> Async.AwaitTask
        |> AsyncReader.liftAsync
    response.EnsureSuccessStatusCode() |> ignore
    use! contentStream = response.Content.ReadAsStreamAsync() |> Async.AwaitTask |> AsyncReader.liftAsync

    let doc = XDocument.Load(contentStream)
    let namespaceManager = XmlNamespaceManager(NameTable())
    namespaceManager.AddNamespace("S", "http://schemas.xmlsoap.org/soap/envelope/")
    namespaceManager.AddNamespace("ns2", "http://wservices.sokrateslfs.siemens.at/")
    let dataExchangeFault = doc.XPathSelectElement("/S:Envelope/S:Body/ns2:*/return/dataExchangeFault", namespaceManager)
    let faultCode = dataExchangeFault.Element(XName.Get "faultCode").Value
    let faultText = dataExchangeFault.Element(XName.Get "faultText").Value
    if faultCode <> "0" then
        failwithf "Data exchange fault: Code %s: %s" faultCode faultText
    return dataExchangeFault.ElementsAfterSelf() |> Seq.exactlyOne
}

let private tryGetAddress street streetNumber zip city country =
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

let private parseTeachers (xmlElement: XElement) =
    SokratesApi.TeacherList(xmlElement).TeacherEntries
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
                    Title = teacher.XElement.Element(XName.Get "title") |> Option.ofObj |> Option.map (fun n -> n.Value)
                    LastName = teacher.LastName
                    FirstName = teacher.FirstName
                    ShortName = token
                    DateOfBirth = DateTimeOffset(teacher.DateOfBirth).DateTime.Date
                    DegreeFront = teacher.XElement.Element(XName.Get "degree") |> Option.ofObj |> Option.map (fun n -> n.Value)
                    DegreeBack = teacher.XElement.Element(XName.Get "degree2") |> Option.ofObj |> Option.map (fun n -> n.Value)
                    Phones = phones
                    Address = address
                }
            | None -> None
        )
    )
    |> Seq.toList

let getTeachers = asyncReader {
    let! xmlElement = getRequestContent "getTeacher" [] |> Reader.bind fetch
    return parseTeachers xmlElement
}

let private parseClassName text =
    Regex.Replace(text, "_(WS|SS)$", "")

let private parseClasses (xmlElement: XElement) =
    SokratesApi.TsnClassList(xmlElement).TsnClassEntries
    |> Seq.filter (fun n -> Regex.IsMatch(n.ClassName, "^\d")) // AP_, VT
    |> Seq.map (fun n -> parseClassName n.ClassName)
    |> Seq.distinct
    |> Seq.toList

let getClasses schoolYear = asyncReader {
    let schoolYear =
        schoolYear
        |> Option.defaultValue (
            if DateTime.Now.Month < 9 then DateTime.Now.Year - 1
            else DateTime.Now.Year
        )
    let! xmlElement = getRequestContent "getTSNClasses" [ "schoolYear", Simple (string schoolYear) ] |> Reader.bind fetch
    return parseClasses xmlElement
}

let private parseStudents (xmlElement: XElement) =
    SokratesApi.PupilList(xmlElement).PupilEntries
    |> Seq.choose (fun n -> n.Pupil)
    |> Seq.map (fun student ->
        {
            Id = SokratesId student.SokratesId
            LastName = student.LastName
            FirstName1 = student.FirstName1
            FirstName2 = student.XElement.Element(XName.Get "firstName2") |> Option.ofObj |> Option.map (fun n -> n.Value)
            DateOfBirth = DateTimeOffset(student.DateOfBirth).DateTime.Date
            SchoolClass = parseClassName student.SchoolClass
        }
    )
    |> Seq.toList

let getStudents className date = asyncReader {
    let date = date |> Option.defaultValue DateTime.Today
    let! xmlElement = getRequestContent "getPupils" [ "dateOfInterest", Simple (date.ToString("s")) ] |> Reader.bind fetch
    let students = parseStudents xmlElement
    match className with
    | Some className -> return students |> List.filter (fun student -> CIString student.SchoolClass = CIString className)
    | None -> return students
}

let private parseStudentAddresses (xmlElement: XElement) =
    SokratesApi.PupilList(xmlElement).PupilEntries
    |> Seq.collect (fun n ->
        match n.Pupil with
        | Some student -> n.AddressHomes |> Seq.map (fun address -> student, address)
        | None -> Seq.empty
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

let getStudentAddresses date = asyncReader {
    let date = date |> Option.defaultValue DateTime.Today
    let! xmlElement = getRequestContent "getPupils" [ "dateOfInterest", Simple (date.ToString("s")) ] |> Reader.bind fetch
    return parseStudentAddresses xmlElement
}

let private parseContactInfos (xmlElement: XElement) =
    SokratesApi.ContactInfoList(xmlElement).ContactEntries
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

let getStudentContactInfos studentIds date = asyncReader {
    let date = date |> Option.defaultValue DateTime.Today
    let personIdsParameter =
        studentIds
        |> List.map (fun (SokratesId sokratesId) -> "personIDEntry", sokratesId)
        |> List
    let! xmlElement = getRequestContent "getContactInfos" [ "dateOfInterest", Simple (date.ToString("s")); "personIDs", personIdsParameter ] |> Reader.bind fetch
    return parseContactInfos xmlElement
}
