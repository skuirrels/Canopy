SHELL := /bin/bash
.DEFAULT_GOAL := help

.PHONY: help dev prod

help:
	@printf 'make dev   Run the API and Vite with hot reload\nmake prod  Build and serve the Release application\n'

dev:
	@node scripts/dev.mjs

prod:
	@node scripts/dev.mjs --check
	@ASPNETCORE_ENVIRONMENT=Production ./scripts/start.sh Release
