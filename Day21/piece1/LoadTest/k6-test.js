// QuotesApi HybridCache k6 Load Test
// Run: k6 run LoadTest/k6-test.js
//      k6 run LoadTest/k6-test.js -e BASE_URL=http://localhost:5051
//
// Phases
// ──────
// Phase 1  Cold-cache STAMPEDE   50 VUs, cache empty  → expect 1 DB query
// Phase 2  Warm-cache concurrent 50 VUs, cache warm   → expect 0 DB queries
// Phase 3  No-cache baseline      1 VU,  20 iters, evict each time → 20 DB queries
// Phase 4  Paged-list stampede   30 VUs, list cache empty → expect 1 DB query
// Phase 5  Warm-cache sequential  1 VU,  20 iters, cache warm → 0 DB queries

import http from 'k6/http';
import { check } from 'k6';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5051';

export const options = {
  scenarios: {
    phase1_cold_stampede: {
      executor: 'shared-iterations',
      vus: 50,
      iterations: 50,
      maxDuration: '30s',
      exec: 'phase1',
      startTime: '2s',
    },
    phase2_warm_concurrent: {
      executor: 'shared-iterations',
      vus: 50,
      iterations: 50,
      maxDuration: '30s',
      exec: 'phase2',
      startTime: '40s',
    },
    phase3_no_cache_baseline: {
      executor: 'per-vu-iterations',
      vus: 1,
      iterations: 20,
      maxDuration: '120s',
      exec: 'phase3',
      startTime: '80s',
    },
    phase4_list_stampede: {
      executor: 'shared-iterations',
      vus: 30,
      iterations: 30,
      maxDuration: '30s',
      exec: 'phase4',
      startTime: '145s',
    },
    phase5_warm_sequential: {
      executor: 'per-vu-iterations',
      vus: 1,
      iterations: 20,
      maxDuration: '60s',
      exec: 'phase5',
      startTime: '185s',
    },
  },
  thresholds: {
    'http_req_failed': ['rate<0.01'],
  },
};

// ── setup: runs once before all VUs ──────────────────────────────────────────

export function setup() {
  const listRes = http.get(`${BASE_URL}/api/quotes?page=1&size=1`);
  const quotes = listRes.json();
  const quoteId = quotes[0].id;

  const info = http.get(`${BASE_URL}/diag/cache-info`).json();

  console.log(`\n╔══════════════════════════════════════════════════╗`);
  console.log(`║   QuotesApi  ·  HybridCache  ·  k6 Load Test    ║`);
  console.log(`╚══════════════════════════════════════════════════╝`);
  console.log(`  Target   : ${BASE_URL}`);
  console.log(`  Quote ID : ${quoteId}`);
  console.log(`  L1       : ${info.l1}`);
  console.log(`  L2       : ${info.l2}`);
  console.log(`  Redis    : ${info.redisActive ? 'YES ✓' : 'NO  (in-memory fallback)'}`);
  console.log(`  Stampede : ${info.stampede}`);

  // Evict and reset so Phase 1 starts cold
  http.post(`${BASE_URL}/diag/cache/evict/${quoteId}`, null);
  http.post(`${BASE_URL}/diag/db-queries/reset`, null);
  console.log(`\n  Cache evicted · counter reset · starting phases …\n`);

  return { quoteId };
}

// ── Phase 1: Cold-cache STAMPEDE ─────────────────────────────────────────────
// 50 VUs all fire simultaneously on a cold cache.
// HybridCache coalesces: only 1 factory reaches the DB.

export function phase1(data) {
  const res = http.get(`${BASE_URL}/api/quotes/${data.quoteId}`);
  check(res, { 'phase1 200': r => r.status === 200 });
}

// ── Phase 2: Warm-cache concurrent ───────────────────────────────────────────
// Cache is warm from Phase 1. 50 VUs — expect 0 DB queries.

export function phase2(data) {
  // VU 1 on its first iteration: log phase 1 result and reset the counter.
  if (__VU === 1 && __ITER === 0) {
    const count = http.get(`${BASE_URL}/diag/db-queries`).json().count;
    console.log(`  Phase 1 DB queries : ${count}  (expected 1 — stampede coalesced 50 → 1)`);
    http.post(`${BASE_URL}/diag/db-queries/reset`, null);
  }
  const res = http.get(`${BASE_URL}/api/quotes/${data.quoteId}`);
  check(res, { 'phase2 200': r => r.status === 200 });
}

// ── Phase 3: No-cache baseline (sequential, evict before each) ───────────────
// 1 VU, 20 iterations. Cache cleared before every request → every request hits DB.

export function phase3(data) {
  if (__ITER === 0) {
    const count = http.get(`${BASE_URL}/diag/db-queries`).json().count;
    console.log(`  Phase 2 DB queries : ${count}  (expected 0 — all from L1 cache)`);
    http.post(`${BASE_URL}/diag/db-queries/reset`, null);
  }
  // Evict before every request to force a DB hit each time
  http.post(`${BASE_URL}/diag/cache/evict/${data.quoteId}`, null);
  const res = http.get(`${BASE_URL}/api/quotes/${data.quoteId}`);
  check(res, { 'phase3 200': r => r.status === 200 });
}

// ── Phase 4: Paged-list STAMPEDE ─────────────────────────────────────────────
// 30 VUs hit GET /api/quotes?page=1 simultaneously on a cold list cache.

export function phase4(data) {
  if (__VU === 1 && __ITER === 0) {
    const count = http.get(`${BASE_URL}/diag/db-queries`).json().count;
    console.log(`  Phase 3 DB queries : ${count}  (expected 20 — every request hit DB)`);
    http.post(`${BASE_URL}/diag/cache/evict-lists`, null);
    http.post(`${BASE_URL}/diag/db-queries/reset`, null);
  }
  const res = http.get(`${BASE_URL}/api/quotes?page=1&size=10`);
  check(res, { 'phase4 200': r => r.status === 200 });
}

// ── Phase 5: Warm-cache sequential (fair compare with Phase 3) ───────────────
// 1 VU, 20 iterations. Cache warm — every request served from L1.
// p99 here vs Phase 3 p99 = real latency speedup.

export function phase5(data) {
  if (__ITER === 0) {
    const count = http.get(`${BASE_URL}/diag/db-queries`).json().count;
    console.log(`  Phase 4 DB queries : ${count}  (expected 1 — list stampede coalesced)`);
    // Warm the individual quote cache entry
    http.post(`${BASE_URL}/diag/cache/evict/${data.quoteId}`, null);
    http.get(`${BASE_URL}/api/quotes/${data.quoteId}`); // prime L1
    http.post(`${BASE_URL}/diag/db-queries/reset`, null);
  }
  const res = http.get(`${BASE_URL}/api/quotes/${data.quoteId}`);
  check(res, { 'phase5 200': r => r.status === 200 });
}

// ── teardown: runs once after all VUs ────────────────────────────────────────

export function teardown(data) {
  const count = http.get(`${BASE_URL}/diag/db-queries`).json().count;
  console.log(`  Phase 5 DB queries : ${count}  (expected 0 — all from L1 memory)`);
  console.log(`\n  k6 summary above shows p50/p99 per scenario.`);
  console.log(`  Compare phase3_no_cache_baseline vs phase5_warm_sequential for true speedup.\n`);
}
