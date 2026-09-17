# Clear output directory
Remove-Item ./out -Recurse -Force -ErrorAction SilentlyContinue

# Publish to output directory
dotnet publish ./GameServer.Cli -c Release -o ./out

# Move to user bin
New-Item -ItemType Directory -Force -Path $env:USERPROFILE\.local\bin > $null
Move-Item ./out/* $env:USERPROFILE\.local\bin -Force

# Ensure local bin is in PATH
if (-not ([Environment]::GetEnvironmentVariable('PATH', 'User') -like "*$env:USERPROFILE\.local\bin*")) {
    [Environment]::SetEnvironmentVariable('PATH', "$env:USERPROFILE\.local\bin;" + [Environment]::GetEnvironmentVariable('PATH', 'User'), 'User')
    Write-Output "Added $env:USERPROFILE\.local\bin to User PATH"
}