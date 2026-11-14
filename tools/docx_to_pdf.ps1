# Exports the documentation DOCX to PDF with Microsoft Word (COM automation).
param(
    [string]$Docx = (Join-Path $PSScriptRoot "..\docs\BlochSphereSimulator_dokumentacio.docx")
)
$Docx = (Resolve-Path $Docx).Path
$Pdf = [IO.Path]::ChangeExtension($Docx, ".pdf")
$word = New-Object -ComObject Word.Application
$word.Visible = $false
try {
    $d = $word.Documents.Open($Docx, $false, $true)
    $d.Fields.Update() | Out-Null
    $d.ExportAsFixedFormat($Pdf, 17)   # 17 = wdExportFormatPDF
    "Pages: " + $d.ComputeStatistics(2)  # 2 = wdStatisticPages
    $d.Close($false)
    "Saved $Pdf"
}
finally {
    $word.Quit()
}
