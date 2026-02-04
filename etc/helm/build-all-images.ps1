./build-image.ps1 -ProjectPath "../../src/OrderXChange.DbMigrator/OrderXChange.DbMigrator.csproj" -ImageName orderxchange/dbmigrator
./build-image.ps1 -ProjectPath "../../src/OrderXChange.HttpApi.Host/OrderXChange.HttpApi.Host.csproj" -ImageName orderxchange/httpapihost
./build-image.ps1 -ProjectPath "../../angular" -ImageName orderxchange/angular -ProjectType "angular"
