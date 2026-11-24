# Pygmalion containers

Configure the single root `.env` for the frontend build, API, and services. The
local `.env` file has placeholder passwords; replace them with unique secrets
before running production targets.

```sh
cp .env.example .env
# Set SEQ_ADMIN_PASSWORD and GRAFANA_ADMIN_PASSWORD to unique secrets before production.
```

From the repository root, run the production checks/builds and container build:

```sh
make prod
```

Start the stack:

```sh
make up
```

- Container UI: http://localhost:3000 (local interface check)
- API: http://127.0.0.1:5050 (local API check)
- Production site: https://pygmalion.mohammedbourass.me (Nginx container on ports 80/443)
- Seq logs: http://127.0.0.1:5341
- Grafana: https://pygmalion.mohammedbourass.me/Grafana/ (Pygmalion · Generation Overview dashboard)
- Prometheus: http://127.0.0.1:9090
- Tempo traces: http://127.0.0.1:3200

Compose stores generated JSON files in `pygmalion-output`. The Compose API and interface ports bind to localhost;
the Nginx container is the public TLS reverse proxy for the interface and API.
Place the Cloudflare origin certificate and key on the server at the paths set by
`TLS_CERT_PATH` and `TLS_KEY_PATH` in `.env` before starting the stack.

The API sends structured logs to Seq and OpenTelemetry traces/metrics through
the Collector. Prometheus scrapes the Collector's metrics; Grafana is provisioned
with Prometheus and Tempo data sources and the generation dashboard. It charts
parse, fake-data synthesis, serialization/file-write, output
size, request volume, capped requests, and errors. Configure unique
`SEQ_ADMIN_PASSWORD` and `GRAFANA_ADMIN_PASSWORD` values in `.env`. Seq,
Prometheus, and Tempo stay localhost-only; Grafana is publicly reachable through
the Nginx `/Grafana/` route and uses its own login. Set `GRAFANA_ROOT_URL` when
serving the site from a different hostname.

Each generation writes a distinct `{Guid}.json` file to the configured output
directory. GitHub Actions runs tests and builds the UI and containers; on a
successful push to `main`, it deploys when `DEPLOY_HOST` is configured. The
deploy job syncs source without overwriting the server's `.env`, then rebuilds
and starts the Compose stack, including its Nginx TLS proxy. No host Nginx
installation is needed.

Provision Docker Compose and the Cloudflare certificate files on the host before
enabling deployment. The SSH deployment account needs Docker access. Runtime
settings are kept in the ignored root `.env` on the host.

Configure these GitHub repository **Variables**:

- `DEPLOY_HOST` — set to `84.8.219.142`; enables the deploy job.
- `DEPLOY_USER` — optional SSH account override; defaults to `ubuntu`.
- `DEPLOY_PATH` — optional checkout location; defaults to `/home/ubuntu/pygmalion`.
- `VITE_DONATE_URL` — optional sponsor-link override.

Configure these GitHub repository **Secrets**:

- `DEPLOY_SSH_KEY` — private deploy key; install its public key on the host.
- `DEPLOY_KNOWN_HOSTS` — verified `known_hosts` entry for the server.

The workflow does not need the deployment secrets; they remain on the host in
root `.env` and are never copied into GitHub Actions.
