param (
	[string]$version = "latest",
	[string]$manifestPath = "./manifest.json",
	[string]$outputDir = "."
)

# Load the manifest
$manifest = Get-Content $manifestPath | ConvertFrom-Json

if ([string]::IsNullOrEmpty($version) -or $version -eq "latest") {
	$version = $manifest.latest
}

# Check if the specified version exists in the manifest
if (-not ($manifest.versions.PSObject.Properties.Name -contains $version)) {
	Write-Error "Specified version '${version}' does not exist in the manifest."
	exit 1
}

# Get the files for the specified version
$files = $manifest.versions.$version.files

Write-Host "Downloading $($files.Count) files for ${version}:" -ForegroundColor White

foreach ($file in $files) {
	$name = $file.name
	$fileUrl = $file.url
	$fileChecksum = $file.checksum
	$rid = $file.rid

	# Where should we store the file ?
	$fileTargetDir = Join-Path -Path $outputDir -ChildPath (Join-Path -Path "runtimes" -ChildPath (Join-Path -Path $rid -ChildPath "native"))
	$fileTargetPath = Join-Path -Path $fileTargetDir -ChildPath $name

	# Ensure the path exists
	if (-not (Test-Path -Path $fileTargetDir)) {
		New-Item -ItemType Directory -Path $fileTargetDir | Out-Null
	}

	Write-Host ""
	Write-Host "- ${name} (${rid})" -ForegroundColor Yellow
	Write-Host "  - target   : " -ForegroundColor DarkGray -NoNewLine; Write-Host $fileTargetPath
	Write-Host "  - url      : " -ForegroundColor DarkGray -NoNewLine; Write-Host $fileUrl
	Write-Host "  - checksum : " -ForegroundColor DarkGray -NoNewLine; Write-Host $fileChecksum.ToLower()

	# If we already have the file (with the correct checksum), skip it
	if (Test-Path -Path $fileTargetPath) {
		$computedChecksum = Get-FileHash $fileTargetPath -Algorithm SHA256 | Select-Object -ExpandProperty Hash
		Write-Host "  - actual   : " -ForegroundColor DarkGray -NoNewLine;
		Write-Host $computedChecksum.ToLower()
		if ($fileChecksum -eq $computedChecksum) {
			Write-Host "=> CACHED" -ForegroundColor Green
			continue
		} else {
			Write-Host "File $fileTargetPath exists but has an incorrect checksum. Re-downloading." -ForegroundColor Red
		}
	}

	# Download the file
	Invoke-WebRequest -Uri $fileUrl -OutFile $fileTargetPath

	# Verify the checksum
	$computedChecksum = Get-FileHash $fileTargetPath -Algorithm SHA256 | Select-Object -ExpandProperty Hash

	Write-Host "  - actual   : " -ForegroundColor DarkGray -NoNewLine;
	Write-Host $computedChecksum.ToLower()

	if ($fileChecksum -ne $computedChecksum) {
		Write-Error "Checksum verification failed for ${fileTargetPath}!"
		exit 1
	}

	Write-Host "=> OK" -ForegroundColor Green

}

Write-Host ""
Write-Host "Download complete." -ForegroundColor Green
