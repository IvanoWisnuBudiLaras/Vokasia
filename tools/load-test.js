import http from "k6/http";
import { check } from "k6";
export const options = {
  scenarios: { journal_burst: { executor: "constant-arrival-rate", rate: 50, timeUnit: "1s", duration: "5m", preAllocatedVUs: 100, maxVUs: 500 } },
  thresholds: { http_req_failed: ["rate==0"], http_req_duration: ["p(95)<300"] },
};

export default function () {
  const url = __ENV.K6_JOURNAL_URL;
  const token = __ENV.K6_BEARER_TOKEN;
  const slotIds = (__ENV.K6_SLOT_IDS || __ENV.K6_SLOT_ID || "").split(",").map((value) => value.trim()).filter(Boolean);
  if (!url || !token || slotIds.length === 0) throw new Error("K6_JOURNAL_URL, K6_BEARER_TOKEN, and K6_SLOT_IDS are required.");
  const slotId = slotIds[(__ITER + __VU) % slotIds.length];
  const response = http.post(url, JSON.stringify({ slotId, text: "Deterministic release verification journal.", competencyIds: [], photoIds: [] }), { headers: { Authorization: `Bearer ${token}`, "Content-Type": "application/json" } });
  check(response, { "journal submit accepted": (r) => r.status >= 200 && r.status < 300 });
}
