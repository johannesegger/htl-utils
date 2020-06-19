module ServiceUrl

let private url serviceName path =
    sprintf "http://localhost:%s/v1.0/invoke/%s/method/api/%s" (Environment.getEnvVarOrFail "DAPR_HTTP_PORT") serviceName path

let aad = url "aad"
let fileStorage = url "file-storage"
let finalTheses = url "final-theses"
let photoLibrary = url "photo-library"
let sokrates = url "sokrates"
let untis = url "untis"
let wakeUpComputer = url "wake-up-computer"
