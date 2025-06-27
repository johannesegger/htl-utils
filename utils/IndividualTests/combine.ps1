$inputFiles = Get-ChildItem .\out\students\*.pdf
pdftk $inputFiles cat output ".\out\Prüfungseinteilung Schüler.pdf"

$inputFiles = Get-ChildItem .\out\teachers\*.pdf
pdftk $inputFiles cat output ".\out\Prüfungseinteilung Lehrer.pdf"