# Build a task

Open a project's **Software Factory** tab. Five numbered steps run down the left;
the right shows one step at a time. Steps 1 and 2 happen once per project. The
designer, run history, reviews, repairs, GitHub delivery, calibration, intake and
budgets are under **Advanced tools**, unchanged.

## 1. Your code

Choose the folder that holds the project's code, the branch kept builds go into,
who writes the code and, optionally, the model. Canopy reads the folder's
branches as you type. A folder without Git, or without a first commit, offers
**Set up Git here**, which commits the files already in the folder. Each build
works in its own copy of the code, so the folder only changes when you keep a
result.

## 2. How to test it

The commands that tell finished work from broken work, usually the build and
test commands. Canopy pre-fills those it finds evidence for (see
[Guided factory setup](FACTORY-SETUP.md)) and offers the common commands from
`Factory:Setup:StarterChecks` when the folder has no code yet. Saving them sets
up the project's workflow.

## 3. Choose a task

Tasks that are ready to build: they have a description and no unfinished
prerequisite. **Show the tasks that aren't ready** lists the rest with what each
needs, and **Write a description** opens the task.

## 4. Build it

Shows the task's description and what will happen. **Build** prepares and
starts the build in one go: pressing it is the go-ahead, so there is no separate
confirmation. If the task can't be built yet, the reason is shown beside the
button.

## 5. Check the result

While it builds, the steps show live: copy your code, the agent writes the code,
run your tests, the agent checks the work, your turn. You can leave the page;
**Builds** in the step list shows any build that is working or needs you, and
the page reopens on it.

When it is your turn you see what the agent says it did, the files changed, each
test with its output, and the notes from the check.

- **Keep the changes** merges the build into the chosen branch with a merge
  commit and marks the task done (it moves to the project's first completed
  column). When that branch is checked out, its working copy is fast-forwarded,
  and Keep refuses if it has uncommitted changes to tracked files. When the
  branch has moved on and both change the same files, Keep explains which files
  and offers **Build again from the latest** branch.
- **Ask for changes** sends your note back. A new build starts from this
  build's result, and its changes are still measured from where the first build
  started.

If a test fails, the build stops and shows which test, opened at the end of its
output, with **Ask for changes**, **Try again** and **Edit the task**.

Passing tests and your decision are enough to keep a build: no acceptance
criteria are required. GitHub pull request delivery stays under
**Advanced tools**.

## How a build runs

A build is a run of the project workflow with these differences:

- Manual steps that come before any work are approved by pressing **Build**.
- After the agent's writing step, Canopy commits its changes in the build's
  copy (`git commit --no-verify`, as your Git identity). A build that changes
  nothing stops with that reason.
- The agent is told not to run the test commands or commit; Canopy runs the
  tests straight after. If the agent's tool use is refused, the refusal is
  added to its notes rather than failing finished work, because the tests and
  the check that follow decide. Other runs still stop on a refusal, and name
  what was refused.
- A build waiting for you survives a restart of Canopy.

## API

All under `/api/factory`, documented in `postman/specs/planner.openapi.json`:

| Call | Does |
| --- | --- |
| `POST /tasks/{id}/build` | Prepares and starts a build. `expectedRevision` is the project revision. |
| `GET /tasks/{id}/build` | The task's latest build, or 204. |
| `GET /projects/{id}/builds` | The latest build of each task. |
| `GET /builds/{id}` | One build: phase, files changed, tests and output, notes. |
| `POST /builds/{id}/keep` | Merges into the base branch and marks the task done. |
| `POST /builds/{id}/changes` | Asks for changes with a `note`; returns the new build. |
| `POST /builds/{id}/again` | Sets aside a waiting build and builds again from the latest base branch. |

Mutations take a `requestId`; retrying with the same one returns the same
result without repeating the work. `expectedRevision` on a build is the build's
revision. Conflicts and stale revisions return 409 with a plain reason.
