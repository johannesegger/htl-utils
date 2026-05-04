$Config = dotnet user-secrets list --id htl-utils --json | ConvertFrom-Json

$ClientCertificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($Config.'Sokrates:ClientCertificatePath', $Config.'Sokrates:ClientCertificatePassphrase')
Invoke-WebRequest -Certificate $ClientCertificate -Uri "$($Config.'Sokrates:WebServiceUrl')?xsd=1" -OutFile ./src/Sokrates/sokrates.xsd
