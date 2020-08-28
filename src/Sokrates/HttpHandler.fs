module Sokrates.HttpHandler

open FSharp.Control.Tasks.V2.ContextInsensitive
open FSharp.Data
open Giraffe
open Microsoft.AspNetCore.Http
open Sokrates.DataTransferTypes
open System
open System.Net.Http
open System.Text
open System.Text.RegularExpressions
open System.Xml
open System.Xml.Linq
open System.Xml.XPath

type SokratesApi = XmlProvider<Schema = "sokrates.xsd">

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

let private getRequestContent messageName parameters =
    let xmlParameters =
        parameters
        |> List.map (fun (key, value) -> sprintf "<%s xmlns=\"\">%s</%s>" key value key)
        |> String.concat Environment.NewLine
    let userName = Environment.getEnvVarOrFail "SOKRATES_USER_NAME"
    let password = Environment.getEnvVarOrFail "SOKRATES_PASSWORD"
    let schoolId = Environment.getEnvVarOrFail "SOKRATES_SCHOOL_ID"
    sprintf """<?xml version="1.0" encoding="utf-8"?>
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
</soap:Envelope>""" userName password messageName schoolId xmlParameters messageName

let private fetch (ctx: HttpContext) requestContent = async {
    let httpClientFactory = ctx.GetService<IHttpClientFactory>()
    use httpClient = httpClientFactory.CreateClient("SokratesApiClient")
    use! response =
        httpClient.PostAsync(
            "https://www.sokrates-bund.at/BRZPRODWS/ws/dataexchange",
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
    printfn "DataExchangeFault: %A" dataExchangeFault
    let faultCode = dataExchangeFault.Element(XName.Get "faultCode").Value
    let faultText = dataExchangeFault.Element(XName.Get "faultText").Value
    if faultCode <> "0" then
        failwithf "Data exchange fault: Code %s: %s" faultCode faultText
    return dataExchangeFault.ElementsAfterSelf() |> Seq.exactlyOne
}

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

let handleGetTeachers : HttpHandler =
    fun next ctx -> task {
        let! xmlElement = fetch ctx (getRequestContent "getTeacher" [])
        let teachers = parseTeachers xmlElement
        return! Successful.OK teachers next ctx
    }

let private parseClassName text =
    Regex.Replace(text, "_(WS|SS)$", "")

let private parseClasses (xmlElement: XElement) =
    SokratesApi.TsnClassList(xmlElement).TsnClassEntries
    |> Seq.filter (fun n -> n.ClassName |> String.startsWithCaseInsensitive "AP_" |> not) // looks like final classes
    |> Seq.map (fun n -> parseClassName n.ClassName)
    |> Seq.distinct
    |> Seq.toList

let handleGetClasses schoolYear : HttpHandler =
    fun next ctx -> task {
        let schoolYear =
            schoolYear
            |> Option.defaultValue (
                if DateTime.Now.Month < 8 then DateTime.Now.Year - 1
                else DateTime.Now.Year
            )
        let! xmlElement = fetch ctx (getRequestContent "getTSNClasses" [ "schoolYear", string schoolYear ])
        let classes = parseClasses xmlElement
        return! Successful.OK classes next ctx
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

let handleGetStudents className date : HttpHandler =
    fun next ctx -> task {
        let date = date |> Option.defaultValue DateTime.Today
        let! xmlElement = fetch ctx (getRequestContent "getPupils" [ "dateOfInterest", date.ToString("s") ])
        let students = parseStudents xmlElement
        let filteredStudents =
            match className with
            | Some className ->
                students
                |> List.filter (fun student -> CIString student.SchoolClass = CIString className)
            | None -> students
        return! Successful.OK filteredStudents next ctx
    }