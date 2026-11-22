.DEFAULT_GOAL := help

ifneq (,$(wildcard .env))
include .env
export
endif

COMPOSE = docker compose --env-file .env -f Infrastructure/docker-compose.yml

.PHONY: help check-env test frontend-build build docker-build up down prod nginx-check deploy-static

help:
	@echo "Pygmalion targets: test, frontend-build, build, docker-build, up, down, prod, nginx-check, deploy-static"

check-env:
	@test -f .env || (echo "Missing root .env. Copy .env.example to .env and configure it." && exit 1)
	@test "$$POSTGRES_PASSWORD" != "CHANGE_ME_BEFORE_DEPLOYMENT" || (echo "Set a unique POSTGRES_PASSWORD in root .env before production." && exit 1)
	@test -n "$$POSTGRES_PASSWORD" || (echo "POSTGRES_PASSWORD is required in root .env." && exit 1)
	@test $${#POSTGRES_PASSWORD} -ge 24 || (echo "POSTGRES_PASSWORD must contain at least 24 characters." && exit 1)

test:
	dotnet test Backend-Api/Backend-Api.Tests/Backend-Api.Tests.csproj --configuration Release

frontend-build:
	npm --prefix Interface ci
	npm --prefix Interface run build

build: frontend-build
	dotnet publish Backend-Api/Backend-Api.csproj --configuration Release --output Backend-Api/publish

docker-build: check-env
	$(COMPOSE) build

up: check-env
	$(COMPOSE) up -d --build

down:
	$(COMPOSE) down

prod: test check-env build docker-build

nginx-check:
	sudo nginx -t

deploy-static: check-env frontend-build
	sudo mkdir -p /var/www/pygmalion/dist
	sudo rsync -a --delete Interface/dist/ /var/www/pygmalion/dist/
	sudo install -m 0644 Infrastructure/nginx/pygmalion.conf /etc/nginx/sites-available/pygmalion
	sudo ln -sfn /etc/nginx/sites-available/pygmalion /etc/nginx/sites-enabled/pygmalion
	sudo nginx -t
	sudo systemctl reload nginx
