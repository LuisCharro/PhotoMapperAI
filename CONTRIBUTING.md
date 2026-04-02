# Contributing

Thanks for contributing to PhotoMapperAI.

## Development Principles

- Keep changes focused and easy to review.
- Prefer repo-relative paths in docs, scripts, and examples.
- Do not commit machine-specific setup, private dataset locations, or internal network details.
- Update documentation when behavior, commands, or outputs change.

## Local-Only Workflow Files

If you use personal agent tooling, local wrappers, or private validation data:

- keep those files untracked
- use local excludes or your global git ignore
- do not reference private home-directory paths in committed docs or scripts

Examples of things that should stay local:

- personal `AGENTS.md` or similar assistant wrappers
- private sample datasets
- machine-specific IPs, ports, or directory layouts

## Code Changes

- follow the existing project structure and naming
- keep public templates generic and reusable
- avoid introducing environment assumptions unless they are documented and portable

## Validation

Before opening a PR, verify the relevant workflow for your change:

- build or run the affected path locally
- update sample configs or docs if command usage changed
- keep generated outputs and temporary validation artifacts out of git unless they are intentional fixtures

## Documentation

When adding examples:

- use repo-relative paths like `samples/`, `photos/`, or `docs/`
- prefer placeholder names such as `sample-data/` over personal directories
- describe private or proprietary inputs generically

## Pull Requests

- explain the user-visible change
- mention any follow-up work or known limitations
- keep unrelated cleanup out of the same PR unless it is required for clarity
