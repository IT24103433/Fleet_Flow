import { readFile } from 'node:fs/promises';
const file=process.argv[2];
if(!file) throw new Error('Pass the environment.json readiness file');
let ready=false;
for(let i=0;i<360;i++) {
  try { const metadata=JSON.parse(await readFile(file,'utf8')); if(metadata.readyUtc){ ready=true;break; } } catch { /* not ready yet */ }
  await new Promise(resolve=>setTimeout(resolve,500));
}
if(!ready) throw new Error('Application environment did not become ready within 180 seconds; inspect startup logs');
console.log('Real application environment is ready');
