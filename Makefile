.DEFAULT_GOAL := help

ifneq (,$(wildcard .env))
include .env
export
endif

COMPOSE = docker compose --env-file .env -f Infrastructure/docker-compose.yml

.PHONY: help check-env test frontend-build build docker-build up down prod nginx-check

help:
	@echo "Pygmalion targets: test, frontend-build, build, docker-build, up, down, prod, nginx-check"

check-env:
	@test -f .env || (echo "Missing root .env. Copy .env.example to .env and configure it." && exit 1)
	@test "$$POSTGRES_PASSWORD" != "CHANGE_ME_BEFORE_DEPLOYMENT" || (echo "Set a unique POSTGRES_PASSWORD in root .env before production." && exit 1)
	@test -n "$$POSTGRES_PASSWORD" || (echo "POSTGRES_PASSWORD is required in root .env." && exit 1)
	@test $${#POSTGRES_PASSWORD} -ge 24 || (echo "POSTGRES_PASSWORD must contain at least 24 characters." && exit 1)

test:
	docker build --file Infrastructure/docker/test.Dockerfile --tag pygmalion-tests .
	docker run --rm pygmalion-tests

frontend-build:
	docker run --rm -v "$(CURDIR):/workspace" -w /workspace/Interface \
		-e VITE_API_BASE_URL="$(VITE_API_BASE_URL)" \
		-e VITE_DONATE_URL="$(VITE_DONATE_URL)" \
		node:22-alpine sh -c "npm ci && npm run build"

build: frontend-build docker-build

docker-build: check-env
	$(COMPOSE) build

up: check-env
	$(COMPOSE) up -d --build

down:
	$(COMPOSE) down

prod: test check-env build

nginx-check:
	$(COMPOSE) exec nginx nginx -t
