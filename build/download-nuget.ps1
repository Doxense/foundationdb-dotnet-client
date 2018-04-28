$source = "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe"
$destination = ".\.nuget\nuget.exe"

$wc = New-Object System.Net.WebClient
$wc.DownloadFile($source, $destination)

