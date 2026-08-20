const http = require("http");
const { exec } = require("child_process");
const path = require("path");

const PORT = 8080;
const PROJECT_ROOT = process.env.WORKSPACE_DIR || path.dirname(path.dirname(__dirname));
const COMPOSE_FILE = process.env.COMPOSE_FILE || path.join(PROJECT_ROOT, "docker-compose.yml");
const PROJECT_NAME = process.env.COMPOSE_PROJECT || "vokasia";

// ---------- helpers ----------
// Use docker compose with project name filter (labels already on containers).
// This avoids needing -f <path> inside a container where the compose file
// may not be visible (Docker Desktop WSL2 bind mount quirks).
function runDocker(args) {
  return new Promise((resolve) => {
    exec(`docker compose -p ${PROJECT_NAME} ${args}`, { maxBuffer: 1024 * 1024 * 2 }, (error, stdout, stderr) => {
      resolve({ stdout: stdout || "", stderr: stderr || "", returncode: error ? (error.code || 1) : 0 });
    });
  });
}

// Fallback: use docker ps with label filter (works even without compose file)
function runDockerPs() {
  return new Promise((resolve) => {
    exec(`docker ps --filter "label=com.docker.compose.project=${PROJECT_NAME}" --format json`, { maxBuffer: 1024 * 1024 * 2 }, (error, stdout, stderr) => {
      resolve({ stdout: stdout || "", stderr: stderr || "", returncode: error ? (error.code || 1) : 0 });
    });
  });
}

function runDockerRaw(args) {
  return new Promise((resolve) => {
    exec(`docker ${args}`, { maxBuffer: 1024 * 1024 * 2 }, (error, stdout, stderr) => {
      resolve({ stdout: stdout || "", stderr: stderr || "", returncode: error ? (error.code || 1) : 0 });
    });
  });
}

function runTest() {
  return new Promise((resolve) => {
    // Run dotnet test via SDK container with backend source mounted from host
    const cmd = `docker run --rm -v I:/Web/Vokasia/backend:/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 sh -c "dotnet test --no-restore 2>&1 | tail -30"`;
    exec(cmd, { maxBuffer: 1024 * 1024 * 2 }, (error, stdout, stderr) => {
      resolve({ stdout: stdout || "", stderr: stderr || "", returncode: error ? (error.code || 1) : 0 });
    });
  });
}

function runPsql(query) {
  return new Promise((resolve) => {
    const escapedQuery = query.replace(/"/g, '\\"');
    const cmd = `docker exec vokasia-postgres-1 psql -U vokasia -d vokasia -c "${escapedQuery}"`;
    exec(cmd, { maxBuffer: 1024 * 1024 }, (error, stdout, stderr) => {
      resolve({ stdout: stdout || "", stderr: stderr || "", returncode: error ? (error.code || 1) : 0 });
    });
  });
}

function apiCall(method, path, body) {
  return new Promise((resolve) => {
    const options = {
      hostname: "localhost",
      port: 5000,
      path,
      method,
      headers: { "Content-Type": "application/json" }
    };
    const req = http.request(options, (res) => {
      let data = "";
      res.on("data", chunk => data += chunk);
      res.on("end", () => {
        try { resolve({ status: res.statusCode, data: JSON.parse(data) }); }
        catch { resolve({ status: res.statusCode, data }); }
      });
    });
    req.on("error", (e) => resolve({ status: 0, error: e.message }));
    if (body) req.write(JSON.stringify(body));
    req.end();
  });
}

const server = http.createServer(async (req, res) => {
  const url = new URL(req.url, "http://localhost");
  res.setHeader("Access-Control-Allow-Origin", "*");
  res.setHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
  res.setHeader("Access-Control-Allow-Headers", "Content-Type");
  if (req.method === "OPTIONS") { res.writeHead(200); res.end(); return; }

  // API: status
  if (url.pathname === "/api/status") {
    // Try compose ps first, fallback to docker ps with label filter
    let r = await runDocker("ps --format json");
    if (r.returncode !== 0 || !r.stdout.trim()) {
      r = await runDockerPs();
    }
    const containers = [];
    if (r.returncode === 0 && r.stdout) {
      r.stdout.trim().split("\n").forEach(line => {
        if (line.trim()) {
          try {
            const obj = JSON.parse(line);
            // For docker ps (non-compose), extract service from labels
            let service = obj.Service || obj.Names || "?";
            if (!obj.Service && obj.Labels) {
              const labels = obj.Labels.split(",");
              const svcLabel = labels.find(l => l.includes("com.docker.compose.service="));
              if (svcLabel) service = svcLabel.split("=")[1];
            }
            containers.push({
              Service: service,
              Image: obj.Image || "",
              Status: obj.Status || "",
              State: obj.State || "stopped",
              Ports: obj.Ports || ""
            });
          } catch {}
        }
      });
    }
    res.writeHead(200, { "Content-Type": "application/json" });
    res.end(JSON.stringify({ containers }));
    return;
  }

  // API: logs
  if (url.pathname.startsWith("/api/logs/")) {
    const service = url.pathname.substring(10);
    // Map service name to container name
    const containerName = `vokasia-${service}-1`;
    const r = await runDockerRaw(`logs --tail 50 ${containerName}`);
    res.writeHead(200, { "Content-Type": "application/json" });
    res.end(JSON.stringify(r));
    return;
  }

  // API: up/down
  if (url.pathname === "/api/up" && req.method === "POST") {
    let body = "";
    req.on("data", chunk => body += chunk);
    req.on("end", async () => {
      const data = JSON.parse(body || "{}");
      const service = data.service || "";
      // Use docker compose -p vokasia (project name) - works without compose file path
      const r = await runDocker(`up -d ${service}`);
      res.writeHead(200, { "Content-Type": "application/json" });
      res.end(JSON.stringify(r));
    });
    return;
  }
  if (url.pathname === "/api/down" && req.method === "POST") {
    const r = await runDocker("down");
    res.writeHead(200, { "Content-Type": "application/json" });
    res.end(JSON.stringify(r));
    return;
  }

  // API: test
  if (url.pathname === "/api/test" && req.method === "POST") {
    const r = await runTest();
    res.writeHead(200, { "Content-Type": "application/json" });
    res.end(JSON.stringify(r));
    return;
  }

  // === NEW: Send test email to mentor ===
  if (url.pathname === "/api/send-email" && req.method === "POST") {
    const r = await apiCall("POST", "/api/notifications/test-email", { Type: "mentor-invite" });
    res.writeHead(200, { "Content-Type": "application/json" });
    res.end(JSON.stringify(r));
    return;
  }

  // === NEW: Magic Link flow trace ===
  if (url.pathname === "/api/magic-link-flow" && req.method === "POST") {
    // Step 1: Check if API is up
    const healthCheck = await apiCall("GET", "/health");
    if (healthCheck.status !== 200) {
      res.writeHead(200, { "Content-Type": "application/json" });
      res.end(JSON.stringify({ error: "API not ready. Start servers first.", healthCheck }));
      return;
    }
    // Step 2: Check .emails folder
    const emailsBefore = await runDockerRaw(`exec vokasia-api-1 ls /app/.emails/ 2>/dev/null || echo "no .emails dir"`);
    // Step 3: Call the test-email endpoint
    const emailResult = await apiCall("POST", "/api/notifications/test-email", { Type: "magic-link" });
    // Step 4: Check emails after
    const emailsAfter = await runDockerRaw(`exec vokasia-api-1 ls /app/.emails/ 2>/dev/null || echo "no .emails dir"`);
    // Step 5: Check worker logs for email consumer
    const workerLogs = await runDockerRaw(`logs --tail 20 vokasia-worker-1 2>&1 || echo "worker not running"`);
    // Step 6: Check SentEmail table
    const dbCheck = await runPsql("SELECT COUNT(*) FROM \"SentEmails\"");
    // Step 7: Check idempotent email log
    const apiLogs = await runDockerRaw(`logs --tail 10 vokasia-api-1 2>&1 || echo "api not running"`);

    res.writeHead(200, { "Content-Type": "application/json" });
    res.end(JSON.stringify({
      steps: [
        { step: "1. API Health", result: healthCheck.status === 200 ? "OK" : "FAIL" },
        { step: "2. .emails folder before", result: emailsBefore.stdout },
        { step: "3. POST /api/notifications/test-email", result: emailResult },
        { step: "4. .emails folder after", result: emailsAfter.stdout },
        { step: "5. Worker logs (email consumer)", result: workerLogs.stdout },
        { step: "6. SentEmails table count", result: dbCheck.stdout },
        { step: "7. API logs (DevLogEmailSender)", result: apiLogs.stdout }
      ]
    }));
    return;
  }

  // === NEW: Custom SQL query ===
  if (url.pathname === "/api/run-query" && req.method === "POST") {
    let body = "";
    req.on("data", chunk => body += chunk);
    req.on("end", async () => {
      const { query = "" } = JSON.parse(body || "{}");
      const r = await runPsql(query);
      res.writeHead(200, { "Content-Type": "application/json" });
      res.end(JSON.stringify(r));
    });
    return;
  }

  // === NEW: Table coverage ===
  if (url.pathname === "/api/table-coverage") {
    const q = `
      SELECT schemaname, tablename, n_live_tup, n_dead_tup, seq_scan, idx_scan
      FROM pg_stat_user_tables
      ORDER BY n_live_tup DESC;
    `;
    const r = await runPsql(q);
    res.writeHead(200, { "Content-Type": "application/json" });
    res.end(JSON.stringify(r));
    return;
  }

  // === NEW: Brute force rate limit test ===
  if (url.pathname === "/api/rate-limit-test" && req.method === "POST") {
    let body = "";
    req.on("data", chunk => body += chunk);
    req.on("end", async () => {
      const { count = 10, email = "brute@test.com" } = JSON.parse(body || "{}");
      const results = [];
      const start = Date.now();
      for (let i = 0; i < count; i++) {
        const r = await apiCall("POST", "/account/login", { email, password: "wrong-password" });
        results.push({
          attempt: i + 1,
          status: r.status,
          time: Date.now() - start,
          limited: r.status === 429
        });
        if (r.status === 429) break;
      }
      const elapsed = Date.now() - start;
      res.writeHead(200, { "Content-Type": "application/json" });
      res.end(JSON.stringify({
        config: { email, totalAttempts: count, rateLimit: "5/mnt per IP+email" },
        limitedAfter: results.find(r => r.limited)?.attempt || null,
        totalSent: results.length,
        // If we hit 429, remaining results are simulated
        results,
        elapsedMs: elapsed
      }));
    });
    return;
  }

  // === NEW: Seed demo data ===
  if (url.pathname === "/api/seed-demo" && req.method === "POST") {
    let body = "";
    req.on("data", chunk => body += chunk);
    req.on("end", async () => {
      const { force = false } = JSON.parse(body || "{}");
      // Run dotnet seed via the API container
      const forceArg = force ? " --force" : "";
      const r = await runDockerRaw(`exec vokasia-api-1 sh -c "dotnet Vokasia.Api.dll${forceArg}" 2>&1 || echo 'API container not ready'`);
      res.writeHead(200, { "Content-Type": "application/json" });
      res.end(JSON.stringify({ result: r.stdout || r.stderr }));
    });
    return;
  }

  // UI
  res.writeHead(200, { "Content-Type": "text/html" });
  res.end(HTML);
});

server.listen(PORT, "0.0.0.0", () => {
  console.log("Vokasia Dev Dashboard running at http://localhost:" + PORT);
});

// ---------- HTML ----------
const HTML = `
<!DOCTYPE html>
<html>
<head>
    <title>Vokasia Dev Dashboard</title>
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <style>
        body { margin:0; font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif; background:#0f172a; color:#e2e8f0; }
        .header { background:#1e293b; padding:1rem 2rem; border-bottom:1px solid #334155; }
        .header h1 { margin:0; font-size:1.5rem; color:#38bdf8; }
        .container { max-width:1400px; margin:0 auto; padding:1rem; }
        .grid { display:grid; grid-template-columns:repeat(auto-fit,minmax(300px,1fr)); gap:1rem; }
        .card { background:#1e293b; border-radius:8px; padding:1.5rem; border:1px solid #334155; margin-top:1rem; }
        .card h3 { margin-top:0; color:#94a3b8; font-size:.875rem; text-transform:uppercase; letter-spacing:.05em; }
        .card-title { color:#38bdf8; font-size:1.1rem; font-weight:600; margin-bottom:1rem; }
        .dot { width:10px; height:10px; border-radius:50%; display:inline-block; }
        .running { background:#22c55e; }
        .stopped { background:#ef4444; }
        .btn { background:#0ea5e9; color:white; border:none; padding:.5rem 1rem; border-radius:6px; cursor:pointer; font-size:.875rem; margin:.25rem; text-decoration:none; display:inline-block; }
        .btn:hover { background:#0284c7; }
        .btn-danger { background:#ef4444; }
        .btn-danger:hover { background:#dc2626; }
        .btn-success { background:#22c55e; }
        .btn-success:hover { background:#16a34a; }
        .btn-warning { background:#f59e0b; color:#000; }
        .btn-warning:hover { background:#d97706; }
        .btn-purple { background:#8b5cf6; }
        .btn-purple:hover { background:#7c3aed; }
        .logs { background:#0f172a; border:1px solid #334155; border-radius:6px; padding:1rem; font-family:'Courier New',monospace; font-size:.75rem; max-height:500px; overflow-y:auto; white-space:pre-wrap; margin-top:1rem; }
        .srow { display:flex; justify-content:space-between; align-items:center; padding:.5rem 0; border-bottom:1px solid #334155; }
        .srow:last-child { border-bottom:none; }
        .refresh { float:right; font-size:.75rem; color:#64748b; }
        .tab { display:flex; gap:.5rem; margin-bottom:1rem; flex-wrap:wrap; }
        .tab button { background:#334155; color:#e2e8f0; border:none; padding:.5rem 1rem; border-radius:6px; cursor:pointer; }
        .tab button.active { background:#0ea5e9; }
        .badge { display:inline-block; padding:.15rem .5rem; border-radius:999px; font-size:.7rem; font-weight:600; }
        .badge-green { background:#22c55e; color:#000; }
        .badge-red { background:#ef4444; color:#fff; }
        .badge-yellow { background:#f59e0b; color:#000; }
        input, select { background:#0f172a; color:#e2e8f0; border:1px solid #334155; padding:.5rem; border-radius:6px; margin:.25rem; font-size:.875rem; }
        label { color:#94a3b8; font-size:.8rem; display:block; margin:.5rem 0 .25rem; }
        .step { border-left:2px solid #334155; padding-left:1rem; margin:.5rem 0; }
        .step-ok { border-left-color:#22c55e; }
        .step-fail { border-left-color:#ef4444; }
        .step-ok::before { content:"✓ "; color:#22c55e; }
        .step-fail::before { content:"✗ "; color:#ef4444; }
    </style>
</head>
<body>
<div class="header"><h1>Vokasia Dev Dashboard</h1></div>
<div class="container">

    <!-- Tab navigation -->
    <div class="tab">
        <button class="active" onclick="switchTab('monitor',this)">Monitor</button>
        <button onclick="switchTab('test',this)">Test Tools</button>
        <button onclick="switchTab('email',this)">Email & Magic Link</button>
        <button onclick="switchTab('rate',this)">Rate Limit</button>
        <button onclick="switchTab('db',this)">Database</button>
    </div>

    <!-- Tab: Monitor -->
    <div id="tab-monitor" class="tab-content">
        <div class="grid">
            <div class="card">
                <div class="card-title">Quick Actions</div>
                <button class="btn btn-success" onclick="dockerApi('up')">Start All</button>
                <button class="btn btn-danger" onclick="dockerApi('down')">Stop All</button>
                <button class="btn" onclick="loadStatus()">Refresh</button>
            </div>
            <div class="card">
                <div class="card-title">Links</div>
                <a href="http://localhost:3000" target="_blank" class="btn">Frontend :3000</a>
                <a href="http://localhost:5000" target="_blank" class="btn">API :5000</a>
                <a href="http://localhost:8025" target="_blank" class="btn">Mailpit :8025</a>
                <a href="http://localhost:9001" target="_blank" class="btn">MinIO :9001</a>
                <a href="http://localhost:15672" target="_blank" class="btn">RabbitMQ :15672</a>
            </div>
        </div>
        <div class="card">
            <h3>Containers <span class="refresh" id="lastUpdate"></span></h3>
            <div id="containers">Loading...</div>
        </div>
        <div class="card">
            <h3>Logs</h3>
            <button class="btn" onclick="showLogs('api')">API</button>
            <button class="btn" onclick="showLogs('worker')">Worker</button>
            <button class="btn" onclick="showLogs('frontend')">Frontend</button>
            <button class="btn" onclick="showLogs('postgres')">Postgres</button>
            <button class="btn" onclick="showLogs('rabbitmq')">RabbitMQ</button>
            <button class="btn" onclick="showLogs('minio')">MinIO</button>
            <button class="btn" onclick="showLogs('mailpit')">Mailpit</button>
            <div class="logs" id="logs">Select a service to view logs...</div>
        </div>
    </div>

    <!-- Tab: Test Tools -->
    <div id="tab-test" class="tab-content" style="display:none">
        <div class="grid">
            <div class="card">
                <div class="card-title">Unit Tests</div>
                <button class="btn" onclick="runTestSuite()">Run All Tests</button>
                <div class="logs" id="test-output">Click to run tests...</div>
            </div>
            <div class="card">
                <div class="card-title">Seed Demo Data</div>
                <button class="btn btn-warning" onclick="seedDemo(false)">Seed (Idempotent)</button>
                <button class="btn btn-danger" onclick="seedDemo(true)">Force Reset + Seed</button>
                <div class="logs" id="seed-output">Ready...</div>
            </div>
            <div class="card">
                <div class="card-title">Table Coverage</div>
                <button class="btn btn-purple" onclick="showTableCoverage()">Refresh Coverage</button>
                <div class="logs" id="table-coverage">Click to see table coverage...</div>
            </div>
        </div>
    </div>

    <!-- Tab: Email & Magic Link -->
    <div id="tab-email" class="tab-content" style="display:none">
        <div class="grid">
            <div class="card">
                <div class="card-title">Send Test Email (via API)</div>
                <button class="btn" onclick="sendTestEmail()">Send Mentor Invite Email</button>
                <div class="logs" id="email-output">Click to send test email...</div>
            </div>
            <div class="card">
                <div class="card-title">Magic Link Flow Trace</div>
                <button class="btn btn-purple" onclick="traceMagicLink()">Trace Full Flow</button>
                <div id="magic-link-output" style="margin-top:1rem;font-size:.85rem;color:#94a3b8;">Click to trace magic link flow...</div>
            </div>
            <div class="card">
                <div class="card-title">Mailpit</div>
                <p style="color:#94a3b8;font-size:.85rem;">Check captured emails in Mailpit UI</p>
                <a href="http://localhost:8025" target="_blank" class="btn">Open Mailpit</a>
                <button class="btn" onclick="showLogs('mailpit')">Mailpit Logs</button>
            </div>
        </div>
    </div>

    <!-- Tab: Rate Limit -->
    <div id="tab-rate" class="tab-content" style="display:none">
        <div class="grid">
            <div class="card">
                <div class="card-title">Rate Limit Brute Force</div>
                <label>Email</label>
                <input type="text" id="rate-email" value="guru0@10000001.vokasia.demo" />
                <label>Password (wrong to trigger limit)</label>
                <input type="text" id="rate-pass" value="wrong-password" />
                <label>Attempts</label>
                <input type="number" id="rate-count" value="10" min="1" max="20" />
                <button class="btn btn-danger" onclick="bruteForceTest()">Start Brute Force</button>
                <div id="rate-output" class="logs" style="margin-top:1rem;">Configure & click to start...</div>
            </div>
            <div class="card">
                <div class="card-title">Rate Limit Policy</div>
                <div style="color:#e2e8f0;font-size:.85rem;line-height:1.6">
                    <p><strong>Login Policy:</strong> 5 attempts/min per IP+email</p>
                    <p><strong>Public Policy:</strong> 10 requests/min per IP</p>
                    <p><strong>Global IP Limiter:</strong> 20 requests/min per IP on /account/login</p>
                    <p style="color:#94a3b8;margin-top:1rem;">
                    After 5 wrong attempts, you'll get <span class="badge badge-red">429</span> 
                    for that email+IP combination. The API sends a Retry-After header.
                    </p>
                </div>
            </div>
        </div>
    </div>

    <!-- Tab: Database -->
    <div id="tab-db" class="tab-content" style="display:none">
        <div class="grid">
            <div class="card">
                <div class="card-title">Database Tables</div>
                <button class="btn" onclick="showTableCoverage()">Refresh</button>
                <div class="logs" id="db-tables" style="max-height:600px">Click to load...</div>
            </div>
            <div class="card">
                <div class="card-title">Custom Query</div>
                <label>SQL Query</label>
                <input type="text" id="custom-query" value="SELECT tablename, tableowner FROM pg_tables WHERE schemaname='public' ORDER BY tablename;" style="width:100%;box-sizing:border-box;font-family:monospace;" />
                <button class="btn" onclick="runCustomQuery()">Run</button>
                <div class="logs" id="custom-query-output">Enter query above...</div>
            </div>
        </div>
    </div>

</div>

<script>
// Tabs
function switchTab(name, btn) {
    document.querySelectorAll('.tab-content').forEach(t => t.style.display = 'none');
    document.querySelectorAll('.tab button').forEach(b => b.classList.remove('active'));
    document.getElementById('tab-' + name).style.display = 'block';
    btn.classList.add('active');
}

// Monitor
async function loadStatus() {
    const r = await fetch('/api/status');
    const d = await r.json();
    const el = document.getElementById('containers');
    if (!d.containers || d.containers.length === 0) {
        el.innerHTML = '<div class="srow"><span>No containers running</span></div>';
        return;
    }
    el.innerHTML = d.containers.map(c => {
        const st = c.State === 'running' ? 'running' : 'stopped';
        return '<div class="srow">' +
            '<div><span class="dot ' + st + '"></span> <strong>' + c.Service + '</strong> <span style="color:#64748b;font-size:.75rem">' + c.Status + '</span></div>' +
            '<div><button class="btn" onclick="startService(\\'' + c.Service + '\\')">Start</button>' +
            '<button class="btn" onclick="showLogs(\\'' + c.Service + '\\')">Logs</button></div>' +
            '</div>';
    }).join('');
    document.getElementById('lastUpdate').textContent = 'Updated: ' + new Date().toLocaleTimeString();
}
async function dockerApi(a) {
    document.getElementById('logs').textContent = 'Executing ' + a + '...';
    const r = await fetch('/api/' + a, {method:'POST'});
    const d = await r.json();
    document.getElementById('logs').textContent = d.stdout || d.stderr || JSON.stringify(d,null,2);
    loadStatus();
}
async function startService(svc) {
    await fetch('/api/up', {method:'POST', headers:{'Content-Type':'application/json'}, body:JSON.stringify({service:svc})});
    loadStatus();
}
async function showLogs(svc) {
    const el = document.getElementById('logs');
    el.textContent = 'Loading...';
    const r = await fetch('/api/logs/' + svc);
    const d = await r.json();
    el.textContent = d.stdout || d.stderr || 'No output';
}

// Test
async function runTestSuite() {
    const el = document.getElementById('test-output');
    el.textContent = 'Running tests (this may take a while)...';
    const r = await fetch('/api/test', {method:'POST'});
    const d = await r.json();
    el.textContent = d.stdout || d.stderr || 'Complete';
}
async function seedDemo(force) {
    const el = document.getElementById('seed-output');
    el.textContent = 'Seeding...';
    const r = await fetch('/api/seed-demo', {method:'POST', headers:{'Content-Type':'application/json'}, body:JSON.stringify({force})});
    const d = await r.json();
    el.textContent = JSON.stringify(d, null, 2);
}

// Email & Magic Link
async function sendTestEmail() {
    const el = document.getElementById('email-output');
    el.textContent = 'Sending test email...';
    const r = await fetch('/api/send-email', {method:'POST'});
    const d = await r.json();
    el.textContent = JSON.stringify(d, null, 2);
}
async function traceMagicLink() {
    const el = document.getElementById('magic-link-output');
    el.innerHTML = '<div style="color:#f59e0b">Tracing magic link flow...</div>';
    const r = await fetch('/api/magic-link-flow', {method:'POST'});
    const d = await r.json();
    if (d.steps) {
        el.innerHTML = d.steps.map(s => {
            const cls = s.result === 'OK' ? 'step-ok' : 'step-fail';
            return '<div class="step ' + cls + '"><strong>' + s.step + '</strong><br><span style="color:#94a3b8;font-size:.75rem">' + JSON.stringify(s.result) + '</span></div>';
        }).join('');
    } else {
        el.innerHTML = '<div class="step step-fail">' + JSON.stringify(d) + '</div>';
    }
}

// Rate Limit
async function bruteForceTest() {
    const el = document.getElementById('rate-output');
    const email = document.getElementById('rate-email').value;
    const count = parseInt(document.getElementById('rate-count').value);
    el.textContent = 'Brute forcing ' + email + ' (' + count + ' attempts)...\\n';
    const r = await fetch('/api/rate-limit-test', {method:'POST', headers:{'Content-Type':'application/json'}, body:JSON.stringify({email, count})});
    const d = await r.json();
    let html = 'Attempts: ' + d.totalSent + '/' + d.totalAttempts + '\\n';
    html += 'Limited after attempt #' + d.limitedAfter + '\\n';
    html += 'Elapsed: ' + d.elapsedMs + 'ms\\n\\n';
    html += 'Results:\\n';
    d.results.forEach(r => {
        const icon = r.limited ? '🛑' : (r.status < 400 ? '✅' : '❌');
        html += '  ' + icon + ' #' + r.attempt + ' -> HTTP ' + r.status + ' (' + r.time + 'ms)\\n';
    });
    if (d.limitedAfter) {
        html += '\\n⚠ Rate limit triggered! Retry-After header will be sent.';
    } else {
        html += '\\n✓ No rate limit triggered (all ' + d.totalAttempts + ' attempts passed).';
    }
    el.textContent = html;
}

// Database
async function showTableCoverage() {
    const el = document.getElementById('table-coverage');
    const db = document.getElementById('db-tables');
    el.textContent = 'Loading...';
    db.textContent = 'Loading...';
    const r = await fetch('/api/table-coverage');
    const d = await r.json();
    el.textContent = d.stdout || d.stderr || 'No data';
    db.textContent = d.stdout || d.stderr || 'No data';
}
async function runCustomQuery() {
    const el = document.getElementById('custom-query-output');
    const query = document.getElementById('custom-query').value;
    el.textContent = 'Running...';
    const r = await fetch('/api/run-query', {method:'POST', headers:{'Content-Type':'application/json'}, body:JSON.stringify({query})});
    const d = await r.json();
    el.textContent = d.stdout || d.stderr || 'No result';
}

// Auto-refresh
loadStatus();
setInterval(loadStatus, 10000);
</script>
</body>
</html>
`;