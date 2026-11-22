<p align="center">
  <img src="Interface/public/pygmalion-banner.svg" alt="Pygmalion — AST-to-Data Synthesis Engine" width="430" />
</p>

<h1 align="center">Pygmalion</h1>

<p align="center">
  <a href="https://pygmalion.mohammedbourass.me"><strong>Try Pygmalion live →</strong></a>
</p>

<p align="center">
  No signup required. Turn class and struct definitions into realistic, nested JSON and a reusable GET endpoint.
</p>

<p align="center">
  <img alt="Languages" src="https://img.shields.io/badge/languages-8-F59E0B" />
  <img alt=".NET 10" src="https://img.shields.io/badge/.NET-10-512BD4" />
  <img alt="React and TypeScript" src="https://img.shields.io/badge/UI-React%20%2B%20TypeScript-3178C6" />
  <img alt="PostgreSQL" src="https://img.shields.io/badge/database-PostgreSQL-4169E1" />
</p>

## 30-second demo

> **Demo recording coming soon.** Add a ~30-second GIF or clip at `docs/demo.gif` showing: paste `Person` and `Address` classes → click **Generate data** → show the saved GET URL and generated JSON.

## What is Pygmalion?

Pygmalion is a free, open-source AST-to-Data synthesis engine. Paste models you already have, choose how many root records and nested items to create, and Pygmalion parses the definitions with Tree-sitter and generates realistic data with Bogus.

Each successful generation is saved as `{Guid}.json` and exposed through a reusable REST `GET` endpoint. Call the URL from your frontend, mobile app, API client, or demo; repeated requests return the same saved dataset. Identical language, source code, and count settings reuse the same generated file.

## Supported languages

Java · C# · Python · C++ · Go · Dart · TypeScript · JavaScript

## Why Pygmalion?

Waiting for an unfinished backend or hand-writing mock JSON slows down frontend and mobile development. Pygmalion turns the models you already wrote into realistic API data, so you can prototype screens, test nested layouts, and demo flows before your production endpoints are ready.

### Use it to

- Prototype React, Flutter, and Next.js experiences against nested responses.
- Stress-test lists, pagination, and rendering with larger datasets.
- Populate product catalogs, profiles, and dashboards with realistic values and image URLs.
- Explore class relationships and generated JSON across eight languages.

## How it works

1. Select the source language.
2. Paste all related classes or structs, including referenced nested types.
3. Set root and nested counts, then generate, inspect, and download the JSON.

## Run locally with Docker

Requirements: Docker Engine with the Compose plugin.

```sh
cp .env.example .env
```

Set a unique `POSTGRES_PASSWORD` in `.env`, then run the production checks and build:

```sh
make prod
make up
```

- UI: <http://localhost:3000>
- API: <http://127.0.0.1:5050>

Generated files and Postgres data are stored in persistent Docker volumes. To stop the stack:

```sh
make down
```

See [`Infrastructure/README.md`](Infrastructure/README.md) for the Nginx host setup and continuous deployment configuration.

## Example endpoint

Generation returns a saved file URL, for example:

```text
https://pygmalion.mohammedbourass.me/api/v1/Pygmalion/GUID
```

Request the saved JSON with a normal `GET`:

```sh
curl https://pygmalion.mohammedbourass.me/api/v1/Pygmalion/GUID
```

## Stack

- **Parsing and generation:** ASP.NET Core 10, Tree-sitter, Bogus
- **Persistence:** EF Core, PostgreSQL, SHA-256 input deduplication
- **Interface:** React, TypeScript, Vite
- **Operations:** Docker Compose, Nginx, GitHub Actions

## Roadmap

- **Available:** Generated GET endpoints, nested class resolution, configurable record counts, and `{Guid}.json` downloads.
- **Coming next:** Stateful CRUD endpoints (`POST`, `PUT`, `PATCH`, and `DELETE`).

## Contributing

Issues and pull requests are welcome. The CI workflow runs backend tests and builds the UI and containers; pushes to `main` can deploy to the configured host.

## Support

If Pygmalion saves you time, consider [sponsoring the project](https://github.com/sponsors/mohammedbourass-youssef).
