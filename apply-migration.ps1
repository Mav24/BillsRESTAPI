# Apply migrations to production database
$env:ASPNETCORE_ENVIRONMENT = "Production"
dotnet ef database update --project src/BillsApi
