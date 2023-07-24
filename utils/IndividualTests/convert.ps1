foreach ($file in Get-ChildItem .\out\students\*.html) {
    $studentName = $file.BaseName
    $className = $studentName -replace "-.*",""
    "Converting letter for $studentName"
    $source = $file.FullName
    $target = "$($file.Directory.FullName)\pdf\$([IO.Path]::ChangeExtension($file.Name, ".pdf"))"
    mkdir (Split-Path -Parent $target) -Force | Out-Null
    $footerTemplateUri = ([Uri]([IO.Path]::GetFullPath("$PSScriptRoot\templates\student-letter\html-footer-template.html"))).AbsoluteUri
    $output = wkhtmltopdf.exe --margin-left 0 --margin-top 0 --margin-right 0 --footer-html "${footerTemplateUri}?class-name=$className" $source --print-media-type $target 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Conversion failed: $output"
    }
}

foreach ($file in Get-ChildItem .\out\teachers\*.html) {
    $teacherShortName = $file.BaseName
    "Converting letter for $teacherShortName"
    $source = $file.FullName
    $target = "$($file.Directory.FullName)\pdf\$([IO.Path]::ChangeExtension($file.Name, ".pdf"))"
    mkdir (Split-Path -Parent $target) -Force | Out-Null
    $headerTemplateUri = ([Uri]([IO.Path]::GetFullPath("$PSScriptRoot\templates\teacher-letter\html-header-template.html"))).AbsoluteUri
    $footerTemplateUri = "$PSScriptRoot/templates/teacher-letter/html-footer-template.html"
    $output = wkhtmltopdf.exe --orientation landscape --header-html "${headerTemplateUri}?teacher-name=$teacherShortName" --footer-html $footerTemplateUri $source --print-media-type $target 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Conversion failed: $output"
    }
}
