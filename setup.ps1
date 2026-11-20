# Run once from the EventHub folder:  powershell -ExecutionPolicy Bypass -File .\setup.ps1
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

$sdk = (dotnet --version).Trim()
$major = [int]$sdk.Split('.')[0]
Write-Host "Using .NET SDK $sdk (target net$major.0)"

# 1. Match the target framework to the installed SDK
$props = Get-Content Directory.Build.props -Raw
$props = $props -replace '<TargetFramework>net\d+\.0</TargetFramework>', "<TargetFramework>net$major.0</TargetFramework>"
Set-Content Directory.Build.props $props

# 2. Solution file (classic .sln so Visual Studio and VS Code both open it)
if (-not (Test-Path EventHub.sln)) {
    if ($major -ge 10) { dotnet new sln -n EventHub --format sln } else { dotnet new sln -n EventHub }
}

# 3. MVC apps and test project (class libraries are already in the repo)
if (-not (Test-Path EventHub.Web/EventHub.Web.csproj)) { dotnet new mvc   -n EventHub.Web -o EventHub.Web --no-restore }
if (-not (Test-Path SupplierSystem.Web/SupplierSystem.Web.csproj))   { dotnet new mvc   -n SupplierSystem.Web  -o SupplierSystem.Web  --no-restore }
if (-not (Test-Path EventHub.Tests/EventHub.Tests.csproj))   { dotnet new xunit -n EventHub.Tests  -o EventHub.Tests  --no-restore }

# 4. Add projects to the solution
dotnet sln EventHub.sln add EventHub.Contracts/EventHub.Contracts.csproj EventHub.Messaging/EventHub.Messaging.csproj `
    EventHub.Web/EventHub.Web.csproj SupplierSystem.Web/SupplierSystem.Web.csproj EventHub.Tests/EventHub.Tests.csproj

# 5. Project references
dotnet add EventHub.Web reference EventHub.Contracts EventHub.Messaging
dotnet add SupplierSystem.Web  reference EventHub.Contracts EventHub.Messaging
dotnet add EventHub.Tests  reference EventHub.Contracts EventHub.Messaging

# 6. NuGet packages (latest compatible with your SDK)
foreach ($p in "EventHub.Web", "SupplierSystem.Web") {
    dotnet add $p package Microsoft.EntityFrameworkCore.SqlServer --version "$major.*"
    dotnet add $p package Microsoft.EntityFrameworkCore.Design --version "$major.*"
    dotnet add $p package Microsoft.AspNetCore.Identity.EntityFrameworkCore --version "$major.*"
    dotnet add $p package Serilog.AspNetCore
}
dotnet add EventHub.Tests package Moq

# 7. Fixed ports so the two apps never clash
function Set-Ports($proj, $http, $https) {
    $path = "$proj/Properties/launchSettings.json"
    $json = Get-Content $path -Raw | ConvertFrom-Json
    if ($json.profiles.http)  { $json.profiles.http.applicationUrl  = "http://localhost:$http" }
    if ($json.profiles.https) { $json.profiles.https.applicationUrl = "https://localhost:$https;http://localhost:$http" }
    $json | ConvertTo-Json -Depth 10 | Set-Content $path
}
Set-Ports "EventHub.Web" 5001 7001
Set-Ports "SupplierSystem.Web"  5002 7002

# 8. Tools and infrastructure
dotnet tool update --global dotnet-ef 2>$null
if (-not (Test-Path .git)) { git init | Out-Null }
docker compose up -d

dotnet build EventHub.sln
Write-Host "`nDone. RabbitMQ UI: http://localhost:15672 (eventhub / eventhub_pw)"
