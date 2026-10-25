# Lessons learned: Power Platform ALM lab

*Power Platform team, QPS · lab attended 2026-10-25 · draft for team discussion*

## Why this matters to us

So far our Power Platform work has mostly been FAQ agents built directly in an environment. Building real business apps for other teams needs a different way of working: how we build them, how we ship them, and how we secure them. This lab (a warehouse stock app built end to end on Power Platform) showed that way of working. This note covers what we should take from it, what we shouldn't copy as-is, and the questions we need to put to IT and Legal.

A teammate is attending a separate CI/CD pipelines lab. That lab covers *delivery* security. This one covered *building* and *data* security. We need both.

## 1. The core shift: an app is a package we build, not a thing we edit in place

We'd never modify a live plant without drawings, review and MOC. Our apps deserve the same discipline.

| Today (typical) | Package model |
|---|---|
| Build directly in one environment, often the default one | Separate environments: **Dev** (people build), **Test**, **Production** |
| The live app is the only copy | **Solutions** hold everything (tables, apps, flows, roles, agents) and live in **source control** |
| Changes go live when someone clicks Save | Dev → source control → review → **build once** → Test → Production |
| "Who changed this?" has no answer | Every change is a pull request with an owner, a reason and a reviewer |
| Production can be edited by hand | Test and Production only receive **managed** (sealed) solutions |

Five things follow from this:

1. **Git is the master copy, not the environment.** An environment is one installation of the app. If it's lost, we rebuild it from source.
2. **Build once, install the same thing everywhere.** The package that passed Test is the exact file that goes to Production.
3. **Production is read-only to people.** Rule of thumb: *if a person can edit it, it belongs only in Dev.*
4. **Every change has an owner, a reason and a reviewer:** a small MOC.
5. **Undo is possible.** Rolling back means reinstalling an earlier package version.

**The costs:** small fixes are slower (a typo goes through a PR), every release needs a new version number (like a drawing revision), and there is upfront setup.

### The same process in EPC terms

| EPC | Power Platform |
|---|---|
| IFC drawings / specs | The approved spec in `docs/`, under version control |
| RFI | A written question to the spec owner, raised as an issue or PR comment, **before** building on an assumption |
| TQ / design deviation | A spec change, made through its own PR |
| MOC | Pull request |
| Systems / subsystems | The package and its solutions (data model, logic, security, UI), each with one owner |
| Fabrication | The build |
| Redlines → as-builts | Changes made by hand in Dev, pulled back into source |
| Hold point / witness point | A required gate (build check, required reviewer, approval before Production) / an optional reviewer |
| ITP / ITR | Acceptance criteria / test results. Automated tests are ITRs that re-run on every change. |
| Subsystem walkdown | Reviewing one solution against its spec, in the PR and hands-on in Dev with the right security role |
| FAT | Deploy to Test and verify |
| Mechanical completion | Merged to main: build passed, review done, no open A-items |
| Turnover package | Release: package, version, release notes, test evidence |
| Energization / RFSU | Deploy to Production |
| LOTO | Managed solutions: nobody edits Test or Production by hand |
| NCR | Defect or incident: what's deployed doesn't match the spec |

### Punch list categories for app releases

*(Align these with QPS's own A/B/C definitions.)*

| Category | Meaning for an app | Gate |
|---|---|---|
| **A** | Blocks merge or deploy: wrong security, exposed data, a business rule that can be bypassed | Enforced automatically where possible (required checks, required reviewer) |
| **B** | Blocks Production, not Test: the app works but fails an acceptance criterion | Release checklist before Production |
| **C** | Cosmetic or nice-to-have | Backlog, fixed in a later release |

Examples from the lab:

- **A-items to look for:** a worker role with Delete on stock history, a business rule registered twice or not at all, PII readable by every role.
- **B-items actually found:** list views missing the columns the spec required, and subgrids pointing at the wrong views.
- **C-item actually found:** a navigation area with no title.

> **One-liner:** *A pull request is our MOC, the spec is our IFC, a review is a subsystem walkdown, and nothing gets energized in Production with an open A-item.*

## 2. Security

Security has two halves, and the two labs cover different ones:

| Half | Question | Covered by |
|---|---|---|
| **Delivery security** | Who can change the app and get it into Production? | The CI/CD lab (tomorrow), plus parts of this one |
| **Data security** | Once the app is live, who can see and change which records and fields? | This lab, at a basic level. **Business units, teams, column security and auditing weren't covered by either lab.** |

### Patterns worth adopting

- **Security roles stored as files in source control.** A privilege change becomes a reviewed, auditable diff, not a click in the admin portal.
- **Design the privilege matrix first:** table × privilege × depth, per role, signed off like any other design.
- **Roles for personas, not people.** Individuals get access by assignment, not by new roles.
- **The server enforces business rules, not the screen.** Rules run inside Dataverse (plugins), so they apply to every app, import, flow and API call.
- **Server-side rules run as the calling user,** so automation can't quietly give someone extra access.
- **Deploy without stored passwords.** The pipeline signs in to Azure through a trust link to one specific repository and branch (federated credential): no secrets to leak or rotate.
- **No credentials inside solutions.** Each environment supplies its own connections.

### Gaps before this is ready for QPS

| Area | What the lab does | What we'd need |
|---|---|---|
| Access scope | Only "own records" or "whole organization" | **Business units or teams per project or client,** so Project A can't see Project B's data |
| Sensitive columns | None | **Column security profiles** for PII, commercial and client-confidential fields |
| Auditing | Off | Dataverse auditing on what matters, with retention matching our records policy |
| Assigning access | Roles assigned to individuals | Roles assigned to **teams linked to Entra ID groups** |
| Deployment identity | System Administrator on Test | Least privilege; separate identities for Test and Production |
| Change review | PRs required, **0 approvals** | At least 1 required reviewer; CODEOWNERS on the security solution; approval before Production |
| External connectors | A public API | **Data loss prevention policies** on which connectors may be used together with business data |
| Testing security | Not tested | Automated checks that each role can and can't do what the matrix says |

### EPC-specific considerations

- **The main boundary is probably the project or client, not the department.** Decide early between business units, owner teams and access teams. It's painful to change later.
- **PII isn't the only sensitive data.** Bid pricing, vendor quotes, contract values and client datacenter power designs may carry commercial, contractual or critical-infrastructure obligations.
- **External users** (subcontractors, vendors) are a separate decision with its own security model (guest accounts or Power Pages). Flag it early.

### Questions for IT and Legal

1. Which data types need column-level protection (PII, commercial, client-confidential, critical infrastructure)?
2. What audit retention do we need for app data and for security changes?
3. Should app data be separated per project or per client? Who decides who sees what?
4. Which connectors are allowed or blocked (DLP), especially with external services?
5. What are the rules for giving external parties access to our apps?
6. Who must approve a security-role change before it reaches Production?

## 3. Building with an AI assistant: what we observed

The lab has an AI assistant build each part from a written spec, while the person reviews and merges.

- **It's fast, and that's a risk to learning and to quality.** Most of the build happened with little hands-on involvement. The value comes from reading the spec, reviewing **Files changed** in every PR, and trying the result in Dev with the right role. Merging without walking it down skips the inspection.
- **Conflicts between the spec and the implementation must be RFIs, not judgment calls.** Twice during the lab the assistant hit a conflict between the spec and the instructions (a feature to defer, and wrongly named views). It resolved both itself and reported afterwards. On a real project, both should have gone to the spec owner in writing first. Our process should say so explicitly.
- **Good specs make good builds.** The lab's specs (data dictionary, privilege matrix, user stories with Given/When/Then acceptance criteria) are a template worth reusing. They're also where IT and Legal sign off.

## 4. What not to copy as-is

- **The toolchain.** The lab uses TALXIS DevKit (`txc`), a third-party tool for building everything as code. It's powerful, but it's a big step for us right now. Microsoft's own tools get most of the benefit with less to learn:
  - **Power Platform Pipelines** for Dev → Test → Production
  - **Dataverse Git integration** to connect solutions to source control
  - **`pac` CLI / Power Platform Build Tools** when we want full CI/CD (likely the other lab)
- **Working entirely in code.** Most teams keep building in the maker portal and use source control to *record and review* changes ("redlines → as-builts").

## 5. Suggested adoption path

| Stage | What we do | What we get |
|---|---|---|
| **1. Basics** | Dev, Test and Production environments per app area; everything in solutions; managed solutions via Power Platform Pipelines. **Start with our existing FAQ agents.** | No more building in Production; repeatable releases |
| **2. Review** | Solutions in Git; a reviewed PR for every change; a short spec per app with acceptance criteria | Audit trail; a place for security and PII sign-off |
| **3. Automation** | Build and deploy automatically from source control, with an approval before Production (the CI/CD lab) | Releases without manual steps |
| **4. Governance** | Managed Environments, DLP policies, security roles in source control, project- or client-based access | Controls IT and Legal can trust |

**Proposed first step:** take one existing FAQ agent through stage 1 end to end, then use that process for the first real app.

## Appendix: lab structure

- **Setup checkpoints:** CP01–CP05 (tools, repository, CI, environments, CD), CP10 (deploy and sync), CP13 (required build checks)
- **Build checkpoints, where the lessons are:** CP06 data model, CP07 server-side rules, CP08 security roles, CP09 model-driven app and code app, CP11 external data, CP12 reference data, CP14–CP15 tests
- **Specs** (good templates): `docs/design/`, especially `lab-1-data-model.md` (data dictionary) and `lab-3-security.md` (privilege matrix)
