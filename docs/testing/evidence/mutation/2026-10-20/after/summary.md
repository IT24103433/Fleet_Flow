# Mutation execution summary

Stryker exit code: 0
Mutation score: 95.96%

| Status | Count |
| --- | ---: |
| CompileError | 11 |
| Ignored | 231 |
| Killed | 95 |
| NoCoverage | 2 |
| Survived | 2 |
| Timeout | 0 |

100 * (Killed + Timeout) / (Killed + Timeout + Survived + NoCoverage)

## Surviving and uncovered mutants

- Survived C:\Users\jadav\Documents\Codex\2026-10-20\referenced-chatgpt-conversation-this-is-an\work\Fleet_Flow\src\backend\services\FleetService\FleetService.Api\Services\ReportCsv.cs:17:70 [2226] String mutation: '""'
- Survived C:\Users\jadav\Documents\Codex\2026-10-20\referenced-chatgpt-conversation-this-is-an\work\Fleet_Flow\src\backend\services\FleetService\FleetService.Api\Services\ReportCsv.cs:19:41 [2234] String mutation: '""'
- NoCoverage C:\Users\jadav\Documents\Codex\2026-10-20\referenced-chatgpt-conversation-this-is-an\work\Fleet_Flow\src\backend\services\FleetService\FleetService.Api\Services\ReportCsv.cs:45:91 [2268] String mutation: '"Stryker was here!"'
- NoCoverage C:\Users\jadav\Documents\Codex\2026-10-20\referenced-chatgpt-conversation-this-is-an\work\Fleet_Flow\src\backend\services\FleetService\FleetService.Api\Services\ReportCsv.cs:46:38 [2270] String mutation: '"Stryker was here!"'
