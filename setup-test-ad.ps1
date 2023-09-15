# TODO manually download Windows Server image file and create new Hyper-V VM

Install-WindowsFeature -Name AD-Domain-Services -IncludeManagementTools -Restart:$false
Install-ADDSForest -DomainName htlvb.intern -SafeModeAdministratorPassword (ConvertTo-SecureString -AsPlainText "Admin123" -Force) -NoRebootOnCompletion -Force
Restart-Computer

Install-WindowsFeature -Name AD-Certificate -IncludeManagementTools -Restart:$false
Install-ADcsCertificationAuthority `
    -CAType EnterpriseRootCA `
    -CACommonName "htlvb-intern-CA" `
    -CADistinguishedNameSuffix "DC=htlvb,DC=intern" `
    -CryptoProviderName "RSA#Microsoft Software Key Storage Provider" `
    -KeyLength 2048 `
    -HashAlgorithmName SHA256 `
    -ValidityPeriod Years `
    -ValidityPeriodUnits 5 `
    -DatabaseDirectory "C:\Windows\system32\CertLog" `
    -LogDirectory "C:\Windows\system32\CertLog" `
    -Force

New-Item C:\Shared -Type Directory | Out-Null
New-SmbShare -Name data -Path C:\Shared -ChangeAccess BUILTIN\Users -FullAccess BUILTIN\Administrators | Out-Null

# TODO
# * Disable password complexity requirement: GPO -> Default Domain Policy -> Computer Configuration -> Windows Settings -> Security Settings -> Account Policy -> Password Policy -> Password must meet complexity requirements -> Disabled

Restart-Computer
