.PHONY: parity-status parity-report parity-comment parity-merge parity-merge-apply parity-watch parity-validate

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
