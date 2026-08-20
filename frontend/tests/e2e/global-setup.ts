import { execFileSync } from "node:child_process";
import path from "node:path";

export default function globalSetup() {
  if (!process.env.E2E_TENANT_ADMIN_EMAIL) {
    return;
  }

  const repositoryRoot = path.resolve(__dirname, "../../..");

  execFileSync(
    "docker",
    [
      "compose",
      "run",
      "--rm",
      "-e",
      "E2E_FIXTURES_ENABLED=true",
      "worker",
      "--generate-next-e2e-invoices",
    ],
    { cwd: repositoryRoot, stdio: "inherit" }
  );
}
