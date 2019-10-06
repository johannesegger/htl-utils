var soap = require("soap");
var fs = require("fs");
var http = require("http");
var moment = require("moment");

let clientCertFile = fs.readFileSync(process.env.CLIENT_CERTIFICATE_PATH);
let clientCertPassphrase = process.env.CLIENT_CERTIFICATE_PASSPHRASE;

let port = process.env.PORT || 80;

http
    .createServer(async (req, res) => {
        if (req.url == "/api/teachers")
        {
            await tryGet(res, getTeachers);
            return;
        }

        let getStudentsParams = /^\/api\/students\/(?<date>\d{4}-\d{2}-\d{2})$/.exec(req.url);
        if (getStudentsParams)
        {
            let date = moment(getStudentsParams.groups.date, "YYYY-MM-DD");
            if (!date.isValid())
            {
                res.statusCode = 400;
                res.write(`Invalid date: ${getStudentsParams.groups.date}`);
                res.end();
                return;
            }
            await tryGet(res, () => getStudents(date));
            return;
        }

        // if (true)
        // {
        //     await tryGet(res, () => getStudentContacts(["41742720190069", "41742720190001", "952485"], moment().startOf("day")));
        // }

        res.statusCode = 404;
        res.end();
    })
    .listen(port, () => { console.log(`Server is listening on port ${port}`); });

let tryGet = async (res, fn) =>
{
    try
    {
        let data = await fn();
        res.statusCode = 200;
        res.setHeader("Content-Type", "application/json; charset=utf-8");
        res.write(JSON.stringify(data));
    }
    catch (e)
    {
        res.statusCode = 500;
        res.setHeader("Content-Type", "text/plain; charset=utf-8");
        res.write(`${e}`);
    }
    res.end();
};

let createSoapClient = async () =>
{
    let soapClient = await soap.createClientAsync(
        "https://www.sokrates-bund.at/BRZPRODWS/ws/dataexchange?wsdl",
        {
            wsdl_options:
            {
                pfx: clientCertFile,
                passphrase: clientCertPassphrase
            }
        }
    );
    // console.log("Service methods: ", JSON.stringify(soapClient.describe(), null, 4));
    soapClient.setSecurity(new soap.ClientSSLSecurityPFX(
        clientCertFile,
        clientCertPassphrase
    ));

    let username = "*****";
    let password = "*****";
    soapClient.addSoapHeader(`<UsernameToken xmlns="http://wservices.sokrateslfs.siemens.at/"><username xmlns="">${username}</username><password xmlns="">${password}</password></UsernameToken>`);

    return soapClient;
};

const schoolId = "*****";

let getStudents = async date =>
{
    let soapClient = await createSoapClient();
    let [result, rawResponse, soapheader, rawRequest] = await soapClient.getPupilsAsync({ schoolID: schoolId, dateOfInterest: date.format() });
    return result.return.lstPupils.pupilEntry.map(student => (
        {
            id: student.pupil.sokratesID,
            lastName: student.pupil.lastName,
            firstName1: student.pupil.firstName1,
            firstName2: student.pupil.firstName2,
            dateOfBirth: moment(student.pupil.dateOfBirth, "YYYY-MM-DDZ").format("YYYY-MM-DD"),
            schoolClass: student.pupil.schoolClass
        })
    );
};

let getStudentContacts = async (personIds, date) =>
{
    let soapClient = await createSoapClient();
    let [result, rawResponse, soapheader, rawRequest] = await soapClient.getContactInfosAsync({ schoolID: schoolId, personIDs: { personIDEntry: personIds }, dateOfInterest: date.format() });
    return result.return.lstContactInfo.contactEntry.map(contact => (
        {
            id: contact.personID,
            addressType: contact.address.type,
            name: contact.address.lastName,
            country: contact.address.country,
            zip: contact.address.plz,
            city: contact.address.city,
            street: contact.address.street,
            streetNumber: contact.address.streetNumber
        }
    ));
}

let getTeachers = async () =>
{
    let soapClient = await createSoapClient();
    let [result, rawResponse, soapheader, rawRequest] = await soapClient.getTeacherAsync({ schoolID: schoolId });
    return result.return.lstTeacher.teacherEntry.map(teacher => (
        {
            id: teacher.teacher.personID,
            title: teacher.teacher.title,
            lastName: teacher.teacher.lastName,
            firstName: teacher.teacher.firstName,
            shortName: teacher.teacher.token,
            dateOfBirth: moment(teacher.teacher.dateOfBirth, "YYYY-MM-DDZ").format("YYYY-MM-DD"),
            degreeFront: teacher.teacher.degree,
            degreeBack: teacher.teacher.degree2
        })
    );
};