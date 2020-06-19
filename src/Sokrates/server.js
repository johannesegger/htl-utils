var soap = require("soap");
var fs = require("fs");
var http = require("http");
var moment = require("moment");

let clientCertFile = fs.readFileSync(process.env.SOKRATES_CLIENT_CERTIFICATE_PATH);
let clientCertPassphrase = process.env.SOKRATES_CLIENT_CERTIFICATE_PASSPHRASE;

let port = process.argv[2];
if (!port) {
    throw "ERROR: undefined port"
}

http
    .createServer(async (req, res) => {
        if (req.url == "/api/teachers")
        {
            await tryGet(res, getTeachers);
            return;
        }

        let getStudentsParams = /^\/api\/students(\/(?<date>\d{4}-\d{2}-\d{2}))?$/.exec(req.url);
        if (getStudentsParams)
        {
            let date;
            if (getStudentsParams.groups.date)
            {
                date = moment(getStudentsParams.groups.date, "YYYY-MM-DD");
                if (!date.isValid())
                {
                    res.statusCode = 400;
                    res.write(`Invalid date: ${getStudentsParams.groups.date}`);
                    res.end();
                    return;
                }
            }
            else
            {
                date = moment().startOf("day");
            }
            await tryGet(res, () => getStudents(date));
            return;
        }

        let getStudentsOfClassParams = /^\/api\/classes\/(?<className>\w+)\/students(\/(?<date>\d{4}-\d{2}-\d{2}))?$/.exec(req.url);
        if (getStudentsOfClassParams)
        {
            let date;
            if (getStudentsOfClassParams.groups.date)
            {
                date = moment(getStudentsOfClassParams.groups.date, "YYYY-MM-DD");
                if (!date.isValid())
                {
                    res.statusCode = 400;
                    res.write(`Invalid date: ${getStudentsOfClassParams.groups.date}`);
                    res.end();
                    return;
                }
            }
            else
            {
                date = moment().startOf("day");
            }
            await tryGet(res, () => getStudentsOfClass(getStudentsOfClassParams.groups.className, date));
            return;
        }

        let getClassesParams = /^\/api\/classes(\/(?<schoolYear>\d{4}))?$/.exec(req.url);
        if (getClassesParams)
        {
            let schoolYear;
            if (getClassesParams.groups.schoolYear)
            {
                schoolYear = parseInt(getClassesParams.groups.schoolYear);
            }
            else
            {
                let now = moment();
                schoolYear = now.month() < 8 ? now.year() - 1 : now.year()
            }
            await tryGet(res, () => getClasses(schoolYear));
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

let sokratesStudentToDto = student => {
    return {
        id: student.pupil.sokratesID,
        lastName: student.pupil.lastName,
        firstName1: student.pupil.firstName1,
        firstName2: student.pupil.firstName2,
        dateOfBirth: moment(student.pupil.dateOfBirth, "YYYY-MM-DDZ").format("YYYY-MM-DD"),
        schoolClass: student.pupil.schoolClass.replace(/_(WS|SS)$/, "")
    };
};

let getStudents = async date =>
{
    let soapClient = await createSoapClient();
    let [result, rawResponse, soapHeader, rawRequest] = await soapClient.getPupilsAsync({ schoolID: schoolId, dateOfInterest: date.format() });
    return result.return.lstPupils.pupilEntry.map(sokratesStudentToDto);
};

let getStudentsOfClass = async (className, date) =>
{
    let soapClient = await createSoapClient();
    let [result, rawResponse, soapHeader, rawRequest] = await soapClient.getPupilsAsync({ schoolID: schoolId, dateOfInterest: date.format() });
    return result.return.lstPupils.pupilEntry
        .map(sokratesStudentToDto)
        .filter(student => student.schoolClass == className);
};

let getStudentContacts = async (personIds, date) =>
{
    let soapClient = await createSoapClient();
    let [result, rawResponse, soapHeader, rawRequest] = await soapClient.getContactInfosAsync({ schoolID: schoolId, personIDs: { personIDEntry: personIds }, dateOfInterest: date.format() });
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
};

let parsePhoneNumber = v =>
{
    v = v.replace(/[\/\s]/g, "");
    let prefix = parseInt(v.substring(0, 4));
    // see https://de.wikipedia.org/wiki/Telefonvorwahl_(%C3%96sterreich)#Mobilfunk
    if ((prefix >= 650 && prefix <= 653) ||
        (prefix == 655) || (prefix == 657) ||
        (prefix >= 659 && prefix <= 661) ||
        (prefix >= 663 && prefix <= 699))
    {
        return {
            type: "mobile",
            number: v
        }
    }
    return {
        type: "home",
        number: v
    }
};

let addPhoneNumber = (list, phoneNumber) =>
{
    let newList = Object.assign({ [phoneNumber.type]: [] }, list);
    newList[phoneNumber.type].push(phoneNumber.number);
    return newList;
}

let getTeachers = async () =>
{
    let soapClient = await createSoapClient();
    let [result, rawResponse, soapHeader, rawRequest] = await soapClient.getTeacherAsync({ schoolID: schoolId });
    return result.return.lstTeacher.teacherEntry
        .filter(teacher => teacher.teacher.token)
        .map(teacher => {
            let phones, address;
            if (teacher.addressHome)
            {
                if (teacher.addressHome.phone1 ||
                    teacher.addressHome.phone2)
                {
                    phones = [teacher.addressHome.phone1, teacher.addressHome.phone2]
                        .filter(v => v)
                        .map(parsePhoneNumber)
                        .reduce(addPhoneNumber, {});
                }

                if (teacher.addressHome.country ||
                    teacher.addressHome.plz ||
                    teacher.addressHome.city ||
                    teacher.addressHome.street ||
                    teacher.addressHome.streetNumber)
                {
                    address = {
                        country: teacher.addressHome.country,
                        zip: teacher.addressHome.plz,
                        city: teacher.addressHome.city,
                        street: teacher.addressHome.streetNumber
                            ? `${teacher.addressHome.street} ${teacher.addressHome.streetNumber}`
                            : teacher.addressHome.street
                    };
                }
            }
            return {
                id: teacher.teacher.personID,
                title: teacher.teacher.title,
                lastName: teacher.teacher.lastName,
                firstName: teacher.teacher.firstName,
                shortName: teacher.teacher.token,
                dateOfBirth: teacher.teacher.dateOfBirth.replace(/\+.*$/, ""),
                degreeFront: teacher.teacher.degree,
                degreeBack: teacher.teacher.degree2,
                phones: phones,
                address: address
            };
        });
};

let getClasses = async schoolYear =>
{
    let soapClient = await createSoapClient();
    let [result, rawResponse, soapHeader, rawRequest] = await soapClient.getTSNClassesAsync({ schoolID: schoolId, schoolYear: schoolYear });
    let list = result.return.lstTSNClasses.tsnClassEntry
        .filter(schoolClass => !schoolClass.className.startsWith("AP_")) // looks like final classes
        .map(schoolClass => schoolClass.className.replace(/_(WS|SS)$/, ""));
    return [...new Set(list)];
};
