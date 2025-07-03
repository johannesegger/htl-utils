Connect-MgGraph

$Group = Get-MgGroup -Filter "DisplayName eq 'GrpSchueler'"
$Members = Get-MgGroupMemberAsUser -GroupId $Group.Id -All -Property Department, Surname, GivenName, Mail, extension_0b429365529a4f1ea9337bdcd9346b84_htlvbSokratesId `
    | Foreach-Object {
        [PSCustomObject]@{ ClassName = $_.Department; LastName = $_.Surname; FirstName = $_.GivenName; Mail = $_.Mail; SokratesId = $_.AdditionalProperties.extension_0b429365529a4f1ea9337bdcd9346b84_htlvbSokratesId }
    } `
    | Sort-Object LastName, FirstName
$Members | Export-Csv "students.csv" -NoTypeInformation -Encoding UTF8
