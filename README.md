# 🤖 DevOps-Agent - Autonomous AI Remediation Agent

> An autonomous, LLM-powered DevOps agent that investigates infrastructure alerts and remediates Docker containers using natural-language reasoning and real tool calls.

---

## 🛠️ Tech Stack

| Component | Technology | Description |
| :--- | :--- | :--- |
| **API** | ![.NET](https://img.shields.io/badge/.NET%2010-512BD4?style=flat&logo=dotnet&logoColor=white) **ASP.NET Core** | REST API exposing the alert trigger endpoint |
| **AI Orchestration** | ![Semantic Kernel](https://img.shields.io/badge/Semantic%20Kernel-0078D4?style=flat&logo=microsoft&logoColor=white) | Function-calling agent runtime that lets the LLM invoke tools |
| **LLM** | ![Gemini](https://img.shields.io/badge/Google%20Gemini-8E75B2?style=flat&logo=googlegemini&logoColor=white) **gemini-2.5-flash** | Reasoning engine that decides which tools to call |
| **Infrastructure Tooling** | ![Docker](https://img.shields.io/badge/Docker-2496ED?style=flat&logo=docker&logoColor=white) **Docker.DotNet** | Programmatic access to the Docker daemon |
| **API Docs** | ![Scalar](https://img.shields.io/badge/Scalar-1A1A1A?style=flat&logo=readme&logoColor=white) **OpenAPI** | Interactive API reference UI |

---

## 🏗️ Architecture

DevOps-Agent is a single ASP.NET Core service. When an alert arrives, it hands the incident to a Semantic Kernel agent backed by Gemini. The LLM autonomously decides which Docker tools to invoke — inspecting status, reading logs, and (within guardrails) restarting containers — then returns a natural-language diagnosis.

### Remediation Pipeline

```
Alert (ContainerName + ErrorMessage)
    │  POST /api/agent/trigger
    ▼
AgentController (builds ChatHistory + persona)
    │  FunctionChoiceBehavior.Auto()
    ▼
Semantic Kernel + Gemini  ◄─────────┐
    │  model picks a tool           │ (loops until diagnosed)
    ▼                               │
DockerOperationsPlugin ────────────►┘
    │  get_container_status
    │  get_container_logs
    │  restart_container (allowlist-guarded)
    ▼
Docker Daemon (npipe on Windows / unix socket on Linux)
    │
    ▼
AgentResponse (natural-language diagnosis + actions taken)
```

### Available Tools (`DockerOperationsPlugin`)

| Tool | Purpose |
| :--- | :--- |
| `get_container_status` | Reports running state, exit code, and error for a container |
| `get_container_logs` | Fetches recent stdout/stderr (default 50 lines, max 200) |
| `restart_container` | Restarts a container — **default-deny**, only names in the allowlist are permitted |

---

## 🚀 Getting Started

### Prerequisites
* [.NET 10 SDK](https://dotnet.microsoft.com/download)
* Docker (the daemon must be reachable — the agent talks to it directly)
* A [Google Gemini API key](https://aistudio.google.com/app/apikey)
* Git

### Configuration

The Gemini API key is a **secret** and is never committed. The non-secret model id lives in `appsettings.json`:

```json
"AI": {
  "ModelID": "gemini-2.5-flash"
},
"Docker": {
  "RestartAllowlist": [ "web-app", "worker" ]
}
```

The `RestartAllowlist` is a safety guardrail — the agent may only restart containers whose exact names appear here. Anything else is refused, even if the LLM decides a restart is warranted.

**Provide the API key** (choose one):

* **Development** — user-secrets (recommended locally):
    ```bash
    dotnet user-secrets set "AI:ApiKey" "<your-gemini-key>"
    ```
* **Production / containers** — environment variable (double underscore maps to the nested key):
    ```bash
    export AI__ApiKey="<your-gemini-key>"
    ```

### Running Locally

1. **Clone the repository**
    ```bash
    git clone https://github.com/YousefTantawy/DevOps-Agent
    cd DevOps-Agent
    ```

2. **Set your API key** (see Configuration above)

3. **Run the service**
    ```bash
    dotnet run
    ```

### Triggering the Agent

Send an alert to the trigger endpoint:

```bash
curl -X POST http://localhost:5186/api/agent/trigger \
  -H "Content-Type: application/json" \
  -d '{
        "containerName": "web-app",
        "errorMessage": "Container is returning 502 errors"
      }'
```

The agent investigates and responds with its diagnosis:

```json
{
  "agentResponse": "Container 'web-app' has exited with code 1. The logs show an unhandled database connection error. I restarted the container as a remediation step; it is now running."
}
```

### Access Points

| Service | URL |
| :--- | :--- |
| **API (Scalar UI)** | `http://localhost:5186/scalar/v1` |
| **OpenAPI spec** | `http://localhost:5186/openapi/v1.json` |

---

## ⚡ Key Features

* **Autonomous Remediation:** The LLM reasons over the alert and decides which tools to call — no hardcoded runbook.
* **Real Tool Calls:** Semantic Kernel `[KernelFunction]`s give the agent genuine access to the Docker daemon, not just text generation.
* **Safety Guardrail:** `restart_container` is default-deny — only explicitly allowlisted containers can be restarted, so the agent can never take a destructive action outside its sanctioned scope.
* **Cross-Platform Docker Access:** Automatically uses the Windows named pipe or the Linux/macOS Unix socket.
* **Secret Hygiene:** The Gemini API key is loaded from user-secrets or environment variables and never lives in source control.

---

## 🔧 Common Issues

* **`Cannot connect to the Docker daemon`:** The daemon isn't running or the socket isn't reachable. Start Docker and confirm `docker ps` works. When containerized, mount the socket with `-v /var/run/docker.sock:/var/run/docker.sock` (⚠️ this grants near-root access to the host — restrict who can trigger the agent).
* **`401 / API key not valid`:** The Gemini key is missing or wrong. Re-set it via user-secrets (dev) or the `AI__ApiKey` env var (prod). Note: user-secrets only load in the Development environment.
* **`Restarting 'X' is not permitted`:** Expected — `X` isn't in the `RestartAllowlist`. Add the exact container name to the `Docker:RestartAllowlist` array in `appsettings.json`.
* **`Container 'X' not found`:** The container name must match exactly (case-sensitive). Check `docker ps -a`.

---

## 👥 Contributors
* **Yousef Tantawy** — Agent architecture, .NET & DevOps

---

## ⚙️ Future Updates
* **Multi-step reasoning trace** — persist assistant replies to `ChatHistory` so the full investigation is inspectable
* **Auth on `/trigger`** — API key header or HMAC webhook signature
* **Prompt-injection hardening** — delimit untrusted `ErrorMessage` input inside the prompt
* **Dry-run / approval mode + audit log** for `restart_container`
* **Structured logging** — Semantic Kernel `IFunctionInvocationFilter` to audit every tool call, its args, and result
* **Timeouts & cancellation** — flow request `CancellationToken` into Docker and LLM calls
* **`/health` endpoint** — ping the Docker socket
* **Integration tests** — mock `IDockerClient`
* **New capability plugins** — Kubernetes, metrics (Prometheus/Grafana), notifications (Slack/Discord/Teams), Git/GitHub incident correlation, and a runbook RAG store
