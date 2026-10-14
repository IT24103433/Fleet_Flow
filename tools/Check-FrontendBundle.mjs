import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const require = createRequire(import.meta.url);
const parser = require(path.join(repo, 'src/frontend/node_modules/@babel/parser'));
const dist = path.join(repo, 'src/frontend/dist');
const html = fs.readFileSync(path.join(dist, 'index.html'), 'utf8');
const entryPath = html.match(/src="([^"\s]+\.js)"/)?.[1];
if (!entryPath) throw new Error('Production entry script is missing. Build the frontend first.');
function closure(file, excluded = new Set(), files = new Map()) {
  if (!file.startsWith(dist + path.sep)) throw new Error('Static import escaped the production directory.');
  if (excluded.has(file) || files.has(file)) return files;
  const bytes = fs.readFileSync(file); files.set(file, bytes.length);
  const ast = parser.parse(bytes.toString(), { sourceType: 'module' });
  for (const node of ast.program.body) {
    if (['ImportDeclaration', 'ExportNamedDeclaration', 'ExportAllDeclaration'].includes(node.type) && node.source?.value.startsWith('.'))
      closure(path.resolve(path.dirname(file), node.source.value), excluded, files);
  }
  return files;
}
const initial = closure(path.join(dist, entryPath));
const sum = files => [...files.values()].reduce((total, bytes) => total + bytes, 0);
const excluded = new Set(initial.keys()), lazy = {};
const assets = fs.readdirSync(path.join(dist, 'assets'));
for (const prefix of ['LandingPage', 'RentalQueuePage', 'RentalRecordPage', 'MaintenanceDashboardPage', 'NotificationDeliveryPage']) {
  const name = assets.find(file => file.startsWith(prefix + '-') && file.endsWith('.js'));
  if (!name) throw new Error(`Missing expected lazy route: ${prefix}`);
  const file = path.join(dist, 'assets', name);
  lazy[prefix] = { ownBytes: fs.statSync(file).size, additionalStaticClosureBytes: sum(closure(file, excluded)) };
}
const result = { definition: 'Entry plus recursively static imports, uncompressed; lazy routes reported separately.', entryBytes: sum(initial), budgetBytes: 350000, lazy, passes: sum(initial) < 350000 };
const outputIndex = process.argv.indexOf('--output');
if (outputIndex >= 0) {
  const output = path.resolve(process.argv[outputIndex + 1]); fs.mkdirSync(path.dirname(output), { recursive: true }); fs.writeFileSync(output, JSON.stringify(result, null, 2) + '\n');
}
console.log(JSON.stringify(result));
if (!result.passes) process.exitCode = 1;
