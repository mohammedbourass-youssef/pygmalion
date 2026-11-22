# Pygmalion containers

Configure the single root `.env` for the frontend build, API, and database. The
local `.env` file has a placeholder password; replace it with a unique secret
before running production targets. If setting up from a fresh clone:

```sh
cp .env.example .env
# Set POSTGRES_PASSWORD to a unique secret before production.
```

From the repository root, run the production checks/builds and container build:

```sh
make prod
```

Start the stack:

```sh
make up
```

- Container UI: http://localhost:3000 (local reverse-proxy smoke check)
- API: http://127.0.0.1:5050
- Production site: https://pygmalion.mohammedbourass.me (after host Nginx setup)
- PostgreSQL: private to the Compose network (database `pygmalion`)

Compose stores PostgreSQL data in `pygmalion-postgres` and generated JSON files
in `pygmalion-output`. The Compose API port binds to localhost only; the supplied
host Nginx config proxies public HTTPS `/api/` traffic to it. Run `make up` to
start the containers. Install the Cloudflare origin certificate at the configured
host paths and publish the built `Interface/dist` directory at
`/var/www/pygmalion/dist`, then run `make deploy-static`.

The API creates the initial EF Core schema at startup. Each saved generation is
indexed by a SHA-256 hash of language, source code, root count, and nested count.
Repeat generation requests with identical inputs return the original GUID and
file; changing any generation input creates a distinct saved result. GitHub
Actions runs tests and builds the UI and containers; on a successful push to
`main`, it deploys when `DEPLOY_HOST` is configured. The deploy job syncs source
without overwriting the server's `.env`, starts the Compose stack, publishes the
static UI to `/var/www/pygmalion/dist`, installs the Nginx site, validates it,
and reloads Nginx.

Provision Docker Compose, Nginx, and the Cloudflare certificate files on the host
before enabling deployment. The SSH deployment account needs Docker access and
passwordless sudo for the Nginx install/reload commands. Keep `POSTGRES_PASSWORD`
and other runtime settings in the ignored root `.env` on the host.

Configure these GitHub repository **Variables**:

- `DEPLOY_HOST` — set to `84.8.219.142`; enables the deploy job.
- `DEPLOY_USER` — optional SSH account override; defaults to `ubuntu`.
- `DEPLOY_PATH` — optional checkout location; defaults to `/home/ubuntu/pygmalion`.
- `VITE_DONATE_URL` — optional sponsor-link override.

Configure these GitHub repository **Secrets**:

- `DEPLOY_SSH_KEY` — private deploy key; install its public key on the host.
- `DEPLOY_KNOWN_HOSTS` — verified `known_hosts` entry for the server.

The workflow does not need `POSTGRES_PASSWORD`; it remains on the deployment
host in root `.env` and is never copied into GitHub Actions.
