.PHONY: parity-status parity-report parity-comment parity-merge parity-merge-apply parity-watch parity-validate publish-all publish-mac publish-win publish-linux publish-cli-mac publish-cli-win publish-cli-linux

# Publish targets for distributing the application to users
# These create self-contained, single-file executables

publish-all: publish-mac publish-win publish-linux

# Publish UI app for macOS (Apple Silicon)
publish-mac:
	@echo "Publishing PhotoMapperAI UI for macOS (arm64)..."
	dotnet publish src/PhotoMapperAI.UI/PhotoMapperAI.UI.csproj \
		-c Release \
		-r osx-arm64 \
		--self-contained true \
		-o ./publish/PhotoMapperAI-macOS
	@echo "Published to ./publish/PhotoMapperAI-macOS"

# Publish UI app for Windows
publish-win:
	@echo "Publishing PhotoMapperAI UI for Windows (x64)..."
	dotnet publish src/PhotoMapperAI.UI/PhotoMapperAI.UI.csproj \
		-c Release \
		-r win-x64 \
		--self-contained true \
		-o ./publish/PhotoMapperAI-Windows
	@echo "Published to ./publish/PhotoMapperAI-Windows"

# Publish UI app for Linux
publish-linux:
	@echo "Publishing PhotoMapperAI UI for Linux (x64)..."
	dotnet publish src/PhotoMapperAI.UI/PhotoMapperAI.UI.csproj \
		-c Release \
		-r linux-x64 \
		--self-contained true \
		-o ./publish/PhotoMapperAI-Linux
	@echo "Published to ./publish/PhotoMapperAI-Linux"

# Publish CLI tool for macOS
publish-cli-mac:
	@echo "Publishing PhotoMapperAI CLI for macOS (arm64)..."
	dotnet publish src/PhotoMapperAI/PhotoMapperAI.csproj \
		-c Release \
		-r osx-arm64 \
		--self-contained true \
		-o ./publish/PhotoMapperAI-CLI-macOS
	@echo "Published to ./publish/PhotoMapperAI-CLI-macOS"

# Publish CLI tool for Windows
publish-cli-win:
	@echo "Publishing PhotoMapperAI CLI for Windows (x64)..."
	dotnet publish src/PhotoMapperAI/PhotoMapperAI.csproj \
		-c Release \
		-r win-x64 \
		--self-contained true \
		-o ./publish/PhotoMapperAI-CLI-Windows
	@echo "Published to ./publish/PhotoMapperAI-CLI-Windows"

# Publish CLI tool for Linux
publish-cli-linux:
	@echo "Publishing PhotoMapperAI CLI for Linux (x64)..."
	dotnet publish src/PhotoMapperAI/PhotoMapperAI.csproj \
		-c Release \
		-r linux-x64 \
		--self-contained true \
		-o ./publish/PhotoMapperAI-CLI-Linux
	@echo "Published to ./publish/PhotoMapperAI-CLI-Linux"

parity-status:
	./scripts/ops/parity_pipeline.sh status

parity-report:
	./scripts/ops/parity_pipeline.sh report

parity-comment:
	./scripts/ops/parity_pipeline.sh comment

parity-merge:
	./scripts/ops/parity_pipeline.sh merge

parity-merge-apply:
	./scripts/ops/parity_pipeline.sh merge --apply

parity-watch:
	./scripts/ops/parity_pipeline.sh watch

parity-validate:
	./scripts/ops/validate_ops_scripts.sh
