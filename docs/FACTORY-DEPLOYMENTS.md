# Deployment environments and recovery

Named deployment targets extend controlled delivery. A merged delivery can be
promoted to an explicitly authorized environment without repeating its PR merge.
Planning, factory execution, deployment and task acceptance remain separate.
No targets or deployment commands are adopted by default.

## Configuration and authority

Administrator-owned `Delivery.Targets` entries contain `id`, `name`, `environment`,
`deployHookId` and optional `recoveryHookId`. Hook IDs reference the existing
`Delivery.Hooks` command/probe configuration. Deploy and recovery hooks must use
the same health URL, snapshot URL, expected health and instance/revision pointers.
Commands, endpoints, credentials, timeouts and integrity inputs remain backend
configuration; API clients cannot submit arbitrary commands.

Project delivery policy explicitly adopts `deploymentTargetIds`. Omitted or null
means no named-target authority and preserves compatibility with earlier captured
policy hashes. Existing post-merge hooks continue to work independently. Editing
policy or target/hook/executable/script configuration invalidates captured action
authority. Restoring the exact configuration allows observation; closing and
capturing a new deployment requires fresh approval.

A capture stores the source delivery, actual merged commit, named environment,
provider identity, policy digest and deployment/recovery configuration hashes.
The provider must confirm the PR repository/head/merge identity and actual Git
commit tree, both ordered parents and target ancestry. Deployment uses that
immutable merged revision; later edits to the planning task do not substitute a
new revision. Each approval binds its action, revision, environment and captured
configuration. Approval notes must address sensitive files and migrations where
applicable. Approval does not execute a command.

## Execution and recovery

Before deployment, Canopy reads the actual target's health, process identity,
running immutable Git commit ID and canonical persistence snapshot. Version
labels and mutable refs are rejected before dispatch because they cannot bind
recovery to an immutable prior revision. It persists a command intent
before dispatch. Command input includes `CANOPY_DELIVERY_ID`,
`CANOPY_DEPLOYMENT_ID`, `CANOPY_REPOSITORY`, `CANOPY_MERGE_SHA`, `CANOPY_REVISION`,
`CANOPY_TARGET_ID`, `CANOPY_ENVIRONMENT` and `CANOPY_RECOVERY`. `CANOPY_REVISION`
is the desired deployed revision; during recovery it is the previously observed
revision, while `CANOPY_MERGE_SHA` continues to identify the source merge.

Success requires command exit zero, expected health, the approved running commit,
a changed process instance and the original persistence hash. A no-op, wrong
revision, changed persistence, timeout, failed command or unavailable probe stays
visible as needs-attention. A green health check cannot erase a nonzero command
exit. Failed/interrupted deployment and recovery receipts remain unchanged when
later observation establishes an outcome; observation has its own journal entry.

Recovery is a separate approval and one explicitly requested configured command.
It restores the original pre-deployment revision and persistence snapshot. The
provider must establish that prior revision's target-branch ancestry. A down target
does not prevent recovery: the baseline comes from its retained pre-deployment
observation. Recovery scripts must implement the project's actual restore procedure;
Canopy does not invent database rollback or migration compatibility. Recovery is
unavailable if no command is configured or no prior revision can be verified.

Targets sharing the same normalized health/snapshot endpoints reserve one resource
across projects and target aliases. Open deployments block source task/project
deletion and conflicting legacy post-merge actions. Successful deployment/recovery
releases the reservation. Closing tracking is explicit, retains receipts and makes
no claim about an unknown target state; inspect that state before new work.

## API and UI

All routes use the existing loopback/same-origin factory boundary and no-store.
Unknown fields are rejected. Every mutation takes a unique `requestId`; exact
retries return the existing record/operation, while changed content conflicts.

- GET `/api/factory/deliveries/{id}/deployments`: configured targets, project
  authorization, source revision and deployment history.
- POST the same path: `{requestId,expectedRevision,targetId}`. Expected revision
  belongs to the source delivery. Capture executes nothing.
- GET `/api/factory/deployments/{id}`: capture, approvals, original command
  receipts, authority errors and operation journal.
- POST `/api/factory/deployments/{id}/actions`:
  `{requestId,expectedRevision,action,approvalNote}`. Expected revision belongs to
  the deployment. Actions: `approveDeploy`, `deploy`, `approveRecovery`, `recover`,
  `reconcile`, `close`.

Operations are asynchronous. Backend restart marks pending operations uncertain
and never replays commands. `reconcile` only reads the configured target and
provider. Commands cannot be repeated within a deployment record. An explicit
new capture is a new deployment decision, not an automatic retry.

In a merged run's Controlled PR delivery panel, open **Deployment environments**.
Choose a configured target, capture the merged revision, approve, then deploy.
History shows the environment, desired/recovery revisions, outcomes, original
receipts and operation evidence. Project delivery policy contains the explicit
allowed-target checklist. No task workflow status changes automatically.
