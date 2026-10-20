// Optional Docker-free local PostgreSQL. CI uses its own PostgreSQL service.
import { pathToFileURL, fileURLToPath } from 'node:url';
import path from 'node:path';
import { mkdir } from 'node:fs/promises';
const repo=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..');
const runtime=path.resolve(process.argv[2] || path.join(repo,'artifacts/pg-runtime'));
if(!process.env.ASE_DB_PASSWORD) throw new Error('ASE_DB_PASSWORD is required');
const data=path.resolve(repo,process.env.ASE_DB_DATA_DIR || 'artifacts/assessment-postgres');
await mkdir(path.dirname(data),{recursive:true});
const {default:EmbeddedPostgres}=await import(pathToFileURL(path.join(runtime,'node_modules/embedded-postgres/dist/index.js')));
const pg=new EmbeddedPostgres({databaseDir:data,user:'postgres',password:process.env.ASE_DB_PASSWORD,
  port:Number(process.env.ASE_DB_PORT || 15432),persistent:true,postgresFlags:['-h','127.0.0.1'],
  onLog:()=>{},onError:message=>console.error(String(message))});
await pg.initialise();
await pg.start();
const client=pg.getPgClient();await client.connect();
console.log((await client.query('SELECT version()')).rows[0].version);await client.end();
console.log('ASSESSMENT_POSTGRES_READY: loopback only');
process.on('SIGINT',async()=>{await pg.stop();process.exit(0);});
process.on('SIGTERM',async()=>{await pg.stop();process.exit(0);});
setInterval(()=>{},60000);
