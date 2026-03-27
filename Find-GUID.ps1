# Find-GUID.ps1
Write-Host "Modalità ricerca GUID attivata. Digita 'exit' per uscire.`n"

# Funzione per cercare il file con quel GUID
function Find-GuidInProject {
    param ([string]$guid)

    $matches = Get-ChildItem -Recurse -Filter "*.meta" | Select-String -Pattern "guid: $guid" | Select-Object -ExpandProperty Path

    if ($matches.Count -eq 0) {
        Write-Host "❌ Nessun file trovato per il GUID: $guid" -ForegroundColor Red
    } else {
        foreach ($path in $matches) {
            $assetPath = $path -replace '\.meta$', ''
            $relativePath = Resolve-Path -Relative $assetPath
            Write-Host "✅ Trovato: $relativePath"
        }
    }
}

# Loop interattivo
while ($true) {
    $inputGuid = Read-Host "Inserisci GUID"
    if ($inputGuid -eq "exit") {
        Write-Host "Uscita dalla modalità ricerca GUID." -ForegroundColor Yellow
        break
    }

    Find-GuidInProject $inputGuid
}
