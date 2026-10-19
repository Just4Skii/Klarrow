#!/usr/bin/env node
/**
 * Klarrow evaluation runner.
 *
 * Executes locked evaluation scenarios through the public HTTP API of a freshly started
 * API process with an isolated data root, then scores the run against the scenario's
 * pre-registered ground truth.
 *
 * Usage:
 *   node scripts/evaluation/run-scenario.mjs --scenario scenario_a_run4_baseline --mode deterministic_only
 *   node scripts/evaluation/run-scenario.mjs --suite [--run-id <id>] [--modes deterministic_only,llama]
 *
 * The runner never writes inside data/evaluation/scenarios. Raw API responses and computed
 * metrics are written under results/evaluation/runs/<runId>/<scenario>/<mode>/.
 */

import { spawn, spawnSync } from "node:child_process";
import { createHash } from "node:crypto";
import { mkdir, readFile, readdir, rm, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = path.resolve(__dirname, "..", "..");
const SCENARIOS_ROOT = path.join(REPO_ROOT, "data", "evaluation", "scenarios");
const RUNS_ROOT = path.join(REPO_ROOT, "results", "evaluation", "runs");
const API_EXE = path.join(
  REPO_ROOT,
  "src",
  "RecallGraph.Api",
  "bin",
  "Release",
  "net10.0",
  "RecallGraph.Api.exe"
);

const DEFAULT_MODEL = "llama3.2:3b";
const DEFAULT_LLAMA_BASE = "http://127.0.0.1:11434/v1";

function parseArgs(argv) {
  const args = { modes: ["deterministic_only", "llama"] };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === "--scenario") args.scenario = argv[++i];
    else if (a === "--mode") args.mode = argv[++i];
    else if (a === "--suite") args.suite = true;
    else if (a === "--run-id") args.runId = argv[++i];
    else if (a === "--modes") args.modes = argv[++i].split(",").map((m) => m.trim()).filter(Boolean);
    else if (a === "--skip-build") args.skipBuild = true;
    else throw new Error(`Unknown argument: ${a}`);
  }
  return args;
}

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

function sha256(buffer) {
  return createHash("sha256").update(buffer).digest("hex");
}

async function readJson(file) {
  return JSON.parse(await readFile(file, "utf8"));
}

async function writeJson(file, value) {
  await mkdir(path.dirname(file), { recursive: true });
  await writeFile(file, JSON.stringify(value, null, 2), "utf8");
}

function tupleKey(kind, fromId, toId) {
  return `${String(kind).trim().toLowerCase()}|${String(fromId).trim().toLowerCase()}|${String(toId).trim().toLowerCase()}`;
}

function unorderedPair(a, b) {
  const x = String(a).trim().toUpperCase();
  const y = String(b).trim().toUpperCase();
  return x <= y ? `${x}::${y}` : `${y}::${x}`;
}

async function listScenarioIds() {
  const entries = await readdir(SCENARIOS_ROOT, { withFileTypes: true });
  return entries.filter((e) => e.isDirectory()).map((e) => e.name).sort();
}

class ApiClient {
  constructor(baseUrl) {
    this.baseUrl = baseUrl;
  }

  async request(method, urlPath, { body, timeoutMs = 60000 } = {}) {
    const res = await fetch(`${this.baseUrl}${urlPath}`, {
      method,
      headers: body !== undefined ? { "Content-Type": "application/json" } : undefined,
      body: body !== undefined ? JSON.stringify(body) : undefined,
      signal: AbortSignal.timeout(timeoutMs)
    });
    const text = await res.text();
    let parsed = null;
    try {
      parsed = text ? JSON.parse(text) : null;
    } catch {
      parsed = { raw: text };
    }
    if (!res.ok) {
      throw new Error(`${method} ${urlPath} -> ${res.status}: ${text.slice(0, 500)}`);
    }
    return parsed;
  }

  get(p, opts) {
    return this.request("GET", p, opts);
  }

  post(p, body, opts) {
    return this.request("POST", p, { body, ...opts });
  }

  async upload(filePath, investigationId) {
    const bytes = await readFile(filePath);
    const fileName = path.basename(filePath);
    const form = new FormData();
    form.append("file", new Blob([bytes]), fileName);
    form.append("investigationId", investigationId);
    const res = await fetch(`${this.baseUrl}/api/documents/upload`, {
      method: "POST",
      body: form,
      signal: AbortSignal.timeout(120000)
    });
    const text = await res.text();
    if (!res.ok) throw new Error(`upload ${fileName} -> ${res.status}: ${text.slice(0, 300)}`);
    return JSON.parse(text);
  }
}

async function waitForHealth(client, timeoutMs = 60000) {
  const start = Date.now();
  let lastError = null;
  while (Date.now() - start < timeoutMs) {
    try {
      return await client.get("/api/health", { timeoutMs: 5000 });
    } catch (e) {
      lastError = e;
      await sleep(700);
    }
  }
  throw new Error(`API did not become healthy within ${timeoutMs}ms: ${lastError?.message ?? "no response"}`);
}

function startApi({ dataRoot, port, mode, logDir }) {
  const env = {
    ...process.env,
    ASPNETCORE_URLS: `http://127.0.0.1:${port}`,
    ASPNETCORE_ENVIRONMENT: "Development",
    Storage__DataRoot: dataRoot,
    Storage__UploadsPath: path.join(dataRoot, "uploads"),
    Storage__SeedEvidencePath: path.join(REPO_ROOT, "seed", "evidence"),
    Seed__DemoEnabled: "false"
  };
  if (mode === "deterministic_only") {
    env.EXTRACTION_MODE = "deterministic_only";
    env.LLAMA_EXTRACTION_DISABLED = "true";
  } else {
    delete env.EXTRACTION_MODE;
    delete env.LLAMA_EXTRACTION_DISABLED;
    env.LLAMA_API_BASE_URL = process.env.LLAMA_API_BASE_URL || DEFAULT_LLAMA_BASE;
    env.LLAMA_MODEL = process.env.LLAMA_MODEL || DEFAULT_MODEL;
    env.LLAMA_TIMEOUT_SECONDS = process.env.LLAMA_TIMEOUT_SECONDS || "180";
  }

  const out = spawn(API_EXE, [], { cwd: REPO_ROOT, env, windowsHide: true });
  const logs = { stdout: [], stderr: [] };
  out.stdout.on("data", (d) => logs.stdout.push(d.toString()));
  out.stderr.on("data", (d) => logs.stderr.push(d.toString()));
  const processInfo = { proc: out, pid: out.pid, port, logs, logDir, async stop() {
    try {
      out.kill();
    } catch {
      /* already gone */
    }
    const exited = await Promise.race([
      new Promise((resolve) => out.once("exit", () => resolve(true))),
      sleep(8000).then(() => false)
    ]);
    if (!exited && processInfo.pid) {
      spawnSync("taskkill", ["/PID", String(processInfo.pid), "/T", "/F"], { windowsHide: true });
    }
    if (logDir) {
      await writeFile(path.join(logDir, "api.stdout.log"), logs.stdout.join(""), "utf8").catch(() => {});
      await writeFile(path.join(logDir, "api.stderr.log"), logs.stderr.join(""), "utf8").catch(() => {});
    }
  } };
  return processInfo;
}

function pickLink(links, spec) {
  const kind = spec.linkKind ? spec.linkKind.toLowerCase() : null;
  const fromId = spec.fromId ? spec.fromId.toLowerCase() : null;
  const toId = spec.toId ? spec.toId.toLowerCase() : null;
  const toIdContains = spec.toIdContains ? spec.toIdContains.toLowerCase() : null;
  return links.find((l) => {
    if (kind && String(l.linkKind).toLowerCase() !== kind) return false;
    if (fromId && String(l.fromId).toLowerCase() !== fromId) return false;
    if (toId && String(l.toId).toLowerCase() !== toId) return false;
    if (toIdContains && !String(l.toId).toLowerCase().includes(toIdContains)) return false;
    return true;
  });
}

async function executeScenario(client, scenario, manifest, groundTruth, mode, rawDir) {
  const raw = {};
  const inv = await client.post("/api/investigations", {
    name: groundTruth.investigation.name,
    reference: groundTruth.investigation.reference,
    suspectLotType: groundTruth.investigation.suspectLotType,
    suspectLotId: groundTruth.investigation.suspectLotId,
    description: `Evaluation run (${scenario}, ${mode}).`,
    status: "Open",
    actor: "eval-runner"
  });
  raw.investigation = inv;

  const uploads = [];
  const uploadedIds = [];
  for (const doc of manifest.documents) {
    const filePath = path.join(SCENARIOS_ROOT, scenario, "documents", doc.fileName);
    const result = await client.upload(filePath, inv.id);
    uploads.push(result);
    uploadedIds.push(result.documentId);
  }
  raw.uploads = uploads;

  raw.documents = await client.get(`/api/documents?investigationId=${inv.id}`);
  raw.aiStatusBefore = await client.get("/api/extraction/status");
  raw.extractionRun = await client.post(
    "/api/extraction/run",
    { documentIds: uploadedIds, reviewer: "eval-runner" },
    { timeoutMs: mode === "llama" ? 45 * 60 * 1000 : 5 * 60 * 1000 }
  );
  raw.aiStatusAfter = await client.get("/api/extraction/status");
  if (mode === "llama") {
    raw.smoke = await client.post("/api/extraction/smoke", undefined, { timeoutMs: 5 * 60 * 1000 });
  }

  raw.relationshipGeneration1 = await client.post("/api/relationship-proposals/generate", { investigationId: inv.id }, { timeoutMs: 300000 });
  raw.facts = await client.get(`/api/workspace/facts?investigationId=${inv.id}`);
  raw.matches1 = await client.get("/api/match-proposals?refresh=true");
  raw.linksPreDecisions = await client.get(`/api/relationship-proposals?investigationId=${inv.id}`);
  raw.findingsPreDecisions = await client.get(`/api/gaps?refresh=true&investigationId=${inv.id}`);

  const script = groundTruth.decisionScript;
  const preTraces = {};
  for (const traceSpec of script.traces.filter((t) => t.traceBeforeDecisions)) {
    preTraces[traceSpec.label] = await client.post(
      "/api/trace-runs",
      { investigationId: inv.id, selectedType: traceSpec.selectedType, selectedId: traceSpec.selectedId, direction: traceSpec.direction },
      { timeoutMs: 120000 }
    );
  }
  raw.tracesPreDecisions = preTraces;

  const decisionsLog = [];
  for (const spec of script.confirmLinks) {
    const link = pickLink(raw.linksPreDecisions, spec);
    if (!link) {
      decisionsLog.push({ action: "confirm", spec, outcome: "LINK_NOT_FOUND" });
      continue;
    }
    const updated = await client.post(`/api/relationship-proposals/${link.id}/confirm`, { reviewer: "eval-runner", notes: "Evaluation suite scripted decision." });
    decisionsLog.push({ action: "confirm", spec, outcome: "ok", linkId: link.id, state: updated.state });
  }
  for (const spec of script.rejectLinks ?? []) {
    const link = pickLink(raw.linksPreDecisions, spec);
    if (!link) {
      decisionsLog.push({ action: "reject", spec, outcome: "LINK_NOT_FOUND" });
      continue;
    }
    const updated = await client.post(`/api/relationship-proposals/${link.id}/reject`, { reviewer: "eval-runner", notes: "Evaluation suite scripted rejection of expected noise candidate." });
    decisionsLog.push({ action: "reject", spec, outcome: "ok", linkId: link.id, state: updated.state });
  }
  for (const decision of script.matchDecisions ?? []) {
    const [a, b] = decision.pairUnordered;
    const pairKey = unorderedPair(a, b);
    const proposal = raw.matches1.find((m) => unorderedPair(m.leftValue, m.rightValue) === pairKey);
    if (!proposal) {
      decisionsLog.push({ action: `match-${decision.action}`, pairKey, outcome: "MATCH_NOT_FOUND" });
      continue;
    }
    const action = decision.action === "confirm" ? "confirm" : decision.action === "reject" ? "reject" : "defer";
    const updated = await client.post(`/api/match-proposals/${proposal.id}/${action}`, { actor: "eval-runner", reason: decision.reason ?? "" });
    decisionsLog.push({ action: `match-${action}`, pairKey, outcome: "ok", proposalId: proposal.id, state: updated.state });
  }
  raw.decisions = decisionsLog;

  if (script.regenerateAfterAlias) {
    raw.relationshipGeneration2 = await client.post("/api/relationship-proposals/generate", { investigationId: inv.id }, { timeoutMs: 300000 });
    const linksAfterRegen = raw.relationshipGeneration2.proposals ?? (await client.get(`/api/relationship-proposals?investigationId=${inv.id}`));
    for (const spec of script.confirmNewCanonicalLinksAfterRegeneration ?? []) {
      const link = pickLink(linksAfterRegen, spec);
      if (!link) {
        decisionsLog.push({ action: "confirm-post-regen", spec, outcome: "LINK_NOT_FOUND" });
        continue;
      }
      const updated = await client.post(`/api/relationship-proposals/${link.id}/confirm`, { reviewer: "eval-runner", notes: "Evaluation suite: canonical link after alias confirmation." });
      decisionsLog.push({ action: "confirm-post-regen", spec, outcome: "ok", linkId: link.id, state: updated.state });
    }
    raw.matches2 = await client.get("/api/match-proposals?refresh=true");
  }

  raw.findingsFinal = await client.get(`/api/gaps?refresh=true&investigationId=${inv.id}`);
  raw.linksFinal = await client.get(`/api/relationship-proposals?investigationId=${inv.id}`);
  raw.aliases = await client.get("/api/entity-aliases");
  raw.matchesFinal = await client.get("/api/match-proposals");

  const finalTraces = {};
  for (const traceSpec of script.traces.filter((t) => !t.traceBeforeDecisions)) {
    finalTraces[traceSpec.label] = await client.post(
      "/api/trace-runs",
      { investigationId: inv.id, selectedType: traceSpec.selectedType, selectedId: traceSpec.selectedId, direction: traceSpec.direction },
      { timeoutMs: 120000 }
    );
  }
  raw.tracesFinal = finalTraces;
  raw.audits = await client.get("/api/workspace/audits");
  raw.home = await client.get("/api/workspace/home");

  await writeJson(path.join(rawDir, "raw-run.json"), raw);
  return raw;
}

function tracePossibleScope(trace) {
  const confirmedFinished = new Set(trace.affectedFinishedLots ?? []);
  const confirmedCustomers = new Set(trace.affectedCustomers ?? []);
  const derivedPossibleFinished = [...new Set((trace.nodes ?? []).filter((n) => n.kind === "finished-lot").map((n) => n.id))].filter(
    (x) => !confirmedFinished.has(x)
  );
  const derivedPossibleCustomers = [...new Set((trace.nodes ?? []).filter((n) => n.kind === "customer").map((n) => n.id))].filter(
    (x) => !confirmedCustomers.has(x)
  );
  return {
    confirmedFinishedLots: [...confirmedFinished].sort(),
    confirmedCustomers: [...confirmedCustomers].sort(),
    possibleFinishedLots: (trace.possibleFinishedLots ?? derivedPossibleFinished).sort(),
    possibleCustomers: (trace.possibleCustomers ?? derivedPossibleCustomers).sort(),
    nodes: (trace.nodes ?? []).map((n) => `${n.kind}:${n.id}`)
  };
}

function sameSet(a, b) {
  const sa = [...new Set(a)].sort();
  const sb = [...new Set(b)].sort();
  return sa.length === sb.length && sa.every((v, i) => v === sb[i]);
}

function computeMetrics({ scenario, mode, manifest, groundTruth, raw, hashChecks }) {
  const metrics = {
    schemaVersion: "1.0",
    scenario,
    mode,
    generatedAt: new Date().toISOString(),
    manifestIntegrity: hashChecks,
    provider: {
      requestedMode: mode,
      statusModeBefore: raw.aiStatusBefore?.mode ?? null,
      statusModeAfter: raw.aiStatusAfter?.mode ?? null,
      provider: raw.extractionRun?.provider ?? null,
      modelId: raw.extractionRun?.modelId ?? null,
      extractionRunStatus: raw.extractionRun?.status ?? null,
      documentsProcessed: raw.extractionRun?.documentsProcessed ?? null,
      factsCreatedByExtraction: raw.extractionRun?.factsCreated ?? null,
      isDemo: raw.extractionRun?.isDemo ?? null
    },
    forbiddenLinksFound: [],
    links: {},
    facts: {},
    matches: {},
    findings: {},
    traces: {},
    aiClaims: null
  };

  // ---- links ----
  const linksFinal = raw.linksFinal ?? [];
  const matchedRequired = [];
  const missingRequired = [];
  for (const req of groundTruth.requiredLinks) {
    const found = pickLink(linksFinal, { linkKind: req.linkKind, fromId: req.fromId, toId: req.toId });
    if (!found) {
      missingRequired.push(`${req.linkKind}:${req.fromId}->${req.toId}`);
      continue;
    }
    const stateOk = String(found.state).toLowerCase() === String(req.finalState).toLowerCase();
    matchedRequired.push(`${req.linkKind}:${req.fromId}->${req.toId}${stateOk ? "" : ` [state=${found.state}, expected ${req.finalState}]`}`);
  }
  const requiredKeys = new Set(groundTruth.requiredLinks.map((r) => tupleKey(r.linkKind, r.fromId, r.toId)));
  const noiseSpecs = groundTruth.expectedNoiseLinks ?? [];
  const matchedNoise = [];
  const extras = [];
  for (const link of linksFinal) {
    const key = tupleKey(link.linkKind, link.fromId, link.toId);
    if (requiredKeys.has(key)) continue;
    const noiseSpec = noiseSpecs.find((n) => {
      if (n.linkKind && n.linkKind.toLowerCase() !== String(link.linkKind).toLowerCase()) return false;
      if (n.fromId && n.fromId.toLowerCase() !== String(link.fromId).toLowerCase()) return false;
      if (n.toIdContains && !String(link.toId).toLowerCase().includes(n.toIdContains.toLowerCase())) return false;
      return true;
    });
    if (noiseSpec) {
      const stateOk = !noiseSpec.expectedFinalState || String(link.state).toLowerCase() === noiseSpec.expectedFinalState.toLowerCase();
      matchedNoise.push({ tuple: key, state: link.state, stateOk });
      continue;
    }
    extras.push({ tuple: key, state: link.state, detectionMethod: link.detectionMethod });
  }
  metrics.links = {
    totalFinal: linksFinal.length,
    requiredMatched: matchedRequired.length,
    requiredTotal: groundTruth.requiredLinks.length,
    missingRequired,
    expectedNoiseMatched: matchedNoise,
    unexpectedLinks: extras,
    linkCountExpected: groundTruth.requiredLinks.length + noiseSpecs.length
  };

  // ---- forbidden link patterns ----
  for (const pattern of groundTruth.forbiddenLinkPatterns ?? []) {
    const hits = linksFinal.filter((l) => {
      if (pattern.endpointKinds && !pattern.endpointKinds.includes(l.linkKind)) return false;
      const evidence = `${l.sourceReferences ?? ""} ${l.evidenceText ?? ""}`.toLowerCase();
      if (pattern.evidenceContainsAny && pattern.evidenceContainsAny.some((s) => evidence.includes(s.toLowerCase()))) return true;
      if (pattern.evidenceContains && evidence.includes(pattern.evidenceContains.toLowerCase())) return true;
      if (pattern.toIdContainsAny && pattern.toIdContainsAny.some((s) => String(l.toId).toLowerCase().includes(s.toLowerCase()))) return true;
      return false;
    });
    if (hits.length > 0) {
      metrics.forbiddenLinksFound.push({ description: pattern.description, hits: hits.map((h) => tupleKey(h.linkKind, h.fromId, h.toId)) });
    }
  }

  // ---- facts ----
  const facts = raw.facts ?? [];
  const factText = (f) => `${f.rawValue ?? ""} ${f.claimSubject ?? ""} ${f.claimObject ?? ""} ${f.evidenceText ?? ""} ${f.normalizedValue ?? ""}`.toLowerCase();
  const tokenResults = (groundTruth.requiredFacts?.tokens ?? []).map((token) => ({
    token,
    found: facts.some((f) => factText(f).includes(token.toLowerCase()))
  }));
  const quantityResults = (groundTruth.requiredFacts?.quantities ?? []).map((q) => ({
    value: q.value,
    unit: q.unit,
    found: facts.some((f) => Number(f.quantity) === Number(q.value) && String(f.quantityUnit ?? "").toLowerCase() === String(q.unit).toLowerCase())
  }));
  const trapResults = (groundTruth.requiredFacts?.verbatimTraps ?? []).map((trap) => {
    const wanted = String(trap.contains).toLowerCase();
    const parsed = /^([\d.,]+)\s*(\S+)$/.exec(String(trap.contains).trim());
    const foundVerbatim = facts.some((f) => {
      if (!String(f.sourceDocumentName ?? "").includes(trap.fileName)) return false;
      const blob = `${f.rawValue ?? ""} ${f.normalizedValue ?? ""} ${f.evidenceText ?? ""} ${f.sourceQuote ?? ""} ${f.claimObject ?? ""}`.toLowerCase();
      if (blob.includes(wanted)) return true;
      if (parsed) {
        const quantity = Number(String(parsed[1]).replace(/,/g, ""));
        return Number(f.quantity) === quantity && String(f.quantityUnit ?? "").toLowerCase() === parsed[2].toLowerCase();
      }
      return false;
    });
    return { fileName: trap.fileName, contains: trap.contains, foundVerbatim };
  });
  const byMethod = {};
  for (const f of facts) byMethod[f.extractionMethod] = (byMethod[f.extractionMethod] ?? 0) + 1;
  const aiRows = facts.filter((f) => f.extractionMethod === "ai-extraction");
  const locatorPresent = facts.filter((f) => String(f.sourceLocation ?? "").length > 0).length;
  metrics.facts = {
    total: facts.length,
    byExtractionMethod: byMethod,
    aiRowCount: aiRows.length,
    aiRowsWithQuantity: aiRows.filter((f) => f.quantity !== null && f.quantity !== undefined).length,
    aiRowsWithSubjectAndObject: aiRows.filter((f) => String(f.claimSubject ?? "").length > 0 && String(f.claimObject ?? "").length > 0).length,
    tokenRecall: tokenResults,
    tokenRecallRate: tokenResults.length ? tokenResults.filter((t) => t.found).length / tokenResults.length : null,
    quantityRecall: quantityResults,
    quantityRecallRate: quantityResults.length ? quantityResults.filter((q) => q.found).length / quantityResults.length : null,
    verbatimTraps: trapResults,
    locatorPresentRate: facts.length ? locatorPresent / facts.length : null
  };

  // ---- AI claim recall (llama mode only) ----
  const expectedAi = groundTruth.expectedAiClaims ?? [];
  if (mode === "llama" && expectedAi.length > 0) {
    const claimResults = expectedAi.map((claim) => {
      const hit = aiRows.some((f) => {
        const subjectText = `${f.claimSubject ?? ""} ${f.rawValue ?? ""} ${f.evidenceText ?? ""}`.toLowerCase();
        if (!subjectText.includes(String(claim.subjectContains).toLowerCase())) return false;
        const qtyOk = claim.quantity === undefined || Number(f.quantity) === Number(claim.quantity);
        const unitOk = claim.unit === undefined || String(f.quantityUnit ?? "").toLowerCase() === String(claim.unit).toLowerCase();
        return qtyOk && unitOk;
      });
      return { claim: `${claim.subjectContains} ${claim.quantity ?? ""}${claim.unit ?? ""}`, found: hit };
    });
    metrics.aiClaims = {
      expected: claimResults.length,
      matched: claimResults.filter((c) => c.found).length,
      rate: claimResults.filter((c) => c.found).length / claimResults.length,
      results: claimResults
    };
  }

  // ---- matches ----
  const matchesFinal = raw.matchesFinal ?? [];
  const expectedMatches = (groundTruth.expectedMatchProposals ?? []).map((m) => {
    const pairKey = unorderedPair(m.pairUnordered[0], m.pairUnordered[1]);
    const found = matchesFinal.find((x) => unorderedPair(x.leftValue, x.rightValue) === pairKey);
    return {
      pairKey,
      found: Boolean(found),
      state: found?.state ?? null,
      stateOk: found ? String(found.state).toLowerCase() === String(m.finalStateAfterScript ?? m.initialState).toLowerCase() : false
    };
  });
  const forbiddenPairs = [];
  for (const fp of groundTruth.forbiddenMatchPairs ?? []) {
    const pairKey = unorderedPair(fp.pairUnordered[0], fp.pairUnordered[1]);
    const found = matchesFinal.find((x) => unorderedPair(x.leftValue, x.rightValue) === pairKey);
    if (found) forbiddenPairs.push({ pairKey, state: found.state });
  }
  metrics.matches = {
    totalFinal: matchesFinal.length,
    expected: expectedMatches,
    forbiddenPairsFound: forbiddenPairs
  };

  // ---- findings ----
  const findingsOf = (list) =>
    (list ?? []).map((f) => ({ kind: f.kind, relatedEntityId: f.relatedEntityId ?? null, status: f.status, title: f.title }));
  const checkKinds = (expectedList, actualList) =>
    (expectedList ?? []).map((e) => ({
      kind: e.kind,
      relatedEntityId: e.relatedEntityId ?? null,
      found: (actualList ?? []).some(
        (a) => a.kind === e.kind && (!e.relatedEntityId || String(a.relatedEntityId ?? "").toLowerCase() === e.relatedEntityId.toLowerCase())
      )
    }));
  const preList = findingsOf(raw.findingsPreDecisions).filter((f) => f.status === "Open");
  const finalList = findingsOf(raw.findingsFinal).filter((f) => f.status === "Open");
  const mustNot = (groundTruth.expectedFindings?.mustNotExistKinds ?? []).map((kind) => ({
    kind,
    violated: [...preList, ...finalList].some((f) => f.kind === kind)
  }));
  metrics.findings = {
    preReviewOpen: preList,
    finalOpen: finalList,
    expectedPreReview: checkKinds(groundTruth.expectedFindings?.preReviewKindsWithEntity, preList),
    expectedPostAlias: groundTruth.expectedFindings?.postAliasKindsWithEntity
      ? checkKinds(groundTruth.expectedFindings.postAliasKindsWithEntity, finalList)
      : null,
    mustNotExist: mustNot
  };

  // ---- traces ----
  const traceChecks = {};
  const expectedTraces = groundTruth.expectedTrace ?? {};
  for (const [label, expected] of Object.entries(expectedTraces)) {
    const trace = raw.tracesPreDecisions?.[label] ?? raw.tracesFinal?.[label];
    if (!trace) {
      traceChecks[label] = { status: "TRACE_MISSING" };
      continue;
    }
    const scope = tracePossibleScope(trace);
    const stateCountsActual = trace.stateCounts ?? {};
    const stateCountsOk =
      !expected.stateCounts ||
      Object.entries(expected.stateCounts).every(([k, v]) => Number(stateCountsActual[k] ?? 0) === Number(v));
    const mb = trace.massBalance ?? {};
    const massOk =
      !expected.massBalanceStatus || String(mb.status ?? "").toLowerCase() === String(expected.massBalanceStatus).toLowerCase();
    const massInputOk =
      !expected.massBalanceInput ||
      (Number(mb.inputTotal) === Number(expected.massBalanceInput.value) && String(mb.inputUnit).toLowerCase() === expected.massBalanceInput.unit.toLowerCase());
    const massOutputOk =
      !expected.massBalanceOutput ||
      (Number(mb.outputTotal) === Number(expected.massBalanceOutput.value) && String(mb.outputUnit).toLowerCase() === expected.massBalanceOutput.unit.toLowerCase());
    traceChecks[label] = {
      status: "checked",
      confirmedFinishedLotsOk: sameSet(scope.confirmedFinishedLots, expected.confirmedFinishedLots ?? []),
      confirmedCustomersOk: sameSet(scope.confirmedCustomers, expected.confirmedCustomers ?? []),
      possibleFinishedLotsOk: sameSet(scope.possibleFinishedLots, expected.possiblyReachableFinishedLots ?? []),
      possibleCustomersOk: sameSet(scope.possibleCustomers, expected.possiblyReachableCustomers ?? []),
      mustNotReachOk: (expected.mustNotReach ?? []).every((id) => !scope.nodes.some((n) => n.endsWith(`:${id}`))),
      stateCountsOk,
      massBalanceStatusOk: massOk,
      massBalanceInputOk: massInputOk,
      massBalanceOutputOk: massOutputOk,
      actual: {
        ...scope,
        stateCounts: stateCountsActual,
        massBalance: { status: mb.status, inputTotal: mb.inputTotal, inputUnit: mb.inputUnit, outputTotal: mb.outputTotal, outputUnit: mb.outputUnit, notes: mb.notes }
      }
    };
  }
  metrics.traces = traceChecks;

  const allChecks = [
    metrics.links.missingRequired.length === 0,
    metrics.links.unexpectedLinks.length === 0,
    metrics.links.expectedNoiseMatched.every((n) => n.stateOk),
    metrics.forbiddenLinksFound.length === 0,
    metrics.facts.verbatimTraps.every((t) => t.foundVerbatim),
    mode !== "llama" || metrics.facts.aiRowCount > 0,
    mode !== "deterministic_only" || metrics.facts.aiRowCount === 0,
    metrics.matches.expected.every((m) => m.found && m.stateOk),
    metrics.matches.forbiddenPairsFound.length === 0,
    metrics.findings.expectedPreReview.every((f) => f.found),
    (metrics.findings.expectedPostAlias ?? []).every((f) => f.found),
    metrics.findings.mustNotExist.every((m) => !m.violated),
    Object.values(traceChecks).every(
      (t) =>
        t.status === "checked" &&
        t.confirmedFinishedLotsOk &&
        t.confirmedCustomersOk &&
        t.possibleFinishedLotsOk &&
        t.possibleCustomersOk &&
        t.mustNotReachOk &&
        t.stateCountsOk &&
        t.massBalanceStatusOk &&
        t.massBalanceInputOk &&
        t.massBalanceOutputOk
    )
  ];
  metrics.verdict = {
    checksPassed: allChecks.filter(Boolean).length,
    checksTotal: allChecks.length,
    passed: allChecks.every(Boolean)
  };
  return metrics;
}

async function runSingle({ scenario, mode, runId, skipBuild }) {
  const scenarioDir = path.join(SCENARIOS_ROOT, scenario);
  const manifest = await readJson(path.join(scenarioDir, "manifest.json"));
  const groundTruth = await readJson(path.join(scenarioDir, "ground-truth.json"));

  const runDir = path.join(RUNS_ROOT, runId, scenario, mode);
  const rawDir = path.join(runDir, "raw");
  const dataRoot = path.join(REPO_ROOT, "data", "evaluation", "runs", runId, scenario, mode);
  await mkdir(rawDir, { recursive: true });
  await rm(dataRoot, { recursive: true, force: true });
  await mkdir(dataRoot, { recursive: true });

  const hashChecks = [];
  for (const doc of manifest.documents) {
    const bytes = await readFile(path.join(scenarioDir, "documents", doc.fileName));
    const actual = sha256(bytes);
    hashChecks.push({ fileName: doc.fileName, expected: doc.sha256, actual, ok: actual === doc.sha256 });
  }
  if (hashChecks.some((h) => !h.ok)) {
    throw new Error(`Manifest integrity failure for ${scenario}: ${hashChecks.filter((h) => !h.ok).map((h) => h.fileName).join(", ")}`);
  }

  const port = mode === "llama" ? 5321 : 5311;
  const api = startApi({ dataRoot, port, mode, logDir: runDir });
  const client = new ApiClient(`http://127.0.0.1:${port}`);
  try {
    const health = await waitForHealth(client);
    await writeJson(path.join(rawDir, "health.json"), health);
    const raw = await executeScenario(client, scenario, manifest, groundTruth, mode, rawDir);
    const metrics = computeMetrics({ scenario, mode, manifest, groundTruth, raw, hashChecks });
    await writeJson(path.join(runDir, "metrics.json"), metrics);
    console.log(
      `[run-scenario] ${scenario} / ${mode}: verdict ${metrics.verdict.passed ? "PASS" : "FAIL"} (${metrics.verdict.checksPassed}/${metrics.verdict.checksTotal} checks)`
    );
    if (!metrics.verdict.passed) {
      console.log(JSON.stringify({
        missingRequired: metrics.links.missingRequired,
        unexpectedLinks: metrics.links.unexpectedLinks,
        forbiddenLinksFound: metrics.forbiddenLinksFound,
        matches: metrics.matches,
        findings: metrics.findings,
        traces: Object.fromEntries(Object.entries(metrics.traces).map(([k, v]) => [k, { status: v.status, confirmedFinishedLotsOk: v.confirmedFinishedLotsOk, possibleFinishedLotsOk: v.possibleFinishedLotsOk, stateCountsOk: v.stateCountsOk, massBalanceStatusOk: v.massBalanceStatusOk, massBalanceInputOk: v.massBalanceInputOk, massBalanceOutputOk: v.massBalanceOutputOk }]))
      }, null, 2));
    }
    return metrics;
  } finally {
    await api.stop();
  }
}

async function buildSuiteSummary(runId, modes, scenarios) {
  const orchestration = {
    runId,
    generatedAt: new Date().toISOString(),
    modes,
    scenarios,
    perRun: {},
    modelValue: []
  };
  for (const scenario of scenarios) {
    for (const mode of ["deterministic_only", "llama"]) {
      const metricsPath = path.join(RUNS_ROOT, runId, scenario, mode, "metrics.json");
      try {
        orchestration.perRun[`${scenario}/${mode}`] = await readJson(metricsPath);
      } catch {
        orchestration.perRun[`${scenario}/${mode}`] = null;
      }
    }
    const det = orchestration.perRun[`${scenario}/deterministic_only`];
    const llama = orchestration.perRun[`${scenario}/llama`];
    const entry = {
      scenario,
      deterministicOnly: det ? summarizeMode(det) : null,
      llama: llama ? summarizeMode(llama) : null,
      aiAddedValue: det && llama ? compareModes(det, llama) : null
    };
    orchestration.modelValue.push(entry);
  }
  orchestration.verdicts = Object.fromEntries(Object.entries(orchestration.perRun).map(([k, v]) => [k, v?.verdict ?? null]));
  await writeJson(path.join(REPO_ROOT, "results", "evaluation", "model-value-evaluation.json"), {
    schemaVersion: "1.0",
    runId,
    generatedAt: new Date().toISOString(),
    entries: orchestration.modelValue,
    verdicts: orchestration.verdicts
  });

  const csvRows = [
    ["scenario", "mode", "verdict", "factsTotal", "aiRows", "tokenRecallRate", "quantityRecallRate", "aiClaimRecallRate", "requiredLinksMatched", "requiredLinksTotal", "forbiddenLinksFound"]
  ];
  for (const entry of orchestration.modelValue) {
    for (const [modeKey, m] of [["deterministic_only", entry.deterministicOnly], ["llama", entry.llama]]) {
      if (!m) {
        csvRows.push([entry.scenario, modeKey, "MISSING", "", "", "", "", "", "", "", ""]);
        continue;
      }
      csvRows.push([
        entry.scenario,
        modeKey,
        m.verdict.passed ? "PASS" : "FAIL",
        m.facts.total,
        m.facts.aiRowCount,
        m.facts.tokenRecallRate,
        m.facts.quantityRecallRate,
        m.aiClaimRecallRate ?? "",
        m.links.requiredMatched,
        m.links.requiredTotal,
        m.forbiddenLinksFound
      ]);
    }
  }
  const csv = csvRows.map((r) => r.map((v) => (v === null || v === undefined ? "" : String(v))).join(",")).join("\n");
  await writeFile(path.join(REPO_ROOT, "results", "evaluation", "model-value-evaluation.csv"), csv, "utf8");
  await writeJson(path.join(RUNS_ROOT, runId, "orchestration.json"), orchestration);
  return orchestration;
}

function summarizeMode(m) {
  return {
    verdict: m.verdict,
    provider: m.provider,
    facts: {
      total: m.facts.total,
      byExtractionMethod: m.facts.byExtractionMethod,
      aiRowCount: m.facts.aiRowCount,
      aiRowsWithQuantity: m.facts.aiRowsWithQuantity,
      aiRowsWithSubjectAndObject: m.facts.aiRowsWithSubjectAndObject,
      tokenRecallRate: m.facts.tokenRecallRate,
      quantityRecallRate: m.facts.quantityRecallRate,
      locatorPresentRate: m.facts.locatorPresentRate,
      missingTokens: m.facts.tokenRecall.filter((t) => !t.found).map((t) => t.token)
    },
    links: m.links,
    matches: m.matches,
    findings: m.findings,
    traces: m.traces,
    aiClaimRecallRate: m.aiClaims?.rate ?? null
  };
}

function compareModes(det, llama) {
  const detTokens = new Set((det.facts.tokenRecall ?? []).filter((t) => t.found).map((t) => t.token.toLowerCase()));
  const llamaTokens = new Set((llama.facts.tokenRecall ?? []).filter((t) => t.found).map((t) => t.token.toLowerCase()));
  const onlyInLlama = [...llamaTokens].filter((t) => !detTokens.has(t));
  const onlyInDeterministic = [...detTokens].filter((t) => !llamaTokens.has(t));
  return {
    aiExtractionRows: llama.facts.aiRowCount,
    aiRowsWithQuantity: llama.facts.aiRowsWithQuantity,
    aiRowsWithSubjectAndObject: llama.facts.aiRowsWithSubjectAndObject,
    aiClaimRecallRate: llama.aiClaims?.rate ?? null,
    tokensOnlyInLlamaMode: onlyInLlama,
    tokensOnlyInDeterministicMode: onlyInDeterministic,
    deterministicVerdict: det.verdict,
    llamaVerdict: llama.verdict,
    llamaUnexpectedLinks: llama.links.unexpectedLinks,
    deterministicUnexpectedLinks: det.links.unexpectedLinks,
    note: "Deterministic mode produces no AI-extraction rows by design (EXTRACTION_MODE=deterministic_only); AI claim recall is null there."
  };
}

async function main() {
  const args = parseArgs(process.argv.slice(2));
  const runId = args.runId ?? `eval-${new Date().toISOString().replace(/[:.]/g, "-")}`;

  if (!args.skipBuild) {
    console.log("[run-scenario] building Release API binary…");
    const build = spawnSync("dotnet", ["build", path.join(REPO_ROOT, "RecallGraph.slnx"), "-c", "Release", "--nologo", "-v", "q"], {
      cwd: REPO_ROOT,
      stdio: "inherit",
      windowsHide: true
    });
    if (build.status !== 0) throw new Error("Release build failed; aborting evaluation runs.");
  }
  if (!(await readFile(API_EXE).catch(() => null))) {
    throw new Error(`API binary not found at ${API_EXE}. Run dotnet build -c Release first.`);
  }

  if (args.scenario && args.mode) {
    await runSingle({ scenario: args.scenario, mode: args.mode, runId, skipBuild: true });
    return;
  }

  const scenarios = args.scenario ? [args.scenario] : await listScenarioIds();
  const modes = args.modes;
  const results = [];
  for (const scenario of scenarios) {
    for (const mode of modes) {
      results.push(await runSingle({ scenario, mode, runId, skipBuild: true }));
    }
  }
  const orchestration = await buildSuiteSummary(runId, modes, scenarios);
  const failures = Object.entries(orchestration.verdicts).filter(([, v]) => !v?.passed);
  console.log(`[run-scenario] suite complete: ${results.filter((r) => r.verdict.passed).length}/${results.length} runs passed.`);
  if (failures.length > 0) {
    console.log(`[run-scenario] failing runs: ${failures.map(([k]) => k).join(", ")}`);
    process.exitCode = 1;
  }
}

main().catch((e) => {
  console.error(`[run-scenario] fatal: ${e.message}`);
  console.error(e.stack);
  process.exitCode = 1;
});
