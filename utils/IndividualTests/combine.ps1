$inputFiles = Get-ChildItem .\out\students\pdf\*.pdf
pdftk $inputFiles cat output ".\out\Prüfungseinteilung Schüler.pdf"

$inputFiles = Get-ChildItem .\out\teachers\pdf\*.pdf
pdftk $inputFiles cat output ".\out\Prüfungseinteilung Lehrer.pdf"