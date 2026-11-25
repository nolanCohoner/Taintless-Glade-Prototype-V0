# Lance Unity en ligne de commande (tests ou build Windows) sur le runner auto-hebergé.
# Usage : unity.ps1 -Action test -Mode EditMode|PlayMode
#         unity.ps1 -Action build -Version 1.2.3
# Unity est cherché dans le dossier Unity Hub, a la version de ProjectSettings/ProjectVersion.txt.
# Pour un autre emplacement, definis la variable d'environnement UNITY_PATH sur le runner.
param(
    [Parameter(Mandatory = $true)][ValidateSet('test', 'build')][string]$Action,
    [ValidateSet('EditMode', 'PlayMode')][string]$Mode = 'EditMode',
    [string]$Version = '',
    [string]$Out = 'ci-output'
)

$ErrorActionPreference = 'Stop'

$projectVersion = (Select-String -Path 'ProjectSettings/ProjectVersion.txt' -Pattern 'm_EditorVersion: (\S+)').Matches[0].Groups[1].Value
if ($env:UNITY_PATH) { $unity = $env:UNITY_PATH }
else { $unity = "C:\Program Files\Unity\Hub\Editor\$projectVersion\Editor\Unity.exe" }
if (-not (Test-Path $unity)) {
    throw "Unity $projectVersion introuvable : $unity. Installe cette version via Unity Hub ou definis UNITY_PATH sur le runner."
}

New-Item -ItemType Directory -Force -Path $Out | Out-Null
$outFull = (Resolve-Path $Out).Path
$project = (Get-Location).Path
$log = Join-Path $outFull "unity-$Action-$Mode.log"

function Invoke-Unity([string[]]$UnityArgs) {
    Write-Host "Unity $projectVersion : $($UnityArgs -join ' ')"
    # Unity.exe est une application graphique : il faut Start-Process -Wait pour attendre sa fin.
    $process = Start-Process -FilePath $unity -ArgumentList $UnityArgs -Wait -PassThru -NoNewWindow
    return $process.ExitCode
}

function Write-Summary([string]$Text) {
    Write-Host $Text
    if ($env:GITHUB_STEP_SUMMARY) { Add-Content -Path $env:GITHUB_STEP_SUMMARY -Value $Text }
}

if ($Action -eq 'test') {
    $results = Join-Path $outFull "results-$Mode.xml"
    $unityArgs = @('-batchmode', '-projectPath', $project, '-runTests', '-testPlatform', $Mode, '-testResults', $results, '-logFile', $log)
    if ($Mode -eq 'EditMode') { $unityArgs += '-nographics' }
    $code = Invoke-Unity $unityArgs

    if (Test-Path $results) {
        [xml]$xml = Get-Content $results
        $run = $xml.'test-run'
        Write-Summary "### Tests Unity $Mode : $($run.result)"
        Write-Summary "Total $($run.total) - reussis $($run.passed) - echoues $($run.failed) - ignores $($run.skipped)"
    }
    if ($code -ne 0) {
        Write-Host "---- fin du journal Unity ----"
        if (Test-Path $log) { Get-Content $log -Tail 60 }
        throw "Les tests $Mode ont echoue (code de sortie $code)."
    }
    exit 0
}

# Action build : TaintlessGlade.exe + ses donnees, puis archive zip.
if ([string]::IsNullOrEmpty($Version)) { $Version = '0.0.0' }
$buildDir = Join-Path $outFull 'TaintlessGlade'
if (Test-Path $buildDir) { Remove-Item -Recurse -Force $buildDir }

$unityArgs = @('-quit', '-batchmode', '-nographics', '-projectPath', $project,
    '-executeMethod', 'TaintlessGlade.EditorTools.BuildScript.BuildWindows',
    '-buildOutput', $buildDir, '-buildVersion', $Version, '-logFile', $log)
$code = Invoke-Unity $unityArgs
if ($code -ne 0 -or -not (Test-Path (Join-Path $buildDir 'TaintlessGlade.exe'))) {
    Write-Host "---- fin du journal Unity ----"
    if (Test-Path $log) { Get-Content $log -Tail 80 }
    throw "Le build Windows a echoue (code de sortie $code)."
}

$zip = Join-Path $outFull 'TaintlessGlade-windows-x64.zip'
if (Test-Path $zip) { Remove-Item -Force $zip }
Compress-Archive -Path (Join-Path $buildDir '*') -DestinationPath $zip
$sizeMb = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Summary "### Build Windows $Version : $sizeMb Mo"
Write-Summary "Archive : TaintlessGlade-windows-x64.zip"
