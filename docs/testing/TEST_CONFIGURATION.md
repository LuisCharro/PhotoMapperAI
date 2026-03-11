# Test Configuration Guide

## Overview

Local validation data should stay outside the repository. The checked-in template is [`../../test-config.template.json`](../../test-config.template.json).

Use your own copy, for example:

- `test-config.local.json`
- `test-config.dev.json`

## Template Scope

The template currently describes:

- local test data root
- extraction fixtures
- name-match fixtures
- face-recognition fixtures
- portrait-extraction fixtures
- benchmark output location
- output profile aliases

It is a documentation/config convention for local workflows, not a first-class CLI option consumed directly by `extract`, `map`, or `generatephotos`.

## Recommended Setup

1. Copy the template.
2. Change the root path to a location outside the repo.
3. Keep real data, connection strings, and non-anonymized images out of git.

Example:

```bash
Copy-Item test-config.template.json test-config.local.json
```

## Suggested Local Layout

```text
<root>
  DataExtraction/
  NameMatch/
  FaceRecognition/
  PortraitExtraction/
  benchmark-results/
```

The folder names in the template are examples; adjust them to match your own layout.

## What To Commit

Safe to commit:

- templates
- synthetic SQL queries
- anonymized fixtures already in `tests/Data`
- docs that describe local setup

Do not commit:

- real player photos
- personal database connection strings
- non-anonymized customer/team data
- machine-specific local config copies

## Related Files

- [`../../test-config.template.json`](../../test-config.template.json)
- [`ANONYMIZED_VALIDATION.md`](ANONYMIZED_VALIDATION.md)
- [`TESTING_STRATEGY.md`](TESTING_STRATEGY.md)
