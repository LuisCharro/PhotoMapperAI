# Commits & Pull Requests

## Commit Format

Use [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <description>

[optional body]
```

### Types
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation only
- `style`: Formatting, no code change
- `refactor`: Code change that neither fixes nor adds
- `test`: Adding/updating tests
- `chore`: Build, tools, dependencies
- `perf`: Performance improvement
- `ci`: CI/CD changes

### Examples
```bash
# Feature
git commit -m "feat(map): add confidence threshold option"

# Bug fix
git commit -m "fix(face-detection): handle empty images"

# Refactor
git commit -m "refactor(services): extract AI matching to separate class"

# Documentation
git commit -m "docs(readme): add usage examples for map command"
```

## Rules

### Subject Line
- Use imperative mood ("add" not "added")
- Keep under 72 characters
- Don't end with period

### Body
- Wrap at 100 characters
- Explain "what" and "why", not "how"
- Reference issues: `Closes #123`

### Scope
Common scopes for PhotoMapperAI:
- `map` — MapCommand
- `generate` — GeneratePhotosCommand
- `benchmark` — BenchmarkCommand
- `face-detection` — Face detection services
- `ai` — AI/LLM services
- `cli` — CLI framework
- `tests` — Test files

## Branch Strategy

### Branch Types
- `main` — Production-ready code
- `development` — Integration branch
- `feature/*` — New features
- `fix/*` — Bug fixes
- `refactor/*` — Code improvements

### Workflow
```bash
# Create feature branch
git checkout -b feature/new-mapping-algorithm

# Work and commit
git add .
git commit -m "feat(map): implement fuzzy name matching"

# Push and create PR
git push -u origin feature/new-mapping-algorithm
```

## Pull Requests

### PR Title
Same format as commits:
```
feat(map): add confidence threshold option
```

### PR Description
```markdown
## Summary
Add configurable confidence threshold for player name matching.

## Changes
- Add `--confidence-threshold` option to map command
- Validate threshold value (0.0-1.0)
- Update CSV output to include confidence scores

## Testing
- [x] Unit tests pass
- [x] Tested with 100+ player dataset
- [x] Validation script passes

## Screenshots
(if applicable)
```

### PR Checklist
- [ ] Tests pass
- [ ] Build succeeds
- [ ] Code follows style guide
- [ ] Documentation updated
- [ ] No merge conflicts

## Code Review

### For Reviewers
- Review within 24 hours
- Focus on logic, not style (let linter handle)
- Suggest improvements, don't demand
- Approve if changes are reasonable

### For Authors
- Keep PRs small and focused
- Respond to feedback promptly
- Don't take feedback personally
- Ask for clarification if unclear
