param([switch]$SkipBuild, [string]$Image = 'fleetflow-s4-review:9a12658')
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if($Image -notmatch '^fleetflow-s4-review:[a-z0-9-]+$'){throw 'Unexpected rehearsal image.'}
$stamp=[Guid]::NewGuid().ToString('N').Substring(0,12)
$project='fleetflow-s411-'+$stamp
$directory=Join-Path $repo ('ReviewResults/'+$stamp)
New-Item -ItemType Directory -Path $directory -Force|Out-Null
$compose=Join-Path $directory 'compose.json';$toolEnv=Join-Path $directory 'tools.env'
$started=$false;$success=$false
function Secret { [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32)) }
$password=Secret;$jwt=Secret;$internal=Secret;$fleetSecret=Secret;$rentalSecret=Secret;$notificationSecret=Secret
$base=@{ASPNETCORE_ENVIRONMENT='Development';DB_HOST='postgres';DB_PORT='5432';DB_USER='postgres';DB_PASSWORD=$password;Jwt__Key=$jwt;Jwt__Issuer='FleetFlow.IdentityService';Jwt__Audience='FleetFlow.Client';SERVICE_AUTH_ENABLED='true';SERVICE_AUTH_SIGNING_KEY=$internal;INTERNAL_IDENTITY_URL='http://identity-service:8080/';Kafka__Enabled='false'}
$services=@{postgres=@{image='postgres:15-alpine';environment=@{POSTGRES_PASSWORD=$password};volumes=@('review-db:/var/lib/postgresql/data');healthcheck=@{test=@('CMD-SHELL','pg_isready -U postgres');interval='1s';timeout='3s';retries=30}}}
foreach($item in @(@('identity-service','IdentityService',15101),@('fleet-service','FleetService',15102),@('rental-service','RentalService',15103),@('notification-service','NotificationService',15104))){
    $name=$item[0];$service=$item[1];$port=$item[2];$envs=$base.Clone()
    if($name -eq 'identity-service'){$envs.AUTH_DB_NAME='fleetflow_auth';$envs.SERVICE_AUTH_FLEET_SECRET=$fleetSecret;$envs.SERVICE_AUTH_RENTAL_SECRET=$rentalSecret;$envs.SERVICE_AUTH_NOTIFICATION_SECRET=$notificationSecret}
    if($name -eq 'fleet-service'){$envs.FLEET_DB_NAME='fleetflow_fleet';$envs.SERVICE_AUTH_FLEET_SECRET=$fleetSecret;$envs.RENTAL_OWNERSHIP='Legacy';$envs.NOTIFICATION_OWNERSHIP='Legacy';$envs.INTERNAL_RENTAL_URL='http://rental-service:8080/';$envs.INTERNAL_NOTIFICATION_URL='http://notification-service:8080/'}
    if($name -eq 'rental-service'){$envs.RENTAL_DB_NAME='fleetflow_rental';$envs.SERVICE_AUTH_RENTAL_SECRET=$rentalSecret;$envs.RENTAL_SERVICE_ACTIVE='false';$envs.INTERNAL_FLEET_URL='http://fleet-service:8080/';$envs.INTERNAL_NOTIFICATION_URL='http://notification-service:8080/'}
    if($name -eq 'notification-service'){$envs.NOTIFICATION_DB_NAME='fleetflow_notifications';$envs.SERVICE_AUTH_NOTIFICATION_SECRET=$notificationSecret;$envs.NOTIFICATION_SERVICE_ACTIVE='false'}
    $owned=@{'identity-service'='fleetflow_auth';'fleet-service'='fleetflow_fleet';'rental-service'='fleetflow_rental';'notification-service'='fleetflow_notifications'}[$name]
    # This PostgreSQL exists only on the disposable private Docker network.
    # Explicit connection settings avoid treating its "postgres" hostname as Azure/TLS.
    $envs.ConnectionStrings__DefaultConnection="Host=postgres;Port=5432;Database=$owned;Username=postgres;Password=$password;Ssl Mode=Disable;Timeout=5;Command Timeout=30"
    $services[$name]=@{image=$Image;command=@('dotnet',"/review/$service/$service.Api.dll");working_dir="/review/$service";ports=@("127.0.0.1:${port}:8080");environment=$envs;depends_on=@{postgres=@{condition='service_healthy'}}}
}
$config=@{services=$services;volumes=@{'review-db'=@{}}}
function WriteCompose { $config|ConvertTo-Json -Depth 12|Set-Content -LiteralPath $compose -Encoding utf8 }
function Compose([string[]]$arguments){& docker compose -p $project -f $compose @arguments;if($LASTEXITCODE -ne 0){throw 'Disposable Compose operation failed.'}}
function Sql($database,$sql){$value=& docker exec $script:pg psql -U postgres -d $database -v ON_ERROR_STOP=1 -At -c $sql;if($LASTEXITCODE -ne 0){throw 'Disposable SQL operation failed.'};return $value}
function Ready($port){for($i=0;$i -lt 60;$i++){try{$response=Invoke-WebRequest "http://127.0.0.1:$port/health/ready" -SkipHttpErrorCheck -TimeoutSec 6;if($response.StatusCode -eq 200){return}}catch{};Start-Sleep -Milliseconds 200};throw "Rehearsal service on $port is not ready."}
function Fingerprint($database){$tables=Sql $database "SELECT tablename FROM pg_tables WHERE schemaname='public' ORDER BY tablename";$rows=@{};foreach($table in $tables){if($table -notmatch '^[A-Za-z0-9_]+$'){throw 'Unexpected table identifier.'};$rows[$table]=Sql $database "SELECT count(*)::text||':'||coalesce(md5(string_agg(row_to_json(t)::text,',' ORDER BY row_to_json(t)::text)),'empty') FROM `"$table`" t"};return ($rows|ConvertTo-Json -Compress)}
$backup=@{verified=$false;databases=@{};uploads=@{}}
function Backup($database){
    if($database -notin @('fleetflow_auth','fleetflow_fleet','fleetflow_rental','fleetflow_notifications')){throw 'Unexpected owned test database.'}
    $before=Fingerprint $database;$name=$database+'.dump'
    & docker exec $script:pg pg_dump -U postgres -Fc -f ('/tmp/'+$name) $database;if($LASTEXITCODE -ne 0){throw 'Dump failed.'}
    & docker cp "${script:pg}:/tmp/$name" (Join-Path $directory $name);if($LASTEXITCODE -ne 0){throw 'Dump copy failed.'}
    $verify='s411_verify_'+$database
    $null=Sql postgres "CREATE DATABASE $verify"
    & docker exec $script:pg pg_restore -U postgres --exit-on-error -d $verify ('/tmp/'+$name);if($LASTEXITCODE -ne 0){throw 'Isolated restore failed.'}
    if((Fingerprint $verify) -ne $before){throw 'Restored row fingerprints differ.'}
    $null=Sql postgres "DROP DATABASE $verify WITH (FORCE)"
    $backup.databases[$database]=@{dump='/evidence/'+$name;sha256=(Get-FileHash (Join-Path $directory $name)).Hash.ToLowerInvariant();restoredAndVerified=$true}
}
function Tool($name,[string[]]$arguments){
    $result=& docker run --rm --network "container:$script:pg" --env-file $toolEnv --mount "type=bind,source=$directory,target=/evidence" $Image dotnet "/review/$name/$name.dll" @arguments
    if($LASTEXITCODE -ne 0){throw 'Guarded migration failed.'};return ($result|ConvertFrom-Json)
}
try{
    foreach($port in @(15101,15102,15103,15104)){$probe=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,$port);$probe.Start();$probe.Stop()}
    if(-not $SkipBuild){& docker build -f (Join-Path $PSScriptRoot 'Dockerfile') -t $Image $repo;if($LASTEXITCODE -ne 0){throw 'Linux rehearsal build failed.'}}
    WriteCompose;Compose @('up','-d','postgres');$started=$true
    $script:pg=(& docker compose -p $project -f $compose ps -q postgres).Trim();if(-not $script:pg){throw 'Disposable PostgreSQL container missing.'}
    for($i=0;$i -lt 30;$i++){& docker exec $script:pg pg_isready -U postgres *> $null;if($LASTEXITCODE -eq 0){break};Start-Sleep -Milliseconds 200}
    foreach($database in @('fleetflow_auth','fleetflow_fleet')){$null=Sql postgres "CREATE DATABASE $database"}
    Compose @('up','-d','identity-service','fleet-service');Ready 15101;Ready 15102
    Compose @('stop','identity-service','fleet-service') # Explicit write pause in this disposable stack.
    Backup 'fleetflow_auth';Backup 'fleetflow_fleet'
    $uploads=Join-Path $directory 'uploads';$restored=Join-Path $directory 'restored-uploads';New-Item -ItemType Directory -Path $uploads,$restored -Force|Out-Null
    & tar -cf (Join-Path $directory 'uploads.tar') -C $uploads .;if($LASTEXITCODE -ne 0){throw 'Uploads archive failed.'}
    & tar -xf (Join-Path $directory 'uploads.tar') -C $restored;if($LASTEXITCODE -ne 0 -or @(Get-ChildItem $restored -Recurse -File).Count -ne 0){throw 'Fresh empty uploads restore differs.'}
    $backup.uploads=@{archive='/evidence/uploads.tar';sha256=(Get-FileHash (Join-Path $directory 'uploads.tar')).Hash.ToLowerInvariant();verified=$true};$backup.verified=$true
    $backup|ConvertTo-Json -Depth 8|Set-Content (Join-Path $directory 'backup.json') -Encoding utf8
    @('DB_HOST=127.0.0.1','DB_USER=postgres',"DB_PASSWORD=$password")|Set-Content -LiteralPath $toolEnv -Encoding utf8
    $notificationExport=Tool 'NotificationMigration' @('--mode','export','--file','/evidence/inbox.json')
    $notificationImport=Tool 'NotificationMigration' @('--mode','import','--file','/evidence/inbox.json','--sha256',$notificationExport.sha256,'--backup','/evidence/backup.json','--write-pause','confirmed','--apply','true')
    $rentalExport=Tool 'RentalMigration' @('--mode','export','--file','/evidence/bookings.json')
    $rentalImport=Tool 'RentalMigration' @('--mode','import','--file','/evidence/bookings.json','--sha256',$rentalExport.sha256,'--backup','/evidence/backup.json','--write-pause','confirmed','--apply','true')
    Backup 'fleetflow_rental'
    $backup|ConvertTo-Json -Depth 8|Set-Content (Join-Path $directory 'backup.json') -Encoding utf8
    $target=Tool 'RentalMigration' @('--mode','export-target','--file','/evidence/rental-target.json')
    $upgrade=Tool 'RentalMigration' @('--mode','upgrade-rentals','--file','/evidence/rental-target.json','--sha256',$target.sha256,'--backup','/evidence/backup.json','--write-pause','confirmed','--apply','true')
    foreach($name in @('fleet-service','rental-service')){$services[$name].environment.RENTAL_MIGRATION_ID=$rentalImport.MigrationId}
    $services['fleet-service'].environment.RENTAL_OWNERSHIP='Remote';$services['fleet-service'].environment.NOTIFICATION_OWNERSHIP='Remote'
    $services['rental-service'].environment.RENTAL_SERVICE_ACTIVE='true';$services['notification-service'].environment.NOTIFICATION_SERVICE_ACTIVE='true';$services['notification-service'].environment.NOTIFICATION_MIGRATION_ID=$notificationImport.MigrationId
    WriteCompose;Compose @('up','-d','identity-service','fleet-service','rental-service','notification-service');foreach($port in @(15101,15102,15103,15104)){Ready $port}
    @{project=$project;image=$Image;baseCommit=(& git -C $repo rev-parse HEAD);freshDisposableDatabases=$true;verifiedRestores=$true;importedBookings=$rentalImport.RecordCount;importedNotifications=$notificationImport.RecordCount;schemaVersion=$upgrade.schemaVersion;host=@{os=(& docker info --format '{{.OSType}}');cpus=(& docker info --format '{{.NCPU}}');memoryBytes=(& docker info --format '{{.MemTotal}}')}}|ConvertTo-Json -Depth 6|Set-Content (Join-Path $directory 'environment.json') -Encoding utf8
    & node (Join-Path $PSScriptRoot 'verify.mjs') $directory $project $compose
    if($LASTEXITCODE -ne 0){throw 'HTTP rehearsal failed.'}
    $success=$true;Write-Output "Rehearsal evidence: $directory/result.json"
}finally{
    if($started){
        & docker compose -p $project -f $compose logs --no-color *> (Join-Path $directory 'service-logs.txt')
        $ids=@(& docker ps -aq --filter "label=com.docker.compose.project=$project")
        foreach($id in $ids){$label=& docker inspect --format '{{index .Config.Labels "com.docker.compose.project"}}' $id;if($label -ne $project -or $project -notmatch '^fleetflow-s411-[a-f0-9]{12}$'){throw 'Unsafe cleanup target.'}}
        Compose @('down','--volumes','--remove-orphans')
    }
    foreach($file in @($compose,$toolEnv)){if(Test-Path -LiteralPath $file){Remove-Item -LiteralPath $file}}
    @{successful=$success;ownedStackRemoved=$started;credentialsRemoved=$true;workingStackTouched=$false}|ConvertTo-Json|Set-Content (Join-Path $directory 'cleanup.json') -Encoding utf8
}
