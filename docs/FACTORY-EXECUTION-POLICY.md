# Model routing and execution policy

This phase adds opt-in project role profiles and per-work-order execution budgets.
It preserves explicit preparation/start, native DuckDB persistence, independent
QA/review, repair lineage and task acceptance.

## Adopted contract

- GET/PUT `/api/factory/projects/{id}/execution-policy` reads/adopts policy using
  the project revision. Unknown input fields are rejected. Policy includes an
  enabled flag, role routes, maximum agent and command invocations, elapsed seconds,
  explicit unmetered-cost consent, and optional requested token/currency caps.
- Each role route supplies its provider, model ID and optional reasoning effort.
  Roles/providers come from existing domain/provider contracts. No model IDs,
  prices, live-account access or availability are inferred or adopted by default.
- Explicit step/task provider or model choices select that provider/project-provider
  and its configured settings. Otherwise a configured role route applies, followed
  by existing project settings. Explicit step effort wins. A role route never
  injects a model into an explicitly selected different provider.
- Enabled policy requires explicit resolved model IDs for agent steps. A work
  order captures the policy digest, resolved provider/model/effort and each source.
  Current policy changes revoke unstarted or subsequent execution; captures never
  silently reroute. Existing policy-free captures retain their original meaning.
- Invocation counts are reserved persistently before dispatch. Elapsed time runs
  from execution start, includes manual waits and stops owned child processes at
  the deadline. Repair attempts use the same captured execution policy in addition
  to their existing loop-wide limits; repair cannot bypass project limits.
- CLI usage reports are observations, not a spend guarantee. Retain supported
  provider completion counters with provenance. Missing/malformed/truncated usage
  remains explicit and never becomes zero. Token/currency caps that cannot be
  enforced by the current runner block dispatch; unmetered model execution needs
  explicit consent. No billing estimate or exchange rate is fabricated.
- Preparation, policy editing, readiness and usage inspection execute nothing.
  Exact start retries retain one work order. Backend restart retains policy,
  routing, counters and reports, and never replays interrupted execution.

## Acceptance evidence

Verify role routing, explicit-setting precedence, provider isolation, captured
settings and actual CLI arguments; invocation reservations, command/agent and
manual-wait deadlines, cancellation and restart; policy drift before start and
between steps; repair-policy interaction; absent/malformed usage; unsupported
cost/token caps; no workflow/layout edits; real native persistence and running UI.

## Operations and evidence semantics

Open **Software Factory → Model routing & budgets**. A new project has no policy.
Enter limits explicitly; no model IDs, prices or routes are supplied as defaults.
Agent invocations count CLI process reservations, not the provider requests/tools
inside an agent session. Each repair work order gets its captured project limits;
the existing repair policy separately bounds attempts and loop-wide agent calls.
Standalone specification/documentation assistance keeps its existing controls.

Changing the effective enabled policy revokes its old prepared work and cancels
active owned processes. Editing the same normalized policy preserves its hash.
Disabling a policy requires newly prepared work to return to the policy-free path.
Completed history keeps its original capture; policy drift makes QA and downstream
review/delivery gates stale. Historical elapsed usage is evaluated at recorded
finish time. Reservations remain consumed on failure/cancellation and are never
replayed after restart.

`Agents:MaxUsageEvents` and `Agents:MaxOutputCharacters` bound retained observations
per agent record. Codex `turn.completed` and Claude `result` counters retain their
provider/event identity, observation time, raw usage JSON and SHA-256. Model and
session context remain on the owning agent/work order. Missing reports stay
`unknown`; invalid counters, mixed missing reports or retention loss yield
`incomplete`. Repeated completions remain separate observations. Cache fields are
retained without assuming providers share accounting semantics. Currency totals
are not inferred from tokens, and no billing amount or enforced spend cap is
claimed.

Provider protocol references:
[Codex JSON events](https://learn.chatgpt.com/docs/non-interactive-mode#make-output-machine-readable)
and [Claude completion usage](https://platform.claude.com/cookbook/claude-agent-sdk-07-hosting-the-agent).
Only the reported usage subtree is retained; schema changes can leave usage
unknown/incomplete without turning it into zero.

See [validation](FACTORY-EXECUTION-POLICY-VALIDATION.md) for tested guarantees and
limits. Native persistence uses the public EFCoreProvider and the established
transactional copy migration; existing agent fields/indexes are preserved.

On macOS and Linux, subprocesses run in an owned session with a supervising process
that stays alive until the child and inherited output streams settle. Deadline or
cancellation terminates that active process group, including inherited-output children
whose direct parent exited. Output reads are cancellable. This does not sandbox
arbitrary commands or claim ownership of deliberately detached daemons. Final diff
and receipt collection retain their bounded backend inspection timeouts.

An enabled policy requires the owned-process lifetime adapter. Other operating
systems report the elapsed-limit capability as unavailable and block preparation
and dispatch, including captures moved from another machine. Existing policy-free
operation retains its previous platform behavior.

The supervisor holds an inherited local owner lease. An abrupt backend death closes
the lease and terminates the active owned process group; startup then preserves
interrupted state without replay. Lease plumbing follows the platform's
[anonymous-pipe contract](https://learn.microsoft.com/en-us/dotnet/standard/io/how-to-use-anonymous-pipes-for-local-interprocess-communication).
