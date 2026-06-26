---
name: smart-task
description: >-
  Adaptive model router — analyze task complexity and delegate to the
  right subagent model. Triggered when the user asks to build a feature,
  fix a bug, refactor code, review changes, debug an issue, design
  architecture, optimize performance, write tests, explain complex code,
  or any development task beyond a trivial one-liner. Routes simple
  mechanical tasks to haiku (fast), medium tasks to sonnet (balanced),
  and complex/hard tasks to opus (deep reasoning). Always report routing
  decision with a one-line summary.
user-invocable: true
argument-hint: "<task description>"
model: opus
---

# Smart Task — Adaptive Model Router

Route user tasks to the most appropriate model based on complexity analysis.

## When This Skill Activates

This skill auto-triggers on most substantive development requests. It may also be
invoked explicitly via `/smart-task <description>`.

## Routing Logic

### Step 1 — Assess Task Complexity

Analyze the user's request against these criteria. Score complexity as one of:

| Level | Criteria | Examples |
|-------|----------|----------|
| **simple** | Single-file, mechanical change. No design decisions. Obvious solution. < 5 lines of logic. | Fix a typo, add a log line, rename a variable, format code, simple config change, answer a factual question |
| **medium** | Multi-step but well-understood. Within a few files. Clear patterns exist. Some design judgment needed. | Add a CRUD endpoint, moderate refactoring, fix a bug with known root cause, add tests for existing code, implement a standard UI component |
| **complex** | Cross-cutting concern. Multiple files/modules. Architectural implications. Ambiguous requirements. Performance critical. | Design a new system component, refactor across services, implement a complex algorithm, debug a race condition, security audit, data migration |
| **hard** | Greenfield architecture. Deep domain expertise. Novel problem. High stakes (data loss, security). Requires extensive exploration first. | Design a distributed system from scratch, multi-service auth flow, novel ML pipeline, protocol design |

**Tie-breaking rules:**
- If unsure between two levels, pick the higher one
- Tasks involving **data safety, security, or production impact** → bump up one level
- Tasks the user explicitly marked as **urgent or critical** → bump up one level
- User explicitly asked for a specific model → respect that choice

### Step 2 — Select Model

| Complexity | Model | Effort | Reasoning |
|------------|-------|--------|-----------|
| simple | `haiku` | `low` | Fast, cheap. No deep reasoning needed. |
| medium | `sonnet` | `medium` | Good balance of speed and capability. |
| complex | `opus` | `high` | Deep reasoning for architectural decisions. |
| hard | `opus` | `max` | Maximum reasoning budget for greenfield design. |

**Adapt to your environment:** Check whether `sonnet` and `opus` resolve to
different models in the user's `settings.json`. If they map to the same model,
skip `sonnet` and route medium tasks to `opus` with `medium` effort.

### Step 3 — Spawn Subagent

Use the `Agent` tool with the selected `model` and `effort`:

```
Agent({
  description: "short 3-5 word summary",
  prompt: "<the full task>",
  model: "<haiku|sonnet|opus>",
  effort: "<low|medium|high|xhigh|max>"
})
```

### Step 4 — Report Routing Decision

Before returning results, state the routing decision in one line:

> 🧠 Routed to `opus` (high effort) — cross-cutting architecture change across 5 files

Keep it concise. No need to explain the framework — just report what happened.

## Important Rules

1. **Only route delegable tasks.** Conversations, preference questions, and
   interactive back-and-forth stay in the main session.
2. **Don't route trivial tasks.** One-line answers, factual lookups, and
   simple file reads don't need a subagent.
3. **Respect explicit model requests.** If the user says "use haiku", use haiku.
4. **Don't overthink.** Spend at most a sentence or two assessing complexity.
5. **Transparency always.** Always report which model was chosen and why.

## Edge Cases

- **Subagent fails or times out:** Retry once with a higher-tier model. Report both attempts.
- **Task is too vague to assess:** Ask one clarifying question, then route.
- **User follows up on subagent's work:** Handle the follow-up yourself — don't
  spawn another subagent for a quick question about the result.
